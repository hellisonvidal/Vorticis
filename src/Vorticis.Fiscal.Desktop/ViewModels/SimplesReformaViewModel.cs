using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Fiscal.Comum;
using Vorticis.Fiscal.Reforma;
using Vorticis.Fiscal.Simples;

namespace Vorticis.Fiscal.Desktop.ViewModels;

/// <summary>Opção de atividade da lista da simulação.</summary>
public sealed record OpcaoAtividadeSimples(AtividadeSimples Valor, string Texto)
{
    public override string ToString() => Texto;
}

/// <summary>Apuração › Simples Nacional × IBS/CBS: imposto de hoje (DAS) comparado ao de cada ano da reforma tributária, dentro ou fora do DAS.</summary>
public partial class SimplesReformaViewModel(IContexto contexto) : RelatorioFiscalViewModel(contexto)
{
    public static IReadOnlyList<OpcaoAtividadeSimples> Atividades { get; } =
    [
        new(AtividadeSimples.ComercioRevenda, "Comércio (revenda de mercadorias) - Anexo I"),
        new(AtividadeSimples.IndustriaVenda, "Indústria (venda de produtos) - Anexo II"),
        new(AtividadeSimples.ServicosAnexoIII, "Serviços do Anexo III"),
        new(AtividadeSimples.ServicosAnexoIV, "Serviços do Anexo IV (construção, limpeza, vigilância)"),
        new(AtividadeSimples.ServicosAnexoV, "Serviços do Anexo V"),
        new(AtividadeSimples.ServicosFatorR, "Serviços intelectuais (Fator R decide entre Anexo III e V)"),
    ];

    [ObservableProperty] private string _receitaTexto = "";
    [ObservableProperty] private string _comprasTexto = "";
    [ObservableProperty] private string _folhaTexto = "";
    [ObservableProperty] private string _reducaoTexto = "0";
    [ObservableProperty] private OpcaoAtividadeSimples _atividade = Atividades[0];
    [ObservableProperty] private string _avisosTexto = "";
    [ObservableProperty] private string _explicacao = "";
    private string _parametros = "";

    protected override string TituloRelatorio => "Simples Nacional × IBS/CBS";
    protected override string Parametros => _parametros;
    protected override string NomeArquivo => "simples_x_ibs_cbs";
    protected override bool Paisagem => true;

    private static decimal V(string t) => Numeros.LerValor(t) ?? 0m;

    [RelayCommand]
    private Task TrazerHistoricoAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Fiscal().SimplesReforma.SugerirAsync(empresa, DateOnly.FromDateTime(DateTime.Today), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        (ReceitaTexto, ComprasTexto, FolhaTexto) = (Numeros.Moeda(r.Valor.Receita12), Numeros.Moeda(r.Valor.Compras12), Numeros.Moeda(r.Valor.Folha12));
        AvisosTexto = string.Join(Environment.NewLine, r.Valor.Avisos);
    });

    [RelayCommand]
    private Task SimularAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var b = new BaseSimulacao(V(ReceitaTexto), V(ComprasTexto), Atividade.Valor, V(FolhaTexto), V(ReducaoTexto));
        var r = await app.Fiscal().SimplesReforma.SimularAsync(empresa, DateOnly.FromDateTime(DateTime.Today), b, usuario);
        if (!r.Sucesso) { Limpar(); MostrarResultado(r, ""); return; }
        _parametros = $"Receita 12 meses {Numeros.Moeda(b.Receita12)} | Compras {Numeros.Moeda(b.Compras12)} | {Atividade.Texto}";
        Mostrar(r.Valor.Tabelas());
        AvisosTexto = string.Join(Environment.NewLine, r.Valor.Avisos);
        Explicacao = SimulacaoSimples.Explicacao;
    });
}
