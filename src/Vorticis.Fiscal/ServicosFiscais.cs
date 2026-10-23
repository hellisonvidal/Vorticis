using System.Runtime.CompilerServices;
using Vorticis.Fiscal.Comum;
using Vorticis.Fiscal.Contabilizacao;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Guias;
using Vorticis.Fiscal.Lucro;
using Vorticis.Fiscal.Produtos;
using Vorticis.Fiscal.Postgres;
using Vorticis.Fiscal.Retencoes;
using Vorticis.Fiscal.Simples;
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
        var autorizador = app.Autorizador;
        var repoDocumentos = new RepositorioDocumentos(ds);
        var repoContabilizacao = new RepositorioContabilizacao(ds);
        var repoSimples = new RepositorioSimples(ds);
        var repoLucro = new RepositorioLucro(ds);
        var repoRetencoes = new RepositorioRetencoes(ds);
        Parametros = new ParametrosFiscaisPostgres(ds);
        Documentos = new ServicoDocumentosFiscais(repoDocumentos, autorizador);
        Digitacao = new ServicoDigitacaoFiscal(new RepositorioDigitacao(ds), repoDocumentos, autorizador);
        ParticipantesNota = new ServicoParticipanteNota(app);
        Produtos = new ServicoProdutos(new RepositorioProdutos(ds), autorizador);
        Contabilizacao = new ServicoContabilizacaoFiscal(repoContabilizacao, repoDocumentos, app.Lancamentos, autorizador);
        Simples = new ServicoSimplesNacional(repoSimples, repoDocumentos, Parametros, autorizador);
        Retencoes = new ServicoRetencoes(repoRetencoes, repoDocumentos, Parametros, autorizador);
        Lucro = new ServicoLucro(repoLucro, repoDocumentos, repoRetencoes, Parametros, autorizador);
        Guias = new ServicoGuias(new RepositorioGuias(ds), repoSimples, repoLucro, repoRetencoes, Retencoes, autorizador);
        Provisoes = new ServicoProvisoes(new RepositorioProvisoes(ds), repoContabilizacao, repoSimples, repoLucro, app.Lancamentos, autorizador);
    }

    public IParametrosFiscais Parametros { get; }
    public ServicoDocumentosFiscais Documentos { get; }
    public ServicoDigitacaoFiscal Digitacao { get; }
    public ServicoParticipanteNota ParticipantesNota { get; }
    public ServicoProdutos Produtos { get; }
    public ServicoContabilizacaoFiscal Contabilizacao { get; }
    public ServicoSimplesNacional Simples { get; }
    public ServicoLucro Lucro { get; }
    public ServicoRetencoes Retencoes { get; }
    public ServicoGuias Guias { get; }
    public ServicoProvisoes Provisoes { get; }
}

public static class VorticisAppFiscalExtensions
{
    /// <summary>Serviços do módulo Fiscal para esta aplicação (<c>app.Fiscal().Documentos</c>).</summary>
    public static ServicosFiscais Fiscal(this VorticisApp app) => ServicosFiscais.De(app);
}
