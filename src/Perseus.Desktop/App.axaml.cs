using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Perseus.Desktop.ViewModels;
using Perseus.Desktop.Views;
using Perseus.Domain.Comum;

namespace Perseus.Desktop;

public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        CultureInfo.DefaultThreadCurrentCulture = Formato.PtBr;
        CultureInfo.DefaultThreadCurrentUICulture = Formato.PtBr;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();
            desktop.MainWindow = new MainWindow { DataContext = vm };
            desktop.ShutdownRequested += async (_, _) => await vm.DisposeAsync();
            _ = vm.IniciarAsync();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
