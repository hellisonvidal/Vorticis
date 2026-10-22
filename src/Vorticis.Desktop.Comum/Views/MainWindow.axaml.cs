using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Vorticis.Desktop.Comum.Modulos;
using Vorticis.Desktop.Comum.ViewModels;

namespace Vorticis.Desktop.Comum.Views;

public partial class MainWindow : Window
{
    private MainViewModelBase? _vm;

    public MainWindow()
    {
        InitializeComponent();
        // Sempre inicia maximizada (tela cheia, mantendo a barra de título com o nome do programa).
        WindowState = WindowState.Maximized;
        Opened += (_, _) => WindowState = WindowState.Maximized;
        // Fechar o sistema sempre pede confirmação.
        Closing += AoFechar;
        // As janelas das funções ocupam a área de trabalho (abaixo do título, menus e barra de janelas) e a acompanham.
        PositionChanged += (_, _) => ReposicionarJanelas();
        AreaPrincipal.PropertyChanged += (_, e) =>
        {
            if (e.Property == BoundsProperty) ReposicionarJanelas();
        };
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null)
            {
                _vm.PropertyChanged -= AoMudarVm;
                _vm.JanelasAbertas.CollectionChanged -= AoMudarJanelas;
            }
            _vm = DataContext as MainViewModelBase;
            if (_vm is not null)
            {
                Title = _vm.Modulo.Nome;
                AplicarIdentidadeVisual(_vm);
                MontarMenu(_vm);
                MontarAtalhos(_vm);
                _vm.PropertyChanged += AoMudarVm;
                _vm.JanelasAbertas.CollectionChanged += AoMudarJanelas;
            }
        };
    }

    /// <summary>Ícone e cor do módulo (Contábil verde, Fiscal laranja, Folha roxo) na janela e nas janelas das funções.</summary>
    private void AplicarIdentidadeVisual(MainViewModelBase vm)
    {
        Resources["CorDestaqueModulo"] = vm.PincelDestaque;
        try
        {
            using var stream = Avalonia.Platform.AssetLoader.Open(new Uri($"avares://Vorticis.Desktop.Comum/Assets/{vm.Modulo.NomeExecutavel}.ico"));
            Icon = new WindowIcon(stream);
        }
        catch (Exception)
        {
            // sem o ícone do módulo, fica o ícone padrão da janela
        }
    }

    // ------------------------------------------------------------------ janelas flutuantes das funções

    private readonly Dictionary<FuncaoAberta, Window> _janelas = [];
    private readonly HashSet<Window> _fechandoPorCodigo = [];
    private Window? _dialogo;

    /// <summary>Janela flutuante da função ativa (null se nenhuma função estiver aberta).</summary>
    private bool _fechamentoConfirmado;

    /// <summary>Pergunta antes de fechar o programa; só fecha de fato se o usuário confirmar.</summary>
    private async void AoFechar(object? sender, WindowClosingEventArgs e)
    {
        if (_fechamentoConfirmado || _vm is null) return;
        e.Cancel = true;
        if (_vm.Confirmacao is not null) return;                          // já há uma pergunta aberta
        if (await _vm.ConfirmarAsync($"Fechar o {_vm.Modulo.Nome}", "Deseja realmente fechar o sistema?", "Fechar", "Continuar trabalhando"))
        {
            _fechamentoConfirmado = true;
            Close();
        }
    }

    public Window? JanelaAtiva => _vm?.FuncaoAtiva is { } f && _janelas.TryGetValue(f, out var w) ? w : null;

    public Window? JanelaDa(object viewModel) => _janelas.FirstOrDefault(kv => ReferenceEquals(kv.Key.ViewModel, viewModel)).Value;

    private void AoMudarJanelas(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_vm is null) return;
        // Sincroniza: abre janelas das funções novas e fecha as que saíram da lista.
        foreach (var f in _vm.JanelasAbertas.Where(f => !_janelas.ContainsKey(f)).ToList()) AbrirJanela(f);
        foreach (var (f, w) in _janelas.Where(kv => !_vm.JanelasAbertas.Contains(kv.Key)).ToList())
        {
            _janelas.Remove(f);
            _fechandoPorCodigo.Add(w);
            w.Close();
        }
    }

    private void AbrirJanela(FuncaoAberta f)
    {
        var titulo = new TextBlock
        {
            Text = f.Titulo, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0),
        };
        var fechar = new Button
        {
            Content = "✕  Fechar", FontSize = 12, Padding = new Thickness(10, 2), Margin = new Thickness(4, 2),
            Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0),
            [ToolTip.TipProperty] = "Fechar esta função",
        };
        var cabecalho = new Border
        {
            Background = _vm?.PincelDestaque ?? new SolidColorBrush(Color.Parse("#3A5F8F")), Height = 30,
            Child = new DockPanel { Children = { fechar, titulo } },
        };
        DockPanel.SetDock(cabecalho, Dock.Top);
        DockPanel.SetDock(fechar, Dock.Right);
        var w = new Window
        {
            Title = $"{f.Titulo} — {_vm?.Modulo.Nome}",
            Content = new DockPanel
            {
                Children = { cabecalho, new ContentControl { Content = f.ViewModel, Margin = new Thickness(4) } },
            },
            SystemDecorations = SystemDecorations.BorderOnly,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Background = Background,
            Icon = Icon,
        };
        Posicionar(w);
        fechar.Click += (_, _) => _vm?.FecharFuncao(f);
        f.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(FuncaoAberta.Titulo)) return;
            w.Title = $"{f.Titulo} — {_vm?.Modulo.Nome}";
            titulo.Text = f.Titulo;
        };
        w.Activated += (_, _) => _vm?.AtivarFuncao(f);
        w.Closed += (_, _) =>
        {
            if (!_fechandoPorCodigo.Remove(w)) _vm?.FecharFuncao(f);   // Alt+F4
        };
        w.KeyBindings.AddRange(KeyBindings);   // mesmos atalhos (Ctrl+L, Ctrl+B, F1)
        _janelas[f] = w;
        w.Show(this);
    }

    /// <summary>Encaixa a janela da função na área de trabalho da janela principal (tela cheia sem cobrir título e menus).</summary>
    private void Posicionar(Window w)
    {
        var area = AreaPrincipal.Bounds;
        if (area.Width < 10 || area.Height < 10) return;
        var origem = AreaPrincipal.TranslatePoint(new Point(0, 0), this) ?? default;
        w.Position = this.PointToScreen(origem);
        (w.Width, w.Height) = (area.Width, area.Height);
    }

    private void ReposicionarJanelas()
    {
        foreach (var w in _janelas.Values) Posicionar(w);
    }

    private void TrazerParaFrente(FuncaoAberta? f)
    {
        if (f is not null && _janelas.TryGetValue(f, out var w) && !w.IsActive) w.Activate();
    }

    // ------------------------------------------------------------------ confirmação (Sim/Não)

    private void MostrarConfirmacao(PedidoConfirmacao? pedido)
    {
        if (pedido is null)
        {
            if (_dialogo is { } aberto) { _dialogo = null; _fechandoPorCodigo.Add(aberto); aberto.Close(); }
            return;
        }
        var botaoSim = new Button { Content = pedido.TextoSim, Classes = { "accent" }, IsDefault = true, MinWidth = 90,
                                    HorizontalContentAlignment = HorizontalAlignment.Center };
        var botaoNao = new Button { Content = pedido.TextoNao, IsCancel = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        var dialogo = new Window
        {
            Title = pedido.Titulo,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(24, 20),
                Spacing = 18,
                MaxWidth = 520,
                Children =
                {
                    new TextBlock { Text = pedido.Titulo, FontSize = 16, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = pedido.Mensagem, TextWrapping = TextWrapping.Wrap },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right,
                                     Children = { botaoSim, botaoNao } },
                },
            },
        };
        if (pedido.PedeSenha)
        {
            var campo = new TextBox { PasswordChar = '●', Watermark = "Senha de administrador", Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
            campo.TextChanged += (_, _) => pedido.Senha = campo.Text ?? "";
            ((StackPanel)dialogo.Content!).Children.Insert(2, campo);
            dialogo.Opened += (_, _) => campo.Focus();
        }
        botaoSim.Click += (_, _) => _vm?.ResponderConfirmacaoCommand.Execute(true);
        botaoNao.Click += (_, _) => _vm?.ResponderConfirmacaoCommand.Execute(false);
        dialogo.Closed += (_, _) =>
        {
            if (!_fechandoPorCodigo.Remove(dialogo) && ReferenceEquals(_vm?.Confirmacao, pedido)) _vm.ResponderConfirmacaoCommand.Execute(false);
        };
        _dialogo = dialogo;
        _ = dialogo.ShowDialog(JanelaAtiva ?? this);
    }

    /// <summary>Ao entrar ou sair do sistema, volta a maximizar (caso o usuário tenha restaurado o tamanho).</summary>
    private void AoMudarVm(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModelBase.Logado)) WindowState = WindowState.Maximized;
        if (e.PropertyName == nameof(MainViewModelBase.FuncaoAtiva)) TrazerParaFrente(_vm?.FuncaoAtiva);
        if (e.PropertyName == nameof(MainViewModelBase.Confirmacao)) MostrarConfirmacao(_vm?.Confirmacao);
    }

    // ------------------------------------------------------------------ menu e atalhos fornecidos pelo módulo

    private void MontarMenu(MainViewModelBase vm)
    {
        MenuPrincipal.Items.Clear();
        foreach (var item in vm.Menus) MenuPrincipal.Items.Add(CriarItem(vm, item));
    }

    private static Control CriarItem(MainViewModelBase vm, ItemMenu item)
    {
        if (item.EhSeparador) return new Separator();
        var menu = new MenuItem { Header = item.Titulo, IsEnabled = item.Habilitado };
        if (item.Atalho is { } atalho) menu.InputGesture = KeyGesture.Parse(atalho);   // só indicativo no Avalonia
        if (item.Filhos is { } filhos)
            foreach (var filho in filhos) menu.Items.Add(CriarItem(vm, filho));
        else if (item.Chave == TelasComuns.Sair)
            menu.Command = vm.SairCommand;
        else if (item.Chave is { } chave)
        {
            menu.Command = vm.AbrirCommand;
            menu.CommandParameter = chave;
        }
        return menu;
    }

    /// <summary>Atalhos globais (os InputGesture do menu são apenas indicativos no Avalonia).</summary>
    private void MontarAtalhos(MainViewModelBase vm)
    {
        KeyBindings.Clear();
        foreach (var a in vm.Atalhos.Where(a => a.Chave is not null))
        {
            var chave = a.Chave!;
            KeyBindings.Add(new KeyBinding
            {
                Gesture = KeyGesture.Parse(a.Gesto),
                Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() =>
                {
                    if (DataContext is MainViewModelBase atual && atual.AbrirCommand.CanExecute(chave)) atual.AbrirCommand.Execute(chave);
                }),
            });
        }
    }
}
