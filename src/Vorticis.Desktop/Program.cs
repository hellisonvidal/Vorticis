using Avalonia;

namespace Vorticis.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Configuracao.MigrarPastasDoNomeAnterior();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            BancoDoAplicativo.Encerrar();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
