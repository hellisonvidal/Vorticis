using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Desktop.Comum.Modulos;

namespace Vorticis.Desktop.Comum.ViewModels;

/// <summary>Temas de navegação da Suite (o usuário escolhe em Exibir › Layout da Suite ou no botão Layout da barra superior).</summary>
public enum LayoutSuite
{
    /// <summary>A: clássico, com a faixa de abas dos módulos (ícone e nome) no topo e os menus do módulo abaixo.</summary>
    Abas,
    /// <summary>B (padrão): barra lateral recolhível; ícones dos módulos numa coluna estreita e, ao expandir, as funções do módulo agrupadas.</summary>
    Lateral,
    /// <summary>C: navegador em árvore com todos os módulos, busca instantânea, favoritos e recentes.</summary>
    Arvore,
    /// <summary>D: painel inicial (launcher) com um cartão grande por módulo.</summary>
    Painel,
    /// <summary>E: faixa de opções (ribbon): abas por módulo e grupos de botões grandes por categoria.</summary>
    Faixa,
}

/// <summary>Uma das opções do seletor de layouts (letra, nome e descrição curta, com a miniatura desenhada pela tela).</summary>
public sealed record OpcaoLayout(LayoutSuite Valor, string Letra, string Nome, string Descricao);

/// <summary>Um módulo dentro da Suite: o ViewModel completo do módulo e os dados para os botões de troca.</summary>
public sealed partial class ModuloSuite(MainViewModelBase vm) : ObservableObject
{
    public MainViewModelBase Vm { get; } = vm;
    public string Nome => Vm.Modulo.NomeCurto;
    public string NomeCompleto => Vm.Modulo.Nome;
    public string Subtitulo => Vm.Modulo.Subtitulo;
    public IBrush Cor => Vm.PincelDestaque;
    public IBrush CorBarra => Vm.PincelBarra;

    /// <summary>Ícone vetorial do módulo (na cor de destaque dele).</summary>
    public Geometry Icone => Views.IconesSuite.DoModulo(Vm.Modulo.NomeExecutavel);

    /// <summary>Sigla do módulo (C, F, FP, E, G, CD), usada como texto alternativo.</summary>
    public string Sigla => Vm.Modulo.NomeExecutavel switch
    {
        "VorticisContabil" => "C",
        "VorticisFiscal" => "F",
        "VorticisFolha" => "FP",
        "VorticisEscritorio" => "E",
        "VorticisGerencial" => "G",
        "VorticisCondominio" => "CD",
        _ => Nome[..1],
    };

    [ObservableProperty] private bool _ativo;

    /// <summary>Raiz da árvore de funções do módulo (categorias, submenus e telas).</summary>
    [ObservableProperty] private NoSuite _raiz = null!;

    /// <summary>As mesmas funções em lista plana, com os títulos das categorias como cabeçalhos (barra lateral B).</summary>
    [ObservableProperty] private IReadOnlyList<NoSuite> _linhas = [];

    /// <summary>Funções mais usadas pelo usuário neste módulo; completa com os atalhos de teclado do módulo (cartão do painel D).</summary>
    [ObservableProperty] private IReadOnlyList<NoSuite> _atalhos = [];

    /// <summary>Funções abertas no módulo (selo no ícone).</summary>
    public int Abertas => Vm.JanelasAbertas.Count;
    public bool TemAbertas => Abertas > 0;

    internal void AvisarAbertas()
    {
        OnPropertyChanged(nameof(Abertas));
        OnPropertyChanged(nameof(TemAbertas));
    }
}

/// <summary>Nó da árvore de navegação: módulo, categoria, submenu ou função.</summary>
public sealed partial class NoSuite(string titulo, ModuloSuite modulo, string? chave, IReadOnlyList<NoSuite> filhos, string caminho,
                                    Func<Geometry>? icone = null, int nivel = 0) : ObservableObject
{
    public string Titulo { get; } = titulo;
    public ModuloSuite Modulo { get; } = modulo;
    public string? Chave { get; } = chave;
    public IReadOnlyList<NoSuite> Filhos { get; } = filhos;

    /// <summary>Módulo › menu › função (resultado da busca).</summary>
    public string Caminho { get; } = caminho;
    private Geometry? _geometria;

    /// <summary>Ícone da categoria ou do módulo (desenhado só quando a tela pede).</summary>
    public Geometry? Icone => _geometria ??= icone?.Invoke();
    public int Nivel { get; } = nivel;
    public bool EhModulo => Chave is null && Caminho == Modulo.Nome;
    public bool EhFuncao => Chave is not null;
    public bool EhGrupo => Chave is null && !EhModulo;
    public Thickness Recuo => new(Nivel * 12, 0, 0, 0);

    /// <summary>Identificador estável (módulo e tela), usado nos favoritos, recentes e contagem de uso.</summary>
    public string Id => $"{Modulo.Vm.Modulo.NomeExecutavel}|{Chave}";

    /// <summary>Cor do ícone: a do módulo (módulo e funções) ou um cinza-azulado (categorias).</summary>
    public IBrush CorIcone => EhGrupo ? Brushes.SlateGray : Modulo.Cor;

    [ObservableProperty] private bool _favorito;
    public override string ToString() => Caminho;
}

/// <summary>
/// Suite: todos os módulos instalados na mesma janela. Cada módulo continua com o seu ViewModel (menus, telas, empresa
/// ativa); o login é feito uma vez, no primeiro módulo, e a sessão vale para todos. Sair em qualquer módulo sai de todos.
/// </summary>
public sealed partial class SuiteViewModel : ObservableObject
{
    private bool _propagando;
    private bool _carregando;

    /// <param name="nome">Nome do produto na janela e no menu (Vorticis Suite Empresarial).</param>
    /// <param name="principal">Executável do módulo que conecta e faz o login; sem ele, o que administra a suíte (Escritório) ou o primeiro.</param>
    public SuiteViewModel(IReadOnlyList<MainViewModelBase> modulos, LayoutSuite? layout = null, string nome = "Vorticis Suite Empresarial", string? principal = null)
    {
        Nome = nome;
        if (modulos.Count == 0) throw new ArgumentException("A Suite precisa de pelo menos um módulo.", nameof(modulos));
        // Em ordem alfabética em todos os layouts (abas, barra lateral, árvore, painel, faixa e Ctrl+1, Ctrl+2...)
        var ordem = StringComparer.Create(Domain.Comum.Formato.PtBr, ignoreCase: true);
        Modulos = [.. modulos.Select(m => new ModuloSuite(m)).OrderBy(m => m.Nome, ordem)];
        // Quem conecta e faz o login: o módulo que administra a suíte (Escritório), senão o primeiro da lista
        _principal = Modulos.FirstOrDefault(m => principal is not null && m.Vm.Modulo.NomeExecutavel == principal)
                     ?? Modulos.FirstOrDefault(m => m.Vm.Modulo.GerenciaSistema) ?? Modulos[0];
        _carregando = true;
        _layout = layout ?? LerLayout();
        _lateralFixa = LerFixa();
        _faixaFixa = LerFaixaFixa();
        _carregando = false;
        foreach (var m in Modulos)
        {
            if (!ReferenceEquals(m, _principal)) m.Vm.DelegarReinicio = msg => _principal.Vm.ReiniciarDoZeroAsync(msg);   // limpar o banco: recomeça pelo principal
            m.Vm.PerguntarAtivacaoAoEntrar = false;   // na Suite cada módulo mostra as empresas dele, sem perguntar
            m.Vm.SessaoPronta += async (_, _) => await PropagarSessaoAsync(m.Vm);
            m.Vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModelBase.Logado) && !m.Vm.Logado) SairDeTodos();
                if (e.PropertyName == nameof(MainViewModelBase.Logado))
                {
                    if (ReferenceEquals(m, _principal) && m.Vm.Logado) CarregarPreferenciasDoUsuario();
                    OnPropertyChanged(nameof(Logado)); OnPropertyChanged(nameof(MostraPainel)); OnPropertyChanged(nameof(Saudacao));
                }
                if (e.PropertyName == nameof(MainViewModelBase.RecursosAtivos)) Reconstruir(m);
            };
            m.Vm.MenusAlterados += (_, _) => Reconstruir(m);
            m.Vm.JanelasAbertas.CollectionChanged += (_, _) => m.AvisarAbertas();
        }
        _ativo = _principal;
        _ativo.Ativo = true;
        foreach (var m in Modulos) Reconstruir(m);
    }

    public ObservableCollection<ModuloSuite> Modulos { get; }

    /// <summary>Nome do produto (título da janela e marca do menu).</summary>
    public string Nome { get; }

    /// <summary>Módulo que faz a conexão e o login (o Escritório, quando instalado; senão, o primeiro da lista).</summary>
    private readonly ModuloSuite _principal;

    public MainViewModelBase Principal => _principal.Vm;

    [ObservableProperty] private ModuloSuite _ativo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EhAbas), nameof(EhLateral), nameof(EhArvore), nameof(EhPainel), nameof(EhFaixa), nameof(MostraPainel), nameof(NomeLayout),
                              nameof(NavLateral), nameof(MostraNavFixa), nameof(MostraRail), nameof(NavFlutuanteAberta), nameof(MostraAbas), nameof(MostraCorpoFaixa),
                              nameof(FaixaFlutuanteAberta), nameof(AbasCompactas), nameof(TextoFixarFaixa), nameof(IconeFixarFaixa))]
    private LayoutSuite _layout;

    /// <summary>Layout D: o painel inicial com os cartões dos módulos está à mostra (em vez das telas do módulo).</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(MostraPainel))] private bool _noPainel = true;

    // ------------------------------------------------------------------ navegação à esquerda que se esconde (B e C)

    /// <summary>
    /// A barra lateral (B) e a árvore (C) ficam por padrão ocultas: só a faixa de ícones aparece à esquerda; ao passar o mouse ou clicar
    /// a navegação abre POR CIMA das telas e fecha ao sair ou escolher uma função. "Fixar" deixa sempre aberta, empurrando o conteúdo
    /// (gravado por usuário).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NavFlutuanteAberta))]
    private bool _lateralAberta;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostraNavFixa), nameof(MostraRail), nameof(NavFlutuanteAberta), nameof(TextoFixar), nameof(IconeFixar))]
    private bool _lateralFixa;

    /// <summary>Faixa superior (abas do layout A e faixa de opções do E) fixa; oculta, mostra só os títulos das abas e abre por cima ao passar o mouse.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostraCorpoFaixa), nameof(FaixaFlutuanteAberta), nameof(AbasCompactas), nameof(TextoFixarFaixa), nameof(IconeFixarFaixa))]
    private bool _faixaFixa = true;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FaixaFlutuanteAberta))] private bool _faixaAberta;

    public bool NavLateral => EhLateral || EhArvore;
    public bool MostraNavFixa => NavLateral && LateralFixa;
    public bool MostraRail => NavLateral && !LateralFixa;
    public bool NavFlutuanteAberta => MostraRail && LateralAberta;
    public bool MostraAbas => EhAbas || EhFaixa;
    public bool MostraCorpoFaixa => EhFaixa && FaixaFixa;
    public bool FaixaFlutuanteAberta => EhFaixa && !FaixaFixa && FaixaAberta;
    public bool AbasCompactas => EhAbas && !FaixaFixa;
    public string TextoFixar => LateralFixa ? "Ocultar" : "Fixar";
    public Geometry IconeFixar => LateralFixa ? Views.Icones.PinOff : Views.Icones.Pin;
    public string TextoFixarFaixa => FaixaFixa ? "Ocultar faixa" : "Fixar faixa";
    public Geometry IconeFixarFaixa => FaixaFixa ? Views.Icones.PinOff : Views.Icones.Pin;

    partial void OnLateralFixaChanged(bool value) { if (value) LateralAberta = false; SalvarPreferencias(); }
    partial void OnFaixaFixaChanged(bool value) { FaixaAberta = false; SalvarPreferencias(); }

    [RelayCommand]
    private void AlternarFixa() => LateralFixa = !LateralFixa;

    [RelayCommand]
    private void AlternarFaixa() => FaixaFixa = !FaixaFixa;

    public bool EhAbas => Layout == LayoutSuite.Abas;
    public bool EhLateral => Layout == LayoutSuite.Lateral;
    public bool EhArvore => Layout == LayoutSuite.Arvore;
    public bool EhPainel => Layout == LayoutSuite.Painel;
    public bool EhFaixa => Layout == LayoutSuite.Faixa;
    public bool MostraPainel => EhPainel && NoPainel && Logado;
    public bool Logado => Principal.Logado;

    public string NomeLayout => Nomes[Layout];

    public static IReadOnlyDictionary<LayoutSuite, string> Nomes { get; } = new Dictionary<LayoutSuite, string>
    {
        [LayoutSuite.Abas] = "A – Clássico com abas",
        [LayoutSuite.Lateral] = "B – Barra lateral (padrão)",
        [LayoutSuite.Arvore] = "C – Árvore com busca",
        [LayoutSuite.Painel] = "D – Painel inicial",
        [LayoutSuite.Faixa] = "E – Faixa de opções",
    };

    /// <summary>As cinco opções com a descrição mostrada no seletor.</summary>
    public static IReadOnlyList<OpcaoLayout> Opcoes { get; } =
    [
        new(LayoutSuite.Abas, "A", "Clássico com abas", "Uma aba com ícone para cada módulo no topo e os menus do módulo logo abaixo."),
        new(LayoutSuite.Lateral, "B", "Barra lateral (padrão)", "Coluna de ícones à esquerda que abre com os nomes e as funções do módulo agrupadas."),
        new(LayoutSuite.Arvore, "C", "Árvore com busca", "Todos os módulos e funções numa árvore, com busca, favoritos e recentes."),
        new(LayoutSuite.Painel, "D", "Painel inicial", "Cartões grandes por módulo, atalhos das funções mais usadas e próximos vencimentos."),
        new(LayoutSuite.Faixa, "E", "Faixa de opções", "Abas por módulo e botões grandes agrupados por categoria, como no Office."),
    ];

    /// <summary>Troca de módulo pedida pela janela (ela esconde as janelas do módulo anterior e mostra as do novo).</summary>
    public event EventHandler<ModuloSuite>? AoTrocar;

    partial void OnAtivoChanged(ModuloSuite? oldValue, ModuloSuite newValue)
    {
        if (oldValue is not null) oldValue.Ativo = false;
        newValue.Ativo = true;
        OnPropertyChanged(nameof(ModuloNoSeletor));
        OnPropertyChanged(nameof(Vencimentos));
    }

    /// <summary>Seletor "Módulo ▾" do layout D (escolher troca de módulo).</summary>
    public ModuloSuite? ModuloNoSeletor
    {
        get => Ativo;
        set => Trocar(value);
    }

    /// <summary>Saudação do painel D (bom dia/boa tarde/boa noite e o nome do usuário).</summary>
    public string Saudacao
    {
        get
        {
            var h = DateTime.Now.Hour;
            var nome = Principal.Sessao?.Nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            return (h < 12 ? "Bom dia" : h < 18 ? "Boa tarde" : "Boa noite") + (nome.Length > 0 ? $", {nome}" : "");
        }
    }

    public string Hoje => DateTime.Today.ToString("dddd, d 'de' MMMM 'de' yyyy", Domain.Comum.Formato.PtBr) is var t && t.Length > 0 ? char.ToUpper(t[0]) + t[1..] : "";

    /// <summary>Tela inicial (próximas obrigações) do módulo à frente, ou do primeiro que usa o calendário; alimenta os vencimentos do painel D.</summary>
    public InicioViewModel? Vencimentos =>
        (Ativo.Vm.Modulo.UsaCalendarioObrigacoes ? Ativo : Modulos.FirstOrDefault(m => m.Vm.Modulo.UsaCalendarioObrigacoes))?.Vm.Inicio;

    /// <summary>Usuário e empresa para o canto do painel D.</summary>
    public string UsuarioRotulo => Principal.UsuarioRotulo;

    partial void OnLayoutChanged(LayoutSuite value)
    {
        SalvarPreferencias();
        NoPainel = value == LayoutSuite.Painel;
        LateralAberta = false;
        FaixaAberta = false;
    }

    [RelayCommand]
    private void EscolherLayout(LayoutSuite layout) => Layout = layout;

    /// <summary>Vai para o módulo; a empresa escolhida continua a mesma quando ela também está liberada no módulo.</summary>
    [RelayCommand]
    public void Trocar(ModuloSuite? modulo)
    {
        if (modulo is null) return;
        NoPainel = false;
        if (ReferenceEquals(modulo, Ativo)) return;
        if (!modulo.Vm.Logado && Principal.Logado) { Status($"O {modulo.NomeCompleto} ainda está entrando com o seu usuário. Tente de novo em instantes."); return; }
        if (!Principal.Logado && !ReferenceEquals(modulo.Vm, Principal)) { Status("Entre com o seu usuário primeiro."); return; }
        var empresa = Ativo.Vm.EmpresaSelecionada?.Id;
        Ativo = modulo;
        if (empresa is { } id && modulo.Vm.EmpresasPermitidas.FirstOrDefault(e => e.Id == id) is { } mesma) modulo.Vm.EmpresaSelecionada = mesma;
        AoTrocar?.Invoke(this, modulo);
    }

    [RelayCommand]
    private void IrParaPainel() => NoPainel = true;

    /// <summary>Abre a função de um módulo (navegação lateral, árvore, busca, faixa de opções e atalhos do painel D).</summary>
    [RelayCommand]
    public void AbrirFuncao(NoSuite? no)
    {
        if (no is null) return;
        LateralAberta = false;   // a navegação que abre por cima fecha ao escolher
        FaixaAberta = false;
        Trocar(no.Modulo);
        if (no.Chave is { } chave && ReferenceEquals(Ativo, no.Modulo))
        {
            RegistrarUso(no);
            if (chave == TelasComuns.Sair) no.Modulo.Vm.SairCommand.Execute(null);
            else if (no.Modulo.Vm.AbrirCommand.CanExecute(chave)) no.Modulo.Vm.AbrirCommand.Execute(chave);
        }
    }

    private void Status(string texto) => Ativo.Vm.Status = texto;

    // ------------------------------------------------------------------ árvore, busca, favoritos e recentes

    /// <summary>Raízes (uma por módulo) da árvore do layout C.</summary>
    public IReadOnlyList<NoSuite> Arvore { get; private set; } = [];
    public ObservableCollection<NoSuite> Resultados { get; } = [];
    public ObservableCollection<NoSuite> Favoritos { get; } = [];
    public ObservableCollection<NoSuite> Recentes { get; } = [];
    public bool TemFavoritos => Favoritos.Count > 0;
    public bool TemRecentes => Recentes.Count > 0;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Buscando))] private string _busca = "";
    public bool Buscando => Busca.Trim().Length > 0;

    private readonly HashSet<string> _idsFavoritos = [];
    private readonly List<string> _idsRecentes = [];
    private readonly Dictionary<string, int> _usos = [];
    private Dictionary<string, NoSuite> _porId = [];

    /// <summary>Remonta a árvore de um módulo (menus escondidos, recursos ligados) e atualiza favoritos, recentes e atalhos.</summary>
    private void Reconstruir(ModuloSuite m)
    {
        var raiz = new NoSuite(m.Nome, m, null, [.. m.Vm.MenusVisiveis.Select(i => MontarNo(m, i, m.Nome, 0)).OfType<NoSuite>()], m.Nome, () => m.Icone);
        m.Raiz = raiz;
        m.Linhas = Planificar(raiz);
        Arvore = [.. Modulos.Select(x => x.Raiz).Where(r => r is not null)];
        OnPropertyChanged(nameof(Arvore));
        AtualizarIndices();
    }

    private void AtualizarIndices()
    {
        _porId = Modulos.Where(m => m.Raiz is not null).SelectMany(m => Folhas(m.Raiz)).GroupBy(n => n.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var n in _porId.Values) n.Favorito = _idsFavoritos.Contains(n.Id);
        Favoritos.Clear();
        foreach (var id in _idsFavoritos.Order()) if (_porId.TryGetValue(id, out var n)) Favoritos.Add(n);
        Recentes.Clear();
        foreach (var id in _idsRecentes) if (_porId.TryGetValue(id, out var n)) Recentes.Add(n);
        OnPropertyChanged(nameof(TemFavoritos));
        OnPropertyChanged(nameof(TemRecentes));
        foreach (var m in Modulos.Where(m => m.Raiz is not null)) m.Atalhos = CalcularAtalhos(m);
    }

    private static NoSuite? MontarNo(ModuloSuite m, ItemMenu item, string caminho, int nivel)
    {
        if (item.EhSeparador) return null;
        if (item.Recurso is { } r && !m.Vm.RecursoAtivo(r)) return null;
        if (item.Filhos is null && item.Chave is null) return null;   // "em breve"
        var titulo = item.Titulo.Replace("_", "");
        var meu = $"{caminho} › {titulo}";
        if (item.Filhos is null) return new NoSuite(titulo, m, item.Chave, [], meu, null, nivel);
        var filhos = item.Filhos.Select(f => MontarNo(m, f, meu, nivel + 1)).OfType<NoSuite>().ToList();
        return filhos.Count == 0 ? null : new NoSuite(titulo, m, null, filhos, meu, () => Views.IconesSuite.DaCategoria(titulo), nivel);
    }

    /// <summary>Categorias como cabeçalhos e as funções em seguida (submenus aninhados viram cabeçalhos recuados).</summary>
    private static IReadOnlyList<NoSuite> Planificar(NoSuite raiz)
    {
        var res = new List<NoSuite>();
        void Add(NoSuite n) { res.Add(n); foreach (var f in n.Filhos) Add(f); }
        foreach (var f in raiz.Filhos) Add(f);
        return res;
    }

    private static IEnumerable<NoSuite> Folhas(NoSuite no) => no.Filhos.Count == 0 ? (no.Chave is null ? [] : [no]) : no.Filhos.SelectMany(Folhas);

    partial void OnBuscaChanged(string value)
    {
        Resultados.Clear();
        var termo = Normalizar(value);
        if (termo.Length == 0) return;
        foreach (var no in Arvore.SelectMany(Folhas).Where(n => Normalizar(n.Caminho).Contains(termo)).Take(40)) Resultados.Add(no);
    }

    /// <summary>Busca sem acento e sem diferença de maiúsculas ("ferias" acha "Férias").</summary>
    public static string Normalizar(string texto)
    {
        var d = texto.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        return new string([.. d.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)]);
    }

    [RelayCommand]
    private void AlternarFavorito(NoSuite? no)
    {
        if (no is null || no.Chave is null) return;
        if (!_idsFavoritos.Remove(no.Id)) _idsFavoritos.Add(no.Id);
        AtualizarIndices();
        SalvarPreferencias();
    }

    private void RegistrarUso(NoSuite no)
    {
        _usos[no.Id] = _usos.GetValueOrDefault(no.Id) + 1;
        _idsRecentes.Remove(no.Id);
        _idsRecentes.Insert(0, no.Id);
        if (_idsRecentes.Count > 6) _idsRecentes.RemoveRange(6, _idsRecentes.Count - 6);
        AtualizarIndices();
        SalvarPreferencias();
    }

    /// <summary>Até quatro funções: as mais usadas pelo usuário no módulo e, para completar, os atalhos de teclado dele (menos o manual).</summary>
    private IReadOnlyList<NoSuite> CalcularAtalhos(ModuloSuite m)
    {
        var folhas = Folhas(m.Raiz).ToList();
        var lista = folhas.Where(n => _usos.GetValueOrDefault(n.Id) > 0).OrderByDescending(n => _usos[n.Id]).Take(4).ToList();
        foreach (var a in m.Vm.Atalhos.Where(a => a.Chave is { } c && c != TelasComuns.ManualContextual))
        {
            if (lista.Count >= 4) break;
            var no = folhas.FirstOrDefault(n => n.Chave == a.Chave) ?? new NoSuite(a.Descricao, m, a.Chave, [], $"{m.Nome} › {a.Descricao}");
            if (!lista.Contains(no)) lista.Add(no);
        }
        return lista;
    }

    // ------------------------------------------------------------------ sessão única

    /// <summary>Login concluído num módulo: os demais entram com a mesma conexão e o mesmo usuário.</summary>
    private async Task PropagarSessaoAsync(MainViewModelBase origem)
    {
        if (_propagando) return;
        _propagando = true;
        try
        {
            foreach (var m in Modulos.Where(m => !ReferenceEquals(m.Vm, origem)))
            {
                try { await m.Vm.EntrarComSessaoDeAsync(origem); }
                catch (Exception ex) { m.Vm.Status = $"Não foi possível entrar no {m.NomeCompleto}: {ex.Message}"; }
            }
        }
        finally { _propagando = false; }
        OnPropertyChanged(nameof(Logado));
        OnPropertyChanged(nameof(MostraPainel));
    }

    /// <summary>Sair (trocar de usuário) em qualquer módulo encerra a sessão em todos e volta ao login do principal.</summary>
    private void SairDeTodos()
    {
        if (_propagando) return;
        _propagando = true;
        try
        {
            foreach (var m in Modulos) m.Vm.SairDaSessao();
        }
        finally { _propagando = false; }
        _usuarioAtual = null;
        if (!ReferenceEquals(Ativo.Vm, Principal)) { Ativo = _principal; AoTrocar?.Invoke(this, Ativo); }
        NoPainel = EhPainel;
        OnPropertyChanged(nameof(MostraPainel));
    }

    // ------------------------------------------------------------------ preferências (por usuário; no computador enquanto ninguém entrou)

    /// <summary>Arquivo da última escolha neste computador (vale na tela de entrada); substituível nos testes. As preferências de cada usuário ficam ao lado.</summary>
    public static string ArquivoLayout { get; set; } = Path.Combine(Configuracao.PastaLocal, "suite-layout.txt");

    public static string ArquivoFixa => Path.ChangeExtension(ArquivoLayout, null) + "-fixa.txt";
    public static string ArquivoFaixa => Path.ChangeExtension(ArquivoLayout, null) + "-faixa.txt";

    /// <summary>Arquivo das preferências do usuário (layout, fixar/ocultar, favoritos, recentes e uso).</summary>
    public static string ArquivoDoUsuario(long usuario) => Path.ChangeExtension(ArquivoLayout, null) + $"-u{usuario}.json";

    private sealed class PreferenciasGravadas
    {
        public LayoutSuite Layout { get; set; } = LayoutSuite.Lateral;
        public bool NavFixa { get; set; }
        public bool FaixaFixa { get; set; } = true;
        public List<string> Favoritos { get; set; } = [];
        public List<string> Recentes { get; set; } = [];
        public Dictionary<string, int> Usos { get; set; } = [];
    }

    public static LayoutSuite LerLayout()
    {
        try
        {
            return File.Exists(ArquivoLayout) && Enum.TryParse<LayoutSuite>(File.ReadAllText(ArquivoLayout).Trim(), out var l) && Enum.IsDefined(l) ? l : LayoutSuite.Lateral;
        }
        catch (Exception) { return LayoutSuite.Lateral; }
    }

    private static bool LerFixa()
    {
        try { return File.Exists(ArquivoFixa) && File.ReadAllText(ArquivoFixa).Trim() == "fixa"; }
        catch (Exception) { return false; }
    }

    private static bool LerFaixaFixa()
    {
        try { return !File.Exists(ArquivoFaixa) || File.ReadAllText(ArquivoFaixa).Trim() != "oculta"; }
        catch (Exception) { return true; }
    }

    /// <summary>Quem entrou (preferências próprias); null antes do login.</summary>
    private long? UsuarioId => _usuarioAtual ?? (Principal.Logado ? Principal.Sessao?.Id : null);
    private long? _usuarioAtual;

    /// <summary>Ao entrar, aplica as preferências gravadas para o usuário (sem arquivo ainda, vale a última escolha do computador).</summary>
    public void CarregarPreferenciasDoUsuario()
    {
        if (UsuarioId is { } id) UsarPreferenciasDe(id);
    }

    /// <summary>Passa a usar (e gravar) as preferências deste usuário: layout, fixar/ocultar, favoritos, recentes e uso.</summary>
    public void UsarPreferenciasDe(long id)
    {
        _usuarioAtual = id;
        try
        {
            var arquivo = ArquivoDoUsuario(id);
            if (!File.Exists(arquivo)) { SalvarPreferencias(); return; }
            var p = System.Text.Json.JsonSerializer.Deserialize<PreferenciasGravadas>(File.ReadAllText(arquivo)) ?? new();
            _carregando = true;
            try
            {
                _idsFavoritos.Clear(); _idsFavoritos.UnionWith(p.Favoritos);
                _idsRecentes.Clear(); _idsRecentes.AddRange(p.Recentes.Take(6));
                _usos.Clear(); foreach (var (k, v) in p.Usos) _usos[k] = v;
                LateralFixa = p.NavFixa;
                FaixaFixa = p.FaixaFixa;
                Layout = Enum.IsDefined(p.Layout) ? p.Layout : LayoutSuite.Lateral;
                AtualizarIndices();
            }
            finally { _carregando = false; }
            SalvarPreferencias(apenasComputador: true);
        }
        catch (Exception) { _carregando = false; /* arquivo ilegível: continua com o que está na tela */ }
    }

    private void SalvarPreferencias(bool apenasComputador = false)
    {
        if (_carregando) return;
        GravarTexto(ArquivoLayout, Layout.ToString());
        GravarTexto(ArquivoFixa, LateralFixa ? "fixa" : "recolhe");
        GravarTexto(ArquivoFaixa, FaixaFixa ? "fixa" : "oculta");
        if (apenasComputador || UsuarioId is not { } id) return;
        var p = new PreferenciasGravadas
        {
            Layout = Layout, NavFixa = LateralFixa, FaixaFixa = FaixaFixa,
            Favoritos = [.. _idsFavoritos.Order()], Recentes = [.. _idsRecentes], Usos = new(_usos),
        };
        GravarTexto(ArquivoDoUsuario(id), System.Text.Json.JsonSerializer.Serialize(p));
    }

    private static void GravarTexto(string arquivo, string texto)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
            File.WriteAllText(arquivo, texto);
        }
        catch (Exception) { /* sem gravar a preferência, ela vale só nesta execução */ }
    }
}
