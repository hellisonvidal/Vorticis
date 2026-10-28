namespace Vorticis.Domain.Contabil;

public enum TipoLinhaEbitda { Item, Subtotal, Total, Percentual }

public sealed record LinhaEbitda(string Chave, string Descricao, decimal? Valor, TipoLinhaEbitda Tipo, int Recuo = 0);

/// <summary>
/// Demonstração do EBITDA (LAJIDA). Valores com sinal de resultado: receitas positivas, custos e despesas negativos.
/// <see cref="ConfereDre"/>: EBIT + resultado financeiro − IR/CSLL = lucro líquido da DRE.
/// </summary>
public sealed record DemonstracaoEbitda(DateOnly Inicio, DateOnly Fim, IReadOnlyList<LinhaEbitda> Linhas, decimal ReceitaBruta, decimal Deducoes,
                                        decimal ReceitaLiquida, decimal CustosDespesas, decimal Depreciacao, decimal LucroOperacional, decimal Ebitda,
                                        decimal NaoRecorrentes, decimal EbitdaAjustado, decimal ResultadoFinanceiro, decimal ImpostoLucro,
                                        decimal LucroLiquido)
{
    public decimal MargemEbitda => ReceitaLiquida == 0 ? 0 : Math.Round(Ebitda / ReceitaLiquida * 100, 2, MidpointRounding.AwayFromZero);
    public decimal MargemAjustada => ReceitaLiquida == 0 ? 0 : Math.Round(EbitdaAjustado / ReceitaLiquida * 100, 2, MidpointRounding.AwayFromZero);
    public bool ConfereDre => LucroOperacional + ResultadoFinanceiro + ImpostoLucro == LucroLiquido;
}

public static class CalculoEbitda
{
    /// <param name="movimentos">Movimento do período, sem lançamentos de encerramento.</param>
    /// <param name="naoRecorrentes">Contas de resultado marcadas como itens não recorrentes.</param>
    public static DemonstracaoEbitda Montar(DateOnly inicio, DateOnly fim, IReadOnlyList<ContaPlano> plano, IReadOnlyList<MovimentoConta> movimentos,
                                            IReadOnlyDictionary<long, ClasseConta> classes, IReadOnlySet<long> naoRecorrentes)
    {
        var mov = movimentos.GroupBy(m => m.ContaId).ToDictionary(g => g.Key, g => g.Sum(m => m.Creditos - m.Debitos));
        var resultado = plano.Where(c => c.Analitica && c.CodNat == "04" && mov.ContainsKey(c.Id)).ToList();
        ClasseConta Classe(ContaPlano c) => classes.TryGetValue(c.Id, out var k) ? k : ClasseConta.CustoDespesa;
        decimal Soma(Func<ClasseConta, bool> f) => resultado.Where(c => f(Classe(c))).Sum(c => mov[c.Id]);

        var receita = Soma(k => k == ClasseConta.Receita);
        var deducoes = Soma(k => k == ClasseConta.Deducao);
        var depreciacao = Soma(k => k == ClasseConta.Depreciacao);
        var financeiro = Soma(k => k == ClasseConta.Financeiro);
        var imposto = Soma(k => k == ClasseConta.ImpostoLucro);
        var custos = Soma(k => k == ClasseConta.CustoDespesa);
        var receitaLiquida = receita + deducoes;
        var ebit = receitaLiquida + custos + depreciacao;
        var ebitda = ebit - depreciacao;
        // Não recorrentes: retira do EBITDA o efeito das contas marcadas que compõem o lucro operacional (receita sai, despesa volta).
        var marcadas = resultado.Where(c => naoRecorrentes.Contains(c.Id) && Classe(c) is ClasseConta.Receita or ClasseConta.Deducao or ClasseConta.CustoDespesa).ToList();
        var ajuste = -marcadas.Sum(c => mov[c.Id]);
        var ajustado = ebitda + ajuste;
        var lucro = resultado.Sum(c => mov[c.Id]);

        var linhas = new List<LinhaEbitda>
        {
            new("receita", "Receita bruta", receita, TipoLinhaEbitda.Item),
            new("deducoes", "(-) Deduções da receita", deducoes, TipoLinhaEbitda.Item),
            new("liquida", "= Receita líquida", receitaLiquida, TipoLinhaEbitda.Subtotal),
            new("custos", "(-) Custos e despesas operacionais (sem depreciação e amortização)", custos, TipoLinhaEbitda.Item),
            new("deprec", "(-) Depreciação e amortização", depreciacao, TipoLinhaEbitda.Item),
            new("ebit", "= Lucro operacional (EBIT)", ebit, TipoLinhaEbitda.Subtotal),
            new("deprec.mais", "(+) Depreciação e amortização", -depreciacao, TipoLinhaEbitda.Item),
            new("ebitda", "= EBITDA (LAJIDA)", ebitda, TipoLinhaEbitda.Total),
        };
        foreach (var c in marcadas.OrderBy(c => c.Codigo, StringComparer.Ordinal))
            linhas.Add(new("nr." + c.Codigo, $"{(mov[c.Id] >= 0 ? "(-)" : "(+)")} Não recorrente: {c.Codigo} {c.Descricao}", -mov[c.Id], TipoLinhaEbitda.Item, 1));
        linhas.Add(new("ajustado", "= EBITDA ajustado", ajustado, TipoLinhaEbitda.Total));
        linhas.Add(new("margem", "Margem EBITDA (% da receita líquida)", receitaLiquida == 0 ? 0 : Math.Round(ebitda / receitaLiquida * 100, 2, MidpointRounding.AwayFromZero), TipoLinhaEbitda.Percentual));
        linhas.Add(new("margem.aj", "Margem EBITDA ajustada (% da receita líquida)", receitaLiquida == 0 ? 0 : Math.Round(ajustado / receitaLiquida * 100, 2, MidpointRounding.AwayFromZero), TipoLinhaEbitda.Percentual));
        linhas.Add(new("conc", "Conciliação com o lucro líquido", null, TipoLinhaEbitda.Item));
        linhas.Add(new("conc.ebit", "Lucro operacional (EBIT)", ebit, TipoLinhaEbitda.Item, 1));
        linhas.Add(new("conc.fin", "(+/-) Resultado financeiro líquido", financeiro, TipoLinhaEbitda.Item, 1));
        linhas.Add(new("conc.ir", "(-) IR e CSLL", imposto, TipoLinhaEbitda.Item, 1));
        linhas.Add(new("lucro", "= Lucro líquido do período (DRE)", lucro, TipoLinhaEbitda.Subtotal));
        return new DemonstracaoEbitda(inicio, fim, linhas, receita, deducoes, receitaLiquida, custos, depreciacao, ebit, ebitda, ajuste, ajustado, financeiro, imposto, lucro);
    }
}
