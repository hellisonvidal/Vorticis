using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Perseus.Desktop.ViewModels;

namespace Perseus.Desktop.Views;

public partial class ImportacaoView : UserControl
{
    public ImportacaoView() => InitializeComponent();

    private static readonly FilePickerFileType Planilha = new("Planilha (Excel ou CSV)") { Patterns = ["*.xlsx", "*.xlsm", "*.csv", "*.txt"] };

    private async Task<string?> EscolherAsync(string titulo, FilePickerFileType tipo)
    {
        if (TopLevel.GetTopLevel(this) is not { } top) return null;
        var arquivos = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = titulo, AllowMultiple = false, FileTypeFilter = [tipo, FilePickerFileTypes.All],
        });
        return arquivos.Count > 0 ? arquivos[0].TryGetLocalPath() : null;
    }

    private async void ProcurarPlanilha(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ImportacaoViewModel vm && await EscolherAsync("Planilha com os lançamentos", Planilha) is { } c) vm.Planilhas.Arquivo = c;
    }

    private async void ProcurarOfx(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ImportacaoViewModel vm
            && await EscolherAsync("Extrato bancário (OFX)", new FilePickerFileType("Extrato OFX") { Patterns = ["*.ofx", "*.OFX"] }) is { } c)
            vm.Extratos.Arquivo = c;
    }

    private async void ProcurarPadrao(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ImportacaoViewModel vm && await EscolherAsync("Lançamentos no layout padrão", Planilha) is { } c) vm.LancamentosPadrao.Arquivo = c;
    }
}
