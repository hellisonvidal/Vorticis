using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Cadastros;

namespace Vorticis.Desktop.Comum.ViewModels;

/// <summary>
/// Campo "Logo da empresa" dos cadastros de empresa: carrega PNG/JPG (redimensionado para caber no limite do banco), mostra a prévia,
/// remove e guarda a opção "Usar a logo nos relatórios". Quem usa o editor grava <see cref="Logo"/> junto com o cadastro.
/// </summary>
public sealed partial class LogoEmpresaEditor : ObservableObject
{
    [ObservableProperty] private Bitmap? _previa;
    [ObservableProperty] private bool _usarNosRelatorios = true;
    [ObservableProperty] private string? _erro;

    private byte[]? _imagem;

    /// <summary>Houve mudança (nova imagem, remoção ou opção) desde o último <see cref="Carregar"/>.</summary>
    public bool Alterado { get; private set; }

    public bool TemLogo => _imagem is not null;
    public string Situacao => TemLogo ? "Logo carregada." : "Nenhuma logo carregada.";

    /// <summary>Logo a gravar (nula = sem logo).</summary>
    public LogoEmpresa? Logo => _imagem is null ? null : new LogoEmpresa(_imagem, UsarNosRelatorios);

    public void Carregar(LogoEmpresa? logo)
    {
        Aplicar(logo?.Imagem);
        UsarNosRelatorios = logo?.UsarNosRelatorios ?? true;
        Erro = null;
        Alterado = false;
    }

    partial void OnUsarNosRelatoriosChanged(bool value) => Alterado = true;

    /// <summary>Lê a imagem escolhida: aceita PNG ou JPG, reduz (lado maior de 600 px) e guarda em PNG. Devolve a mensagem de erro, se houver.</summary>
    public string? DefinirImagem(byte[] bytes)
    {
        Erro = null;
        if (!LogoEmpresa.EhPng(bytes) && !LogoEmpresa.EhJpg(bytes)) return Erro = "Escolha uma imagem PNG ou JPG.";
        try
        {
            var reduzida = Reduzir(bytes);
            if (LogoEmpresa.Validar(reduzida) is { } erro) return Erro = erro;
            Aplicar(reduzida);
            Alterado = true;
            return null;
        }
        catch (Exception)
        {
            return Erro = "Não foi possível ler a imagem. Escolha outro arquivo PNG ou JPG.";
        }
    }

    [RelayCommand]
    public void Remover()
    {
        Aplicar(null);
        Alterado = true;
        Erro = null;
    }

    private void Aplicar(byte[]? imagem)
    {
        _imagem = imagem;
        Previa?.Dispose();
        Previa = imagem is null ? null : new Bitmap(new MemoryStream(imagem));
        OnPropertyChanged(nameof(TemLogo));
        OnPropertyChanged(nameof(Situacao));
    }

    /// <summary>Redimensiona (sem ampliar) até caber no limite de tamanho; sempre devolve PNG.</summary>
    internal static byte[] Reduzir(byte[] bytes)
    {
        using var origem = new Bitmap(new MemoryStream(bytes));
        var lado = LogoEmpresa.LadoMaximoPx;
        byte[] saida = [];
        for (var tentativa = 0; tentativa < 4; tentativa++, lado /= 2)
        {
            var escala = Math.Min(1.0, (double)lado / Math.Max(origem.PixelSize.Width, origem.PixelSize.Height));
            var tam = new Avalonia.PixelSize(Math.Max(1, (int)Math.Round(origem.PixelSize.Width * escala)), Math.Max(1, (int)Math.Round(origem.PixelSize.Height * escala)));
            using var reduzida = origem.CreateScaledBitmap(tam, BitmapInterpolationMode.HighQuality);
            using var ms = new MemoryStream();
            reduzida.Save(ms);
            saida = ms.ToArray();
            if (saida.Length <= LogoEmpresa.TamanhoMaximo) break;
        }
        return saida;
    }
}
