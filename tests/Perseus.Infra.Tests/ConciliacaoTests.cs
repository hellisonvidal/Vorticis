using Perseus.Application.Conciliacao;
using Perseus.Domain.Contabil;

namespace Perseus.Infra.Tests;

public class ConciliacaoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Theory]
    [InlineData("NF 000123", "NF123")]
    [InlineData("nf123", "NF123")]
    [InlineData("00045", "45")]
    public void Documento_normalizado(string doc, string esperado) => Assert.Equal(esperado, ServicoConciliacao.Normalizar(doc));

    [Fact]
    public async Task Concilia_por_documento_manual_e_desfaz()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await db.CriarEmpresaAsync("07526557000100");
        var adm = await db.AdminAsync();
        async Task Lancar(int dia, string deb, string cred, decimal v, string? doc) =>
            Assert.True((await db.App.Lancamentos.EfetivarAsync(new LancamentoProposto(empresa, new(2026, 3, dia), OrigemLancamento.Manual,
                [new(deb, IndicadorDC.D, v, "x", NumeroArquivo: doc), new(cred, IndicadorDC.C, v, "x", NumeroArquivo: doc)]), adm, ct)).Sucesso);
        const string fornecedores = "2.1.01.001";
        await Lancar(1, "1.1.04.001", fornecedores, 1_000m, "NF 123");    // compra (crédito no fornecedor)
        await Lancar(10, fornecedores, "1.1.01.002", 1_000m, "nf000123"); // pagamento da mesma NF
        await Lancar(2, "1.1.04.001", fornecedores, 500m, "NF 200");
        await Lancar(12, fornecedores, "1.1.01.002", 300m, "NF 200");     // pago em parte: não fecha
        await Lancar(5, "1.1.04.001", fornecedores, 80m, null);
        await Lancar(6, fornecedores, "1.1.01.002", 80m, null);

        var svc = db.App.Conciliacao;
        var auto = await svc.ConciliarAutomaticoAsync(empresa, fornecedores, new DateOnly(2026, 3, 31), adm, ct);
        Assert.True(auto.Sucesso, string.Join("; ", auto.Erros));
        Assert.Equal((1, 2, 1), (auto.Valor.Conciliacoes, auto.Valor.Partidas, auto.Valor.DocumentosEmAberto));

        var partidas = (await svc.ListarAsync(empresa, fornecedores, new DateOnly(2026, 3, 31), adm, ct)).Valor;
        Assert.Equal(2, partidas.Count(p => p.Conciliada));
        var semDoc = partidas.Where(p => p.Documento is null).Select(p => p.PartidaId).ToList();
        Assert.Contains("não se anulam", (await svc.ConciliarManualAsync(empresa, fornecedores,
            partidas.Where(p => p.Documento == "NF 200").Select(p => p.PartidaId).ToList(), adm, ct)).Erros[0]);
        var manual = await svc.ConciliarManualAsync(empresa, fornecedores, semDoc, adm, ct);
        Assert.True(manual.Sucesso, string.Join("; ", manual.Erros));
        Assert.Contains("já conciliadas", (await svc.ConciliarManualAsync(empresa, fornecedores, semDoc, adm, ct)).Erros[0]);

        Assert.True((await svc.DesfazerAsync(empresa, manual.Valor, adm, ct)).Sucesso);
        partidas = (await svc.ListarAsync(empresa, "2.1.01.001", new DateOnly(2026, 3, 31), adm, ct)).Valor;
        Assert.Equal(4, partidas.Count(p => !p.Conciliada));
        Assert.Contains("não encontrada", (await svc.ListarAsync(empresa, "9.9.9", DateOnly.MaxValue, adm, ct)).Erros[0]);
        Assert.True((await db.App.Empresas.ExcluirAsync(empresa, ct)).Sucesso);           // exclusão leva as conciliações
    }
}
