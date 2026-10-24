using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Vorticis.Desktop.Comum.ViewModels;

namespace Vorticis.Desktop.Comum.Views;

public partial class LoginView : UserControl
{
    private static readonly FilePickerFileType Licenca = new("Licença do Vorticis") { Patterns = ["*.vlic"] };

    public LoginView() => InitializeComponent();

    private async void AoInstalarLicenca(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && await EscolhaArquivo.AbrirAsync(this, "Arquivo de licença do Vorticis", Licenca) is { } arquivo)
            await vm.InstalarLicencaAsync(arquivo);
    }
}
