using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>
/// Filtros nas listas: coloca, acima de cada grade de consulta, a barra "Buscar / Filtros / X de Y registros / Limpar filtros".
/// Ligado a todas as grades pelo estilo global de <c>Estilos.axaml</c> (<c>v:FiltroGrade.Ativa="True"</c>). Só entram grades somente
/// leitura (listas de registros); grades de digitação ficam de fora, a menos que marcadas com <c>v:FiltroGrade.Forcar="True"</c>
/// (listas com caixas de marcação) ou desligadas com <c>v:FiltroGrade.Ativa="False"</c>. O filtro age sobre as linhas já carregadas
/// (<c>CollectionView.Filter</c>), sem consultar o banco, e os filtros ficam lembrados por tela até fechar o programa.
/// </summary>
public static class FiltroGrade
{
    public static readonly AttachedProperty<bool> AtivaProperty = AvaloniaProperty.RegisterAttached<DataGrid, bool>("Ativa", typeof(FiltroGrade));
    public static readonly AttachedProperty<bool> ForcarProperty = AvaloniaProperty.RegisterAttached<DataGrid, bool>("Forcar", typeof(FiltroGrade));
    private static readonly AttachedProperty<BarraFiltro?> BarraProperty = AvaloniaProperty.RegisterAttached<DataGrid, BarraFiltro?>("Barra", typeof(FiltroGrade));

    public static bool GetAtiva(DataGrid g) => g.GetValue(AtivaProperty);
    public static void SetAtiva(DataGrid g, bool v) => g.SetValue(AtivaProperty, v);
    public static bool GetForcar(DataGrid g) => g.GetValue(ForcarProperty);
    public static void SetForcar(DataGrid g, bool v) => g.SetValue(ForcarProperty, v);

    /// <summary>Barra instalada acima da grade (nula se a grade não é uma lista de registros).</summary>
    public static BarraFiltro? ObterBarra(DataGrid g) => g.GetValue(BarraProperty);

    /// <summary>Filtros lembrados por tela durante a sessão.</summary>
    internal static readonly Dictionary<string, EstadoFiltro> Memoria = [];

    private static readonly ConditionalWeakTable<Control, List<BarraFiltro>> Raizes = new();

    static FiltroGrade()
    {
        AtivaProperty.Changed.AddClassHandler<DataGrid>((g, e) =>
        {
            if (e.NewValue is not true) return;
            if (g.GetVisualRoot() is not null) Agendar(g);
            else g.AttachedToVisualTree += AoAnexar;
        });
    }

    private static void AoAnexar(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not DataGrid g) return;
        g.AttachedToVisualTree -= AoAnexar;
        Agendar(g);
    }

    private static void Agendar(DataGrid g) => Dispatcher.UIThread.Post(() => Instalar(g), DispatcherPriority.Loaded);

    /// <summary>Instala a barra acima da grade, se ela for uma lista de registros. Devolve a barra (ou nulo).</summary>
    public static BarraFiltro? Instalar(DataGrid g)
    {
        if (g.GetValue(BarraProperty) is { } ja) return ja;
        if (!GetAtiva(g) || !(g.IsReadOnly || GetForcar(g))) return null;
        var pai = g.Parent;
        if (pai is null) return null;

        var raiz = (Control?)g.FindAncestorOfType<UserControl>() ?? TopLevel.GetTopLevel(g) as Control;
        var chave = $"{raiz?.GetType().FullName}|{string.Join("|", g.Columns.Select(c => TextoCabecalho(c)))}";
        if (!Memoria.TryGetValue(chave, out var estado)) Memoria[chave] = estado = new EstadoFiltro();

        var barra = new BarraFiltro(g, estado);
        var invólucro = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        // o invólucro assume a posição e o tamanho da grade no painel de origem
        foreach (var p in new AvaloniaProperty[] { Grid.RowProperty, Grid.ColumnProperty, Grid.RowSpanProperty, Grid.ColumnSpanProperty, DockPanel.DockProperty,
                     Layoutable.MarginProperty, Layoutable.HorizontalAlignmentProperty, Layoutable.VerticalAlignmentProperty, Layoutable.WidthProperty, Layoutable.MaxWidthProperty })
        {
            if (!g.IsSet(p)) continue;
            invólucro.SetValue(p, g.GetValue(p));
            g.ClearValue(p);
        }

        switch (pai)
        {
            case Panel painel:
                var i = painel.Children.IndexOf(g);
                if (i < 0) return null;
                painel.Children.RemoveAt(i);
                invólucro.Children.Add(barra);
                Grid.SetRow(g, 1);
                invólucro.Children.Add(g);
                painel.Children.Insert(i, invólucro);
                break;
            case Decorator d when d.Child == g:
                d.Child = null;
                invólucro.Children.Add(barra);
                Grid.SetRow(g, 1);
                invólucro.Children.Add(g);
                d.Child = invólucro;
                break;
            case ContentControl c when c.Content == g:
                c.Content = null;
                invólucro.Children.Add(barra);
                Grid.SetRow(g, 1);
                invólucro.Children.Add(g);
                c.Content = invólucro;
                break;
            default:
                return null;
        }
        g.SetValue(BarraProperty, barra);
        barra.Iniciar();

        if (raiz is not null)
        {
            if (!Raizes.TryGetValue(raiz, out var lista))
            {
                Raizes.Add(raiz, lista = []);
                raiz.AddHandler(InputElement.KeyDownEvent, (_, ev) => AoTeclar(lista, ev), RoutingStrategies.Tunnel);
            }
            lista.Add(barra);
        }
        return barra;
    }

    private static void AoTeclar(List<BarraFiltro> barras, KeyEventArgs e)
    {
        if (e.Key != Key.F || !e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        var visiveis = barras.Where(b => b.IsEffectivelyVisible && b.IsAttachedToVisualTree()).ToList();
        if (visiveis.Count == 0) return;
        var foco = TopLevel.GetTopLevel(visiveis[0])?.FocusManager?.GetFocusedElement() as Visual;
        var alvo = visiveis.FirstOrDefault(b => foco is not null && b.Grade.Parent is Visual inv && (foco == inv || inv.IsVisualAncestorOf(foco))) ?? visiveis[0];
        alvo.FocarBusca();
        e.Handled = true;
    }

    internal static string? TextoCabecalho(DataGridColumn c) => c.Header switch { string t => t, TextBlock b => b.Text, _ => c.Header?.ToString() };

    // ---- leitura das colunas ----

    private static readonly ConcurrentDictionary<(Type, string), Func<object, object?>> Getters = new();

    /// <summary>Leitor do valor de uma propriedade (caminho com pontos) de uma linha, com cache por tipo.</summary>
    internal static Func<object, object?> Getter(string caminho) => linha =>
    {
        var g = Getters.GetOrAdd((linha.GetType(), caminho), k =>
        {
            var partes = k.Item2.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var tipos = new List<PropertyInfo?>();
            var t = k.Item1;
            foreach (var p in partes)
            {
                var pi = t?.GetProperty(p, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                tipos.Add(pi);
                t = pi?.PropertyType;
            }
            return o =>
            {
                object? v = o;
                foreach (var pi in tipos)
                {
                    if (v is null || pi is null) return null;
                    v = pi.GetValue(v);
                }
                return v;
            };
        });
        return g(linha);
    };

    internal static List<LeitorColuna> Leitores(DataGrid g)
    {
        var r = new List<LeitorColuna>();
        foreach (var c in g.Columns)
        {
            var titulo = TextoCabecalho(c) ?? "";
            string? caminho = null, formato = null;
            if (c is DataGridBoundColumn { Binding: { } b })
            {
                switch (b)
                {
                    case Binding bb: caminho = bb.Path; formato = bb.StringFormat; break;
                    default: caminho = b.GetType().GetProperty("Path")?.GetValue(b)?.ToString(); formato = b.GetType().GetProperty("StringFormat")?.GetValue(b) as string; break;
                }
            }
            var col = c;
            Func<object, object?> leitor = string.IsNullOrEmpty(caminho) || caminho == "." ? (_ => null) : Getter(caminho.TrimStart('.'));
            r.Add(new LeitorColuna(titulo, leitor, formato) { Visivel = () => col.IsVisible });
        }
        return r;
    }

    internal static IEnumerable<object> Linhas(DataGrid g)
        => g.ItemsSource is IEnumerable e ? e.Cast<object>().Where(o => o is not null) : [];
}

/// <summary>Barra de busca rápida e painel de filtros por coluna, colocada acima de uma grade de registros.</summary>
public sealed class BarraFiltro : StackPanel
{
    private const int LimiteLista = 40;
    private readonly EstadoFiltro _estado;
    private readonly TextBox _busca;
    private readonly Button _botaoFiltros;
    private readonly Button _botaoLimpar;
    private readonly TextBlock _contador;
    private readonly Border _painel;
    private readonly Func<object, bool> _filtro;
    private List<LeitorColuna>? _leitores;
    private INotifyCollectionChanged? _fonte;
    private bool _contagemPendente;

    public DataGrid Grade { get; }
    public EstadoFiltro Estado => _estado;
    public bool PainelAberto => _painel.IsVisible;

    public BarraFiltro(DataGrid grade, EstadoFiltro estado)
    {
        Grade = grade;
        _estado = estado;
        Margin = new Thickness(0, 0, 0, 4);
        Spacing = 4;
        _filtro = linha => _estado.Aceita(linha, Leitores());

        _busca = new TextBox { Name = "BuscaRapida", Width = 280, Watermark = "Buscar na lista (Ctrl+F)", Text = estado.Busca };
        ToolTip.SetTip(_busca, "Digite parte de qualquer coluna. Vários termos separados por espaço: a linha precisa ter todos. Não diferencia maiúsculas nem acentos.");
        _busca.TextChanged += (_, _) => { _estado.Busca = _busca.Text ?? ""; Mudou(); };
        _busca.KeyDown += (_, e) => { if (e.Key == Key.Escape && !string.IsNullOrEmpty(_busca.Text)) { _busca.Text = ""; e.Handled = true; } };

        _botaoFiltros = new Button { Name = "BotaoFiltros", Content = "Filtros", Padding = new Thickness(10, 3) };
        _botaoFiltros.Click += (_, _) => AlternarPainel();
        _botaoLimpar = new Button { Name = "BotaoLimparFiltros", Content = "Limpar filtros", Padding = new Thickness(10, 3), IsVisible = false };
        _botaoLimpar.Click += (_, _) => Limpar();
        _contador = new TextBlock { Name = "ContadorFiltro", VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.Parse("#44506A")) };

        var linha = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        linha.Children.Add(_busca);
        linha.Children.Add(_botaoFiltros);
        linha.Children.Add(_botaoLimpar);
        linha.Children.Add(_contador);
        Children.Add(linha);

        _painel = new Border
        {
            Name = "PainelFiltros", IsVisible = false, Padding = new Thickness(8), CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.Parse("#C9D1E0")), Background = new SolidColorBrush(Color.Parse("#F5F7FB")),
        };
        Children.Add(_painel);
        Bind(IsVisibleProperty, Grade.GetObservable(Visual.IsVisibleProperty));
    }

    internal void Iniciar()
    {
        Grade.PropertyChanged += (_, e) => { if (e.Property == ItemsControl.ItemsSourceProperty || e.Property == DataGrid.ItemsSourceProperty) AoTrocarFonte(); };
        Grade.Columns.CollectionChanged += (_, _) => _leitores = null;
        AoTrocarFonte();
    }

    private List<LeitorColuna> Leitores() => _leitores ??= FiltroGrade.Leitores(Grade);

    private void AoTrocarFonte()
    {
        if (_fonte is not null) _fonte.CollectionChanged -= AoMudarFonte;
        _fonte = Grade.ItemsSource as INotifyCollectionChanged;
        if (_fonte is not null) _fonte.CollectionChanged += AoMudarFonte;
        Aplicar();
        Dispatcher.UIThread.Post(Aplicar, DispatcherPriority.Background);   // a visão da grade pode ser criada depois da troca
    }

    private void AoMudarFonte(object? s, NotifyCollectionChangedEventArgs e)
    {
        if (_contagemPendente) return;
        _contagemPendente = true;
        Dispatcher.UIThread.Post(() => { _contagemPendente = false; AtualizarContador(); }, DispatcherPriority.Background);
    }

    /// <summary>Liga o filtro na visão da grade (sem consultar o banco) e atualiza o contador.</summary>
    public void Aplicar()
    {
        var v = Grade.CollectionView;
        if (v is not null)
        {
            if (_estado.Ativo) { v.Filter = _filtro; v.Refresh(); }
            else if (v.Filter is not null) v.Filter = null!;
        }
        AtualizarContador();
    }

    private void Mudou() => Aplicar();

    public void Limpar()
    {
        _estado.Limpar();
        if (!string.IsNullOrEmpty(_busca.Text)) _busca.Text = "";
        if (_painel.IsVisible) ConstruirPainel();
        Aplicar();
    }

    public void FocarBusca()
    {
        _busca.Focus();
        _busca.SelectAll();
    }

    public void AlternarPainel()
    {
        if (_painel.IsVisible) { _painel.IsVisible = false; return; }
        ConstruirPainel();
        _painel.IsVisible = true;
    }

    private void AtualizarContador()
    {
        var linhas = FiltroGrade.Linhas(Grade);
        int total, mostradas;
        if (linhas is ICollection c0 && !_estado.Ativo) total = mostradas = c0.Count;
        else
        {
            var lista = linhas as IReadOnlyCollection<object> ?? linhas.ToList();
            total = lista.Count;
            mostradas = _estado.Ativo ? lista.Count(_filtro) : total;
        }
        _contador.Text = _estado.Ativo ? $"{mostradas} de {total} {(total == 1 ? "registro" : "registros")}" : $"{total} {(total == 1 ? "registro" : "registros")}";
        _botaoLimpar.IsVisible = _estado.Ativo;
        var n = _estado.ColunasAtivas;
        _botaoFiltros.Content = n > 0 ? $"Filtros ({n})" : "Filtros";
    }

    // ---- painel de filtros por coluna ----

    private void ConstruirPainel()
    {
        var linhas = FiltroGrade.Linhas(Grade).ToList();
        var leitores = Leitores();
        var cartoes = new WrapPanel { Orientation = Orientation.Horizontal };
        for (var i = 0; i < leitores.Count; i++)
        {
            var leitor = leitores[i];
            if (string.IsNullOrWhiteSpace(leitor.Titulo)) continue;
            var tipo = FiltroLinhas.DetectarTipo(linhas.Take(2000).Select(l => leitor.Valor(l)));
            var cartao = CriarCartao(i, leitor, tipo, linhas);
            if (cartao is not null) cartoes.Children.Add(cartao);
        }
        _painel.Child = new ScrollViewer { MaxHeight = 240, Content = cartoes, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control? CriarCartao(int i, LeitorColuna leitor, TipoDadoColuna tipo, List<object> linhas)
    {
        var f = _estado.Coluna(i);
        var corpo = new StackPanel { Spacing = 3, Width = 200 };
        corpo.Children.Add(new TextBlock { Text = leitor.Titulo, FontWeight = FontWeight.SemiBold });

        switch (tipo)
        {
            case TipoDadoColuna.Texto:
            {
                var modo = new ComboBox { Name = $"Filtro{i}Modo", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = new[] { "Contém", "Começa com", "É igual a" }, SelectedIndex = (int)f.Modo };
                var texto = new TextBox { Name = $"Filtro{i}Texto", Text = f.Texto, Watermark = "Digite para filtrar" };
                modo.SelectionChanged += (_, _) => { f.Modo = (ModoTexto)Math.Max(0, modo.SelectedIndex); Mudou(); };
                texto.TextChanged += (_, _) => { f.Texto = texto.Text ?? ""; Mudou(); };
                corpo.Children.Add(modo);
                corpo.Children.Add(texto);
                break;
            }
            case TipoDadoColuna.Numero:
            {
                var de = new TextBox { Name = $"Filtro{i}De", Watermark = "de", Text = f.Min?.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR")) };
                var ate = new TextBox { Name = $"Filtro{i}Ate", Watermark = "até", Text = f.Max?.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR")) };
                de.TextChanged += (_, _) => { f.Min = LerNumero(de.Text); Mudou(); };
                ate.TextChanged += (_, _) => { f.Max = LerNumero(ate.Text); Mudou(); };
                corpo.Children.Add(de);
                corpo.Children.Add(ate);
                break;
            }
            case TipoDadoColuna.Data:
            {
                var de = new CalendarDatePicker { Name = $"Filtro{i}De", SelectedDate = f.De, HorizontalAlignment = HorizontalAlignment.Stretch };
                var ate = new CalendarDatePicker { Name = $"Filtro{i}Ate", SelectedDate = f.Ate, HorizontalAlignment = HorizontalAlignment.Stretch };
                de.SelectedDateChanged += (_, _) => { f.De = de.SelectedDate; Mudou(); };
                ate.SelectedDateChanged += (_, _) => { f.Ate = ate.SelectedDate; Mudou(); };
                corpo.Children.Add(new TextBlock { Text = "De" });
                corpo.Children.Add(de);
                corpo.Children.Add(new TextBlock { Text = "Até" });
                corpo.Children.Add(ate);
                break;
            }
            case TipoDadoColuna.SimNao:
            {
                var sn = new ComboBox { Name = $"Filtro{i}SimNao", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = new[] { "Todos", "Sim", "Não" }, SelectedIndex = f.SimNao is null ? 0 : f.SimNao.Value ? 1 : 2 };
                sn.SelectionChanged += (_, _) => { f.SimNao = sn.SelectedIndex switch { 1 => true, 2 => false, _ => null }; Mudou(); };
                corpo.Children.Add(sn);
                break;
            }
        }

        if (tipo is TipoDadoColuna.Texto or TipoDadoColuna.Numero)
        {
            var valores = FiltroLinhas.ValoresDistintos(linhas, leitor, LimiteLista);
            if (valores.Count is >= 2 and <= LimiteLista)
            {
                var caixas = new StackPanel();
                var marcas = new List<(CheckBox Caixa, string Valor)>();
                void Atualizar()
                {
                    var marcados = marcas.Where(m => m.Caixa.IsChecked == true).Select(m => m.Valor).ToHashSet(StringComparer.Ordinal);
                    f.Valores = marcados.Count == marcas.Count ? null : marcados;
                    Mudou();
                }
                foreach (var v in valores)
                {
                    var cb = new CheckBox { Content = v.Length == 0 ? "(vazio)" : v, IsChecked = f.Valores is null || f.Valores.Contains(v), Margin = new Thickness(0, 0, 0, 0) };
                    cb.IsCheckedChanged += (_, _) => Atualizar();
                    marcas.Add((cb, v));
                    caixas.Children.Add(cb);
                }
                corpo.Children.Add(new Expander
                {
                    Name = $"Filtro{i}Lista", Header = "Escolher valores", IsExpanded = f.Valores is not null,
                    Content = new ScrollViewer { MaxHeight = 130, Content = caixas },
                });
            }
        }
        return new Border
        {
            Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(6), CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.Parse("#D5DBE8")), Background = Brushes.White, Child = corpo,
        };
    }

    private static decimal? LerNumero(string? t)
        => decimal.TryParse(t?.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, CultureInfo.GetCultureInfo("pt-BR"), out var n) ? n : null;
}
