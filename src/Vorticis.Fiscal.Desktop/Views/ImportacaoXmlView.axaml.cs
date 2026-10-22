using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Vorticis.Fiscal.Desktop.ViewModels;

namespace Vorticis.Fiscal.Desktop.Views;

public partial class ImportacaoXmlView : UserControl
{
    private static readonly FilePickerFileType Xml = new("XML fiscal (.xml, .zip)") { Patterns = ["*.xml", "*.XML", "*.zip", "*.ZIP"] };

    public ImportacaoXmlView() => InitializeComponent();

    private async void EscolherArquivos(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ImportacaoXmlViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
        var arquivos = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "XML de notas fiscais", AllowMultiple = true, FileTypeFilter = [Xml, FilePickerFileTypes.All],
        });
        var caminhos = arquivos.Select(a => a.TryGetLocalPath()).OfType<string>().ToList();
        if (caminhos.Count > 0) await vm.ImportarArquivosAsync(caminhos);
    }

    private async void EscolherPasta(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ImportacaoXmlViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
        var pastas = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Pasta com os XML", AllowMultiple = false });
        if (pastas.Count > 0 && pastas[0].TryGetLocalPath() is { } caminho) vm.Pasta = caminho;
    }
}
