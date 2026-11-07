using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Relatorios;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Desktop.ViewModels;

public enum RelatorioComparado { Balanco, Balancete, Dre, Dfc, Ebitda, Dmpl, Dlpa }

public sealed record OpcaoRelatorio(RelatorioComparado Valor, string Texto, bool Pontual, bool UsaNivel, bool AnaliseVertical)
{
    public override string ToString() => Texto;
}

/// <summary>
/// Relatórios › Comparativos: um só lugar para comparar períodos no Balanço, Balancete, DRE, DFC, EBITDA, DMPL e DLPA — período anterior,
/// mesmo período do ano anterior, até 12 períodos anteriores seguidos (evolução) e até 3 períodos escolhidos, com variação em R$ e %
/// e análise vertical. Usa o mesmo comparativo dos relatórios (mesmas regras e permissões).
/// </summary>
public partial class ComparativosViewModel : RelatorioViewModel
{
    public ComparativosViewModel(IContexto contexto) : base(contexto)
    {
        Comparacao.SempreAtivo = true;
        Comparacao.Ativo = true;
        Comparacao.PodeAnaliseVertical = Relatorio.AnaliseVertical;
    }

    public static IReadOnlyList<OpcaoRelatorio> Relatorios { get; } =
    [
        new(RelatorioComparado.Balanco, "Balanço Patrimonial", true, true, true),
        new(RelatorioComparado.Balancete, "Balancete de verificação", false, false, false),
        new(RelatorioComparado.Dre, "Demonstração do Resultado (DRE)", false, true, true),
        new(RelatorioComparado.Dfc, "DFC (método indireto)", false, true, false),
        new(RelatorioComparado.Ebitda, "EBITDA (LAJIDA)", false, false, false),
        new(RelatorioComparado.Dmpl, "DMPL (mutações do patrimônio líquido)", false, false, false),
        new(RelatorioComparado.Dlpa, "DLPA (lucros ou prejuízos acumulados)", false, false, false),
    ];
    public static IReadOnlyList<short> Niveis => BalancoViewModel.Niveis;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(MostraInicio), nameof(MostraNivel), nameof(MostraZeradas), nameof(RotuloData))]
    private OpcaoRelatorio _relatorio = Relatorios[2];
    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private DateTime? _fim = new DateTime(DateTime.Today.Year, 12, 31);
    [ObservableProperty] private short _nivel = 3;
    [ObservableProperty] private bool _incluirZeradas;
    [ObservableProperty] private string _resumo = "";
    private string _parametros = "";

    public bool MostraInicio => !Relatorio.Pontual;
    public bool MostraNivel => Relatorio.UsaNivel;
    public bool MostraZeradas => Relatorio.Valor == RelatorioComparado.Balancete;
    public string RotuloData => Relatorio.Pontual ? "Data" : "Período atual";

    partial void OnRelatorioChanged(OpcaoRelatorio value)
    {
        Comparacao.PodeAnaliseVertical = value.AnaliseVertical;
        TabelaComparativa = null;
    }

    protected override string TituloRelatorio => $"{Relatorio.Texto} — comparativo";
    protected override string Parametros => _parametros;
    protected override string NomeArquivo => $"comparativo_{Relatorio.Valor.ToString().ToLowerInvariant()}";
    protected override bool Paisagem => true;
    protected override bool SuportaAnaliseVertical => true;
    protected override IReadOnlyList<TabelaRelatorio> Tabelas() => TabelaComparativa is { } t ? [t] : [];

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var fim = Data(Fim, DateOnly.FromDateTime(DateTime.Today));
        var ini = Relatorio.Pontual ? fim : Data(Inicio, fim);
        if (ini > fim) { Erros.Add("A data inicial do período atual é posterior à final."); return; }
        _parametros = Relatorio.Pontual ? $"Data-base {fim:dd/MM/yyyy}" : $"Período atual de {ini:dd/MM/yyyy} a {fim:dd/MM/yyyy}";
        var c = app.Comparativos;
        var tratou = await TentarComparativoAsync(ini, fim, Relatorio.Pontual, op => Relatorio.Valor switch
        {
            RelatorioComparado.Balanco => c.BalancoAsync(empresa, fim, Nivel, op, usuario),
            RelatorioComparado.Balancete => c.BalanceteAsync(empresa, ini, fim, op, IncluirZeradas, usuario),
            RelatorioComparado.Dfc => c.DfcAsync(empresa, ini, fim, false, Nivel, op, usuario),
            RelatorioComparado.Ebitda => c.EbitdaAsync(empresa, ini, fim, op, usuario),
            RelatorioComparado.Dmpl => c.DmplAsync(empresa, ini, fim, op, usuario),
            RelatorioComparado.Dlpa => c.DlpaAsync(empresa, ini, fim, op, usuario),
            _ => c.DreAsync(empresa, ini, fim, Nivel, op, usuario),
        });
        Resumo = tratou && TabelaComparativa is not null
            ? Comparativo.Descricao(Comparativo.Periodos(ini, fim, Comparacao.Opcoes(), Relatorio.Pontual))
              + ". Variação = período atual − período comparado."
            : "";
    }, Permissao.RelatorioDemonstracoes);
}
