using Avalonia.Controls;
using Avalonia.Input;
using Perseus.Desktop.ViewModels;

namespace Perseus.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Atalhos globais (os InputGesture do menu são apenas indicativos no Avalonia).
        KeyBindings.Add(Atalho(Key.L, KeyModifiers.Control, Telas.Lancamento));
        KeyBindings.Add(Atalho(Key.B, KeyModifiers.Control, Telas.Balancete));
        KeyBindings.Add(Atalho(Key.F1, KeyModifiers.None, Telas.Sobre));
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
