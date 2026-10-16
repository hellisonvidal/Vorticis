using Npgsql;
using Perseus.Application.Cadastros;
using Perseus.Application.Contabil;
using Perseus.Infra.Migracoes;
using Perseus.Infra.Postgres;

namespace Perseus.Infra;

/// <summary>Composição dos serviços da aplicação sobre um banco PostgreSQL (raiz de composição).</summary>
public sealed class PerseusApp : IAsyncDisposable
{
    private readonly NpgsqlDataSource _ds;

    private PerseusApp(NpgsqlDataSource ds)
    {
        _ds = ds;
        var repoContabil = new RepositorioContabil(ds);
        Lancamentos = new ServicoLancamento(repoContabil);
        Periodos = new ServicoPeriodo(repoContabil);
        Consultas = new ConsultasContabeis(ds);
        Relatorios = new ServicoRelatorios(Consultas);
        RepositorioEmpresas = new RepositorioEmpresa(ds);
        Empresas = new ServicoEmpresa(RepositorioEmpresas, new PlanoModeloEmbutido());
        Migrador = new Migrador(ds);
    }

    public ServicoLancamento Lancamentos { get; }
    public ServicoPeriodo Periodos { get; }
    public ServicoRelatorios Relatorios { get; }
    public ServicoEmpresa Empresas { get; }
    public IRepositorioEmpresa RepositorioEmpresas { get; }
    public IConsultasContabeis Consultas { get; }
    public Migrador Migrador { get; }

    /// <summary>Conecta e aplica migrações pendentes.</summary>
    public static async Task<PerseusApp> IniciarAsync(string connectionString, CancellationToken ct = default)
    {
        var ds = NpgsqlDataSource.Create(connectionString);
        var app = new PerseusApp(ds);
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
