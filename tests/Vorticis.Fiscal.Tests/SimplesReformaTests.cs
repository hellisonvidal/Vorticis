using Vorticis.Fiscal.Comum;
using Vorticis.Fiscal.Reforma;
using Vorticis.Fiscal.Simples;

namespace Vorticis.Fiscal.Tests;

/// <summary>Simples Nacional × IBS/CBS: projeção ano a ano com IBS/CBS dentro do DAS (A) ou fora dele (B), pelo cronograma dos parâmetros legais.</summary>
public class SimplesReformaTests
{
    private static readonly TabelaSimples Tabela = TabelaSimples.Carregar(ParametrosEmbutidos.Json(ChavesParametro.SimplesNacional));
    private static readonly CronogramaReforma Crono = CronogramaReforma.Carregar(ParametrosEmbutidos.Json("REFORMA_SIMPLES"),
        ParametrosReforma.Carregar(ParametrosEmbutidos.Json(ServicoReformaTributaria.Chave)).First(a => a.Ano == 2026));

    private static SimulacaoSimples Simular(decimal receita, decimal compras, decimal reducao = 0)
    {
        var b = new BaseSimulacao(receita, compras, AtividadeSimples.ComercioRevenda, 0, reducao);
        var mensal = CalculadoraSimples.Calcular(Tabela, new EntradaSimples(new(2026, 11, 1), [new ReceitaSimples(b.Atividade, receita / 12m)], receita, 0));
        return CalculoSimulacaoSimples.Simular(b, mensal, Crono);
    }

    [Fact]
    public void Cronograma_vem_dos_parametros_de_2026_a_2033()
    {
        Assert.Equal(Enumerable.Range(2026, 8), Crono.Anos.Select(a => a.Ano));
        Assert.True(Crono.Anos[0].Teste);
        Assert.Equal(1.0m, Crono.Anos[0].Cbs + Crono.Anos[0].Ibs);   // 0,9% + 0,1% (REFORMA_IBS_CBS)
        Assert.Equal(100m, Crono.Anos[^1].IcmsIssExtinto);
        Assert.Equal(17.7m, Crono.Anos[^1].Ibs);
    }

    [Fact]
    public void Dentro_do_das_mantem_a_carga_e_2026_nao_muda_nada()
    {
        var s = Simular(360_000, 120_000);
        Assert.Equal(8, s.Linhas.Count);
        Assert.All(s.Linhas, l => Assert.Equal(s.DasAnual, l.DasAtual));
        Assert.All(s.Linhas, l => Assert.Equal(l.DasAtual, l.DasDentro));
        var l26 = s.Linhas[0];
        Assert.Equal(l26.DasAtual, l26.TotalFora);
        Assert.Equal(3_600m, l26.CreditoClienteDentro);   // 1% de 360 mil, só informativo
    }

    [Fact]
    public void Fora_do_das_tira_pis_cofins_em_2027_e_icms_iss_aos_poucos_e_abate_o_credito_das_compras()
    {
        var s = Simular(360_000, 120_000);
        var l27 = s.Linhas.Single(l => l.Ano == 2027);
        Assert.Equal(s.DasAnual - s.PisCofinsAnual, l27.DasFora, 1);
        // CBS 8,8% e IBS 0,1% sobre (receita - compras)
        Assert.Equal(21_120m, l27.CbsFora);
        Assert.Equal(240m, l27.IbsFora);
        var l33 = s.Linhas.Single(l => l.Ano == 2033);
        Assert.Equal(s.DasAnual - s.PisCofinsAnual - s.IcmsIssAnual, l33.DasFora, 1);
        Assert.Equal(240_000m * 0.177m, l33.IbsFora, 1);
        var l30 = s.Linhas.Single(l => l.Ano == 2030);
        Assert.Equal(s.DasAnual - s.PisCofinsAnual - s.IcmsIssAnual * 0.2m, l30.DasFora, 1);
        Assert.True(l33.CreditoClienteFora > l33.CreditoClienteDentro);
    }

    [Fact]
    public void Reducao_de_aliquota_vale_so_para_fora_e_compras_maiores_geram_saldo_credor()
    {
        var cheia = Simular(360_000, 0).Linhas.Single(l => l.Ano == 2033);
        var reduzida = Simular(360_000, 0, 60).Linhas.Single(l => l.Ano == 2033);
        Assert.Equal(cheia.CbsFora * 0.4m, reduzida.CbsFora, 1);
        Assert.Equal(cheia.DasDentro, reduzida.DasDentro);
        var credor = Simular(100_000, 300_000);
        Assert.True(credor.Linhas.Single(l => l.Ano == 2027).CbsFora < 0);
        Assert.Contains(credor.Avisos, a => a.Contains("saldo credor"));
    }

    [Fact]
    public void Tabelas_trazem_situacao_atual_ano_a_ano_e_cronograma()
    {
        var t = Simular(360_000, 120_000).Tabelas();
        Assert.Equal(3, t.Count);
        Assert.Equal(8, t[1].Linhas.Count);
        Assert.Equal(t[1].Colunas.Count, t[1].Linhas[0].Valores.Count);
        Assert.Equal(8, t[2].Linhas.Count);
    }
}
