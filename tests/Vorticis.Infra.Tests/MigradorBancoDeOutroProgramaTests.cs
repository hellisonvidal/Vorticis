using Npgsql;
using Vorticis.Infra.Migracoes;

namespace Vorticis.Infra.Tests;

/// <summary>Banco usado por outro programa Vorticis (ex.: Suite Logística): o migrador explica em vez de mostrar o erro cru.</summary>
public class MigradorBancoDeOutroProgramaTests
{
    private static async Task<string> CriarBancoAsync()
    {
        var nome = "vorticis_outro_" + Guid.NewGuid().ToString("N");
        await using var srv = new NpgsqlConnection(BancoDeTeste.ConexaoServidor);
        await srv.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE {nome}", srv);
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return nome;
    }

    private static async Task RemoverAsync(string nome)
    {
        NpgsqlConnection.ClearAllPools();
        await using var srv = new NpgsqlConnection(BancoDeTeste.ConexaoServidor);
        await srv.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS {nome} WITH (FORCE)", srv);
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Recusa_banco_com_migracao_desconhecida_com_mensagem_clara()
    {
        BancoDeTeste.ExigirBanco();
        var nome = await CriarBancoAsync();
        try
        {
            var cs = new NpgsqlConnectionStringBuilder(BancoDeTeste.ConexaoServidor) { Database = nome, Pooling = false }.ConnectionString;
            await using (var con = new NpgsqlConnection(cs))
            {
                await con.OpenAsync(TestContext.Current.CancellationToken);
                await using var cmd = new NpgsqlCommand("""
                    CREATE TABLE public.vorticis_migracao (nome TEXT PRIMARY KEY, sha256 CHAR(64) NOT NULL, aplicada_em TIMESTAMPTZ NOT NULL DEFAULT now());
                    INSERT INTO public.vorticis_migracao (nome, sha256) VALUES ('999_de_outro_produto.sql', repeat('a', 64))
                    """, con);
                await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }
            await using var ds = NpgsqlDataSource.Create(cs);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new Migrador(ds).AplicarAsync(TestContext.Current.CancellationToken));
            Assert.Contains("usado por outro programa Vorticis", ex.Message);
            Assert.Contains("999_de_outro_produto.sql", ex.Message);
            Assert.Contains("Configurar automaticamente", ex.Message);
        }
        finally { await RemoverAsync(nome); }
    }

    [Fact]
    public async Task Explica_objeto_que_ja_existe_sem_tentar_consertar_o_banco()
    {
        BancoDeTeste.ExigirBanco();
        var nome = await CriarBancoAsync();
        try
        {
            var cs = new NpgsqlConnectionStringBuilder(BancoDeTeste.ConexaoServidor) { Database = nome, Pooling = false }.ConnectionString;
            var primeira = Migrador.ScriptsEmbutidos()[0].Sql;
            // a primeira migração cria objetos; aplicada à mão (sem registro) ela colide com a do migrador
            await using (var con = new NpgsqlConnection(cs))
            {
                await con.OpenAsync(TestContext.Current.CancellationToken);
                await using var cmd = new NpgsqlCommand(primeira, con);
                await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }
            await using var ds = NpgsqlDataSource.Create(cs);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new Migrador(ds).AplicarAsync(TestContext.Current.CancellationToken));
            Assert.Contains("outro programa Vorticis", ex.Message);
            Assert.Contains("Configurar automaticamente", ex.Message);
        }
        finally { await RemoverAsync(nome); }
    }
}
