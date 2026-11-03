using Vorticis.Domain.Comum;
using Vorticis.Domain.Contabil;

namespace Vorticis.Domain.Tests;

public class EvolucaoPatrimonialTests
{
    private static readonly IReadOnlyList<ContaPlano> Plano =
    [
        new(1, "1", "Ativo", 1, false, null, IndicadorDC.D, "01"),
        new(2, "1.1.01.001", "Caixa", 4, true, 1, IndicadorDC.D, "01"),
        new(3, "2", "Passivo", 1, false, null, IndicadorDC.C, "02"),
        new(4, "2.1.01.001", "Fornecedores", 4, true, 3, IndicadorDC.C, "02"),
        new(5, "2.3", "Patrimônio Líquido", 2, false, null, IndicadorDC.C, "03"),
        new(6, "2.3.01.001", "Capital Social", 4, true, 5, IndicadorDC.C, "03"),
        new(7, "2.3.02.001", "Reservas de Lucros", 4, true, 5, IndicadorDC.C, "03"),
        new(8, "2.3.03.001", "Lucros Acumulados", 4, true, 5, IndicadorDC.C, "03"),
        new(9, "3.1.01.001", "Receita de vendas", 4, true, null, IndicadorDC.C, "04"),
    ];

    [Fact]
    public void Datas_mensais_e_so_fim_de_ano()
    {
        var m = EvolucaoPatrimonial.Datas(new(2025, 11, 15), new(2026, 2, 10), false);
        Assert.Equal([new DateOnly(2025, 11, 30), new(2025, 12, 31), new(2026, 1, 31), new(2026, 2, 28)], m);
        var a = EvolucaoPatrimonial.Datas(new(2024, 3, 1), new(2026, 5, 1), true);
        Assert.Equal([new DateOnly(2024, 12, 31), new(2025, 12, 31), new(2026, 5, 31)], a);
    }

    [Fact]
    public void Ponto_separa_capital_reservas_e_lucros_e_soma_o_patrimonio()
    {
        var saldos = new Dictionary<long, decimal> { [2] = 180_000, [4] = -30_000, [6] = -100_000, [7] = -20_000, [8] = -10_000, [9] = -20_000 };
        var p = EvolucaoPatrimonial.Ponto(Plano, saldos, new(2026, 3, 31), 20_000);
        Assert.Equal(180_000, p.Ativo);
        Assert.Equal(30_000, p.Passivo);
        Assert.Equal(150_000, p.PatrimonioLiquido);
        Assert.Equal(100_000, p.Capital);
        Assert.Equal(20_000, p.Reservas);
        Assert.Equal(30_000, p.Lucros);   // lucros acumulados 10 mil + resultado ainda não encerrado 20 mil
        Assert.Equal(p.PatrimonioLiquido, p.Capital + p.Reservas + p.Lucros);
        Assert.Equal(20_000, p.Resultado);
    }

    [Fact]
    public void Variacao_absoluta_e_percentual()
    {
        var v = VariacaoValor.De(150, 100);
        Assert.Equal(50, v.Absoluta);
        Assert.Equal(50m, v.Percentual);
        Assert.Equal(40m, VariacaoValor.De(-60, -100).Percentual);   // de -100 para -60: +40%
        Assert.Null(VariacaoValor.De(10, 0).Percentual);
        var pontos = new[] { new PontoPatrimonial(new(2026, 1, 31), 100, 0, 0, 0, 0, 0, 0), new PontoPatrimonial(new(2026, 2, 28), 120, 0, 0, 0, 0, 0, 0) };
        var vs = EvolucaoPatrimonial.Variacoes(pontos, x => x.Ativo);
        Assert.Null(vs[0]);
        Assert.Equal(20m, vs[1]!.Percentual);
    }
}
