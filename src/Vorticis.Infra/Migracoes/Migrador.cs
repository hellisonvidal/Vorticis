using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace Vorticis.Infra.Migracoes;

public sealed record MigracaoAplicada(string Nome, bool Nova);

/// <summary>
/// Aplica os scripts db/NNN_*.sql (embutidos no assembly) em ordem, cada um na sua transação.
/// Um script já aplicado cujo conteúdo mudou é erro: migrações publicadas não se editam, criam-se novas.
/// </summary>
public sealed class Migrador(NpgsqlDataSource dataSource)
{
    private const string Prefixo = "Migracoes.";

    public static IReadOnlyList<(string Nome, string Sql)> ScriptsEmbutidos()
    {
        var asm = typeof(Migrador).Assembly;
        return asm.GetManifestResourceNames()
            .Where(n => n.StartsWith(Prefixo, StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => (n[Prefixo.Length..], Ler(asm, n)))
            .ToArray();
    }

    public async Task<IReadOnlyList<MigracaoAplicada>> AplicarAsync(CancellationToken ct = default)
    {
        await using var con = await dataSource.OpenConnectionAsync(ct);
        // Bancos criados antes da troca de nome (Perseus) controlam as migrações em perseus_migracao. Se ela existe
        // (inclusive depois de restaurar um backup antigo), é a que vale.
        await using (var cmd = new NpgsqlCommand("""
            DO $$
            BEGIN
                IF to_regclass('public.perseus_migracao') IS NOT NULL THEN
                    DROP TABLE IF EXISTS public.vorticis_migracao;
                    ALTER TABLE public.perseus_migracao RENAME TO vorticis_migracao;
                END IF;
            END $$
            """, con))
            await cmd.ExecuteNonQueryAsync(ct);
        await using (var cmd = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS public.vorticis_migracao (
                nome        TEXT PRIMARY KEY,
                sha256      CHAR(64)    NOT NULL,
                aplicada_em TIMESTAMPTZ NOT NULL DEFAULT now())
            """, con))
            await cmd.ExecuteNonQueryAsync(ct);

        var aplicadas = new Dictionary<string, string>();
        await using (var cmd = new NpgsqlCommand("SELECT nome, sha256 FROM public.vorticis_migracao", con))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) aplicadas[r.GetString(0)] = r.GetString(1);

        var scripts = ScriptsEmbutidos();
        var conhecidas = scripts.Select(x => x.Nome).ToHashSet(StringComparer.Ordinal);
        var desconhecidas = aplicadas.Keys.Where(n => !conhecidas.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        if (desconhecidas.Length > 0)
            throw new InvalidOperationException(
                "Este banco de dados foi usado por outro programa Vorticis (ex.: Suite Logística) e tem atualizações que este programa não reconhece: "
                + string.Join(", ", desconhecidas)
                + ". Use um banco próprio: Configurações › Conexão › Configurar automaticamente.");

        var resultado = new List<MigracaoAplicada>();
        foreach (var (nome, sql) in scripts)
        {
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
            if (aplicadas.TryGetValue(nome, out var hashAplicado))
            {
                if (hashAplicado != hash)
                {
                    // Builds intermediárias da troca de nome publicaram as migrações 010 a 017 com "Perseus" trocado por
                    // "Vorticis" (os textos originais foram restaurados). Bancos criados por elas são aceitos e passam a
                    // registrar o hash original; qualquer outra diferença continua sendo erro.
                    if (hashAplicado != HashTrocaDeNome(sql))
                        throw new InvalidOperationException($"A migração {nome} foi alterada depois de aplicada. Crie uma nova migração em vez de editar esta.");
                    await using var corrige = new NpgsqlCommand("UPDATE public.vorticis_migracao SET sha256 = @h WHERE nome = @n", con);
                    corrige.Parameters.AddWithValue("h", hash);
                    corrige.Parameters.AddWithValue("n", nome);
                    await corrige.ExecuteNonQueryAsync(ct);
                }
                resultado.Add(new(nome, false));
                continue;
            }

            await using var tx = await con.BeginTransactionAsync(ct);
            try
            {
                await using var cmd = new NpgsqlCommand(sql, con, tx);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.DuplicateTable or PostgresErrorCodes.DuplicateColumn
                                                or PostgresErrorCodes.DuplicateObject or PostgresErrorCodes.DuplicateSchema or PostgresErrorCodes.DuplicateFunction)
            {
                throw new InvalidOperationException(
                    $"A migração {nome} não pôde ser aplicada porque o banco já tem um objeto com o mesmo nome ({ex.MessageText}). "
                    + "Isso acontece quando o banco foi usado por outro programa Vorticis (ex.: Suite Logística). "
                    + "Use um banco próprio: Configurações › Conexão › Configurar automaticamente.", ex);
            }
            await using (var cmd = new NpgsqlCommand("INSERT INTO public.vorticis_migracao (nome, sha256) VALUES (@n, @h)", con, tx))
            {
                cmd.Parameters.AddWithValue("n", nome);
                cmd.Parameters.AddWithValue("h", hash);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
            resultado.Add(new(nome, true));
        }
        return resultado;
    }

    private static string HashTrocaDeNome(string sql) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            sql.Replace("Perseus", "Vorticis").Replace("PERSEUS", "VORTICIS").Replace("perseus", "vorticis"))));

    private static string Ler(Assembly asm, string recurso)
    {
        using var s = asm.GetManifestResourceStream(recurso)!;
        using var r = new StreamReader(s, Encoding.UTF8);
        return r.ReadToEnd();
    }
}
