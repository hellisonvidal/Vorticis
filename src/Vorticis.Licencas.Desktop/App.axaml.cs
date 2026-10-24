using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Vorticis.Licencas.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new JanelaPrincipal { DataContext = new GerenciadorViewModel(GerenciadorViewModel.PastaPadrao()) };
        base.OnFrameworkInitializationCompleted();
    }
}
