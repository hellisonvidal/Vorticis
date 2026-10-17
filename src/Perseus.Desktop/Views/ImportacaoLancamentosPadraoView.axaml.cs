using Avalonia.Controls;
using Avalonia.Interactivity;
using Perseus.Desktop.ViewModels;

namespace Perseus.Desktop.Views;

public partial class ImportacaoLancamentosPadraoView : UserControl
{
    public ImportacaoLancamentosPadraoView() => InitializeComponent();

    private async void ProcurarPadrao(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ImportacaoLancamentosPadraoViewModel vm
            && await EscolhaArquivo.AbrirAsync(this, "Lançamentos no layout padrão", EscolhaArquivo.Planilha) is { } c)
            vm.Arquivo = c;
    }
}
