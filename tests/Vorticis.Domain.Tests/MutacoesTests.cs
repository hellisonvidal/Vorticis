using Vorticis.Domain.Contabil;

namespace Vorticis.Domain.Tests;

public class MutacoesTests
{
    private static readonly ContaPlano[] Plano =
    [
        new(1, "1.1", "Caixa", 2, true, null, IndicadorDC.D, "01"),
        new(2, "2.1", "Dividendos a pagar", 2, true, null, IndicadorDC.C, "02"),
        new(10, "2.3", "PATRIMÔNIO LÍQUIDO", 1, false, null, IndicadorDC.C, "03"),
        new(11, "2.3.01", "CAPITAL SOCIAL", 2, false, 10, IndicadorDC.C, "03"),
        new(12, "2.3.01.001", "Capital Subscrito", 3, true, 11, IndicadorDC.C, "03"),
        new(13, "2.3.02", "RESERVAS", 2, false, 10, IndicadorDC.C, "03"),
        new(14, "2.3.02.001", "Reserva Legal", 3, true, 13, IndicadorDC.C, "03"),
        new(15, "2.3.03", "LUCROS OU PREJUÍZOS ACUMULADOS", 2, false, 10, IndicadorDC.C, "03"),
        new(16, "2.3.03.001", "Lucros Acumulados", 3, true, 15, IndicadorDC.C, "03"),
        new(20, "3.1", "Vendas", 2, true, null, IndicadorDC.C, "04"),
        new(21, "3.9", "Apuração do resultado", 2, true, null, IndicadorDC.C, "04"),
    ];

    private static PartidaMutacao P(long lanc, long conta, IndicadorDC dc, decimal v, bool enc = false) => new(lanc, conta, dc, v, enc);

    [Fact]
    public void Classifica_contas_do_pl_pelas_descricoes()
    {
        var porId = Plano.ToDictionary(c => c.Id);
        Assert.Equal(ColunaPl.CapitalSocial, Mutacoes.Classificar(porId[12], porId));
        Assert.Equal(ColunaPl.ReservasLucros, Mutacoes.Classificar(porId[14], porId));
        Assert.Equal(ColunaPl.LucrosAcumulados, Mutacoes.Classificar(porId[16], porId));
        Assert.Null(Mutacoes.Classificar(porId[1], porId));
    }

    [Fact]
    public void Dmpl_e_dlpa_conferem_com_o_saldo_final()
    {
        var saldos = new Dictionary<long, decimal> { [12] = -10_000m, [16] = -2_000m };
        IReadOnlyList<PartidaMutacao> partidas =
        [
            P(1, 1, IndicadorDC.D, 5_000m), P(1, 12, IndicadorDC.C, 5_000m),          // integralização em dinheiro
            P(2, 16, IndicadorDC.D, 100m), P(2, 14, IndicadorDC.C, 100m),             // reserva legal
            P(3, 16, IndicadorDC.D, 800m), P(3, 2, IndicadorDC.C, 800m),              // dividendos propostos
            P(4, 21, IndicadorDC.D, 3_000m, true), P(4, 16, IndicadorDC.C, 3_000m, true),   // encerramento: só transfere
        ];
        var d = Mutacoes.Montar(new(2026, 1, 1), new(2026, 12, 31), Plano, saldos, partidas, lucroLiquido: 3_000m);

        Assert.Equal("Saldos em 31/12/2025", d.Dmpl[0].Descricao);
        Assert.Equal(12_000m, d.Dmpl[0].Total);
        Assert.Equal(5_000m, d.Dmpl.Single(l => l.Descricao == Mutacoes.AumentoCapital).Valor(ColunaPl.CapitalSocial));
        var reservas = d.Dmpl.Single(l => l.Descricao == Mutacoes.ConstituicaoReservas);
        Assert.Equal((100m, -100m, 0m), (reservas.Valor(ColunaPl.ReservasLucros), reservas.Valor(ColunaPl.LucrosAcumulados), reservas.Total));
        Assert.Equal(-800m, d.Dmpl.Single(l => l.Descricao == Mutacoes.Dividendos).Total);
        var final = d.Dmpl[^1];
        Assert.Equal((15_000m, 100m, 4_100m), (final.Valor(ColunaPl.CapitalSocial), final.Valor(ColunaPl.ReservasLucros), final.Valor(ColunaPl.LucrosAcumulados)));
        Assert.Equal(19_200m, final.Total);                                           // 12.000 + 5.000 + 3.000 − 800
        Assert.DoesNotContain(ColunaPl.AcoesTesouraria, d.Colunas);

        // DLPA: 2.000 + 3.000 − 100 − 800 = 4.100
        Assert.Equal(2_000m, d.Dlpa[0].Valor);
        Assert.Equal(5_000m, d.Dlpa.Single(l => l.Descricao == "Saldo à disposição").Valor);
        Assert.Equal(-100m, d.Dlpa.Single(l => l.Descricao.Contains("Transferências para reservas")).Valor);
        Assert.Equal(4_100m, d.Dlpa[^1].Valor);
    }
}
