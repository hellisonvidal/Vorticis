using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Vorticis.Fiscal.Sefaz;
using Vorticis.Domain.Comum;
using Vorticis.Fiscal.Documentos;

namespace Vorticis.Fiscal.Tests;

public class DistribuicaoDfeTests
{
    internal static string DocZip(string xml)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true)) gz.Write(Encoding.UTF8.GetBytes(xml));
        return Convert.ToBase64String(ms.ToArray());
    }

    internal static string Retorno(int cStat, string motivo, params (string Schema, string Xml)[] docs)
        => "<soap:Envelope xmlns:soap=\"http://www.w3.org/2003/05/soap-envelope\"><soap:Body><nfeDistDFeInteresseResponse xmlns=\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe\">"
           + $"<nfeDistDFeInteresseResult><retDistDFeInt xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"1.01\"><tpAmb>1</tpAmb><cStat>{cStat}</cStat><xMotivo>{motivo}</xMotivo>"
           + (docs.Length == 0 ? "" : "<loteDistDFeInt>" + string.Concat(docs.Select((d, i) => $"<docZip NSU=\"{i + 1:D15}\" schema=\"{d.Schema}\">{DocZip(d.Xml)}</docZip>")) + "</loteDistDFeInt>")
           + "</retDistDFeInt></nfeDistDFeInteresseResult></nfeDistDFeInteresseResponse></soap:Body></soap:Envelope>";

    [Fact]
    public void Monta_a_consulta_por_chave_e_le_o_retorno_compactado()
    {
        var chave = DigitacaoCalculosTests.ChaveValida("3526036169522700019355001000000001100000000");
        var msg = ClienteNfe.MontarConsultaChave("61695227000193", 35, chave, producao: true);
        Assert.Contains("<cUFAutor>35</cUFAutor><CNPJ>61695227000193</CNPJ><consChNFe><chNFe>" + chave, msg);
        Assert.Contains("<tpAmb>1</tpAmb>", msg);

        var r = ClienteNfe.LerDistribuicao(Retorno(138, "Documento localizado", ("procNFe_v4.00.xsd", "<nfeProc/>")));
        Assert.True(r.Localizado);
        Assert.True(r.Documentos.Single().NotaCompleta);
        Assert.Equal("<nfeProc/>", r.Documentos.Single().Xml);
        Assert.False(ClienteNfe.LerDistribuicao(Retorno(137, "Nenhum documento localizado")).Localizado);
    }

    [Fact]
    public void Valida_a_chave_e_assina_o_evento_de_ciencia()
    {
        Assert.Contains("44 dígitos", ServicoImportacaoChave.Validar("123"));
        Assert.Contains("dígito verificador", ServicoImportacaoChave.Validar(new string('1', 44)));
        var chave = DigitacaoCalculosTests.ChaveValida("3526036169522700019355001000000001100000000");
        Assert.Null(ServicoImportacaoChave.Validar(chave[..4] + " " + chave[4..]));   // espaços e pontos são ignorados

        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=EMPRESA TESTE:61695227000193", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
        var assinado = ClienteNfe.AssinarEvento(ClienteNfe.MontarCiencia("61695227000193", chave, DateTimeOffset.Now, true), cert);
        Assert.Contains($"Id=\"ID210210{chave}01\"", assinado);
        Assert.Contains($"URI=\"#ID210210{chave}01\"", assinado);
        Assert.Contains("<tpEvento>210210</tpEvento>", assinado);
        Assert.Equal(1, CodigosUf.De("pr") is 41 ? 1 : 0);
    }
}

/// <summary>Cliente da SEFAZ simulado (os testes não acessam a internet).</summary>
internal sealed class ClienteNfeFalso(RespostaDistribuicao resposta) : IClienteNfe
{
    public List<(string Cnpj, int Uf, string Chave)> Consultas { get; } = [];
    public Task<RespostaDistribuicao> ConsultarChaveAsync(string cnpj, int codigoUf, string chave, CancellationToken ct = default)
    {
        Consultas.Add((cnpj, codigoUf, chave));
        return Task.FromResult(resposta);
    }
    public Task<RespostaEvento> ManifestarCienciaAsync(string cnpj, string chave, CancellationToken ct = default) => Task.FromResult(new RespostaEvento(135, "Evento registrado"));
}

public class ImportacaoChaveIntegracaoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Importa_a_nfe_devolvida_pela_sefaz_e_avisa_quando_so_ha_resumo()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await db.CriarEmpresaAsync(XmlsDeTeste.CnpjEmpresa);
        var adm = await db.AdminAsync();
        var svc = db.App.Fiscal().ImportacaoChave;
        var chave = DigitacaoCalculosTests.ChaveValida($"352603{XmlsDeTeste.CnpjFornecedor}55001000000777100000000");
        var xml = XmlsDeTeste.Nfe(XmlsDeTeste.CnpjFornecedor, XmlsDeTeste.CnpjEmpresa, 1, 777, new DateOnly(2026, 3, 10), [new ItemNfe("5102", 500m)], chave: chave);

        var falso = new ClienteNfeFalso(ClienteNfe.LerDistribuicao(DistribuicaoDfeTests.Retorno(138, "Documento localizado", ("procNFe_v4.00.xsd", xml))));
        var r = await svc.ImportarAsync(empresa, chave, falso, adm, ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        Assert.Equal(SituacaoChave.Importada, r.Valor.Situacao);
        Assert.Equal((XmlsDeTeste.CnpjEmpresa, 35), (falso.Consultas[0].Cnpj, falso.Consultas[0].Uf));
        Assert.Equal(SituacaoChave.JaImportada, (await svc.ImportarAsync(empresa, chave, falso, adm, ct)).Valor.Situacao);

        var resumo = new ClienteNfeFalso(ClienteNfe.LerDistribuicao(DistribuicaoDfeTests.Retorno(138, "Documento localizado", ("resNFe_v1.01.xsd", "<resNFe/>"))));
        var outra = DigitacaoCalculosTests.ChaveValida($"352603{XmlsDeTeste.CnpjFornecedor}55001000000778100000000");
        Assert.Equal(SituacaoChave.SomenteResumo, (await svc.ImportarAsync(empresa, outra, resumo, adm, ct)).Valor.Situacao);
        Assert.True((await svc.ManifestarCienciaAsync(empresa, outra, resumo, adm, ct)).Sucesso);
    }
}

/// <summary>Consulta da chave para conferência (nada é gravado): primeiro as notas da empresa, depois a SEFAZ.</summary>
public class ConsultaChaveIntegracaoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Procura_primeiro_na_empresa_e_so_depois_baixa_da_sefaz_sem_gravar()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await db.CriarEmpresaAsync(XmlsDeTeste.CnpjEmpresa);
        var adm = await db.AdminAsync();
        var svc = db.App.Fiscal().ImportacaoChave;
        var chave = DigitacaoCalculosTests.ChaveValida($"352603{XmlsDeTeste.CnpjFornecedor}55001000000555100000000");
        var xml = XmlsDeTeste.Nfe(XmlsDeTeste.CnpjFornecedor, XmlsDeTeste.CnpjEmpresa, 1, 555, new DateOnly(2026, 3, 10), [new ItemNfe("5102", 80m, Icms: 14.4m)], chave: chave);

        Assert.Null((await svc.ProcurarLocalAsync(empresa, chave, adm, ct)).Valor);                           // ainda não existe na empresa
        Assert.Contains("44 dígitos", (await svc.ProcurarLocalAsync(empresa, "123", adm, ct)).Erros[0]);

        var sefaz = new ClienteNfeFalso(ClienteNfe.LerDistribuicao(DistribuicaoDfeTests.Retorno(138, "Documento localizado", ("procNFe_v4.00.xsd", xml))));
        var b = await svc.BaixarAsync(empresa, chave, sefaz, adm, ct);
        Assert.True(b.Sucesso, string.Join("; ", b.Erros));
        Assert.Equal(SituacaoChave.Baixada, b.Valor.Situacao);
        Assert.Equal((TipoOperacao.Entrada, "555", 14.4m), (b.Valor.Nota!.Operacao, b.Valor.Nota.Documento.Numero, b.Valor.Nota.Documento.Itens[0].Icms));
        Assert.Equal(XmlsDeTeste.CnpjFornecedor, b.Valor.Nota.Contraparte!.Documento);
        Assert.Equal((XmlsDeTeste.CnpjEmpresa, 35), (sefaz.Consultas.Single().Cnpj, sefaz.Consultas.Single().Uf));
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.documento WHERE empresa_id = @e", ("e", empresa)));   // só conferência

        // resumo (resNFe): falta a manifestação; nada localizado: mostra o motivo da SEFAZ
        var resumo = new ClienteNfeFalso(ClienteNfe.LerDistribuicao(DistribuicaoDfeTests.Retorno(138, "Documento localizado", ("resNFe_v1.01.xsd", "<resNFe/>"))));
        var r = await svc.BaixarAsync(empresa, chave, resumo, adm, ct);
        Assert.Equal(SituacaoChave.SomenteResumo, r.Valor.Situacao);
        Assert.Contains("manifestação", r.Valor.Mensagem);
        var vazio = new ClienteNfeFalso(ClienteNfe.LerDistribuicao(DistribuicaoDfeTests.Retorno(137, "Nenhum documento localizado")));
        Assert.Contains("137", (await svc.BaixarAsync(empresa, chave, vazio, adm, ct)).Valor.Mensagem);

        // depois de importada, a chave é achada na própria empresa (com o XML guardado) e a SEFAZ nem é consultada
        Assert.Equal(SituacaoChave.Importada, (await svc.ImportarAsync(empresa, chave, sefaz, adm, ct)).Valor.Situacao);
        var local = (await svc.ProcurarLocalAsync(empresa, chave, adm, ct)).Valor!;
        Assert.Equal(SituacaoChave.JaCadastrada, local.Situacao);
        Assert.Equal("555", local.Existente!.Numero);
        Assert.Equal(14.4m, local.Nota!.Documento.Itens[0].Icms);

        // XML de outra empresa: recusado com o motivo
        var estranho = XmlsDeTeste.Nfe("99887766000155", "55443322000199", 1, 9, new DateOnly(2026, 3, 1), [new ItemNfe("5102", 1m)]);
        Assert.Contains("não pertence à empresa", (await db.App.Fiscal().Documentos.PrepararAsync(empresa, XmlsDeTeste.Bytes(estranho), adm, ct)).Erros[0]);
        Assert.Contains("não reconhecido", (await db.App.Fiscal().Documentos.PrepararAsync(empresa, XmlsDeTeste.Bytes("<abc/>"), adm, ct)).Erros[0]);
    }

    [Fact]
    public async Task Cliente_da_sefaz_fala_com_o_endereco_informado_no_ambiente_nacional()
    {
        var listener = new System.Net.HttpListener();
        var porta = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        porta.Start();
        var n = ((System.Net.IPEndPoint)porta.LocalEndpoint).Port;
        porta.Stop();
        listener.Prefixes.Add($"http://127.0.0.1:{n}/");
        listener.Start();
        string? corpo = null, tipo = null;
        var chave = DigitacaoCalculosTests.ChaveValida("3526036169522700019355001000000001100000000");
        var servidor = Task.Run(async () =>
        {
            var c = await listener.GetContextAsync();
            tipo = c.Request.ContentType;
            corpo = await new StreamReader(c.Request.InputStream).ReadToEndAsync();
            var bytes = Encoding.UTF8.GetBytes(DistribuicaoDfeTests.Retorno(138, "Documento localizado", ("resNFe_v1.01.xsd", "<resNFe/>")));
            await c.Response.OutputStream.WriteAsync(bytes);
            c.Response.Close();
        }, TestContext.Current.CancellationToken);
        var cliente = new ClienteNfe(new HttpClient(), null, producao: true, new Uri($"http://127.0.0.1:{n}/NFeDistribuicaoDFe.asmx"));
        var r = await cliente.ConsultarChaveAsync("61695227000193", 35, chave, TestContext.Current.CancellationToken);
        await servidor;
        listener.Stop();
        Assert.True(r.Documentos.Single().Resumo);
        Assert.Contains("nfeDistDFeInteresse", corpo);
        Assert.Contains($"<consChNFe><chNFe>{chave}</chNFe></consChNFe>", corpo);
        Assert.Contains("<tpAmb>1</tpAmb>", corpo);
        Assert.Contains("soap+xml", tipo);
    }
}

/// <summary>Tributos do item: base x alíquota recalcula o valor, que continua editável.</summary>
public class ItemFormTributosTests
{
    [Fact]
    public void Base_e_aliquota_recalculam_o_valor_e_o_valor_pode_ser_sobrescrito()
    {
        var f = new Vorticis.Fiscal.Desktop.ViewModels.ItemForm();
        f.BcIcms = "200";
        Assert.Equal("", f.Icms);                 // sem alíquota, nada a calcular
        f.AliqIcms = "18";
        Assert.Equal("36", f.Icms);
        f.BcIcms = "250,50";
        Assert.Equal("45,09", f.Icms);            // 250,50 x 18% = 45,09
        f.Icms = "40";                            // correção manual vale
        Assert.Equal("40", f.Icms);
        f.AliqIcms = "12";
        Assert.Equal("30,06", f.Icms);            // mexeu na alíquota: recalcula
        (f.BcIpi, f.AliqIpi) = ("100", "5");
        (f.BcPis, f.AliqPis) = ("100", "1,65");
        (f.BcCofins, f.AliqCofins) = ("100", "7,6");
        (f.BcIss, f.AliqIss) = ("1000", "2");
        Assert.Equal(("5", "1,65", "7,6", "20"), (f.Ipi, f.Pis, f.Cofins, f.Iss));
    }

    [Fact]
    public void Carregar_um_item_nao_recalcula_nem_perde_casas_decimais()
    {
        var f = new Vorticis.Fiscal.Desktop.ViewModels.ItemForm();
        var item = new Vorticis.Fiscal.Documentos.ItemFiscal(1, "P1", "Item", "84714900", null, "1102", "UN", 3m, 0.1234567m, 0.37m, 0, 0, 0, 0, 0.37m,
            "00", "0", 100m, 18m, 17.5m, 0, 0, null, 0, 0, 0, null, 0, 0, 0, null, 0, 0, 0, 0, 0, 0);
        f.Carregar(item);
        Assert.Equal("17,5", f.Icms);             // o valor do XML (17,50) não é refeito pela fórmula (18)
        Assert.Equal("0,1234567", f.Unitario);    // quantidade/unitário mantêm as casas do XML
        Assert.Equal(item.ValorUnitario, f.Montar(null, null).ValorUnitario);
    }
}
