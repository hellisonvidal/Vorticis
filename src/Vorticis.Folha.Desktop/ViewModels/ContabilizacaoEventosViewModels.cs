using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Folha.Servicos;

namespace Vorticis.Folha.Desktop.ViewModels;

public sealed partial class EventoPreviaLinha(EventoPrevia e) : ObservableObject
{
    public EventoPrevia E { get; } = e;
    [ObservableProperty] private bool _marcado = e.Pronto;
    public string Codigo => E.Codigo;
    public string Descricao => E.Descricao;
    public string Historico => E.Historico;
    public string Valor => Formato.Moeda(E.Valor);
    public string Contas => E.Contas;
    public string Centros => E.Centros;
    public string Situacao => E.Situacao;
}

/// <summary>
/// Cálculos › Folha › Contabilização por eventos: escolhe a competência e o tipo de cálculo, mostra a prévia dos eventos (valor, contas, centro e situação),
/// contabiliza os marcados (um lançamento por evento) e desfaz (estorno).
/// </summary>
public partial class ContabilizacaoEventosViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public static IReadOnlyList<Opcao> Tipos { get; } = [.. ServicoContabilizacaoFolha.TiposCalculo.Select(t => new Opcao(t.Codigo, t.Nome))];

    public ObservableCollection<EventoPreviaLinha> Eventos { get; } = [];
    public ObservableCollection<string> Lancamento { get; } = [];

    [ObservableProperty] private DateTime? _competencia = FolhaExtensoes.Mes(contexto.PeriodoTrabalho);
    [ObservableProperty] private Opcao? _tipo = Tipos[0];
    [ObservableProperty] private EventoPreviaLinha? _selecionado;
    [ObservableProperty] private string _modo = "";
    [ObservableProperty] private string _total = "";

    public bool PodeContabilizar => Contexto.Pode(Permissao.FolhaContabilizar);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeContabilizar));

    private DateOnly Comp => FolhaExtensoes.InicioMes(Competencia);
    private string TipoCalc => Tipo?.Codigo ?? ServicoContabilizacaoFolha.CalcMensal;

    partial void OnSelecionadoChanged(EventoPreviaLinha? value)
    {
        Lancamento.Clear();
        if (value is not null) foreach (var l in value.E.Linhas) Lancamento.Add(l);
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var g = NovaGeracao();
        var c = await app.Folha().Cadastros.ObterConfiguracaoAsync(empresa, sessao.Id);
        if (!EhAtual(g)) return;
        Modo = c.PorEventos
            ? $"Modo de contabilização: por eventos{(c.DetalharPorFuncionario ? ", detalhando por funcionário" : "")}."
            : "Modo de contabilização: resumida. Para usar esta tela, escolha \"Por eventos\" em Cadastros › Parâmetros › Folha.";
    }

    [RelayCommand]
    private Task PreviaAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Folha().Contabilizacao.PreviaEventosAsync(empresa, Comp, TipoCalc, usuario);
        Eventos.Clear();
        Lancamento.Clear();
        Total = "";
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        foreach (var e in r.Valor) Eventos.Add(new EventoPreviaLinha(e));
        Total = $"{r.Valor.Count} evento(s), total {Formato.Moeda(r.Valor.Sum(e => e.Valor))}; {r.Valor.Count(e => e.Pronto)} pronto(s), {r.Valor.Count(e => e.Contabilizado)} já contabilizado(s).";
    }, Permissao.FolhaContabilizar);

    [RelayCommand] private void MarcarTodos() { foreach (var e in Eventos) e.Marcado = e.E.Pronto; }
    [RelayCommand] private void DesmarcarTodos() { foreach (var e in Eventos) e.Marcado = false; }

    [RelayCommand]
    private Task ContabilizarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var codigos = Eventos.Where(e => e.Marcado).Select(e => e.Codigo).ToList();
        var r = await app.Folha().Contabilizacao.ContabilizarEventosAsync(empresa, Comp, TipoCalc, codigos, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await PreviaAsync();
        Mensagem = r.Valor.All(x => x.Numero == 0)
            ? $"Contabilização em lote: {r.Valor.Count} pendência(s) registrada(s). Gere os lançamentos em Integrações › Exportar › Contabilização em lote."
            : $"{r.Valor.Count} evento(s) contabilizado(s): lançamentos nº {string.Join(", ", r.Valor.Select(x => x.Numero))}.";
    }, Permissao.FolhaContabilizar);

    [RelayCommand]
    private async Task DesfazerAsync()
    {
        var codigos = Eventos.Where(e => e.Marcado && e.E.Contabilizado).Select(e => e.Codigo).ToList();
        if (codigos.Count == 0) { Erros.Clear(); Erros.Add("Marque ao menos um evento já contabilizado para desfazer."); return; }
        if (!await Contexto.ConfirmarAsync("Desfazer contabilização", $"Estornar o lançamento de {codigos.Count} evento(s)? Será gerado um lançamento de estorno para cada um.", "Desfazer", "Cancelar")) return;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            var r = await app.Folha().Contabilizacao.DesfazerEventosAsync(empresa, Comp, TipoCalc, codigos, usuario);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            await PreviaAsync();
            Mensagem = $"{r.Valor} evento(s) desfeito(s) (lançamentos estornados).";
        }, Permissao.FolhaContabilizar);
    }
}

public sealed record EventoContabilLinha(Vorticis.Folha.EventoContabil E)
{
    public string Codigo => E.Codigo;
    public string Descricao => E.Descricao;
    public string Rubricas => string.Join(", ", E.Chaves);
}

/// <summary>Cálculos › Folha › Eventos contábeis: agrupa rubricas (e códigos ENC_*, PROV_*) em eventos; cada evento vira um lançamento na contabilização por eventos.</summary>
public partial class EventosContabeisViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<EventoContabilLinha> Eventos { get; } = [];
    [ObservableProperty] private EventoContabilLinha? _selecionado;
    [ObservableProperty] private string _codigo = "";
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private string _rubricas = "";
    [ObservableProperty] private string _disponiveis = "";

    public bool PodeEditar => Contexto.Pode(Permissao.FolhaParametrosManter);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeEditar));

    partial void OnSelecionadoChanged(EventoContabilLinha? value)
    {
        if (value is null) return;
        (Codigo, Descricao, Rubricas) = (value.E.Codigo, value.E.Descricao, string.Join(", ", value.E.Chaves));
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var g = NovaGeracao();
        var s = app.Folha().Contabilizacao;
        var eventos = await s.ListarEventosContabeisAsync(empresa, sessao.Id);
        var regras = await s.ListarRegrasAsync(empresa, sessao.Id);
        if (!EhAtual(g)) return;
        var id = Selecionado?.E.Id;
        Eventos.Clear(); foreach (var e in eventos) Eventos.Add(new EventoContabilLinha(e));
        Selecionado = Eventos.FirstOrDefault(e => e.E.Id == id);
        Disponiveis = regras.Count == 0 ? "Cadastre antes as regras em Cálculos › Folha › Contabilização."
            : "Rubricas com regra: " + string.Join(", ", regras.Select(r => r.Chave));
    }

    [RelayCommand]
    private void Novo() { Selecionado = null; (Codigo, Descricao, Rubricas) = ("", "", ""); }

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var chaves = Rubricas.Split([',', ';', ' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var r = await app.Folha().Contabilizacao.GravarEventoContabilAsync(empresa, new Vorticis.Folha.EventoContabil(Selecionado?.E.Id ?? 0, Codigo, Descricao, chaves), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = "Evento gravado.";
    }, Permissao.FolhaParametrosManter);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionado is null) { Erros.Add("Selecione um evento."); return; }
        var r = await app.Folha().Contabilizacao.ExcluirEventoContabilAsync(empresa, Selecionado.E.Id, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Novo();
        await CarregarAsync();
        Mensagem = "Evento excluído. As rubricas dele voltam a formar eventos próprios.";
    }, Permissao.FolhaParametrosManter);
}
