using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vorticis.Desktop.Comum.ViewModels;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>
/// Parte de cima da Suite (layouts A, D e E). No E monta a faixa de opções (ribbon) do módulo à frente: um grupo por categoria do menu
/// (Cadastros, Lançamentos, Relatórios...), com botões grandes; o que não cabe no grupo vai para "Mais". Oculta, a faixa mostra só as
/// abas e abre por cima ao passar o mouse ou clicar numa aba.
/// </summary>
public partial class SuiteTopoView : UserControl
{
    private static readonly IBrush Texto = new SolidColorBrush(Color.Parse("#1E293B"));
    private static readonly IBrush Sutil = new SolidColorBrush(Color.Parse("#64748B"));
    private static readonly IBrush Linha = new SolidColorBrush(Color.Parse("#E2E8F0"));
    private readonly DispatcherTimer _recolher = new() { Interval = TimeSpan.FromMilliseconds(420) };
    private SuiteViewModel? _suite;
    private ModuloSuite? _observado;

    public SuiteTopoView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Ligar();
        _recolher.Tick += (_, _) =>
        {
            _recolher.Stop();
            if (_suite is { } s && !BarraAbas.IsPointerOver && !CorpoPopup.IsPointerOver && !MenuAberto()) s.FaixaAberta = false;
        };
        BarraAbas.PointerEntered += (_, _) => AbrirFaixaFlutuante();
        BarraAbas.PointerPressed += (_, _) => AbrirFaixaFlutuante();
        BarraAbas.PointerExited += (_, _) => _recolher.Start();
        CorpoPopup.PointerEntered += (_, _) => _recolher.Stop();
        CorpoPopup.PointerExited += (_, _) => _recolher.Start();
    }

    private bool MenuAberto() => _flyoutsAbertos > 0;
    private int _flyoutsAbertos;

    private void AbrirFaixaFlutuante()
    {
        _recolher.Stop();
        if (_suite is { EhFaixa: true, FaixaFixa: false } s) s.FaixaAberta = true;
    }

    private void Ligar()
    {
        if (_suite is not null) _suite.PropertyChanged -= AoMudarSuite;
        _suite = DataContext as SuiteViewModel;
        if (_suite is null) return;
        _suite.PropertyChanged += AoMudarSuite;
        Observar(_suite.Ativo);
        MontarFaixa();
    }

    private void AoMudarSuite(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SuiteViewModel.Ativo)) { Observar(_suite!.Ativo); MontarFaixa(); }
        else if (e.PropertyName is nameof(SuiteViewModel.Layout)) MontarFaixa();
    }

    private void Observar(ModuloSuite m)
    {
        if (_observado is not null) _observado.PropertyChanged -= AoMudarModulo;
        _observado = m;
        m.PropertyChanged += AoMudarModulo;
    }

    private void AoMudarModulo(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModuloSuite.Raiz)) MontarFaixa();
    }

    // ------------------------------------------------------------------ faixa de opções (E)

    /// <summary>Refaz os botões da faixa a partir dos menus do módulo à frente (a faixa fixa e a que abre por cima são duas cópias).</summary>
    public void MontarFaixa()
    {
        if (_suite is not { EhFaixa: true } s || s.Ativo.Raiz is null) return;
        CorpoFixo.Content = CriarCorpo(s, s.Ativo, flutuante: false);
        CorpoPopup.Content = CriarCorpo(s, s.Ativo, flutuante: true);
    }

    private Control CriarCorpo(SuiteViewModel suite, ModuloSuite modulo, bool flutuante)
    {
        var grupos = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 6, 8, 8) };
        foreach (var categoria in modulo.Raiz.Filhos)
        {
            grupos.Children.Add(CriarGrupo(suite, modulo, categoria));
            grupos.Children.Add(new Border { Width = 1, Background = Linha, Margin = new Thickness(4, 4, 4, 16) });
        }
        var corpo = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#D5DCE6")), BorderThickness = new Thickness(0, 0, 0, 1),
            Height = 104, MinWidth = flutuante ? 900 : 0,
            Child = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = grupos },
        };
        if (flutuante)
        {
            corpo.BoxShadow = BoxShadows.Parse("0 8 22 0 #33233B55");
            corpo.BorderThickness = new Thickness(1);
            corpo.CornerRadius = new CornerRadius(0, 0, 10, 10);
            corpo.PointerEntered += (_, _) => _recolher.Stop();
            corpo.PointerExited += (_, _) => _recolher.Start();
            corpo.Width = Math.Max(900, (VisualRoot as Control)?.Bounds.Width ?? 900);
        }
        return corpo;
    }

    private Control CriarGrupo(SuiteViewModel suite, ModuloSuite modulo, NoSuite categoria)
    {
        var itens = categoria.Filhos.ToList();
        var visiveis = itens.Count <= 3 ? itens : itens.Take(2).ToList();
        var resto = itens.Count <= 3 ? [] : itens.Skip(2).ToList();
        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        var icone = categoria.Icone ?? Icones.FolderOutline;
        foreach (var item in visiveis)
            botoes.Children.Add(item.Chave is not null
                ? Botao(item.Titulo, IconesSuite.DaFuncao(item.Titulo, icone), modulo, () => suite.AbrirFuncaoCommand.Execute(item), item)
                : BotaoMenu(item.Titulo, item.Icone ?? icone, modulo, suite, item.Filhos));
        if (resto.Count > 0) botoes.Children.Add(BotaoMenu("Mais", Icones.FormatListBulleted, modulo, suite, resto));
        return new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"), MinWidth = 76,
            Children =
            {
                botoes,
                new TextBlock { Text = categoria.Titulo, FontSize = 11, Foreground = Sutil, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 5),
                                [Grid.RowProperty] = 1, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 220 },
            },
        };
    }

    private static Button Botao(string texto, Geometry icone, ModuloSuite modulo, Action acao, NoSuite? no = null, bool seta = false)
    {
        var linhaIcone = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        linhaIcone.Children.Add(new PathIcon { Data = icone, Width = 26, Height = 26, Foreground = modulo.Cor, HorizontalAlignment = HorizontalAlignment.Center });
        if (seta)
            linhaIcone.Children.Add(new PathIcon { Data = Icones.ChevronDown, Width = 10, Height = 10, Foreground = Sutil, HorizontalAlignment = HorizontalAlignment.Right,
                                                   VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, -12, -1) });
        var b = new Button
        {
            Width = 84, Height = 68, Margin = new Thickness(1, 0), Classes = { "su" }, VerticalContentAlignment = VerticalAlignment.Top, HorizontalContentAlignment = HorizontalAlignment.Center,
            Content = new StackPanel
            {
                Spacing = 4, Margin = new Thickness(2, 6, 2, 2), HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    linhaIcone,
                    new TextBlock { Text = texto, FontSize = 11.5, Foreground = Texto, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 80, MaxHeight = 32 },
                },
            },
        };
        b.Tag = no;
        b.SetValue(ToolTip.TipProperty, texto);
        b.Click += (_, _) => acao();
        return b;
    }

    private Button BotaoMenu(string texto, Geometry icone, ModuloSuite modulo, SuiteViewModel suite, IReadOnlyList<NoSuite> itens)
    {
        var b = Botao(texto, icone, modulo, () => { }, seta: true);
        var fly = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        foreach (var i in itens) fly.Items.Add(ItemDeMenu(suite, i));
        fly.Opened += (_, _) => _flyoutsAbertos++;
        fly.Closed += (_, _) => { _flyoutsAbertos = Math.Max(0, _flyoutsAbertos - 1); suite.FaixaAberta = false; };
        b.Flyout = fly;
        return b;
    }

    /// <summary>Chaves das telas que a faixa fixa oferece (botões e menus "Mais"); usado nos testes de acesso.</summary>
    public IReadOnlyList<string> ChavesDaFaixa()
    {
        var chaves = new List<string>();
        void Menu(IEnumerable<object?> itens)
        {
            foreach (var i in itens.OfType<MenuItem>())
            {
                if ((i.CommandParameter as NoSuite)?.Chave is { } c) chaves.Add(c);
                Menu(i.Items.Cast<object?>());
            }
        }
        foreach (var b in CorpoFixo.GetVisualDescendants().OfType<Button>())
        {
            if ((b.Tag as NoSuite)?.Chave is { } c) chaves.Add(c);
            if (b.Flyout is MenuFlyout f) Menu(f.Items.Cast<object?>());
        }
        return chaves;
    }

    private static MenuItem ItemDeMenu(SuiteViewModel suite, NoSuite no)
    {
        var m = new MenuItem { Header = no.Titulo };
        if (no.Icone is { } ic) m.Icon = IconesSuite.IconeDe(ic, 15);
        if (no.Chave is not null) { m.Command = suite.AbrirFuncaoCommand; m.CommandParameter = no; }
        foreach (var f in no.Filhos) m.Items.Add(ItemDeMenu(suite, f));
        return m;
    }
}
