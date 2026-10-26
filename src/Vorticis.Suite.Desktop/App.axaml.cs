using Avalonia.Markup.Xaml;

namespace Vorticis.Suite.Desktop;

/// <summary>Vorticis Suite Empresarial: os módulos do escritório contábil numa janela; o Escritório faz o login.</summary>
public partial class App : AppSuiteBase
{
    protected override string NomeSuite => "Vorticis Suite Empresarial";
    protected override IReadOnlyList<(string Executavel, Func<MainViewModelBase> Criar)> Modulos => ModulosDaSuite.Todos;
    protected override IEnumerable<Avalonia.Application> AppsDosModulos() => ModulosDaSuite.Apps();
    protected override void CarregarXaml() => AvaloniaXamlLoader.Load(this);
}
