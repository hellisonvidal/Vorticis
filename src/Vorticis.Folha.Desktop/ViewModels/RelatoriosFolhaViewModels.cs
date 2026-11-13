using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Relatorios;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Folha.Desktop.ViewModels;

/// <summary>Base dos relatórios da folha por competência: a tabela mostrada na tela é a que vai para PDF, Excel e CSV.</summary>
public abstract partial class RelatorioFolhaBase(IContexto contexto) : RelatorioViewModel(contexto)
{
    [ObservableProperty] private DateTime? _competencia = FolhaExtensoes.Mes(contexto.PeriodoTrabalho);
    [ObservableProperty] private TabelaRelatorio? _tabela;
    [ObservableProperty] private TabelaRelatorio? _tabela2;

    protected DateOnly Comp => FolhaExtensoes.InicioMes(Competencia);
    protected DateOnly CompGerada { get; set; }

    protected override string Parametros => $"Competência {CompGerada:MM/yyyy}";
    protected override bool Paisagem => true;

    protected override IReadOnlyList<TabelaRelatorio> Tabelas() => new[] { Tabela, Tabela2 }.OfType<TabelaRelatorio>().ToArray();

    protected void Mostrar(IReadOnlyList<TabelaRelatorio> tabelas)
    {
        CompGerada = Comp;
        Tabela = tabelas.Count > 0 ? tabelas[0] : null;
        Tabela2 = tabelas.Count > 1 ? tabelas[1] : null;
        TemDados = tabelas.Count > 0;
        AtualizarPermissoesExportacao();
    }

    public void Limpar() { Tabela = Tabela2 = null; TemDados = false; }
}

/// <summary>Folha › Relatórios › Resumo da folha: por funcionário e totais por rubrica.</summary>
public partial class ResumoFolhaViewModel(IContexto contexto) : RelatorioFolhaBase(contexto)
{
    protected override string TituloRelatorio => "Resumo da folha de pagamento";
    protected override string NomeArquivo => $"resumo_folha_{CompGerada:yyyyMM}";

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Folha().Relatorios.ResumoFolhaAsync(empresa, Comp, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Mostrar(r.Valor);
        if (r.Valor[0].Linhas.Count <= 1) Mensagem = "Não há folha calculada nesta competência.";
    }, Permissao.FolhaRelatorios);
}

/// <summary>Folha › Relatórios › Encargos patronais: INSS patronal, RAT x FAP, terceiros e FGTS.</summary>
public partial class EncargosFolhaViewModel(IContexto contexto) : RelatorioFolhaBase(contexto)
{
    protected override string TituloRelatorio => "Encargos patronais da folha";
    protected override string NomeArquivo => $"encargos_folha_{CompGerada:yyyyMM}";

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Folha().Relatorios.EncargosAsync(empresa, Comp, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Mostrar([r.Valor]);
    }, Permissao.FolhaRelatorios);
}

/// <summary>Folha › Relatórios › Provisões: cálculo mensal das provisões de férias e 13º com encargos, e relatório.</summary>
public partial class ProvisoesFolhaViewModel(IContexto contexto) : RelatorioFolhaBase(contexto)
{
    protected override string TituloRelatorio => "Provisões de férias e 13º salário";
    protected override string NomeArquivo => $"provisoes_{CompGerada:yyyyMM}";

    public bool PodeCalcular => Contexto.Pode(Permissao.FolhaCalcular);
    public void AtualizarPermissoes() { OnPropertyChanged(nameof(PodeCalcular)); AtualizarPermissoesExportacao(); }

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Folha().Relatorios.ProvisoesAsync(empresa, Comp, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Mostrar([r.Valor]);
        if (r.Valor.Linhas.Count <= 1) Mensagem = "Não há provisões calculadas nesta competência: clique em \"Calcular provisões\".";
    }, Permissao.FolhaRelatorios);

    [RelayCommand]
    private Task CalcularAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Folha().Folha.CalcularProvisoesAsync(empresa, Comp, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var t = await app.Folha().Relatorios.ProvisoesAsync(empresa, Comp, usuario);
        if (t.Sucesso) Mostrar([t.Valor]);
        foreach (var a in r.Valor.Avisos) Erros.Add(a);
        Mensagem = $"Provisões calculadas para {r.Valor.Calculados} funcionário(s): {Vorticis.Domain.Comum.Formato.Moeda(r.Valor.Liquido)} com encargos.";
    }, Permissao.FolhaCalcular);
}

/// <summary>Cadastros › Tabelas legais › Tabelas da previdência social: INSS, teto, salário-família, salário mínimo, FGTS e IRRF vigentes na competência.</summary>
public partial class TabelasPrevidenciaViewModel(IContexto contexto) : RelatorioFolhaBase(contexto)
{
    protected override string TituloRelatorio => Vorticis.Folha.Regras.TabelasPrevidencia.Titulo;
    protected override string NomeArquivo => $"tabelas_previdencia_{CompGerada:yyyyMM}";
    protected override bool Paisagem => false;

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var legais = await app.Folha().Cadastros.ListarTabelasLegaisAsync(empresa, usuario);
        try { Mostrar([Vorticis.Folha.Regras.TabelasPrevidencia.Montar(Comp, legais)]); }
        catch (InvalidOperationException ex) { Limpar(); Erros.Add(ex.Message); }
    });
}

/// <summary>
/// Obrigações › FGTS Digital: demonstrativo do FGTS da competência (por trabalhador, dos cálculos da folha) para conferir com a guia emitida
/// no portal do FGTS Digital; registro da guia (com a competência fechada) e do pagamento.
/// </summary>
public partial class FgtsDigitalViewModel(IContexto contexto) : RelatorioFolhaBase(contexto)
{
    [ObservableProperty] private string _situacao = "";
    [ObservableProperty] private string _identificador = "";
    [ObservableProperty] private DateTime? _dataPagamento = DateTime.Today;
    [ObservableProperty] private string _valorPagoTexto = "";

    public bool PodeRegistrar => Contexto.Pode(Permissao.FolhaCalcular);
    public void AtualizarPermissoes() { OnPropertyChanged(nameof(PodeRegistrar)); AtualizarPermissoesExportacao(); }

    protected override string TituloRelatorio => "FGTS Digital";
    protected override string NomeArquivo => $"fgts_digital_{CompGerada:yyyyMM}";
    protected override bool Paisagem => false;

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Folha().FgtsDigital.DemonstrativoAsync(empresa, Comp, usuario);
        if (!r.Sucesso) { Limpar(); MostrarResultado(r, ""); return; }
        var d = r.Valor!;
        Mostrar([d.Tabela()]);
        Situacao = d.Situacao;
        Identificador = d.Guia?.Identificador ?? "";
        ValorPagoTexto = Numeros.Moeda(d.Guia?.ValorPago ?? d.Guia?.Valor ?? d.Total);
    });

    [RelayCommand]
    private Task RegistrarGuiaAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Folha().FgtsDigital.RegistrarGuiaAsync(empresa, Comp, Identificador, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await GerarAsync();
        Mensagem = "Guia do FGTS Digital registrada.";
    }, Permissao.FolhaCalcular);

    [RelayCommand]
    private Task RegistrarPagamentoAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (FolhaExtensoes.Data(DataPagamento) is not { } data) { Erros.Add("Informe a data do pagamento."); return; }
        var r = await app.Folha().FgtsDigital.RegistrarPagamentoAsync(empresa, Comp, data, Numeros.LerValor(ValorPagoTexto) ?? 0, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await GerarAsync();
        Mensagem = "Pagamento do FGTS registrado.";
    }, Permissao.FolhaCalcular);

    [RelayCommand]
    private Task ExcluirGuiaAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (!await Contexto.ConfirmarAsync("Excluir guia", "Excluir o registro da guia do FGTS Digital desta competência?", "Excluir", "Cancelar")) return;
        var r = await app.Folha().FgtsDigital.ExcluirGuiaAsync(empresa, Comp, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await GerarAsync();
        Mensagem = "Registro da guia excluído.";
    }, Permissao.FolhaCalcular);
}

/// <summary>
/// Folha › Relatórios › Informe de rendimentos: valores do comprovante anual de cada funcionário (IN RFB 2.060/2021) na tela,
/// com exportação, e os comprovantes em PDF para entregar (de todos ou de um funcionário).
/// </summary>
public partial class InformeRendimentosViewModel(IContexto contexto) : RelatorioViewModel(contexto)
{
    [ObservableProperty] private decimal? _ano = contexto.PeriodoTrabalho.Year - (contexto.PeriodoTrabalho.Month <= 6 ? 1 : 0);
    [ObservableProperty] private TabelaRelatorio? _tabela;
    [ObservableProperty] private OpcaoFuncionarioHolerite? _funcionario;
    public System.Collections.ObjectModel.ObservableCollection<OpcaoFuncionarioHolerite> Funcionarios { get; } = [new(null, "Todos os funcionários")];

    private int AnoGerado { get; set; }
    private int AnoEscolhido => (int)(Ano ?? 0);

    protected override string TituloRelatorio => "Informe de rendimentos";
    protected override string NomeArquivo => $"informe_rendimentos_{AnoGerado}";
    protected override string Parametros => $"Ano-calendário {AnoGerado}";
    protected override bool Paisagem => true;
    protected override IReadOnlyList<TabelaRelatorio> Tabelas() => Tabela is null ? [] : [Tabela];

    public void Limpar()
    {
        Tabela = null; TemDados = false;
        while (Funcionarios.Count > 1) Funcionarios.RemoveAt(1);
        Funcionario = Funcionarios[0];
    }

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Folha().Relatorios.InformesRendimentosAsync(empresa, AnoEscolhido, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Limpar();
        AnoGerado = AnoEscolhido;
        foreach (var i in r.Valor) Funcionarios.Add(new(i.FuncionarioId, $"{i.Matricula} – {i.Nome}"));
        Tabela = Vorticis.Folha.Servicos.ServicoRelatoriosFolha.TabelaInformes(r.Valor, AnoGerado);
        TemDados = r.Valor.Count > 0;
        AtualizarPermissoesExportacao();
        Mensagem = r.Valor.Count == 0 ? $"Não há rendimentos pagos em {AnoGerado}." : $"{r.Valor.Count} funcionário(s) com rendimentos em {AnoGerado}.";
    }, Permissao.FolhaRelatorios);

    [RelayCommand]
    private Task ComprovantesAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var id = Funcionario?.Id;
        var r = await app.Folha().Relatorios.ComprovantesRendimentosAsync(empresa, AnoEscolhido, usuario, funcionarioId: id);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var sufixo = id is null ? "" : "_" + new string([.. Funcionario!.Texto.TakeWhile(c => c != ' ')]);
        Mensagem = "Comprovantes gerados: " + ArquivosGerados.Salvar($"comprovante_rendimentos_{AnoEscolhido}{sufixo}.pdf", r.Valor.Conteudo);
    }, Permissao.FolhaRelatorios);
}

/// <summary>Folha › Relatórios › Vencimentos de contratos e férias: prazos de experiência/contratos a termo e férias a conceder.</summary>
public partial class VencimentosPessoalViewModel(IContexto contexto) : RelatorioViewModel(contexto)
{
    [ObservableProperty] private decimal? _dias = 60;
    [ObservableProperty] private TabelaRelatorio? _tabela;
    private DateOnly _hoje;

    protected override string TituloRelatorio => "Vencimentos de contratos e férias";
    protected override string NomeArquivo => $"vencimentos_pessoal_{_hoje:yyyyMMdd}";
    protected override string Parametros => $"Prazos até {_hoje.AddDays((int)(Dias ?? 0)):dd/MM/yyyy}";
    protected override bool Paisagem => true;
    protected override IReadOnlyList<TabelaRelatorio> Tabelas() => Tabela is null ? [] : [Tabela];

    public void Limpar() { Tabela = null; TemDados = false; }

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        _hoje = DateOnly.FromDateTime(DateTime.Today);
        var r = await app.Folha().Relatorios.VencimentosAsync(empresa, _hoje, (int)(Dias ?? 60), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Tabela = r.Valor;
        TemDados = r.Valor.Linhas.Count > 0;
        AtualizarPermissoesExportacao();
        Mensagem = r.Valor.Linhas.Count == 0 ? "Nenhum contrato ou férias vencendo no prazo escolhido." : r.Valor.Rodape ?? "";
    }, Permissao.FolhaRelatorios);
}

/// <summary>Folha › Relatórios › Admitidos e desligados: movimentação de pessoal do período e rotatividade.</summary>
public partial class AdmitidosDesligadosViewModel(IContexto contexto) : RelatorioViewModel(contexto)
{
    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private DateTime? _fim = DateTime.Today;
    [ObservableProperty] private TabelaRelatorio? _tabela;
    [ObservableProperty] private TabelaRelatorio? _tabela2;
    private (DateOnly Ini, DateOnly Fim) _periodo;

    protected override string TituloRelatorio => "Admitidos e desligados";
    protected override string NomeArquivo => $"admitidos_desligados_{_periodo.Ini:yyyyMMdd}_{_periodo.Fim:yyyyMMdd}";
    protected override string Parametros => $"Período: {_periodo.Ini:dd/MM/yyyy} a {_periodo.Fim:dd/MM/yyyy}";
    protected override bool Paisagem => true;
    protected override IReadOnlyList<TabelaRelatorio> Tabelas() => new[] { Tabela, Tabela2 }.OfType<TabelaRelatorio>().ToArray();

    public void Limpar() { Tabela = Tabela2 = null; TemDados = false; }

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var ini = DateOnly.FromDateTime(Inicio ?? new DateTime(DateTime.Today.Year, 1, 1));
        var fim = DateOnly.FromDateTime(Fim ?? DateTime.Today);
        var r = await app.Folha().Relatorios.AdmitidosDesligadosAsync(empresa, ini, fim, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        _periodo = (ini, fim);
        (Tabela, Tabela2) = (r.Valor[0], r.Valor[1]);
        TemDados = true;
        AtualizarPermissoesExportacao();
        Mensagem = r.Valor[1].Rodape ?? "";
    }, Permissao.FolhaRelatorios);
}

/// <summary>Obrigações › Conferência do INSS (DCTFWeb): contribuições da competência por apuração e por funcionário.</summary>
public partial class ConferenciaInssViewModel(IContexto contexto) : RelatorioFolhaBase(contexto)
{
    protected override string TituloRelatorio => "Conferência do INSS para a DCTFWeb";
    protected override string NomeArquivo => $"conferencia_inss_{CompGerada:yyyyMM}";

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Folha().Relatorios.ConferenciaInssAsync(empresa, Comp, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Mostrar(r.Valor);
        Mensagem = r.Valor[0].Linhas.Count == 0 ? "Não há folha calculada nesta competência." : r.Valor[0].Rodape ?? "";
    }, Permissao.FolhaRelatorios);
}
