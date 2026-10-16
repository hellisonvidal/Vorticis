using System.Globalization;

namespace Perseus.Domain.Comum;

/// <summary>Formatação de valores para o usuário sempre em pt-BR, independente da cultura do Windows.</summary>
public static class Formato
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Moeda(decimal valor) => valor.ToString("N2", PtBr);
}
