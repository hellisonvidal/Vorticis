using Npgsql;
using Perseus.Application.Contabil;
using Perseus.Domain.Contabil;

namespace Perseus.Infra.Postgres;

public sealed class RepositorioPlanoContas(NpgsqlDataSource ds) : IRepositorioPlanoContas
{
    public async Task<IReadOnlyList<ContaDetalhe>> ListarAsync(long empresaId, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT c.id, c.codigo, c.descricao, c.tipo = 'A', c.nivel, c.conta_pai_id, c.cod_nat, c.natureza_saldo::text,
                   c.exige_centro_custo, c.exige_participante, c.data_inclusao, c.ativa,
                   EXISTS (SELECT 1 FROM contabil.partida p WHERE p.conta_id = c.id),
                   EXISTS (SELECT 1 FROM contabil.plano_contas f WHERE f.conta_pai_id = c.id),
                   COALESCE((SELECT SUM(s.total_debito - s.total_credito) FROM contabil.saldo_mensal s WHERE s.conta_id = c.id), 0),
                   c.codigo_reduzido
              FROM contabil.plano_contas c WHERE c.empresa_id = @e ORDER BY c.codigo
            """).Com("e", empresaId);
        var res = new List<ContaDetalhe>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            res.Add(new ContaDetalhe(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetBoolean(3), r.GetInt16(4),
                r.IsDBNull(5) ? null : r.GetInt64(5), r.GetString(6), Mapeamentos.DC(r.GetString(7)), r.GetBoolean(8), r.GetBoolean(9),
                r.GetFieldValue<DateOnly>(10), r.GetBoolean(11), r.GetBoolean(12), r.GetBoolean(13), r.GetDecimal(14),
                r.IsDBNull(15) ? null : r.GetInt32(15)));
        return res;
    }

    public Task<long> IncluirAsync(long empresaId, NovaConta c, long? contaPaiId, short nivel, bool analitica, long? paiParaSintetica,
                                   long usuarioId, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            await using var con = await ds.OpenConnectionAsync(ct);
            await using var tx = await con.BeginTransactionAsync(ct);
            await Mapeamentos.DefinirUsuarioAsync(con, tx, usuarioId, ct);
            if (paiParaSintetica is long pai)
                await using (var cmd = new NpgsqlCommand("""
                    UPDATE contabil.plano_contas SET tipo = 'S'
                     WHERE id = @id AND tipo = 'A' AND NOT EXISTS (SELECT 1 FROM contabil.partida WHERE conta_id = @id)
                    """, con, tx).Com("id", pai))
                    if (await cmd.ExecuteNonQueryAsync(ct) != 1)
                        throw new Domain.Comum.ViolacaoRegraException("A conta superior recebeu lançamentos e não pode virar sintética.");

            long id;
            await using (var cmd = new NpgsqlCommand("""
                INSERT INTO contabil.plano_contas (empresa_id, codigo, descricao, tipo, nivel, conta_pai_id, cod_nat, natureza_saldo,
                                                   exige_centro_custo, exige_participante, data_inclusao, codigo_reduzido)
                VALUES (@e, @c, @d, @t::contabil.tipo_conta, @n, @pai, @nat, @sal::contabil.indicador_dc, @cc, @part, @inc, @red) RETURNING id
                """, con, tx)
                .Com("e", empresaId).Com("c", c.Codigo).Com("d", c.Descricao).Com("t", analitica ? "A" : "S").Com("n", nivel)
                .Com("pai", NpgsqlTypes.NpgsqlDbType.Bigint, contaPaiId).Com("nat", c.CodNat).Com("sal", c.NaturezaSaldo.ToString())
                .Com("cc", c.ExigeCentroCusto).Com("part", c.ExigeParticipante).Com("inc", c.DataInclusao)
                .Com("red", NpgsqlTypes.NpgsqlDbType.Integer, c.CodigoReduzido))
                id = (long)(await cmd.ExecuteScalarAsync(ct))!;
            await tx.CommitAsync(ct);
            return id;
        });

    public Task AlterarAsync(long contaId, string descricao, IndicadorDC naturezaSaldo, bool exigeCentroCusto, bool exigeParticipante, bool ativa,
                             long usuarioId, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            await using var con = await ds.OpenConnectionAsync(ct);
            await using var tx = await con.BeginTransactionAsync(ct);
            await Mapeamentos.DefinirUsuarioAsync(con, tx, usuarioId, ct);
            await using (var cmd = new NpgsqlCommand("""
                UPDATE contabil.plano_contas
                   SET descricao = @d, natureza_saldo = @s::contabil.indicador_dc, exige_centro_custo = @cc,
                       exige_participante = @p, ativa = @a
                 WHERE id = @id
                """, con, tx).Com("d", descricao).Com("s", naturezaSaldo.ToString()).Com("cc", exigeCentroCusto)
                             .Com("p", exigeParticipante).Com("a", ativa).Com("id", contaId))
                await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return 0;
        });

    public Task AlterarReduzidoAsync(long contaId, int reduzido, long usuarioId, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            await using var con = await ds.OpenConnectionAsync(ct);
            await using var tx = await con.BeginTransactionAsync(ct);
            await Mapeamentos.DefinirUsuarioAsync(con, tx, usuarioId, ct);
            await using (var cmd = new NpgsqlCommand("UPDATE contabil.plano_contas SET codigo_reduzido = @r WHERE id = @id", con, tx)
                             .Com("r", reduzido).Com("id", contaId))
                await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return 0;
        });

    public Task ExcluirAsync(long contaId, long usuarioId, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            await using var con = await ds.OpenConnectionAsync(ct);
            await using var tx = await con.BeginTransactionAsync(ct);
            await Mapeamentos.DefinirUsuarioAsync(con, tx, usuarioId, ct);
            await using (var cmd = new NpgsqlCommand("""
                DELETE FROM contabil.mapeamento_referencial WHERE conta_id = @id;
                DELETE FROM contabil.saldo_mensal WHERE conta_id = @id AND total_debito = 0 AND total_credito = 0;
                DELETE FROM contabil.plano_contas WHERE id = @id;
                """, con, tx).Com("id", contaId))
                await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return 0;
        });

    public Task SubstituirAsync(long empresaId, IReadOnlyList<Application.Cadastros.ContaModelo> plano, long usuarioId, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            await using var con = await ds.OpenConnectionAsync(ct);
            await using var tx = await con.BeginTransactionAsync(ct);
            await Mapeamentos.DefinirUsuarioAsync(con, tx, usuarioId, ct);
            DateOnly inicio;
            await using (var cmd = new NpgsqlCommand("""
                SELECT EXISTS (SELECT 1 FROM contabil.partida p JOIN contabil.plano_contas c ON c.id = p.conta_id WHERE c.empresa_id = @e),
                       (SELECT min(data_inicio) FROM contabil.exercicio WHERE empresa_id = @e)
                """, con, tx).Com("e", empresaId))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
            {
                await r.ReadAsync(ct);
                if (r.GetBoolean(0)) throw new Domain.Comum.ViolacaoRegraException("o plano atual já tem lançamentos.");
                inicio = r.IsDBNull(1) ? DateOnly.FromDateTime(DateTime.Today) : r.GetFieldValue<DateOnly>(1);
            }
            await using (var cmd = new NpgsqlCommand("""
                DELETE FROM contabil.mapeamento_referencial WHERE conta_id IN (SELECT id FROM contabil.plano_contas WHERE empresa_id = @e);
                DELETE FROM contabil.saldo_mensal WHERE conta_id IN (SELECT id FROM contabil.plano_contas WHERE empresa_id = @e);
                DELETE FROM contabil.plano_contas WHERE empresa_id = @e;
                """, con, tx).Com("e", empresaId))
                await cmd.ExecuteNonQueryAsync(ct);
            await RepositorioEmpresa.InserirPlanoAsync(con, tx, empresaId, plano, inicio, ct);
            await tx.CommitAsync(ct);
            return 0;
        });

    public Task RecodificarAsync(long empresaId, IReadOnlyDictionary<long, string> codigos, long usuarioId, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            await using var con = await ds.OpenConnectionAsync(ct);
            await using var tx = await con.BeginTransactionAsync(ct);
            await Mapeamentos.DefinirUsuarioAsync(con, tx, usuarioId, ct);
            var ids = codigos.Keys.ToArray();
            // Em duas etapas para não colidir com códigos que ainda serão trocados (1.1.1 ↔ 1.1.01).
            await using (var cmd = new NpgsqlCommand(
                "UPDATE contabil.plano_contas SET codigo = '#' || id WHERE empresa_id = @e AND id = ANY(@ids)", con, tx)
                .Com("e", empresaId).Com("ids", ids))
                await cmd.ExecuteNonQueryAsync(ct);
            await using (var cmd = new NpgsqlCommand("""
                UPDATE contabil.plano_contas c SET codigo = n.codigo
                  FROM unnest(@ids, @cods) AS n(id, codigo) WHERE c.id = n.id AND c.empresa_id = @e
                """, con, tx).Com("e", empresaId).Com("ids", ids).Com("cods", ids.Select(i => codigos[i]).ToArray()))
                await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return 0;
        });

    public async Task<IReadOnlyList<CentroCustoDetalhe>> ListarCentrosAsync(long empresaId, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT c.id, c.codigo, c.descricao, c.centro_pai_id, c.ativo,
                   EXISTS (SELECT 1 FROM contabil.partida p WHERE p.centro_custo_id = c.id)
              FROM contabil.centro_custo c WHERE c.empresa_id = @e ORDER BY c.codigo
            """).Com("e", empresaId);
        var res = new List<CentroCustoDetalhe>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            res.Add(new CentroCustoDetalhe(r.GetInt64(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetInt64(3),
                                           r.GetBoolean(4), r.GetBoolean(5)));
        return res;
    }

    public Task<long> IncluirCentroAsync(long empresaId, string codigo, string descricao, long? paiId, DateOnly dataInclusao, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            await using var cmd = ds.CreateCommand("""
                INSERT INTO contabil.centro_custo (empresa_id, codigo, descricao, centro_pai_id, data_inclusao)
                VALUES (@e, @c, @d, @p, @i) RETURNING id
                """).Com("e", empresaId).Com("c", codigo).Com("d", descricao).Com("p", NpgsqlTypes.NpgsqlDbType.Bigint, paiId).Com("i", dataInclusao);
            return (long)(await cmd.ExecuteScalarAsync(ct))!;
        });

    public Task AlterarCentroAsync(long centroId, string descricao, bool ativo, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            await using var cmd = ds.CreateCommand("UPDATE contabil.centro_custo SET descricao = @d, ativo = @a WHERE id = @id")
                                    .Com("d", descricao).Com("a", ativo).Com("id", centroId);
            return await cmd.ExecuteNonQueryAsync(ct);
        });

    public Task ExcluirCentroAsync(long centroId, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            await using var cmd = ds.CreateCommand("DELETE FROM contabil.centro_custo WHERE id = @id").Com("id", centroId);
            return await cmd.ExecuteNonQueryAsync(ct);
        });
}
