using Vorticis.Fiscal.Dctf;
using Vorticis.Fiscal.Guias;

namespace Vorticis.Fiscal.Tests;

/// <summary>Obrigações › DCTFWeb (MIT): só com a apuração encerrada; débitos das guias DARF; recibo da transmissão; mês transmitido não reabre.</summary>
public class DctfWebTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private static readonly DateOnly Mar = new(2026, 3, 1);

    private static Guia G(string tributo, string? codigo, decimal valor, DocumentoGuia doc, string origem, DateOnly? comp = null) =>
        new(0, tributo, $"{tributo} · 03/2026", comp ?? Mar, Mar.AddMonths(1), valor, codigo, doc, origem, null, false, null, null, null);

    [Fact]
    public void Debitos_do_MIT_saem_das_guias_DARF_e_as_retencoes_ficam_so_como_informacao()
    {
        var d = ServicoDctfWeb.Montar(Mar,
        [
            G("IRPJ", "2089", 3_600m, DocumentoGuia.Darf, "LUCRO"), G("CSLL", "2372", 3_240m, DocumentoGuia.Darf, "LUCRO"),
            G("PIS", "8109", 650m, DocumentoGuia.Darf, "PIS_COFINS"), G("PIS", "8109", 100m, DocumentoGuia.Darf, "MANUAL"),
            G("IRRF", "1708", 15m, DocumentoGuia.Darf, "RETENCAO"), G("INSS", null, 110m, DocumentoGuia.DctfWeb, "RETENCAO"),
            G("DAS", null, 900m, DocumentoGuia.Das, "SIMPLES"), G("ISS", null, 50m, DocumentoGuia.Iss, "MANUAL"),
            G("COFINS", "2172", 3_000m, DocumentoGuia.Darf, "PIS_COFINS", new DateOnly(2026, 4, 1)),   // outro mês
        ]);
        Assert.Equal([("CSLL", "2372", 3_240m), ("IRPJ", "2089", 3_600m), ("PIS", "8109", 750m)], d.Debitos.Select(x => (x.Tributo, x.CodigoReceita, x.Valor)));
        Assert.Equal(7_590m, d.Total);
        Assert.Equal(2, d.Informativos.Count);
        Assert.All(d.Informativos, i => Assert.Contains("EFD-Reinf", i.Descricao));
        Assert.True(ServicoDctfWeb.Montar(Mar, []).SemMovimento);
    }

    [Fact]
    public async Task Prepara_so_com_o_mes_fechado_registra_o_recibo_e_o_mes_transmitido_nao_reabre()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await db.CriarEmpresaAsync(CnpjsDeTeste.Valido(83));   // Lucro Presumido
        var adm = await db.AdminAsync();
        var f = db.App.Fiscal();
        Assert.True((await f.Guias.GravarManualAsync(empresa, new NovaGuia("IRPJ", "IRPJ 1º tri", Mar, new DateOnly(2026, 4, 30), 3_600m, "2089", DocumentoGuia.Darf, "MANUAL", null), adm, ct)).Sucesso);
        var dctf = f.DctfWeb;

        var aberto = await dctf.PrepararAsync(empresa, Mar, adm, ct);
        Assert.Contains("não está encerrada", aberto.Erros[0]);
        Assert.Equal(3_600m, (await dctf.PreverAsync(empresa, Mar, adm, ct)).Valor!.Total);   // conferir antes, mesmo em aberto

        Assert.True((await f.Fechamento.FecharAsync(empresa, Mar, adm, ct)).Sucesso);
        var prep = await dctf.PrepararAsync(empresa, Mar, adm, ct);
        Assert.True(prep.Sucesso, string.Join(";", prep.Erros));
        Assert.Equal(3_600m, prep.Valor!.Total);
        var mar = (await dctf.ListarAsync(empresa, 2026, adm, ct)).Valor!.Single(m => m.Competencia == Mar);
        Assert.Equal((true, "PREPARADA", 3_600m), (mar.Fechado, mar.Situacao, mar.Total));

        Assert.False((await dctf.RegistrarTransmissaoAsync(empresa, Mar, " ", new DateOnly(2026, 4, 10), adm, ct)).Sucesso);
        Assert.True((await dctf.RegistrarTransmissaoAsync(empresa, Mar, "12.34.56.78.90-12", new DateOnly(2026, 4, 10), adm, ct)).Sucesso);
        Assert.Contains("12.34.56.78.90-12", (await dctf.ListarAsync(empresa, 2026, adm, ct)).Valor!.Single(m => m.Competencia == Mar).SituacaoTexto);
        Assert.Contains("já foi transmitida", (await dctf.PrepararAsync(empresa, Mar, adm, ct)).Erros[0]);
        Assert.Contains("já foi transmitida", (await f.Fechamento.ReabrirAsync(empresa, Mar, adm, ct)).Erros[0]);   // retificar: desfazer antes

        Assert.True((await dctf.DesfazerTransmissaoAsync(empresa, Mar, adm, ct)).Sucesso);
        Assert.True((await f.Fechamento.ReabrirAsync(empresa, Mar, adm, ct)).Sucesso);

        // Simples Nacional: os tributos vão no DAS
        await db.EscalarAsync<int>("WITH x AS (UPDATE core.empresa_regime SET regime = 'SIMPLES_NACIONAL' WHERE empresa_id = @e RETURNING 1) SELECT count(*)::int FROM x", ("e", empresa));
        Assert.Contains("regime normal", (await dctf.PrepararAsync(empresa, Mar, adm, ct)).Erros[0]);
    }
}
