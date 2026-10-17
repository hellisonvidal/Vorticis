using Perseus.Domain.Contabil;
using Perseus.Domain.Seguranca;

namespace Perseus.Infra.Tests;

public class LimpezaBancoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Limpar_banco_exige_administrador_e_confirmacao_e_mantem_usuarios()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var empresa = await db.CriarEmpresaAsync("11444777000161");
        await db.App.Lancamentos.EfetivarAsync(new LancamentoProposto(empresa, new(2026, 2, 1), OrigemLancamento.Manual,
            [new("1.1.01.001", IndicadorDC.D, 50m, "x"), new("2.3.01.001", IndicadorDC.C, 50m, "x")]), adm, ct);
        var svc = db.App.ConfiguracoesGerais;
        var admin = new SessaoUsuario(adm, "admin_teste", "Admin", true, false);

        Assert.Contains("Somente o administrador", (await svc.LimparBancoAsync(admin with { Administrador = false }, "LIMPAR", ct)).Erros[0]);
        Assert.Contains("digite LIMPAR", (await svc.LimparBancoAsync(admin, "limpar", ct)).Erros[0]);
        var r = await svc.LimparBancoAsync(admin, "LIMPAR", ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        Assert.Equal(1, r.Valor);
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM core.empresa"));
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM contabil.lancamento"));
        Assert.True(await db.EscalarAsync<long>("SELECT count(*) FROM core.usuario") >= 1);
        Assert.True(await db.EscalarAsync<long>("SELECT count(*) FROM imobilizado.grupo") > 0);
    }
}
