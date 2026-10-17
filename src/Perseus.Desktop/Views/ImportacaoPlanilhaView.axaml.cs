using Avalonia.Controls;
using Avalonia.Interactivity;
using Perseus.Desktop.ViewModels;

namespace Perseus.Desktop.Views;

public partial class ImportacaoPlanilhaView : UserControl
{
    public ImportacaoPlanilhaView() => InitializeComponent();

    private async void ProcurarPlanilha(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ImportacaoPlanilhaViewModel vm && await EscolhaArquivo.AbrirAsync(this, "Planilha com os lançamentos", EscolhaArquivo.Planilha) is { } c)
            vm.Arquivo = c;
    }
}
