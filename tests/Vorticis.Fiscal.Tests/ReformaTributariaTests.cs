using Vorticis.Fiscal.Comum;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Reforma;

namespace Vorticis.Fiscal.Tests;

/// <summary>Reforma tributária: grupo IBSCBS da NF-e lido na importação e apuração assistida de IBS e CBS (débitos − créditos).</summary>
public class ReformaTributariaTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    /// <summary>Acrescenta o grupo IBSCBS (2026: CBS 0,9%, IBS UF 0,1%) a cada item do XML de teste.</summary>
    private static string ComIbsCbs(string xml, decimal baseItem) => xml.Replace("</COFINS></imposto>",
        $"</COFINS><IBSCBS><CST>000</CST><cClassTrib>000001</cClassTrib><gIBSCBS><vBC>{baseItem:0.00}</vBC>" +
        $"<gIBSUF><pIBSUF>0.1000</pIBSUF><vIBSUF>{baseItem * 0.001m:0.00}</vIBSUF></gIBSUF><gIBSMun><pIBSMun>0.0000</pIBSMun><vIBSMun>0.00</vIBSMun></gIBSMun>" +
        $"<vIBS>{baseItem * 0.001m:0.00}</vIBS><gCBS><pCBS>0.9000</pCBS><vCBS>{baseItem * 0.009m:0.00}</vCBS></gCBS></gIBSCBS></IBSCBS></imposto>");

    [Fact]
    public void Parametros_da_transicao_trazem_2026_como_ano_de_teste()
    {
        var anos = ParametrosReforma.Carregar(ParametrosEmbutidos.Json(ServicoReformaTributaria.Chave));
        var a2026 = Assert.Single(anos, a => a.Ano == 2026);
        Assert.Equal((0.9m, 0.1m, 0m), (a2026.Cbs, a2026.IbsUf, a2026.IbsMun));
        Assert.Contains("teste", a2026.Observacao);
    }

    [Fact]
    public async Task Le_o_grupo_IBSCBS_da_NFe_e_apura_debitos_menos_creditos()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var cnpj = CnpjsDeTeste.Valido(86);
        var empresa = await db.CriarEmpresaAsync(cnpj);
        var adm = await db.AdminAsync();
        var dia = new DateOnly(2026, 4, 10);
        var venda = ComIbsCbs(XmlsDeTeste.Nfe(cnpj, XmlsDeTeste.CnpjCliente, 1, 51, dia, [new ItemNfe("5102", 10_000m)]), 10_000m);
        var compra = ComIbsCbs(XmlsDeTeste.Nfe(XmlsDeTeste.CnpjFornecedor, cnpj, 1, 52, dia, [new ItemNfe("5102", 4_000m)]), 4_000m);
        var semGrupo = XmlsDeTeste.Nfe(cnpj, XmlsDeTeste.CnpjCliente, 1, 53, dia, [new ItemNfe("5102", 1_000m)]);
        var imp = await db.App.Fiscal().Documentos.ImportarAsync(empresa,
            [new ArquivoXml("v.xml", XmlsDeTeste.Bytes(venda)), new ArquivoXml("c.xml", XmlsDeTeste.Bytes(compra)), new ArquivoXml("s.xml", XmlsDeTeste.Bytes(semGrupo))], adm, ct: ct);
        Assert.Equal(3, imp.Valor.Importados);

        var r = await db.App.Fiscal().Reforma.ApurarAsync(empresa, dia, adm, ct);
        Assert.True(r.Sucesso, string.Join(";", r.Erros));
        var a = r.Valor!;
        Assert.Equal((1, 10_000m, 90m, 10m), (a.Debitos.Notas, a.Debitos.Base, a.Debitos.Cbs, a.Debitos.IbsUf));
        Assert.Equal((1, 36m, 4m), (a.Creditos.Notas, a.Creditos.Cbs, a.Creditos.Ibs));
        Assert.Equal((54m, 6m), (a.CbsAPagar, a.IbsAPagar));
        Assert.Equal(1, a.SaidasSemGrupo);
        Assert.Equal(90m, a.CbsEsperada);
        Assert.Contains(a.Avisos(), x => x.Contains("sem o grupo IBS/CBS"));
        Assert.DoesNotContain(a.Avisos(), x => x.Contains("diferente da esperada"));
        Assert.Equal(5, a.Tabela().Linhas.Count);
        var vazio = (await db.App.Fiscal().Reforma.ApurarAsync(empresa, new DateOnly(2026, 5, 1), adm, ct)).Valor!;
        Assert.Equal((0m, 0m), (vazio.CbsAPagar, vazio.IbsAPagar));
    }
}
