using System.Runtime.CompilerServices;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Postgres;
using Vorticis.Infra;

namespace Vorticis.Fiscal;

/// <summary>
/// Composição dos serviços do módulo Fiscal sobre o mesmo banco e as mesmas permissões do <see cref="VorticisApp"/>
/// (raiz de composição do núcleo). Cada <see cref="VorticisApp"/> tem uma instância única, obtida por <see cref="De"/>.
/// </summary>
public sealed class ServicosFiscais
{
    private static readonly ConditionalWeakTable<VorticisApp, ServicosFiscais> Instancias = new();

    public static ServicosFiscais De(VorticisApp app) => Instancias.GetValue(app, a => new ServicosFiscais(a));

    private ServicosFiscais(VorticisApp app)
    {
        var ds = app.DataSource;
        Documentos = new ServicoDocumentosFiscais(new RepositorioDocumentos(ds), app.Autorizador);
    }

    public ServicoDocumentosFiscais Documentos { get; }
}

public static class VorticisAppFiscalExtensions
{
    /// <summary>Serviços do módulo Fiscal para esta aplicação (<c>app.Fiscal().Documentos</c>).</summary>
    public static ServicosFiscais Fiscal(this VorticisApp app) => ServicosFiscais.De(app);
}
