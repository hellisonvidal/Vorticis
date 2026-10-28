using Vorticis.Application.Contabil;
using Vorticis.Application.Relatorios;
using Vorticis.Domain.Contabil;

namespace Vorticis.Infra.Tests;

/// <summary>DFC, EBITDA, classificação das contas e comparativo sobre lançamentos reais no banco.</summary>
public class FluxosEbitdaComparativoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private async Task Lancar(long empresa, long adm, DateOnly data, string debito, string credito, decimal valor)
    {
        var r = await db.App.Lancamentos.EfetivarAsync(new LancamentoProposto(empresa, data, OrigemLancamento.Manual,
            [new(debito, IndicadorDC.D, valor, "Teste"), new(credito, IndicadorDC.C, valor, "Teste")]), adm, TestContext.Current.CancellationToken);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
    }

    private async Task<(long Empresa, long Adm)> PrepararAsync(string cnpj = "11444777000161")
    {
        var empresa = await db.CriarEmpresaAsync(cnpj);
        var adm = await db.AdminAsync();
        const string caixa = "1.1.01.001", banco = "1.1.01.002", clientes = "1.1.02.001", vendas = "3.1.01.001";
        await Lancar(empresa, adm, new(2026, 1, 1), caixa, "2.3.01.001", 10_000);          // capital
        await Lancar(empresa, adm, new(2026, 1, 10), clientes, vendas, 6_000);             // venda a prazo
        await Lancar(empresa, adm, new(2026, 1, 20), banco, clientes, 4_000);              // recebimento
        await Lancar(empresa, adm, new(2026, 1, 25), "4.2.01.001", caixa, 1_500);          // salários pagos
        await Lancar(empresa, adm, new(2026, 1, 28), "1.2.03.003", caixa, 3_000);          // veículo
        await Lancar(empresa, adm, new(2026, 1, 31), "4.2.02.007", "1.2.03.007", 200);     // depreciação
        await Lancar(empresa, adm, new(2026, 2, 5), banco, "2.1.05.001", 5_000);           // empréstimo
        await Lancar(empresa, adm, new(2026, 2, 10), "1.1.04.001", "2.1.01.001", 2_000);   // estoque a prazo
        await Lancar(empresa, adm, new(2026, 2, 12), caixa, vendas, 2_500);                // venda à vista
        await Lancar(empresa, adm, new(2026, 2, 15), "4.3.01.001", banco, 100);            // juros
        return (empresa, adm);
    }

    [Fact]
    public async Task Dfc_fecha_com_a_variacao_de_caixa_do_balanco_e_ebitda_confere_com_a_dre()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var (empresa, adm) = await PrepararAsync();
        var f = db.App.Fluxos;

        var r = await f.DfcAsync(empresa, new(2026, 1, 1), new(2026, 2, 28), false, 3, adm, ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        var d = r.Valor.Demonstracao;
        Assert.Equal(4_900m, d.CaixaOperacional);          // 6.700 + 200 − 2.000 − 2.000 + 2.000
        Assert.Equal(-3_000m, d.CaixaInvestimento);
        Assert.Equal(15_000m, d.CaixaFinanciamento);
        Assert.Equal(0m, d.SaldoInicial);
        Assert.Equal(16_900m, d.SaldoFinal);
        Assert.True(d.Fecha);
        Assert.True(r.Valor.ConfereBalanco);                // caixa do Balanço: 16.900
        Assert.Equal(16_900m, r.Valor.CaixaBalancoFim);

        // O mesmo caixa no método direto.
        var direta = (await f.DfcAsync(empresa, new(2026, 1, 1), new(2026, 2, 28), true, 3, adm, ct)).Valor;
        Assert.Equal(4_900m, direta.Demonstracao.CaixaOperacional);
        Assert.True(direta.Demonstracao.Fecha && direta.ConfereBalanco);

        // Fevereiro isolado: o saldo inicial é o caixa de 31/01 (9.500).
        var fev = (await f.DfcAsync(empresa, new(2026, 2, 1), new(2026, 2, 28), false, 3, adm, ct)).Valor;
        Assert.Equal(9_500m, fev.Demonstracao.SaldoInicial);
        Assert.Equal(16_900m, fev.Demonstracao.SaldoFinal);
        Assert.True(fev.Demonstracao.Fecha && fev.ConfereBalanco);

        var e = (await f.EbitdaAsync(empresa, new(2026, 1, 1), new(2026, 2, 28), adm, ct)).Valor;
        Assert.Equal(8_500m, e.ReceitaLiquida);
        Assert.Equal(6_800m, e.LucroOperacional);
        Assert.Equal(7_000m, e.Ebitda);
        Assert.Equal(82.35m, e.MargemEbitda);
        Assert.Equal(-100m, e.ResultadoFinanceiro);
        Assert.Equal(6_700m, e.LucroLiquido);
        Assert.True(e.ConfereDre);

        var mensal = (await f.EbitdaMensalAsync(empresa, 2026, adm, ct)).Valor;
        Assert.Equal(13, mensal.Count);
        Assert.Equal((6_000m, 4_500m), (mensal[0].Demonstracao.ReceitaLiquida, mensal[0].Demonstracao.Ebitda));
        Assert.Equal((2_500m, 2_500m), (mensal[1].Demonstracao.ReceitaLiquida, mensal[1].Demonstracao.Ebitda));
        Assert.Equal(7_000m, mensal[^1].Demonstracao.Ebitda);
        Assert.Equal(2, TabelasFluxos.EbitdaMensal(mensal).Colunas.Count(c => c.Titulo is "jan/2026" or "fev/2026"));

        var centros = (await f.EbitdaPorCentroAsync(empresa, new(2026, 1, 1), new(2026, 2, 28), adm, ct)).Valor;
        Assert.Equal(["Sem centro de custo", "Total"], centros.Select(c => c.Rotulo));
        Assert.Equal(7_000m, centros[^1].Demonstracao.Ebitda);

        // A DFC também pode compor o Livro Contábil Completo.
        var livro = await db.App.Livros.LivroAsync(empresa, new(2026, 1, 1), new(2026, 2, 28), new DadosTermos(1, "", "", "", ""),
            new OpcoesLivro(false, false, false, false, false, false, false, false, false, Dfc: true), adm, ct);
        Assert.True(livro.Sucesso, string.Join("; ", livro.Erros));
        Assert.True(livro.Valor.Paginas >= 1);
        Assert.Equal("DFC", new OpcoesLivro(false, false, false, false, false, false, false, false, false, Dfc: true).Descricao());
    }

    [Fact]
    public async Task Classificacao_personalizada_muda_a_dfc_e_o_ebitda_ajustado()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await db.CriarEmpresaAsync("11222333000181");
        var adm = await db.AdminAsync();
        await Lancar(empresa, adm, new(2026, 1, 1), "1.1.01.001", "2.3.01.001", 5_000);
        await Lancar(empresa, adm, new(2026, 1, 5), "1.1.01.001", "3.1.01.001", 3_000);
        await Lancar(empresa, adm, new(2026, 1, 6), "1.1.01.001", "3.3.02.001", 400);   // ganho eventual
        var f = db.App.Fluxos;

        var lista = (await f.ListarClassificacaoAsync(empresa, adm, ct)).Valor;
        var capital = lista.Single(l => l.Codigo == "2.3.01.001");
        Assert.Equal((ClasseConta.Financiamento, false), (capital.Classe, capital.Personalizada));
        var ganho = lista.Single(l => l.Codigo == "3.3.02.001");
        Assert.False(ganho.Patrimonial);

        // Erros: classe patrimonial em conta de resultado e não recorrente em conta patrimonial.
        Assert.False((await f.SalvarClassificacaoAsync(empresa, [new(ganho.ContaId, ClasseConta.Operacional)], adm, ct)).Sucesso);
        Assert.False((await f.SalvarClassificacaoAsync(empresa, [new(capital.ContaId, ClasseConta.Financiamento, true)], adm, ct)).Sucesso);

        var antes = (await f.EbitdaAsync(empresa, new(2026, 1, 1), new(2026, 1, 31), adm, ct)).Valor;
        Assert.Equal(3_400m, antes.Ebitda);
        Assert.Equal(antes.Ebitda, antes.EbitdaAjustado);

        // O ganho eventual é item não recorrente; o capital passa a ser tratado como operacional (muda a DFC).
        var salvo = await f.SalvarClassificacaoAsync(empresa, [new(ganho.ContaId, ClasseConta.CustoDespesa, true), new(capital.ContaId, ClasseConta.Operacional)], adm, ct);
        Assert.True(salvo.Sucesso, string.Join("; ", salvo.Erros));
        var depois = (await f.EbitdaAsync(empresa, new(2026, 1, 1), new(2026, 1, 31), adm, ct)).Valor;
        Assert.Equal(3_400m, depois.Ebitda);
        Assert.Equal(-400m, depois.NaoRecorrentes);
        Assert.Equal(3_000m, depois.EbitdaAjustado);
        var dfc = (await f.DfcAsync(empresa, new(2026, 1, 1), new(2026, 1, 31), false, 3, adm, ct)).Valor.Demonstracao;
        Assert.Equal(8_400m, dfc.CaixaOperacional);        // 3.400 do lucro + 5.000 de capital agora operacional
        Assert.Equal(0m, dfc.CaixaFinanciamento);
        Assert.True(dfc.Fecha);

        var relido = (await f.ListarClassificacaoAsync(empresa, adm, ct)).Valor;
        Assert.True(relido.Single(l => l.Codigo == "2.3.01.001").Personalizada);
        Assert.True(relido.Single(l => l.Codigo == "3.3.02.001").NaoRecorrente);
    }

    [Fact]
    public async Task Comparativo_dos_relatorios_contabeis_traz_periodos_variacao_e_percentual()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var (empresa, adm) = await PrepararAsync("98765432000198");
        var c = db.App.Comparativos;
        var op = new OpcoesComparativo(PeriodoAnterior: true, AnaliseVertical: true);

        var dre = await c.DreAsync(empresa, new(2026, 2, 1), new(2026, 2, 28), 4, op, adm, ct);
        Assert.True(dre.Sucesso, string.Join("; ", dre.Erros));
        Assert.Equal(["Código", "Descrição", "fev/2026", "AV %", "jan/2026", "AV %", "Var. R$ × jan/2026", "Var. % × jan/2026"], dre.Valor.Colunas.Select(x => x.Titulo));
        var vendas = dre.Valor.Linhas.Single(l => (string?)l.Valores[0] == "3.1.01.001").Valores;
        Assert.Equal(2_500m, vendas[2]);
        Assert.Equal(6_000m, vendas[4]);
        Assert.Equal(-3_500m, vendas[6]);
        Assert.Equal(-58.33m, vendas[7]);

        var balanco = await c.BalancoAsync(empresa, new(2026, 2, 28), 4, new OpcoesComparativo(PeriodoAnterior: true, MesmoPeriodoAnoAnterior: true), adm, ct);
        Assert.True(balanco.Sucesso, string.Join("; ", balanco.Erros));
        Assert.Equal(["Código", "Descrição", "28/02/2026", "31/01/2026", "Var. R$ × 31/01/2026", "Var. % × 31/01/2026", "28/02/2025", "Var. R$ × 28/02/2025", "Var. % × 28/02/2025"],
                     balanco.Valor.Colunas.Select(x => x.Titulo));
        var totalAtivo = balanco.Valor.Linhas.Single(l => (string?)l.Valores[1] == "TOTAL DO ATIVO").Valores;
        Assert.Equal(23_700m, totalAtivo[2]);      // caixa 16.900 + clientes 2.000 + estoques 2.000 + veículos 3.000 − depreciação 200
        Assert.Equal(14_300m, totalAtivo[3]);      // em 31/01: caixa 9.500 + clientes 2.000 + veículos 3.000 − depreciação 200
        Assert.Equal(9_400m, totalAtivo[4]);
        Assert.Equal(65.73m, totalAtivo[5]);
        Assert.Equal(0m, totalAtivo[6]);           // 28/02/2025: empresa sem movimento
    }
}
