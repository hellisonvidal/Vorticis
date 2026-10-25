using Avalonia.Controls.Templates;

namespace Vorticis.Suite.Desktop;

/// <summary>
/// Módulos hospedados pela Suite: os instalados (o executável do módulo está na pasta da Suite). A Suite os mostra em ordem
/// alfabética, e o Escritório (quando instalado) faz a conexão e o login. Sem nenhum executável ao lado (pasta de
/// desenvolvimento), entram todos.
/// </summary>
public static class ModulosDaSuite
{
    private static readonly (string Executavel, Func<MainViewModelBase> Criar)[] Todos =
    [
        ("VorticisEscritorio", () => new Vorticis.Escritorio.Desktop.MainViewModel()),
        ("VorticisContabil", () => new Vorticis.Desktop.ViewModels.MainViewModel()),
        ("VorticisFiscal", () => new Vorticis.Fiscal.Desktop.MainViewModel()),
        ("VorticisFolha", () => new Vorticis.Folha.Desktop.MainViewModel()),
        ("VorticisCondominio", () => new Vorticis.Condominio.Desktop.MainViewModel()),
        ("VorticisGerencial", () => new Vorticis.Erp.Desktop.MainViewModel()),
    ];

    /// <summary>Executáveis dos módulos instalados (nomes sem extensão).</summary>
    public static IReadOnlyList<string> Instalados(string pasta) =>
        [.. Todos.Select(t => t.Executavel).Where(e => File.Exists(Path.Combine(pasta, e + ".exe")) || File.Exists(Path.Combine(pasta, e)))];

    public static IReadOnlyList<MainViewModelBase> Criar(string? pasta = null)
    {
        var instalados = Instalados(pasta ?? AppContext.BaseDirectory);
        return [.. Todos.Where(t => instalados.Count == 0 || instalados.Contains(t.Executavel)).Select(t => t.Criar())];
    }

    /// <summary>
    /// Leva para a Suite as telas (DataTemplates) de cada módulo, carregando o App.axaml de cada executável: assim cada
    /// tela continua declarada num lugar só, o App.axaml do próprio módulo.
    /// </summary>
    public static void IncluirTelas(Avalonia.Application destino)
    {
        Avalonia.Application[] modulos =
        [
            new Vorticis.Escritorio.Desktop.App(), new Vorticis.Desktop.App(), new Vorticis.Fiscal.Desktop.App(),
            new Vorticis.Folha.Desktop.App(), new Vorticis.Condominio.Desktop.App(), new Vorticis.Erp.Desktop.App(),
        ];
        foreach (var app in modulos)
        {
            app.Initialize();
            foreach (var t in app.DataTemplates.Where(t => t is not TemplatesComuns).ToList())
            {
                app.DataTemplates.Remove(t);
                destino.DataTemplates.Add(t);
            }
        }
    }
}
