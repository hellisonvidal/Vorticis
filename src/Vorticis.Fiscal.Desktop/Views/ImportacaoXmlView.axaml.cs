using Avalonia.Controls;
using Avalonia.Interactivity;
using Vorticis.Fiscal.Desktop.ViewModels;

namespace Vorticis.Fiscal.Desktop.Views;

public partial class ImportacaoXmlView : UserControl
{
    public ImportacaoXmlView() => InitializeComponent();

    private async void EscolherArquivos(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ImportacaoXmlViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
        var caminhos = await SelecaoXml.ArquivosAsync(top);
        if (caminhos.Count > 0) await vm.ImportarArquivosAsync(caminhos);
    }

    private async void EscolherPasta(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ImportacaoXmlViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
        if (await SelecaoXml.PastaAsync(top) is { } caminho) vm.Pasta = caminho;
    }
}
