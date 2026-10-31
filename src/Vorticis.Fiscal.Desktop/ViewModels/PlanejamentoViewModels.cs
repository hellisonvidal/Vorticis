using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Planejamento;

namespace Vorticis.Fiscal.Desktop.ViewModels;

public partial class ReceitaPlanejamentoLinha(TipoReceitaInfo info) : ObservableObject
{
    public TipoReceitaPlanejamento Tipo { get; } = info.Tipo;
    public string Descricao { get; } = info.Descricao;
    public string Tributacao { get; } = info.Servico ? "ISS" : "ICMS";
    [ObservableProperty] private string _valorTexto = "";
}

/// <summary>
/// Apuração › Planejamento tributário: receitas do ano por tipo, folha, custos e percentuais; compara Simples Nacional, Lucro Presumido
/// e Lucro Real e aponta o de menor carga. As simulações ficam gravadas por empresa (descrição e ano) e saem em Relatórios › Planejamento tributário.
/// </summary>
public partial class PlanejamentoTributarioViewModel(IContexto contexto) : RelatorioFiscalViewModel(contexto)
{
    public ObservableCollection<ReceitaPlanejamentoLinha> Receitas { get; } = [.. PlanejamentoTributario.Tipos.Select(t => new ReceitaPlanejamentoLinha(t))];
    public ObservableCollection<SimulacaoPlanejamento> Simulacoes { get; } = [];

    [ObservableProperty] private int _ano = DateTime.Today.Year + 1;
    [ObservableProperty] private string _descricao = "Cenário base";
    [ObservableProperty] private string _folhaTexto = "";
    [ObservableProperty] private string _encargosTexto = "";
    [ObservableProperty] private string _custosTexto = "";
    [ObservableProperty] private string _comprasTexto = "";
    [ObservableProperty] private string _icmsTexto = "";
    [ObservableProperty] private string _issTexto = "";
    [ObservableProperty] private string _conclusao = "";
    [ObservableProperty] private SimulacaoPlanejamento? _simulacao;
    private string _calculada = "";

    public bool PodeGravar => Contexto.Pode(Permissao.FiscalApurar);

    protected override string TituloRelatorio => TabelasPlanejamento.Titulo;
    protected override string Parametros => _calculada;
    protected override string NomeArquivo => $"planejamento_tributario_{Ano}";
    protected override bool Paisagem => false;

    private static decimal V(string t) => Numeros.LerValor(t) ?? 0m;

    public EntradaPlanejamento Entrada() => new(Ano, [.. Receitas.Select(r => new ReceitaPlanejamento(r.Tipo, V(r.ValorTexto))).Where(r => r.Valor != 0)],
        V(FolhaTexto), V(EncargosTexto), V(CustosTexto), V(ComprasTexto), V(IcmsTexto), V(IssTexto));

    public void Preencher(SimulacaoPlanejamento s)
    {
        (Ano, Descricao) = (s.Ano, s.Descricao);
        foreach (var r in Receitas) r.ValorTexto = Numeros.MoedaOuVazio(s.Dados.Receitas.Where(x => x.Tipo == r.Tipo).Sum(x => x.Valor));
        (FolhaTexto, EncargosTexto, CustosTexto) = (Numeros.Moeda(s.Dados.Folha), s.Dados.EncargosPercentual.ToString("0.##", Formato.PtBr), Numeros.Moeda(s.Dados.CustosDespesas));
        (ComprasTexto, IcmsTexto, IssTexto) = (Numeros.Moeda(s.Dados.ComprasComCredito), s.Dados.AliquotaIcms.ToString("0.##", Formato.PtBr), s.Dados.AliquotaIss.ToString("0.##", Formato.PtBr));
    }

    public override void AtualizarPermissoes()
    {
        base.AtualizarPermissoes();
        OnPropertyChanged(nameof(PodeGravar));
    }

    public Task CarregarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (string.IsNullOrWhiteSpace(EncargosTexto)) EncargosTexto = (await app.Fiscal().Planejamento.EncargoPadraoAsync(Ano)).ToString("0.##", Formato.PtBr);
        await RecarregarSimulacoesAsync(app, empresa, usuario);
    });

    private async Task RecarregarSimulacoesAsync(VorticisApp app, long empresa, long usuario)
    {
        var r = await app.Fiscal().Planejamento.ListarAsync(empresa, usuario);
        Simulacoes.Clear();
        foreach (var s in r.Valor ?? []) Simulacoes.Add(s);
    }

    partial void OnSimulacaoChanged(SimulacaoPlanejamento? value)
    {
        if (value is null) return;
        Preencher(value);
        _ = CompararAsync();
    }

    [RelayCommand]
    private Task CompararAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Fiscal().Planejamento.CompararAsync(empresa, Entrada(), usuario);
        if (!r.Sucesso) { Limpar(); Conclusao = ""; MostrarResultado(r, ""); return; }
        _calculada = $"Ano {Ano} · {Descricao}";
        Mostrar(TabelasPlanejamento.Tabelas(r.Valor!, Descricao));
        Conclusao = r.Valor!.Melhor is { } m
            ? $"Menor carga: {m.Nome} ({Formato.Moeda(m.Total)}, {r.Valor.Percentual(m):0.00}% da receita)" + (r.Valor.Economia > 0 ? $" · economia de {Formato.Moeda(r.Valor.Economia)} no ano." : ".")
            : "Nenhum regime permitido com estes dados.";
    });

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Fiscal().Planejamento.GravarAsync(empresa, Descricao, Entrada(), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await RecarregarSimulacoesAsync(app, empresa, usuario);
        Mensagem = $"Simulação \"{Descricao.Trim()}\" de {Ano} gravada. Ela aparece também em Relatórios › Planejamento tributário.";
    }, Permissao.FiscalApurar);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Simulacao is not { } s) { Erros.Add("Escolha a simulação gravada a excluir."); return; }
        if (!await Contexto.ConfirmarAsync("Excluir simulação", $"Excluir a simulação \"{s.Descricao}\" de {s.Ano}?", "Excluir", "Cancelar")) return;
        var r = await app.Fiscal().Planejamento.ExcluirAsync(empresa, s.Id, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Simulacao = null;
        await RecarregarSimulacoesAsync(app, empresa, usuario);
        Mensagem = "Simulação excluída.";
    }, Permissao.FiscalApurar);

    /// <summary>Traz a receita e a folha do ano anterior do Histórico de receita e folha (a receita vai para o tipo já preenchido, ou Comércio).</summary>
    [RelayCommand]
    private Task TrazerHistoricoAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Fiscal().Planejamento.MovimentoAsync(empresa, Ano - 1, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        if (r.Valor!.Meses == 0) { Erros.Add($"Não há receita de {Ano - 1} em Apuração › Histórico de receita e folha."); return; }
        var destino = Receitas.FirstOrDefault(x => V(x.ValorTexto) != 0) ?? Receitas[0];
        foreach (var x in Receitas) x.ValorTexto = "";
        destino.ValorTexto = Numeros.Moeda(r.Valor.Receita);
        FolhaTexto = Numeros.Moeda(r.Valor.Folha);
        Mensagem = $"Receita de {Ano - 1} ({r.Valor.Meses} meses) em \"{destino.Descricao}\" e folha do ano. Separe a receita por tipo, se houver mais de um, e clique em Comparar.";
    });
}

/// <summary>Relatórios › Planejamento tributário: o comparativo de uma simulação gravada, para imprimir ou exportar.</summary>
public partial class RelPlanejamentoViewModel(IContexto contexto) : RelatorioFiscalViewModel(contexto)
{
    public ObservableCollection<SimulacaoPlanejamento> Simulacoes { get; } = [];
    [ObservableProperty] private SimulacaoPlanejamento? _simulacao;

    protected override string TituloRelatorio => TabelasPlanejamento.Titulo;
    protected override string Parametros => Simulacao is { } s ? $"Ano {s.Ano} · {s.Descricao}" : "";
    protected override string NomeArquivo => $"planejamento_tributario_{Simulacao?.Ano}";
    protected override bool Paisagem => false;

    public Task CarregarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Fiscal().Planejamento.ListarAsync(empresa, usuario);
        Simulacoes.Clear();
        foreach (var s in r.Valor ?? []) Simulacoes.Add(s);
        if (r.Valor is { Count: 0 }) Mensagem = "Nenhuma simulação gravada: monte e grave uma em Apuração › Planejamento tributário.";
        Simulacao ??= Simulacoes.FirstOrDefault();
    });

    partial void OnSimulacaoChanged(SimulacaoPlanejamento? value) => _ = GerarAsync();

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Simulacao is not { } s) { Limpar(); return; }
        var r = await app.Fiscal().Planejamento.CompararAsync(empresa, s.Dados, usuario);
        if (!r.Sucesso) { Limpar(); MostrarResultado(r, ""); return; }
        Mostrar(TabelasPlanejamento.Tabelas(r.Valor!, s.Descricao));
    });
}
