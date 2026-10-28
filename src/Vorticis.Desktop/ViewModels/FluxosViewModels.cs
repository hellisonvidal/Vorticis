using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Contabil;
using Vorticis.Application.Relatorios;
using Vorticis.Domain.Contabil;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Desktop.ViewModels;

/// <summary>Relatórios › Contábeis › DFC: Demonstração dos Fluxos de Caixa (CPC 03), método indireto ou direto.</summary>
public sealed partial class DfcViewModel(IContexto contexto) : RelatorioViewModel(contexto)
{
    private (DateOnly Ini, DateOnly Fim) _periodo;

    public static IReadOnlyList<short> Niveis => BalancoViewModel.Niveis;
    public IReadOnlyList<string> Metodos { get; } = ["Método indireto", "Método direto"];

    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private DateTime? _fim = new DateTime(DateTime.Today.Year, 12, 31);
    [ObservableProperty] private int _metodo;
    [ObservableProperty] private short _nivel = 3;
    [ObservableProperty] private string _resumo = "";

    protected override string TituloRelatorio => TabelasFluxos.TituloDfc;
    protected override string Parametros => $"DFC de {_periodo.Ini:dd/MM/yyyy} a {_periodo.Fim:dd/MM/yyyy} ({Metodos[Metodo].ToLowerInvariant()})";
    protected override string NomeArquivo => $"dfc_{_periodo.Ini:yyyyMMdd}_{_periodo.Fim:yyyyMMdd}";
    protected override IReadOnlyList<TabelaRelatorio> Tabelas() => TabelaTela is { } t ? [t] : [];

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var ini = Data(Inicio, new DateOnly(DateTime.Today.Year, 1, 1));
        var fim = Data(Fim, ini);
        _periodo = (ini, fim);
        var direto = Metodo == 1;
        if (await TentarComparativoAsync(ini, fim, false, op => app.Comparativos.DfcAsync(empresa, ini, fim, direto, Nivel, op, usuario))) { Resumo = ""; return; }
        var r = await app.Fluxos.DfcAsync(empresa, ini, fim, direto, Nivel, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        TabelaTela = TabelasFluxos.Dfc(r.Valor);
        Resumo = TabelasFluxos.RodapeDfc(r.Valor);
        TemDados = true;
        AtualizarPermissoesExportacao();
    }, Permissao.RelatorioDemonstracoes);
}

/// <summary>Relatórios › Contábeis › EBITDA (LAJIDA): demonstração do período, mês a mês ou por centro de custo.</summary>
public sealed partial class EbitdaViewModel(IContexto contexto) : RelatorioViewModel(contexto)
{
    private (DateOnly Ini, DateOnly Fim) _periodo;
    private int _modoGerado;

    public IReadOnlyList<string> Modos { get; } = ["Demonstração do período", "Mensal (ano da data inicial)", "Por centro de custo"];

    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private DateTime? _fim = new DateTime(DateTime.Today.Year, 12, 31);
    [ObservableProperty] private int _modo;
    [ObservableProperty] private string _resumo = "";

    protected override bool SuportaAnaliseVertical => true;
    protected override bool Paisagem => _modoGerado == 1;
    protected override string TituloRelatorio => _modoGerado switch { 1 => TabelasFluxos.TituloEbitdaMensal, 2 => TabelasFluxos.TituloEbitdaCentro, _ => TabelasFluxos.TituloEbitda };
    protected override string Parametros => _modoGerado == 1 ? $"Ano {_periodo.Ini.Year}" : $"Período: {_periodo.Ini:dd/MM/yyyy} a {_periodo.Fim:dd/MM/yyyy}";
    protected override string NomeArquivo => $"ebitda{(_modoGerado == 1 ? "_mensal" : _modoGerado == 2 ? "_centro" : "")}_{_periodo.Ini:yyyyMMdd}_{_periodo.Fim:yyyyMMdd}";
    protected override IReadOnlyList<TabelaRelatorio> Tabelas() => TabelaTela is { } t ? [t] : [];

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var ini = Data(Inicio, new DateOnly(DateTime.Today.Year, 1, 1));
        var fim = Data(Fim, ini);
        _periodo = (ini, fim);
        _modoGerado = Modo;
        if (Modo == 0 && await TentarComparativoAsync(ini, fim, false, op => app.Comparativos.EbitdaAsync(empresa, ini, fim, op, usuario))) { Resumo = ""; return; }
        TabelaComparativa = null;
        switch (Modo)
        {
            case 1:
            {
                var m = await app.Fluxos.EbitdaMensalAsync(empresa, ini.Year, usuario);
                if (!m.Sucesso) { MostrarResultado(m, ""); return; }
                _periodo = (new DateOnly(ini.Year, 1, 1), new DateOnly(ini.Year, 12, 31));
                var t = TabelasFluxos.EbitdaMensal(m.Valor);
                TabelaTela = t;
                Resumo = t.Rodape ?? "";
                break;
            }
            case 2:
            {
                var c = await app.Fluxos.EbitdaPorCentroAsync(empresa, ini, fim, usuario);
                if (!c.Sucesso) { MostrarResultado(c, ""); return; }
                var t = TabelasFluxos.EbitdaPorCentro(c.Valor);
                TabelaTela = t;
                Resumo = t.Rodape ?? "";
                break;
            }
            default:
            {
                var e = await app.Fluxos.EbitdaAsync(empresa, ini, fim, usuario);
                if (!e.Sucesso) { MostrarResultado(e, ""); return; }
                TabelaTela = TabelasFluxos.Ebitda(e.Valor);
                Resumo = TabelasFluxos.RodapeEbitda(e.Valor);
                break;
            }
        }
        if (Modo != 0 && Comparacao.Ativo) Mensagem = "O comparativo vale para a demonstração do período; esta visão já mostra vários meses ou centros.";
        TemDados = true;
        AtualizarPermissoesExportacao();
    }, Permissao.RelatorioDemonstracoes);
}

public sealed record OpcaoClasse(ClasseConta Classe, string Nome);

/// <summary>Uma conta na tela "Classificação da DFC e do EBITDA".</summary>
public sealed partial class LinhaClassificacaoEdicao : ObservableObject
{
    public LinhaClassificacaoEdicao(LinhaClassificacao l)
    {
        ContaId = l.ContaId;
        Codigo = l.Codigo;
        Descricao = l.Descricao;
        Patrimonial = l.Patrimonial;
        Sugerida = l.Sugerida;
        Opcoes = ClassesConta.Opcoes(l.Patrimonial).Select(c => new OpcaoClasse(c, c.Nome())).ToList();
        _classe = Opcoes.First(o => o.Classe == l.Classe);
        _naoRecorrente = l.NaoRecorrente;
    }

    public long ContaId { get; }
    public string Codigo { get; }
    public string Descricao { get; }
    public bool Patrimonial { get; }
    public ClasseConta Sugerida { get; }
    public IReadOnlyList<OpcaoClasse> Opcoes { get; }
    public string Tipo => Patrimonial ? "Balanço" : "Resultado";
    public string TextoSugestao => Sugerida.Nome();
    public bool PodeNaoRecorrente => !Patrimonial;
    public bool Personalizada => Classe.Classe != Sugerida;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Personalizada))] private OpcaoClasse _classe;
    [ObservableProperty] private bool _naoRecorrente;

    public void Restaurar()
    {
        Classe = Opcoes.First(o => o.Classe == Sugerida);
        NaoRecorrente = false;
    }
}

/// <summary>Relatórios › Contábeis › Classificação da DFC e do EBITDA.</summary>
public sealed partial class ClassificacaoDfcViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    private readonly List<LinhaClassificacaoEdicao> _todas = [];

    public ObservableCollection<LinhaClassificacaoEdicao> Linhas { get; } = [];

    [ObservableProperty] private string _filtro = "";
    [ObservableProperty] private bool _podeManter;

    partial void OnFiltroChanged(string value) => Aplicar();

    public void AtualizarPermissoes() => PodeManter = Contexto.Pode(Permissao.PlanoContasManter);

    private void Aplicar()
    {
        var f = SugestaoClasse.Normalizar(Filtro.Trim());
        Linhas.Clear();
        foreach (var l in _todas.Where(l => f.Length == 0 || SugestaoClasse.Normalizar(l.Codigo + " " + l.Descricao + " " + l.Classe.Nome + " " + l.Tipo).Contains(f)))
            Linhas.Add(l);
    }

    public Task CarregarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        AtualizarPermissoes();
        var r = await app.Fluxos.ListarClassificacaoAsync(empresa, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        _todas.Clear();
        _todas.AddRange(r.Valor.Select(l => new LinhaClassificacaoEdicao(l)));
        Aplicar();
    }, Permissao.RelatorioDemonstracoes);

    [RelayCommand]
    private Task RecarregarAsync() => CarregarAsync();

    [RelayCommand]
    private void RestaurarSugestao()
    {
        foreach (var l in _todas) l.Restaurar();
        Mensagem = "Sugestão automática restaurada na tela. Clique em Salvar classificação para gravar.";
    }

    [RelayCommand]
    private Task SalvarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var itens = _todas.Select(l => new ClassificacaoConta(l.ContaId, l.Classe.Classe, l.NaoRecorrente && !l.Patrimonial)).ToList();
        var r = await app.Fluxos.SalvarClassificacaoAsync(empresa, itens, usuario);
        MostrarResultado(r, "Classificação salva. A DFC e o EBITDA passam a usá-la.");
    }, Permissao.PlanoContasManter);
}
