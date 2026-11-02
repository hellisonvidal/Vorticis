using Vorticis.Application.Contabil;
using Vorticis.Domain.Contabil;

namespace Vorticis.Infra.Tests;

/// <summary>Plano de contas › Importar contas do Excel: acrescenta só as contas novas, da superior para as subcontas.</summary>
public class PlanoAcrescentarTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Acrescenta_as_novas_em_ordem_e_informa_as_que_nao_entram()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var empresa = await db.CriarEmpresaAsync("11222333000181");
        var antes = await db.App.PlanoContas.ListarAsync(empresa, adm, ct);
        var grupo = antes.First(c => !c.Analitica && c.Codigo.Count(ch => ch == '.') == 2 && c.Codigo.StartsWith("1."));
        var dia = new DateOnly(2026, 1, 1);
        NovaConta C(string cod, string desc) => new(cod, desc, grupo.CodNat, grupo.NaturezaSaldo, false, false, dia);

        var r = await db.App.PlanoContas.AcrescentarAsync(empresa,
        [
            C(grupo.Codigo + ".98.001", "Subconta da nova"),            // a superior vem depois na lista: entra na ordem certa
            C(grupo.Codigo + ".98", "Grupo novo"),
            C(grupo.Codigo, grupo.Descricao),                             // já existe: fica como está
            C("9.9.99.001", "Sem conta superior"),                        // não entra: falta a superior
        ], adm, ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        Assert.Equal((2, 1), (r.Valor.Incluidas, r.Valor.JaExistiam));
        Assert.Single(r.Valor.Problemas, p => p.StartsWith("9.9.99.001"));
        var depois = await db.App.PlanoContas.ListarAsync(empresa, adm, ct);
        Assert.Contains(depois, c => c.Codigo == grupo.Codigo + ".98.001" && c.Analitica);
        Assert.Equal(antes.Count + 2, depois.Count);

        Assert.False((await db.App.PlanoContas.AcrescentarAsync(empresa, [], adm, ct)).Sucesso);
    }
}
