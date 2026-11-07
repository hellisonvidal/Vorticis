using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Desktop.Comum;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Domain.Seguranca;
using Vorticis.Domain.Sped.Importacao;
using Vorticis.Folha.Esocial;
using Vorticis.Folha.Esocial.Importacao;
using Vorticis.Folha.Servicos;

namespace Vorticis.Folha.Desktop.ViewModels;

/// <summary>Opção da lista "Usar a rubrica": uma rubrica que já existe no Vorticis ou "criar nova".</summary>
public sealed record OpcaoRubrica(string Codigo, string Texto)
{
    public override string ToString() => Texto;
}

/// <summary>Uma linha da grade de rubricas: a rubrica do arquivo e a escolha de onde ela cai na folha do Vorticis.</summary>
public sealed partial class LinhaRubricaEsocial : ObservableObject
{
    private readonly RubricaPlano _p;
    private readonly Action _alterou;
    private readonly bool _inicial;

    public LinhaRubricaEsocial(RubricaPlano p, IReadOnlyList<OpcaoRubrica> opcoes, Action alterou)
    {
        _p = p;
        _alterou = alterou;
        Opcoes = opcoes;
        _destino = p.Existente ? opcoes.FirstOrDefault(o => o.Codigo == p.Destino) ?? opcoes[0] : opcoes[0];
        _variavel = p.Variavel;
        _inicial = true;
    }

    public IReadOnlyList<OpcaoRubrica> Opcoes { get; }
    public string Origem => _p.Origem;
    public string Descricao => _p.Descricao;
    public string Natureza => _p.Natureza;
    public string Tipo => _p.Tipo switch { 'P' => "Provento", 'D' => "Desconto", _ => "Informativa" };
    public string Situacao => _p.Situacao;
    public string Aviso => _p.Aviso ?? "";
    public int Usos => _p.Usos;
    /// <summary>Código que a rubrica terá no Vorticis (existente ou nova).</summary>
    public string DestinoFinal => _p.Destino;
    public bool Existente => _p.Existente;
    public bool PodeEditarMedia => !_p.Existente;

    [ObservableProperty] private OpcaoRubrica? _destino;
    [ObservableProperty] private bool _variavel;

    partial void OnDestinoChanged(OpcaoRubrica? value) { if (_inicial) _alterou(); }
    partial void OnVariavelChanged(bool value) { if (_inicial) _alterou(); }

    /// <summary>Decisão do contador para esta rubrica (código em branco = deixar a sugestão do sistema).</summary>
    public DecisaoRubrica Decisao => new(Origem, Destino?.Codigo ?? "", Variavel);
}

/// <summary>
/// Folha › Integrações › Importar eSocial: escolhe os arquivos (XML, pasta ou ZIP baixados do portal ou exportados por outro sistema) ou baixa direto do
/// eSocial, mostra a prévia (novos, existentes, conflitos, erros e o mapeamento das rubricas), importa com andamento e permite desfazer.
/// </summary>
public sealed partial class ImportacaoEsocialViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public const string Aceita = "Aceita os XML dos eventos do eSocial (S-1000, S-1005, S-1010, S-1020, S-2200, S-2205, S-2206, S-2230, S-2299, S-2300, S-2399, S-1200, S-1210 e S-3000), " +
                                 "soltos em uma pasta, em vários arquivos ou dentro de um ZIP, com ou sem o recibo do eSocial.";

    private List<ArquivoEsocial> _arquivos = [];
    private AnaliseEsocial? _analise;
    private PreviaEsocial? _previa;
    private bool _montando;
    private CancellationTokenSource? _cancelamento;
    private RelatorioImportacao? _relatorio;

    [ObservableProperty] private string _origem = "";
    [ObservableProperty] private string _resumoLeitura = "";

    // opções da importação
    [ObservableProperty] private bool _ignorarComErro;
    [ObservableProperty] private bool _importarRemuneracoes = true;
    [ObservableProperty] private bool _importarFerias = true;
    [ObservableProperty] private bool _fecharCompetencias = true;
    [ObservableProperty] private bool _atualizarParametros;
    [ObservableProperty] private bool _limitarPeriodo;
    [ObservableProperty] private DateTime? _de = FolhaExtensoes.Mes(DateOnly.FromDateTime(DateTime.Today).AddYears(-1));
    [ObservableProperty] private DateTime? _ate = FolhaExtensoes.Mes(DateOnly.FromDateTime(DateTime.Today));

    // prévia
    [ObservableProperty] private string _resumoPrevia = "";
    [ObservableProperty] private string _periodo = "";
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ImportarCommand))] private bool _previaPronta;
    public ObservableCollection<LinhaPrevia> Previa { get; } = [];
    public ObservableCollection<OcorrenciaTela> Ocorrencias { get; } = [];
    public ObservableCollection<LinhaRubricaEsocial> Rubricas { get; } = [];

    // importação
    [ObservableProperty] private double _progresso;
    [ObservableProperty] private string _progressoTexto = "";
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ImportarCommand), nameof(CancelarCommand), nameof(DesfazerCommand), nameof(BaixarCommand))] private bool _importando;

    // relatório e histórico
    [ObservableProperty] private string _resumoRelatorio = "";
    [ObservableProperty] private string? _arquivoRelatorio;
    public ObservableCollection<Domain.Sped.Importacao.LinhaRelatorio> RelatorioLinhas { get; } = [];
    public ObservableCollection<OcorrenciaTela> RelatorioOcorrencias { get; } = [];
    public ObservableCollection<HistoricoImportacao> Historico { get; } = [];
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(DesfazerCommand))] private HistoricoImportacao? _historicoSelecionado;
    public string? RelatorioCsv => _relatorio?.ParaCsv();

    // baixar do eSocial
    public static IReadOnlyList<OpcaoAmbiente> Ambientes => TransmissaoEsocialViewModel.Ambientes;
    public static IReadOnlyList<OpcaoTransmissor> Transmissores => TransmissaoEsocialViewModel.Transmissores;
    [ObservableProperty] private OpcaoAmbiente _ambiente = TransmissaoEsocialViewModel.Ambientes[1];
    [ObservableProperty] private OpcaoTransmissor _transmissor = TransmissaoEsocialViewModel.Transmissores[0];
    [ObservableProperty] private DateTime? _baixarDe = FolhaExtensoes.Mes(DateOnly.FromDateTime(DateTime.Today).AddMonths(-12));
    [ObservableProperty] private DateTime? _baixarAte = FolhaExtensoes.Mes(DateOnly.FromDateTime(DateTime.Today));
    [ObservableProperty] private bool _baixarTabelas = true;
    [ObservableProperty] private bool _baixarRemuneracoes = true;
    [ObservableProperty] private bool _baixarPagamentos = true;
    [ObservableProperty] private bool _baixarTrabalhadores = true;
    [ObservableProperty] private string _cpfsExtras = "";

    public bool PodeUsar => Contexto.Pode(Permissao.FolhaCadastrosManter);
    public bool PodeBaixar => PodeUsar && Contexto.Pode(Permissao.FolhaESocial);
    public bool EhProducao => Ambiente.Valor == AmbienteEsocial.Producao;
    public bool TemRubricas => Rubricas.Count > 0;
    public void AtualizarPermissoes() { OnPropertyChanged(nameof(PodeUsar)); OnPropertyChanged(nameof(PodeBaixar)); }

    partial void OnAmbienteChanged(OpcaoAmbiente value) => OnPropertyChanged(nameof(EhProducao));
    partial void OnIgnorarComErroChanged(bool value) => ReverPrevia();
    partial void OnImportarRemuneracoesChanged(bool value) => ReverPrevia();
    partial void OnImportarFeriasChanged(bool value) => ReverPrevia();
    partial void OnFecharCompetenciasChanged(bool value) => ReverPrevia();
    partial void OnAtualizarParametrosChanged(bool value) => ReverPrevia();
    partial void OnLimitarPeriodoChanged(bool value) => ReverPrevia();
    partial void OnDeChanged(DateTime? value) { if (LimitarPeriodo) ReverPrevia(); }
    partial void OnAteChanged(DateTime? value) { if (LimitarPeriodo) ReverPrevia(); }

    private void ReverPrevia()
    {
        if (_montando || _analise is null || Importando) return;
        _ = AtualizarPreviaAsync();
    }

    private OpcoesImportacaoEsocial Opcoes() => new(IgnorarComErro, ImportarRemuneracoes, ImportarFerias, FecharCompetencias, AtualizarParametros,
        LimitarPeriodo ? FolhaExtensoes.Data(De) : null, LimitarPeriodo ? FolhaExtensoes.Data(Ate) : null, Rubricas.Select(r => r.Decisao).ToList());

    private void LimparPrevia()
    {
        _montando = true;
        try { Previa.Clear(); Ocorrencias.Clear(); Rubricas.Clear(); }
        finally { _montando = false; }
        Periodo = ResumoPrevia = "";
        PreviaPronta = false;
        _previa = null;
        OnPropertyChanged(nameof(TemRubricas));
    }

    /// <summary>Limpa a tela (troca de empresa ou de usuário).</summary>
    public void Limpar()
    {
        _arquivos = [];
        _analise = null;
        Origem = ResumoLeitura = "";
        LimparPrevia();
        Erros.Clear();
        Mensagem = null;
        Historico.Clear();
        _relatorio = null;
        RelatorioLinhas.Clear();
        RelatorioOcorrencias.Clear();
        ResumoRelatorio = "";
        ArquivoRelatorio = null;
        Progresso = 0;
        ProgressoTexto = "";
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) { Historico.Clear(); return; }
        var g = NovaGeracao();
        var lista = await app.Folha().ImportacaoEsocial.HistoricoAsync(empresa, sessao.Id);
        if (!EhAtual(g)) return;
        Historico.Clear();
        foreach (var h in lista.OrderByDescending(h => h.EmUtc).ThenByDescending(h => h.Id)) Historico.Add(h);
    }

    // ------------------------------------------------------------------ 1. arquivos

    /// <summary>Lê os arquivos, pastas ou ZIPs escolhidos (cada caminho pode ser arquivo ou pasta) e confere o conteúdo.</summary>
    public async Task LerCaminhosAsync(IReadOnlyList<string> caminhos)
    {
        Erros.Clear();
        Mensagem = null;
        _analise = null;
        LimparPrevia();
        ResumoLeitura = "";
        if (caminhos.Count == 0) { Erros.Add("Escolha os arquivos, a pasta ou o ZIP com os eventos do eSocial."); return; }
        var lidos = new List<ArquivoEsocial>();
        try
        {
            await Task.Run(() =>
            {
                foreach (var c in caminhos)
                {
                    if (Directory.Exists(c))
                    {
                        foreach (var f in Directory.EnumerateFiles(c, "*.*", SearchOption.AllDirectories).Where(f => Path.GetExtension(f).ToLowerInvariant() is ".xml" or ".zip"))
                            lidos.Add(new ArquivoEsocial(Path.GetRelativePath(c, f), File.ReadAllBytes(f)));
                    }
                    else if (File.Exists(c)) lidos.Add(new ArquivoEsocial(Path.GetFileName(c), File.ReadAllBytes(c)));
                    else throw new FileNotFoundException($"Não encontrei \"{c}\".");
                }
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Erros.Add($"Não foi possível ler os arquivos: {ex.Message}");
            return;
        }
        Origem = caminhos.Count == 1 ? caminhos[0] : $"{caminhos.Count} itens selecionados";
        if (lidos.Count == 0) { Erros.Add("Não há arquivos .xml ou .zip no que foi escolhido."); return; }
        _arquivos = lidos;
        await AnalisarAsync();
    }

    /// <summary>Lê o que foi digitado na caixa de caminho (um arquivo ou uma pasta).</summary>
    [RelayCommand]
    public Task LerArquivosAsync() => LerCaminhosAsync(string.IsNullOrWhiteSpace(Origem) ? [] : [Origem.Trim().Trim('"')]);

    private async Task AnalisarAsync()
    {
        if (_arquivos.Count == 0) return;
        var arquivos = _arquivos;
        var origem = Origem;
        var g = NovaGeracao();
        AnaliseEsocial? analise = null;
        List<string>? falhas = null;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            var r = await app.Folha().ImportacaoEsocial.AnalisarAsync(empresa, arquivos, origem, usuario);
            if (r.Sucesso) analise = r.Valor; else falhas = [.. r.Erros];
        });
        if (!EhAtual(g)) return;
        if (falhas is not null) { Erros.Clear(); foreach (var f in falhas) Erros.Add(f); return; }
        _analise = analise;
        if (analise is null) return;
        var d = analise.Dados;
        ResumoLeitura = $"{arquivos.Count} arquivo(s) lido(s): " + string.Join(", ", d.Eventos.OrderBy(e => e.Key).Select(e => $"{e.Value} {e.Key}")) +
                        (d.VersoesAnterioresIgnoradas > 0 ? $" · {d.VersoesAnterioresIgnoradas} versão(ões) anterior(es) substituída(s)" : "") +
                        (d.Excluidos > 0 ? $" · {d.Excluidos} evento(s) excluído(s) por S-3000" : "");
        await AtualizarPreviaAsync();
    }

    // ------------------------------------------------------------------ 2. prévia

    public async Task AtualizarPreviaAsync()
    {
        if (_analise is not { } analise) return;
        var opcoes = Opcoes();
        var g = NovaGeracao();
        PreviaPronta = false;
        PreviaEsocial? previa = null;
        List<string>? falhas = null;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            var r = await app.Folha().ImportacaoEsocial.PreviaAsync(empresa, analise, opcoes, usuario);
            if (r.Sucesso) previa = r.Valor; else falhas = [.. r.Erros];
        });
        if (!EhAtual(g)) return;
        if (falhas is not null) { Erros.Clear(); foreach (var f in falhas) Erros.Add(f); return; }
        if (previa is null) return;
        _previa = previa;
        var plano = previa.Plano;
        // as rubricas existentes são lidas antes de limpar as grades: a tela nunca fica vazia no meio da troca
        var existentes = new List<OpcaoRubrica> { new("", "(criar uma rubrica nova)") };
        if (Contexto.App is { } app && Contexto.EmpresaId is { } empresa && Contexto.Sessao is { } sessao)
            existentes.AddRange((await app.Folha().Cadastros.ListarRubricasAsync(empresa, sessao.Id)).OrderBy(r => r.Codigo, StringComparer.Ordinal).Select(r => new OpcaoRubrica(r.Codigo, $"{r.Codigo} – {r.Descricao}")));
        if (!EhAtual(g)) return;
        _montando = true;
        try
        {
            Previa.Clear(); Ocorrencias.Clear(); Rubricas.Clear();
            foreach (var l in plano.Linhas) Previa.Add(l);
            foreach (var o in plano.Ocorrencias.OrderBy(o => o.Gravidade).ThenBy(o => o.Registro)) Ocorrencias.Add(OcorrenciaTela.De(o));
            foreach (var r in plano.GradeRubricas) Rubricas.Add(new LinhaRubricaEsocial(r, existentes, ReverPrevia));
        }
        finally { _montando = false; }
        OnPropertyChanged(nameof(TemRubricas));
        Periodo = plano.Inicio is { } ini && plano.Fim is { } fim ? $"{ini:MM/yyyy} a {fim:MM/yyyy}" : "";
        PreviaPronta = previa.PodeImportar(opcoes);
        ResumoPrevia = previa.Resumo(opcoes);
    }

    // ------------------------------------------------------------------ 3. importar

    private bool PodeImportar() => PreviaPronta && !Importando;

    [RelayCommand(CanExecute = nameof(PodeImportar))]
    private async Task ImportarAsync()
    {
        if (_analise is not { } analise) return;
        if (Contexto.App is null || Contexto.Sessao is null || Contexto.EmpresaId is null) { Erros.Clear(); Erros.Add("Selecione uma empresa."); return; }
        var opcoes = Opcoes();
        if (!await Contexto.ConfirmarAsync("Importar do eSocial",
                $"Importar os eventos do eSocial ({analise.Origem}) na empresa ativa?\n\n{ResumoPrevia}\n\n" +
                (opcoes.FecharCompetencias && opcoes.ImportarRemuneracoes ? "As competências do histórico importado ficam FECHADAS, para não serem recalculadas por engano. " : "") +
                "Tudo é gravado numa só vez: se algo falhar, nada fica pela metade. Depois você pode desfazer pelo Histórico de importações.",
                "Importar", "Cancelar")) return;
        _cancelamento = new CancellationTokenSource();
        var ct = _cancelamento.Token;
        Importando = true;
        Progresso = 0;
        ProgressoTexto = "Iniciando...";
        RelatorioImportacao? rel = null;
        List<string>? falhas = null;
        try
        {
            await ExecutarAsync(async (app, empresa, usuario) =>
            {
                var andamento = new Progress<ProgressoImportacao>(p => { Progresso = p.Percentual; ProgressoTexto = $"{p.Etapa} ({p.Atual} de {p.Total})"; });
                var r = await app.Folha().ImportacaoEsocial.ImportarAsync(empresa, analise, opcoes, usuario, andamento, ct);
                if (r.Sucesso) rel = r.Valor; else falhas = [.. r.Erros];
            });
        }
        finally
        {
            Importando = false;
            _cancelamento.Dispose();
            _cancelamento = null;
        }
        if (falhas is not null) { Erros.Clear(); foreach (var f in falhas) Erros.Add(f); ProgressoTexto = "Importação não realizada."; return; }
        if (rel is null) return;
        Progresso = 100;
        ProgressoTexto = "Concluída.";
        _analise = null;                      // o material já foi importado: nova prévia mostraria só "já importado"
        LimparPrevia();
        MostrarRelatorio(rel);
        await CarregarAsync();
        Mensagem = "Importação concluída. " + rel.Resumo;
    }

    [RelayCommand(CanExecute = nameof(Importando))]
    private void Cancelar() => _cancelamento?.Cancel();

    private void MostrarRelatorio(RelatorioImportacao rel)
    {
        _relatorio = rel;
        ResumoRelatorio = rel.Resumo;
        RelatorioLinhas.Clear();
        foreach (var l in rel.Linhas) RelatorioLinhas.Add(l);
        RelatorioOcorrencias.Clear();
        foreach (var o in rel.Ocorrencias.OrderBy(o => o.Gravidade).ThenBy(o => o.Registro)) RelatorioOcorrencias.Add(OcorrenciaTela.De(o));
        OnPropertyChanged(nameof(RelatorioCsv));
    }

    [RelayCommand]
    private void SalvarRelatorio()
    {
        Erros.Clear();
        if (_relatorio is not { } rel) { Erros.Add("Ainda não há relatório: importe os eventos primeiro."); return; }
        try
        {
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(rel.ParaCsv())).ToArray();
            ArquivoRelatorio = ArquivosGerados.Salvar($"relatorio_importacao_esocial_{rel.ImportacaoId}.csv", bytes);
            Mensagem = $"Relatório salvo: {ArquivoRelatorio}";
        }
        catch (IOException ex) { Erros.Add(ex.Message); }
    }

    private bool PodeDesfazer() => HistoricoSelecionado is { Desfeita: false } && !Importando;

    [RelayCommand(CanExecute = nameof(PodeDesfazer))]
    private async Task DesfazerAsync()
    {
        if (HistoricoSelecionado is not { Desfeita: false } h) return;
        if (!await Contexto.ConfirmarAsync("Desfazer importação",
                $"Desfazer a importação \"{h.Arquivo}\" ({h.Quando})?\n\nSerão removidos os funcionários, as rubricas, o histórico de holerites, as férias e os afastamentos que ela criou, e as competências que ela fechou voltam a ficar abertas. " +
                "Se algo já foi usado depois (cálculos novos, rescisões, contabilização), nada é removido e você é avisado.",
                "Desfazer", "Cancelar")) return;
        RelatorioImportacao? rel = null;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            var r = await app.Folha().ImportacaoEsocial.DesfazerAsync(empresa, h.Id, usuario);
            MostrarResultado(r, "");
            if (r.Sucesso) rel = r.Valor;
        });
        if (rel is null) return;
        MostrarRelatorio(rel);
        await CarregarAsync();
        if (_analise is not null) await AtualizarPreviaAsync();
        Mensagem = rel.Resumo;
    }

    // ------------------------------------------------------------------ baixar do eSocial

    private bool PodeBaixarAgora() => !Importando;

    [RelayCommand(CanExecute = nameof(PodeBaixarAgora))]
    private async Task BaixarAsync()
    {
        Erros.Clear();
        Mensagem = null;
        var de = FolhaExtensoes.InicioMes(BaixarDe);
        var ate = FolhaExtensoes.InicioMes(BaixarAte);
        var cpfs = CpfsExtras.Split([' ', '\n', '\r', ';', ',', '\t'], StringSplitOptions.RemoveEmptyEntries).Select(c => new string(c.Where(char.IsDigit).ToArray())).Where(c => c.Length == 11).ToList();
        var opcoes = new OpcoesDownloadEsocial(de, ate, BaixarTabelas, BaixarRemuneracoes, BaixarPagamentos, BaixarTrabalhadores, cpfs);
        ResultadoDownloadEsocial? resultado = null;
        List<string>? falhas = null;
        Importando = true;
        Progresso = 0;
        ProgressoTexto = "Consultando o eSocial...";
        try
        {
            await ExecutarAsync(async (app, empresa, usuario) =>
            {
                var cert = await CertificadosPadrao.AbrirParaEsocialAsync(Contexto, Transmissor.Papel);
                if (!cert.Sucesso) { falhas = [.. cert.Erros]; return; }
                using var certificado = cert.Valor;
                var andamento = new Progress<ProgressoImportacao>(p => { Progresso = p.Percentual; ProgressoTexto = $"{p.Etapa} ({p.Atual} de {p.Total})"; });
                var r = await app.Folha().DownloadEsocial.BaixarAsync(empresa, opcoes, Ambiente.Valor, Transmissor.Valor, certificado, usuario, andamento);
                if (r.Sucesso) resultado = r.Valor; else falhas = [.. r.Erros];
            }, Permissao.FolhaESocial);
        }
        finally { Importando = false; }
        if (falhas is not null) { Erros.Clear(); foreach (var f in falhas) Erros.Add(f); ProgressoTexto = "Nada foi baixado."; return; }
        if (resultado is null) return;
        foreach (var a in resultado.Avisos) Erros.Add(a);
        ProgressoTexto = $"{resultado.Baixados} evento(s) baixado(s) do eSocial.";
        _arquivos = [.. resultado.Arquivos];
        Origem = $"Baixado do eSocial em {DateTime.Now:dd/MM/yyyy HH:mm} ({de:MM/yyyy} a {ate:MM/yyyy})";
        await AnalisarAsync();
    }
}
