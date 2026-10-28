namespace Vorticis.Application.Cadastros;

/// <summary>Logo da empresa (PNG ou JPG, já redimensionada) e a opção de usá-la no cabeçalho dos relatórios.</summary>
public sealed record LogoEmpresa(byte[] Imagem, bool UsarNosRelatorios)
{
    /// <summary>Tamanho máximo da imagem gravada (a tela redimensiona antes de enviar).</summary>
    public const int TamanhoMaximo = 512 * 1024;
    /// <summary>Lado maior, em pixels, após o redimensionamento.</summary>
    public const int LadoMaximoPx = 600;

    public static bool EhPng(ReadOnlySpan<byte> b) => b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;
    public static bool EhJpg(ReadOnlySpan<byte> b) => b.Length > 8 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;

    /// <summary>Erro de validação da imagem (formato e tamanho) ou null se estiver correta.</summary>
    public static string? Validar(byte[] imagem)
    {
        if (imagem.Length == 0) return "Imagem vazia.";
        if (!EhPng(imagem) && !EhJpg(imagem)) return "A logo deve ser uma imagem PNG ou JPG.";
        if (imagem.Length > TamanhoMaximo) return $"A logo ficou com {imagem.Length / 1024} KB; o máximo é {TamanhoMaximo / 1024} KB. Use uma imagem menor.";
        return null;
    }
}
