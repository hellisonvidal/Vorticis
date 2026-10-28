using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Vorticis.Desktop.Comum.ViewModels;

namespace Vorticis.Desktop.Comum.Views;

public partial class LogoEmpresaView : UserControl
{
    private static readonly FilePickerFileType Imagens = new("Imagem (PNG ou JPG)") { Patterns = ["*.png", "*.PNG", "*.jpg", "*.JPG", "*.jpeg", "*.JPEG"] };

    public LogoEmpresaView()
    {
        InitializeComponent();
        this.FindControl<Button>("CarregarLogo")!.Click += AoCarregar;
    }

    private async void AoCarregar(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LogoEmpresaEditor editor) return;
        var caminho = await EscolhaArquivo.AbrirAsync(this, "Escolher a logo da empresa", Imagens);
        if (caminho is null) return;
        try
        {
            editor.DefinirImagem(await File.ReadAllBytesAsync(caminho));
        }
        catch (Exception ex)
        {
            editor.Erro = $"Não foi possível abrir o arquivo: {ex.Message}";
        }
    }
}
