using Vorticis.Application.Contabil;

namespace Vorticis.Desktop.Comum.ViewModels;

/// <summary>Conta do plano na caixa de pesquisa: busca pelo código, pelo reduzido ou por parte do nome.</summary>
public sealed record ContaPesquisavel(string Codigo, string Descricao, int? Reduzido = null)
{
    public string Rotulo => $"{(Reduzido is { } r ? $"{r,5}  " : "")}{Codigo}  {Descricao}";

    public static bool Combina(ContaPesquisavel c, string? texto)
    {
        var f = (texto ?? "").Trim();
        return f.Length == 0 || c.Codigo.StartsWith(f, StringComparison.Ordinal) || c.Reduzido?.ToString() == f
               || c.Descricao.Contains(f, StringComparison.CurrentCultureIgnoreCase);
    }
}

/// <summary>Contas analíticas do plano oferecidas na pesquisa de cada campo de encerramento (apuração = resultado; lucros e prejuízos = patrimônio líquido).</summary>
public sealed class OpcoesContasEncerramento
{
    public IReadOnlyList<ContaPesquisavel> Apuracao { get; private set; } = [];
    public IReadOnlyList<ContaPesquisavel> Patrimonio { get; private set; } = [];

    public static OpcoesContasEncerramento De(IEnumerable<ContaDetalhe> plano)
    {
        var analiticas = plano.Where(c => c.Analitica && c.Ativa).OrderBy(c => c.Codigo, StringComparer.Ordinal).ToList();
        ContaPesquisavel P(ContaDetalhe c) => new(c.Codigo, c.Descricao, c.CodigoReduzido);
        return new OpcoesContasEncerramento
        {
            Apuracao = [.. analiticas.Where(c => c.CodNat == ConferenciaContasEncerramento.NaturezaResultado).Select(P)],
            Patrimonio = [.. analiticas.Where(c => c.CodNat == ConferenciaContasEncerramento.NaturezaPatrimonio).Select(P)],
        };
    }
}
