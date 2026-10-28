using Vorticis.Application.Contabil;
using Vorticis.Domain.Contabil;

namespace Vorticis.Application.Relatorios;

/// <summary>DFC e EBITDA como tabelas de relatório (tela, PDF, Excel, CSV e Livro Contábil Completo usam as mesmas).</summary>
public static class TabelasFluxos
{
    public const string TituloDfc = "DFC — Demonstração dos Fluxos de Caixa";
    public const string TituloEbitda = "EBITDA (LAJIDA)";
    public const string TituloEbitdaMensal = "EBITDA mensal";
    public const string TituloEbitdaCentro = "EBITDA por centro de custo";

    private static readonly IReadOnlyList<ColunaRelatorio> ColunasDescricao = [new("Descrição"), new("R$", TipoColuna.Valor, 3.2)];

    public static string RodapeDfc(DfcApurada a)
    {
        var d = a.Demonstracao;
        var metodo = d.Direto ? "Método direto, com a conciliação pelo método indireto." : "Método indireto (CPC 03).";
        var confere = a.ConfereBalanco && d.Fecha
            ? $"✓ Caixa e equivalentes conferem com o Balanço Patrimonial (início {Formato(a.CaixaBalancoInicio)}; fim {Formato(a.CaixaBalancoFim)})."
            : $"✗ Não confere: caixa pelo Balanço {Formato(a.CaixaBalancoInicio)} → {Formato(a.CaixaBalancoFim)}; pela DFC {Formato(d.SaldoInicial)} → {Formato(d.SaldoFinal)}; diferença nas atividades {Formato(d.Diferenca)}. Revise a Classificação da DFC.";
        return $"{metodo} {confere}";
    }

    private static string Formato(decimal v) => v.ToString("#,##0.00", Vorticis.Domain.Comum.Formato.PtBr);

    public static TabelaRelatorio Dfc(DfcApurada a) =>
        new(TituloDfc, ColunasDescricao, a.Demonstracao.Linhas.Select(l => new LinhaRelatorio([l.Descricao, l.Valor],
            Destaque: l.Tipo is TipoLinhaFluxo.Titulo or TipoLinhaFluxo.Subtotal or TipoLinhaFluxo.Total, Recuo: l.Recuo)).ToList(), RodapeDfc(a));

    public static IReadOnlyList<LinhaComparavel> LinhasDfc(DemonstracaoFluxoCaixa d) =>
        d.Linhas.Select(l => new LinhaComparavel(l.Chave, [l.Descricao], l.Valor, l.Tipo is TipoLinhaFluxo.Titulo or TipoLinhaFluxo.Subtotal or TipoLinhaFluxo.Total, l.Recuo)).ToList();

    public static string RodapeEbitda(DemonstracaoEbitda e) =>
        "EBITDA = lucro operacional + depreciação e amortização. " + (e.ConfereDre ? "✓ Confere com o lucro líquido da DRE." : "✗ Não confere com a DRE: revise a Classificação da DFC e do EBITDA.");

    public static TabelaRelatorio Ebitda(DemonstracaoEbitda e) =>
        new(TituloEbitda, ColunasDescricao, e.Linhas.Select(l => new LinhaRelatorio([l.Descricao, l.Valor],
            Destaque: l.Tipo is TipoLinhaEbitda.Subtotal or TipoLinhaEbitda.Total, Recuo: l.Recuo)).ToList(), RodapeEbitda(e));

    public static IReadOnlyList<LinhaComparavel> LinhasEbitda(DemonstracaoEbitda e) =>
        e.Linhas.Select(l => new LinhaComparavel(l.Chave, [l.Descricao], l.Valor, l.Tipo is TipoLinhaEbitda.Subtotal or TipoLinhaEbitda.Total, l.Recuo,
                                                 Percentual: l.Tipo == TipoLinhaEbitda.Percentual)).ToList();

    /// <summary>EBITDA mês a mês: uma linha por indicador, uma coluna por mês e o total do ano.</summary>
    public static TabelaRelatorio EbitdaMensal(IReadOnlyList<EbitdaRecorte> meses)
    {
        var colunas = new List<ColunaRelatorio> { new("Indicador", LarguraCm: 4.5) };
        colunas.AddRange(meses.Select(m => new ColunaRelatorio(m.Rotulo, TipoColuna.Valor, 2.1)));
        LinhaRelatorio L(string nome, Func<DemonstracaoEbitda, decimal> f, bool destaque = false) =>
            new(new object?[] { nome }.Concat(meses.Select(m => (object?)f(m.Demonstracao))).ToList(), destaque);
        return new(TituloEbitdaMensal, colunas,
        [
            L("Receita líquida", d => d.ReceitaLiquida),
            L("Lucro operacional (EBIT)", d => d.LucroOperacional),
            L("(+) Depreciação e amortização", d => -d.Depreciacao),
            L("EBITDA", d => d.Ebitda, true),
            L("Itens não recorrentes", d => d.NaoRecorrentes),
            L("EBITDA ajustado", d => d.EbitdaAjustado, true),
            L("Margem EBITDA (%)", d => d.MargemEbitda),
            L("Lucro líquido", d => d.LucroLiquido),
        ], "Meses sem lançamentos aparecem zerados. Itens não recorrentes: contas de resultado marcadas na Classificação da DFC e do EBITDA.");
    }

    /// <summary>EBITDA por centro de custo: uma linha por centro e uma última com o total.</summary>
    public static TabelaRelatorio EbitdaPorCentro(IReadOnlyList<EbitdaRecorte> centros) =>
        new(TituloEbitdaCentro,
            [new("Centro de custo"), new("Receita líquida", TipoColuna.Valor), new("Lucro operacional", TipoColuna.Valor), new("EBITDA", TipoColuna.Valor),
             new("Margem %", TipoColuna.Valor, 2), new("EBITDA ajustado", TipoColuna.Valor)],
            centros.Select((c, i) => new LinhaRelatorio([c.Rotulo, c.Demonstracao.ReceitaLiquida, c.Demonstracao.LucroOperacional, c.Demonstracao.Ebitda,
                                                          c.Demonstracao.MargemEbitda, c.Demonstracao.EbitdaAjustado], Destaque: i == centros.Count - 1)).ToList(),
            "Receitas, custos e despesas sem centro de custo aparecem em \"Sem centro de custo\". Lançamentos de encerramento não entram.");
}
