using System.Text.Json;
using Npgsql;
using Vorticis.Application.Relatorios;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Comum;
using Vorticis.Fiscal.Postgres;

namespace Vorticis.Fiscal.Reforma;

/// <summary>Alíquotas de referência de um ano da transição (%; null = ainda não definida em lei ou resolução do Senado).</summary>
public sealed record AliquotasReforma(int Ano, decimal? Cbs, decimal? IbsUf, decimal? IbsMun, string? Observacao);

public static class ParametrosReforma
{
    public static IReadOnlyList<AliquotasReforma> Carregar(string json)
    {
        using var doc = JsonDocument.Parse(json);
        static decimal? D(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;
        return [.. doc.RootElement.GetProperty("anos").EnumerateArray().Select(a => new AliquotasReforma(a.GetProperty("ano").GetInt32(), D(a, "cbs"), D(a, "ibs_uf"), D(a, "ibs_mun"),
            a.TryGetProperty("observacao", out var o) ? o.GetString() : null))];
    }
}

/// <summary>Valores do grupo IBSCBS somados num sentido (saídas = débitos; entradas = créditos).</summary>
public sealed record TotaisReforma(int Notas, decimal Base, decimal Cbs, decimal IbsUf, decimal IbsMun)
{
    public decimal Ibs => IbsUf + IbsMun;
    public static TotaisReforma Zero { get; } = new(0, 0, 0, 0, 0);
}

/// <summary>Apuração assistida de IBS e CBS do mês: débitos das saídas − créditos das entradas, com as notas sem o grupo IBSCBS.</summary>
public sealed record ApuracaoReforma(DateOnly Competencia, TotaisReforma Debitos, TotaisReforma Creditos, int SaidasSemGrupo, int EntradasSemGrupo,
                                     AliquotasReforma? Referencia)
{
    public decimal CbsAPagar => Math.Max(0, Debitos.Cbs - Creditos.Cbs);
    public decimal IbsAPagar => Math.Max(0, Debitos.Ibs - Creditos.Ibs);
    public decimal CbsCredor => Math.Max(0, Creditos.Cbs - Debitos.Cbs);
    public decimal IbsCredor => Math.Max(0, Creditos.Ibs - Debitos.Ibs);

    /// <summary>CBS esperada nas saídas pela alíquota de referência do ano (null se o ano não tem alíquota definida).</summary>
    public decimal? CbsEsperada => Referencia?.Cbs is { } a ? Dinheiro.Arredondar(Debitos.Base * a / 100m) : null;

    public IReadOnlyList<string> Avisos()
    {
        var a = new List<string>();
        if (SaidasSemGrupo > 0) a.Add($"{SaidasSemGrupo} nota(s) de saída sem o grupo IBS/CBS: a partir de 2026 a NF-e deve destacar o IBS e a CBS por item.");
        if (EntradasSemGrupo > 0) a.Add($"{EntradasSemGrupo} nota(s) de entrada sem o grupo IBS/CBS: não geram crédito nesta apuração.");
        if (CbsEsperada is { } e && Math.Abs(e - Debitos.Cbs) > 0.05m * Math.Max(1, e))
            a.Add($"CBS destacada nas saídas ({Formato.Moeda(Debitos.Cbs)}) diferente da esperada pela alíquota de referência de {Referencia!.Cbs:0.##}% ({Formato.Moeda(e)}): confira a classificação tributária dos itens.");
        if (Referencia is null) a.Add($"Sem alíquotas de referência cadastradas para {Competencia.Year} nos parâmetros legais.");
        else if (Referencia.Observacao is { Length: > 0 } o) a.Add(o);
        return a;
    }

    public TabelaRelatorio Tabela()
    {
        LinhaRelatorio L(string item, decimal cbs, decimal ibsUf, decimal ibsMun, bool d = false) => new([item, cbs, ibsUf, ibsMun, ibsUf + ibsMun], d);
        var linhas = new List<LinhaRelatorio>
        {
            L($"Débitos das saídas ({Debitos.Notas} notas, base {Formato.Moeda(Debitos.Base)})", Debitos.Cbs, Debitos.IbsUf, Debitos.IbsMun),
            L($"(−) Créditos das entradas ({Creditos.Notas} notas, base {Formato.Moeda(Creditos.Base)})", Creditos.Cbs, Creditos.IbsUf, Creditos.IbsMun),
            new(["Saldo a recolher", CbsAPagar, null, null, IbsAPagar], true),
            new(["Saldo credor para o mês seguinte", CbsCredor, null, null, IbsCredor]),
        };
        return new TabelaRelatorio($"IBS e CBS · {Competencia:MM/yyyy}",
            [new("Apuração", TipoColuna.Texto, 8), new("CBS", TipoColuna.Valor, 3), new("IBS UF", TipoColuna.Valor, 3), new("IBS município", TipoColuna.Valor, 3),
             new("IBS total", TipoColuna.Valor, 3)], linhas, string.Join(" ", Avisos()));
    }
}

/// <summary>
/// Apuração › IBS e CBS (reforma tributária): soma o IBS e a CBS destacados nos itens das notas autorizadas do mês (importadas de XML com o
/// grupo IBSCBS). Apuração assistida para a transição: em 2026 os valores são informativos; o recolhimento e a apuração oficial seguem as
/// regras do Comitê Gestor e da Receita Federal. FISCAL_APURAR ou FISCAL_RELATORIOS.
/// </summary>
public sealed class ServicoReformaTributaria(NpgsqlDataSource ds, IParametrosFiscais parametros, Autorizador autorizador)
{
    public const string Chave = "REFORMA_IBS_CBS";

    public async Task<IReadOnlyList<AliquotasReforma>> AliquotasAsync(int ano, CancellationToken ct = default) =>
        ParametrosReforma.Carregar(await parametros.ObterJsonAsync(Chave, new DateOnly(Math.Max(ano, 2026), 1, 1), ct));

    public async Task<Resultado<ApuracaoReforma>> ApurarAsync(long empresa, DateOnly competencia, long usuario, CancellationToken ct = default)
    {
        var p = await autorizador.ObterAsync(usuario, empresa, ct);
        if (!p.Pode(Permissao.FiscalApurar) && !p.Pode(Permissao.FiscalRelatorios))
            return await autorizador.NegarAsync<ApuracaoReforma>(usuario, empresa, Permissao.FiscalApurar, ct) ?? Resultado<ApuracaoReforma>.Falha("Acesso negado.");
        var mes = new DateOnly(competencia.Year, competencia.Month, 1);
        var tot = new Dictionary<string, TotaisReforma>();
        var sem = new Dictionary<string, int>();
        await using (var cmd = ds.CreateCommand("""
            SELECT d.tipo_operacao,
                   count(DISTINCT d.id) FILTER (WHERE i.tem_ibscbs)::int,
                   COALESCE(sum(i.vl_bc_ibscbs), 0), COALESCE(sum(i.vl_cbs), 0), COALESCE(sum(i.vl_ibs_uf), 0), COALESCE(sum(i.vl_ibs_mun), 0),
                   (count(DISTINCT d.id) - count(DISTINCT d.id) FILTER (WHERE i.tem_ibscbs))::int
              FROM fiscal.documento d JOIN fiscal.documento_item i ON i.documento_id = d.id
             WHERE d.empresa_id = @e AND d.situacao = 'AUTORIZADA' AND date_trunc('month', d.data_entrada_saida) = @m
             GROUP BY d.tipo_operacao
            """).Com("e", empresa).Com("m", mes))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
            {
                tot[r.GetString(0)] = new TotaisReforma(r.GetInt32(1), r.GetDecimal(2), r.GetDecimal(3), r.GetDecimal(4), r.GetDecimal(5));
                sem[r.GetString(0)] = r.GetInt32(6);
            }
        var anos = await AliquotasAsync(mes.Year, ct);
        return Resultado<ApuracaoReforma>.Ok(new ApuracaoReforma(mes, tot.GetValueOrDefault("S", TotaisReforma.Zero), tot.GetValueOrDefault("E", TotaisReforma.Zero),
            sem.GetValueOrDefault("S"), sem.GetValueOrDefault("E"), anos.FirstOrDefault(a => a.Ano == mes.Year)));
    }
}
