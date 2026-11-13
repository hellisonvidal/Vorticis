using Vorticis.Application.Contabil;
using Vorticis.Domain.Contabil;

namespace Vorticis.Infra.Tests;

public class ConsolidacaoTests
{
    private static LinhaBalancete L(string cod, decimal saldo, bool analitica = true)
        => new(0, cod, "Conta " + cod, (short)cod.Split('.').Length, analitica, 0, 0, 0, saldo, false);

    [Fact]
    public void Soma_pelo_codigo_e_elimina_conta_e_sinteticas_acima()
    {
        var a = new[] { L("1", 1000, false), L("1.1", 1000, false), L("1.1.01", 600), L("1.1.02", 400), L("2", -1000, false), L("2.1", -1000) };
        var b = new[] { L("1", 500, false), L("1.1", 500, false), L("1.1.01", 500), L("2", -500, false), L("2.1", -500) };
        var c = Consolidacao.Consolidar([a, b], ["1.1.02", "9.9"]);   // 1.1.02 = investimento na controlada; 9.9 não existe
        var d = c.ToDictionary(x => x.Codigo);
        Assert.Equal((1500m, 1100m), (d["1"].Saldos.Sum(), d["1"].Consolidado));
        Assert.Equal(-400m, d["1"].Eliminacao);
        Assert.Equal(-400m, d["1.1"].Eliminacao);
        Assert.Equal((-400m, 0m), (d["1.1.02"].Eliminacao, d["1.1.02"].Consolidado));
        Assert.Equal(1100m, d["1.1.01"].Consolidado);
        Assert.Equal(-1500m, d["2"].Consolidado);
        var t = Consolidacao.Tabela(c, ["A", "B"], new DateOnly(2026, 12, 31));
        Assert.Equal(6, t.Colunas.Count);
        Assert.Equal(1100m, t.Linhas.Single(l => (string)l.Valores[0]! == "1").Valores[5]);
    }
}

public class ConsolidacaoIntegracaoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Consolida_duas_empresas_do_banco()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var a = await db.CriarEmpresaAsync("77123456000173");
        var b = await db.CriarEmpresaAsync("77234567000157");
        foreach (var (e, v) in new[] { (a, 100m), (b, 250m) })
        {
            var r = await db.App.Lancamentos.EfetivarAsync(new LancamentoProposto(e, new DateOnly(2026, 3, 1), OrigemLancamento.Manual,
                [new("1.1.01.001", IndicadorDC.D, v, "Teste"), new("3.1.01.001", IndicadorDC.C, v, "Teste")]), adm, ct);
            Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        }
        Assert.Contains("duas empresas", (await db.App.Relatorios.ConsolidadoAsync([a], new DateOnly(2026, 12, 31), [], adm, ct)).Erros[0]);
        var c = await db.App.Relatorios.ConsolidadoAsync([a, b], new DateOnly(2026, 12, 31), [], adm, ct);
        Assert.True(c.Sucesso, string.Join("; ", c.Erros));
        Assert.Equal(350m, c.Valor.Single(l => l.Codigo == "1.1.01.001").Consolidado);
    }
}
