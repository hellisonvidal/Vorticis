using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
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
            if (_vm is not null) _vm.PropertyChanged -= AoMudarVm;
            _vm = DataContext as MainViewModel;
            if (_vm is not null) _vm.PropertyChanged += AoMudarVm;
        };
    }

    /// <summary>Ao entrar ou sair do sistema, volta a maximizar (caso o usuário tenha restaurado o tamanho).</summary>
    private void AoMudarVm(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Logado)) WindowState = WindowState.Maximized;
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
