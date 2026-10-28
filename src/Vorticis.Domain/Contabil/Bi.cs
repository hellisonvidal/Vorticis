using System.Globalization;
using System.Text;

namespace Vorticis.Domain.Contabil;

// ---- Indicadores ----------------------------------------------------------------------------------------------------

public enum FormatoIndicador { Moeda, Percentual, Razao, Dias }

/// <summary>Um indicador calculado: valor (nulo quando não há dados para calcular), fórmula e interpretação curta.</summary>
public sealed record Indicador(string Chave, string Grupo, string Nome, FormatoIndicador Formato, bool MaiorEMelhor, string Formula, string Interpretacao, decimal? Valor);

/// <summary>Valores do balanço (positivos) usados nos indicadores: ativo pelo saldo devedor, passivo e PL pelo saldo credor.</summary>
public sealed record PosicaoPatrimonial(decimal Disponivel, decimal Clientes, decimal Estoques, decimal AtivoCirculante, decimal RealizavelLongoPrazo,
                                        decimal AtivoNaoCirculante, decimal Fornecedores, decimal PassivoCirculante, decimal PassivoNaoCirculante,
                                        decimal PatrimonioLiquido)
{
    public decimal AtivoTotal => AtivoCirculante + AtivoNaoCirculante;
    public decimal Dividas => PassivoCirculante + PassivoNaoCirculante;
}

public sealed record PontoMes(DateOnly Mes, decimal Receita, decimal Despesas)
{
    public decimal Lucro => Receita - Despesas;
}

public sealed record ParteValor(string Rotulo, decimal Valor);

/// <summary>Degrau do gráfico em cascata: valor de variação, ou total (barra que parte do zero) quando <paramref name="Total"/>.</summary>
public sealed record DegrauCascata(string Rotulo, decimal Valor, bool Total);

public sealed record PainelBi(DateOnly Inicio, DateOnly Fim, DateOnly InicioAnterior, DateOnly FimAnterior, IReadOnlyList<Indicador> Atual, IReadOnlyList<Indicador> Anterior,
                              DemonstracaoEbitda Resultado, IReadOnlyList<PontoMes> Meses, IReadOnlyList<ParteValor> DespesasPorGrupo,
                              IReadOnlyList<ParteValor> CustosPorCentro, IReadOnlyList<DegrauCascata> Cascata)
{
    public Indicador? Obter(string chave) => Atual.FirstOrDefault(i => i.Chave == chave);
    public Indicador? ObterAnterior(string chave) => Anterior.FirstOrDefault(i => i.Chave == chave);
}

public static class CalculoBi
{
    private static string Normalizar(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in d) if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToUpperInvariant(ch));
        return sb.ToString();
    }

    /// <summary>
    /// Monta a posição patrimonial pelos saldos (débito positivo) na data. Circulante, não circulante, disponível, estoques, clientes e fornecedores
    /// são reconhecidos pelos nomes dos grupos do plano (e, na falta deles, pelo segundo nível do código: x.1 circulante, x.2 não circulante).
    /// O patrimônio líquido inclui o resultado ainda não encerrado.
    /// </summary>
    public static PosicaoPatrimonial Posicao(IReadOnlyList<ContaPlano> plano, IReadOnlyDictionary<long, decimal> saldos)
    {
        var porId = plano.ToDictionary(c => c.Id);
        var nomes = new Dictionary<long, string[]>();
        string[] Cadeia(ContaPlano c)
        {
            if (nomes.TryGetValue(c.Id, out var n)) return n;
            var lista = new List<string> { Normalizar(c.Descricao) };
            var atual = c;
            for (var guarda = 0; guarda < 20 && atual.ContaPaiId is { } pai && porId.TryGetValue(pai, out var p); guarda++) { lista.Add(Normalizar(p.Descricao)); atual = p; }
            return nomes[c.Id] = lista.ToArray();
        }

        decimal disp = 0, cli = 0, est = 0, ac = 0, rlp = 0, anc = 0, forn = 0, pc = 0, pnc = 0, pl = 0;
        foreach (var c in plano.Where(c => c.Analitica))
        {
            var s = saldos.GetValueOrDefault(c.Id);
            if (s == 0) continue;
            var cad = Cadeia(c);
            bool Tem(string t) => cad.Any(x => x.Contains(t));
            var segundo = c.Codigo.Split('.').ElementAtOrDefault(1);
            var naoCirc = Tem("NAO CIRCULANTE") || (!Tem("CIRCULANTE") && segundo != "1");
            switch (c.CodNat)
            {
                case "01":
                    if (naoCirc)
                    {
                        anc += s;
                        if (Tem("REALIZAVEL A LONGO PRAZO")) rlp += s;
                    }
                    else
                    {
                        ac += s;
                        var d = cad[0];
                        if (Tem("ESTOQUE")) est += s;
                        if (cad.Skip(1).Any(x => x.Contains("DISPONIVEL")) || d.Contains("CAIXA") || d.StartsWith("BANCO") || d.Contains("APLICACOES FINANCEIRAS DE LIQUIDEZ")) disp += s;
                        if (d.Contains("CLIENTE") || d.Contains("DUPLICATAS A RECEBER")) cli += s;
                    }
                    break;
                case "02":
                    if (naoCirc) pnc -= s; else pc -= s;
                    if (!naoCirc && cad[0].Contains("FORNECEDOR")) forn -= s;
                    break;
                case "03" or "04":
                    pl -= s;
                    break;
            }
        }
        return new PosicaoPatrimonial(disp, cli, est, ac, rlp, anc, forn, pc, pnc, pl);
    }

    private static decimal? Razao(decimal num, decimal den, int casas = 2, decimal fator = 1) =>
        den == 0 ? null : Math.Round(num / den * fator, casas, MidpointRounding.AwayFromZero);

    /// <summary>Todos os indicadores do painel e da análise, na ordem de exibição.</summary>
    public static IReadOnlyList<Indicador> Indicadores(PosicaoPatrimonial p, DemonstracaoEbitda r, int dias)
    {
        var despesas = -(r.CustosDespesas + r.Depreciacao);
        var custosOperacionais = -r.CustosDespesas;
        decimal? Prazo(decimal saldo, decimal base_) => saldo == 0 ? null : Razao(saldo, base_, 1, dias);
        return
        [
            new("receita", "Resultado", "Receita líquida", FormatoIndicador.Moeda, true, "Receita bruta − deduções da receita", "Quanto a empresa vendeu no período, já sem impostos e devoluções.", r.ReceitaLiquida),
            new("despesas", "Resultado", "Custos e despesas", FormatoIndicador.Moeda, false, "Custos e despesas operacionais + depreciação e amortização", "Quanto custou operar. Cresça menos que a receita.", despesas),
            new("lucro", "Resultado", "Lucro líquido", FormatoIndicador.Moeda, true, "Resultado final da DRE do período", "O que sobrou depois de tudo. Negativo significa prejuízo.", r.LucroLiquido),
            new("ebitda", "Resultado", "EBITDA", FormatoIndicador.Moeda, true, "Lucro operacional + depreciação e amortização", "Geração de caixa da operação, antes de juros, impostos sobre o lucro e depreciação.", r.Ebitda),
            new("margem_ebitda", "Resultado", "Margem EBITDA (%)", FormatoIndicador.Percentual, true, "EBITDA ÷ receita líquida × 100", "Quantos centavos de cada real vendido viram caixa operacional.", Razao(r.Ebitda, r.ReceitaLiquida, 2, 100)),
            new("margem_liquida", "Resultado", "Margem líquida (%)", FormatoIndicador.Percentual, true, "Lucro líquido ÷ receita líquida × 100", "Quantos centavos de cada real vendido sobram como lucro.", Razao(r.LucroLiquido, r.ReceitaLiquida, 2, 100)),

            new("liq_corrente", "Liquidez", "Liquidez corrente", FormatoIndicador.Razao, true, "Ativo circulante ÷ passivo circulante", "Acima de 1,00: há dinheiro e direitos de curto prazo para pagar as dívidas de curto prazo.", Razao(p.AtivoCirculante, p.PassivoCirculante)),
            new("liq_seca", "Liquidez", "Liquidez seca", FormatoIndicador.Razao, true, "(Ativo circulante − estoques) ÷ passivo circulante", "Como a corrente, sem contar com a venda dos estoques.", Razao(p.AtivoCirculante - p.Estoques, p.PassivoCirculante)),
            new("liq_imediata", "Liquidez", "Liquidez imediata", FormatoIndicador.Razao, true, "Disponível (caixa e bancos) ÷ passivo circulante", "Quanto da dívida de curto prazo dá para pagar agora, só com o dinheiro em caixa e bancos.", Razao(p.Disponivel, p.PassivoCirculante)),
            new("liq_geral", "Liquidez", "Liquidez geral", FormatoIndicador.Razao, true, "(Ativo circulante + realizável a longo prazo) ÷ (passivo circulante + não circulante)", "Capacidade de pagar todas as dívidas com o que a empresa tem a receber e a gastar.", Razao(p.AtivoCirculante + p.RealizavelLongoPrazo, p.Dividas)),

            new("endividamento", "Estrutura", "Endividamento (%)", FormatoIndicador.Percentual, false, "(Passivo circulante + não circulante) ÷ ativo total × 100", "Quanto do que a empresa tem foi financiado por dívidas. Quanto menor, mais folga.", Razao(p.Dividas, p.AtivoTotal, 2, 100)),
            new("composicao_divida", "Estrutura", "Composição do endividamento (%)", FormatoIndicador.Percentual, false, "Passivo circulante ÷ (passivo circulante + não circulante) × 100", "Parte da dívida que vence em até um ano. Quanto menor, mais tempo para pagar.", Razao(p.PassivoCirculante, p.Dividas, 2, 100)),
            new("capital_terceiros", "Estrutura", "Capital de terceiros sobre o PL (%)", FormatoIndicador.Percentual, false, "(Passivo circulante + não circulante) ÷ patrimônio líquido × 100", "Quanto a empresa deve para cada R$ 100 de capital próprio.", Razao(p.Dividas, p.PatrimonioLiquido, 2, 100)),

            new("roe", "Rentabilidade", "Retorno sobre o patrimônio líquido — ROE (%)", FormatoIndicador.Percentual, true, "Lucro líquido ÷ patrimônio líquido × 100", "Quanto o capital dos sócios rendeu no período.", Razao(r.LucroLiquido, p.PatrimonioLiquido, 2, 100)),
            new("roa", "Rentabilidade", "Retorno sobre o ativo — ROA (%)", FormatoIndicador.Percentual, true, "Lucro líquido ÷ ativo total × 100", "Quanto cada real investido nos bens e direitos da empresa rendeu no período.", Razao(r.LucroLiquido, p.AtivoTotal, 2, 100)),
            new("giro_ativo", "Rentabilidade", "Giro do ativo (vezes)", FormatoIndicador.Razao, true, "Receita líquida ÷ ativo total", "Quantas vezes a receita do período cobre o ativo. Quanto maior, melhor o uso dos bens.", Razao(r.ReceitaLiquida, p.AtivoTotal)),

            new("pmr", "Prazos", "Prazo médio de recebimento (dias)", FormatoIndicador.Dias, false, "Clientes ÷ receita bruta × dias do período", "Em quantos dias, em média, os clientes pagam. Só aparece se houver contas de clientes.", Prazo(p.Clientes, r.ReceitaBruta)),
            new("pmp", "Prazos", "Prazo médio de pagamento (dias)", FormatoIndicador.Dias, true, "Fornecedores ÷ custos e despesas operacionais × dias do período", "Em quantos dias, em média, a empresa paga os fornecedores. Só aparece se houver contas de fornecedores.", Prazo(p.Fornecedores, custosOperacionais)),
            new("pme", "Prazos", "Prazo médio de estocagem (dias)", FormatoIndicador.Dias, false, "Estoques ÷ custos e despesas operacionais × dias do período", "Por quantos dias, em média, o estoque fica parado. Só aparece se houver estoques.", Prazo(p.Estoques, custosOperacionais)),
        ];
    }

    public static IReadOnlyList<DegrauCascata> Cascata(DemonstracaoEbitda r) =>
    [
        new("Receita bruta", r.ReceitaBruta, true),
        new("Deduções", r.Deducoes, false),
        new("Custos e despesas", r.CustosDespesas, false),
        new("Depreciação", r.Depreciacao, false),
        new("Resultado financeiro", r.ResultadoFinanceiro, false),
        new("IR e CSLL", r.ImpostoLucro, false),
        new("Lucro líquido", r.LucroLiquido, true),
    ];

    /// <summary>Ancestral de nível 2 da conta (o "grupo": 4.2 Despesas operacionais); a própria conta se ela for de nível 1 ou 2.</summary>
    public static ContaPlano Grupo(ContaPlano conta, IReadOnlyDictionary<long, ContaPlano> porId)
    {
        var atual = conta;
        for (var guarda = 0; guarda < 20 && atual.Nivel > 2 && atual.ContaPaiId is { } pai && porId.TryGetValue(pai, out var p); guarda++) atual = p;
        return atual;
    }

    /// <summary>Receita e despesas (custos e despesas) de cada um dos meses entre <paramref name="primeiro"/> e <paramref name="ultimo"/> (primeiros dias dos meses).</summary>
    public static IReadOnlyList<PontoMes> Serie(IReadOnlyList<ContaPlano> plano, IReadOnlyList<MovimentoCentro> movimentos, DateOnly primeiro, DateOnly ultimo)
    {
        var classes = plano.ToDictionary(c => c.Id, Gerenciais.Classe);
        var res = new List<PontoMes>();
        for (var m = new DateOnly(primeiro.Year, primeiro.Month, 1); m <= ultimo; m = m.AddMonths(1))
        {
            decimal rec = 0, desp = 0;
            foreach (var x in movimentos.Where(x => x.Mes.Year == m.Year && x.Mes.Month == m.Month))
                switch (classes.GetValueOrDefault(x.ContaId))
                {
                    case ClasseResultado.Receita: rec += x.Creditos - x.Debitos; break;
                    case ClasseResultado.CustoDespesa: desp += x.Debitos - x.Creditos; break;
                }
            res.Add(new PontoMes(m, rec, desp));
        }
        return res;
    }

    /// <summary>Custos e despesas do período por grupo de contas (nível 2 do plano), do maior para o menor; os menores viram "Outros".</summary>
    public static IReadOnlyList<ParteValor> DespesasPorGrupo(IReadOnlyList<ContaPlano> plano, IReadOnlyList<MovimentoCentro> movimentos, int maximo = 8)
    {
        var porId = plano.ToDictionary(c => c.Id);
        var partes = movimentos.Where(m => porId.TryGetValue(m.ContaId, out var c) && Gerenciais.Classe(c) == ClasseResultado.CustoDespesa)
            .GroupBy(m => Grupo(porId[m.ContaId], porId))
            .Select(g => new ParteValor(g.Key.Descricao, g.Sum(m => m.Debitos - m.Creditos)))
            .Where(p => p.Valor > 0).OrderByDescending(p => p.Valor).ToList();
        return Agrupar(partes, maximo);
    }

    /// <summary>Custos e despesas do período por centro de custo (sem centro = "Sem centro de custo").</summary>
    public static IReadOnlyList<ParteValor> CustosPorCentro(IReadOnlyList<ContaPlano> plano, IReadOnlyList<CentroPlano> centros, IReadOnlyList<MovimentoCentro> movimentos, int maximo = 8)
    {
        var contas = plano.ToDictionary(c => c.Id);
        var nomes = centros.ToDictionary(c => c.Id, c => $"{c.Codigo} {c.Descricao}");
        var partes = movimentos.Where(m => contas.TryGetValue(m.ContaId, out var c) && Gerenciais.Classe(c) == ClasseResultado.CustoDespesa)
            .GroupBy(m => m.CentroId)
            .Select(g => new ParteValor(g.Key != 0 && nomes.TryGetValue(g.Key, out var n) ? n : Gerenciais.SemCentro, g.Sum(m => m.Debitos - m.Creditos)))
            .Where(p => p.Valor > 0).OrderByDescending(p => p.Valor).ToList();
        return Agrupar(partes, maximo);
    }

    private static IReadOnlyList<ParteValor> Agrupar(List<ParteValor> partes, int maximo)
    {
        if (partes.Count <= maximo) return partes;
        var topo = partes.Take(maximo - 1).ToList();
        topo.Add(new ParteValor("Outros", partes.Skip(maximo - 1).Sum(p => p.Valor)));
        return topo;
    }
}
