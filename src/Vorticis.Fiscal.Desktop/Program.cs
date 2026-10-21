using Avalonia;

namespace Vorticis.Fiscal.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => AplicacaoDesktop.Executar<App>(args);

    public static AppBuilder BuildAvaloniaApp() => AplicacaoDesktop.Configurar<App>();
}
