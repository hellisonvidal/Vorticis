using Npgsql;
using NpgsqlTypes;
using Vorticis.Domain.Comum;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Regras;

namespace Vorticis.Fiscal.Postgres;

/// <summary>Regras de entrada da empresa e se o centro de custo está ativo no Fiscal (carregados uma vez por lote de importação).</summary>
internal sealed record ContextoRegras(IReadOnlyList<RegraFiscal> Regras, bool CentroCustoAtivo);

public sealed class RepositorioRegrasFiscais(NpgsqlDataSource ds) : IRepositorioRegrasFiscais
{
    private const string Colunas = """
        r.id, r.tipo_operacao, r.ordem, r.ativo, r.descricao, r.cfop_origem, r.participante_id, r.ncm, r.produto_id, r.cst_origem, r.destino,
        r.cfop_destino, r.cst_destino, r.credita_icms, r.credita_ipi, r.credita_pis, r.credita_cofins, r.conta_debito, r.conta_credito, r.centro_custo_id,
        p.nome, pr.descricao
        """;

    private static RegraFiscal Ler(NpgsqlDataReader r) => new(
        r.GetInt64(0), ModelosExtensions.OperacaoDe(r.GetString(1)), r.GetInt32(2), r.GetBoolean(3), r.Texto(4), r.Texto(5), r.LongOuNulo(6), r.Texto(7), r.LongOuNulo(8),
        r.Texto(9), r.Texto(10), r.Texto(11), r.Texto(12), r.GetBoolean(13), r.GetBoolean(14), r.GetBoolean(15), r.GetBoolean(16), r.Texto(17), r.Texto(18),
        r.LongOuNulo(19), r.Texto(20), r.Texto(21));

    private const string Juncoes = """
        FROM fiscal.regra_fiscal r
        LEFT JOIN core.participante p ON p.id = r.participante_id
        LEFT JOIN fiscal.produto pr ON pr.id = r.produto_id
        """;

    public async Task<IReadOnlyList<RegraFiscal>> ListarAsync(long empresaId, TipoOperacao? operacao, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand($"""
            SELECT {Colunas} {Juncoes}
             WHERE r.empresa_id = @e AND (@op::text IS NULL OR r.tipo_operacao = @op)
             ORDER BY r.tipo_operacao DESC, r.cfop_origem NULLS LAST, r.ordem, r.id
            """).Com("e", empresaId).Com("op", NpgsqlDbType.Text, operacao?.Codigo());
        var res = new List<RegraFiscal>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) res.Add(Ler(rd));
        return res;
    }

    /// <summary>Regras ativas da operação, lidas na transação em andamento.</summary>
    internal static async Task<ContextoRegras> CarregarContextoAsync(NpgsqlConnection con, NpgsqlTransaction tx, long empresaId, TipoOperacao operacao, CancellationToken ct)
    {
        var regras = new List<RegraFiscal>();
        await using (var cmd = new NpgsqlCommand($"SELECT {Colunas} {Juncoes} WHERE r.empresa_id = @e AND r.tipo_operacao = @op AND r.ativo", con, tx)
                         .Com("e", empresaId).Com("op", operacao.Codigo()))
        await using (var rd = await cmd.ExecuteReaderAsync(ct))
            while (await rd.ReadAsync(ct)) regras.Add(Ler(rd));
        bool centro;
        await using (var q = new NpgsqlCommand("""
            SELECT COALESCE((SELECT c.usa_centro_custo FROM fiscal.configuracao c WHERE c.empresa_id = e.id), FALSE) AND e.usa_centro_custo
              FROM core.empresa e WHERE e.id = @e
            """, con, tx).Com("e", empresaId))
            centro = await q.ExecuteScalarAsync(ct) is true;
        return new ContextoRegras(regras, centro);
    }

    public Task<long> GravarAsync(long empresaId, RegraFiscal g, long usuarioId, CancellationToken ct)
        => Sql.TraduzirErrosAsync(async () =>
        {
            await using var con = await ds.OpenConnectionAsync(ct);
            await using var tx = await con.BeginTransactionAsync(ct);
            await Sql.DefinirUsuarioAsync(con, tx, usuarioId, ct);
            // participante, produto e centro de custo precisam ser da empresa
            if (g.ParticipanteId is { } pid)
                await using (var q = new NpgsqlCommand("SELECT 1 FROM core.participante WHERE id = @i AND empresa_id = @e", con, tx).Com("i", pid).Com("e", empresaId))
                    if (await q.ExecuteScalarAsync(ct) is null) throw new ViolacaoRegraException("Participante não encontrado nesta empresa.");
            if (g.ProdutoId is { } prid)
                await using (var q = new NpgsqlCommand("SELECT 1 FROM fiscal.produto WHERE id = @i AND empresa_id = @e", con, tx).Com("i", prid).Com("e", empresaId))
                    if (await q.ExecuteScalarAsync(ct) is null) throw new ViolacaoRegraException("Produto não encontrado nesta empresa.");
            if (g.CentroCustoId is { } cid)
                await using (var q = new NpgsqlCommand("SELECT 1 FROM contabil.centro_custo WHERE id = @i AND empresa_id = @e AND ativo", con, tx).Com("i", cid).Com("e", empresaId))
                    if (await q.ExecuteScalarAsync(ct) is null) throw new ViolacaoRegraException("Centro de custo inexistente ou inativo.");
            await using var cmd = g.Id == 0
                ? new NpgsqlCommand("""
                    INSERT INTO fiscal.regra_fiscal (empresa_id, tipo_operacao, ordem, ativo, descricao, cfop_origem, participante_id, ncm, produto_id, cst_origem, destino,
                        cfop_destino, cst_destino, credita_icms, credita_ipi, credita_pis, credita_cofins, conta_debito, conta_credito, centro_custo_id)
                    VALUES (@e, @op, @ordem, @ativo, @desc, @co, @part, @ncm, @prod, @cso, @dest, @cd, @cstd, @ci, @cip, @cp, @cc, @cdeb, @ccred, @cent)
                    RETURNING id
                    """, con, tx)
                : new NpgsqlCommand("""
                    UPDATE fiscal.regra_fiscal SET tipo_operacao = @op, ordem = @ordem, ativo = @ativo, descricao = @desc, cfop_origem = @co, participante_id = @part,
                        ncm = @ncm, produto_id = @prod, cst_origem = @cso, destino = @dest, cfop_destino = @cd, cst_destino = @cstd, credita_icms = @ci,
                        credita_ipi = @cip, credita_pis = @cp, credita_cofins = @cc, conta_debito = @cdeb, conta_credito = @ccred, centro_custo_id = @cent
                     WHERE id = @id AND empresa_id = @e RETURNING id
                    """, con, tx).Com("id", g.Id);
            cmd.Com("e", empresaId).Com("op", g.Operacao.Codigo()).Com("ordem", g.Ordem).Com("ativo", g.Ativo).ComTexto("desc", g.Descricao)
               .ComTexto("co", g.CfopOrigem).Com("part", NpgsqlDbType.Bigint, g.ParticipanteId).ComTexto("ncm", g.Ncm).Com("prod", NpgsqlDbType.Bigint, g.ProdutoId)
               .ComTexto("cso", g.CstOrigem).ComTexto("dest", g.Destino).ComTexto("cd", g.CfopDestino).ComTexto("cstd", g.CstDestino)
               .Com("ci", g.CreditaIcms).Com("cip", g.CreditaIpi).Com("cp", g.CreditaPis).Com("cc", g.CreditaCofins)
               .ComTexto("cdeb", g.ContaDebito).ComTexto("ccred", g.ContaCredito).Com("cent", NpgsqlDbType.Bigint, g.CentroCustoId);
            var id = await cmd.ExecuteScalarAsync(ct) as long? ?? throw new ViolacaoRegraException("Regra não encontrada.");
            await tx.CommitAsync(ct);
            return id;
        });

    public async Task<bool> ExcluirAsync(long empresaId, long regraId, long usuarioId, CancellationToken ct)
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        await Sql.DefinirUsuarioAsync(con, tx, usuarioId, ct);
        await using var cmd = new NpgsqlCommand("DELETE FROM fiscal.regra_fiscal WHERE id = @i AND empresa_id = @e", con, tx).Com("i", regraId).Com("e", empresaId);
        var n = await cmd.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return n > 0;
    }

    public async Task<int> GerarPadraoAsync(long empresaId, IReadOnlyList<(string Origem, string Destino)> pares, long usuarioId, CancellationToken ct)
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        await Sql.DefinirUsuarioAsync(con, tx, usuarioId, ct);
        var criadas = 0;
        foreach (var (origem, destino) in pares)
        {
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO fiscal.regra_fiscal (empresa_id, tipo_operacao, ordem, descricao, cfop_origem, cfop_destino)
                SELECT @e, 'E', 1000, 'Padrão', @o, @d
                 WHERE NOT EXISTS (SELECT 1 FROM fiscal.regra_fiscal r WHERE r.empresa_id = @e AND r.tipo_operacao = 'E' AND r.cfop_origem = @o
                                      AND r.participante_id IS NULL AND r.ncm IS NULL AND r.produto_id IS NULL AND r.cst_origem IS NULL)
                """, con, tx).Com("e", empresaId).Com("o", origem).Com("d", destino);
            criadas += await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return criadas;
    }

    public async Task<string?> UfEmpresaAsync(long empresaId, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("SELECT uf FROM core.empresa WHERE id = @e").Com("e", empresaId);
        return (await cmd.ExecuteScalarAsync(ct))?.ToString()?.Trim();
    }

    public async Task<bool> ContaAnaliticaExisteAsync(long empresaId, string codigo, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("SELECT 1 FROM contabil.plano_contas WHERE empresa_id = @e AND codigo = @c AND tipo = 'A' AND ativa").Com("e", empresaId).Com("c", codigo);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    /// <summary>Grava no item a escrituração decidida (CFOP só se diferente do XML; CST só se diferente).</summary>
    internal static NpgsqlCommand ComandoAplicarItem(NpgsqlConnection con, NpgsqlTransaction tx, long documentoId, int nItem, string cfopXml, string? cstXml, DecisaoFiscal d, bool centroAtivo)
        => new NpgsqlCommand("""
            UPDATE fiscal.documento_item SET cfop_escrituracao = @ce, cst_icms_escrituracao = @cst, credita_icms = @ci, credita_ipi = @cip, credita_pis = @cp,
                   credita_cofins = @cc, sem_regra = @sr, regra_fiscal_id = @rid, centro_custo_id = COALESCE(@cent, centro_custo_id)
             WHERE documento_id = @d AND n_item = @n
            """, con, tx)
            .Com("d", documentoId).Com("n", nItem).ComTexto("ce", d.Cfop == cfopXml ? null : d.Cfop)
            .ComTexto("cst", d.CstIcms is null || d.CstIcms == cstXml ? null : d.CstIcms)
            .Com("ci", d.CreditaIcms).Com("cip", d.CreditaIpi).Com("cp", d.CreditaPis).Com("cc", d.CreditaCofins).Com("sr", d.SemRegra)
            .Com("rid", NpgsqlDbType.Bigint, d.RegraId).Com("cent", NpgsqlDbType.Bigint, centroAtivo ? d.CentroCustoId : null);

    internal static async Task AplicarContasDoDocumentoAsync(NpgsqlConnection con, NpgsqlTransaction tx, long documentoId, string? debito, string? credito, CancellationToken ct)
    {
        if (debito is null && credito is null) return;
        await using var cmd = new NpgsqlCommand("""
            UPDATE fiscal.documento SET conta_debito = COALESCE(conta_debito, @cd), conta_credito = COALESCE(conta_credito, @cr) WHERE id = @d
            """, con, tx).Com("d", documentoId).ComTexto("cd", debito).ComTexto("cr", credito);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ResultadoReaplicacao> ReaplicarAsync(long empresaId, DateOnly inicio, DateOnly fim, long usuarioId, CancellationToken ct)
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        await Sql.DefinirUsuarioAsync(con, tx, usuarioId, ct);
        var contexto = await CarregarContextoAsync(con, tx, empresaId, TipoOperacao.Entrada, ct);
        var documentos = new List<(long Id, long? ParticipanteId)>();
        await using (var q = new NpgsqlCommand("""
            SELECT d.id, d.participante_id
              FROM fiscal.documento d LEFT JOIN contabil.lancamento l ON l.id = d.lancamento_id
             WHERE d.empresa_id = @e AND d.tipo_operacao = 'E' AND d.origem = 'XML' AND d.modelo <> 'NFSE' AND d.data_entrada_saida BETWEEN @ini AND @fim
               AND NOT COALESCE(l.status = 'EFETIVADO', FALSE)
             ORDER BY d.id
            """, con, tx).Com("e", empresaId).Com("ini", inicio).Com("fim", fim))
        await using (var rd = await q.ExecuteReaderAsync(ct))
            while (await rd.ReadAsync(ct)) documentos.Add((rd.GetInt64(0), rd.LongOuNulo(1)));

        int notas = 0, comRegra = 0, semRegra = 0, puladas = 0;
        foreach (var (docId, participanteId) in documentos)
        {
            var itens = new List<(int N, string Cfop, string? Ncm, long? ProdutoId, string? Cst, bool Revisado)>();
            await using (var qi = new NpgsqlCommand("""
                SELECT n_item, cfop, ncm, produto_id, cst_icms, (NOT sem_regra AND regra_fiscal_id IS NULL AND cfop_escrituracao IS NOT NULL)
                  FROM fiscal.documento_item WHERE documento_id = @d ORDER BY n_item
                """, con, tx).Com("d", docId))
            await using (var rd = await qi.ExecuteReaderAsync(ct))
                while (await rd.ReadAsync(ct)) itens.Add((rd.GetInt32(0), rd.GetString(1), rd.Texto(2), rd.LongOuNulo(3), rd.Texto(4), rd.GetBoolean(5)));

            await using (var sp = new NpgsqlCommand("SAVEPOINT reaplicar", con, tx)) await sp.ExecuteNonQueryAsync(ct);
            try
            {
                int cr = 0, sr = 0;
                string? debito = null, credito = null;
                foreach (var i in itens)
                {
                    var d = MotorRegrasFiscais.DecidirEntrada(contexto.Regras, new ItemParaRegra(i.Cfop, participanteId, i.Ncm, i.ProdutoId, i.Cst));
                    if (d.RegraId is null && i.Revisado) continue;   // sem regra e já conferido à mão: não mexe
                    await using (var up = ComandoAplicarItem(con, tx, docId, i.N, i.Cfop, i.Cst, d, contexto.CentroCustoAtivo)) await up.ExecuteNonQueryAsync(ct);
                    debito ??= d.ContaDebito;
                    credito ??= d.ContaCredito;
                    if (d.RegraId is not null) cr++; else if (d.SemRegra) sr++;
                }
                await AplicarContasDoDocumentoAsync(con, tx, docId, debito, credito, ct);
                await using (var rel = new NpgsqlCommand("RELEASE SAVEPOINT reaplicar", con, tx)) await rel.ExecuteNonQueryAsync(ct);
                (notas, comRegra, semRegra) = (notas + 1, comRegra + cr, semRegra + sr);
            }
            catch (PostgresException)
            {
                // período fiscal fechado (ou outra regra do banco): a nota é pulada e as demais continuam
                await using (var rb = new NpgsqlCommand("ROLLBACK TO SAVEPOINT reaplicar", con, tx)) await rb.ExecuteNonQueryAsync(CancellationToken.None);
                puladas++;
            }
        }
        await tx.CommitAsync(ct);
        return new ResultadoReaplicacao(notas, comRegra, semRegra, puladas);
    }
}
