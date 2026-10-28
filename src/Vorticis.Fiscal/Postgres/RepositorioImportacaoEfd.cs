using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Vorticis.Application.Sped.Importacao;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Sped.Importacao;
using Vorticis.Fiscal.Lucro;
using Vorticis.Fiscal.Sped.Importacao;
using Vorticis.Infra.Sped;

namespace Vorticis.Fiscal.Postgres;

/// <summary>Grava a importação da EFD (ICMS/IPI ou Contribuições) numa única transação e desfaz o que ela criou.</summary>
public sealed class RepositorioImportacaoEfd(NpgsqlDataSource ds) : IRepositorioImportacaoEfd
{
    private const string TDoc = "fiscal.documento", TProd = "fiscal.produto", TPart = "core.participante", TAjuste = "fiscal.efd_ajuste_icms", TApIcms = "fiscal.apuracao_icms_historico",
                         TApPis = "fiscal.apuracao_pis_cofins", TInv = "fiscal.inventario", TCons = "fiscal.sped_consolidado";
    private static readonly string[] Csosn = ["101", "102", "103", "201", "202", "203", "300", "400", "500", "900"];
    private static readonly string[] TiposItem = ["00", "01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "99"];

    // ------------------------------------------------------------------ situação
    public async Task<SituacaoFiscal?> SituacaoAsync(long empresaId, CancellationToken ct)
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        string cnpj, nome; string? ie; bool simples;
        await using (var cmd = new NpgsqlCommand("""
            SELECT e.cnpj, e.razao_social, e.inscricao_estadual,
                   COALESCE((SELECT r.regime::text IN ('SIMPLES_NACIONAL', 'MEI') FROM core.empresa_regime r WHERE r.empresa_id = e.id ORDER BY r.vigencia_inicio DESC LIMIT 1), FALSE)
              FROM core.empresa e WHERE e.id = @e
            """, con).Com("e", empresaId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            if (!await r.ReadAsync(ct)) return null;
            (cnpj, nome, ie, simples) = (r.GetString(0).Trim(), r.GetString(1), r.Texto(2), r.GetBoolean(3));
        }
        var porChave = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var porNumero = new Dictionary<string, decimal>(StringComparer.Ordinal);
        await using (var cmd = new NpgsqlCommand("""
            SELECT d.chave_acesso, d.tipo_operacao, d.modelo, d.serie, d.numero, p.cpf_cnpj, d.valor_total
              FROM fiscal.documento d LEFT JOIN core.participante p ON p.id = d.participante_id WHERE d.empresa_id = @e
            """, con).Com("e", empresaId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
            {
                var total = r.GetDecimal(6);
                if (r.Texto(0) is { Length: > 0 } ch) porChave[ch] = total;
                porNumero[SituacaoFiscal.ChaveNumero(r.GetString(1) == "E", r.GetString(2), r.GetString(3), r.GetString(4), r.Texto(5))] = total;
            }
        var vinc = new Dictionary<string, long>(StringComparer.Ordinal); var gtin = new Dictionary<string, long>(StringComparer.Ordinal);
        await using (var cmd = new NpgsqlCommand("SELECT cod_produto_emitente, produto_id FROM fiscal.produto_vinculo WHERE empresa_id = @e AND emitente_documento = ''", con).Com("e", empresaId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) vinc[r.GetString(0)] = r.GetInt64(1);
        await using (var cmd = new NpgsqlCommand("SELECT gtin, id FROM fiscal.produto WHERE empresa_id = @e AND gtin IS NOT NULL", con).Com("e", empresaId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) gtin[r.GetString(0)] = r.GetInt64(1);
        var pdoc = new Dictionary<string, long>(StringComparer.Ordinal); var pnome = new Dictionary<string, long>(StringComparer.Ordinal);
        await using (var cmd = new NpgsqlCommand("SELECT id, cpf_cnpj, upper(nome) FROM core.participante WHERE empresa_id = @e", con).Com("e", empresaId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
                if (r.Texto(1) is { Length: > 0 } d) pdoc.TryAdd(d, r.GetInt64(0)); else pnome.TryAdd(r.GetString(2).Trim(), r.GetInt64(0));
        var fechados = new HashSet<DateOnly>();
        await using (var cmd = new NpgsqlCommand("SELECT competencia FROM fiscal.periodo_fechado WHERE empresa_id = @e", con).Com("e", empresaId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) fechados.Add(r.GetFieldValue<DateOnly>(0));
        var apI = await Datas(con, "SELECT competencia FROM fiscal.apuracao_icms_historico WHERE empresa_id = @e", empresaId, ct);
        var apP = await Datas(con, "SELECT competencia FROM fiscal.apuracao_pis_cofins WHERE empresa_id = @e", empresaId, ct);
        var invs = new HashSet<(DateOnly, string)>();
        await using (var cmd = new NpgsqlCommand("SELECT data, motivo FROM fiscal.inventario WHERE empresa_id = @e", con).Com("e", empresaId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) invs.Add((r.GetFieldValue<DateOnly>(0), r.GetString(1).Trim()));
        var ajs = new HashSet<(DateOnly, string, decimal)>();
        await using (var cmd = new NpgsqlCommand("SELECT competencia, codigo, valor FROM fiscal.efd_ajuste_icms WHERE empresa_id = @e", con).Com("e", empresaId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) ajs.Add((r.GetFieldValue<DateOnly>(0), r.GetString(1).Trim(), r.GetDecimal(2)));
        return new SituacaoFiscal(cnpj, nome, ie, simples, porChave, porNumero, vinc, gtin, pdoc, pnome, fechados, apI, apP, invs, ajs);
    }

    private static async Task<HashSet<DateOnly>> Datas(NpgsqlConnection con, string sql, long empresaId, CancellationToken ct)
    {
        var res = new HashSet<DateOnly>();
        await using var cmd = new NpgsqlCommand(sql, con).Com("e", empresaId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) res.Add(r.GetFieldValue<DateOnly>(0));
        return res;
    }

    public async Task<(long Id, string Nome)?> EmpresaPorCnpjAsync(string cnpj, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("SELECT id, razao_social FROM core.empresa WHERE cnpj = @c").Com("c", cnpj);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? (r.GetInt64(0), r.GetString(1)) : null;
    }

    // ------------------------------------------------------------------ importação
    internal static (string? Cst, string Origem) DividirCst(string? cst, bool simples)
    {
        var c = new string((cst ?? "").Where(char.IsAsciiDigit).ToArray());
        if (c.Length == 3)
        {
            if (simples && Csosn.Contains(c)) return (c, "0");
            return c[0] is >= '0' and <= '8' ? (c[1..], c[..1]) : (c, "0");
        }
        return (c.Length == 0 ? null : c, "0");
    }

    private static string Cortar(string? s, int max) => s is null ? "" : s.Length > max ? s[..max] : s;
    private static string Digitos(string? s) => new((s ?? "").Where(char.IsAsciiDigit).ToArray());

    public async Task<ResultadoGravacao> ImportarAsync(long empresaId, PlanoImportacaoEfd plano, MetaImportacao meta, bool ignorarComErro, long usuarioId,
                                                       IProgress<ProgressoImportacao>? progresso, CancellationToken ct)
    {
        var importados = new Dictionary<string, int>();
        void Soma(string reg, int n = 1) { if (n > 0) importados[reg] = importados.GetValueOrDefault(reg) + n; }
        var oc = new List<OcorrenciaImportacao>();
        var efd = plano.Efd;
        var tipoHist = efd.Contribuicoes ? "EFD_CONTRIB" : "EFD_ICMS";
        await using var con = await ds.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        await Sql.DefinirUsuarioAsync(con, tx, usuarioId, ct);
        await using (var c = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext('vorticis_importacao_sped'), @e::int)", con, tx).Com("e", empresaId)) await c.ExecuteNonQueryAsync(ct);
        var id = await HistoricoImportacaoSql.AbrirAsync(con, tx, empresaId, tipoHist, meta, usuarioId, ct);
        string empCnpj, empNome, empUf; bool simples;
        await using (var c = new NpgsqlCommand("""
            SELECT e.cnpj, e.razao_social, e.uf, COALESCE((SELECT r.regime::text IN ('SIMPLES_NACIONAL', 'MEI') FROM core.empresa_regime r WHERE r.empresa_id = e.id ORDER BY r.vigencia_inicio DESC LIMIT 1), FALSE)
              FROM core.empresa e WHERE e.id = @e
            """, con, tx).Com("e", empresaId))
        await using (var r = await c.ExecuteReaderAsync(ct))
        {
            await r.ReadAsync(ct);
            (empCnpj, empNome, empUf, simples) = (r.GetString(0).Trim(), r.GetString(1), r.GetString(2).Trim(), r.GetBoolean(3));
        }

        // ---- participantes
        progresso?.Report(new ProgressoImportacao("Importando participantes", 0, 100));
        var partId = new Dictionary<string, long>(StringComparer.Ordinal);
        var partNovos = new List<long>();
        foreach (var p in plano.Participantes)
        {
            ct.ThrowIfCancellationRequested();
            if (p.ExistenteId is { } ex) { partId[p.Origem.Codigo] = ex; continue; }
            var o = p.Origem;
            var (pid, _, criado) = await HistoricoImportacaoSql.GarantirParticipanteAsync(con, tx, empresaId, o.Nome, o.Documento is { Length: 11 or 14 } ? o.Documento : null, null,
                                                                                         o.CodMunicipio, o.Ie, p.Cliente, p.Fornecedor, ct);
            partId[o.Codigo] = pid;
            if (criado) partNovos.Add(pid);
        }
        await HistoricoImportacaoSql.ItensAsync(con, tx, id, TPart, partNovos, ct);
        Soma("0150", partNovos.Count);

        // ---- produtos
        var prodId = new Dictionary<string, long>(StringComparer.Ordinal);
        var prodLido = new Dictionary<string, ProdutoLidoEfd>(StringComparer.Ordinal);
        var prodNovos = new List<long>();
        var codigosUsados = new HashSet<int>();
        await using (var c = new NpgsqlCommand("SELECT codigo FROM fiscal.produto WHERE empresa_id = @e", con, tx).Com("e", empresaId))
        await using (var r = await c.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) codigosUsados.Add(r.GetInt32(0));
        var proximo = 1;
        var gtinsUsados = new HashSet<string>(StringComparer.Ordinal);
        await using (var c = new NpgsqlCommand("SELECT gtin FROM fiscal.produto WHERE empresa_id = @e AND gtin IS NOT NULL", con, tx).Com("e", empresaId))
        await using (var r = await c.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) gtinsUsados.Add(r.GetString(0));
        foreach (var pp in plano.Produtos)
        {
            ct.ThrowIfCancellationRequested();
            var o = pp.Origem;
            prodLido.TryAdd(o.Codigo, o);
            if (prodId.ContainsKey(o.Codigo)) continue;
            if (pp.ExistenteId is { } ex)
            {
                prodId[o.Codigo] = ex;
                await Vincular(con, tx, empresaId, ex, o.Codigo, ct);
                continue;
            }
            while (codigosUsados.Contains(proximo)) proximo++;
            codigosUsados.Add(proximo);
            var gtin = Digitos(o.Gtin);
            if (gtin.Length is 0 or > 14 || !gtinsUsados.Add(gtin)) gtin = "";
            var ncm = Digitos(o.Ncm);
            await using var ins = new NpgsqlCommand("""
                INSERT INTO fiscal.produto (empresa_id, codigo, descricao, unidade, ncm, cest, gtin, tipo_item, aliq_icms)
                VALUES (@e, @c, @d, @u, @ncm, @cest, @g, @t, @a) RETURNING id
                """, con, tx).Com("e", empresaId).Com("c", proximo).Com("d", Cortar(o.Descricao.Length == 0 ? o.Codigo : o.Descricao, 200))
                             .Com("u", Cortar(o.Unidade.Length == 0 ? "UN" : o.Unidade, 10)).ComTexto("ncm", ncm.Length == 8 ? ncm : null)
                             .ComTexto("cest", Digitos(o.Cest) is { Length: 7 } cs ? cs : null).ComTexto("g", gtin)
                             .Com("t", TiposItem.Contains(o.TipoItem) ? o.TipoItem : "99").Com("a", Math.Min(o.AliqIcms, 99999m));
            var pid = (long)(await ins.ExecuteScalarAsync(ct))!;
            prodId[o.Codigo] = pid;
            prodNovos.Add(pid);
            await Vincular(con, tx, empresaId, pid, o.Codigo, ct);
        }
        await HistoricoImportacaoSql.ItensAsync(con, tx, id, TProd, prodNovos, ct);
        Soma("0200", prodNovos.Count);

        // ---- notas
        var docIds = new List<long>();
        var partPorCodigo = efd.Participantes.GroupBy(p => p.Codigo).ToDictionary(g => g.Key, g => g.First());
        var aImportar = plano.Documentos.Where(d => d.Importavel).ToList();
        var feitos = 0;
        foreach (var dp in aImportar)
        {
            ct.ThrowIfCancellationRequested();
            if (++feitos % 25 == 0 || feitos == aImportar.Count) progresso?.Report(new ProgressoImportacao("Importando notas fiscais", feitos, aImportar.Count));
            var d = dp.Doc;
            await using (var sp = new NpgsqlCommand("SAVEPOINT nota", con, tx)) await sp.ExecuteNonQueryAsync(ct);
            try
            {
                var did = await GravarNotaAsync(con, tx, empresaId, d, efd.Cabecalho.Inicio, partId, partPorCodigo, prodId, prodLido, empCnpj, empNome, empUf, simples, usuarioId, ct);
                if (did is null) continue;      // já existia (corrida): ignora
                docIds.Add(did.Value);
                Soma(d.Registro); Soma(d.Registro switch { "A100" => "A170", "D100" => "D190", _ => "C170" }, d.Itens.Count);
            }
            catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.RaiseException or PostgresErrorCodes.CheckViolation or PostgresErrorCodes.UniqueViolation
                                                 or PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.StringDataRightTruncation or PostgresErrorCodes.NumericValueOutOfRange)
            {
                await using var rb = new NpgsqlCommand("ROLLBACK TO SAVEPOINT nota", con, tx);
                await rb.ExecuteNonQueryAsync(ct);
                var motivo = $"{d.Identificacao}: {ex.MessageText}";
                if (!ignorarComErro) throw new ViolacaoRegraException(motivo, ex);
                oc.Add(OcorrenciaImportacao.ErroPontual(d.Linha, d.Registro, motivo));
            }
        }
        await HistoricoImportacaoSql.ItensAsync(con, tx, id, TDoc, docIds, ct);

        // ---- consolidados (C180/C190 da EFD-Contribuições)
        var consIds = new List<long>();
        foreach (var c in plano.Consolidados)
        {
            await using var ins = new NpgsqlCommand("""
                INSERT INTO fiscal.sped_consolidado (empresa_id, competencia, registro, modelo, cfop, ncm, cst_pis, cst_cofins, valor, bc_pis, aliq_pis, vl_pis, bc_cofins, aliq_cofins, vl_cofins)
                VALUES (@e, @comp, @reg, @mod, @cfop, @ncm, @cp, @cc, @v, @bp, @ap, @p, @bc, @ac, @c) RETURNING id
                """, con, tx).Com("e", empresaId).Com("comp", c.Competencia).Com("reg", c.Registro).ComTexto("mod", Cortar(c.Modelo, 2)).ComTexto("cfop", Cortar(c.Cfop, 4))
                             .ComTexto("ncm", Cortar(Digitos(c.Ncm), 8)).ComTexto("cp", Cortar(c.CstPis, 2)).ComTexto("cc", Cortar(c.CstCofins, 2)).Com("v", c.Valor)
                             .Com("bp", c.BcPis).Com("ap", c.AliqPis).Com("p", c.Pis).Com("bc", c.BcCofins).Com("ac", c.AliqCofins).Com("c", c.Cofins);
            consIds.Add((long)(await ins.ExecuteScalarAsync(ct))!);
        }
        await HistoricoImportacaoSql.ItensAsync(con, tx, id, TCons, consIds, ct);
        Soma(efd.Consolidados.FirstOrDefault()?.Registro ?? "C180", consIds.Count);

        // ---- apuração e ajustes do ICMS
        var apIds = new List<long>();
        foreach (var a in plano.ApuracoesIcms)
        {
            await using var ins = new NpgsqlCommand("""
                INSERT INTO fiscal.apuracao_icms_historico (empresa_id, competencia, vl_tot_debitos, vl_aj_debitos, vl_tot_aj_debitos, vl_estornos_cred, vl_tot_creditos, vl_aj_creditos,
                    vl_tot_aj_creditos, vl_estornos_deb, vl_sld_credor_ant, vl_sld_apurado, vl_tot_ded, vl_icms_recolher, vl_sld_credor_transp, deb_esp)
                VALUES (@e, @c, @a1, @a2, @a3, @a4, @a5, @a6, @a7, @a8, @a9, @a10, @a11, @a12, @a13, @a14) ON CONFLICT DO NOTHING RETURNING id
                """, con, tx).Com("e", empresaId).Com("c", a.Competencia).Com("a1", a.TotDebitos).Com("a2", a.AjDebitos).Com("a3", a.TotAjDebitos).Com("a4", a.EstornosCred)
                             .Com("a5", a.TotCreditos).Com("a6", a.AjCreditos).Com("a7", a.TotAjCreditos).Com("a8", a.EstornosDeb).Com("a9", a.SldCredorAnt).Com("a10", a.SldApurado)
                             .Com("a11", a.TotDed).Com("a12", a.IcmsRecolher).Com("a13", a.SldCredorTransportar).Com("a14", a.DebEsp);
            if (await ins.ExecuteScalarAsync(ct) is long aid) apIds.Add(aid);
        }
        await HistoricoImportacaoSql.ItensAsync(con, tx, id, TApIcms, apIds, ct);
        Soma("E110", apIds.Count);
        var ajIds = new List<long>();
        var qtdPorReg = new Dictionary<string, int>();
        foreach (var a in plano.Ajustes)
        {
            await using var ins = new NpgsqlCommand("""
                INSERT INTO fiscal.efd_ajuste_icms (empresa_id, competencia, codigo, descricao, valor, imposto, sentido, observacao)
                VALUES (@e, @c, @cod, @d, @v, @imp, @sen, @obs) RETURNING id
                """, con, tx).Com("e", empresaId).Com("c", a.Origem.Competencia).Com("cod", a.Origem.Codigo).Com("d", Cortar(a.Origem.Descricao, 500)).Com("v", a.Origem.Valor)
                             .Com("imp", a.Imposto).Com("sen", NpgsqlDbType.Char, a.Sentido.ToString()).ComTexto("obs", Cortar(a.Origem.Observacao, 255));
            ajIds.Add((long)(await ins.ExecuteScalarAsync(ct))!);
            qtdPorReg[a.Origem.Registro] = qtdPorReg.GetValueOrDefault(a.Origem.Registro) + 1;
        }
        await HistoricoImportacaoSql.ItensAsync(con, tx, id, TAjuste, ajIds, ct);
        foreach (var (reg, n) in qtdPorReg) Soma(reg, n);

        // ---- inventário
        var invIds = new List<long>();
        var inicioComp = new DateOnly(efd.Cabecalho.Inicio.Year, efd.Cabecalho.Inicio.Month, 1);
        foreach (var inv in plano.Inventarios)
        {
            await using var ins = new NpgsqlCommand("""
                INSERT INTO fiscal.inventario (empresa_id, data, motivo, competencia_efd, conta_contabil) VALUES (@e, @d, @m, @c, @cta)
                ON CONFLICT DO NOTHING RETURNING id
                """, con, tx).Com("e", empresaId).Com("d", inv.Data).Com("m", inv.Motivo.Length == 2 ? inv.Motivo : "01").Com("c", inicioComp).Com("cta", Cortar(inv.Conta, 60));
            if (await ins.ExecuteScalarAsync(ct) is not long iid) continue;
            invIds.Add(iid);
            var itens = 0;
            foreach (var it in inv.Itens.Where(i => i.Quantidade > 0 && prodId.ContainsKey(i.CodItem)).GroupBy(i => i.CodItem).Select(g => g.First()))
            {
                await using var insI = new NpgsqlCommand("""
                    INSERT INTO fiscal.inventario_item (inventario_id, produto_id, unidade, quantidade, valor_unitario, valor) VALUES (@i, @p, @u, @q, @vu, @v)
                    """, con, tx).Com("i", iid).Com("p", prodId[it.CodItem]).Com("u", Cortar(it.Unidade.Length == 0 ? "UN" : it.Unidade, 6)).Com("q", it.Quantidade)
                                 .Com("vu", it.ValorUnitario).Com("v", it.Valor);
                await insI.ExecuteNonQueryAsync(ct);
                itens++;
            }
            Soma("H010", itens);
        }
        await HistoricoImportacaoSql.ItensAsync(con, tx, id, TInv, invIds, ct);

        // ---- apuração de PIS/COFINS
        var pcIds = new List<long>();
        foreach (var a in plano.ApuracoesContrib)
        {
            await using var ins = new NpgsqlCommand("""
                INSERT INTO fiscal.apuracao_pis_cofins (empresa_id, competencia, regime, receita_base, pis_debito, cofins_debito, base_credito, pis_credito, cofins_credito,
                    pis_retido, cofins_retido, pis_a_pagar, cofins_a_pagar, vencimento, resultado, criado_por)
                VALUES (@e, @c, @reg, @rb, @pd, @cd, @bc, @pc, @cc, @pr, @cr, @pp, @cp, @v, @res::jsonb, @u) ON CONFLICT DO NOTHING RETURNING id
                """, con, tx).Com("e", empresaId).Com("c", a.Competencia).Com("reg", a.Cumulativo ? "CUMULATIVO" : "NAO_CUMULATIVO").Com("rb", a.ReceitaBase).Com("pd", a.PisDebito)
                             .Com("cd", a.CofinsDebito).Com("bc", a.BaseCredito).Com("pc", a.PisCredito).Com("cc", a.CofinsCredito).Com("pr", a.PisRetido).Com("cr", a.CofinsRetido)
                             .Com("pp", a.PisAPagar).Com("cp", a.CofinsAPagar).Com("v", CalculadoraLucro.VencimentoPisCofins(a.Competencia))
                             .Com("res", JsonSerializer.Serialize(new { origem = "SPED", arquivo = meta.Arquivo })).Com("u", usuarioId);
            if (await ins.ExecuteScalarAsync(ct) is long pid) pcIds.Add(pid);
        }
        await HistoricoImportacaoSql.ItensAsync(con, tx, id, TApPis, pcIds, ct);
        Soma("M200", pcIds.Count);

        progresso?.Report(new ProgressoImportacao("Gravando", 100, 100));
        var g = new ResultadoGravacao(id, importados, oc);
        var rel = AuxiliarImportacao.Relatorio(id, meta.Arquivo, efd.Cabecalho.Tipo, meta.Modo, plano.Linhas, g, plano.Ocorrencias);
        await HistoricoImportacaoSql.FecharAsync(con, tx, id, partNovos.Count + prodNovos.Count + docIds.Count + apIds.Count + ajIds.Count + invIds.Count + pcIds.Count + consIds.Count, rel, null, ct);
        await tx.CommitAsync(ct);
        return g;
    }

    private static async Task Vincular(NpgsqlConnection con, NpgsqlTransaction tx, long empresaId, long produtoId, string codigo, CancellationToken ct)
    {
        await using var v = new NpgsqlCommand("""
            INSERT INTO fiscal.produto_vinculo (empresa_id, produto_id, emitente_documento, cod_produto_emitente) VALUES (@e, @p, '', @c) ON CONFLICT DO NOTHING
            """, con, tx).Com("e", empresaId).Com("p", produtoId).Com("c", Cortar(codigo, 60));
        await v.ExecuteNonQueryAsync(ct);
    }

    private static async Task<long?> GravarNotaAsync(NpgsqlConnection con, NpgsqlTransaction tx, long empresaId, DocumentoLidoEfd d, DateOnly inicioPeriodo, Dictionary<string, long> partId,
        Dictionary<string, ParticipanteLidoEfd> partLido, Dictionary<string, long> prodId, Dictionary<string, ProdutoLidoEfd> prodLido,
        string empCnpj, string empNome, string empUf, bool simples, long usuarioId, CancellationToken ct)
    {
        var part = d.CodPart is not null && partLido.TryGetValue(d.CodPart, out var pl) ? pl : null;
        long? partIdDoc = d.CodPart is not null && partId.TryGetValue(d.CodPart, out var pid) ? pid : null;
        var docPart = part?.Documento; var nomePart = part?.Nome;
        var ativo = d.Situacao == "AUTORIZADA";
        var itens = d.Itens.Select((i, n) => (Item: i, N: n + 1)).ToList();
        decimal Soma(Func<ItemLidoEfd, decimal> f) => itens.Sum(x => f(x.Item));
        var nfse = d.Modelo == "NFSE";
        var temItens = itens.Count > 0;
        var bcIcms = !ativo ? 0 : temItens && !nfse ? Soma(i => i.BcIcms) : d.BcIcms;
        var emitenteDoc = d.Entrada ? docPart : empCnpj; var emitenteNome = d.Entrada ? nomePart : empNome;
        var destDoc = d.Entrada ? empCnpj : docPart; var destNome = d.Entrada ? empNome : nomePart;
        var data = d.DataEfetiva(inicioPeriodo)!.Value;
        long documentoId;
        await using (var ins = new NpgsqlCommand("""
            INSERT INTO fiscal.documento (empresa_id, modelo, chave_acesso, tipo_operacao, numero, serie, data_emissao, data_entrada_saida, situacao,
                emitente_documento, emitente_nome, emitente_uf, destinatario_documento, destinatario_nome, destinatario_uf, participante_id,
                valor_produtos, valor_desconto, valor_frete, valor_seguro, valor_outras, valor_total, vl_bc_icms, vl_icms, vl_bc_icms_st, vl_icms_st, vl_ipi, vl_pis, vl_cofins, vl_iss,
                vl_ret_pis, vl_ret_cofins, origem, importado_por)
            VALUES (@e, @mod, @chave, @op, @num, @serie, @emi, @es, @sit, @ed, @en, @eu, @dd, @dn, @du, @part, @vprod, @vdesc, @vfrete, @vseg, @voutras, @vtotal,
                @bc, @icms, @bcst, @st, @ipi, @pis, @cofins, @iss, @rpis, @rcofins, 'SPED', @usu)
            ON CONFLICT DO NOTHING RETURNING id
            """, con, tx)
            .Com("e", empresaId).Com("mod", d.Modelo).ComTexto("chave", d.Chave).Com("op", d.Entrada ? "E" : "S").Com("num", Cortar(d.Numero, 20)).Com("serie", Cortar(d.Serie, 10))
            .Com("emi", d.Emissao ?? data).Com("es", data).Com("sit", d.Situacao)
            .ComTexto("ed", Cortar(emitenteDoc, 14)).ComTexto("en", Cortar(emitenteNome, 150)).ComTexto("eu", d.Entrada ? null : empUf)
            .ComTexto("dd", Cortar(destDoc, 14)).ComTexto("dn", Cortar(destNome, 150)).ComTexto("du", d.Entrada ? empUf : null)
            .Com("part", NpgsqlDbType.Bigint, partIdDoc)
            .Com("vprod", ativo ? d.Mercadorias : 0).Com("vdesc", ativo ? d.Desconto : 0).Com("vfrete", ativo ? d.Frete : 0).Com("vseg", ativo ? d.Seguro : 0).Com("voutras", ativo ? d.Outras : 0)
            .Com("vtotal", ativo ? d.Total : 0).Com("bc", bcIcms).Com("icms", ativo ? (temItens && !nfse ? Soma(i => i.Icms) : d.Icms) : 0)
            .Com("bcst", ativo ? (temItens && !nfse ? Soma(i => i.BcSt) : d.BcSt) : 0).Com("st", ativo ? (temItens && !nfse ? Soma(i => i.IcmsSt) : d.IcmsSt) : 0)
            .Com("ipi", ativo ? (temItens && !nfse ? Soma(i => i.Ipi) : d.Ipi) : 0).Com("pis", ativo ? d.Pis : 0).Com("cofins", ativo ? d.Cofins : 0).Com("iss", ativo ? d.Iss : 0)
            .Com("rpis", ativo ? d.RetPis : 0).Com("rcofins", ativo ? d.RetCofins : 0).Com("usu", usuarioId))
        {
            if (await ins.ExecuteScalarAsync(ct) is not long novo) return null;
            documentoId = novo;
        }
        if (d.Modelo == "57")
        {
            await using var cte = new NpgsqlCommand("INSERT INTO fiscal.documento_transporte (documento_id, valor_receber) VALUES (@d, @v)", con, tx).Com("d", documentoId).Com("v", ativo ? d.ValorServico : 0);
            await cte.ExecuteNonQueryAsync(ct);
        }
        if (!ativo) return documentoId;

        // itens: C170/A170; sem eles, uma linha por registro analítico (C190/D190) para guardar CFOP, CST e valores
        var linhas = new List<(string? Cod, string Desc, string? Ncm, string? Cest, string Cfop, string Unid, decimal Qtd, decimal Vitem, decimal Desc2, decimal Contabil,
                               string? Cst, string Orig, decimal Bc, decimal Aliq, decimal Icms, decimal BcSt, decimal St, string? CstIpi, decimal BcIpi, decimal AliqIpi, decimal Ipi,
                               string? CstPis, decimal BcPis, decimal AliqPis, decimal Pis, string? CstCof, decimal BcCof, decimal AliqCof, decimal Cof, decimal Iss, long? Produto, string? Gtin)>();
        var issRestante = d.Iss;
        var totalItens = itens.Sum(x => x.Item.ValorItem);
        for (var k = 0; k < itens.Count; k++)
        {
            var i = itens[k].Item;
            prodLido.TryGetValue(i.CodItem, out var pr);
            var (cst, orig) = nfse ? (null, "0") : DividirCst(i.CstIcms, simples);
            decimal iss = 0;
            if (nfse) { iss = k == itens.Count - 1 ? issRestante : totalItens > 0 ? Math.Round(d.Iss * i.ValorItem / totalItens, 2) : 0; issRestante -= iss; }
            var ncm = Digitos(pr?.Ncm);
            linhas.Add((i.CodItem.Length == 0 ? null : Cortar(i.CodItem, 60), Cortar(pr?.Descricao ?? i.Descricao ?? i.CodItem, 500), ncm.Length == 8 ? ncm : null,
                        Digitos(pr?.Cest) is { Length: 7 } ce ? ce : null, nfse ? "NFSE" : Cortar(Digitos(i.Cfop), 10), Cortar(i.Unidade.Length == 0 ? "UN" : i.Unidade, 10), i.Quantidade, i.ValorItem, i.Desconto,
                        i.ValorItem - i.Desconto + i.Ipi + i.IcmsSt, cst, orig, i.BcIcms, i.AliqIcms, i.Icms, i.BcSt, i.IcmsSt, i.CstIpi, i.BcIpi, i.AliqIpi, i.Ipi,
                        i.CstPis, i.BcPis, i.AliqPis, i.Pis, i.CstCofins, i.BcCofins, i.AliqCofins, i.Cofins, iss,
                        prodId.TryGetValue(i.CodItem, out var prodItem) ? prodItem : null, Digitos(pr?.Gtin) is { Length: > 0 and <= 14 } gt ? gt : null));
        }
        if (itens.Count == 0)
            foreach (var a in d.Analitico)
            {
                var (cst, orig) = DividirCst(a.Cst, simples);
                linhas.Add((null, "Operação consolidada (registro analítico do SPED)", null, null, Cortar(Digitos(a.Cfop), 10), "UN", 1, a.Operacao, 0, a.Operacao, cst, orig, a.BcIcms, a.Aliquota, a.Icms,
                            a.BcSt, a.IcmsSt, null, 0, 0, a.Ipi, null, 0, 0, 0, null, 0, 0, 0, 0, null, null));
            }
        var nItem = 0;
        foreach (var l in linhas)
        {
            nItem++;
            await using var insI = new NpgsqlCommand("""
                INSERT INTO fiscal.documento_item (documento_id, n_item, cod_produto, descricao, ncm, cest, cfop, unidade, quantidade, valor_unitario, valor_produto, valor_desconto, valor_contabil,
                    cst_icms, origem_mercadoria, vl_bc_icms, aliq_icms, vl_icms, vl_bc_icms_st, vl_icms_st, cst_ipi, vl_bc_ipi, aliq_ipi, vl_ipi, cst_pis, vl_bc_pis, aliq_pis, vl_pis,
                    cst_cofins, vl_bc_cofins, aliq_cofins, vl_cofins, vl_iss, produto_id, gtin)
                VALUES (@d, @n, @cod, @desc, @ncm, @cest, @cfop, @un, @q, @vu, @vp, @vd, @vc, @cst, @orig, @bc, @al, @ic, @bcs, @st, @cipi, @bipi, @aipi, @ipi, @cpis, @bpis, @apis, @pis,
                    @ccof, @bcof, @acof, @cof, @iss, @prod, @gtin)
                """, con, tx).Com("d", documentoId).Com("n", nItem).ComTexto("cod", l.Cod).Com("desc", l.Desc.Length == 0 ? "Item" : l.Desc).ComTexto("ncm", l.Ncm).ComTexto("cest", l.Cest)
                             .Com("cfop", l.Cfop.Length == 0 ? "0000" : l.Cfop).Com("un", l.Unid).Com("q", Math.Round(l.Qtd, 4)).Com("vu", l.Qtd > 0 ? Math.Round(l.Vitem / l.Qtd, 10) : l.Vitem)
                             .Com("vp", l.Vitem).Com("vd", l.Desc2).Com("vc", l.Contabil).ComTexto("cst", Cortar(l.Cst, 4)).Com("orig", NpgsqlDbType.Char, l.Orig).Com("bc", l.Bc).Com("al", l.Aliq)
                             .Com("ic", l.Icms).Com("bcs", l.BcSt).Com("st", l.St).ComTexto("cipi", Cortar(l.CstIpi, 2)).Com("bipi", l.BcIpi).Com("aipi", l.AliqIpi).Com("ipi", l.Ipi)
                             .ComTexto("cpis", Cortar(l.CstPis, 2)).Com("bpis", l.BcPis).Com("apis", l.AliqPis).Com("pis", l.Pis).ComTexto("ccof", Cortar(l.CstCof, 2)).Com("bcof", l.BcCof)
                             .Com("acof", l.AliqCof).Com("cof", l.Cof).Com("iss", l.Iss).Com("prod", NpgsqlDbType.Bigint, l.Produto).ComTexto("gtin", l.Gtin);
            await insI.ExecuteNonQueryAsync(ct);
        }
        return documentoId;
    }

    // ------------------------------------------------------------------ desfazer
    public async Task<RelatorioImportacao> DesfazerAsync(long empresaId, long importacaoId, string tipo, long usuarioId, CancellationToken ct)
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        await Sql.DefinirUsuarioAsync(con, tx, usuarioId, ct);
        var reg = await HistoricoImportacaoSql.CarregarAsync(con, tx, empresaId, importacaoId, ct, travar: true) ?? throw new ViolacaoRegraException("Importação não encontrada nesta empresa.");
        if (reg.Tipo != tipo) throw new ViolacaoRegraException("Esta importação não é deste tipo de arquivo.");
        if (reg.Desfeita) throw new ViolacaoRegraException("Esta importação já foi desfeita.");
        var motivos = new List<string>();
        var docs = await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TDoc, ct);

        if (docs.Count > 0)
        {
            await using (var q = new NpgsqlCommand("""
                SELECT count(*) FILTER (WHERE d.lancamento_id IS NOT NULL), string_agg(DISTINCT to_char(d.data_entrada_saida, 'MM/YYYY'), ', ') FILTER (WHERE fiscal.fn_mes_fechado(d.empresa_id, d.data_entrada_saida))
                  FROM fiscal.documento d WHERE d.id = ANY(@ids)
                """, con, tx))
            {
                q.Parameters.AddWithValue("ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint, docs.ToArray());
                await using var r = await q.ExecuteReaderAsync(ct);
                await r.ReadAsync(ct);
                if (r.GetInt64(0) > 0) motivos.Add($"{r.GetInt64(0)} nota(s) importada(s) já foram contabilizadas; desfaça a contabilização antes.");
                if (!r.IsDBNull(1)) motivos.Add($"O período fiscal de {r.GetString(1)} está fechado; reabra o mês antes de desfazer a importação.");
            }
            var usos = new List<(string Tabela, string Coluna)>();
            await using (var q = new NpgsqlCommand("""
                SELECT n.nspname || '.' || c.relname, a.attname
                  FROM pg_constraint k JOIN pg_class c ON c.oid = k.conrelid JOIN pg_namespace n ON n.oid = c.relnamespace
                  JOIN pg_attribute a ON a.attrelid = k.conrelid AND a.attnum = k.conkey[1]
                 WHERE k.contype = 'f' AND k.confrelid = 'fiscal.documento'::regclass AND array_length(k.conkey, 1) = 1
                   AND NOT (n.nspname = 'fiscal' AND c.relname IN ('documento_item', 'documento_transporte'))
                """, con, tx))
            await using (var r = await q.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct)) usos.Add((r.GetString(0), r.GetString(1)));
            foreach (var (tabela, coluna) in usos)
            {
                await using var q = new NpgsqlCommand($"SELECT count(*) FROM {tabela} WHERE {coluna} = ANY(@ids)", con, tx);
                q.Parameters.AddWithValue("ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint, docs.ToArray());
                var n = (long)(await q.ExecuteScalarAsync(ct))!;
                if (n > 0) motivos.Add($"Notas importadas já foram usadas em {Rotulo(tabela)} ({n}).");
            }
        }
        var pcs = await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TApPis, ct);
        if (pcs.Count > 0)
        {
            await using var q = new NpgsqlCommand("SELECT count(*) FROM fiscal.apuracao_pis_cofins WHERE id = ANY(@ids) AND lancamento_id IS NOT NULL", con, tx);
            q.Parameters.AddWithValue("ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint, pcs.ToArray());
            if ((long)(await q.ExecuteScalarAsync(ct))! > 0) motivos.Add("A apuração de PIS/COFINS importada já foi contabilizada.");
        }
        if (motivos.Count > 0) throw new ViolacaoRegraException(string.Join('\n', motivos.Distinct()));

        var removidos = new Dictionary<string, int>();
        removidos["Notas fiscais"] = await Remover(con, tx, "DELETE FROM fiscal.documento WHERE id = ANY(@ids)", docs, ct);
        removidos["Apuração do ICMS"] = await Remover(con, tx, "DELETE FROM fiscal.apuracao_icms_historico WHERE id = ANY(@ids)", await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TApIcms, ct), ct);
        removidos["Ajustes da apuração"] = await Remover(con, tx, "DELETE FROM fiscal.efd_ajuste_icms WHERE id = ANY(@ids)", await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TAjuste, ct), ct);
        removidos["Inventários"] = await Remover(con, tx, "DELETE FROM fiscal.inventario WHERE id = ANY(@ids)", await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TInv, ct), ct);
        removidos["Apuração de PIS/COFINS"] = await Remover(con, tx, "DELETE FROM fiscal.apuracao_pis_cofins WHERE id = ANY(@ids)", pcs, ct);
        removidos["Operações consolidadas"] = await Remover(con, tx, "DELETE FROM fiscal.sped_consolidado WHERE id = ANY(@ids)", await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TCons, ct), ct);
        var avisos = new List<OcorrenciaImportacao>();
        foreach (var (tabela, nome, sql) in new[] { (TProd, "Produtos", "DELETE FROM fiscal.produto WHERE id = ANY(@ids)"), (TPart, "Participantes", "DELETE FROM core.participante WHERE id = ANY(@ids)") })
        {
            var ids = await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, tabela, ct);
            var ok = 0;
            foreach (var x in ids)
            {
                // produto e participante ainda usados por outras notas (ou por outros cadastros) ficam
                if (tabela == TProd)
                {
                    await using var uso = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM fiscal.documento_item WHERE produto_id = @p)", con, tx).Com("p", x);
                    if ((bool)(await uso.ExecuteScalarAsync(ct))!) continue;
                }
                else
                {
                    await using var uso = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM fiscal.documento WHERE participante_id = @p) OR EXISTS (SELECT 1 FROM contabil.partida WHERE participante_id = @p)", con, tx).Com("p", x);
                    if ((bool)(await uso.ExecuteScalarAsync(ct))!) continue;
                }
                await using (var sp = new NpgsqlCommand("SAVEPOINT rem", con, tx)) await sp.ExecuteNonQueryAsync(ct);
                try { ok += await Remover(con, tx, sql, [x], ct); }
                catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
                { await using var rb = new NpgsqlCommand("ROLLBACK TO SAVEPOINT rem", con, tx); await rb.ExecuteNonQueryAsync(ct); }
            }
            removidos[nome] = ok;
            if (ok < ids.Count) avisos.Add(OcorrenciaImportacao.Aviso(0, "", $"{ids.Count - ok} {nome.ToLowerInvariant()} criado(s) pela importação continuam em uso e foram mantidos."));
        }
        await HistoricoImportacaoSql.MarcarDesfeitaAsync(con, tx, importacaoId, usuarioId, ct);
        await tx.CommitAsync(ct);
        var linhas = removidos.Where(x => x.Value > 0).Select(x => new LinhaRelatorio("", x.Key, x.Value, 0, 0)).ToList();
        return new RelatorioImportacao(importacaoId, reg.Arquivo, reg.Tipo == "EFD_CONTRIB" ? "EFD-Contribuições" : "EFD ICMS/IPI", reg.Modo, linhas, avisos, Desfeita: true);
    }

    private static string Rotulo(string tabela) => tabela switch
    {
        "fiscal.retencao" => "retenções", "fiscal.duplicata" => "duplicatas", "imobilizado.bem_a_cadastrar" => "bens do imobilizado a cadastrar",
        "erp.pedido" => "pedidos", _ => tabela.Replace('_', ' '),
    };

    private static async Task<int> Remover(NpgsqlConnection con, NpgsqlTransaction tx, string sql, IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return 0;
        await using var c = new NpgsqlCommand(sql, con, tx);
        c.Parameters.AddWithValue("ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint, ids.ToArray());
        return await c.ExecuteNonQueryAsync(ct);
    }
}
