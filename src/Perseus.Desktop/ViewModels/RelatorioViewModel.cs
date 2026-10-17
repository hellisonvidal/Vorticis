using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Application.Relatorios;
using Perseus.Domain.Seguranca;
using Perseus.Infra.Relatorios;

namespace Perseus.Desktop.ViewModels;

/// <summary>
/// Base dos relatórios: o relatório é sempre exibido na tela e, a partir dela, exportado para PDF, Excel (.xlsx) ou CSV
/// — os três arquivos saem da mesma tabela mostrada.
/// </summary>
public abstract partial class RelatorioViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ExportarPdfCommand), nameof(ExportarExcelCommand), nameof(ExportarCsvCommand))]
    private bool _temDados;

    [ObservableProperty] private string? _ultimoArquivo;

    /// <summary>Excel e CSV exigem a permissão "Exportar relatórios"; o PDF acompanha a permissão de ver o relatório.</summary>
    public bool PodeExportarPlanilha => Contexto.Pode(Permissao.RelatorioExportar);

    protected abstract string TituloRelatorio { get; }

    /// <summary>Período ou parâmetros do relatório (vai para o cabeçalho e para o nome do arquivo).</summary>
    protected abstract string Parametros { get; }

    protected abstract string NomeArquivo { get; }

    protected virtual bool Paisagem => false;

    /// <summary>Tabelas exatamente como exibidas na tela.</summary>
    protected abstract IReadOnlyList<TabelaRelatorio> Tabelas();

    public void AtualizarPermissoesExportacao() => OnPropertyChanged(nameof(PodeExportarPlanilha));

    [RelayCommand(CanExecute = nameof(TemDados))]
    private void ExportarPdf() => Exportar(".pdf", () =>
    {
        var cabecalho = new List<string> { Contexto.DescricaoEmpresa, Parametros,
            $"Emitido em {DateTime.Now:dd/MM/yyyy HH:mm} por {Contexto.Sessao?.Nome}" };
        return new GeradorPdf().Gerar(ExportacaoRelatorio.ParaPdf(TituloRelatorio, cabecalho, Tabelas(), Paisagem)).Conteudo;
    });

    [RelayCommand(CanExecute = nameof(TemDados))]
    private void ExportarExcel() => Exportar(".xlsx", () => ExportadorExcel.Gerar(Tabelas()), exigePermissao: true);

    [RelayCommand(CanExecute = nameof(TemDados))]
    private void ExportarCsv() => Exportar(".csv", () => ExportacaoRelatorio.Csv(Tabelas()[0]), exigePermissao: true);

    private void Exportar(string extensao, Func<byte[]> gerar, bool exigePermissao = false)
    {
        Erros.Clear();
        Mensagem = null;
        if (exigePermissao && !PodeExportarPlanilha)
        {
            Erros.Add($"Seu perfil nesta empresa não permite: {Permissao.RelatorioExportar.Descricao()}.");
            return;
        }
        try
        {
            UltimoArquivo = ArquivosGerados.Salvar(NomeArquivo + extensao, gerar());
            Mensagem = $"Exportado: {UltimoArquivo}";
        }
        catch (Exception ex)
        {
            Erros.Add($"Não foi possível exportar: {ex.Message}");
        }
    }
}
