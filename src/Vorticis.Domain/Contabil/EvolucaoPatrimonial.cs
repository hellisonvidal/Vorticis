namespace Vorticis.Domain.Contabil;

/// <summary>Posição no fim de um mês: ativo, passivo (obrigações), patrimônio líquido e seus grupos, mais o resultado do próprio mês.</summary>
public sealed record PontoPatrimonial(DateOnly Data, decimal Ativo, decimal Passivo, decimal PatrimonioLiquido, decimal Capital, decimal Reservas,
                                      decimal Lucros, decimal Resultado);

/// <summary>Variação de um valor sobre o período anterior (ou sobre o primeiro): absoluta e em %. Percentual nulo quando a base é zero.</summary>
public sealed record VariacaoValor(decimal Absoluta, decimal? Percentual)
{
    public static VariacaoValor De(decimal atual, decimal baseComparacao) =>
        new(atual - baseComparacao, baseComparacao == 0 ? null : Math.Round((atual - baseComparacao) / Math.Abs(baseComparacao) * 100m, 2, MidpointRounding.AwayFromZero));
}

public static class EvolucaoPatrimonial
{
    public const int MaximoMeses = 120;

    /// <summary>Último dia de cada mês entre os meses de <paramref name="inicio"/> e <paramref name="fim"/> (a limitar a <see cref="MaximoMeses"/>), ou só os de dezembro (e o último mês) se <paramref name="soFimDeAno"/>.</summary>
    public static IReadOnlyList<DateOnly> Datas(DateOnly inicio, DateOnly fim, bool soFimDeAno)
    {
        var res = new List<DateOnly>();
        var ultimo = new DateOnly(fim.Year, fim.Month, 1);
        for (var m = new DateOnly(inicio.Year, inicio.Month, 1); m <= ultimo; m = m.AddMonths(1))
            if (!soFimDeAno || m.Month == 12 || m == ultimo) res.Add(m.AddMonths(1).AddDays(-1));
        return res;
    }

    /// <summary>
    /// Monta o ponto de cada data com os saldos (débito positivo) do fim do mês. Ativo e passivo vêm de <see cref="CalculoBi.Posicao"/>;
    /// o patrimônio líquido é dividido em capital social, reservas (de capital, de lucros, ajustes e ações em tesouraria) e lucros/prejuízos
    /// acumulados — este inclui o resultado ainda não encerrado, para que os grupos somem o patrimônio líquido. O resultado vem do mês.
    /// </summary>
    public static PontoPatrimonial Ponto(IReadOnlyList<ContaPlano> plano, IReadOnlyDictionary<long, decimal> saldos, DateOnly data, decimal resultadoDoMes)
    {
        var p = CalculoBi.Posicao(plano, saldos);
        var porId = plano.ToDictionary(c => c.Id);
        decimal capital = 0, reservas = 0;
        foreach (var c in plano.Where(c => c.Analitica && c.CodNat == "03"))
        {
            var s = -saldos.GetValueOrDefault(c.Id);
            if (s == 0) continue;
            switch (Mutacoes.Classificar(c, porId))
            {
                case ColunaPl.CapitalSocial: capital += s; break;
                case ColunaPl.LucrosAcumulados: break;
                default: reservas += s; break;
            }
        }
        return new PontoPatrimonial(data, p.AtivoTotal, p.Dividas, p.PatrimonioLiquido, capital, reservas, p.PatrimonioLiquido - capital - reservas, resultadoDoMes);
    }

    /// <summary>Variação de cada ponto sobre o anterior (o primeiro fica sem variação).</summary>
    public static IReadOnlyList<VariacaoValor?> Variacoes(IReadOnlyList<PontoPatrimonial> pontos, Func<PontoPatrimonial, decimal> campo) =>
        pontos.Select((p, i) => i == 0 ? null : VariacaoValor.De(campo(p), campo(pontos[i - 1]))).ToList();
}

/// <summary>Resultado da consulta: um ponto por mês (ou por fim de ano) entre os meses escolhidos.</summary>
public sealed record EvolucaoBi(DateOnly Inicio, DateOnly Fim, bool SoFimDeAno, IReadOnlyList<PontoPatrimonial> Pontos);
