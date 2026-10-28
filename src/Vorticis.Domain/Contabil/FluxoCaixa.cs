namespace Vorticis.Domain.Contabil;

public enum TipoLinhaFluxo { Titulo, Item, Subtotal, Total }

/// <param name="Chave">Identifica a linha entre períodos (comparativo).</param>
public sealed record LinhaFluxo(string Chave, string Descricao, decimal? Valor, TipoLinhaFluxo Tipo, int Recuo = 0);

/// <summary>
/// DFC (CPC 03). <see cref="Diferenca"/> é a diferença entre a variação de caixa calculada pelas três atividades e a
/// variação real das contas de caixa; zero quando a classificação das contas está completa.
/// </summary>
public sealed record DemonstracaoFluxoCaixa(DateOnly Inicio, DateOnly Fim, bool Direto, IReadOnlyList<LinhaFluxo> Linhas,
                                            decimal CaixaOperacional, decimal CaixaInvestimento, decimal CaixaFinanciamento,
                                            decimal SaldoInicial, decimal SaldoFinal, decimal Diferenca)
{
    public decimal VariacaoCaixa => CaixaOperacional + CaixaInvestimento + CaixaFinanciamento;
    public bool Fecha => Diferenca == 0 && SaldoInicial + VariacaoCaixa == SaldoFinal;
}

/// <summary>Montagem da DFC pelos métodos indireto e direto a partir dos movimentos do período (sem encerramento).</summary>
public static class FluxoCaixa
{
    /// <param name="movimentos">Movimento do período por conta, sem os lançamentos de encerramento.</param>
    /// <param name="partidas">Partidas do período (só necessárias no método direto).</param>
    /// <param name="nivelMaximo">Nível do plano em que as variações são agrupadas (contas abaixo dele somam na ancestral).</param>
    public static DemonstracaoFluxoCaixa Montar(DateOnly inicio, DateOnly fim, IReadOnlyList<ContaPlano> plano, IReadOnlyList<MovimentoConta> movimentos,
                                                IReadOnlyDictionary<long, ClasseConta> classes, bool direto, IReadOnlyList<PartidaMutacao>? partidas = null,
                                                short nivelMaximo = 3)
    {
        var porId = plano.ToDictionary(c => c.Id);
        var mov = movimentos.GroupBy(m => m.ContaId).ToDictionary(g => g.Key, g => (Deb: g.Sum(m => m.Debitos), Cred: g.Sum(m => m.Creditos), Ant: g.Sum(m => m.SaldoAnterior)));
        ClasseConta? Classe(long id) => classes.TryGetValue(id, out var c) ? c : null;
        ContaPlano Grupo(ContaPlano c)
        {
            while (c.Nivel > nivelMaximo && c.ContaPaiId is { } p && porId.TryGetValue(p, out var pai)) c = pai;
            return c;
        }
        // Variação (crédito − débito) por grupo, para as contas patrimoniais de uma classe.
        List<(ContaPlano Grupo, decimal Valor)> Variacoes(ClasseConta classe) => plano
            .Where(c => c.Analitica && Classe(c.Id) == classe && mov.ContainsKey(c.Id))
            .GroupBy(c => Grupo(c).Id)
            .Select(g => (Grupo: porId[g.Key], Valor: g.Sum(c => mov[c.Id].Cred - mov[c.Id].Deb)))
            .Where(x => x.Valor != 0).OrderBy(x => x.Grupo.Codigo, StringComparer.Ordinal).ToList();

        var resultado = plano.Where(c => c.Analitica && c.CodNat == "04" && mov.ContainsKey(c.Id)).ToList();
        decimal Natural(ContaPlano c) => mov[c.Id].Cred - mov[c.Id].Deb;
        var lucro = resultado.Sum(Natural);
        var depreciacao = resultado.Where(c => Classe(c.Id) == ClasseConta.Depreciacao).Sum(c => -Natural(c));

        var saldoInicial = plano.Where(c => c.Analitica && Classe(c.Id) == ClasseConta.Caixa && mov.ContainsKey(c.Id)).Sum(c => mov[c.Id].Ant);
        var variacaoReal = plano.Where(c => c.Analitica && Classe(c.Id) == ClasseConta.Caixa && mov.ContainsKey(c.Id)).Sum(c => mov[c.Id].Deb - mov[c.Id].Cred);

        var ativosOp = Variacoes(ClasseConta.Operacional).Where(x => x.Grupo.CodNat == "01").ToList();
        var passivosOp = Variacoes(ClasseConta.Operacional).Where(x => x.Grupo.CodNat != "01").ToList();
        var investimentos = Variacoes(ClasseConta.Investimento);
        var financiamentos = Variacoes(ClasseConta.Financiamento);

        // Indireto: ativo cresce => caixa diminui; passivo cresce => caixa aumenta. (crédito − débito) já traz o sinal certo para os dois.
        var variacaoAtivos = ativosOp.Sum(x => x.Valor);
        var variacaoPassivos = passivosOp.Sum(x => x.Valor);
        var caixaOperIndireto = lucro + depreciacao + variacaoAtivos + variacaoPassivos;
        var caixaInvest = investimentos.Sum(x => x.Valor);
        var caixaFinanc = financiamentos.Sum(x => x.Valor);

        var linhas = new List<LinhaFluxo>();
        void Titulo(string chave, string d, int recuo = 0) => linhas.Add(new(chave, d, null, TipoLinhaFluxo.Titulo, recuo));
        void Item(string chave, string d, decimal v, int recuo = 1) => linhas.Add(new(chave, d, v, TipoLinhaFluxo.Item, recuo));
        void Sub(string chave, string d, decimal v) => linhas.Add(new(chave, d, v, TipoLinhaFluxo.Subtotal));

        decimal caixaOper;
        Titulo("op", "ATIVIDADES OPERACIONAIS");
        if (!direto)
        {
            caixaOper = caixaOperIndireto;
            LinhasIndireto();
        }
        else
        {
            var (lista, total) = LinhasDiretas(partidas ?? [], porId, classes, Grupo);
            foreach (var (chave, desc, v) in lista) Item("op.d." + chave, desc, v);
            caixaOper = total;
            Sub("op.total", "Caixa líquido das atividades operacionais", caixaOper);
            Titulo("op.conc", "Conciliação do lucro líquido com o caixa das atividades operacionais");
            LinhasIndireto();
        }

        void LinhasIndireto()
        {
            Item("op.lucro", direto ? "Lucro líquido do período" : "Lucro (prejuízo) líquido do período", lucro);
            if (depreciacao != 0 || resultado.Any(c => Classe(c.Id) == ClasseConta.Depreciacao))
            {
                Titulo("op.ajustes", "Ajustes por itens sem efeito no caixa", 1);
                Item("op.deprec", "Depreciação, amortização e exaustão", depreciacao, 2);
            }
            if (ativosOp.Count > 0)
            {
                Titulo("op.ativos", "(Aumento) / redução em ativos operacionais", 1);
                foreach (var (g, v) in ativosOp) Item("op.a." + g.Codigo, g.Descricao, v, 2);
            }
            if (passivosOp.Count > 0)
            {
                Titulo("op.passivos", "Aumento / (redução) em passivos operacionais", 1);
                foreach (var (g, v) in passivosOp) Item("op.p." + g.Codigo, g.Descricao, v, 2);
            }
            linhas.Add(new("op.total" + (direto ? ".ind" : ""), direto ? "Caixa líquido das atividades operacionais (conferência)" : "Caixa líquido das atividades operacionais",
                           caixaOperIndireto, TipoLinhaFluxo.Subtotal));
        }

        if (direto)
        {
            // No método direto o caixa investimento/financiamento vem das partidas (mesmo total das variações das contas).
            var (invD, finD) = (DiretoPorClasse(partidas ?? [], porId, classes, Grupo, ClasseConta.Investimento), DiretoPorClasse(partidas ?? [], porId, classes, Grupo, ClasseConta.Financiamento));
            caixaInvest = invD.Sum(x => x.Valor);
            caixaFinanc = finD.Sum(x => x.Valor);
            Titulo("inv", "ATIVIDADES DE INVESTIMENTO");
            foreach (var (g, v) in invD) Item("inv." + g.Codigo, g.Descricao, v);
            Sub("inv.total", "Caixa líquido das atividades de investimento", caixaInvest);
            Titulo("fin", "ATIVIDADES DE FINANCIAMENTO");
            foreach (var (g, v) in finD) Item("fin." + g.Codigo, g.Descricao, v);
            Sub("fin.total", "Caixa líquido das atividades de financiamento", caixaFinanc);
        }
        else
        {
            Titulo("inv", "ATIVIDADES DE INVESTIMENTO");
            foreach (var (g, v) in investimentos) Item("inv." + g.Codigo, g.Descricao, v);
            Sub("inv.total", "Caixa líquido das atividades de investimento", caixaInvest);
            Titulo("fin", "ATIVIDADES DE FINANCIAMENTO");
            foreach (var (g, v) in financiamentos) Item("fin." + g.Codigo, g.Descricao, v);
            Sub("fin.total", "Caixa líquido das atividades de financiamento", caixaFinanc);
        }

        var variacao = caixaOper + caixaInvest + caixaFinanc;
        var saldoFinal = saldoInicial + variacaoReal;
        linhas.Add(new("variacao", "AUMENTO (REDUÇÃO) LÍQUIDO DE CAIXA E EQUIVALENTES", variacao, TipoLinhaFluxo.Total));
        linhas.Add(new("inicial", "Caixa e equivalentes no início do período", saldoInicial, TipoLinhaFluxo.Item));
        linhas.Add(new("final", "Caixa e equivalentes no fim do período", saldoFinal, TipoLinhaFluxo.Total));
        return new DemonstracaoFluxoCaixa(inicio, fim, direto, linhas, caixaOper, caixaInvest, caixaFinanc, saldoInicial, saldoFinal, variacao - variacaoReal);
    }

    /// <summary>
    /// Método direto: cada lançamento que mexe no caixa reparte seu efeito pelas contrapartidas (o crédito menos o débito de cada
    /// partida que não é de caixa), classificadas pela conta da contrapartida.
    /// </summary>
    private static IEnumerable<(ContaPlano Conta, ClasseConta? Classe, decimal Valor)> Contrapartidas(IReadOnlyList<PartidaMutacao> partidas,
        IReadOnlyDictionary<long, ContaPlano> porId, IReadOnlyDictionary<long, ClasseConta> classes)
    {
        foreach (var l in partidas.Where(p => !p.Encerramento).GroupBy(p => p.LancamentoId))
        {
            bool Caixa(PartidaMutacao p) => classes.TryGetValue(p.ContaId, out var c) && c == ClasseConta.Caixa;
            if (!l.Any(Caixa)) continue;
            foreach (var p in l.Where(p => !Caixa(p) && porId.ContainsKey(p.ContaId)))
                yield return (porId[p.ContaId], classes.TryGetValue(p.ContaId, out var c) ? c : null, p.DC == IndicadorDC.C ? p.Valor : -p.Valor);
        }
    }

    private static List<(ContaPlano Grupo, decimal Valor)> DiretoPorClasse(IReadOnlyList<PartidaMutacao> partidas, IReadOnlyDictionary<long, ContaPlano> porId,
        IReadOnlyDictionary<long, ClasseConta> classes, Func<ContaPlano, ContaPlano> grupo, ClasseConta classe) =>
        Contrapartidas(partidas, porId, classes).Where(x => x.Classe == classe).GroupBy(x => grupo(x.Conta).Id)
            .Select(g => (Grupo: porId[g.Key], Valor: g.Sum(x => x.Valor))).Where(x => x.Valor != 0).OrderBy(x => x.Grupo.Codigo, StringComparer.Ordinal).ToList();

    private static (List<(string Chave, string Descricao, decimal Valor)> Linhas, decimal Total) LinhasDiretas(IReadOnlyList<PartidaMutacao> partidas,
        IReadOnlyDictionary<long, ContaPlano> porId, IReadOnlyDictionary<long, ClasseConta> classes, Func<ContaPlano, ContaPlano> grupo)
    {
        var res = new List<(string, string, decimal)>();
        var contrapartidas = Contrapartidas(partidas, porId, classes).Where(x => x.Classe is not (ClasseConta.Investimento or ClasseConta.Financiamento)).ToList();
        // Contas de resultado e itens fora da classificação: uma linha por tipo; contas patrimoniais operacionais: uma por grupo.
        var porTipo = contrapartidas.Where(x => x.Classe is null or ClasseConta.ContrapartidaAjuste || !x.Classe!.Value.EhPatrimonial() || x.Classe == ClasseConta.Operacional)
            .GroupBy(x => x.Classe switch
            {
                ClasseConta.Receita or ClasseConta.Deducao => ("1", "Recebimentos de clientes e outras receitas", 0L),
                ClasseConta.CustoDespesa or ClasseConta.Depreciacao => ("2", "Pagamentos de custos e despesas", 0L),
                ClasseConta.Financeiro => ("3", "Juros e demais resultados financeiros", 0L),
                ClasseConta.ImpostoLucro => ("4", "Imposto de renda e contribuição social", 0L),
                ClasseConta.Operacional => ("5", grupo(x.Conta).Descricao, grupo(x.Conta).Id),
                _ => ("6", "Outros recebimentos e pagamentos operacionais", 0L),
            }).OrderBy(g => g.Key.Item1).ThenBy(g => grupo(g.First().Conta).Codigo, StringComparer.Ordinal);
        foreach (var g in porTipo)
        {
            var v = g.Sum(x => x.Valor);
            if (v != 0) res.Add(($"{g.Key.Item1}.{g.Key.Item3}", g.Key.Item2, v));
        }
        return (res, res.Sum(x => x.Item3));
    }
}
