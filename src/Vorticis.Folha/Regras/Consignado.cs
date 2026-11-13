using Vorticis.Domain.Comum;

namespace Vorticis.Folha.Regras;

/// <summary>Contrato de empréstimo consignado do funcionário (Lei nº 10.820/2003), com as parcelas já descontadas.</summary>
public sealed record ContratoConsignado(long Id, long FuncionarioId, string Instituicao, string Contrato, decimal ValorParcela, int TotalParcelas,
                                        DateOnly PrimeiraCompetencia, string Situacao = "ATIVO", string? Observacao = null, string? FuncionarioNome = null,
                                        string? Matricula = null, int ParcelasDescontadas = 0, DateOnly? UltimaCompetencia = null)
{
    public int ParcelasRestantes => Math.Max(0, TotalParcelas - ParcelasDescontadas);
    public decimal SaldoDevedor => ValorParcela * ParcelasRestantes;
    public string ParcelasTexto => $"{ParcelasDescontadas}/{TotalParcelas}";
}

/// <summary>Margem consignável do funcionário na competência.</summary>
public sealed record MargemConsignavel(long FuncionarioId, string Matricula, string Nome, decimal Remuneracao, decimal DescontosObrigatorios,
                                       decimal Disponivel, decimal Margem, decimal Comprometido)
{
    public decimal Livre => Margem - Comprometido;
}

public static class RegrasConsignado
{
    /// <summary>Número da parcela a descontar na <paramref name="competencia"/> ou null (fora do prazo, quitado ou cancelado).</summary>
    public static int? ParcelaDaCompetencia(ContratoConsignado c, DateOnly competencia)
    {
        if (c.Situacao != "ATIVO" || competencia < c.PrimeiraCompetencia || c.ParcelasDescontadas >= c.TotalParcelas) return null;
        if (c.UltimaCompetencia is { } u && competencia <= u) return null;
        return c.ParcelasDescontadas + 1;
    }

    /// <summary>
    /// Margem para empréstimos: <paramref name="percentual"/> da remuneração disponível, que é a remuneração menos os descontos obrigatórios
    /// (INSS, IRRF e pensão alimentícia).
    /// </summary>
    public static decimal Margem(decimal remuneracao, decimal descontosObrigatorios, decimal percentual)
        => Dinheiro.Arredondar(Math.Max(0, remuneracao - descontosObrigatorios) * percentual / 100m);

    public static string? Validar(ContratoConsignado c)
    {
        if (string.IsNullOrWhiteSpace(c.Instituicao)) return "Informe a instituição financeira.";
        if (string.IsNullOrWhiteSpace(c.Contrato)) return "Informe o número do contrato.";
        if (c.ValorParcela <= 0) return "Informe o valor da parcela.";
        if (c.TotalParcelas is < 1 or > 120) return "Quantidade de parcelas: de 1 a 120.";
        return null;
    }
}
