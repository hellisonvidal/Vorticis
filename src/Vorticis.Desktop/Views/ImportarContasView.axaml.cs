using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Vorticis.Desktop.ViewModels;

namespace Vorticis.Desktop.Views;

public partial class ImportarContasView : UserControl
{
    public ImportarContasView() => InitializeComponent();

    private async void Procurar(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ImportarContasPlanoViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
        try
        {
            var arquivos = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Planilha com as contas",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Planilha (Excel ou CSV)") { Patterns = ["*.xlsx", "*.xlsm", "*.csv", "*.txt"] }, FilePickerFileTypes.All],
            });
            if (arquivos.Count > 0 && arquivos[0].TryGetLocalPath() is { } caminho) vm.Arquivo = caminho;
        }
        catch (Exception ex) { vm.Erros.Add($"Não foi possível abrir a planilha: {ex.Message}"); }
    }
}
