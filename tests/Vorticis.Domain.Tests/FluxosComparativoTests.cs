using Vorticis.Application.Relatorios;
using Vorticis.Domain.Contabil;

namespace Vorticis.Domain.Tests;

public class FluxosComparativoTests
{
    private static readonly ContaPlano[] Plano =
    [
        new(1, "1", "ATIVO", 1, false, null, IndicadorDC.D, "01"),
        new(2, "1.1", "Caixa", 2, true, 1, IndicadorDC.D, "01"),
        new(3, "1.2", "Clientes", 2, true, 1, IndicadorDC.D, "01"),
        new(4, "1.3", "Estoques", 2, true, 1, IndicadorDC.D, "01"),
        new(5, "1.4", "Imobilizado", 2, false, 1, IndicadorDC.D, "01"),
        new(6, "1.4.1", "Veículos", 3, true, 5, IndicadorDC.D, "01"),
        new(7, "1.4.2", "(-) Depreciação acumulada", 3, true, 5, IndicadorDC.C, "01"),
        new(8, "2", "PASSIVO", 1, false, null, IndicadorDC.C, "02"),
        new(9, "2.1", "Fornecedores", 2, true, 8, IndicadorDC.C, "02"),
        new(10, "2.2", "Empréstimos bancários", 2, true, 8, IndicadorDC.C, "02"),
        new(11, "3", "PATRIMÔNIO LÍQUIDO", 1, false, null, IndicadorDC.C, "03"),
        new(12, "3.1", "Capital social", 2, true, 11, IndicadorDC.C, "03"),
        new(13, "4", "RECEITAS", 1, false, null, IndicadorDC.C, "04"),
        new(14, "4.1", "Vendas de mercadorias", 2, true, 13, IndicadorDC.C, "04"),
        new(15, "4.2", "Deduções da receita", 2, false, 13, IndicadorDC.D, "04"),
        new(16, "4.2.1", "Impostos sobre vendas", 3, true, 15, IndicadorDC.D, "04"),
        new(17, "5", "DESPESAS", 1, false, null, IndicadorDC.D, "04"),
        new(18, "5.1", "Salários", 2, true, 17, IndicadorDC.D, "04"),
        new(19, "5.2", "Depreciação", 2, true, 17, IndicadorDC.D, "04"),
        new(20, "5.3", "Juros pagos", 2, true, 17, IndicadorDC.D, "04"),
        new(21, "5.4", "IRPJ e CSLL", 2, true, 17, IndicadorDC.D, "04"),
        new(22, "5.5", "Multa rescisória", 2, true, 17, IndicadorDC.D, "04"),
    ];

    // Lucro 3.900 (10.000 − 1.000 − 4.000 − 500 − 200 − 300 − 100); caixa +2.000 (de 1.000 para 3.000).
    private static readonly MovimentoConta[] Movimentos =
    [
        new(2, 1_000m, 5_000m, 3_000m), new(3, 0, 2_000m, 0), new(4, 0, 1_000m, 0), new(6, 0, 3_000m, 0), new(7, 0, 0, 500m),
        new(9, 0, 0, 600m), new(10, 0, 0, 2_000m), new(12, 0, 0, 1_000m),
        new(14, 0, 0, 10_000m), new(16, 0, 1_000m, 0), new(18, 0, 4_000m, 0), new(19, 0, 500m, 0), new(20, 0, 200m, 0), new(21, 0, 300m, 0), new(22, 0, 100m, 0),
    ];

    private static IReadOnlyDictionary<long, ClasseConta> Classes() => SugestaoClasse.Sugerir(Plano);

    [Fact]
    public void Sugestao_classifica_pela_natureza_e_pelo_nome()
    {
        var c = Classes();
        Assert.Equal(ClasseConta.Caixa, c[2]);
        Assert.Equal(ClasseConta.Operacional, c[3]);
        Assert.Equal(ClasseConta.Investimento, c[6]);
        Assert.Equal(ClasseConta.ContrapartidaAjuste, c[7]);
        Assert.Equal(ClasseConta.Operacional, c[9]);
        Assert.Equal(ClasseConta.Financiamento, c[10]);
        Assert.Equal(ClasseConta.Financiamento, c[12]);
        Assert.Equal(ClasseConta.Receita, c[14]);
        Assert.Equal(ClasseConta.Deducao, c[16]);
        Assert.Equal(ClasseConta.CustoDespesa, c[18]);
        Assert.Equal(ClasseConta.Depreciacao, c[19]);
        Assert.Equal(ClasseConta.Financeiro, c[20]);
        Assert.Equal(ClasseConta.ImpostoLucro, c[21]);
        Assert.False(c.ContainsKey(1));   // contas sintéticas não são classificadas
    }

    [Fact]
    public void Classificacao_gravada_vence_a_sugestao_e_so_vale_para_o_tipo_certo()
    {
        var efetivas = SugestaoClasse.Efetivas(Plano, [new(3, ClasseConta.Investimento), new(18, ClasseConta.Investimento)]);
        Assert.Equal(ClasseConta.Investimento, efetivas[3]);
        Assert.Equal(ClasseConta.CustoDespesa, efetivas[18]);   // classe patrimonial numa conta de resultado é ignorada
    }

    [Fact]
    public void Dfc_indireta_fecha_com_a_variacao_de_caixa()
    {
        var d = FluxoCaixa.Montar(new(2026, 1, 1), new(2026, 12, 31), Plano, Movimentos, Classes(), direto: false);
        Assert.Equal(2_000m, d.CaixaOperacional);          // 3.900 + 500 − 3.000 + 600
        Assert.Equal(-3_000m, d.CaixaInvestimento);
        Assert.Equal(3_000m, d.CaixaFinanciamento);        // empréstimo 2.000 + capital 1.000
        Assert.Equal(1_000m, d.SaldoInicial);
        Assert.Equal(3_000m, d.SaldoFinal);
        Assert.Equal(0m, d.Diferenca);
        Assert.True(d.Fecha);
        Assert.Equal(3_900m, d.Linhas.Single(l => l.Chave == "op.lucro").Valor);
        Assert.Equal(500m, d.Linhas.Single(l => l.Chave == "op.deprec").Valor);
        Assert.Equal(-2_000m, d.Linhas.Single(l => l.Chave == "op.a.1.2").Valor);   // clientes
        Assert.Equal(600m, d.Linhas.Single(l => l.Chave == "op.p.2.1").Valor);
        Assert.Equal(-3_000m, d.Linhas.Single(l => l.Chave == "inv.1.4.1").Valor);
    }

    [Fact]
    public void Dfc_acusa_diferenca_quando_a_classificacao_esta_incompleta()
    {
        var classes = new Dictionary<long, ClasseConta>(Classes());
        classes.Remove(3);   // clientes sem classe: a variação de 2.000 não entra em nenhuma atividade
        var d = FluxoCaixa.Montar(new(2026, 1, 1), new(2026, 12, 31), Plano, Movimentos, classes, direto: false);
        Assert.Equal(2_000m, d.Diferenca);
        Assert.False(d.Fecha);
    }

    [Fact]
    public void Dfc_direta_tem_o_mesmo_caixa_do_indireto()
    {
        MovimentoConta[] mov = [new(2, 0, 5_000m, 2_500m), new(14, 0, 0, 3_000m), new(18, 0, 1_000m, 0), new(6, 0, 1_500m, 0), new(10, 0, 0, 2_000m)];
        PartidaMutacao[] partidas =
        [
            new(1, 2, IndicadorDC.D, 3_000m, false), new(1, 14, IndicadorDC.C, 3_000m, false),
            new(2, 18, IndicadorDC.D, 1_000m, false), new(2, 2, IndicadorDC.C, 1_000m, false),
            new(3, 2, IndicadorDC.D, 2_000m, false), new(3, 10, IndicadorDC.C, 2_000m, false),
            new(4, 6, IndicadorDC.D, 1_500m, false), new(4, 2, IndicadorDC.C, 1_500m, false),
            new(5, 2, IndicadorDC.D, 9_999m, true), new(5, 12, IndicadorDC.C, 9_999m, true),   // encerramento não conta
        ];
        var direta = FluxoCaixa.Montar(new(2026, 1, 1), new(2026, 12, 31), Plano, mov, Classes(), direto: true, partidas);
        var indireta = FluxoCaixa.Montar(new(2026, 1, 1), new(2026, 12, 31), Plano, mov, Classes(), direto: false);
        Assert.Equal(2_000m, direta.CaixaOperacional);
        Assert.Equal(-1_500m, direta.CaixaInvestimento);
        Assert.Equal(2_000m, direta.CaixaFinanciamento);
        Assert.Equal(indireta.CaixaOperacional, direta.CaixaOperacional);
        Assert.Equal(2_500m, direta.VariacaoCaixa);
        Assert.True(direta.Fecha);
        Assert.Equal(3_000m, direta.Linhas.Single(l => l.Descricao.StartsWith("Recebimentos de clientes")).Valor);
        Assert.Equal(-1_000m, direta.Linhas.Single(l => l.Descricao.StartsWith("Pagamentos de custos")).Valor);
    }

    [Fact]
    public void Ebitda_parte_da_receita_liquida_e_confere_com_a_dre()
    {
        var e = CalculoEbitda.Montar(new(2026, 1, 1), new(2026, 12, 31), Plano, Movimentos, Classes(), new HashSet<long> { 22 });
        Assert.Equal(10_000m, e.ReceitaBruta);
        Assert.Equal(-1_000m, e.Deducoes);
        Assert.Equal(9_000m, e.ReceitaLiquida);
        Assert.Equal(-4_100m, e.CustosDespesas);
        Assert.Equal(4_400m, e.LucroOperacional);
        Assert.Equal(4_900m, e.Ebitda);
        Assert.Equal(100m, e.NaoRecorrentes);            // a multa volta ao EBITDA
        Assert.Equal(5_000m, e.EbitdaAjustado);
        Assert.Equal(54.44m, e.MargemEbitda);
        Assert.Equal(55.56m, e.MargemAjustada);
        Assert.Equal(-200m, e.ResultadoFinanceiro);
        Assert.Equal(-300m, e.ImpostoLucro);
        Assert.Equal(3_900m, e.LucroLiquido);
        Assert.True(e.ConfereDre);
        Assert.Contains(e.Linhas, l => l.Chave == "nr.5.5" && l.Valor == 100m);
    }

    [Fact]
    public void Ebitda_sem_itens_marcados_nao_ajusta()
    {
        var e = CalculoEbitda.Montar(new(2026, 1, 1), new(2026, 12, 31), Plano, Movimentos, Classes(), new HashSet<long>());
        Assert.Equal(e.Ebitda, e.EbitdaAjustado);
        Assert.Equal(0m, e.NaoRecorrentes);
    }

    // ---- Comparativo ------------------------------------------------------------------------------------------------

    [Fact]
    public void Periodos_anterior_e_ano_anterior_respeitam_mes_trimestre_e_ano()
    {
        var op = new OpcoesComparativo(PeriodoAnterior: true, MesmoPeriodoAnoAnterior: true);
        var mes = Comparativo.Periodos(new(2026, 3, 1), new(2026, 3, 31), op);
        Assert.Equal(["mar/2026", "fev/2026", "mar/2025"], mes.Select(p => p.Rotulo));
        Assert.Equal(new DateOnly(2026, 2, 28), mes[1].Fim);

        var tri = Comparativo.Periodos(new(2026, 4, 1), new(2026, 6, 30), op);
        Assert.Equal(["2º tri/2026", "1º tri/2026", "2º tri/2025"], tri.Select(p => p.Rotulo));

        var ano = Comparativo.Periodos(new(2026, 1, 1), new(2026, 12, 31), op);
        Assert.Equal(["2026", "2025"], ano.Select(p => p.Rotulo));   // o ano anterior é o mesmo período: sem repetir

        var livre = Comparativo.Periodos(new(2026, 3, 10), new(2026, 3, 20), new OpcoesComparativo(PeriodoAnterior: true));
        Assert.Equal(new DateOnly(2026, 2, 27), livre[1].Inicio);    // 11 dias antes
        Assert.Equal(new DateOnly(2026, 3, 9), livre[1].Fim);
    }

    [Fact]
    public void Periodos_ano_anterior_acompanha_fim_de_fevereiro_e_balanco_usa_so_a_data()
    {
        var p = Comparativo.Periodos(new(2025, 2, 1), new(2025, 2, 28), new OpcoesComparativo(MesmoPeriodoAnoAnterior: true));
        Assert.Equal(new DateOnly(2024, 2, 29), p[1].Fim);

        var b = Comparativo.Periodos(new(2026, 1, 1), new(2026, 12, 31), new OpcoesComparativo(PeriodoAnterior: true, MesmoPeriodoAnoAnterior: true), pontual: true);
        Assert.Equal([new DateOnly(2026, 12, 31), new DateOnly(2026, 11, 30), new DateOnly(2025, 12, 31)], b.Select(x => x.Fim));
    }

    [Fact]
    public void Periodos_escolhidos_mes_trimestre_ano_e_intervalo()
    {
        var op = new OpcoesComparativo(Escolhidos:
        [
            new(TipoPeriodoComparado.Mes, new(2025, 7, 15)), new(TipoPeriodoComparado.Trimestre, new(2025, 11, 2)),
            new(TipoPeriodoComparado.Ano, new(2024, 5, 5)), new(TipoPeriodoComparado.Intervalo, new(2025, 1, 10), new(2025, 2, 5)),
        ]);
        Assert.NotNull(Comparativo.Validar(op));   // no máximo 3
        var tres = op with { Escolhidos = op.Periodos.Take(3).ToList() };
        Assert.Null(Comparativo.Validar(tres));
        var p = Comparativo.Periodos(new(2026, 1, 1), new(2026, 12, 31), tres);
        Assert.Equal(["2026", "jul/2025", "4º tri/2025", "2024"], p.Select(x => x.Rotulo));
        Assert.NotNull(Comparativo.Validar(new OpcoesComparativo()));   // nada escolhido
        Assert.NotNull(Comparativo.Validar(new OpcoesComparativo(Escolhidos: [new(TipoPeriodoComparado.Intervalo, new(2025, 5, 1), new(2025, 4, 1))])));
    }

    private static LinhaComparavel L(string chave, string texto, decimal valor, bool percentual = false) => new(chave, [texto], valor, Percentual: percentual);

    private static DadosPeriodo Dados(string rotulo, params LinhaComparavel[] linhas) =>
        new(new PeriodoRelatorio(rotulo, new(2026, 1, 1), new(2026, 12, 31)), linhas, linhas.FirstOrDefault(l => l.Chave == "v")?.Valor);

    [Fact]
    public void Tabela_comparativa_calcula_variacao_absoluta_percentual_e_vertical()
    {
        var atual = Dados("2026", L("v", "Vendas", 1_200m), L("d", "Despesas", -500m), L("n", "Só no atual", 50m));
        var anterior = Dados("2025", L("v", "Vendas", 1_000m), L("d", "Despesas", -400m), L("x", "Só no anterior", 70m));
        var t = Comparativo.Montar("Teste", [new("Descrição")], [atual, anterior], new OpcoesComparativo(PeriodoAnterior: true, AnaliseVertical: true));

        Assert.Equal(["Descrição", "2026", "AV %", "2025", "AV %", "Var. R$ × 2025", "Var. % × 2025"], t.Colunas.Select(c => c.Titulo));
        var vendas = t.Linhas.Single(l => (string?)l.Valores[0] == "Vendas").Valores;
        Assert.Equal([1_200m, 100m, 1_000m, 100m, 200m, 20m], vendas.Skip(1).Cast<decimal>());
        var desp = t.Linhas.Single(l => (string?)l.Valores[0] == "Despesas").Valores;
        Assert.Equal(-500m, desp[1]);
        Assert.Equal(-41.67m, desp[2]);                  // −500 / 1.200
        Assert.Equal(-100m, desp[5]);                    // −500 − (−400)
        Assert.Equal(-25m, desp[6]);                     // −100 / 400
        var soAtual = t.Linhas.Single(l => (string?)l.Valores[0] == "Só no atual").Valores;
        Assert.Equal(0m, soAtual[3]);                    // ausente no comparado = zero
        Assert.Null(soAtual[6]);                         // sem base para o percentual
        var soAnterior = t.Linhas.Single(l => (string?)l.Valores[0] == "Só no anterior").Valores;
        Assert.Equal(0m, soAnterior[1]);
        Assert.Equal(-70m, soAnterior[5]);
        Assert.Equal(-100m, soAnterior[6]);
    }

    [Fact]
    public void Tabela_comparativa_com_tres_periodos_e_colunas_opcionais()
    {
        var p1 = Dados("2026", L("v", "Vendas", 300m));
        var p2 = Dados("2025", L("v", "Vendas", 200m));
        var p3 = Dados("2024", L("v", "Vendas", 400m));
        var t = Comparativo.Montar("T", [new("Descrição")], [p1, p2, p3], new OpcoesComparativo(PeriodoAnterior: true, VariacaoAbsoluta: false));
        Assert.Equal(["Descrição", "2026", "2025", "Var. % × 2025", "2024", "Var. % × 2024"], t.Colunas.Select(c => c.Titulo));
        Assert.Equal([300m, 200m, 50m, 400m, -25m], t.Linhas[0].Valores.Skip(1).Cast<decimal>());
    }

    [Fact]
    public void Linha_de_percentual_nao_tem_variacao_percentual_nem_vertical()
    {
        var a = Dados("2026", L("v", "Receita", 1_000m), L("m", "Margem", 40m, true));
        var b = Dados("2025", L("v", "Receita", 1_000m), L("m", "Margem", 30m, true));
        var t = Comparativo.Montar("T", [new("Descrição")], [a, b], new OpcoesComparativo(PeriodoAnterior: true, AnaliseVertical: true));
        var m = t.Linhas.Single(l => (string?)l.Valores[0] == "Margem").Valores;
        Assert.Null(m[2]);             // sem AV
        Assert.Equal(10m, m[5]);       // pontos percentuais
        Assert.Null(m[6]);
    }

    [Fact]
    public void Chave_sem_datas_junta_linhas_de_saldo_de_periodos_diferentes()
    {
        Assert.Equal(Comparativo.ChaveSemDatas("Saldo em 31/12/2025"), Comparativo.ChaveSemDatas("Saldo em 31/12/2026"));
    }
}
