namespace Vorticis.SuiteLogistica.Desktop;

/// <summary>
/// Módulos da Vorticis Suite Logística (hub da transportadora): Frota (coordenação da frota e manutenção), Gerencial (vendas, compras,
/// estoque e financeiro), Contábil, Fiscal e Folha. A Frota faz a conexão e o login; os demais aparecem em ordem alfabética.
/// Entram os que estão instalados (executável na pasta); sem nenhum ao lado (pasta de desenvolvimento), entram todos.
/// </summary>
public static class ModulosDaLogistica
{
    public const string Nome = "Vorticis Suite Logística";
    public const string Principal = "VorticisFrota";

    public static readonly IReadOnlyList<(string Executavel, Func<MainViewModelBase> Criar)> Todos =
    [
        ("VorticisFrota", () => new Vorticis.Frota.Desktop.MainViewModel()),
        ("VorticisGerencial", () => new Vorticis.Erp.Desktop.MainViewModel()),
        ("VorticisContabil", () => new Vorticis.Desktop.ViewModels.MainViewModel()),
        ("VorticisFiscal", () => new Vorticis.Fiscal.Desktop.MainViewModel()),
        ("VorticisFolha", () => new Vorticis.Folha.Desktop.MainViewModel()),
    ];

    public static IReadOnlyList<MainViewModelBase> Criar(string? pasta = null) => HospedeiroSuite.Criar(Todos, pasta);

    public static SuiteViewModel CriarSuite(string? pasta = null) => new(Criar(pasta), nome: Nome, principal: Principal);

    /// <summary>Telas de cada módulo, do App.axaml de cada executável.</summary>
    public static IEnumerable<Avalonia.Application> Apps() =>
    [
        new Vorticis.Frota.Desktop.App(), new Vorticis.Erp.Desktop.App(), new Vorticis.Desktop.App(), new Vorticis.Fiscal.Desktop.App(),
        new Vorticis.Folha.Desktop.App(),
    ];
}
