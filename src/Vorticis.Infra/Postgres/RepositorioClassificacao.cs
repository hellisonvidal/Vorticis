using Npgsql;
using Vorticis.Application.Contabil;
using Vorticis.Domain.Contabil;

namespace Vorticis.Infra.Postgres;

public sealed class RepositorioClassificacao(NpgsqlDataSource ds) : IRepositorioClassificacao
{
    public async Task<IReadOnlyList<ClassificacaoConta>> ListarAsync(long empresaId, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("SELECT conta_id, classe, nao_recorrente FROM contabil.classificacao_conta WHERE empresa_id = @e").Com("e", empresaId);
        var res = new List<ClassificacaoConta>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            if (ClassesConta.DeCodigo(r.GetString(1)) is { } classe) res.Add(new ClassificacaoConta(r.GetInt64(0), classe, r.GetBoolean(2)));
        return res;
    }

    public async Task SubstituirAsync(long empresaId, IReadOnlyList<ClassificacaoConta> itens, CancellationToken ct)
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        await using (var del = new NpgsqlCommand("DELETE FROM contabil.classificacao_conta WHERE empresa_id = @e", con, tx))
        {
            del.Parameters.AddWithValue("e", empresaId);
            await del.ExecuteNonQueryAsync(ct);
        }
        foreach (var i in itens)
        {
            await using var ins = new NpgsqlCommand(
                "INSERT INTO contabil.classificacao_conta (empresa_id, conta_id, classe, nao_recorrente) VALUES (@e, @c, @k, @n)", con, tx);
            ins.Parameters.AddWithValue("e", empresaId);
            ins.Parameters.AddWithValue("c", i.ContaId);
            ins.Parameters.AddWithValue("k", i.Classe.Codigo());
            ins.Parameters.AddWithValue("n", i.NaoRecorrente);
            await ins.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyDictionary<long, string>> ListarReferenciaisAsync(long empresaId, int ano, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT m.conta_id, p.codigo
              FROM contabil.mapeamento_referencial m JOIN contabil.plano_referencial p ON p.id = m.plano_referencial_id
             WHERE m.empresa_id = @e AND m.centro_custo_id IS NULL
               AND m.ano_vigencia = (SELECT MAX(x.ano_vigencia) FROM contabil.mapeamento_referencial x WHERE x.empresa_id = @e AND x.ano_vigencia <= @a)
            """).Com("e", empresaId).Com("a", (short)ano);
        var res = new Dictionary<long, string>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) res[r.GetInt64(0)] = r.GetString(1);
        return res;
    }
}
