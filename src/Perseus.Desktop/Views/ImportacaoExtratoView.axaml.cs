using Avalonia.Controls;
using Avalonia.Interactivity;
using Perseus.Desktop.ViewModels;

namespace Perseus.Desktop.Views;

public partial class ImportacaoExtratoView : UserControl
{
    public ImportacaoExtratoView() => InitializeComponent();

    private async void ProcurarOfx(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ImportacaoExtratoViewModel vm && await EscolhaArquivo.AbrirAsync(this, "Extrato bancário (OFX)", EscolhaArquivo.Ofx) is { } c)
            vm.Arquivo = c;
    }
}
