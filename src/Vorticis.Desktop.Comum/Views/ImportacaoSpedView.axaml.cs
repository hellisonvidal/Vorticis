using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Vorticis.Desktop.Comum.ViewModels;

namespace Vorticis.Desktop.Comum.Views;

public partial class ImportacaoSpedView : UserControl
{
    private static readonly FilePickerFileType Sped = new("Arquivo SPED (texto)") { Patterns = ["*.txt", "*.TXT", "*.sped"] };

    public ImportacaoSpedView() => InitializeComponent();

    private async void Procurar(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ImportacaoSpedViewModel vm) return;
        if (await EscolhaArquivo.AbrirAsync(this, "Arquivo SPED a importar", Sped) is not { } caminho) return;
        vm.Arquivo = caminho;
        await vm.LerArquivoAsync();
    }
}
