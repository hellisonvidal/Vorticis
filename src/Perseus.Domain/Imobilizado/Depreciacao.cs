namespace Perseus.Domain.Imobilizado;

/// <summary>Método de depreciação contábil (NBC TG 27 / CPC 27, item 62).</summary>
public enum MetodoDepreciacao
{
    /// <summary>Quotas constantes: (custo − valor residual) ÷ vida útil.</summary>
    Linear,
    /// <summary>Soma dos dígitos (decrescente): mais depreciação no início da vida útil.</summary>
    SomaDigitos,
}

/// <summary>Dados do bem necessários ao cálculo.</summary>
public sealed record BemParaDepreciar(
    long Id,
    decimal ValorAquisicao,
    decimal ValorResidual,
    int VidaUtilMeses,
    MetodoDepreciacao Metodo,
    DateOnly InicioUso,
    DateOnly? Baixa,
    decimal DepreciacaoAnterior,
    bool Deprecia,
    decimal TaxaAnualFiscal,
    int Turnos);

/// <summary>Quota de uma competência: contábil (CPC 27) e fiscal (RFB), e o acumulado depois dela.</summary>
public sealed record QuotaDepreciacao(long BemId, DateOnly Competencia, decimal Contabil, decimal Fiscal, decimal AcumuladoAnterior, decimal AcumuladoApos)
{
    /// <summary>Fiscal − contábil: diferença a controlar no LALUR/e-Lalur (exclusão quando positiva, adição quando negativa).</summary>
    public decimal Diferenca => Fiscal - Contabil;
}

/// <summary>
/// Regras de depreciação.
/// Contábil (NBC TG 27 / CPC 27): começa quando o bem está disponível para uso (mês do início de uso, quota integral),
/// sobre o valor depreciável (custo − valor residual), pela vida útil estimada; para quando o acumulado atinge o
/// valor depreciável ou o bem é baixado (a competência da baixa não é depreciada).
/// Fiscal (RIR/2018 arts. 317–330; IN RFB 1.700/2017, Anexo III): taxa anual do grupo sobre o custo, em quotas mensais,
/// multiplicada por 1,0 / 1,5 / 2,0 para bens móveis usados em 1 / 2 / 3 turnos de 8 horas (art. 323); limitada ao custo.
/// </summary>
public static class RegrasDepreciacao
{
    public static DateOnly Competencia(DateOnly data) => new(data.Year, data.Month, 1);

    /// <summary>Meses de uso até a competência, contando a do início (1 = primeiro mês).</summary>
    public static int MesDeUso(BemParaDepreciar b, DateOnly competencia) =>
        (competencia.Year - b.InicioUso.Year) * 12 + competencia.Month - b.InicioUso.Month + 1;

    public static decimal FatorTurnos(int turnos) => turnos switch { 2 => 1.5m, 3 => 2.0m, _ => 1.0m };

    /// <summary>
    /// Quota da competência dado o acumulado contábil e fiscal até o mês anterior.
    /// Retorna null quando o bem não deprecia nessa competência.
    /// </summary>
    public static QuotaDepreciacao? Calcular(BemParaDepreciar b, DateOnly competencia, decimal acumuladoContabil, decimal acumuladoFiscal)
    {
        competencia = Competencia(competencia);
        if (!b.Deprecia || b.VidaUtilMeses <= 0) return null;
        if (competencia < Competencia(b.InicioUso)) return null;
        if (b.Baixa is { } baixa && competencia >= Competencia(baixa)) return null;

        var depreciavel = b.ValorAquisicao - b.ValorResidual;
        var restante = depreciavel - b.DepreciacaoAnterior - acumuladoContabil;
        var mes = MesDeUso(b, competencia);
        decimal contabil = 0;
        if (restante > 0)
        {
            var n = b.VidaUtilMeses;
            contabil = b.Metodo switch
            {
                MetodoDepreciacao.SomaDigitos when mes <= n => Arredondar(depreciavel * (n - mes + 1) / (n * (n + 1) / 2m)),
                MetodoDepreciacao.SomaDigitos => restante,
                _ => Arredondar(depreciavel / n),
            };
            // Último mês da vida útil (ou resíduo de arredondamento): leva o que falta.
            if (mes >= n || contabil > restante) contabil = restante;
        }

        var restanteFiscal = b.ValorAquisicao - b.DepreciacaoAnterior - acumuladoFiscal;
        var fiscal = restanteFiscal <= 0 ? 0 : Math.Min(restanteFiscal, Arredondar(b.ValorAquisicao * b.TaxaAnualFiscal / 100m * FatorTurnos(b.Turnos) / 12m));

        if (contabil <= 0 && fiscal <= 0) return null;
        return new QuotaDepreciacao(b.Id, competencia, contabil, fiscal, b.DepreciacaoAnterior + acumuladoContabil,
                                    b.DepreciacaoAnterior + acumuladoContabil + contabil);
    }

    /// <summary>Vida útil fiscal em meses para a taxa anual (ex.: 10% → 120 meses).</summary>
    public static int VidaUtilFiscalMeses(decimal taxaAnual) => taxaAnual <= 0 ? 0 : (int)Math.Round(1200m / taxaAnual);

    private static decimal Arredondar(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
