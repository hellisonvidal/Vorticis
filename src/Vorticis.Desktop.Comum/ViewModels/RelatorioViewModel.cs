using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Relatorios;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra.Relatorios;

namespace Vorticis.Desktop.Comum.ViewModels;

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

    // ---- Comparativo (opção comum a todos os relatórios contábeis) ------------------------------------------------

    /// <summary>Opções da barra "Comparativo" da tela.</summary>
    public ComparativoViewModel Comparacao => _comparacao ??= new ComparativoViewModel { PodeAnaliseVertical = SuportaAnaliseVertical };
    private ComparativoViewModel? _comparacao;

    /// <summary>Tabela comparativa exibida (e exportada) no lugar do relatório normal, quando o comparativo está ligado.</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(MostraComparativo), nameof(MostraNormal), nameof(TabelaExibida))] private TabelaRelatorio? _tabelaComparativa;

    /// <summary>Relatórios que usam a grade genérica (DFC, EBITDA) guardam aqui a tabela do relatório normal.</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TabelaExibida))] private TabelaRelatorio? _tabelaTela;

    /// <summary>O que a grade genérica mostra: o comparativo, se ligado, ou a tabela normal.</summary>
    public TabelaRelatorio? TabelaExibida => TabelaComparativa ?? TabelaTela;

    public bool MostraComparativo => TabelaComparativa is not null;
    public bool MostraNormal => TabelaComparativa is null;
    private string _descricaoComparativo = "";

    /// <summary>
    /// Gera o relatório comparativo se a opção estiver marcada. Devolve true quando tratou a geração (com sucesso ou com erro);
    /// false quando o comparativo está desligado e a tela deve gerar o relatório normal.
    /// </summary>
    protected async Task<bool> TentarComparativoAsync(DateOnly inicio, DateOnly fim, bool pontual, Func<OpcoesComparativo, Task<Resultado<TabelaRelatorio>>> gerar)
    {
        if (!Comparacao.Ativo) { TabelaComparativa = null; _descricaoComparativo = ""; return false; }
        var op = Comparacao.Opcoes();
        var r = await gerar(op);
        if (!r.Sucesso) { TabelaComparativa = null; MostrarResultado(r, ""); return true; }
        _descricaoComparativo = Comparativo.Descricao(Comparativo.Periodos(inicio, fim, op, pontual));
        TabelaComparativa = r.Valor;
        TemDados = true;
        AtualizarPermissoesExportacao();
        return true;
    }

    private IReadOnlyList<TabelaRelatorio> TabelasExportacao() => TabelaComparativa is { } t ? [t] : Tabelas();
    private string ParametrosExportacao => TabelaComparativa is null ? Parametros : $"{Parametros} | {_descricaoComparativo}";

    /// <summary>Período ou parâmetros do relatório (vai para o cabeçalho e para o nome do arquivo).</summary>
    protected abstract string Parametros { get; }

    protected abstract string NomeArquivo { get; }

    protected virtual bool Paisagem => false;

    /// <summary>Relatórios com um total de referência (receita, total do ativo...) oferecem a análise vertical (%) no comparativo.</summary>
    protected virtual bool SuportaAnaliseVertical => false;


    /// <summary>Tabelas exatamente como exibidas na tela.</summary>
    protected abstract IReadOnlyList<TabelaRelatorio> Tabelas();

    public void AtualizarPermissoesExportacao() => OnPropertyChanged(nameof(PodeExportarPlanilha));

    /// <summary>Opção comum de todos os relatórios em PDF: bloco de assinatura do sócio/responsável ao final.</summary>
    [ObservableProperty] private bool _assinaturaResponsavel;

    /// <summary>Opção comum de todos os relatórios em PDF: bloco de assinatura do contador (com CRC) ao final.</summary>
    [ObservableProperty] private bool _assinaturaContador;

    /// <summary>Assina digitalmente o PDF (PAdES/PKCS#7) com o certificado padrão do contador, ou do responsável (Configurações › Certificados digitais).</summary>
    [ObservableProperty] private bool _assinarPdfDigitalmente;

    [RelayCommand(CanExecute = nameof(TemDados))]
    private async Task ExportarPdfAsync()
    {
        AssinaturaDigitalPdf? digital = null;
        if (AssinarPdfDigitalmente)
        {
            Erros.Clear();
            Mensagem = null;
            var a = await CertificadosPadrao.ParaPdfAsync(Contexto, $"Relatório: {TituloRelatorio}");
            if (!a.Sucesso) { foreach (var e in a.Erros) Erros.Add(e); return; }
            digital = a.Valor;
        }
        IReadOnlyList<string>? assinaturas = null;
        var opcoes = new OpcoesAssinatura(AssinaturaResponsavel, AssinaturaContador);
        if (opcoes.Alguma)
        {
            Erros.Clear();
            Mensagem = null;
            if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa) { Erros.Add("Selecione uma empresa."); return; }
            try
            {
                var r = await app.Assinaturas.MontarAsync(empresa, opcoes);
                if (!r.Sucesso) { foreach (var e in r.Erros) Erros.Add(e); return; }
                assinaturas = r.Valor;
            }
            catch (Exception ex)
            {
                Erros.Add($"Não foi possível obter os dados das assinaturas: {ex.Message}");
                return;
            }
        }
        Exportar(".pdf", () =>
        {
            var cabecalho = new List<string> { Contexto.DescricaoEmpresa, ParametrosExportacao,
                $"Emitido em {DateTime.Now:dd/MM/yyyy HH:mm} por {Contexto.Sessao?.Nome}" };
            return new GeradorPdf().Gerar(ExportacaoRelatorio.ParaPdf(TituloRelatorio, cabecalho, TabelasExportacao(), Paisagem || TabelaComparativa is not null) with { Assinaturas = assinaturas, AssinaturaDigital = digital }).Conteudo;
        });
    }

    [RelayCommand(CanExecute = nameof(TemDados))]
    private void ExportarExcel() => Exportar(".xlsx", () => ExportadorExcel.Gerar(TabelasExportacao()), exigePermissao: true);

    [RelayCommand(CanExecute = nameof(TemDados))]
    private void ExportarCsv() => Exportar(".csv", () => ExportacaoRelatorio.Csv(TabelasExportacao()[0]), exigePermissao: true);

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
            UltimoArquivo = ArquivosGerados.Salvar(NomeArquivo + (TabelaComparativa is null ? "" : "_comparativo") + extensao, gerar());
            Mensagem = $"Exportado: {UltimoArquivo}";
        }
        catch (Exception ex)
        {
            Erros.Add($"Não foi possível exportar: {ex.Message}");
        }
    }
}
