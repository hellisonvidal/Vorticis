using Vorticis.Domain.Comum;
using Vorticis.Domain.Contabil;

namespace Vorticis.Domain.Tests;

public class ImportacaoLancamentosTests
{
    [Fact]
    public void Uma_linha_por_lancamento_com_conta_fixa_e_valor_negativo()
    {
        var t = TabelaImportada.DeTexto("""
            Data;Débito;Crédito;Valor;Histórico;NF
            10/03/2026;1101002;3101001;1.500,00;Venda;123
            11/03/2026;4202001;;-200,00;;
            ;;;;;
            """);
        var layout = new LayoutImportacao
        {
            Nome = "Vendas", ColunaData = "Data", ColunaContaDebito = "débito", ColunaContaCredito = "Crédito", ContaCreditoPadrao = "1.1.01.002",
            ColunaValor = "Valor", ColunaHistorico = "Histórico", ColunaDocumento = "NF", MascaraContas = "9.9.99.999",
        };
        var r = ImportadorLancamentos.Ler(t, layout);
        Assert.Empty(r.Erros);
        Assert.Equal(2, r.Lancamentos.Count);
        var venda = r.Lancamentos[0];
        Assert.Equal((new DateOnly(2026, 3, 10), 1500m, 2), (venda.Data, venda.Total, venda.Linha));
        Assert.Equal(("1.1.01.002", IndicadorDC.D, "Venda", "123"), (venda.Partidas[0].CodigoConta, venda.Partidas[0].DC, venda.Partidas[0].Historico, venda.Partidas[0].NumeroArquivo));
        Assert.Equal("3.1.01.001", venda.Partidas[1].CodigoConta);
        // valor negativo inverte: crédito na despesa, débito no banco (conta fixa)
        var estorno = r.Lancamentos[1];
        Assert.Equal(("1.1.01.002", "4.2.02.001", 200m), (estorno.Partidas[0].CodigoConta, estorno.Partidas[1].CodigoConta, estorno.Total));
        Assert.Equal("Importado de planilha", estorno.Partidas[0].Historico);
    }

    [Fact]
    public void Layout_padrao_agrupa_partidas_pelo_numero()
    {
        var t = TabelaImportada.DeTexto("""
            LANCAMENTO;DATA;CONTA;DC;VALOR;HISTORICO;CENTRO_CUSTO;DOCUMENTO
            1;05/03/2026;4.2.02.001;D;1000,00;Aluguel março;ADM;
            1;05/03/2026;1.1.01.002;C;1000,00;Aluguel março;;
            2;06/03/2026;1.1.01.001;D;300,00;Venda;;77
            2;06/03/2026;3.1.01.001;C;300,00;Venda;;77
            """);
        Assert.Same(LayoutImportacao.PadraoAnterior, LayoutImportacao.PadraoPara(t));      // planilha antiga: CONTA e DC
        var r = ImportadorLancamentos.Ler(t, LayoutImportacao.PadraoPara(t));
        Assert.Empty(r.Erros);
        Assert.Equal(2, r.Lancamentos.Count);
        Assert.Equal("ADM", r.Lancamentos[0].Partidas[0].CodigoCentroCusto);
        Assert.Equal((4, 300m, "77"), (r.Lancamentos[1].Linha, r.Lancamentos[1].Total, r.Lancamentos[1].Partidas[0].NumeroArquivo));
    }

    [Fact]
    public void Layout_padrao_tem_debito_e_credito_em_colunas_separadas()
    {
        Assert.Equal(["DATA", "DEBITO", "CREDITO", "VALOR", "HISTORICO", "CENTRO_CUSTO", "DOCUMENTO"], LayoutImportacao.ColunasPadrao);
        var t = TabelaImportada.DeTexto("""
            DATA;DEBITO;CREDITO;VALOR;HISTORICO;CENTRO_CUSTO;DOCUMENTO
            05/03/2026;4.2.02.001;1.1.01.002;1000,00;Aluguel março;ADM;123
            ;;;;;;
            """);
        Assert.Same(LayoutImportacao.Padrao, LayoutImportacao.PadraoPara(t));
        var r = ImportadorLancamentos.Ler(t, LayoutImportacao.Padrao);
        Assert.Empty(r.Erros);
        var l = Assert.Single(r.Lancamentos);
        Assert.Equal([("4.2.02.001", IndicadorDC.D), ("1.1.01.002", IndicadorDC.C)], l.Partidas.Select(p => (p.CodigoConta, p.DC)));
        Assert.All(l.Partidas, p => Assert.Equal((1000m, "ADM", "123", "Aluguel março"), (p.Valor, p.CodigoCentroCusto, p.NumeroArquivo, p.Historico)));
    }

    [Fact]
    public void Linha_so_com_debito_ou_so_com_credito_agrupa_por_documento_e_data_ate_fechar()
    {
        var t = TabelaImportada.DeTexto("""
            DATA;DEBITO;CREDITO;VALOR;HISTORICO;CENTRO_CUSTO;DOCUMENTO
            06/03/2026;1.1.01.001;;300,00;Venda;;77
            06/03/2026;;3.1.01.001;200,00;Venda;;77
            06/03/2026;;3.1.01.002;100,00;Venda;;77
            07/03/2026;4.2.02.001;1.1.01.002;50,00;Tarifa;;
            08/03/2026;1.1.01.002;;80,00;Depósito;;88
            08/03/2026;;1.1.01.001;80,00;Depósito;;88
            09/03/2026;1.1.01.001;;10,00;Sem contrapartida;;99
            """);
        var r = ImportadorLancamentos.Ler(t, LayoutImportacao.Padrao);
        Assert.Empty(r.Erros);
        Assert.Equal([3, 2, 2, 1], r.Lancamentos.Select(l => l.Partidas.Count));              // o último fica aberto: a validação aponta o desbalanceamento
        Assert.Equal([2, 5, 6, 8], r.Lancamentos.Select(l => l.Linha));
        var venda = r.Lancamentos[0];
        Assert.Equal((300m, 300m), (venda.Partidas.Where(p => p.DC == IndicadorDC.D).Sum(p => p.Valor), venda.Partidas.Where(p => p.DC == IndicadorDC.C).Sum(p => p.Valor)));
        Assert.All(venda.Partidas, p => Assert.Equal("77", p.NumeroArquivo));
        var solto = r.Lancamentos[^1];
        Assert.Equal((new DateOnly(2026, 3, 9), IndicadorDC.D, 10m), (solto.Data, solto.Partidas[0].DC, solto.Partidas[0].Valor));
    }

    [Fact]
    public void Partidas_simples_de_documentos_ou_datas_diferentes_nao_se_misturam()
    {
        var t = TabelaImportada.DeTexto("""
            DATA;DEBITO;CREDITO;VALOR;HISTORICO;CENTRO_CUSTO;DOCUMENTO
            06/03/2026;1.1.01.001;;300,00;;;77
            06/03/2026;;3.1.01.001;300,00;;;78
            07/03/2026;;3.1.01.001;300,00;;;78
            06/03/2026;;1.1.01.001;-50,00;;;5
            06/03/2026;;3.1.01.001;50,00;;;5
            """);
        var r = ImportadorLancamentos.Ler(t, LayoutImportacao.Padrao);
        Assert.Empty(r.Erros);
        Assert.Equal([1, 1, 1, 2], r.Lancamentos.Select(l => l.Partidas.Count));            // 77 e 78 ficam abertos (erro na validação); crédito de -50 vira débito e fecha com o crédito de 50
        Assert.Equal([IndicadorDC.D, IndicadorDC.C], r.Lancamentos[3].Partidas.Select(p => p.DC));
    }

    [Fact]
    public void Layout_padrao_sem_nenhuma_conta_na_linha_da_erro_em_portugues()
    {
        var t = TabelaImportada.DeTexto("""
            DATA;DEBITO;CREDITO;VALOR;HISTORICO;CENTRO_CUSTO;DOCUMENTO
            06/03/2026;;;300,00;;;
            """);
        var r = ImportadorLancamentos.Ler(t, LayoutImportacao.Padrao);
        Assert.Equal(["Linha 2: informe a conta de débito, a de crédito ou as duas."], r.Erros);
        // um layout próprio, sem partida simples, mantém a mensagem de sempre
        var proprio = new LayoutImportacao { ColunaData = "DATA", ColunaContaDebito = "DEBITO", ColunaContaCredito = "CREDITO", ColunaValor = "VALOR" };
        Assert.Equal(["Linha 2: informe a conta débito."], ImportadorLancamentos.Ler(t, proprio).Erros);
    }

    [Fact]
    public void Sem_numero_agrupa_ate_fechar_debito_e_credito_e_aponta_erros()
    {
        var t = TabelaImportada.DeTexto("""
            Data;Conta;Débito;Crédito;Histórico
            01/03/2026;1.1.01.001;100,00;;A
            01/03/2026;3.1.01.001;;60,00;A
            01/03/2026;3.1.01.002;;40,00;A
            02/03/2026;1.1.01.001;50,00;;B
            02/03/2026;3.1.01.001;;50,00;B
            xx;1.1.01.001;1;;C
            """);
        var layout = new LayoutImportacao { Modelo = ModeloLayout.UmaLinhaPorPartida, ColunaData = "Data", ColunaConta = "Conta",
                                            ColunaValorDebito = "Débito", ColunaValorCredito = "Crédito", ColunaHistorico = "Histórico" };
        var r = ImportadorLancamentos.Ler(t, layout);
        Assert.Equal(["Linha 7: data inválida \"xx\"."], r.Erros);
        Assert.Equal([3, 2], r.Lancamentos.Select(l => l.Partidas.Count));
        Assert.Contains("coluna \"Inexistente\"", ImportadorLancamentos.Ler(t, layout with { ColunaHistorico = "Inexistente" }).Erros[0]);
    }

    private const string Ofx = """
        OFXHEADER:100
        DATA:OFXSGML
        CHARSET:1252
        <OFX>
        <BANKMSGSRSV1><STMTTRNRS><STMTRS><CURDEF>BRL
        <BANKACCTFROM><BANKID>0001<BRANCHID>1234-5<ACCTID>98765-0<ACCTTYPE>CHECKING</BANKACCTFROM>
        <BANKTRANLIST><DTSTART>20260301<DTEND>20260331
        <STMTTRN><TRNTYPE>CREDIT<DTPOSTED>20260305120000[-3:BRT]<TRNAMT>1500.00<FITID>A1<CHECKNUM>555<MEMO>PIX RECEBIDO CLIENTE &amp; CIA
        <STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20260306<TRNAMT>-89.90<FITID>A2<NAME>TARIFA</NAME><MEMO>TARIFA PACOTE</MEMO></STMTTRN>
        </BANKTRANLIST><LEDGERBAL><BALAMT>1410.10<DTASOF>20260331</LEDGERBAL>
        </STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
        """;

    [Fact]
    public void Le_extrato_ofx_sgml()
    {
        var r = LeitorOfx.Ler(Ofx);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        var e = r.Valor;
        Assert.Equal(("0001", "1234-5", "98765-0", 1410.10m), (e.Banco, e.Agencia, e.Conta, e.SaldoFinal));
        Assert.Equal(2, e.Transacoes.Count);
        Assert.Equal(("A1", new DateOnly(2026, 3, 5), 1500m, "PIX RECEBIDO CLIENTE & CIA", "555"),
                     (e.Transacoes[0].Id, e.Transacoes[0].Data, e.Transacoes[0].Valor, e.Transacoes[0].Descricao, e.Transacoes[0].Documento));
        Assert.Equal((-89.90m, "TARIFA - TARIFA PACOTE"), (e.Transacoes[1].Valor, e.Transacoes[1].Descricao));
        Assert.Equal("OFX:0001/1234-5/98765-0/A2", e.Chave(e.Transacoes[1]));
        Assert.False(LeitorOfx.Ler("Data;Valor").Sucesso);
    }

    [Theory]
    [InlineData("1.500,00", 1500)]
    [InlineData("1500.5", 1500.5)]
    [InlineData("-80.25", -80.25)]
    [InlineData("(80,00)", -80)]
    [InlineData("R$ 2.000", 2000)]
    public void Le_valores_de_planilha(string texto, double esperado) => Assert.Equal((decimal)esperado, Formato.LerValor(texto));

    [Theory]
    [InlineData("10/03/2026")]
    [InlineData("2026-03-10")]
    [InlineData("10032026")]
    [InlineData("20260310093000[-3:BRT]")]
    [InlineData("46091")]
    public void Le_datas_de_planilha(string texto) => Assert.Equal(new DateOnly(2026, 3, 10), Formato.LerData(texto));
}
