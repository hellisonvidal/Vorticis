using Avalonia.Markup.Xaml;

namespace Vorticis.SuiteLogistica.Desktop;

/// <summary>Vorticis Suite Logística: Frota, Gerencial, Contábil, Fiscal e Folha numa janela; a Frota faz o login.</summary>
public partial class App : AppSuiteBase
{
    protected override string NomeSuite => ModulosDaLogistica.Nome;
    protected override string? Principal => ModulosDaLogistica.Principal;
    protected override IReadOnlyList<(string Executavel, Func<MainViewModelBase> Criar)> Modulos => ModulosDaLogistica.Todos;
    protected override IEnumerable<Avalonia.Application> AppsDosModulos() => ModulosDaLogistica.Apps();
    protected override void CarregarXaml() => AvaloniaXamlLoader.Load(this);
}
