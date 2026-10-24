using System.Globalization;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Vorticis.Domain.Comum;

namespace Vorticis.Suite.Desktop;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ModulosDaSuite.IncluirTelas(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        CultureInfo.DefaultThreadCurrentCulture = Formato.PtBr;
        CultureInfo.DefaultThreadCurrentUICulture = Formato.PtBr;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var suite = new SuiteViewModel(ModulosDaSuite.Criar());
            ErrosInesperados.Instalar(m => suite.Ativo.Vm.MostrarErroInesperado(m));   // erro não previsto não fecha a Suite
            var janela = new MainWindow();
            janela.UsarSuite(suite);
            desktop.MainWindow = janela;
            desktop.ShutdownRequested += async (_, _) =>
            {
                foreach (var m in suite.Modulos.Reverse()) await m.Vm.DisposeAsync();   // o principal (dono da conexão) por último
            };
            _ = suite.Principal.IniciarAsync();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
