using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Regras;

namespace Vorticis.Fiscal.Tests;

public class MotorRegrasFiscaisTests
{
    private static RegraFiscal R(long id, string origem, string destino, int ordem = 100, long? part = null, string? ncm = null, long? prod = null, string? cst = null,
                                 bool ativo = true, bool pis = true) =>
        new(id, TipoOperacao.Entrada, ordem, ativo, null, origem, part, ncm, prod, cst, null, destino, null, CreditaPis: pis);

    private static readonly ItemParaRegra Item = new("5102", 7, "84714900", 9, "00");

    [Fact]
    public void Prioridade_produto_vence_ncm_vence_participante_vence_so_cfop()
    {
        var regras = new[] { R(1, "5102", "1102"), R(2, "5102", "1111", part: 7), R(3, "5102", "1122", ncm: "8471"), R(4, "5102", "1101", prod: 9) };
        Assert.Equal(4, MotorRegrasFiscais.DecidirEntrada(regras, Item).RegraId);
        Assert.Equal(3, MotorRegrasFiscais.DecidirEntrada(regras.Where(r => r.Id != 4), Item).RegraId);
        Assert.Equal(2, MotorRegrasFiscais.DecidirEntrada(regras.Where(r => r.Id is 1 or 2), Item).RegraId);
        Assert.Equal(1, MotorRegrasFiscais.DecidirEntrada(regras.Where(r => r.Id == 1), Item).RegraId);
    }

    [Fact]
    public void Empate_usa_ncm_mais_longo_depois_a_ordem_manual()
    {
        var curto = R(1, "5102", "1102", ncm: "84");
        var longo = R(2, "5102", "1122", ncm: "847149");
        Assert.Equal(2, MotorRegrasFiscais.DecidirEntrada([curto, longo], Item).RegraId);
        Assert.Equal(11, MotorRegrasFiscais.DecidirEntrada([R(10, "5102", "1102", ordem: 50, part: 7), R(11, "5102", "1111", ordem: 10, part: 7)], Item).RegraId);
    }

    [Fact]
    public void Condicoes_que_nao_casam_e_regra_inativa_sao_ignoradas()
    {
        var regras = new[] { R(1, "5102", "1111", part: 8), R(2, "5102", "1122", ncm: "9999"), R(3, "5102", "1133", cst: "10"), R(4, "5102", "1144", ativo: false) };
        var d = MotorRegrasFiscais.DecidirEntrada(regras, Item);
        Assert.True(d.SemRegra);
        Assert.Null(d.RegraId);
        Assert.Equal("1102", d.Cfop);   // conversão padrão
    }

    [Fact]
    public void Sem_regra_usa_conversao_padrao_e_marca_para_revisao()
    {
        Assert.Equal(("1102", true), Pair(MotorRegrasFiscais.DecidirEntrada([], new("5102", null, null, null, null))));
        Assert.Equal(("2403", true), Pair(MotorRegrasFiscais.DecidirEntrada([], new("6403", null, null, null, null))));
        Assert.Equal(("3101", true), Pair(MotorRegrasFiscais.DecidirEntrada([], new("7101", null, null, null, null))));
        Assert.Equal(("NFSE", false), Pair(MotorRegrasFiscais.DecidirEntrada([], new("NFSE", null, null, null, null))));
        Assert.Equal(("1102", false), Pair(MotorRegrasFiscais.DecidirEntrada([], new("1102", null, null, null, null))));
        static (string, bool) Pair(DecisaoFiscal d) => (d.Cfop, d.SemRegra);
    }

    [Fact]
    public void Regra_traz_cst_creditos_contas_e_centro()
    {
        var regra = R(1, "5405", "1403", pis: false) with { CstDestino = "60", CreditaIcms = false, ContaDebito = "1.1.04.001", CentroCustoId = 3 };
        var d = MotorRegrasFiscais.DecidirEntrada([regra], new("5405", null, null, null, null));
        Assert.Equal(("1403", "60", false, true, false, true, "1.1.04.001", 3L, false), (d.Cfop, d.CstIcms, d.CreditaIcms, d.CreditaIpi, d.CreditaPis, d.CreditaCofins, d.ContaDebito, d.CentroCustoId, d.SemRegra));
    }

    [Fact]
    public void Regras_padrao_so_trazem_correspondencias_conferidas()
    {
        var p = MotorRegrasFiscais.RegrasPadrao;
        Assert.Contains(("5102", "1102"), p);
        Assert.Contains(("6102", "2102"), p);
        Assert.Contains(("5101", "1101"), p);
        Assert.Contains(("5405", "1403"), p);
        Assert.Contains(("6405", "2403"), p);
        Assert.Contains(("5933", "1933"), p);
        Assert.Contains(("5949", "1949"), p);
        Assert.Equal(p.Count, p.Distinct().Count());
        Assert.All(p, x => { Assert.Equal(TipoOperacao.Saida, Cfop.Operacao(x.Origem)); Assert.Equal(TipoOperacao.Entrada, Cfop.Operacao(x.Destino)); });
        Assert.DoesNotContain(p, x => x.Origem[0] == '7');
        Assert.DoesNotContain(p, x => x.Origem == "5556");   // sem correspondência segura
    }

    [Fact]
    public void Validacao_exige_cfops_coerentes()
    {
        Assert.False(MotorRegrasFiscais.Validar(R(0, "1102", "1102")).Sucesso);
        Assert.False(MotorRegrasFiscais.Validar(R(0, "5102", "5102")).Sucesso);
        var ok = MotorRegrasFiscais.Validar(R(0, "5.102", "1102", ncm: "8471.49"));
        Assert.True(ok.Sucesso);
        Assert.Equal(("5102", "847149"), (ok.Valor.CfopOrigem, ok.Valor.Ncm));
        var saida = new RegraFiscal(0, TipoOperacao.Saida, 100, true, null, null, null, null, null, null, "X", null, null);
        Assert.False(MotorRegrasFiscais.Validar(saida).Sucesso);   // sem CFOP nem CST a sugerir
        Assert.True(MotorRegrasFiscais.Validar(saida with { CfopDestino = "6102" }).Sucesso);
    }

    [Fact]
    public void Saida_casa_por_destino_interno_ou_interestadual()
    {
        var interna = new RegraFiscal(1, TipoOperacao.Saida, 100, true, null, null, null, null, 9, null, "I", "5102", "00");
        var externa = interna with { Id = 2, Destino = "X", CfopDestino = "6102" };
        Assert.Equal(2, MotorRegrasFiscais.SugerirSaida([interna, externa], new("", null, null, 9, null, "X"))!.Id);
        Assert.Equal(1, MotorRegrasFiscais.SugerirSaida([interna, externa], new("", null, null, 9, null, "I"))!.Id);
        Assert.Null(MotorRegrasFiscais.SugerirSaida([interna, externa], new("", null, null, 10, null, "I")));
    }
}

public class RegrasFiscaisIntegracaoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private const string Empresa = "11222333000181";
    private static readonly DateOnly Mar = new(2026, 3, 10);

    private static byte[] Xml(int numero, params ItemNfe[] itens) =>
        XmlsDeTeste.Bytes(XmlsDeTeste.Nfe(XmlsDeTeste.CnpjFornecedor, Empresa, 1, numero, Mar, itens));

    [Fact]
    public async Task Importacao_aplica_regras_marca_sem_regra_e_reaplica()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await db.CriarEmpresaAsync(Empresa);
        var adm = await db.AdminAsync();
        var f = db.App.Fiscal();

        // regra específica: 5.102 deste fornecedor vira 1.556, sem crédito de PIS/COFINS e de IPI
        var forn = await db.EscalarAsync<long>("INSERT INTO core.participante (empresa_id, codigo, nome, cpf_cnpj, uf, fornecedor) VALUES (@e, '9001', 'Fornecedor Regra', @d, 'SP', TRUE) RETURNING id",
                                               ("e", empresa), ("d", XmlsDeTeste.CnpjFornecedor));
        var gravada = await f.Regras.GravarAsync(empresa, new RegraFiscal(0, TipoOperacao.Entrada, 10, true, "Uso e consumo", "5102", forn, null, null, null, null, "1556", "90",
            CreditaIpi: false, CreditaPis: false, CreditaCofins: false), adm, ct);
        Assert.True(gravada.Sucesso, string.Join(";", gravada.Erros));

        var r = await f.Documentos.ImportarAsync(empresa,
        [
            new ArquivoXml("a.xml", Xml(7001, new ItemNfe("5102", 100m, Icms: 18m, Ipi: 5m, Pis: 1.65m, Cofins: 7.6m), new ItemNfe("6403", 50m))),
        ], adm, ct: ct);
        Assert.Equal(1, r.Valor.Importados);

        var doc = (await f.Documentos.ConsultarAsync(empresa, new FiltroDocumentos(Operacao: TipoOperacao.Entrada), adm, ct)).Valor.Single();
        var itens = (await f.Documentos.ItensAsync(empresa, doc.Id, adm, ct)).Valor;
        Assert.Equal(["1556", "2403"], itens.Select(i => i.Cfop));
        Assert.Equal(["5102", "6403"], itens.Select(i => i.CfopOriginal));
        Assert.Equal("90", itens[0].CstIcms);
        Assert.Equal((18m, 0m, 0m, 0m), (itens[0].Icms, itens[0].Ipi, itens[0].Pis, itens[0].Cofins));   // créditos zerados na escrituração
        Assert.False(itens[0].SemRegra);
        Assert.True(itens[1].SemRegra);
        Assert.True(doc.SemRegra);
        Assert.Single((await f.Documentos.ConsultarAsync(empresa, new FiltroDocumentos(SemRegra: true), adm, ct)).Valor);

        // regras padrão (6.403 -> 2.403) e reaplicar: o item sem regra passa a ter regra
        var padrao = await f.Regras.GerarPadraoAsync(empresa, adm, ct);
        Assert.True(padrao.Valor > 50);
        Assert.Equal(0, (await f.Regras.GerarPadraoAsync(empresa, adm, ct)).Valor);   // não duplica
        var re = (await f.Regras.ReaplicarAsync(empresa, Mar, Mar, adm, ct)).Valor;
        Assert.Equal((1, 2, 0), (re.Notas, re.ItensComRegra, re.ItensSemRegra));
        itens = (await f.Documentos.ItensAsync(empresa, doc.Id, adm, ct)).Valor;
        Assert.Equal(["1556", "2403"], itens.Select(i => i.Cfop));   // a regra específica continua vencendo a padrão
        Assert.All(itens, i => Assert.False(i.SemRegra));
        Assert.Empty((await f.Documentos.ConsultarAsync(empresa, new FiltroDocumentos(SemRegra: true), adm, ct)).Valor);

        // desligar o crédito de ICMS na regra e reaplicar zera o ICMS do item
        var regra = (await f.Regras.ListarAsync(empresa, TipoOperacao.Entrada, adm, ct)).Valor.Single(x => x.ParticipanteId == forn);
        Assert.True((await f.Regras.GravarAsync(empresa, regra with { CreditaIcms = false }, adm, ct)).Sucesso);
        await f.Regras.ReaplicarAsync(empresa, Mar, Mar, adm, ct);
        Assert.Equal(0m, (await f.Documentos.ItensAsync(empresa, doc.Id, adm, ct)).Valor[0].Icms);
    }

    [Fact]
    public async Task Importacao_pelo_popup_grava_como_xml_guarda_o_original_e_nao_duplica()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        const string cnpj = "60746948000112";
        var empresa = await db.CriarEmpresaAsync(cnpj);
        var adm = await db.AdminAsync();
        var f = db.App.Fiscal();
        var bytes = XmlsDeTeste.Bytes(XmlsDeTeste.Nfe(XmlsDeTeste.CnpjFornecedor, cnpj, 1, 7101, Mar, [new ItemNfe("5102", 80m)]));

        var prep = await f.Documentos.PrepararAsync(empresa, bytes, adm, ct);
        var r = await f.Documentos.ImportarPreparadaAsync(empresa, prep.Valor, adm, ct);
        Assert.True(r.Sucesso, string.Join(";", r.Erros));
        Assert.False(r.Valor.JaExistia);

        var doc = (await f.Documentos.LocalizarPorChaveAsync(empresa, prep.Valor.Documento.Chave, adm, ct)).Valor!;
        Assert.Equal(("XML", false), (doc.Origem, doc.Digitado));
        Assert.Equal(r.Valor.DocumentoId, doc.Id);
        Assert.Equal(prep.Valor.Documento.Chave, doc.Chave);
        Assert.Equal(bytes, (await f.Documentos.ObterXmlAsync(empresa, doc.Id, adm, ct)).Valor);   // XML original guardado

        // só a escrituração pode mudar (CFOP, CST e créditos); os valores do XML permanecem
        var esc = new EscrituracaoDocumento(doc.Id, null, null, null, null, [new EscrituracaoItem(1, "1556", null, null, "90", CreditaPis: false)]);
        Assert.True((await f.Digitacao.GravarEscrituracaoAsync(empresa, esc, adm, ct)).Sucesso);
        var item = (await f.Documentos.ItensAsync(empresa, doc.Id, adm, ct)).Valor.Single();
        Assert.Equal(("1556", "90", 80m, false), (item.Cfop, item.CstIcms, item.ValorProduto, item.CreditaPis));
        Assert.False(item.SemRegra);   // conferido

        // mesma chave: avisa e devolve a existente
        var de_novo = await f.Documentos.ImportarPreparadaAsync(empresa, prep.Valor, adm, ct);
        Assert.True(de_novo.Valor.JaExistia);
        Assert.Equal(doc.Id, de_novo.Valor.DocumentoId);
        Assert.Contains("já está lançada", de_novo.Valor.Aviso);
        Assert.Single((await f.Documentos.ConsultarAsync(empresa, new FiltroDocumentos(), adm, ct)).Valor);
    }
}
