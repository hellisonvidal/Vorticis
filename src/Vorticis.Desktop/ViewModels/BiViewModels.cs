using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Relatorios;
using Vorticis.Domain.Contabil;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra.Relatorios;

namespace Vorticis.Desktop.ViewModels;

/// <summary>Cartão do painel: indicador, valor do período e variação sobre o período anterior ("bom", "ruim" ou "neutro" pinta a variação).</summary>
public sealed record CartaoIndicador(string Titulo, string Valor, string Variacao, string Situacao, string Anterior)
{
    public bool Bom => Situacao == "bom";
    public bool Ruim => Situacao == "ruim";
}

/// <summary>Formatação dos indicadores para tela e exportação.</summary>
public static class FormatoBi
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Valor(Indicador i, decimal? v) => v is null ? "—" : i.Formato switch
    {
        FormatoIndicador.Moeda => "R$ " + Numeros.Moeda(v.Value),
        FormatoIndicador.Percentual => v.Value.ToString("N2", PtBr) + "%",
        FormatoIndicador.Dias => v.Value.ToString("N0", PtBr) + (Math.Round(v.Value) == 1 ? " dia" : " dias"),
        _ => v.Value.ToString("N2", PtBr),
    };

    /// <summary>Variação sobre o período anterior: % para valores em R$; diferença (p.p., dias ou pontos) para os demais.</summary>
    public static (string Texto, string Situacao) Variacao(Indicador atual, Indicador? anterior)
    {
        if (atual.Valor is not { } a || anterior?.Valor is not { } b) return ("sem base de comparação", "neutro");
        decimal delta;
        string texto;
        if (atual.Formato == FormatoIndicador.Moeda)
        {
            if (b == 0) return ("sem base de comparação", "neutro");
            delta = a - b;
            texto = $"{Math.Abs(delta / Math.Abs(b) * 100):N1}%";
        }
        else
        {
            delta = a - b;
            texto = atual.Formato switch
            {
                FormatoIndicador.Percentual => $"{Math.Abs(delta):N2} p.p.",
                FormatoIndicador.Dias => Math.Abs(delta) == 1 ? "1 dia" : $"{Math.Abs(delta):N0} dias",
                _ => $"{Math.Abs(delta):N2}",
            };
        }
        if (delta == 0) return ("igual ao período anterior", "neutro");
        var subiu = delta > 0;
        var bom = subiu == atual.MaiorEMelhor;
        return ($"{(subiu ? "▲" : "▼")} {texto} vs. período anterior", bom ? "bom" : "ruim");
    }
}

/// <summary>Relatórios › BI › Painel de indicadores: cartões e gráficos do período, com variação sobre o período anterior.</summary>
public sealed partial class PainelBiViewModel(IContexto contexto) : RelatorioViewModel(contexto)
{
    private static readonly string[] Cartoes_ = ["receita", "despesas", "lucro", "ebitda", "margem_ebitda", "margem_liquida", "liq_corrente", "liq_seca", "liq_geral",
                                                 "endividamento", "roe", "roa", "giro_ativo", "pmr", "pmp", "pme"];
    private TabelaRelatorio? _tabela;
    private string _parametros = "";

    public ObservableCollection<CartaoIndicador> Cartoes { get; } = [];

    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private DateTime? _fim = DateTime.Today;
    [ObservableProperty] private string _resumo = "";
    [ObservableProperty] private IReadOnlyList<string> _rotulosMeses = [];
    [ObservableProperty] private IReadOnlyList<SerieGrafico> _seriesMensais = [];
    [ObservableProperty] private IReadOnlyList<ItemGrafico> _despesasGrupo = [];
    [ObservableProperty] private IReadOnlyList<ItemGrafico> _custosCentro = [];
    [ObservableProperty] private IReadOnlyList<ItemCascata> _cascata = [];

    protected override string TituloRelatorio => "Painel de indicadores";
    protected override string Parametros => _parametros;
    protected override string NomeArquivo => "painel_indicadores_" + new string(_parametros.Where(char.IsDigit).ToArray());
    protected override IReadOnlyList<TabelaRelatorio> Tabelas() => _tabela is null ? [] : [_tabela];

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var ini = Data(Inicio, new DateOnly(DateTime.Today.Year, 1, 1));
        var fim = Data(Fim, DateOnly.FromDateTime(DateTime.Today));
        var r = await app.Bi.PainelAsync(empresa, ini, fim, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var p = r.Valor;
        _parametros = $"Período: {ini:dd/MM/yyyy} a {fim:dd/MM/yyyy} | Comparado com {p.InicioAnterior:dd/MM/yyyy} a {p.FimAnterior:dd/MM/yyyy}";

        Cartoes.Clear();
        var linhas = new List<LinhaRelatorio>();
        string? grupo = null;
        foreach (var i in p.Atual)
        {
            var ant = p.ObterAnterior(i.Chave);
            var (texto, situacao) = FormatoBi.Variacao(i, ant);
            if (Cartoes_.Contains(i.Chave) && i.Valor is not null || i.Chave is "receita" or "despesas" or "lucro" or "ebitda")
                Cartoes.Add(new CartaoIndicador(i.Nome, FormatoBi.Valor(i, i.Valor), texto, situacao, "Anterior: " + FormatoBi.Valor(i, ant?.Valor)));
            if (i.Grupo != grupo) { grupo = i.Grupo; linhas.Add(new LinhaRelatorio([grupo, "", "", ""], true)); }
            linhas.Add(new LinhaRelatorio([i.Nome, FormatoBi.Valor(i, i.Valor), FormatoBi.Valor(i, ant?.Valor), texto.TrimStart('▲', '▼', ' ')]));
        }
        _tabela = new TabelaRelatorio("Painel de indicadores", [new("Indicador", LarguraCm: 7), new("Período", TipoColuna.Texto, 3.5), new("Período anterior", TipoColuna.Texto, 3.5), new("Variação", TipoColuna.Texto, 5)],
                                      linhas, "Fórmulas e interpretação de cada indicador em Relatórios › BI › Análise de indicadores.");

        RotulosMeses = p.Meses.Select(m => m.Mes.ToString("MM/yy", CultureInfo.InvariantCulture)).ToList();
        SeriesMensais =
        [
            new SerieGrafico("Receita", p.Meses.Select(m => (double)m.Receita).ToList(), 0),
            new SerieGrafico("Lucro", p.Meses.Select(m => (double)m.Lucro).ToList(), 2),
        ];
        DespesasGrupo = p.DespesasPorGrupo.Select(x => new ItemGrafico(x.Rotulo, (double)x.Valor)).ToList();
        CustosCentro = p.CustosPorCentro.Select(x => new ItemGrafico(x.Rotulo, (double)x.Valor)).ToList();
        Cascata = p.Cascata.Select(x => new ItemCascata(x.Rotulo, (double)x.Valor, x.Total)).ToList();
        Resumo = p.Meses.All(m => m.Receita == 0 && m.Despesas == 0) ? "Nenhum lançamento de receita ou despesa nos últimos 12 meses." : "";
        TemDados = true;
        AtualizarPermissoesExportacao();
    }, Permissao.RelatorioDemonstracoes);
}

/// <summary>Relatórios › BI › Análise de indicadores: tabela com fórmula, interpretação e comparativo de períodos.</summary>
public sealed partial class AnaliseIndicadoresViewModel(IContexto contexto) : RelatorioViewModel(contexto)
{
    private string _parametros = "";

    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private DateTime? _fim = DateTime.Today;
    [ObservableProperty] private string _resumo = "";

    protected override string TituloRelatorio => "Análise de indicadores";
    protected override string Parametros => _parametros;
    protected override bool Paisagem => true;
    protected override string NomeArquivo => "analise_indicadores_" + new string(_parametros.Where(char.IsDigit).Take(16).ToArray());
    protected override IReadOnlyList<TabelaRelatorio> Tabelas() => TabelaTela is { } t ? [t] : [];

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var ini = Data(Inicio, new DateOnly(DateTime.Today.Year, 1, 1));
        var fim = Data(Fim, DateOnly.FromDateTime(DateTime.Today));
        _parametros = $"Período: {ini:dd/MM/yyyy} a {fim:dd/MM/yyyy}";
        Resumo = "";
        if (await TentarComparativoAsync(ini, fim, false, op => app.Bi.AnaliseAsync(empresa, ini, fim, op, usuario))) return;
        var r = await app.Bi.AnaliseAsync(empresa, ini, fim, new OpcoesComparativo(), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        TabelaTela = r.Valor;
        Resumo = r.Valor.Rodape ?? "";
        TemDados = true;
        AtualizarPermissoesExportacao();
    }, Permissao.RelatorioDemonstracoes);
}

/// <summary>Linha de opção das listas do cubo.</summary>
public sealed record OpcaoCubo<T>(T Valor, string Texto);

/// <summary>Relatórios › BI › Cubo (tabela dinâmica): escolha de linhas e colunas, soma e detalhamento dos lançamentos ao clicar numa célula.</summary>
public sealed partial class CuboBiViewModel(IContexto contexto) : RelatorioViewModel(contexto)
{
    private DadosBi? _dados;
    private ConsultaCubo? _consulta;
    private CuboResultado? _cubo;
    private string _parametros = "";
    private TabelaRelatorio? _detalhe;

    private static readonly IReadOnlyList<OpcaoCubo<DimensaoCubo>> _dimLinha =
        new[] { DimensaoCubo.Conta, DimensaoCubo.Grupo, DimensaoCubo.CentroCusto, DimensaoCubo.Participante, DimensaoCubo.Mes }.Select(d => new OpcaoCubo<DimensaoCubo>(d, Cubo.Nome(d))).ToList();
    private static readonly IReadOnlyList<OpcaoCubo<DimensaoCubo>> _dimColuna =
        new[] { DimensaoCubo.Mes, DimensaoCubo.Trimestre, DimensaoCubo.Ano, DimensaoCubo.CentroCusto, DimensaoCubo.Nenhuma }.Select(d => new OpcaoCubo<DimensaoCubo>(d, Cubo.Nome(d))).ToList();
    private static readonly IReadOnlyList<OpcaoCubo<MedidaCubo>> _medidas = Enum.GetValues<MedidaCubo>().Select(m => new OpcaoCubo<MedidaCubo>(m, Cubo.Nome(m))).ToList();

    public IReadOnlyList<OpcaoCubo<DimensaoCubo>> DimensoesLinha => _dimLinha;
    public IReadOnlyList<OpcaoCubo<DimensaoCubo>> DimensoesColuna => _dimColuna;
    public IReadOnlyList<OpcaoCubo<MedidaCubo>> Medidas => _medidas;

    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private DateTime? _fim = DateTime.Today;
    [ObservableProperty] private OpcaoCubo<DimensaoCubo>? _linha = _dimLinha[0];
    [ObservableProperty] private OpcaoCubo<DimensaoCubo>? _coluna = _dimColuna[0];
    [ObservableProperty] private OpcaoCubo<MedidaCubo>? _medida = _medidas[0];
    [ObservableProperty] private string _contaPrefixo = "";
    [ObservableProperty] private bool _soResultado;
    [ObservableProperty] private string _resumo = "";
    [ObservableProperty] private string _tituloDetalhe = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TemDetalhe))] private TabelaRelatorio? _tabelaDetalhe;

    public bool TemDetalhe => TabelaDetalhe is not null;

    protected override string TituloRelatorio => "Cubo — tabela dinâmica";
    protected override string Parametros => _parametros;
    protected override bool Paisagem => true;
    protected override string NomeArquivo => "cubo_" + new string(_parametros.Where(char.IsDigit).Take(16).ToArray());
    protected override IReadOnlyList<TabelaRelatorio> Tabelas() => TabelaTela is null ? [] : _detalhe is null ? [TabelaTela] : [TabelaTela, _detalhe];

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var ini = Data(Inicio, new DateOnly(DateTime.Today.Year, 1, 1));
        var fim = Data(Fim, DateOnly.FromDateTime(DateTime.Today));
        var q = new ConsultaCubo((Linha ?? DimensoesLinha[0]).Valor, (Coluna ?? DimensoesColuna[0]).Valor, (Medida ?? Medidas[0]).Valor,
                                 string.IsNullOrWhiteSpace(ContaPrefixo) ? null : ContaPrefixo.Trim(), SoResultado);
        var r = await app.Bi.CuboAsync(empresa, ini, fim, q, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        (_dados, _cubo) = r.Valor;
        _consulta = q;
        _parametros = $"Período: {ini:dd/MM/yyyy} a {fim:dd/MM/yyyy} | Linhas: {Cubo.Nome(q.Linha)} | Colunas: {Cubo.Nome(q.Coluna)} | {Cubo.Nome(q.Medida)}"
                      + (q.ContaPrefixo is { } p ? $" | Conta: {p}" : "") + (q.SoResultado ? " | Só contas de resultado" : "");
        TabelaTela = TabelaDoCubo(_cubo);
        _detalhe = null;
        TabelaDetalhe = null;
        TituloDetalhe = "";
        Resumo = _cubo.Linhas.Count == 0 ? "Nenhum lançamento no período com esses filtros." : "Clique numa célula para ver os lançamentos que formam o valor.";
        TemDados = true;
        AtualizarPermissoesExportacao();
    }, Permissao.RelatorioDemonstracoes);

    public static TabelaRelatorio TabelaDoCubo(CuboResultado c)
    {
        var colunas = new List<ColunaRelatorio> { new(c.TituloLinha) };
        colunas.AddRange(c.Colunas.Select(x => new ColunaRelatorio(x.Rotulo, TipoColuna.Valor)));
        colunas.Add(new ColunaRelatorio("Total", TipoColuna.Valor));
        var linhas = c.Linhas.Select(l => new LinhaRelatorio([l.Rotulo, .. l.Valores.Select(v => (object?)v), l.Total])).ToList();
        if (c.Linhas.Count > 0) linhas.Add(new LinhaRelatorio(["Total geral", .. c.TotaisColunas.Select(v => (object?)v), c.Total], true));
        return new TabelaRelatorio("Cubo — tabela dinâmica", colunas, linhas, "Soma da medida escolhida. Clique numa célula para listar os lançamentos.");
    }

    /// <summary>Lista os lançamentos da célula: <paramref name="linha"/> e <paramref name="coluna"/> são os índices da grade (coluna 0 = rótulos; a última = total; a linha após a última = total geral).</summary>
    public void Detalhar(int linha, int coluna)
    {
        if (_dados is null || _cubo is null || _consulta is null || linha < 0 || linha > _cubo.Linhas.Count || coluna < 0) return;
        var linhaCubo = linha < _cubo.Linhas.Count ? _cubo.Linhas[linha] : null;
        var colunaCubo = coluna >= 1 && coluna <= _cubo.Colunas.Count ? _cubo.Colunas[coluna - 1] : null;
        var itens = Cubo.Detalhe(_dados, _consulta, linhaCubo?.Chave, colunaCubo?.Chave);
        var rotulo = $"{linhaCubo?.Rotulo ?? "Todas as linhas"} | {colunaCubo?.Rotulo ?? "Todas as colunas"}";
        TituloDetalhe = $"Lançamentos de: {rotulo} ({itens.Count})";
        _detalhe = new TabelaRelatorio("Lançamentos — " + rotulo,
            [new("Data", TipoColuna.Data), new("Número", TipoColuna.Inteiro), new("Conta", LarguraCm: 6), new("Centro de custo", LarguraCm: 4.5), new("Participante", LarguraCm: 4.5), new("D/C"), new("Valor", TipoColuna.Valor), new("Histórico", LarguraCm: 8)],
            itens.Select(d => new LinhaRelatorio([d.Data, d.Numero, d.Conta, d.Centro, d.Participante, d.DC.ToString(), d.Valor, d.Historico])).ToList());
        TabelaDetalhe = _detalhe;
    }
}

/// <summary>Relatórios › BI › Exportar dados para BI: fatos e dimensões em CSV/Excel numa pasta, prontos para Power BI.</summary>
public sealed partial class ExportarBiViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public IReadOnlyList<string> Formatos { get; } = ["CSV (um arquivo por tabela)", "Excel (uma pasta de trabalho)", "CSV e Excel"];

    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year - 1, 1, 1);
    [ObservableProperty] private DateTime? _fim = DateTime.Today;
    [ObservableProperty] private string _pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Vorticis Contabil", "BI");
    [ObservableProperty] private int _formato;
    [ObservableProperty] private string _resumo = "";

    public bool PodeExportar => Contexto.Pode(Permissao.RelatorioExportar);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeExportar));

    [RelayCommand]
    private Task ExportarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        Resumo = "";
        if (string.IsNullOrWhiteSpace(Pasta)) { Erros.Add("Escolha a pasta onde os arquivos serão gravados."); return; }
        var ini = Data(Inicio, new DateOnly(DateTime.Today.Year - 1, 1, 1));
        var fim = Data(Fim, DateOnly.FromDateTime(DateTime.Today));
        var r = await app.Bi.TabelasParaBiAsync(empresa, ini, fim, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var arquivos = ExportadorBi.Gravar(Pasta.Trim(), r.Valor, (FormatoExportacaoBi)Formato);
        Mensagem = $"{arquivos.Count} arquivos gravados em {Pasta.Trim()}";
        Resumo = string.Join("; ", r.Valor.Select(t => $"{t.Titulo}: {t.Linhas.Count} linhas"));
    }, Permissao.RelatorioExportar);
}
