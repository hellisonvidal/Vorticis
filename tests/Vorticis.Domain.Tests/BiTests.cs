using Vorticis.Application.Relatorios;
using Vorticis.Domain.Contabil;

namespace Vorticis.Domain.Tests;

public class BiTests
{
    private static ContaPlano C(long id, string cod, string desc, short nivel, bool analitica, long? pai, IndicadorDC nat, string codNat) => new(id, cod, desc, nivel, analitica, pai, nat, codNat);

    private static readonly IReadOnlyList<ContaPlano> Plano =
    [
        C(1, "1", "ATIVO", 1, false, null, IndicadorDC.D, "01"),
        C(2, "1.1", "ATIVO CIRCULANTE", 2, false, 1, IndicadorDC.D, "01"),
        C(3, "1.1.01", "DISPONÍVEL", 3, false, 2, IndicadorDC.D, "01"),
        C(4, "1.1.01.001", "Caixa Geral", 4, true, 3, IndicadorDC.D, "01"),
        C(5, "1.1.02", "CRÉDITOS", 3, false, 2, IndicadorDC.D, "01"),
        C(6, "1.1.02.001", "Clientes", 4, true, 5, IndicadorDC.D, "01"),
        C(7, "1.1.04", "ESTOQUES", 3, false, 2, IndicadorDC.D, "01"),
        C(8, "1.1.04.001", "Mercadorias para Revenda", 4, true, 7, IndicadorDC.D, "01"),
        C(9, "1.2", "ATIVO NÃO CIRCULANTE", 2, false, 1, IndicadorDC.D, "01"),
        C(10, "1.2.01", "REALIZÁVEL A LONGO PRAZO", 3, false, 9, IndicadorDC.D, "01"),
        C(11, "1.2.01.001", "Depósitos Judiciais", 4, true, 10, IndicadorDC.D, "01"),
        C(12, "1.2.03", "IMOBILIZADO", 3, false, 9, IndicadorDC.D, "01"),
        C(13, "1.2.03.003", "Veículos", 4, true, 12, IndicadorDC.D, "01"),
        C(20, "2", "PASSIVO E PATRIMÔNIO LÍQUIDO", 1, false, null, IndicadorDC.C, "02"),
        C(21, "2.1", "PASSIVO CIRCULANTE", 2, false, 20, IndicadorDC.C, "02"),
        C(22, "2.1.01.001", "Fornecedores Nacionais", 4, true, 21, IndicadorDC.C, "02"),
        C(23, "2.1.02.001", "Empréstimos de curto prazo", 4, true, 21, IndicadorDC.C, "02"),
        C(24, "2.2", "PASSIVO NÃO CIRCULANTE", 2, false, 20, IndicadorDC.C, "02"),
        C(25, "2.2.01.001", "Financiamentos", 4, true, 24, IndicadorDC.C, "02"),
        C(26, "2.3", "PATRIMÔNIO LÍQUIDO", 2, false, 20, IndicadorDC.C, "03"),
        C(27, "2.3.01.001", "Capital Social", 4, true, 26, IndicadorDC.C, "03"),
        C(30, "3", "RECEITAS", 1, false, null, IndicadorDC.C, "04"),
        C(31, "3.1", "RECEITA OPERACIONAL BRUTA", 2, false, 30, IndicadorDC.C, "04"),
        C(32, "3.1.01.001", "Venda de mercadorias", 4, true, 31, IndicadorDC.C, "04"),
        C(40, "4", "CUSTOS E DESPESAS", 1, false, null, IndicadorDC.D, "04"),
        C(41, "4.2", "DESPESAS OPERACIONAIS", 2, false, 40, IndicadorDC.D, "04"),
        C(42, "4.2.01.001", "Salários", 4, true, 41, IndicadorDC.D, "04"),
        C(43, "4.2.02.001", "Aluguéis", 4, true, 41, IndicadorDC.D, "04"),
    ];

    private static DemonstracaoEbitda Resultado() =>
        new(new(2026, 1, 1), new(2026, 3, 31), [], 30_000m, -3_000m, 27_000m, -15_000m, -2_000m, 10_000m, 12_000m, 0m, 12_000m, 0m, 0m, 10_000m);

    [Fact]
    public void Posicao_reconhece_circulante_estoques_caixa_clientes_e_inclui_o_resultado_nao_encerrado_no_PL()
    {
        var saldos = new Dictionary<long, decimal>
        {
            [4] = 10_000, [6] = 20_000, [8] = 30_000, [11] = 5_000, [13] = 35_000,
            [22] = -15_000, [23] = -5_000, [25] = -30_000, [27] = -40_000, [32] = -30_000, [42] = 20_000,
        };
        var p = CalculoBi.Posicao(Plano, saldos);
        Assert.Equal((10_000m, 20_000m, 30_000m, 60_000m), (p.Disponivel, p.Clientes, p.Estoques, p.AtivoCirculante));
        Assert.Equal((5_000m, 40_000m, 100_000m), (p.RealizavelLongoPrazo, p.AtivoNaoCirculante, p.AtivoTotal));
        Assert.Equal((15_000m, 20_000m, 30_000m, 50_000m), (p.Fornecedores, p.PassivoCirculante, p.PassivoNaoCirculante, p.PatrimonioLiquido));
        Assert.Equal(p.AtivoTotal, p.Dividas + p.PatrimonioLiquido);
    }

    [Fact]
    public void Indicadores_batem_com_as_formulas()
    {
        var p = new PosicaoPatrimonial(10_000, 20_000, 30_000, 60_000, 5_000, 40_000, 15_000, 20_000, 30_000, 50_000);
        var i = CalculoBi.Indicadores(p, Resultado(), 90).ToDictionary(x => x.Chave, x => x.Valor);
        Assert.Equal(27_000m, i["receita"]);
        Assert.Equal(17_000m, i["despesas"]);
        Assert.Equal(10_000m, i["lucro"]);
        Assert.Equal(12_000m, i["ebitda"]);
        Assert.Equal(44.44m, i["margem_ebitda"]);
        Assert.Equal(37.04m, i["margem_liquida"]);
        Assert.Equal(3.00m, i["liq_corrente"]);
        Assert.Equal(1.50m, i["liq_seca"]);
        Assert.Equal(0.50m, i["liq_imediata"]);
        Assert.Equal(1.30m, i["liq_geral"]);
        Assert.Equal(50m, i["endividamento"]);
        Assert.Equal(40m, i["composicao_divida"]);
        Assert.Equal(100m, i["capital_terceiros"]);
        Assert.Equal(20m, i["roe"]);
        Assert.Equal(10m, i["roa"]);
        Assert.Equal(0.27m, i["giro_ativo"]);
        Assert.Equal(60.0m, i["pmr"]);    // 20.000 ÷ 30.000 × 90
        Assert.Equal(90.0m, i["pmp"]);    // 15.000 ÷ 15.000 × 90
        Assert.Equal(180.0m, i["pme"]);   // 30.000 ÷ 15.000 × 90
    }

    [Fact]
    public void Indicadores_sem_dados_ficam_vazios_e_nao_dividem_por_zero()
    {
        var vazio = new PosicaoPatrimonial(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var i = CalculoBi.Indicadores(vazio, new DemonstracaoEbitda(new(2026, 1, 1), new(2026, 1, 31), [], 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), 31);
        Assert.All(i.Where(x => x.Formato != FormatoIndicador.Moeda), x => Assert.Null(x.Valor));
        Assert.All(i, x => Assert.False(string.IsNullOrWhiteSpace(x.Formula) || string.IsNullOrWhiteSpace(x.Interpretacao)));
    }

    [Fact]
    public void Cascata_fecha_no_lucro_liquido()
    {
        var c = CalculoBi.Cascata(Resultado());
        Assert.Equal(30_000m + c.Where(x => !x.Total).Sum(x => x.Valor), c[^1].Valor);
    }

    private static MovimentoCentro M(long conta, long centro, int mes, decimal d, decimal c) => new(conta, centro, new(2026, mes, 1), d, c);

    [Fact]
    public void Serie_grupos_e_centros_somam_receita_e_despesas()
    {
        var movs = new[] { M(32, 0, 1, 0, 1000), M(42, 1, 1, 400, 0), M(43, 2, 1, 100, 0), M(32, 0, 2, 0, 3000), M(42, 1, 2, 800, 0) };
        var serie = CalculoBi.Serie(Plano, movs, new(2025, 12, 1), new(2026, 2, 1));
        Assert.Equal([0m, 1000m, 3000m], serie.Select(s => s.Receita));
        Assert.Equal([0m, 500m, 800m], serie.Select(s => s.Despesas));
        Assert.Equal(2200m, serie[2].Lucro);
        var grupos = CalculoBi.DespesasPorGrupo(Plano, movs);
        Assert.Equal([("DESPESAS OPERACIONAIS", 1300m)], grupos.Select(g => (g.Rotulo, g.Valor)));
        var centros = CalculoBi.CustosPorCentro(Plano, [new(1, "1", "Administrativo", null, true, true)], movs);
        Assert.Equal([("1 Administrativo", 1200m), (Gerenciais.SemCentro, 100m)], centros.Select(g => (g.Rotulo, g.Valor)));
    }

    private static DadosBi Dados() => new(Plano, [new(1, "1", "Administrativo", null, true, true), new(2, "2", "Produção", null, true, true)],
        [new(7, "C01", "Cliente Um")],
        [
            new(1, 1, new(2026, 1, 10), 42, 1, 0, IndicadorDC.D, 400, "Salário jan"),
            new(1, 1, new(2026, 1, 10), 4, 0, 0, IndicadorDC.C, 400, "Salário jan"),
            new(2, 2, new(2026, 2, 5), 42, 1, 0, IndicadorDC.D, 800, "Salário fev"),
            new(3, 3, new(2026, 2, 6), 43, 2, 0, IndicadorDC.D, 100, "Aluguel"),
            new(4, 4, new(2026, 4, 7), 32, 0, 7, IndicadorDC.C, 3000, "Venda"),
            new(4, 4, new(2026, 4, 7), 6, 0, 7, IndicadorDC.D, 3000, "Venda"),
        ]);

    [Fact]
    public void Cubo_soma_por_linha_e_coluna_e_o_detalhe_confere_com_a_celula()
    {
        var d = Dados();
        var q = new ConsultaCubo(DimensaoCubo.Conta, DimensaoCubo.Mes, MedidaCubo.Saldo, SoResultado: true);
        var c = Cubo.Montar(d, q);
        Assert.Equal(["01/2026", "02/2026", "04/2026"], c.Colunas.Select(x => x.Rotulo));
        Assert.Equal(["3.1.01.001", "4.2.01.001", "4.2.02.001"], c.Linhas.Select(l => l.Chave));
        var salarios = c.Linhas.Single(l => l.Chave == "4.2.01.001");
        Assert.Equal([400m, 800m, 0m], salarios.Valores);
        Assert.Equal(1200m, salarios.Total);
        Assert.Equal(-3000m, c.Linhas[0].Total);              // saldo = débitos − créditos: receita é credora
        Assert.Equal(-1700m, c.Total);                         // resultado do período com sinal de saldo
        Assert.Equal(c.Total, c.TotaisColunas.Sum());

        // drill-down: a soma dos lançamentos da célula é o valor da célula
        var det = Cubo.Detalhe(d, q, "4.2.01.001", c.Colunas[1].Chave);
        Assert.Single(det);
        Assert.Equal((800m, "Salário fev", 2L), (det[0].Valor, det[0].Historico, det[0].Numero));
        Assert.Equal(salarios.Total, Cubo.Detalhe(d, q, "4.2.01.001", null).Sum(x => x.DC == IndicadorDC.D ? x.Valor : -x.Valor));
        Assert.Equal(4, Cubo.Detalhe(d, q, null, null).Count);

        var res = Cubo.Montar(d, q with { Medida = MedidaCubo.Resultado });
        Assert.Equal(1700m, res.Total);
    }

    [Fact]
    public void Cubo_por_centro_trimestre_e_participante()
    {
        var d = Dados();
        var porCentro = Cubo.Montar(d, new ConsultaCubo(DimensaoCubo.CentroCusto, DimensaoCubo.Trimestre, MedidaCubo.Debitos, "4"));
        Assert.Equal(["1 — Administrativo", "2 — Produção"], porCentro.Linhas.Select(l => l.Rotulo));
        Assert.Equal([1200m], porCentro.Linhas[0].Valores);
        Assert.Equal(["1º tri/2026"], porCentro.Colunas.Select(c => c.Rotulo));
        var semColuna = Cubo.Montar(d, new ConsultaCubo(DimensaoCubo.Participante, DimensaoCubo.Nenhuma, MedidaCubo.Creditos));
        Assert.Empty(semColuna.Colunas);
        Assert.Equal(3000m, semColuna.Linhas.Single(l => l.Rotulo == "C01 — Cliente Um").Total);
        Assert.Equal(400m, semColuna.Linhas.Single(l => l.Rotulo == "Sem participante").Total);
        var porGrupo = Cubo.Montar(d, new ConsultaCubo(DimensaoCubo.Grupo, DimensaoCubo.Ano, MedidaCubo.Debitos));
        Assert.Contains(porGrupo.Linhas, l => l.Rotulo == "4.2 — DESPESAS OPERACIONAIS" && l.Total == 1300m);
    }

    [Fact]
    public void Tabelas_para_BI_trazem_fatos_dimensoes_e_calendario_completo()
    {
        var t = TabelasBi.Montar(Dados(), new(2026, 1, 1), new(2026, 12, 31));
        Assert.Equal(["fato_partidas", "dim_plano_contas", "dim_centro_custo", "dim_participante", "dim_calendario"], t.Select(x => x.Titulo));
        Assert.Equal(["id_lancamento", "numero_lancamento", "data", "id_conta", "id_centro_custo", "id_participante", "tipo_dc", "valor", "valor_assinado", "historico"], t[0].Colunas.Select(c => c.Titulo));
        Assert.Equal(6, t[0].Linhas.Count);
        Assert.Equal(-400m, t[0].Linhas[1].Valores[8]);
        Assert.Equal(Plano.Count, t[1].Linhas.Count);
        Assert.Equal(365, t[4].Linhas.Count);
        Assert.Equal("Janeiro", t[4].Linhas[0].Valores[4]);
    }
}
