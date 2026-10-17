using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Perseus.Desktop.ViewModels;

namespace Perseus.Desktop.Views;

public partial class ParticipantesView : UserControl
{
    public ParticipantesView() => InitializeComponent();

    private async void ImportarPlanilha(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ParticipantesViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
        var arquivos = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Planilha de {vm.TituloLista.ToLowerInvariant()}",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Planilha (Excel ou CSV)") { Patterns = ["*.xlsx", "*.csv", "*.txt"] }, FilePickerFileTypes.All],
        });
        if (arquivos.Count > 0 && arquivos[0].TryGetLocalPath() is { } caminho) await vm.ImportarPlanilhaAsync(caminho);
    }
}
