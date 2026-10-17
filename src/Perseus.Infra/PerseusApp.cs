using Npgsql;
using Perseus.Application.Cadastros;
using Perseus.Application.Contabil;
using Perseus.Application.Relatorios;
using Perseus.Application.Seguranca;
using Perseus.Application.Sistema;
using Perseus.Infra.Sistema;
using Perseus.Infra.Cnpj;
using Perseus.Infra.Migracoes;
using Perseus.Infra.Postgres;
using Perseus.Infra.Relatorios;

namespace Perseus.Infra;

/// <summary>Composição dos serviços da aplicação sobre um banco PostgreSQL (raiz de composição).</summary>
public sealed class PerseusApp : IAsyncDisposable
{
    private readonly NpgsqlDataSource _ds;

    private PerseusApp(NpgsqlDataSource ds, string connectionString, IConsultaCnpj consultaCnpj, IConsultaInscricaoEstadual consultaIe)
    {
        _ds = ds;
        var repoPerfis = new RepositorioPerfis(ds);
        var autorizador = new Autorizador(repoPerfis);
        Autorizador = autorizador;
        Usuarios = new ServicoUsuarios(new RepositorioUsuarios(ds));
        Perfis = new ServicoPerfis(repoPerfis);
        var repoContabil = new RepositorioContabil(ds);
        Lancamentos = new ServicoLancamento(repoContabil, autorizador);
        Periodos = new ServicoPeriodo(repoContabil, autorizador);
        PlanoContas = new ServicoPlanoContas(new RepositorioPlanoContas(ds), autorizador);
        Importacao = new ServicoImportacao(new RepositorioImportacao(ds), Lancamentos, autorizador);
        Consultas = new ConsultasContabeis(ds);
        Relatorios = new ServicoRelatorios(Consultas, autorizador);
        RepositorioEmpresas = new RepositorioEmpresa(ds);
        Empresas = new ServicoEmpresa(RepositorioEmpresas, new PlanoModeloEmbutido(), consultaCnpj, consultaIe);
        Licenca = new ServicoLicenca(new RepositorioConfiguracao(ds));
        Encerramento = new ServicoEncerramento(repoContabil, Consultas, autorizador);
        PlanoReferencial = new ServicoPlanoReferencial(new RepositorioPlanoReferencial(ds), autorizador);
        Responsaveis = new ServicoResponsaveis(new RepositorioResponsaveis(ds), RepositorioEmpresas, autorizador, consultaCnpj);
        Demonstracoes = new ServicoDemonstracoes(Consultas, autorizador);
        Livros = new ServicoLivros(Relatorios, Demonstracoes, Consultas, RepositorioEmpresas, autorizador, new GeradorPdf());
        Backup = new ServicoBackup(new BackupPostgres(connectionString));
        Migrador = new Migrador(ds);
    }

    public Autorizador Autorizador { get; }
    public ServicoUsuarios Usuarios { get; }
    public ServicoPerfis Perfis { get; }
    public ServicoLicenca Licenca { get; }
    public ServicoLancamento Lancamentos { get; }
    public ServicoPeriodo Periodos { get; }
    public ServicoPlanoContas PlanoContas { get; }
    public ServicoImportacao Importacao { get; }
    public ServicoRelatorios Relatorios { get; }
    public ServicoEmpresa Empresas { get; }
    public ServicoEncerramento Encerramento { get; }
    public ServicoPlanoReferencial PlanoReferencial { get; }
    public ServicoResponsaveis Responsaveis { get; }
    public ServicoDemonstracoes Demonstracoes { get; }
    public ServicoLivros Livros { get; }
    public ServicoBackup Backup { get; }
    public IRepositorioEmpresa RepositorioEmpresas { get; }
    public IConsultasContabeis Consultas { get; }
    public Migrador Migrador { get; }

    /// <summary>Conecta e aplica migrações pendentes.</summary>
    /// <param name="consultaCnpj">Fonte de dados de CNPJ; padrão: base pública da RFB via internet.</param>
    public static async Task<PerseusApp> IniciarAsync(string connectionString, IConsultaCnpj? consultaCnpj = null, CancellationToken ct = default,
                                                      IConsultaInscricaoEstadual? consultaIe = null)
    {
        var ds = NpgsqlDataSource.Create(connectionString);
        var app = new PerseusApp(ds, connectionString, consultaCnpj ?? new ConsultaCnpjPublica(ConsultaCnpjPublica.CriarHttpClient()),
                                 consultaIe ?? new ConsultaInscricaoEstadualPublica(ConsultaCnpjPublica.CriarHttpClient()));
        try
        {
            await app.Migrador.AplicarAsync(ct);
            return app;
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync() => _ds.DisposeAsync();
}
