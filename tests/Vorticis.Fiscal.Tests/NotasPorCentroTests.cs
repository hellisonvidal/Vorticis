using Vorticis.Fiscal.Documentos;

namespace Vorticis.Fiscal.Tests;

/// <summary>Relatórios › Notas por centro de custo: valor contábil das notas autorizadas do período por centro (sem centro, numa linha própria).</summary>
public class NotasPorCentroTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Soma_entradas_e_saidas_por_centro_e_separa_as_sem_centro()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var cnpj = CnpjsDeTeste.Valido(57);
        var empresa = await db.CriarEmpresaAsync(cnpj);
        var adm = await db.AdminAsync();
        var dia = new DateOnly(2026, 5, 10);
        var r = await db.App.Fiscal().Documentos.ImportarAsync(empresa,
            [new ArquivoXml("v.xml", XmlsDeTeste.Bytes(XmlsDeTeste.Nfe(cnpj, XmlsDeTeste.CnpjCliente, 1, 31, dia, [new ItemNfe("5102", 1_000m), new ItemNfe("5102", 500m)])))], adm, ct: ct);
        Assert.Equal(1, r.Valor.Importados);
        var svc = db.App.Fiscal().NotasPorCentro;
        var linhas = (await svc.ListarAsync(empresa, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), adm, ct)).Valor!;
        var sem = Assert.Single(linhas);
        Assert.Equal(("Sem centro de custo", 0, 0m, 1, 1_500m, 0m), (sem.Centro, sem.NotasEntrada, sem.Entradas, sem.NotasSaida, sem.Saidas, sem.Contabilizado));
        Assert.Empty((await svc.ListarAsync(empresa, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), adm, ct)).Valor!);
        Assert.False((await svc.ListarAsync(empresa, new DateOnly(2026, 6, 1), new DateOnly(2026, 5, 1), adm, ct)).Sucesso);
        var tabela = TabelaNotasPorCentro.Montar(linhas);
        Assert.Equal(2, tabela.Linhas.Count);                      // a linha do centro e o total
        Assert.True(tabela.Linhas[^1].Destaque);
    }
}
