using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Perseus.Desktop.ViewModels;

namespace Perseus.Desktop.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();
        // Sempre inicia maximizada (tela cheia, mantendo a barra de título com o nome do programa).
        WindowState = WindowState.Maximized;
        Opened += (_, _) => WindowState = WindowState.Maximized;
        // Atalhos globais (os InputGesture do menu são apenas indicativos no Avalonia).
        KeyBindings.Add(Atalho(Key.L, KeyModifiers.Control, Telas.Lancamento));
        KeyBindings.Add(Atalho(Key.B, KeyModifiers.Control, Telas.Balancete));
        KeyBindings.Add(Atalho(Key.F1, KeyModifiers.None, Telas.Sobre));
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null)
            {
                _vm.PropertyChanged -= AoMudarVm;
                _vm.JanelasAbertas.CollectionChanged -= AoMudarJanelas;
            }
            _vm = DataContext as MainViewModel;
            if (_vm is not null)
            {
                _vm.PropertyChanged += AoMudarVm;
                _vm.JanelasAbertas.CollectionChanged += AoMudarJanelas;
            }
        };
    }

    // ------------------------------------------------------------------ janelas flutuantes das funções

    private readonly Dictionary<FuncaoAberta, Window> _janelas = [];
    private readonly HashSet<Window> _fechandoPorCodigo = [];
    private Window? _dialogo;

    /// <summary>Janela flutuante da função ativa (null se nenhuma função estiver aberta).</summary>
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
        var w = new Window
        {
            Title = $"{f.Titulo} — Perseus Contábil",
            Content = new ContentControl { Content = f.ViewModel, Margin = new Thickness(4) },
            Width = Math.Max(960, Bounds.Width * 0.96),
            Height = Math.Max(560, Bounds.Height * 0.88),
            MinWidth = 640,
            MinHeight = 420,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = Icon,
        };
        f.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FuncaoAberta.Titulo)) w.Title = $"{f.Titulo} — Perseus Contábil";
        };
        w.Activated += (_, _) => _vm?.AtivarFuncao(f);
        w.Closed += (_, _) =>
        {
            if (!_fechandoPorCodigo.Remove(w)) _vm?.FecharFuncao(f);   // ✕ da própria janela
        };
        w.KeyBindings.AddRange(KeyBindings);   // mesmos atalhos (Ctrl+L, Ctrl+B, F1)
        _janelas[f] = w;
        w.Show(this);
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
        if (e.PropertyName == nameof(MainViewModel.Logado)) WindowState = WindowState.Maximized;
        if (e.PropertyName == nameof(MainViewModel.FuncaoAtiva)) TrazerParaFrente(_vm?.FuncaoAtiva);
        if (e.PropertyName == nameof(MainViewModel.Confirmacao)) MostrarConfirmacao(_vm?.Confirmacao);
    }

    private KeyBinding Atalho(Key tecla, KeyModifiers mod, string tela) => new()
    {
        Gesture = new KeyGesture(tecla, mod),
        Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() =>
        {
            if (DataContext is MainViewModel vm && vm.AbrirCommand.CanExecute(tela)) vm.AbrirCommand.Execute(tela);
        }),
    };
}
