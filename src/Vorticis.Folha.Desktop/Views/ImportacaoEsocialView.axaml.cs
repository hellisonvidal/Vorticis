using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Vorticis.Folha.Desktop.ViewModels;

namespace Vorticis.Folha.Desktop.Views;

public partial class ImportacaoEsocialView : UserControl
{
    private static readonly FilePickerFileType Eventos = new("Eventos do eSocial (XML ou ZIP)") { Patterns = ["*.xml", "*.XML", "*.zip", "*.ZIP"] };

    public ImportacaoEsocialView() => InitializeComponent();

    private async void EscolherPasta(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ImportacaoEsocialViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
        var pastas = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Pasta com os eventos do eSocial", AllowMultiple = false });
        if (pastas.Count > 0 && pastas[0].TryGetLocalPath() is { } caminho) await vm.LerCaminhosAsync([caminho]);
    }

    private async void EscolherArquivos(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ImportacaoEsocialViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
        var arquivos = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Eventos do eSocial (XML ou ZIP)", AllowMultiple = true, FileTypeFilter = [Eventos, FilePickerFileTypes.All],
        });
        var caminhos = arquivos.Select(a => a.TryGetLocalPath()).OfType<string>().ToList();
        if (caminhos.Count > 0) await vm.LerCaminhosAsync(caminhos);
    }
}
