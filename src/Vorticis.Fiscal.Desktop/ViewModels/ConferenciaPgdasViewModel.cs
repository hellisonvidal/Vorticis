using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Simples;

namespace Vorticis.Fiscal.Desktop.ViewModels;

/// <summary>
/// Relatórios › Livros e apuração › Conferência do PGDAS-D: receitas do mês por anexo e atividade (com ou sem substituição, retenção e exportação) e o IBS/CBS dos
/// documentos, para comparar com o que a Receita pré-preencheu no PGDAS-D.
/// </summary>
public partial class ConferenciaPgdasViewModel(IContexto contexto) : RelatorioFiscalViewModel(contexto)
{
    private DateOnly _competencia = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty] private DateTime? _competenciaData = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private int _atividadeServicos;
    [ObservableProperty] private string _avisosTexto = "";

    protected override string TituloRelatorio => "Conferência do PGDAS-D";
    protected override string Parametros => $"Competência {_competencia:MM/yyyy}";
    protected override string NomeArquivo => $"conferencia_pgdasd_{_competencia:yyyyMM}";

    [RelayCommand]
    private Task ConferirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var d = Data(CompetenciaData, _competencia);
        var comp = new DateOnly(d.Year, d.Month, 1);
        var r = await app.Fiscal().Simples.ConferirPgdasAsync(empresa, comp, ApuracaoSimplesViewModel.AtividadesServicos[Math.Max(0, AtividadeServicos)], usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); Limpar(); AvisosTexto = ""; return; }
        _competencia = comp;
        Mostrar(r.Valor.Tabelas());
        AvisosTexto = "";
        Mensagem = r.Valor.Linhas.Count == 0 ? "Não há receitas de notas emitidas na competência." : $"Receita da competência: {Numeros.Moeda(r.Valor.Receita)}.";
    }, Permissao.FiscalRelatorios);
}
