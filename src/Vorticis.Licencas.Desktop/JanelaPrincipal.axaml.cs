using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Vorticis.Licencas.Desktop;

public partial class JanelaPrincipal : Window
{
    public JanelaPrincipal() => InitializeComponent();

    private GerenciadorViewModel? Vm => DataContext as GerenciadorViewModel;

    private async void AoImportarChave(object? sender, RoutedEventArgs e)
    {
        var arquivos = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Chave privada do fornecedor (PEM)", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Chave PEM") { Patterns = ["*.pem"] }, FilePickerFileTypes.All],
        });
        if (arquivos.Count > 0 && arquivos[0].TryGetLocalPath() is { } caminho) Vm?.ImportarChave(caminho);
    }

    private async void AoExportarPublica(object? sender, RoutedEventArgs e)
    {
        var arquivo = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Salvar chave pública", SuggestedFileName = "chave-publica.pem" });
        if (arquivo?.TryGetLocalPath() is { } caminho) Vm?.ExportarChavePublica(caminho);
    }
}
