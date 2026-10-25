using Avalonia;

namespace Vorticis.SuiteLogistica.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => AplicacaoDesktop.Executar<App>(args);

    public static AppBuilder BuildAvaloniaApp() => AplicacaoDesktop.Configurar<App>();
}
