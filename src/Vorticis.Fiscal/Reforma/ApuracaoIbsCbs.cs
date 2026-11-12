using System.Text.Json;
using Npgsql;
using Vorticis.Application.Relatorios;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Comum;
using Vorticis.Fiscal.Postgres;

namespace Vorticis.Fiscal.Reforma;

/// <summary>
/// Alíquotas de referência de um ano da transição (%; null = ainda não definida em lei ou resolução do Senado) e as regras do ano:
/// PIS/COFINS ainda vigentes, IPI cobrado (fora da ZFM), Imposto Seletivo e se IBS/CBS já são contabilizados (tributo "por fora", com débito e crédito).
/// </summary>
public sealed record AliquotasReforma(int Ano, decimal? Cbs, decimal? IbsUf, decimal? IbsMun, string? Observacao,
                                      bool PisCofins = true, bool Ipi = true, bool ImpostoSeletivo = false, bool Contabiliza = false);

/// <summary>Código de situação tributária (CST) do IBS e da CBS; <see cref="Tributa"/>: há IBS/CBS a destacar.</summary>
public sealed record CstReforma(string Codigo, string Descricao, bool Tributa)
{
    public override string ToString() => $"{Codigo} – {Descricao}";
}

public static class ParametrosReforma
{
    public static IReadOnlyList<AliquotasReforma> Carregar(string json)
    {
        using var doc = JsonDocument.Parse(json);
        static decimal? D(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;
        static bool B(JsonElement e, string n, bool padrao) => e.TryGetProperty(n, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : padrao;
        return [.. doc.RootElement.GetProperty("anos").EnumerateArray().Select(a =>
        {
            var ano = a.GetProperty("ano").GetInt32();
            return new AliquotasReforma(ano, D(a, "cbs"), D(a, "ibs_uf"), D(a, "ibs_mun"), a.TryGetProperty("observacao", out var o) ? o.GetString() : null,
                B(a, "pis_cofins", ano < 2027), B(a, "ipi", ano < 2027), B(a, "imposto_seletivo", ano >= 2027), B(a, "contabiliza", ano >= 2027));
        })];
    }

    /// <summary>Tabela de CST do IBS/CBS do parâmetro (vazia se o parâmetro não a tiver).</summary>
    public static IReadOnlyList<CstReforma> CarregarCst(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("cst", out var c)
            ? [.. c.EnumerateArray().Select(x => new CstReforma(x.GetProperty("codigo").GetString()!, x.GetProperty("descricao").GetString()!,
                                                                 !x.TryGetProperty("tributa", out var t) || t.GetBoolean()))]
            : [];
    }
}

/// <summary>Valores do grupo IBSCBS somados num sentido (saídas = débitos; entradas = créditos, só dos itens que creditam).</summary>
public sealed record TotaisReforma(int Notas, decimal Base, decimal Cbs, decimal IbsUf, decimal IbsMun, decimal ImpostoSeletivo = 0)
{
    public decimal Ibs => IbsUf + IbsMun;
    public static TotaisReforma Zero { get; } = new(0, 0, 0, 0, 0);
}

/// <summary>Apuração encerrada do mês (fiscal.apuracao_reforma).</summary>
public sealed record ApuracaoReformaEncerrada(long Id, DateOnly Competencia, decimal RecolherCbs, decimal RecolherIbs, decimal SaldoCredorCbs, decimal SaldoCredorIbs,
                                              decimal ImpostoSeletivo, long? LancamentoId, DateTime EncerradaEm);

/// <summary>
/// Apuração do IBS e da CBS do mês: débitos das saídas − créditos das entradas (só itens que creditam) − saldo credor do mês anterior; o que sobra
/// de crédito passa para o mês seguinte. O Imposto Seletivo das saídas é somado à parte (não gera crédito).
/// </summary>
public sealed record ApuracaoReforma(DateOnly Competencia, TotaisReforma Debitos, TotaisReforma Creditos, int SaidasSemGrupo, int EntradasSemGrupo,
                                     AliquotasReforma? Referencia, decimal SaldoAnteriorCbs = 0, decimal SaldoAnteriorIbs = 0,
                                     ApuracaoReformaEncerrada? Encerrada = null, int ItensSemCredito = 0)
{
    private decimal CreditoCbs => Creditos.Cbs + SaldoAnteriorCbs;
    private decimal CreditoIbs => Creditos.Ibs + SaldoAnteriorIbs;
    public decimal CbsAPagar => Math.Max(0, Debitos.Cbs - CreditoCbs);
    public decimal IbsAPagar => Math.Max(0, Debitos.Ibs - CreditoIbs);
    public decimal CbsCredor => Math.Max(0, CreditoCbs - Debitos.Cbs);
    public decimal IbsCredor => Math.Max(0, CreditoIbs - Debitos.Ibs);
    /// <summary>Parte dos débitos quitada com créditos (lançamento de compensação: D a recolher / C a recuperar).</summary>
    public decimal CompensadoCbs => Math.Min(Debitos.Cbs, CreditoCbs);
    public decimal CompensadoIbs => Math.Min(Debitos.Ibs, CreditoIbs);
    public decimal ImpostoSeletivo => Debitos.ImpostoSeletivo;
    /// <summary>Ano de teste (2026): só informativo, sem recolhimento nem encerramento.</summary>
    public bool Informativa => Referencia is not { Contabiliza: true };

    /// <summary>CBS esperada nas saídas pela alíquota de referência do ano (null se o ano não tem alíquota definida).</summary>
    public decimal? CbsEsperada => Referencia?.Cbs is { } a ? Dinheiro.Arredondar(Debitos.Base * a / 100m) : null;

    public IReadOnlyList<string> Avisos()
    {
        var a = new List<string>();
        if (SaidasSemGrupo > 0) a.Add($"{SaidasSemGrupo} nota(s) de saída sem o grupo IBS/CBS: a partir de 2026 a NF-e deve destacar o IBS e a CBS por item.");
        if (EntradasSemGrupo > 0) a.Add($"{EntradasSemGrupo} nota(s) de entrada sem o grupo IBS/CBS: não geram crédito nesta apuração.");
        if (ItensSemCredito > 0) a.Add($"{ItensSemCredito} item(ns) de entrada marcado(s) para não creditar IBS/CBS (uso e consumo pessoal ou classificação sem crédito).");
        if (CbsEsperada is { } e && Math.Abs(e - Debitos.Cbs) > 0.05m * Math.Max(1, e))
            a.Add($"CBS destacada nas saídas ({Formato.Moeda(Debitos.Cbs)}) diferente da esperada pela alíquota de referência de {Referencia!.Cbs:0.##}% ({Formato.Moeda(e)}): confira a classificação tributária dos itens (reduções, isenções, imunidades).");
        if (Referencia is null) a.Add($"Sem alíquotas de referência cadastradas para {Competencia.Year} nos parâmetros legais.");
        else if (Referencia.Observacao is { Length: > 0 } o) a.Add(o);
        if (Encerrada is { } enc) a.Add($"Apuração encerrada em {enc.EncerradaEm:dd/MM/yyyy HH:mm}{(enc.LancamentoId is not null ? ", com o lançamento de compensação na contabilidade" : "")}.");
        return a;
    }

    public TabelaRelatorio Tabela()
    {
        LinhaRelatorio L(string item, decimal cbs, decimal ibsUf, decimal ibsMun, bool d = false) => new([item, cbs, ibsUf, ibsMun, ibsUf + ibsMun], d);
        var linhas = new List<LinhaRelatorio>
        {
            L($"Débitos das saídas ({Debitos.Notas} notas, base {Formato.Moeda(Debitos.Base)})", Debitos.Cbs, Debitos.IbsUf, Debitos.IbsMun),
            L($"(−) Créditos das entradas ({Creditos.Notas} notas, base {Formato.Moeda(Creditos.Base)})", Creditos.Cbs, Creditos.IbsUf, Creditos.IbsMun),
            new(["(−) Saldo credor do mês anterior", SaldoAnteriorCbs, null, null, SaldoAnteriorIbs]),
            new(["Saldo a recolher", CbsAPagar, null, null, IbsAPagar], true),
            new(["Saldo credor para o mês seguinte", CbsCredor, null, null, IbsCredor]),
        };
        if (ImpostoSeletivo > 0) linhas.Add(new(["Imposto Seletivo das saídas (a recolher, sem crédito)", ImpostoSeletivo, null, null, null], true));
        return new TabelaRelatorio($"IBS e CBS · {Competencia:MM/yyyy}",
            [new("Apuração", TipoColuna.Texto, 8), new("CBS", TipoColuna.Valor, 3), new("IBS UF", TipoColuna.Valor, 3), new("IBS município", TipoColuna.Valor, 3),
             new("IBS total", TipoColuna.Valor, 3)], linhas, string.Join(" ", Avisos()));
    }
}

/// <summary>
/// Apuração › IBS e CBS (reforma tributária): soma o IBS, a CBS e o Imposto Seletivo destacados nos itens das notas autorizadas do mês (importadas
/// de XML com o grupo IBSCBS ou digitadas). Em 2026 a apuração é informativa; de 2027 em diante pode ser encerrada: o saldo credor passa ao mês
/// seguinte e a compensação dos créditos com os débitos é lançada na contabilidade. FISCAL_APURAR (encerrar) ou FISCAL_RELATORIOS (consultar).
/// </summary>
public sealed class ServicoReformaTributaria(NpgsqlDataSource ds, IParametrosFiscais parametros, Autorizador autorizador)
{
    public const string Chave = "REFORMA_IBS_CBS";

    /// <summary>Lança a compensação (empresa, data, CBS, IBS, usuário): ligado ao serviço de provisões, que tem as contas dos tributos.</summary>
    public Func<long, DateOnly, decimal, decimal, long, CancellationToken, Task<Resultado<long>>>? ContabilizarCompensacao { get; set; }

    public async Task<IReadOnlyList<AliquotasReforma>> AliquotasAsync(int ano, CancellationToken ct = default) =>
        ParametrosReforma.Carregar(await parametros.ObterJsonAsync(Chave, new DateOnly(Math.Max(ano, 2026), 1, 1), ct));

    /// <summary>Alíquotas e regras do ano (null se o ano não está nos parâmetros).</summary>
    public async Task<AliquotasReforma?> AliquotasDoAnoAsync(int ano, CancellationToken ct = default) =>
        (await AliquotasAsync(ano, ct)).FirstOrDefault(a => a.Ano == ano) ?? (ano > 2033 ? (await AliquotasAsync(ano, ct)).LastOrDefault() : null);

    public async Task<Resultado<ApuracaoReforma>> ApurarAsync(long empresa, DateOnly competencia, long usuario, CancellationToken ct = default)
    {
        var p = await autorizador.ObterAsync(usuario, empresa, ct);
        if (!p.Pode(Permissao.FiscalApurar) && !p.Pode(Permissao.FiscalRelatorios))
            return await autorizador.NegarAsync<ApuracaoReforma>(usuario, empresa, Permissao.FiscalApurar, ct) ?? Resultado<ApuracaoReforma>.Falha("Acesso negado.");
        return Resultado<ApuracaoReforma>.Ok(await CalcularAsync(empresa, new DateOnly(competencia.Year, competencia.Month, 1), ct));
    }

    private async Task<ApuracaoReforma> CalcularAsync(long empresa, DateOnly mes, CancellationToken ct)
    {
        var tot = new Dictionary<string, TotaisReforma>();
        var sem = new Dictionary<string, int>();
        var semCredito = 0;
        await using (var cmd = ds.CreateCommand("""
            SELECT d.tipo_operacao,
                   count(DISTINCT d.id) FILTER (WHERE i.tem_ibscbs)::int,
                   COALESCE(sum(i.vl_bc_ibscbs) FILTER (WHERE d.tipo_operacao = 'S' OR i.credita_ibscbs), 0),
                   COALESCE(sum(i.vl_cbs) FILTER (WHERE d.tipo_operacao = 'S' OR i.credita_ibscbs), 0),
                   COALESCE(sum(i.vl_ibs_uf) FILTER (WHERE d.tipo_operacao = 'S' OR i.credita_ibscbs), 0),
                   COALESCE(sum(i.vl_ibs_mun) FILTER (WHERE d.tipo_operacao = 'S' OR i.credita_ibscbs), 0),
                   (count(DISTINCT d.id) - count(DISTINCT d.id) FILTER (WHERE i.tem_ibscbs))::int,
                   COALESCE(sum(i.vl_is), 0),
                   count(*) FILTER (WHERE d.tipo_operacao = 'E' AND i.tem_ibscbs AND NOT i.credita_ibscbs)::int
              FROM fiscal.documento d JOIN fiscal.documento_item i ON i.documento_id = d.id
             WHERE d.empresa_id = @e AND d.situacao = 'AUTORIZADA' AND date_trunc('month', d.data_entrada_saida) = @m
             GROUP BY d.tipo_operacao
            """).Com("e", empresa).Com("m", mes))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
            {
                var saida = r.GetString(0) == "S";
                tot[r.GetString(0)] = new TotaisReforma(r.GetInt32(1), r.GetDecimal(2), r.GetDecimal(3), r.GetDecimal(4), r.GetDecimal(5), saida ? r.GetDecimal(7) : 0);
                sem[r.GetString(0)] = r.GetInt32(6);
                if (!saida) semCredito = r.GetInt32(8);
            }
        var (antCbs, antIbs) = (0m, 0m);
        await using (var cmd = ds.CreateCommand("""
            SELECT saldo_credor_cbs, saldo_credor_ibs FROM fiscal.apuracao_reforma WHERE empresa_id = @e AND competencia < @m ORDER BY competencia DESC LIMIT 1
            """).Com("e", empresa).Com("m", mes))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            if (await r.ReadAsync(ct)) (antCbs, antIbs) = (r.GetDecimal(0), r.GetDecimal(1));
        return new ApuracaoReforma(mes, tot.GetValueOrDefault("S", TotaisReforma.Zero), tot.GetValueOrDefault("E", TotaisReforma.Zero),
            sem.GetValueOrDefault("S"), sem.GetValueOrDefault("E"), await AliquotasDoAnoAsync(mes.Year, ct), antCbs, antIbs, await EncerradaAsync(empresa, mes, ct), semCredito);
    }

    private async Task<ApuracaoReformaEncerrada?> EncerradaAsync(long empresa, DateOnly mes, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT id, competencia, recolher_cbs, recolher_ibs, saldo_credor_cbs, saldo_credor_ibs, imposto_seletivo, lancamento_id, encerrada_em
              FROM fiscal.apuracao_reforma WHERE empresa_id = @e AND competencia = @m
            """).Com("e", empresa).Com("m", mes);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct)
            ? new ApuracaoReformaEncerrada(r.GetInt64(0), r.GetFieldValue<DateOnly>(1), r.GetDecimal(2), r.GetDecimal(3), r.GetDecimal(4), r.GetDecimal(5),
                                           r.GetDecimal(6), r.IsDBNull(7) ? null : r.GetInt64(7), r.GetDateTime(8))
            : null;
    }

    /// <summary>
    /// Encerra a apuração do mês (2027 em diante): grava os saldos (o credor passa ao mês seguinte) e, com <paramref name="contabilizar"/>, lança a
    /// compensação dos créditos com os débitos (D IBS/CBS a recolher, C IBS/CBS a recuperar). Recusa mês já encerrado ou depois de um mês encerrado.
    /// </summary>
    public async Task<Resultado<ApuracaoReforma>> EncerrarAsync(long empresa, DateOnly competencia, bool contabilizar, long usuario, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<ApuracaoReforma>(usuario, empresa, Permissao.FiscalApurar, ct) is { } negado) return negado;
        var mes = new DateOnly(competencia.Year, competencia.Month, 1);
        var a = await CalcularAsync(empresa, mes, ct);
        if (a.Informativa) return Resultado<ApuracaoReforma>.Falha($"Em {mes.Year} a apuração do IBS e da CBS é só informativa (ano de teste): não há encerramento.");
        if (a.Encerrada is not null) return Resultado<ApuracaoReforma>.Falha($"A apuração de {mes:MM/yyyy} já está encerrada. Reabra para encerrar de novo.");
        await using (var cmd = ds.CreateCommand("SELECT EXISTS (SELECT 1 FROM fiscal.apuracao_reforma WHERE empresa_id = @e AND competencia > @m)").Com("e", empresa).Com("m", mes))
            if (await cmd.ExecuteScalarAsync(ct) is true)
                return Resultado<ApuracaoReforma>.Falha("Há apuração encerrada de mês posterior: reabra-a antes (o saldo credor passa de um mês para o outro).");
        long? lancamento = null;
        if (contabilizar && (a.CompensadoCbs > 0 || a.CompensadoIbs > 0))
        {
            if (ContabilizarCompensacao is not { } lancar) return Resultado<ApuracaoReforma>.Falha("Contabilização indisponível.");
            var r = await lancar(empresa, mes.AddMonths(1).AddDays(-1), a.CompensadoCbs, a.CompensadoIbs, usuario, ct);
            if (!r.Sucesso) return Resultado<ApuracaoReforma>.Falha(r.Erros);
            lancamento = r.Valor;
        }
        await using (var cmd = ds.CreateCommand("""
            INSERT INTO fiscal.apuracao_reforma (empresa_id, competencia, debito_cbs, debito_ibs, credito_cbs, credito_ibs, saldo_anterior_cbs, saldo_anterior_ibs,
                                                 recolher_cbs, recolher_ibs, saldo_credor_cbs, saldo_credor_ibs, imposto_seletivo, lancamento_id)
            VALUES (@e, @m, @dc, @di, @cc, @ci, @ac, @ai, @rc, @ri, @sc, @si, @is, @l)
            """).Com("e", empresa).Com("m", mes).Com("dc", a.Debitos.Cbs).Com("di", a.Debitos.Ibs).Com("cc", a.Creditos.Cbs).Com("ci", a.Creditos.Ibs)
                .Com("ac", a.SaldoAnteriorCbs).Com("ai", a.SaldoAnteriorIbs).Com("rc", a.CbsAPagar).Com("ri", a.IbsAPagar).Com("sc", a.CbsCredor).Com("si", a.IbsCredor)
                .Com("is", a.ImpostoSeletivo).Com("l", NpgsqlTypes.NpgsqlDbType.Bigint, lancamento))
            await cmd.ExecuteNonQueryAsync(ct);
        return Resultado<ApuracaoReforma>.Ok(await CalcularAsync(empresa, mes, ct));
    }

    /// <summary>Reabre a apuração encerrada (a última): apaga os saldos gravados. O lançamento de compensação, se houver, deve ser estornado na contabilidade.</summary>
    public async Task<Resultado> ReabrirAsync(long empresa, DateOnly competencia, long usuario, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuario, empresa, Permissao.FiscalApurar, ct) is { } negado) return Resultado.Falha(negado);
        var mes = new DateOnly(competencia.Year, competencia.Month, 1);
        await using (var cmd = ds.CreateCommand("SELECT EXISTS (SELECT 1 FROM fiscal.apuracao_reforma WHERE empresa_id = @e AND competencia > @m)").Com("e", empresa).Com("m", mes))
            if (await cmd.ExecuteScalarAsync(ct) is true) return Resultado.Falha("Reabra antes a apuração do mês mais recente.");
        await using (var cmd = ds.CreateCommand("DELETE FROM fiscal.apuracao_reforma WHERE empresa_id = @e AND competencia = @m").Com("e", empresa).Com("m", mes))
            if (await cmd.ExecuteNonQueryAsync(ct) == 0) return Resultado.Falha($"A apuração de {mes:MM/yyyy} não está encerrada.");
        return Resultado.Ok();
    }
}
