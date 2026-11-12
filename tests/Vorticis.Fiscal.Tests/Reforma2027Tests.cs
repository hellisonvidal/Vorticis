using Vorticis.Fiscal.Comum;
using Vorticis.Fiscal.Contabilizacao;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Reforma;

namespace Vorticis.Fiscal.Tests;

/// <summary>Reforma tributária a partir de 2027: parâmetros, cálculo do IBS/CBS/IS do item, contabilização nota a nota e encerramento da apuração.</summary>
public class Reforma2027Tests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private static ItemFiscal Item(string cfop, decimal valor, decimal icms = 0, TributosReforma? reforma = null, bool credita = true) =>
        new(1, null, null, null, null, cfop, null, 1, valor, valor, 0, 0, 0, 0, valor, null, null, 0, 0, icms, 0, 0, null, 0, 0, 0, null, 0, 0, 0, null, 0, 0, 0, 0, 0, 0,
            Reforma: reforma, CreditaIbsCbs: credita);

    [Fact]
    public void Parametros_de_2027_tem_CBS_plena_PIS_COFINS_extintos_e_Imposto_Seletivo()
    {
        var json = ParametrosEmbutidos.Json(ServicoReformaTributaria.Chave);
        var anos = ParametrosReforma.Carregar(json);
        var a2026 = anos.Single(a => a.Ano == 2026);
        Assert.Equal((true, true, false, false), (a2026.PisCofins, a2026.Ipi, a2026.ImpostoSeletivo, a2026.Contabiliza));
        var a2027 = anos.Single(a => a.Ano == 2027);
        Assert.Equal((11.2m, 0.05m, 0.05m), (a2027.Cbs, a2027.IbsUf, a2027.IbsMun));
        Assert.Equal((false, false, true, true), (a2027.PisCofins, a2027.Ipi, a2027.ImpostoSeletivo, a2027.Contabiliza));
        Assert.Contains(anos, a => a.Ano == 2033);
        var cst = ParametrosReforma.CarregarCst(json);
        Assert.Contains(cst, c => c is { Codigo: "000", Tributa: true });
        Assert.Contains(cst, c => c is { Codigo: "410", Tributa: false });
    }

    [Fact]
    public void Calcula_IBS_CBS_com_reducao_e_sem_ICMS_na_base_e_o_IS_so_a_partir_de_2027()
    {
        var a2027 = new AliquotasReforma(2027, 11.2m, 0.05m, 0.05m, null, false, false, true, true);
        var item = Item("5102", 1_000m, icms: 180m);
        Assert.Equal(820m, CalculadoraReforma.Base(item));                                      // sem o ICMS
        var integral = CalculadoraReforma.Calcular(1_000m, a2027, "000", "000001", 0, true, "000", "000001", 10m);
        Assert.Equal((112m, 0.50m, 0.50m, 100m), (integral.Cbs, integral.IbsUf, integral.IbsMun, integral.ImpostoSeletivo));
        var reduzida = CalculadoraReforma.Calcular(1_000m, a2027, "200", "200999", 60m, true);
        Assert.Equal((4.48m, 44.80m), (reduzida.AliqCbs, reduzida.Cbs));                         // 11,2% × 40%
        var isenta = CalculadoraReforma.Calcular(1_000m, a2027, "410", null, 0, false);
        Assert.Equal(0m, isenta.IbsCbs);
        var teste = CalculadoraReforma.Calcular(1_000m, new AliquotasReforma(2026, 0.9m, 0.1m, 0, null), "000", null, 0, true, "000", null, 10m);
        Assert.Equal((9m, 0m), (teste.Cbs, teste.ImpostoSeletivo));                              // 2026: sem Imposto Seletivo
        var avisos = CalculadoraReforma.Avisos([item with { Pis = 16.5m }], a2027);
        Assert.Contains(avisos, x => x.Contains("PIS e a COFINS foram substituídos"));
        Assert.Contains(avisos, x => x.Contains("sem o CST do IBS/CBS"));
    }

    [Fact]
    public void Contabiliza_IBS_CBS_por_fora_na_saida_e_o_credito_na_entrada()
    {
        var contas = new ContasReforma("1.1.05.001", "2.1.03.001", "1.1.05.002", "2.1.03.002", "3.1.02.001", "2.1.03.003");
        var tributos = new TributosReforma("000", "000001", 1_000m, 0.05m, 0.50m, 0.05m, 0.50m, 11.2m, 112m, AliqIs: 10m, ImpostoSeletivo: 100m);
        DocumentoFiscal Doc(TipoOperacao op) => new(10, ModeloDocumento.NFe55, new string('1', 44), op, "321", "1", new DateOnly(2027, 3, 9), new DateOnly(2027, 3, 10), null,
            SituacaoDocumento.Autorizada, new ParteDocumento("1", "Parte", "SP"), new ParteDocumento("2", "Eu", "RJ"), 5, "7",
            new TotaisDocumento(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, 0, 0, 0, 0), null, false);
        var venda = new[] { new RegraContabilizacao(1, 100, null, "5102", null, null, "1.1.02.001", "3.1.01.001", "NF {DOCUMENTO}") };
        var s = MotorContabilizacao.Montar(1, Doc(TipoOperacao.Saida), [Item("5102", 1_000m, reforma: tributos)], venda, reforma: contas).Lancamento!;
        Assert.Equal(s.Partidas.Where(p => p.DC == Vorticis.Domain.Contabil.IndicadorDC.D).Sum(p => p.Valor), s.Partidas.Where(p => p.DC == Vorticis.Domain.Contabil.IndicadorDC.C).Sum(p => p.Valor));
        Assert.Contains(s.Partidas, p => p is { CodigoConta: "2.1.03.002", Valor: 112m });              // CBS a recolher
        Assert.Contains(s.Partidas, p => p is { CodigoConta: "2.1.03.001", Valor: 1.00m });             // IBS a recolher
        Assert.Contains(s.Partidas, p => p is { CodigoConta: "1.1.02.001", Valor: 113.00m });           // cliente: + IBS/CBS
        Assert.Contains(s.Partidas, p => p is { CodigoConta: "3.1.02.001", Valor: 100m });              // Imposto Seletivo (despesa)

        var compra = new[] { new RegraContabilizacao(1, 100, null, "1102", null, null, "1.1.04.001", "2.1.01.001", "NF {DOCUMENTO}") };
        var e = MotorContabilizacao.Montar(1, Doc(TipoOperacao.Entrada), [Item("1102", 1_000m, reforma: tributos)], compra, reforma: contas).Lancamento!;
        Assert.Contains(e.Partidas, p => p is { CodigoConta: "1.1.05.002", Valor: 112m });              // CBS a recuperar
        Assert.Contains(e.Partidas, p => p is { CodigoConta: "2.1.01.001", Valor: 113.00m });           // fornecedor
        Assert.DoesNotContain(e.Partidas, p => p.CodigoConta == "3.1.02.001");                           // IS não gera crédito
        var semCredito = MotorContabilizacao.Montar(1, Doc(TipoOperacao.Entrada), [Item("1102", 1_000m, reforma: tributos, credita: false)], compra, reforma: contas).Lancamento!;
        Assert.Equal(2, semCredito.Partidas.Count);                                                        // uso e consumo: sem crédito
        var semContas = MotorContabilizacao.Montar(1, Doc(TipoOperacao.Saida), [Item("5102", 1_000m, reforma: tributos)], venda, reforma: new ContasReforma(null, null, null, null, null, null));
        Assert.Contains(semContas.Erros, x => x.Contains("CBS a recolher"));
        Assert.Equal(2, MotorContabilizacao.Montar(1, Doc(TipoOperacao.Saida), [Item("5102", 1_000m, reforma: tributos)], venda).Lancamento!.Partidas.Count);   // 2026: sem
    }

    [Fact]
    public void Classificacao_tributaria_valida_codigo_cst_e_reducao()
    {
        Assert.Empty(ServicoClassificacaoTributaria.Validar(new ClassificacaoTributaria("200034", "200", "Redução de 60%", 60m)));
        Assert.Equal(4, ServicoClassificacaoTributaria.Validar(new ClassificacaoTributaria("2000", "20", " ", 120m)).Count);
    }

    [Fact]
    public async Task Encerra_a_apuracao_de_2027_e_leva_o_saldo_credor_para_o_mes_seguinte()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var cnpj = CnpjsDeTeste.Valido(98);
        var empresa = await db.CriarEmpresaAsync(cnpj);
        var adm = await db.AdminAsync();
        var fiscal = db.App.Fiscal();
        string Xml(string emit, string dest, int n, DateOnly dia, decimal valor, decimal cbs, decimal ibs) => XmlsDeTeste.Nfe(emit, dest, 1, n, dia, [new ItemNfe("5102", valor)])
            .Replace("</COFINS></imposto>", $"</COFINS><IBSCBS><CST>000</CST><cClassTrib>000001</cClassTrib><gIBSCBS><vBC>{valor:0.00}</vBC>" +
                $"<gIBSUF><pIBSUF>0.0500</pIBSUF><vIBSUF>{ibs / 2:0.00}</vIBSUF></gIBSUF><gIBSMun><pIBSMun>0.0500</pIBSMun><vIBSMun>{ibs / 2:0.00}</vIBSMun></gIBSMun>" +
                $"<vIBS>{ibs:0.00}</vIBS><gCBS><pCBS>11.2000</pCBS><vCBS>{cbs:0.00}</vCBS></gCBS></gIBSCBS></IBSCBS></imposto>");
        var marco = new DateOnly(2027, 3, 10);
        // março: compras maiores que as vendas (saldo credor); abril: vendas
        var arquivos = new[]
        {
            new ArquivoXml("c1.xml", XmlsDeTeste.Bytes(Xml(XmlsDeTeste.CnpjFornecedor, cnpj, 71, marco, 10_000m, 1_120m, 10m))),
            new ArquivoXml("v1.xml", XmlsDeTeste.Bytes(Xml(cnpj, XmlsDeTeste.CnpjCliente, 72, marco, 5_000m, 560m, 5m))),
            new ArquivoXml("v2.xml", XmlsDeTeste.Bytes(Xml(cnpj, XmlsDeTeste.CnpjCliente, 73, marco.AddMonths(1), 20_000m, 2_240m, 20m))),
        };
        Assert.Equal(3, (await fiscal.Documentos.ImportarAsync(empresa, arquivos, adm, ct: ct)).Valor.Importados);

        var marcoAp = (await fiscal.Reforma.ApurarAsync(empresa, marco, adm, ct)).Valor!;
        Assert.False(marcoAp.Informativa);
        Assert.Equal((0m, 560m, 560m), (marcoAp.CbsAPagar, marcoAp.CbsCredor, marcoAp.CompensadoCbs));
        var enc = await fiscal.Reforma.EncerrarAsync(empresa, marco, contabilizar: false, adm, ct);
        Assert.True(enc.Sucesso, string.Join(";", enc.Erros));
        Assert.NotNull(enc.Valor!.Encerrada);
        Assert.False((await fiscal.Reforma.EncerrarAsync(empresa, marco, false, adm, ct)).Sucesso);   // já encerrada

        var abril = (await fiscal.Reforma.ApurarAsync(empresa, marco.AddMonths(1), adm, ct)).Valor!;
        Assert.Equal((560m, 5m), (abril.SaldoAnteriorCbs, abril.SaldoAnteriorIbs));
        Assert.Equal((2_240m - 560m, 20m - 5m), (abril.CbsAPagar, abril.IbsAPagar));
        Assert.False((await fiscal.Reforma.EncerrarAsync(empresa, marco.AddMonths(1), contabilizar: true, adm, ct)).Sucesso);   // sem as contas do IBS/CBS
        Assert.False((await fiscal.Reforma.ReabrirAsync(empresa, new DateOnly(2027, 1, 1), adm, ct)).Sucesso);
        Assert.True((await fiscal.Reforma.ReabrirAsync(empresa, marco, adm, ct)).Sucesso);
        Assert.Equal(0m, (await fiscal.Reforma.ApurarAsync(empresa, marco.AddMonths(1), adm, ct)).Valor!.SaldoAnteriorCbs);
        Assert.False((await fiscal.Reforma.EncerrarAsync(empresa, new DateOnly(2026, 5, 1), false, adm, ct)).Sucesso);   // 2026 é informativo
    }
}

/// <summary>Telas da reforma: o lançamento da nota calcula o IBS/CBS/IS pela classificação; a classificação tributária é cadastrada no Fiscal.</summary>
public class Reforma2027TelasTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Item_da_nota_calcula_IBS_CBS_IS_pela_classificacao_e_a_tela_cadastra_classificacoes()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await db.CriarEmpresaAsync(CnpjsDeTeste.Valido(97));
        var adm = await db.AdminAsync();
        var ctx = new ContextoDeTeste(db.App, adm, empresa);

        var tela = new Vorticis.Fiscal.Desktop.ViewModels.ClassificacaoTributariaViewModel(ctx);
        await tela.CarregarAsync();
        Assert.Contains(tela.Lista, l => l.Codigo == "000001");
        tela.NovaCommand.Execute(null);
        (tela.Codigo, tela.Descricao, tela.Reducao, tela.GeraCredito) = ("200097", "Teste: alíquota reduzida em 60%", "60", false);
        tela.Cst = tela.Csts.Single(c => c.Codigo == "200");
        await tela.GravarCommand.ExecuteAsync(null);
        Assert.Empty(tela.Erros);
        Assert.Equal(("200", "60%", "Não"), (tela.Selecionada!.Cst, tela.Selecionada.Reducao, tela.Selecionada.Credito));

        var nota = new Vorticis.Fiscal.Desktop.ViewModels.NotaEntradaViewModel(ctx) { DataLivro = new DateTime(2027, 3, 10) };
        (nota.Item.Quantidade, nota.Item.Unitario, nota.Item.Icms, nota.Item.ClassTrib) = ("1", "1.000,00", "180,00", "200097");
        await nota.CalcularReformaCommand.ExecuteAsync(null);
        Assert.Equal("200", nota.Item.CstIbsCbs);
        Assert.Equal("820", nota.Item.BcIbsCbs);                     // sem o ICMS na base
        Assert.Equal("4,48", nota.Item.AliqCbs);                      // 11,2% × 40%
        Assert.Equal("36,74", nota.Item.Cbs);
        Assert.False(nota.Item.CreditaIbsCbs);                        // a classificação não gera crédito
        var item = nota.Item.Montar(null, null);
        Assert.Equal(("200", 36.74m, false), (item.Reforma!.Cst, item.Reforma.Cbs, item.CreditaIbsCbs));
        await tela.ExcluirCommand.ExecuteAsync(null);
        Assert.DoesNotContain(tela.Lista, l => l.Codigo == "200097");
    }
}
