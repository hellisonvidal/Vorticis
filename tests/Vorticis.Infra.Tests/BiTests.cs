using Vorticis.Application.Contabil;
using Vorticis.Application.Relatorios;
using Vorticis.Domain.Contabil;
using Vorticis.Infra.Relatorios;

namespace Vorticis.Infra.Tests;

/// <summary>Comparativo dos relatórios gerenciais e as ferramentas de BI (painel, análise, cubo e exportação) sobre lançamentos reais.</summary>
public class BiTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private async Task Lancar(long empresa, long adm, DateOnly data, string debito, string credito, decimal valor, string? centro = null)
    {
        var r = await db.App.Lancamentos.EfetivarAsync(new LancamentoProposto(empresa, data, OrigemLancamento.Manual,
            [new(debito, IndicadorDC.D, valor, "Teste", debito.StartsWith('4') ? centro : null), new(credito, IndicadorDC.C, valor, "Teste", credito.StartsWith('3') ? centro : null)]),
            adm, TestContext.Current.CancellationToken);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
    }

    private async Task<(long Empresa, long Adm)> PrepararAsync(string cnpj)
    {
        var ct = TestContext.Current.CancellationToken;
        var empresa = await db.CriarEmpresaAsync(cnpj);
        var adm = await db.AdminAsync();
        foreach (var (cod, desc) in new[] { ("1", "Administrativo"), ("2", "Produção") })
            Assert.True((await db.App.PlanoContas.IncluirCentroAsync(empresa, cod, desc, null, adm, ct: ct)).Sucesso, cod);
        await Lancar(empresa, adm, new(2026, 1, 1), "1.1.01.001", "2.3.01.001", 20_000);
        await Lancar(empresa, adm, new(2026, 1, 10), "1.1.02.001", "3.1.01.003", 10_000, "2");
        await Lancar(empresa, adm, new(2026, 1, 25), "4.2.02.001", "1.1.01.001", 1_000, "1");
        await Lancar(empresa, adm, new(2026, 2, 12), "1.1.01.001", "3.1.01.003", 8_000, "2");
        await Lancar(empresa, adm, new(2026, 2, 20), "4.2.02.001", "1.1.01.001", 1_200, "1");
        return (empresa, adm);
    }

    private static decimal? Celula(TabelaRelatorio t, Func<LinhaRelatorio, bool> linha, string coluna) =>
        (decimal?)t.Linhas.Single(linha).Valores[t.Colunas.ToList().FindIndex(c => c.Titulo == coluna)];

    [Fact]
    public async Task Comparativo_da_receita_por_centro_mostra_receita_custos_e_margem_de_cada_periodo()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var (empresa, adm) = await PrepararAsync("44778899000143");
        var op = new OpcoesComparativo(PeriodoAnterior: true);
        var r = await db.App.Comparativos.ReceitaCentroAsync(empresa, new(2026, 2, 1), new(2026, 2, 28), null, 0, op, adm, ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        var t = r.Valor;
        Assert.Equal(["Código", "Descrição", "Medida", "fev/2026", "jan/2026"], t.Colunas.Take(5).Select(c => c.Titulo));
        Assert.Contains(t.Colunas, c => c.Titulo.StartsWith("Var. R$"));
        Assert.Contains(t.Colunas, c => c.Titulo.StartsWith("Var. %"));
        bool Prod(LinhaRelatorio l, string medida) => (string?)l.Valores[0] == "2" && (string?)l.Valores[2] == medida;
        var rec = t.Linhas.Single(l => Prod(l, "Receita")).Valores;
        Assert.Equal([8_000m, 10_000m, -2_000m, -20m], rec.Skip(3).Cast<decimal>());
        Assert.Equal([8_000m, 10_000m], t.Linhas.Single(l => Prod(l, "Margem")).Valores.Skip(3).Take(2).Cast<decimal>());
        bool Adm(LinhaRelatorio l, string medida) => (string?)l.Valores[0] == "1" && (string?)l.Valores[2] == medida;
        Assert.Equal([1_200m, 1_000m, 200m, 20m], t.Linhas.Single(l => Adm(l, "Custos e despesas")).Valores.Skip(3).Cast<decimal>());

        // sem a opção de períodos: erro claro, como nos outros relatórios
        Assert.False((await db.App.Comparativos.ReceitaCentroAsync(empresa, new(2026, 2, 1), new(2026, 2, 28), null, 0, new OpcoesComparativo(), adm, ct)).Sucesso);
    }

    [Fact]
    public async Task Comparativo_do_orcado_x_realizado_compara_orcado_e_realizado_dos_periodos()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var (empresa, adm) = await PrepararAsync("44778899000224");
        var plano = (await db.App.Consultas.ObterPlanoAsync(empresa, ct)).ToDictionary(c => c.Codigo);
        var centros = (await db.App.Consultas.ObterCentrosAsync(empresa, ct)).ToDictionary(c => c.Codigo);
        decimal[] Meses(params decimal[] v) => [.. v, .. new decimal[12 - v.Length]];
        Assert.True((await db.App.Gerenciais.SalvarOrcamentoAsync(empresa, 2026, [new(plano["4.2.02.001"].Id, centros["1"].Id, Meses(900, 1100))], adm, ct)).Sucesso);

        var r = await db.App.Comparativos.OrcadoRealizadoAsync(empresa, new(2026, 2, 1), new(2026, 2, 28), null, null, false, new OpcoesComparativo(PeriodoAnterior: true), adm, ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        var t = r.Valor;
        bool Aluguel(LinhaRelatorio l, string medida) => (string?)l.Valores[0] == "4.2.02.001" && (string?)l.Valores[2] == medida;
        Assert.Equal([1_200m, 1_000m, 200m, 20m], t.Linhas.Single(l => Aluguel(l, "Realizado")).Valores.Skip(3).Cast<decimal>());
        Assert.Equal([1_100m, 900m, 200m], t.Linhas.Single(l => Aluguel(l, "Orçado")).Valores.Skip(3).Take(3).Cast<decimal>());
        Assert.Equal(8_000m, Celula(t, l => (string?)l.Valores[1] == "Total das receitas" && (string?)l.Valores[2] == "Realizado", "fev/2026"));

        // período que cruza o ano: janeiro de 2026 contra dezembro de 2025 (sem orçamento nem lançamentos)
        var jan = await db.App.Comparativos.OrcadoRealizadoAsync(empresa, new(2026, 1, 1), new(2026, 1, 31), null, null, false, new OpcoesComparativo(PeriodoAnterior: true), adm, ct);
        Assert.True(jan.Sucesso, string.Join("; ", jan.Erros));
        Assert.Equal(1_000m, Celula(jan.Valor, l => Aluguel(l, "Realizado"), "jan/2026"));
        Assert.Equal(0m, Celula(jan.Valor, l => Aluguel(l, "Realizado"), "dez/2025"));
    }

    [Fact]
    public async Task Painel_calcula_os_indicadores_com_valores_conferidos_e_exige_permissao()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var (empresa, adm) = await PrepararAsync("44778899000305");
        var r = await db.App.Bi.PainelAsync(empresa, new(2026, 1, 1), new(2026, 2, 28), adm, ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        var p = r.Valor;
        decimal? V(string k) => p.Obter(k)!.Valor;
        Assert.Equal(18_000m, V("receita"));
        Assert.Equal(2_200m, V("despesas"));
        Assert.Equal(15_800m, V("lucro"));
        Assert.Equal(15_800m, V("ebitda"));
        Assert.Equal(87.78m, V("margem_ebitda"));
        // ativo = caixa 20.000 − 1.000 + 8.000 − 1.200 + clientes 10.000 = 35.800; sem dívidas; PL = capital 20.000 + lucro 15.800
        Assert.Equal(44.13m, V("roe"));
        Assert.Equal(44.13m, V("roa"));
        Assert.Equal(0m, V("endividamento"));
        Assert.Null(V("liq_corrente"));                  // sem passivo circulante não há como calcular
        Assert.Equal(0.5m, V("giro_ativo"));             // 18.000 ÷ 35.800 = 0,5028...
        Assert.Equal(Math.Round(10_000m / 18_000m * 59, 1), V("pmr"));   // clientes 10.000 ÷ receita bruta 18.000 × 59 dias
        Assert.Equal(12, p.Meses.Count);
        Assert.Equal(new DateOnly(2026, 2, 1), p.Meses[^1].Mes);
        Assert.Equal((8_000m, 1_200m), (p.Meses[^1].Receita, p.Meses[^1].Despesas));
        Assert.Equal((10_000m, 1_000m), (p.Meses[^2].Receita, p.Meses[^2].Despesas));
        Assert.Equal([("DESPESAS OPERACIONAIS", 2_200m)], p.DespesasPorGrupo.Select(x => (x.Rotulo, x.Valor)));
        Assert.Equal([("1 Administrativo", 2_200m)], p.CustosPorCentro.Select(x => (x.Rotulo, x.Valor)));
        Assert.Equal(p.Resultado.LucroLiquido, p.Cascata[^1].Valor);
        Assert.Equal(0m, p.ObterAnterior("receita")!.Valor);   // período anterior sem movimento

        Assert.False((await db.App.Bi.PainelAsync(empresa, new(2026, 1, 1), new(2026, 2, 28), 9_999_999, ct)).Sucesso);
        Assert.False((await db.App.Bi.PainelAsync(empresa, new(2026, 3, 1), new(2026, 2, 28), adm, ct)).Sucesso);
    }

    [Fact]
    public async Task Analise_tem_formula_interpretacao_e_compara_periodos()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var (empresa, adm) = await PrepararAsync("44778899000496");
        var r = await db.App.Bi.AnaliseAsync(empresa, new(2026, 2, 1), new(2026, 2, 28), new OpcoesComparativo(PeriodoAnterior: true), adm, ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        var t = r.Valor;
        Assert.Equal(["Indicador", "Fórmula", "Como ler", "fev/2026", "jan/2026"], t.Colunas.Take(5).Select(c => c.Titulo));
        var lucro = t.Linhas.Single(l => (string?)l.Valores[0] == "Lucro líquido");
        Assert.Equal([6_800m, 9_000m, -2_200m], lucro.Valores.Skip(3).Take(3).Cast<decimal>());
        Assert.False(string.IsNullOrWhiteSpace((string?)lucro.Valores[1]) || string.IsNullOrWhiteSpace((string?)lucro.Valores[2]));
        Assert.Contains(t.Linhas, l => (string?)l.Valores[0] == "Liquidez" && l.Destaque);
        var semComparativo = (await db.App.Bi.AnaliseAsync(empresa, new(2026, 1, 1), new(2026, 2, 28), new OpcoesComparativo(), adm, ct)).Valor;
        Assert.Equal(4, semComparativo.Colunas.Count);
    }

    [Fact]
    public async Task Cubo_soma_por_conta_e_mes_detalha_os_lancamentos_e_a_exportacao_gera_arquivos_com_as_colunas()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var (empresa, adm) = await PrepararAsync("44778899000577");
        var q = new ConsultaCubo(DimensaoCubo.Conta, DimensaoCubo.Mes, MedidaCubo.Resultado, SoResultado: true);
        var c = await db.App.Bi.CuboAsync(empresa, new(2026, 1, 1), new(2026, 2, 28), q, adm, ct);
        Assert.True(c.Sucesso, string.Join("; ", c.Erros));
        var (dados, cubo) = c.Valor;
        Assert.Equal(["01/2026", "02/2026"], cubo.Colunas.Select(x => x.Rotulo));
        Assert.Equal([10_000m, 8_000m], cubo.Linhas.Single(l => l.Chave == "3.1.01.003").Valores);
        Assert.Equal([-1_000m, -1_200m], cubo.Linhas.Single(l => l.Chave == "4.2.02.001").Valores);
        Assert.Equal(15_800m, cubo.Total);
        var det = Cubo.Detalhe(dados, q, "4.2.02.001", cubo.Colunas[1].Chave);
        Assert.Equal([1_200m], det.Select(x => x.Valor));
        Assert.Equal(new DateOnly(2026, 2, 20), det[0].Data);
        Assert.Contains("Administrativo", det[0].Centro);

        // exportação para BI
        var pasta = Path.Combine(Path.GetTempPath(), "vorticis_bi_" + Guid.NewGuid().ToString("N"));
        try
        {
            var t = await db.App.Bi.TabelasParaBiAsync(empresa, new(2026, 1, 1), new(2026, 2, 28), adm, ct);
            Assert.True(t.Sucesso, string.Join("; ", t.Erros));
            var arquivos = ExportadorBi.Gravar(pasta, t.Valor, FormatoExportacaoBi.Ambos);
            foreach (var n in new[] { "fato_partidas.csv", "dim_plano_contas.csv", "dim_centro_custo.csv", "dim_participante.csv", "dim_calendario.csv", "dados_bi.xlsx", "leia-me.txt" })
                Assert.True(File.Exists(Path.Combine(pasta, n)), n);
            Assert.Equal(7, arquivos.Count);
            var linhas = File.ReadAllLines(Path.Combine(pasta, "fato_partidas.csv"));
            Assert.Equal("id_lancamento,numero_lancamento,data,id_conta,id_centro_custo,id_participante,tipo_dc,valor,valor_assinado,historico", linhas[0].TrimStart('﻿'));
            Assert.Equal(dados.Partidas.Count + 1, linhas.Length);
            Assert.Contains(linhas, l => l.Contains(",2026-02-20,") && l.Contains(",D,1200.00,1200.00,Teste"));
            Assert.Equal(59 + 1, File.ReadAllLines(Path.Combine(pasta, "dim_calendario.csv")).Length);
            var plano = File.ReadAllLines(Path.Combine(pasta, "dim_plano_contas.csv"));
            Assert.StartsWith("id_conta,codigo,descricao,nivel,analitica,natureza_saldo,cod_nat,id_conta_pai,codigo_grupo,descricao_grupo,classe_resultado", plano[0].TrimStart('﻿'));
            Assert.Contains(plano, l => l.Contains(",3.1.01.003,") && l.EndsWith(",Receita"));
            var excel = ExportadorExcel.Ler(File.ReadAllBytes(Path.Combine(pasta, "dados_bi.xlsx")));
            Assert.Equal(dados.Partidas.Count, excel.Count);

            // somente CSV
            var so = Path.Combine(pasta, "so_csv");
            Assert.DoesNotContain(ExportadorBi.Gravar(so, t.Valor, FormatoExportacaoBi.Csv), a => a.EndsWith(".xlsx"));
        }
        finally { if (Directory.Exists(pasta)) Directory.Delete(pasta, true); }
    }
}
