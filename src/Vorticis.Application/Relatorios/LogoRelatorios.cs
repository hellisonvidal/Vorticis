namespace Vorticis.Application.Relatorios;

/// <summary>
/// Logo da empresa ativa para o cabeçalho dos PDFs. A tela a define ao trocar de empresa (ou ao gravar a logo); fica nula quando a
/// empresa não tem logo ou a opção "Usar a logo nos relatórios" está desmarcada. Um documento pode trazer a sua própria logo.
/// </summary>
public static class LogoRelatorios
{
    private static volatile byte[]? _atual;
    public static byte[]? Atual { get => _atual; set => _atual = value; }
}
