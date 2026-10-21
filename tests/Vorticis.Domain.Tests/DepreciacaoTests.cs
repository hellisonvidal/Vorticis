using Vorticis.Domain.Imobilizado;

namespace Vorticis.Domain.Tests;

public class DepreciacaoTests
{
    private static BemParaDepreciar Bem(decimal valor = 12_000m, decimal residual = 0, int vida = 120, MetodoDepreciacao metodo = MetodoDepreciacao.Linear,
                                        DateOnly? baixa = null, decimal taxa = 10, int turnos = 1, decimal anterior = 0, bool deprecia = true) =>
        new(1, valor, residual, vida, metodo, new DateOnly(2026, 3, 15), baixa, anterior, deprecia, taxa, turnos);

    [Fact]
    public void Linear_comeca_no_mes_do_inicio_de_uso_e_respeita_residual()
    {
        var b = Bem(residual: 2_000m);
        Assert.Null(RegrasDepreciacao.Calcular(b, new DateOnly(2026, 2, 1), 0, 0));
        var q = RegrasDepreciacao.Calcular(b, new DateOnly(2026, 3, 1), 0, 0)!;
        Assert.Equal((83.33m, 100m), (q.Contabil, q.Fiscal));      // contábil (12.000 − 2.000)/120; fiscal 10% a.a. sobre o custo
        Assert.Equal(16.67m, q.Diferenca);
        // Último mês leva o resíduo de arredondamento e não passa do valor depreciável
        var ultimo = RegrasDepreciacao.Calcular(b, new DateOnly(2036, 2, 1), 83.33m * 119, 0)!;
        Assert.Equal(10_000m - 83.33m * 119, ultimo.Contabil);
        Assert.Equal(10_000m, ultimo.AcumuladoApos);
        Assert.Equal(0, RegrasDepreciacao.Calcular(b, new DateOnly(2036, 3, 1), 10_000m, 12_000m)?.Contabil ?? 0);
    }

    [Fact]
    public void Turnos_aceleram_so_a_fiscal_e_baixa_interrompe()
    {
        var b = Bem(turnos: 3, baixa: new DateOnly(2026, 6, 10));
        var q = RegrasDepreciacao.Calcular(b, new DateOnly(2026, 5, 1), 0, 0)!;
        Assert.Equal((100m, 200m), (q.Contabil, q.Fiscal));        // fator 2,0 para 3 turnos (RIR/2018 art. 323)
        Assert.Null(RegrasDepreciacao.Calcular(b, new DateOnly(2026, 6, 1), 0, 0));
        Assert.Equal(1.5m, RegrasDepreciacao.FatorTurnos(2));
    }

    [Fact]
    public void Soma_dos_digitos_decrescente_e_total_igual_ao_depreciavel()
    {
        var b = Bem(valor: 1_000m, vida: 4, metodo: MetodoDepreciacao.SomaDigitos);
        decimal acumulado = 0;
        var quotas = new List<decimal>();
        for (var m = 0; m < 4; m++)
        {
            var q = RegrasDepreciacao.Calcular(b, new DateOnly(2026, 3, 1).AddMonths(m), acumulado, 0)!;
            quotas.Add(q.Contabil);
            acumulado += q.Contabil;
        }
        Assert.Equal([400m, 300m, 200m, 100m], quotas);             // 4/10, 3/10, 2/10, 1/10
        Assert.Equal(1_000m, acumulado);
    }

    [Fact]
    public void Terreno_nao_deprecia_e_implantacao_desconta_o_ja_depreciado()
    {
        Assert.Null(RegrasDepreciacao.Calcular(Bem(deprecia: false), new DateOnly(2026, 4, 1), 0, 0));
        var b = Bem(anterior: 11_950m);
        var q = RegrasDepreciacao.Calcular(b, new DateOnly(2026, 4, 1), 0, 0)!;
        Assert.Equal((50m, 50m, 12_000m), (q.Contabil, q.Fiscal, q.AcumuladoApos));
        Assert.Equal(120, RegrasDepreciacao.VidaUtilFiscalMeses(10));
        Assert.Equal(300, RegrasDepreciacao.VidaUtilFiscalMeses(4));
    }
}
