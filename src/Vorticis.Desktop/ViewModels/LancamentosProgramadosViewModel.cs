using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Desktop.Comum;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra.Contabil;

namespace Vorticis.Desktop.ViewModels;

/// <summary>Lançamentos › Contábeis › Lançamentos programados: cadastro dos lançamentos mensais e geração da competência.</summary>
public partial class LancamentosProgramadosViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<LancamentoProgramado> Lista { get; } = [];

    [ObservableProperty] private LancamentoProgramado? _selecionado;
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private string _contaDebito = "";
    [ObservableProperty] private string _contaCredito = "";
    [ObservableProperty] private string _valor = "";
    [ObservableProperty] private string _historico = "";
    [ObservableProperty] private decimal? _dia = 1;
    [ObservableProperty] private DateTime? _inicio = new DateTime(contexto.PeriodoTrabalho.Year, contexto.PeriodoTrabalho.Month, 1);
    [ObservableProperty] private DateTime? _fim;
    [ObservableProperty] private string _centroCusto = "";
    [ObservableProperty] private bool _ativo = true;
    [ObservableProperty] private DateTime? _competencia = new DateTime(contexto.PeriodoTrabalho.Year, contexto.PeriodoTrabalho.Month, 1);
    private long _id;

    public bool PodeLancar => Contexto.Pode(Permissao.LancamentoIncluir);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeLancar));

    private static DateOnly Mes(DateTime? d, DateTime padrao) { var x = d ?? padrao; return new DateOnly(x.Year, x.Month, 1); }

    partial void OnSelecionadoChanged(LancamentoProgramado? value)
    {
        if (value is null) return;
        _id = value.Id; Descricao = value.Descricao; ContaDebito = value.ContaDebito; ContaCredito = value.ContaCredito; Valor = Formato.Num(value.Valor);
        Historico = value.Historico; Dia = value.Dia; Inicio = value.Inicio.ToDateTime(TimeOnly.MinValue); Fim = value.Fim?.ToDateTime(TimeOnly.MinValue);
        CentroCusto = value.CentroCusto ?? ""; Ativo = value.Ativo;
    }

    [RelayCommand]
    public void Novo()
    {
        _id = 0; Selecionado = null; Descricao = ContaDebito = ContaCredito = Valor = Historico = CentroCusto = ""; Dia = 1; Fim = null; Ativo = true;
        Inicio = new DateTime(Contexto.PeriodoTrabalho.Year, Contexto.PeriodoTrabalho.Month, 1);
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var lista = await app.LancamentosProgramados.ListarAsync(empresa, sessao.Id);
        Lista.Clear(); foreach (var p in lista) Lista.Add(p);
    }

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var p = new LancamentoProgramado(_id, Descricao, ContaDebito, ContaCredito, Numeros.LerValor(Valor) ?? 0, Historico, (int)(Dia ?? 1),
                                         Mes(Inicio, DateTime.Today), Fim is null ? null : Mes(Fim, DateTime.Today), CentroCusto, Ativo);
        var r = await app.LancamentosProgramados.GravarAsync(empresa, p, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Novo();
        Mensagem = "Lançamento programado gravado.";
    }, Permissao.LancamentoIncluir);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionado is not { } p) { Erros.Add("Selecione o lançamento programado na lista."); return; }
        if (!await Contexto.ConfirmarAsync("Excluir lançamento programado", $"Excluir \"{p.Descricao}\"? Os lançamentos já gerados continuam na contabilidade.")) return;
        var r = await app.LancamentosProgramados.ExcluirAsync(empresa, p.Id, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Novo();
        Mensagem = "Lançamento programado excluído.";
    }, Permissao.LancamentoIncluir);

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var comp = Mes(Competencia, DateTime.Today);
        var r = await app.LancamentosProgramados.GerarAsync(empresa, comp, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        foreach (var a in r.Valor.Avisos) Erros.Add(a);
        await CarregarAsync();
        Mensagem = r.Valor.Gerados == 0
            ? $"Nada a gerar em {comp:MM/yyyy}: os lançamentos vigentes já foram gerados."
            : $"{r.Valor.Gerados} lançamento(s) gerado(s) em {comp:MM/yyyy}, total {Formato.Moeda(r.Valor.Total)}.";
    }, Permissao.LancamentoIncluir);
}
