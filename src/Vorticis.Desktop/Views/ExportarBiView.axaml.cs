using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Vorticis.Desktop.ViewModels;

namespace Vorticis.Desktop.Views;

public partial class ExportarBiView : UserControl
{
    public ExportarBiView() => InitializeComponent();

    private async void EscolherPasta(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ExportarBiViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
        var pastas = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Pasta para gravar os dados de BI", AllowMultiple = false });
        if (pastas.Count > 0 && pastas[0].TryGetLocalPath() is { } caminho) vm.Pasta = caminho;
    }
}
