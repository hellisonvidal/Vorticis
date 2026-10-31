using Vorticis.Fiscal.Comum;
using Vorticis.Fiscal.Lucro;
using Vorticis.Fiscal.Planejamento;
using Vorticis.Fiscal.Simples;

namespace Vorticis.Fiscal.Tests;

/// <summary>Apuração › Planejamento tributário: Simples Nacional × Lucro Presumido × Lucro Real com os mesmos dados do ano.</summary>
public class PlanejamentoTributarioTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private static readonly TabelaSimples Tabela = TabelaSimples.Carregar(ParametrosEmbutidos.Json(ChavesParametro.SimplesNacional));
    private static readonly ParametrosLucro Lucro = ParametrosLucro.Carregar(ParametrosEmbutidos.Json(ChavesParametro.LucroPresumidoReal));
    private static readonly ParametrosPlanejamento Plan = ParametrosPlanejamento.Carregar(ParametrosEmbutidos.Json(ChavesParametro.Planejamento));

    private static EntradaPlanejamento Comercio() =>
        new(2027, [new ReceitaPlanejamento(TipoReceitaPlanejamento.Comercio, 1_200_000m)], Folha: 120_000m, EncargosPercentual: 27.8m,
            CustosDespesas: 700_000m, ComprasComCredito: 600_000m, AliquotaIcms: 5m, AliquotaIss: 0m);

    [Fact]
    public void Comercio_compara_os_tres_regimes_e_aponta_o_de_menor_carga()
    {
        var r = PlanejamentoTributario.Comparar(Comercio(), Tabela, Lucro, Plan);
        var simples = r.Regimes.Single(x => x.Regime == RegimePlanejamento.SimplesNacional);
        var presumido = r.Regimes.Single(x => x.Regime == RegimePlanejamento.LucroPresumido);
        var real = r.Regimes.Single(x => x.Regime == RegimePlanejamento.LucroReal);
        // Simples, Anexo I, 4ª faixa: (1.200.000 × 10,7% − 22.500) ÷ 1.200.000 = 8,825% → DAS de 8.825,00 por mês (ICMS dentro do DAS)
        Assert.Equal(105_900m, simples.Das);
        Assert.Equal(105_900m, simples.Total);
        // Presumido: IRPJ 8% × 15% e CSLL 12% × 9% por trimestre; PIS/COFINS 3,65%; encargos 27,8% da folha; ICMS 5%
        Assert.Equal((14_400m, 12_960m, 7_800m, 36_000m, 33_360m, 60_000m), (presumido.Irpj, presumido.Csll, presumido.Pis, presumido.Cofins, presumido.Cpp, presumido.Icms));
        Assert.Equal(164_520m, presumido.Total);
        // Real: PIS/COFINS 9,25% com crédito das compras; lucro 231.140 → IRPJ 15% e CSLL 9%
        Assert.Equal((9_900m, 45_600m), (real.Pis, real.Cofins));
        Assert.Equal((34_671m, 20_802.60m), (real.Irpj, real.Csll));
        Assert.Equal(204_333.60m, real.Total);
        Assert.Equal(RegimePlanejamento.SimplesNacional, r.Melhor!.Regime);
        Assert.Equal(58_620m, r.Economia);
        Assert.Equal(8.83m, r.Percentual(simples));
    }

    [Fact]
    public void Acima_dos_limites_o_regime_nao_e_permitido_e_o_anexo_IV_paga_a_CPP_fora_do_DAS()
    {
        var grande = Comercio() with { Receitas = [new ReceitaPlanejamento(TipoReceitaPlanejamento.ServicosAnexoIII, 80_000_000m)] };
        var r = PlanejamentoTributario.Comparar(grande, Tabela, Lucro, Plan);
        Assert.False(r.Regimes.Single(x => x.Regime == RegimePlanejamento.SimplesNacional).Permitido);
        Assert.False(r.Regimes.Single(x => x.Regime == RegimePlanejamento.LucroPresumido).Permitido);
        Assert.Equal(RegimePlanejamento.LucroReal, r.Melhor!.Regime);
        Assert.Equal(0m, r.Economia);

        var iv = new EntradaPlanejamento(2027, [new ReceitaPlanejamento(TipoReceitaPlanejamento.ServicosAnexoIV, 600_000m)], 200_000m, 20m, 100_000m, 0m, 0m, 5m);
        var s = PlanejamentoTributario.Simples(iv, Tabela);
        Assert.True(s.Permitido);
        Assert.Equal(40_000m, s.Cpp);                                   // 200.000 × 20%, fora do DAS
        Assert.Equal(s.Das + 40_000m, s.Total);
        Assert.Contains(s.Memoria, m => m.Contains("Anexo IV"));
    }

    [Fact]
    public void Dados_incompletos_sao_recusados()
    {
        var erros = PlanejamentoTributario.Validar(Comercio() with { Receitas = [], AliquotaIcms = 120m });
        Assert.Contains("Informe ao menos uma receita do ano.", erros);
        Assert.Contains("Os percentuais devem estar entre 0 e 100.", erros);
    }

    [Fact]
    public async Task Simulacoes_gravadas_por_empresa_e_ano()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await db.CriarEmpresaAsync("54217813000167");
        var adm = await db.AdminAsync();
        var svc = db.App.Fiscal().Planejamento;
        Assert.Equal(20m, await svc.EncargoPadraoAsync(2027, ct));
        var cmp = await svc.CompararAsync(empresa, Comercio(), adm, ct);
        Assert.True(cmp.Sucesso, string.Join(";", cmp.Erros));
        Assert.Equal(RegimePlanejamento.SimplesNacional, cmp.Valor!.Melhor!.Regime);

        Assert.False((await svc.GravarAsync(empresa, " ", Comercio(), adm, ct)).Sucesso);
        var id = (await svc.GravarAsync(empresa, "Cenário base", Comercio(), adm, ct)).Valor;
        Assert.Equal(id, (await svc.GravarAsync(empresa, "CENÁRIO BASE", Comercio() with { Folha = 150_000m }, adm, ct)).Valor);   // mesma descrição: substitui
        var lista = (await svc.ListarAsync(empresa, adm, ct)).Valor!;
        var sim = Assert.Single(lista);
        Assert.Equal((2027, "CENÁRIO BASE", 150_000m, TipoReceitaPlanejamento.Comercio), (sim.Ano, sim.Descricao, sim.Dados.Folha, sim.Dados.Receitas[0].Tipo));
        Assert.True((await svc.ExcluirAsync(empresa, id, adm, ct)).Sucesso);
        Assert.Empty((await svc.ListarAsync(empresa, adm, ct)).Valor!);
        Assert.Equal(0, (await svc.MovimentoAsync(empresa, 2026, adm, ct)).Valor!.Meses);
    }
}
