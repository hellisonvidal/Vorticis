using Vorticis.Condominio;
using Vorticis.Infra.Tests;

namespace Vorticis.Modulos.Tests;

public class RegrasCondominioTests
{
    private static Unidade U(long id, decimal f, bool ativa = true) => new(id, "A", id.ToString(), "APARTAMENTO", f, null, null, null, false, ativa);

    [Fact]
    public void Rateio_pela_fracao_fecha_o_total_e_partes_iguais_tambem()
    {
        Unidade[] us = [U(1, 0.3m), U(2, 0.3m), U(3, 0.4m), U(4, 0.2m, ativa: false)];
        var r = RegrasCondominio.Ratear(new Taxa(1, "Ordinária", Rateio.Fracao, 1000.01m, new DateOnly(2026, 1, 1), null), us);
        Assert.Equal(1000.01m, r.Values.Sum());
        Assert.Equal(400.01m, r[3]);                      // o centavo vai para a maior fração
        Assert.False(r.ContainsKey(4));                   // unidade inativa não paga
        var iguais = RegrasCondominio.Ratear(new Taxa(2, "Extra", Rateio.Igual, 100m, new DateOnly(2026, 1, 1), null), us);
        Assert.Equal((33.34m, 33.33m, 100m), (iguais[1], iguais[2], iguais.Values.Sum()));
        Assert.All(RegrasCondominio.Ratear(new Taxa(3, "Gás", Rateio.Fixo, 50m, new DateOnly(2026, 1, 1), null), us).Values, v => Assert.Equal(50m, v));
    }

    [Fact]
    public void Encargos_limitam_a_multa_e_cobram_juros_pro_rata()
    {
        var cfg = new ConfiguracaoCondominio(10, 10m, 1m);   // multa acima do limite legal é reduzida a 2%
        var venc = new DateOnly(2026, 3, 10);
        Assert.Equal((0m, 0m), RegrasCondominio.Encargos(500m, venc, venc, cfg, 2m));
        Assert.Equal((10m, 2.50m), RegrasCondominio.Encargos(500m, venc, venc.AddDays(15), cfg, 2m));
    }
}

public class CondominioIntegracaoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Gera_cobrancas_recebe_com_encargos_e_presta_contas()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var empresa = await db.CriarEmpresaAsync("26637142000158");
        var svc = ServicosCondominio.De(db.App);
        var prop = await db.EscalarAsync<long>("INSERT INTO core.participante (empresa_id, codigo, nome, cliente) VALUES (@e, 'P1', 'Proprietária', TRUE) RETURNING id", ("e", empresa));

        Assert.Contains("2%", (await svc.GravarConfiguracaoAsync(empresa, new ConfiguracaoCondominio(10, 5m, 1m), adm, ct)).Erros[0]);
        Assert.True((await svc.GravarConfiguracaoAsync(empresa, new ConfiguracaoCondominio(10, 2m, 1m), adm, ct)).Sucesso);
        Assert.True((await svc.GravarUnidadeAsync(empresa, new Unidade(0, "A", "101", "APARTAMENTO", 0.6m, 80m, prop, null, false), adm, ct)).Sucesso);
        Assert.True((await svc.GravarUnidadeAsync(empresa, new Unidade(0, "A", "102", "APARTAMENTO", 0.4m, 60m, prop, null, false), adm, ct)).Sucesso);
        Assert.Equal("Informe o número da unidade.", (await svc.GravarUnidadeAsync(empresa, new Unidade(0, "A", "", "APARTAMENTO", 1m, null, null, null, false), adm, ct)).Erros[0]);
        Assert.True((await svc.GravarTaxaAsync(empresa, new Taxa(0, "Taxa ordinária", Rateio.Fracao, 1000m, new DateOnly(2026, 1, 1), null), adm, ct)).Sucesso);
        Assert.True((await svc.GravarTaxaAsync(empresa, new Taxa(0, "Fundo de reserva", Rateio.Igual, 100m, new DateOnly(2026, 1, 1), null), adm, ct)).Sucesso);

        var comp = new DateOnly(2026, 3, 1);
        Assert.Equal(2, (await svc.GerarCobrancasAsync(empresa, comp, adm, ct)).Valor);
        Assert.Equal(0, (await svc.GerarCobrancasAsync(empresa, comp, adm, ct)).Valor);
        var cobs = await svc.CobrancasAsync(empresa, comp, false, adm, ct);
        var c101 = cobs.Single(c => c.Unidade == "A-101");
        Assert.Equal((650m, 2, new DateOnly(2026, 3, 10), "Proprietária"), (c101.Valor, c101.Itens.Count, c101.Vencimento, c101.Responsavel));

        var pag = new DateOnly(2026, 3, 25);
        var (multa, juros) = await svc.EncargosAsync(empresa, c101, pag, ct);
        Assert.Equal((13m, 3.25m), (multa, juros));
        Assert.False((await svc.ReceberAsync(empresa, c101, pag, 50m, 0, 0, adm, ct)).Sucesso);   // multa acima do limite
        Assert.True((await svc.ReceberAsync(empresa, c101, pag, multa, juros, 0, adm, ct)).Sucesso);
        Assert.True((await svc.GravarDespesaAsync(empresa, new Despesa(0, pag, pag, null, "LIMPEZA", "Material de limpeza", 120m, pag), adm, ct)).Sucesso);

        var pc = await svc.PrestacaoContasAsync(empresa, comp, adm, ct);
        Assert.Equal((650m, 16.25m, 120m, 1100m, 450m), (pc.Valor.Arrecadado, pc.Valor.EncargosRecebidos, pc.Valor.TotalDespesas, pc.Valor.Emitido, pc.Valor.Inadimplencia));
        Assert.Equal(546.25m, pc.Valor.Saldo);
        var inad = await svc.InadimplenciaAsync(empresa, new DateOnly(2026, 4, 9), adm, ct);
        Assert.Equal("A-102", Assert.Single(inad.Valor).C.Unidade);

        Assert.True((await svc.GravarAreaAsync(empresa, new AreaComum(0, "Salão de festas", 50, 100m), adm, ct)).Sucesso);
        var area = Assert.Single(await svc.AreasAsync(empresa, adm, ct));
        Assert.True((await svc.ReservarAsync(empresa, area.Id, c101.UnidadeId, new DateOnly(2026, 4, 4), null, adm, ct)).Sucesso);
        Assert.Contains("já está reservada", (await svc.ReservarAsync(empresa, area.Id, c101.UnidadeId, new DateOnly(2026, 4, 4), null, adm, ct)).Erros[0]);
    }
}
