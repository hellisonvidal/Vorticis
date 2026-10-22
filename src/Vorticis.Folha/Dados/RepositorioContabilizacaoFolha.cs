using Npgsql;
using Vorticis.Folha.Regras;

namespace Vorticis.Folha.Dados;

public sealed class RepositorioContabilizacaoFolha(NpgsqlDataSource ds) : IRepositorioContabilizacaoFolha
{
    public async Task<IReadOnlyList<RegraContabil>> ListarRegrasAsync(long empresaId, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT id, chave, descricao, conta_debito, conta_credito, usa_centro_custo, lado_centro_custo FROM folha.regra_contabil WHERE empresa_id = @e ORDER BY chave
            """).Com("e", empresaId);
        var res = new List<RegraContabil>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) res.Add(new RegraContabil(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetBoolean(5), r.GetString(6)[0]));
        return res;
    }

    public Task GravarRegraAsync(long empresaId, RegraContabil x, CancellationToken ct) => Sql.TraduzirAsync(async () =>
    {
        await using var cmd = ds.CreateCommand("""
            INSERT INTO folha.regra_contabil (empresa_id, chave, descricao, conta_debito, conta_credito, usa_centro_custo, lado_centro_custo)
            VALUES (@e, @k, @d, @cd, @cc, @u, @l)
            ON CONFLICT (empresa_id, chave) DO UPDATE SET descricao = @d, conta_debito = @cd, conta_credito = @cc, usa_centro_custo = @u, lado_centro_custo = @l
            """).Com("e", empresaId).Com("k", x.Chave).Com("d", x.Descricao).Com("cd", x.ContaDebito).Com("cc", x.ContaCredito).Com("u", x.UsaCentroCusto)
            .Com("l", x.LadoCentroCusto.ToString());
        return await cmd.ExecuteNonQueryAsync(ct);
    });

    public Task ExcluirRegraAsync(long empresaId, long id, CancellationToken ct) => Sql.TraduzirAsync(async () =>
    {
        await using var cmd = ds.CreateCommand("DELETE FROM folha.regra_contabil WHERE id = @id AND empresa_id = @e").Com("id", id).Com("e", empresaId);
        return await cmd.ExecuteNonQueryAsync(ct);
    });

    public Task SubstituirProvisoesAsync(long empresaId, DateOnly competencia,
        IReadOnlyList<(long FuncionarioId, long? CentroCustoId, ProvisaoMensal P)> provisoes, CancellationToken ct) => Sql.TraduzirAsync(async () =>
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        await using (var del = new NpgsqlCommand("DELETE FROM folha.provisao WHERE empresa_id = @e AND competencia = @c", con, tx).Com("e", empresaId).ComData("c", competencia))
            await del.ExecuteNonQueryAsync(ct);
        foreach (var (func, cc, p) in provisoes)
        {
            await using var ins = new NpgsqlCommand("""
                INSERT INTO folha.provisao (empresa_id, funcionario_id, competencia, centro_custo_id, base, ferias, decimo, enc_ferias_inss, enc_ferias_fgts, enc_decimo_inss, enc_decimo_fgts)
                VALUES (@e, @f, @c, @cc, @b, @fe, @de, @efi, @eff, @edi, @edf)
                """, con, tx).Com("e", empresaId).Com("f", func).ComData("c", competencia).ComId("cc", cc).Com("b", p.Base).Com("fe", p.Ferias).Com("de", p.Decimo)
                .Com("efi", p.EncargosFerias.Previdenciarios).Com("eff", p.EncargosFerias.Fgts).Com("edi", p.EncargosDecimo.Previdenciarios).Com("edf", p.EncargosDecimo.Fgts);
            await ins.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return 0;
    });

    public async Task<IReadOnlyList<ProvisaoGravada>> ListarProvisoesAsync(long empresaId, DateOnly competencia, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT p.funcionario_id, f.matricula, f.nome, p.centro_custo_id, cc.codigo, p.base, p.ferias, p.decimo, p.enc_ferias_inss, p.enc_ferias_fgts,
                   p.enc_decimo_inss, p.enc_decimo_fgts
              FROM folha.provisao p JOIN folha.funcionario f ON f.id = p.funcionario_id LEFT JOIN contabil.centro_custo cc ON cc.id = p.centro_custo_id
             WHERE p.empresa_id = @e AND p.competencia = @c ORDER BY f.nome
            """).Com("e", empresaId).ComData("c", competencia);
        var res = new List<ProvisaoGravada>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            res.Add(new ProvisaoGravada(r.GetInt64(0), r.GetString(1), r.GetString(2), r.Id(3), r.Texto(4), r.GetDecimal(5), r.GetDecimal(6), r.GetDecimal(7),
                                        r.GetDecimal(8), r.GetDecimal(9), r.GetDecimal(10), r.GetDecimal(11)));
        return res;
    }

    private const string SelectContab = "SELECT id, competencia, tipo, lancamento_id, numero_lancamento, valor_total, criada_em, desfeita_em FROM folha.contabilizacao";

    private static ContabilizacaoRegistro Ler(NpgsqlDataReader r) => new(r.GetInt64(0), r.GetFieldValue<DateOnly>(1), r.GetString(2), r.GetInt64(3), r.Id(4), r.GetDecimal(5),
        r.GetFieldValue<DateTimeOffset>(6), r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7));

    public async Task<IReadOnlyList<ContabilizacaoRegistro>> ListarContabilizacoesAsync(long empresaId, DateOnly? competencia, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand(SelectContab + " WHERE empresa_id = @e AND (@c::date IS NULL OR competencia = @c) ORDER BY competencia DESC, id DESC")
                                .Com("e", empresaId).ComData("c", competencia);
        var res = new List<ContabilizacaoRegistro>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) res.Add(Ler(r));
        return res;
    }

    public async Task<ContabilizacaoRegistro?> ObterContabilizacaoAtivaAsync(long empresaId, DateOnly competencia, string tipo, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand(SelectContab + " WHERE empresa_id = @e AND competencia = @c AND tipo = @t AND desfeita_em IS NULL")
                                .Com("e", empresaId).ComData("c", competencia).Com("t", tipo);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Ler(r) : null;
    }

    public Task<long> RegistrarContabilizacaoAsync(long empresaId, DateOnly competencia, string tipo, long lancamentoId, long numero, decimal valor, long usuarioId,
                                                   CancellationToken ct) => Sql.TraduzirAsync(async () =>
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        await Sql.DefinirUsuarioAsync(con, tx, usuarioId, ct);
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO folha.contabilizacao (empresa_id, competencia, tipo, lancamento_id, numero_lancamento, valor_total, criada_por)
            VALUES (@e, @c, @t, @l, @n, @v, @u) RETURNING id
            """, con, tx).Com("e", empresaId).ComData("c", competencia).Com("t", tipo).Com("l", lancamentoId).Com("n", numero).Com("v", valor).ComId("u", usuarioId);
        var id = (long)(await cmd.ExecuteScalarAsync(ct))!;
        await tx.CommitAsync(ct);
        return id;
    });

    public Task MarcarDesfeitaAsync(long empresaId, long id, long? estornoId, long usuarioId, CancellationToken ct) => Sql.TraduzirAsync(async () =>
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        await Sql.DefinirUsuarioAsync(con, tx, usuarioId, ct);
        await using var cmd = new NpgsqlCommand("UPDATE folha.contabilizacao SET desfeita_em = now(), desfeita_por = @u, estorno_id = @s WHERE id = @id AND empresa_id = @e",
            con, tx).Com("id", id).Com("e", empresaId).ComId("u", usuarioId).ComId("s", estornoId);
        await cmd.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return 0;
    });
}

public sealed class RepositorioESocial(NpgsqlDataSource ds) : IRepositorioESocial
{
    public async Task<IReadOnlyList<EventoEsocial>> ListarAsync(long empresaId, DateOnly? competencia, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT id, tipo, competencia, referencia, id_evento, xml, status, gerado_em, exportado_em FROM folha.esocial_evento
             WHERE empresa_id = @e AND (@c::date IS NULL OR competencia = @c OR competencia IS NULL) ORDER BY tipo, referencia, id
            """).Com("e", empresaId).ComData("c", competencia);
        var res = new List<EventoEsocial>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            res.Add(new EventoEsocial(r.GetInt64(0), r.GetString(1), r.Data(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6),
                                      r.GetFieldValue<DateTimeOffset>(7), r.IsDBNull(8) ? null : r.GetFieldValue<DateTimeOffset>(8)));
        return res;
    }

    public Task<long> GravarAsync(long empresaId, string tipo, DateOnly? competencia, string referencia, string idEvento, string xml, long usuarioId,
                                  CancellationToken ct) => Sql.TraduzirAsync(async () =>
    {
        await using var cmd = ds.CreateCommand("""
            INSERT INTO folha.esocial_evento (empresa_id, tipo, competencia, referencia, id_evento, xml, gerado_por) VALUES (@e, @t, @c, @r, @i, @x, @u) RETURNING id
            """).Com("e", empresaId).Com("t", tipo).ComData("c", competencia).Com("r", referencia).Com("i", idEvento).Com("x", xml).ComId("u", usuarioId);
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    });

    public Task MarcarExportadosAsync(long empresaId, IReadOnlyList<long> ids, CancellationToken ct) => Sql.TraduzirAsync(async () =>
    {
        await using var cmd = ds.CreateCommand("UPDATE folha.esocial_evento SET status = 'EXPORTADO', exportado_em = now() WHERE empresa_id = @e AND id = ANY(@ids)")
                                .Com("e", empresaId).Com("ids", ids.ToArray());
        return await cmd.ExecuteNonQueryAsync(ct);
    });

    public async Task<int> SequencialDoDiaAsync(long empresaId, string prefixoId, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("SELECT COUNT(*) FROM folha.esocial_evento WHERE empresa_id = @e AND id_evento LIKE @p")
                                .Com("e", empresaId).Com("p", prefixoId + "%");
        return (int)(long)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public Task ExcluirNaoExportadosAsync(long empresaId, string tipo, DateOnly? competencia, string referencia, CancellationToken ct) => Sql.TraduzirAsync(async () =>
    {
        await using var cmd = ds.CreateCommand("""
            DELETE FROM folha.esocial_evento WHERE empresa_id = @e AND tipo = @t AND referencia = @r AND status = 'GERADO'
               AND competencia IS NOT DISTINCT FROM @c::date
            """).Com("e", empresaId).Com("t", tipo).Com("r", referencia).ComData("c", competencia);
        return await cmd.ExecuteNonQueryAsync(ct);
    });
}
