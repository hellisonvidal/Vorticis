using System.Globalization;
using System.Text.Json;
using Vorticis.Application.Relatorios;
using Vorticis.Domain.Comum;
using Vorticis.Fiscal.Comum;
using Vorticis.Fiscal.Simples;

namespace Vorticis.Fiscal.Reforma;

/// <summary>Um ano do cronograma (valores em %): alíquotas de referência e quanto de PIS/COFINS e de ICMS/ISS já foi extinto.</summary>
public sealed record AnoCronograma(int Ano, decimal Cbs, decimal Ibs, decimal PisCofinsExtinto, decimal IcmsIssExtinto, bool Teste, string? Observacao);

public sealed record CronogramaReforma(IReadOnlyList<AnoCronograma> Anos, decimal CargaDentroDoDas, string Fundamento)
{
    /// <summary>Lê REFORMA_SIMPLES (2027 em diante) e acrescenta 2026 (ano de teste) com as alíquotas de REFORMA_IBS_CBS.</summary>
    public static CronogramaReforma Carregar(string json, AliquotasReforma? teste2026)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        static decimal D(JsonElement e, string n) => e.GetProperty(n).GetDecimal();
        var anos = new List<AnoCronograma>();
        if (teste2026 is not null)
            anos.Add(new(2026, teste2026.Cbs ?? 0, (teste2026.IbsUf ?? 0) + (teste2026.IbsMun ?? 0), 0, 0, true, teste2026.Observacao));
        anos.AddRange(r.GetProperty("anos").EnumerateArray().Select(a => new AnoCronograma(a.GetProperty("ano").GetInt32(), D(a, "cbs"), D(a, "ibs"),
            D(a, "pis_cofins_extinto"), D(a, "icms_iss_extinto"), false, a.TryGetProperty("observacao", out var o) ? o.GetString() : null)));
        return new CronogramaReforma(anos.OrderBy(a => a.Ano).ToList(), r.TryGetProperty("carga_dentro_do_das", out var c) ? c.GetDecimal() : 100m,
            r.TryGetProperty("fundamento", out var f) ? f.GetString() ?? "" : "");
    }
}

/// <summary>Dados da empresa para a simulação (12 meses): receita, compras com direito a crédito, atividade e folha (Fator R).</summary>
public sealed record BaseSimulacao(decimal Receita12, decimal Compras12, AtividadeSimples Atividade, decimal Folha12, decimal ReducaoAliquota);

/// <summary>Resultado de um ano: imposto atual (DAS de hoje) × projeção com IBS/CBS dentro do DAS × projeção com IBS/CBS fora do DAS (regime regular).</summary>
public sealed record LinhaSimulacao(int Ano, string Fase, decimal DasAtual, decimal DasDentro, decimal TotalFora, decimal DasFora, decimal CbsFora, decimal IbsFora,
                                    decimal CreditoClienteDentro, decimal CreditoClienteFora)
{
    public decimal DiferencaDentro => DasDentro - DasAtual;
    public decimal DiferencaFora => TotalFora - DasAtual;
    public string Melhor => TotalFora < DasDentro ? "Fora do DAS" : TotalFora > DasDentro ? "Dentro do DAS" : "Igual";
    public static decimal? Percentual(decimal diferenca, decimal baseValor) => baseValor == 0 ? null : Math.Round(diferenca / baseValor * 100m, 2, MidpointRounding.AwayFromZero);
}

public sealed record SimulacaoSimples(BaseSimulacao Base, AnexoSimples Anexo, int Faixa, decimal AliquotaEfetiva, decimal AliquotaAplicada, decimal DasAnual,
                                      decimal PisCofinsAnual, decimal IcmsIssAnual, IReadOnlyList<LinhaSimulacao> Linhas, CronogramaReforma Cronograma, IReadOnlyList<string> Avisos)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static string P(decimal? v) => v is { } x ? x.ToString("N2", PtBr) + "%" : "—";

    public decimal ParteConsumoNoDas => DasAnual == 0 ? 0 : Math.Round((PisCofinsAnual + IcmsIssAnual) / DasAnual * 100m, 2, MidpointRounding.AwayFromZero);

    public IReadOnlyList<TabelaRelatorio> Tabelas()
    {
        var premissas = new TabelaRelatorio("Situação atual do Simples Nacional",
            [new("Item", LarguraCm: 9), new("Valor", TipoColuna.Texto, 5)],
            [
                new(["Receita dos 12 meses", Formato.Moeda(Base.Receita12)]),
                new(["Compras com direito a crédito (12 meses)", Formato.Moeda(Base.Compras12)]),
                new(["Anexo / faixa", $"Anexo {Anexo} / {Faixa}ª faixa"]),
                new(["Alíquota efetiva", AliquotaEfetiva.ToString("N4", PtBr) + "%"]),
                new(["DAS anual (imposto de hoje)", Formato.Moeda(DasAnual)]),
                new(["Parte do DAS que é PIS e COFINS", Formato.Moeda(PisCofinsAnual)]),
                new(["Parte do DAS que é ICMS e ISS", Formato.Moeda(IcmsIssAnual)]),
                new(["Peso de PIS, COFINS, ICMS e ISS no DAS", P(ParteConsumoNoDas)]),
                new(["Redução de alíquota considerada na opção fora do DAS", P(Base.ReducaoAliquota)]),
            ]);
        var linhas = Linhas.Select(l => new LinhaRelatorio([l.Ano.ToString(CultureInfo.InvariantCulture), l.Fase, l.DasAtual, l.DasDentro, l.DiferencaDentro, P(LinhaSimulacao.Percentual(l.DiferencaDentro, l.DasAtual)),
            l.DasFora, l.CbsFora, l.IbsFora, l.TotalFora, l.DiferencaFora, P(LinhaSimulacao.Percentual(l.DiferencaFora, l.DasAtual)), l.CreditoClienteDentro, l.CreditoClienteFora, l.Melhor])).ToList();
        var principal = new TabelaRelatorio("Simples Nacional × IBS/CBS (ano a ano)",
            [new("Ano", LarguraCm: 1.4), new("Fase", LarguraCm: 3.2), new("Imposto atual (DAS)", TipoColuna.Valor), new("A: tudo dentro do DAS", TipoColuna.Valor),
             new("A: diferença R$", TipoColuna.Valor), new("A: diferença %", TipoColuna.Texto), new("B: DAS restante", TipoColuna.Valor), new("B: CBS fora do DAS", TipoColuna.Valor),
             new("B: IBS fora do DAS", TipoColuna.Valor), new("B: total", TipoColuna.Valor), new("B: diferença R$", TipoColuna.Valor), new("B: diferença %", TipoColuna.Texto),
             new("Crédito ao cliente (A)", TipoColuna.Valor), new("Crédito ao cliente (B)", TipoColuna.Valor), new("Menor imposto", LarguraCm: 3)],
            linhas, Explicacao);
        var fases = new TabelaRelatorio("Cronograma usado (parâmetros legais)",
            [new("Ano", LarguraCm: 1.4), new("CBS %", TipoColuna.Texto), new("IBS %", TipoColuna.Texto), new("PIS/COFINS extintos", TipoColuna.Texto), new("ICMS/ISS extintos", TipoColuna.Texto), new("Observação", LarguraCm: 12)],
            Cronograma.Anos.Select(a => new LinhaRelatorio([a.Ano.ToString(CultureInfo.InvariantCulture), P(a.Cbs), P(a.Ibs), P(a.PisCofinsExtinto), P(a.IcmsIssExtinto), a.Observacao ?? ""])).ToList(),
            Cronograma.Fundamento);
        return [premissas, principal, fases];
    }

    public const string Explicacao =
        "A: a empresa continua no Simples e o IBS e a CBS ficam dentro do DAS (as partes de PIS/COFINS viram CBS e as de ICMS/ISS viram IBS, na mesma proporção): o valor total tende a ficar igual, mas o cliente só aproveita como crédito o IBS/CBS que está dentro do DAS. " +
        "B: a empresa continua no Simples para os demais tributos, mas apura o IBS e a CBS fora do DAS, pelo regime regular: paga a alíquota cheia sobre a venda e abate o crédito das compras; o DAS diminui as partes de PIS/COFINS e de ICMS/ISS já extintas, e o cliente aproveita o crédito integral. " +
        "Simulação com a receita e as compras dos últimos 12 meses mantidas nos anos seguintes; as alíquotas são de referência e só valem as fixadas por lei e pelo Senado. 2026 é ano de teste (valores só informativos).";
}

public static class CalculoSimulacaoSimples
{
    /// <summary>
    /// Projeta o imposto de cada ano do cronograma. <paramref name="mensal"/> é o cálculo do DAS de um mês típico (receita ÷ 12, RBT12 = receita dos 12 meses);
    /// os valores anuais são 12 vezes esse mês. Opção A (dentro do DAS): PIS/COFINS viram CBS e ICMS/ISS viram IBS na proporção do cronograma, sem mudar a carga
    /// (ajustável por <see cref="CronogramaReforma.CargaDentroDoDas"/>). Opção B (fora do DAS): saem do DAS as partes extintas e o IBS/CBS é apurado pelo regime regular,
    /// com a alíquota do ano sobre a receita menos as compras (crédito); o resultado negativo é saldo credor.
    /// </summary>
    public static SimulacaoSimples Simular(BaseSimulacao b, ResultadoSimples mensal, CronogramaReforma crono)
    {
        var tributos = mensal.TotalPorTributo;
        decimal Anual(params string[] nomes) => nomes.Sum(n => tributos.GetValueOrDefault(n)) * 12m;
        var das = mensal.ValorDas * 12m;
        var pisCofins = Anual("PIS", "COFINS");
        var icmsIss = Anual("ICMS", "ISS");
        var seg = mensal.Segmentos.FirstOrDefault();
        var reducao = Math.Clamp(b.ReducaoAliquota, 0, 100) / 100m;
        var carga = crono.CargaDentroDoDas / 100m;
        var linhas = new List<LinhaSimulacao>();
        foreach (var a in crono.Anos)
        {
            if (a.Teste)
            {
                var destacado = Dinheiro.Arredondar(b.Receita12 * (a.Cbs + a.Ibs) / 100m);
                linhas.Add(new(a.Ano, "Teste (informativo)", das, das, das, das, 0, 0, destacado, destacado));
                continue;
            }
            var pc = a.PisCofinsExtinto / 100m;
            var ii = a.IcmsIssExtinto / 100m;
            var cbsDentro = pisCofins * pc * carga;
            var ibsDentro = icmsIss * ii * carga;
            var dentro = Dinheiro.Arredondar(das - pisCofins * pc - icmsIss * ii + cbsDentro + ibsDentro);
            var dasFora = Dinheiro.Arredondar(das - pisCofins * pc - icmsIss * ii);
            var fator = 1m - reducao;
            var cbsFora = Dinheiro.Arredondar((b.Receita12 - b.Compras12) * a.Cbs / 100m * fator);
            var ibsFora = Dinheiro.Arredondar((b.Receita12 - b.Compras12) * a.Ibs / 100m * fator);
            var creditoFora = Dinheiro.Arredondar(b.Receita12 * (a.Cbs + a.Ibs) / 100m * fator);
            linhas.Add(new(a.Ano, Fase(a), das, dentro, dasFora + cbsFora + ibsFora, dasFora, cbsFora, ibsFora, Dinheiro.Arredondar(cbsDentro + ibsDentro), creditoFora));
        }
        var avisos = new List<string>(mensal.Avisos);
        if (b.Compras12 > b.Receita12) avisos.Add("As compras superam a receita: o IBS e a CBS da opção B ficam negativos (saldo credor).");
        if (b.Compras12 == 0) avisos.Add("Sem compras informadas: a opção B não considera crédito algum.");
        return new SimulacaoSimples(b, seg?.Anexo ?? default, seg?.Faixa ?? 0, seg?.AliquotaEfetiva ?? 0, seg?.AliquotaAplicada ?? 0, das, pisCofins, icmsIss, linhas, crono, avisos);
    }

    private static string Fase(AnoCronograma a) => a.IcmsIssExtinto switch
    {
        >= 100 => "Reforma completa",
        > 0 => "Transição do IBS",
        _ => "CBS em vigor"
    };
}
