using Npgsql;
using Perseus.Application.Cadastros;
using Perseus.Infra;

namespace Perseus.Infra.Tests;

/// <summary>
/// Cria um banco PostgreSQL descartável por classe de teste. Requer a variável de ambiente
/// PERSEUS_TEST_PG (connection string de um usuário com permissão CREATEDB); sem ela, os testes são pulados.
/// </summary>
public sealed class BancoDeTeste : IAsyncLifetime
{
    public static readonly string? ConexaoServidor = Environment.GetEnvironmentVariable("PERSEUS_TEST_PG");
    private readonly string _nome = $"perseus_teste_{Guid.NewGuid():N}";

    public PerseusApp App { get; private set; } = null!;
    public string ConnectionString { get; private set; } = "";

    public async ValueTask InitializeAsync()
    {
        if (ConexaoServidor is null) return;
        await ExecutarNoServidor($"CREATE DATABASE {_nome}");
        ConnectionString = new NpgsqlConnectionStringBuilder(ConexaoServidor) { Database = _nome }.ConnectionString;
        App = await PerseusApp.IniciarAsync(ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (ConexaoServidor is null) return;
        await App.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await ExecutarNoServidor($"DROP DATABASE IF EXISTS {_nome} WITH (FORCE)");
    }

    public static void ExigirBanco() => Assert.SkipWhen(ConexaoServidor is null, "Defina PERSEUS_TEST_PG para rodar testes de integração.");

    public async Task<long> CriarEmpresaAsync(string cnpj, int ano = 2026)
    {
        var r = await App.Empresas.CadastrarAsync(new NovaEmpresa(cnpj, "Empresa de Teste LTDA", null, "2062", "4751201",
            "SP", "3550308", new DateOnly(2020, 1, 1), RegimeTributario.LucroPresumido, ano), TestContext.Current.CancellationToken);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        return r.Valor;
    }

    public async Task<T> EscalarAsync<T>(string sql, params (string, object)[] parametros)
    {
        await using var con = new NpgsqlConnection(ConnectionString);
        await con.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, con);
        foreach (var (n, v) in parametros) cmd.Parameters.AddWithValue(n, v);
        return (T)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task ExecutarNoServidor(string sql)
    {
        await using var con = new NpgsqlConnection(ConexaoServidor);
        await con.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, con);
        await cmd.ExecuteNonQueryAsync();
    }
}
