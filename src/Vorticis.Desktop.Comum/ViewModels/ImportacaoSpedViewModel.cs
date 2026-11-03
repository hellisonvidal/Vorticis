using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Sped.Importacao;
using Vorticis.Domain.Sped.Importacao;
using Vorticis.Infra;

namespace Vorticis.Desktop.Comum.ViewModels;

/// <summary>Uma ocorrência (erro, aviso ou informação) como aparece nas listas da tela.</summary>
public sealed record OcorrenciaTela(string Gravidade, string Linha, string Registro, string Motivo, bool EhErro)
{
    public static OcorrenciaTela De(OcorrenciaImportacao o) => new(
        o.Gravidade switch { GravidadeOcorrencia.Erro => "Erro", GravidadeOcorrencia.Aviso => "Aviso", _ => "Informação" },
        o.Linha > 0 ? o.Linha.ToString() : "", o.Registro, o.Motivo, o.Gravidade == GravidadeOcorrencia.Erro);
}

/// <summary>
/// Integrações › Importar › Arquivo SPED: escolher o arquivo, reconhecer o tipo (ECD, ECF, EFD ICMS/IPI ou EFD-Contribuições), mostrar a
/// prévia (período, quantidades, conflitos e erros), importar com andamento, ver o relatório e desfazer importações do histórico.
/// A mesma tela serve ao Contábil (ECD e ECF) e ao Fiscal (EFD); cada módulo informa os seus importadores.
/// </summary>
public sealed partial class ImportacaoSpedViewModel(IContexto contexto, string titulo, string aceita, Func<VorticisApp, IReadOnlyList<IImportadorSped>> importadores)
    : ModuloViewModel(contexto)
{
    private byte[]? _bytes;
    private string _nomeArquivo = "";
    private IImportadorSped? _importador;
    private bool _montando;
    private CancellationTokenSource? _cancelamento;

    public string Titulo { get; } = titulo;
    /// <summary>Texto de ajuda da tela: quais arquivos ela aceita.</summary>
    public string Aceita { get; } = aceita;

    [ObservableProperty] private string _arquivo = "";
    [ObservableProperty] private string _tipoDetectado = "";
    public ObservableCollection<ModoImportacao> Modos { get; } = [];
    [ObservableProperty] private ModoImportacao? _modoSelecionado;
    [ObservableProperty] private string _modoDescricao = "";

    [ObservableProperty] private bool _substituirPlano;
    [ObservableProperty] private bool _converterMascara = true;
    [ObservableProperty] private bool _cadastrarSignatarios;
    [ObservableProperty] private bool _ignorarComErro;
    [ObservableProperty] private bool _mostraOpcoesPlano;
    [ObservableProperty] private bool _mostraSignatarios;

    [ObservableProperty] private string _periodo = "";
    [ObservableProperty] private string _empresaDoArquivo = "";
    [ObservableProperty] private string _resumoPrevia = "";
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ImportarCommand))] private bool _previaPronta;
    public ObservableCollection<LinhaPrevia> Previa { get; } = [];
    public ObservableCollection<OcorrenciaTela> Ocorrencias { get; } = [];

    [ObservableProperty] private double _progresso;
    [ObservableProperty] private string _progressoTexto = "";
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ImportarCommand), nameof(CancelarCommand), nameof(DesfazerCommand))] private bool _importando;

    [ObservableProperty] private string _resumoRelatorio = "";
    public ObservableCollection<Domain.Sped.Importacao.LinhaRelatorio> RelatorioLinhas { get; } = [];
    public ObservableCollection<OcorrenciaTela> RelatorioOcorrencias { get; } = [];
    [ObservableProperty] private string? _arquivoRelatorio;
    private RelatorioImportacao? _relatorio;

    public ObservableCollection<HistoricoImportacao> Historico { get; } = [];
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(DesfazerCommand))] private HistoricoImportacao? _historicoSelecionado;

    /// <summary>Relatório da última importação ou desfazer, em CSV (abre direto no Excel).</summary>
    public string? RelatorioCsv => _relatorio?.ParaCsv();

    partial void OnModoSelecionadoChanged(ModoImportacao? value)
    {
        ModoDescricao = value?.Descricao ?? "";
        ReverPrevia();
    }

    partial void OnSubstituirPlanoChanged(bool value) => ReverPrevia();
    partial void OnConverterMascaraChanged(bool value) => ReverPrevia();
    partial void OnCadastrarSignatariosChanged(bool value) => ReverPrevia();
    partial void OnIgnorarComErroChanged(bool value) => ReverPrevia();

    private void ReverPrevia()
    {
        if (_montando || _bytes is null || _importador is null || Importando) return;
        _ = AtualizarPreviaAsync();
    }

    private OpcoesImportacaoSped Opcoes() => new(ModoSelecionado?.Codigo ?? "TUDO", SubstituirPlano, IgnorarComErro, ConverterMascara, CadastrarSignatarios);

    private void LimparPrevia()
    {
        Previa.Clear();
        Ocorrencias.Clear();
        Periodo = EmpresaDoArquivo = ResumoPrevia = "";
        PreviaPronta = false;
    }

    private void LimparTudo()
    {
        _bytes = null;
        _importador = null;
        _montando = true;
        try
        {
            Modos.Clear();
            ModoSelecionado = null;
        }
        finally { _montando = false; }
        TipoDetectado = "";
        MostraOpcoesPlano = MostraSignatarios = false;
        LimparPrevia();
    }

    /// <summary>Ao abrir a tela ou trocar de empresa: carrega o histórico de importações.</summary>
    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao)
        {
            Historico.Clear();
            return;
        }
        var g = NovaGeracao();
        var todos = new List<HistoricoImportacao>();
        try
        {
            foreach (var i in importadores(app)) todos.AddRange(await i.HistoricoAsync(empresa, sessao.Id));
        }
        catch (Exception ex)
        {
            if (EhAtual(g)) { Erros.Clear(); Erros.Add(ex.Message); }
            return;
        }
        if (!EhAtual(g)) return;
        Historico.Clear();
        foreach (var h in todos.OrderByDescending(h => h.EmUtc).ThenByDescending(h => h.Id)) Historico.Add(h);
    }

    /// <summary>Limpa a tela (troca de empresa ou de usuário).</summary>
    public void Limpar()
    {
        Arquivo = "";
        LimparTudo();
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

    /// <summary>Lê o arquivo escolhido, reconhece o tipo e monta a prévia.</summary>
    [RelayCommand]
    public async Task LerArquivoAsync()
    {
        Erros.Clear();
        Mensagem = null;
        LimparTudo();
        _relatorio = null;
        RelatorioLinhas.Clear();
        RelatorioOcorrencias.Clear();
        ResumoRelatorio = "";
        ArquivoRelatorio = null;
        Progresso = 0;
        ProgressoTexto = "";
        if (Contexto.App is not { } app) { Erros.Add("Sem conexão com o banco de dados."); return; }
        if (string.IsNullOrWhiteSpace(Arquivo) || !File.Exists(Arquivo)) { Erros.Add("Escolha o arquivo SPED (.txt) a importar."); return; }
        byte[] bytes;
        try { bytes = await File.ReadAllBytesAsync(Arquivo); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Erros.Add($"Não foi possível ler o arquivo: {ex.Message}"); return; }
        var lido = await Task.Run(() => LeitorSped.Ler(bytes));
        if (lido.Tipo == TipoArquivoSped.Desconhecido)
        {
            Erros.Add(lido.Ocorrencias.FirstOrDefault(o => o.Gravidade == GravidadeOcorrencia.Erro)?.Motivo
                      ?? "Não foi possível reconhecer o arquivo: ele não parece ser um arquivo SPED (ECD, ECF, EFD ICMS/IPI ou EFD-Contribuições).");
            return;
        }
        TipoDetectado = lido.Tipo.Nome();
        var imp = importadores(app).FirstOrDefault(i => i.Tipo == lido.Tipo);
        if (imp is null)
        {
            Erros.Add($"Este arquivo é {lido.Tipo.Nome()}, que não é importado por esta tela. {Aceita}");
            return;
        }
        _bytes = bytes;
        _nomeArquivo = Path.GetFileName(Arquivo);
        _importador = imp;
        _montando = true;
        try
        {
            Modos.Clear();
            foreach (var m in imp.Modos) Modos.Add(m);
            ModoSelecionado = Modos.FirstOrDefault();
            SubstituirPlano = false;
            IgnorarComErro = false;
            ConverterMascara = true;
            CadastrarSignatarios = false;
            MostraOpcoesPlano = lido.Tipo is TipoArquivoSped.Ecd or TipoArquivoSped.Ecf;
            MostraSignatarios = lido.Tipo == TipoArquivoSped.Ecd;
        }
        finally { _montando = false; }
        await AtualizarPreviaAsync();
    }

    /// <summary>Refaz a prévia com o modo e as opções escolhidos (não grava nada).</summary>
    public async Task AtualizarPreviaAsync()
    {
        if (_bytes is null || _importador is not { } imp) return;
        var bytes = _bytes;
        var opcoes = Opcoes();
        var g = NovaGeracao();
        PreviaPronta = false;
        PreviaImportacao? previa = null;
        List<string>? falhas = null;
        await ExecutarAsync(async (_, empresa, usuario) =>
        {
            var r = await imp.PreviaAsync(empresa, bytes, _nomeArquivo, opcoes, usuario);
            if (r.Sucesso) previa = r.Valor; else falhas = [.. r.Erros];
        });
        if (!EhAtual(g)) return;
        Previa.Clear();
        Ocorrencias.Clear();
        if (falhas is not null)
        {
            Erros.Clear();
            foreach (var f in falhas) Erros.Add(f);
            Periodo = EmpresaDoArquivo = ResumoPrevia = "";
            return;
        }
        if (previa is null) return;
        Periodo = previa.Cabecalho.Periodo;
        EmpresaDoArquivo = $"{previa.Cabecalho.Nome} · CNPJ {ServicoImportacaoEcd.Formatar(previa.Cabecalho.Cnpj)}" + (previa.CnpjConfere ? "" : " (não é o da empresa ativa)");
        foreach (var l in previa.Linhas) Previa.Add(l);
        foreach (var o in previa.Ocorrencias.OrderBy(o => o.Gravidade).ThenBy(o => o.Linha)) Ocorrencias.Add(OcorrenciaTela.De(o));
        var novos = previa.Linhas.Sum(l => l.Novos);
        var existentes = previa.Linhas.Sum(l => l.Existentes);
        var conflitos = previa.Linhas.Sum(l => l.Conflitos);
        var erros = previa.Ocorrencias.Count(o => o.Gravidade == GravidadeOcorrencia.Erro);
        var avisos = previa.Ocorrencias.Count(o => o.Gravidade == GravidadeOcorrencia.Aviso);
        ResumoPrevia = $"{novos} registro(s) novo(s), {existentes} já existente(s) (serão ignorados), {conflitos} conflito(s), {erros} erro(s), {avisos} aviso(s)";
        PreviaPronta = previa.PodeImportar && previa.ImportacaoAnteriorId is null && (previa.ErrosPontuais == 0 || IgnorarComErro);
        if (!PreviaPronta && previa.ErrosPontuais > 0 && previa.PodeImportar && previa.ImportacaoAnteriorId is null)
            ResumoPrevia += " · marque \"Ignorar o que tiver erro\" para importar o restante";
        else if (PreviaPronta) ResumoPrevia += " · pronto para importar";
    }

    private bool PodeImportar() => PreviaPronta && !Importando;

    [RelayCommand(CanExecute = nameof(PodeImportar))]
    private async Task ImportarAsync()
    {
        if (_bytes is null || _importador is not { } imp || ModoSelecionado is not { } modo) return;
        var bytes = _bytes;
        var opcoes = Opcoes();
        var nome = _nomeArquivo;
        if (Contexto.App is null || Contexto.Sessao is null || Contexto.EmpresaId is null) { Erros.Clear(); Erros.Add("Selecione uma empresa."); return; }
        if (!await Contexto.ConfirmarAsync("Importar arquivo SPED",
                $"Importar \"{nome}\" ({TipoDetectado}, {Periodo}) na empresa ativa?\n\nO que será feito: {modo.Titulo}.\n{ResumoPrevia}\n\n" +
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
            await ExecutarAsync(async (_, empresa, usuario) =>
            {
                var andamento = new Progress<ProgressoImportacao>(p => { Progresso = p.Percentual; ProgressoTexto = $"{p.Etapa} ({p.Atual} de {p.Total})"; });
                var r = await imp.ImportarAsync(empresa, bytes, nome, opcoes, usuario, andamento, ct);
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
        LimparTudo();                       // o arquivo já foi importado: nova prévia mostraria só "já importado"
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
        foreach (var o in rel.Ocorrencias.OrderBy(o => o.Gravidade).ThenBy(o => o.Linha)) RelatorioOcorrencias.Add(OcorrenciaTela.De(o));
        OnPropertyChanged(nameof(RelatorioCsv));
    }

    /// <summary>Grava o relatório da última importação (CSV) na pasta de relatórios e o abre.</summary>
    [RelayCommand]
    private void SalvarRelatorio()
    {
        Erros.Clear();
        if (_relatorio is not { } rel) { Erros.Add("Ainda não há relatório: importe um arquivo primeiro."); return; }
        try
        {
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(rel.ParaCsv())).ToArray();
            ArquivoRelatorio = ArquivosGerados.Salvar($"relatorio_importacao_sped_{rel.ImportacaoId}.csv", bytes);
            Mensagem = $"Relatório salvo: {ArquivoRelatorio}";
        }
        catch (IOException ex) { Erros.Add(ex.Message); }
    }

    private bool PodeDesfazer() => HistoricoSelecionado is { Desfeita: false } && !Importando;

    [RelayCommand(CanExecute = nameof(PodeDesfazer))]
    private async Task DesfazerAsync()
    {
        if (HistoricoSelecionado is not { Desfeita: false } h || Contexto.App is not { } app) return;
        var imp = importadores(app).FirstOrDefault(i => i.Tipo.Codigo() == h.Tipo);
        if (imp is null) { Erros.Clear(); Erros.Add("Esta importação não pode ser desfeita por esta tela."); return; }
        if (!await Contexto.ConfirmarAsync("Desfazer importação",
                $"Desfazer a importação de \"{h.Arquivo}\" ({h.Quando})?\n\nSerá removido o que ela criou. Se algo já foi usado, contabilizado ou fechado, nada é removido e você é avisado.",
                "Desfazer", "Cancelar")) return;
        RelatorioImportacao? rel = null;
        await ExecutarAsync(async (_, empresa, usuario) =>
        {
            var r = await imp.DesfazerAsync(empresa, h.Id, usuario);
            MostrarResultado(r, "");
            if (r.Sucesso) rel = r.Valor;
        });
        if (rel is null) return;
        MostrarRelatorio(rel);
        await CarregarAsync();
        if (_bytes is not null) await AtualizarPreviaAsync();
        Mensagem = rel.Resumo;
    }
}
