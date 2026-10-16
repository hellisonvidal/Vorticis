using System.Globalization;
using Perseus.Domain.Comum;

namespace Perseus.Desktop;

internal static class Numeros
{
    /// <summary>
    /// Lê valores digitados: "1.234,56" e "1234,56" (pt-BR) ou "1234.56" (ponto decimal, comum ao colar de planilhas/OFX).
    /// Um único ponto seguido de 1–2 dígitos é tratado como separador decimal.
    /// </summary>
    public static decimal? LerValor(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim().Replace("R$", "").Replace(" ", "");
        var usaPontoDecimal = !t.Contains(',') && t.Count(c => c == '.') == 1 && t.Length - t.IndexOf('.') - 1 is 1 or 2;
        var cultura = usaPontoDecimal ? CultureInfo.InvariantCulture : Formato.PtBr;
        return decimal.TryParse(t, NumberStyles.Number, cultura, out var v) ? v : null;
    }

    public static string Moeda(decimal v) => Formato.Moeda(v);

    public static string MoedaOuVazio(decimal v) => v == 0 ? "" : Formato.Moeda(v);
}
