using Npgsql;
using NpgsqlTypes;
using Vorticis.Application.Sped.Importacao;
using Vorticis.Domain.Sped.Importacao;
using Vorticis.Infra.Sped;

namespace Vorticis.Infra.Postgres;

/// <summary>
/// Grava a importação da ECF numa só transação: o plano de contas (J050, se faltar) passa pela mesma rotina da ECD; depois entram o e-Lalur/e-Lacs
/// (fiscal.lalur_lancamento), a Parte B (fiscal.lalur_parte_b) e as apurações do IRPJ/CSLL (fiscal.apuracao_lucro). As tabelas fiscais são acessadas
/// direto no banco (o Infra não referencia o módulo Fiscal), como nas consultas da geração da ECF.
/// </summary>
public sealed class RepositorioImportacaoEcf(NpgsqlDataSource ds, RepositorioImportacaoEcd contabil) : IRepositorioImportacaoEcf
{
    public async Task<SituacaoEcf?> SituacaoAsync(long empresaId, CancellationToken ct)
    {
        var sit = await contabil.SituacaoAsync(empresaId, ct);
        if (sit is null) return null;
        await using var con = await ds.OpenConnectionAsync(ct);
        string? regime = null;
        await using (var cmd = new NpgsqlCommand("SELECT regime::text FROM core.empresa_regime WHERE empresa_id = @e ORDER BY vigencia_inicio DESC LIMIT 1", con).Com("e", empresaId))
            regime = await cmd.ExecuteScalarAsync(ct) as string;
        var ap = new HashSet<(string, int, int)>();
        await using (var cmd = new NpgsqlCommand("SELECT regime, ano, trimestre FROM fiscal.apuracao_lucro WHERE empresa_id = @e", con).Com("e", empresaId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) ap.Add((r.GetString(0), r.GetInt16(1), r.GetInt16(2)));
        var lal = new HashSet<(int, int, string, bool, string, decimal)>();
        await using (var cmd = new NpgsqlCommand("SELECT ano, trimestre, tributo, tipo = 'ADICAO', COALESCE(codigo_ecf, ''), valor FROM fiscal.lalur_lancamento WHERE empresa_id = @e", con).Com("e", empresaId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) lal.Add((r.GetInt16(0), r.GetInt16(1), r.GetString(2), r.GetBoolean(3), r.GetString(4).Trim(), r.GetDecimal(5)));
        var saldos = new HashSet<string>();
        await using (var cmd = new NpgsqlCommand("SELECT DISTINCT tributo FROM fiscal.lalur_parte_b WHERE empresa_id = @e AND tipo = 'SALDO_INICIAL'", con).Com("e", empresaId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) saldos.Add(r.GetString(0));
        return new SituacaoEcf(sit, regime, ap, lal, saldos);
    }

    public Task<(long Id, string Nome)?> EmpresaPorCnpjAsync(string cnpj, CancellationToken ct) => contabil.EmpresaPorCnpjAsync(cnpj, ct);

    public Task<ResultadoGravacao> ImportarAsync(long empresaId, PlanoImportacaoEcf plano, MetaImportacao meta, long usuarioId, IProgress<ProgressoImportacao>? progresso, CancellationToken ct)
    {
        // sem plano a importar, a rotina da ECD só abre o histórico e fecha: o plano vazio não faz nada
        var planoContabil = plano.Plano ?? new PlanoImportacaoEcd { Ecd = new EcdLida { Cabecalho = meta.Cabecalho } };
        return contabil.ImportarNucleoAsync(empresaId, planoContabil, meta, "ECF", plano.Linhas, plano.Ocorrencias, false, usuarioId, progresso,
            async (con, tx, id, idsConta, soma, oc, ct2) => await GravarFiscalAsync(con, tx, empresaId, id, plano, idsConta, soma, usuarioId, ct2), ct);
    }

    private static async Task GravarFiscalAsync(NpgsqlConnection con, NpgsqlTransaction tx, long empresaId, long importacaoId, PlanoImportacaoEcf plano,
                                                IReadOnlyDictionary<string, long> idsConta, Action<string, int> soma, long usuarioId, CancellationToken ct)
    {
        var apIds = new Dictionary<(int Ano, int Tri), long>();
        var novos = new List<long>();
        foreach (var a in plano.Apuracoes.Where(a => !a.Existente))
        {
            await using var ins = new NpgsqlCommand("""
                INSERT INTO fiscal.apuracao_lucro (empresa_id, regime, ano, trimestre, periodo_inicio, periodo_fim, receita_bruta, lucro_contabil, adicoes_irpj, exclusoes_irpj, adicoes_csll,
                    exclusoes_csll, compensacao_irpj, compensacao_csll, prejuizo_irpj, prejuizo_csll, base_irpj, base_csll, irpj_normal, irpj_adicional, irpj_deducoes, irpj_a_pagar,
                    csll_devida, csll_deducoes, csll_a_pagar, vencimento, resultado, criado_por)
                VALUES (@e, @reg, @ano, @tri, @ini, @fim, @rb, @lc, @ai, @ei, @ac, @ec, @ci, @cc, @pi, @pc, @bi, @bc, @in, @ia, @id, @ip, @cd, @cded, @cp, @v, @res::jsonb, @u)
                ON CONFLICT DO NOTHING RETURNING id
                """, con, tx).Com("e", empresaId).Com("reg", a.Regime).Com("ano", (short)a.Ano).Com("tri", (short)a.Trimestre).Com("ini", a.Inicio).Com("fim", a.Fim)
                             .Com("rb", a.ReceitaBruta).Com("lc", NpgsqlDbType.Numeric, a.LucroContabil).Com("ai", a.AdicoesIrpj).Com("ei", a.ExclusoesIrpj).Com("ac", a.AdicoesCsll)
                             .Com("ec", a.ExclusoesCsll).Com("ci", a.CompensacaoIrpj).Com("cc", a.CompensacaoCsll).Com("pi", a.PrejuizoIrpj).Com("pc", a.PrejuizoCsll).Com("bi", a.BaseIrpj)
                             .Com("bc", a.BaseCsll).Com("in", a.IrpjNormal).Com("ia", a.IrpjAdicional).Com("id", a.IrpjDeducoes).Com("ip", a.IrpjAPagar).Com("cd", a.CsllDevida)
                             .Com("cded", a.CsllDeducoes).Com("cp", a.CsllAPagar).Com("v", a.Vencimento).Com("res", a.Memoria).Com("u", usuarioId);
            if (await ins.ExecuteScalarAsync(ct) is long id) { apIds[(a.Ano, a.Trimestre)] = id; novos.Add(id); }
        }
        await HistoricoImportacaoSql.ItensAsync(con, tx, importacaoId, "fiscal.apuracao_lucro", novos, ct);
        soma(plano.Ecf.Real ? "N630" : "P300", novos.Count);

        var lalIds = new List<long>();
        foreach (var l in plano.Lalur.Where(l => !l.Existente))
        {
            long? contaId = l.CodigoContaFinal is not null && idsConta.TryGetValue(l.CodigoContaFinal, out var cid) ? cid : null;
            await using var ins = new NpgsqlCommand("""
                INSERT INTO fiscal.lalur_lancamento (empresa_id, ano, trimestre, tributo, tipo, descricao, valor, codigo_ecf, conta_id, criado_por)
                VALUES (@e, @ano, @tri, @t, @tipo, @d, @v, @c, @conta, @u) RETURNING id
                """, con, tx).Com("e", empresaId).Com("ano", (short)l.Ano).Com("tri", (short)l.Trimestre).Com("t", l.Tributo).Com("tipo", l.Adicao ? "ADICAO" : "EXCLUSAO")
                             .Com("d", l.Descricao).Com("v", l.Valor).ComTexto("c", l.Codigo.Length > 20 ? l.Codigo[..20] : l.Codigo).Com("conta", NpgsqlDbType.Bigint, contaId).Com("u", usuarioId);
            lalIds.Add((long)(await ins.ExecuteScalarAsync(ct))!);
        }
        await HistoricoImportacaoSql.ItensAsync(con, tx, importacaoId, "fiscal.lalur_lancamento", lalIds, ct);
        soma("M300", lalIds.Count);

        var pbIds = new List<long>();
        foreach (var m in plano.ParteB)
        {
            long? apId = m.Ano is int a && m.Trimestre is int t && apIds.TryGetValue((a, t), out var x) ? x : null;
            if (m.Tipo != "SALDO_INICIAL" && apId is null) continue;       // movimento de apuração que já existia: não duplica
            await using var ins = new NpgsqlCommand("""
                INSERT INTO fiscal.lalur_parte_b (empresa_id, tributo, tipo, ano, trimestre, valor, apuracao_id, descricao) VALUES (@e, @t, @tipo, @ano, @tri, @v, @ap, @d) RETURNING id
                """, con, tx).Com("e", empresaId).Com("t", m.Tributo).Com("tipo", m.Tipo).Com("ano", NpgsqlDbType.Smallint, (short?)m.Ano).Com("tri", NpgsqlDbType.Smallint, (short?)m.Trimestre)
                             .Com("v", m.Valor).Com("ap", NpgsqlDbType.Bigint, apId).Com("d", m.Descricao);
            pbIds.Add((long)(await ins.ExecuteScalarAsync(ct))!);
        }
        await HistoricoImportacaoSql.ItensAsync(con, tx, importacaoId, "fiscal.lalur_parte_b", pbIds, ct);
        soma("M010", pbIds.Count);
    }

    public Task<RelatorioImportacao> DesfazerAsync(long empresaId, long importacaoId, long usuarioId, CancellationToken ct)
        => contabil.DesfazerNucleoAsync(empresaId, importacaoId, "ECF", usuarioId, ct);
}
