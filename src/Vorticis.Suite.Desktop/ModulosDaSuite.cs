namespace Vorticis.Suite.Desktop;

/// <summary>
/// Módulos hospedados pela Vorticis Suite (escritório contábil): os instalados (o executável do módulo está na pasta da Suite). A Suite os
/// mostra em ordem alfabética, e o Escritório (quando instalado) faz a conexão e o login. Sem nenhum executável ao lado (pasta de
/// desenvolvimento), entram todos.
/// </summary>
public static class ModulosDaSuite
{
    public static readonly IReadOnlyList<(string Executavel, Func<MainViewModelBase> Criar)> Todos =
    [
        ("VorticisEscritorio", () => new Vorticis.Escritorio.Desktop.MainViewModel()),
        ("VorticisContabil", () => new Vorticis.Desktop.ViewModels.MainViewModel()),
        ("VorticisFiscal", () => new Vorticis.Fiscal.Desktop.MainViewModel()),
        ("VorticisFolha", () => new Vorticis.Folha.Desktop.MainViewModel()),
        ("VorticisCondominio", () => new Vorticis.Condominio.Desktop.MainViewModel()),
        ("VorticisGerencial", () => new Vorticis.Erp.Desktop.MainViewModel()),
    ];

    /// <summary>Executáveis dos módulos instalados (nomes sem extensão).</summary>
    public static IReadOnlyList<string> Instalados(string pasta) => HospedeiroSuite.Instalados(pasta, Todos.Select(t => t.Executavel));

    public static IReadOnlyList<MainViewModelBase> Criar(string? pasta = null) => HospedeiroSuite.Criar(Todos, pasta);

    /// <summary>Telas de cada módulo, do App.axaml de cada executável.</summary>
    public static IEnumerable<Avalonia.Application> Apps() =>
    [
        new Vorticis.Escritorio.Desktop.App(), new Vorticis.Desktop.App(), new Vorticis.Fiscal.Desktop.App(),
        new Vorticis.Folha.Desktop.App(), new Vorticis.Condominio.Desktop.App(), new Vorticis.Erp.Desktop.App(),
    ];

    public static void IncluirTelas(Avalonia.Application destino) => HospedeiroSuite.IncluirTelas(destino, Apps());
}
