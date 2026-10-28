using System.Globalization;
using Vorticis.Domain.Sped;
using Vorticis.Fiscal.Desktop.ViewModels;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Sped;

namespace Vorticis.Fiscal.Tests;

public static class CteDeTeste
{
    private static string N(decimal v) => v.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>procCTe mínimo: emitente (transportadora), remetente, destinatário, tomador (toma3 = 3, destinatário) e ICMS00.</summary>
    public static string Cte(string cnpjEmit, string cnpjRem, string cnpjDest, int numero, DateOnly emissao, decimal valor, decimal aliq = 12m, string cfop = "6353", string? ufIni = "SP")
    {
        var chave = ChaveCte(cnpjEmit, numero);
        var icms = Math.Round(valor * aliq / 100m, 2);
        return $"""
            <cteProc xmlns="http://www.portalfiscal.inf.br/cte" versao="4.00"><CTe><infCte Id="CTe{chave}" versao="4.00">
            <ide><cUF>35</cUF><CFOP>{cfop}</CFOP><natOp>PRESTACAO DE SERVICO DE TRANSPORTE</natOp><mod>57</mod><serie>1</serie><nCT>{numero}</nCT>
            <dhEmi>{emissao:yyyy-MM-dd}T10:00:00-03:00</dhEmi><tpCTe>0</tpCTe><modal>01</modal><cMunIni>3550308</cMunIni><xMunIni>SAO PAULO</xMunIni><UFIni>{ufIni}</UFIni>
            <cMunFim>3304557</cMunFim><xMunFim>RIO DE JANEIRO</xMunFim><UFFim>RJ</UFFim><toma3><toma>3</toma></toma3></ide>
            <emit><CNPJ>{cnpjEmit}</CNPJ><IE>111</IE><xNome>Transportadora Teste</xNome><enderEmit><cMun>3550308</cMun><xMun>SAO PAULO</xMun><UF>SP</UF></enderEmit></emit>
            <rem><CNPJ>{cnpjRem}</CNPJ><xNome>Remetente Teste</xNome><enderReme><cMun>3550308</cMun><UF>SP</UF></enderReme></rem>
            <dest><CNPJ>{cnpjDest}</CNPJ><xNome>Destinatario Teste</xNome><enderDest><cMun>3304557</cMun><UF>RJ</UF></enderDest></dest>
            <vPrest><vTPrest>{N(valor)}</vTPrest><vRec>{N(valor)}</vRec></vPrest>
            <imp><ICMS><ICMS00><CST>00</CST><vBC>{N(valor)}</vBC><pICMS>{N(aliq)}</pICMS><vICMS>{N(icms)}</vICMS></ICMS00></ICMS></imp>
            <infCTeNorm><infDoc><infNFe><chave>35240300000000000191550010000000011000000011</chave></infNFe></infDoc></infCTeNorm>
            </infCte></CTe><protCTe><infProt><cStat>100</cStat></infProt></protCTe></cteProc>
            """;
    }

    public static string ChaveCte(string cnpjEmit, int numero)
    {
        var b = $"352403{cnpjEmit}57001{numero:D9}1{numero:D8}";
        int soma = 0, peso = 2;
        for (var i = b.Length - 1; i >= 0; i--) { soma += (b[i] - '0') * peso; peso = peso == 9 ? 2 : peso + 1; }
        var r = soma % 11;
        return b + (r < 2 ? 0 : 11 - r);
    }

    public static string Cancelamento(string chave) => $"""
        <procEventoCTe xmlns="http://www.portalfiscal.inf.br/cte"><eventoCTe><infEvento><chCTe>{chave}</chCTe><tpEvento>110111</tpEvento></infEvento></eventoCTe></procEventoCTe>
        """;
}

public class NfseAbrasfTests
{
    [Fact]
    public void Le_nfse_abrasf_basica_com_prestador_tomador_valores_e_iss()
    {
        const string xml = """
            <CompNfse xmlns="http://www.abrasf.org.br/nfse.xsd"><Nfse><InfNfse><Numero>123</Numero><CodigoVerificacao>ABC123</CodigoVerificacao><DataEmissao>2026-03-05T10:00:00</DataEmissao>
            <Servico><Valores><ValorServicos>2000.00</ValorServicos><ValorIss>100.00</ValorIss><Aliquota>5</Aliquota><BaseCalculo>2000.00</BaseCalculo><IssRetido>2</IssRetido></Valores>
            <ItemListaServico>1.07</ItemListaServico><Discriminacao>Suporte tecnico</Discriminacao></Servico>
            <PrestadorServico><IdentificacaoPrestador><Cnpj>45997418000153</Cnpj></IdentificacaoPrestador><RazaoSocial>Prestador SA</RazaoSocial><Endereco><CodigoMunicipio>3550308</CodigoMunicipio><Uf>SP</Uf></Endereco></PrestadorServico>
            <TomadorServico><IdentificacaoTomador><CpfCnpj><Cnpj>11222333000181</Cnpj></CpfCnpj></IdentificacaoTomador><RazaoSocial>Tomador Ltda</RazaoSocial></TomadorServico>
            </InfNfse></Nfse></CompNfse>
            """;
        var d = Assert.IsType<LeituraDocumento>(LeitorXmlFiscal.Ler(XmlsDeTeste.Bytes(xml))).Documento;
        Assert.Equal((ModeloDocumento.NFSe, "123"), (d.Modelo, d.Numero));
        Assert.StartsWith("ABRASF45997418000153123", d.Chave);
        Assert.Equal(("45997418000153", "11222333000181"), (d.Emitente.Documento, d.Destinatario!.Documento));
        Assert.Equal((2000m, 100m, false), (d.Totais.Total, d.Totais.Iss, d.Totais.IssRetido));
        Assert.Equal(5m, d.Itens[0].AliqIss);
        Assert.Equal(TipoOperacao.Saida, IdentificadorOperacao.Identificar(d, ["45997418000153"]).Valor.Operacao);
    }
}

public class CteLeitorTests
{
    [Fact]
    public void Le_cte_com_emitente_tomador_partes_valores_icms_cfop_e_chaves()
    {
        var xml = CteDeTeste.Cte("45997418000153", "11222333000181", "00000000000191", 7, new DateOnly(2026, 3, 10), 1000m);
        var lido = Assert.IsType<LeituraDocumento>(LeitorXmlFiscal.Ler(XmlsDeTeste.Bytes(xml))).Documento;
        Assert.Equal(ModeloDocumento.CTe57, lido.Modelo);
        Assert.Equal("7", lido.Numero);
        Assert.Equal(44, lido.Chave.Length);
        Assert.Equal("45997418000153", lido.Emitente.Documento);
        Assert.Equal("00000000000191", lido.Destinatario!.Documento);   // tomador
        Assert.Equal("00000000000191", lido.Transporte!.Tomador.Documento);
        Assert.Equal("11222333000181", lido.Transporte.Remetente!.Documento);
        Assert.Equal(("SP", "RJ"), (lido.Transporte.UfInicio, lido.Transporte.UfFim));
        Assert.Single(lido.Transporte.ChavesNfe);
        var item = Assert.Single(lido.Itens);
        Assert.Equal(("6353", 1000m, 120m, 12m), (item.Cfop, item.ValorContabil, item.Icms, item.AliqIcms));
        Assert.Equal((1000m, 120m), (lido.Totais.Total, lido.Totais.Icms));
    }

    [Fact]
    public void Evento_de_cancelamento_do_cte_e_reconhecido()
    {
        var chave = new string('1', 44);
        Assert.Equal(chave, Assert.IsType<LeituraCancelamento>(LeitorXmlFiscal.Ler(XmlsDeTeste.Bytes(CteDeTeste.Cancelamento(chave)))).Chave);
    }

    [Fact]
    public void Tomador_e_entrada_e_emitente_e_saida()
    {
        var emitente = LeitorXmlFiscal.Ler(XmlsDeTeste.Bytes(CteDeTeste.Cte("45997418000153", "11222333000181", "00000000000191", 1, new DateOnly(2026, 3, 1), 100m)));
        var doc = ((LeituraDocumento)emitente).Documento;
        Assert.Equal(TipoOperacao.Saida, IdentificadorOperacao.Identificar(doc, ["45997418000153"]).Valor.Operacao);
        Assert.Equal(TipoOperacao.Entrada, IdentificadorOperacao.Identificar(doc, ["00000000000191"]).Valor.Operacao);
        Assert.False(IdentificadorOperacao.Identificar(doc, ["11222333000181"]).Sucesso);   // só remetente: não é entrada nem saída da empresa
    }
}

public class CteIntegracaoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private static readonly DateOnly Mar = new(2026, 3, 10);

    [Fact]
    public async Task Importa_cte_entrada_e_saida_consulta_cancela_e_entra_na_efd_em_d100_d190()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var cnpj = CnpjsDeTeste.Valido(91);
        var empresa = await db.CriarEmpresaAsync(cnpj);
        await db.EscalarAsync<long>("""
            UPDATE core.empresa SET inscricao_estadual = '123456789012', cep = '01310100', logradouro = 'Av. Paulista', numero = '1000', bairro = 'Bela Vista', email = 'a@b.com', telefone = '1130000000'
             WHERE id = @e RETURNING id
            """, ("e", empresa));
        var contador = await db.EscalarAsync<long>("INSERT INTO core.contador (nome, cpf, crc, uf_crc, email) VALUES ('Contador de Teste', '12345691000', '1SP123456/O-7', 'SP', 'c@c.com') RETURNING id");
        await db.EscalarAsync<long>("INSERT INTO core.empresa_contabilidade (empresa_id, contador_id, inicio, responsavel_tecnico) VALUES (@e, @c, '2025-01-01', TRUE) RETURNING id", ("e", empresa), ("c", contador));
        var adm = await db.AdminAsync();
        var ctx = new ContextoDeTeste(db.App, adm, empresa);
        var pasta = Directory.CreateTempSubdirectory("cte").FullName;
        try
        {
            var entrada = CteDeTeste.Cte("45997418000153", "11222333000181", cnpj, 10, Mar, 1000m, cfop: "6353");       // empresa é a tomadora
            var saida = CteDeTeste.Cte(cnpj, "11222333000181", "45997418000153", 11, Mar, 500m, cfop: "5353");          // empresa é a transportadora (tomador = destinatário)
            File.WriteAllBytes(Path.Combine(pasta, "entrada.xml"), XmlsDeTeste.Bytes(entrada));
            File.WriteAllBytes(Path.Combine(pasta, "saida.xml"), XmlsDeTeste.Bytes(saida));
            File.WriteAllBytes(Path.Combine(pasta, "nfse.xml"), XmlsDeTeste.Bytes(XmlsDeTeste.Nfse(cnpj, XmlsDeTeste.CnpjCliente, 3, Mar, 100m, iss: 5m)));

            // tela Importar NFS-e recusa CT-e; tela Importar CT-e recusa NFS-e
            var nfse = new ImportacaoNfseViewModel(ctx) { Pasta = pasta, IncluirSubpastas = false, CompletarPeloCnpj = false };
            await nfse.ImportarPastaCommand.ExecuteAsync(null);
            Assert.Equal(1, nfse.Resultados.Count(r => r.Situacao == "Importado"));
            Assert.Equal(2, nfse.Resultados.Count(r => r.Situacao != "Importado"));

            var cte = new ImportacaoCteViewModel(ctx) { Pasta = pasta, IncluirSubpastas = false, CompletarPeloCnpj = false };
            await cte.ImportarPastaCommand.ExecuteAsync(null);
            Assert.Empty(cte.Erros);
            Assert.Equal(2, cte.Resultados.Count(r => r.Situacao == "Importado"));

            var consulta = new ConsultaCteViewModel(ctx) { Inicio = new DateTime(2026, 3, 1), Fim = new DateTime(2026, 3, 31) };
            await consulta.ConsultarCommand.ExecuteAsync(null);
            Assert.Equal(2, consulta.Lista.Count);
            var e = consulta.Lista.Single(l => l.Operacao == "Entrada");
            Assert.Equal(("Transportadora Teste", "Remetente Teste", "SP → RJ", "2353"), (e.Emitente, e.Remetente, e.Percurso, e.Cfop));
            Assert.Contains("2 CT-e", consulta.Resumo);

            // XML guardado
            var xml = await db.App.Fiscal().Documentos.ObterXmlAsync(empresa, e.Id, adm, ct);
            Assert.True(xml.Sucesso);

            // EFD: D100/D190 e nada de C100 para o CT-e; crédito de 120 e débito de 60 na apuração
            var efd = await db.App.Fiscal().Efd.GerarIcmsAsync(empresa, Mar, new OpcoesEfdIcms('A', '1', CodigoReceitaIcms: "046-2"), adm, null, ct);
            Assert.True(efd.Sucesso, string.Join(";", efd.Erros));
            Assert.Equal(2, efd.Valor.Registros["D100"]);
            Assert.Equal(2, efd.Valor.Registros["D190"]);
            Assert.False(efd.Valor.Registros.ContainsKey("C100"));
            var texto = FormatoSped.Latin1.GetString(efd.Valor.Conteudo);
            Assert.Contains("|D100|0|1|", texto);
            Assert.Contains("|D190|", texto);

            // cancelamento pelo evento
            var chave = (await db.App.Fiscal().Documentos.ConsultarAsync(empresa, new FiltroDocumentos(Modelo: ModeloDocumento.CTe57), adm, ct)).Valor.First().Chave;
            var canc = await db.App.Fiscal().Documentos.ImportarAsync(empresa, [new ArquivoXml("c.xml", XmlsDeTeste.Bytes(CteDeTeste.Cancelamento(chave)))], adm, ct: ct);
            Assert.Equal(SituacaoImportacao.Cancelado, canc.Valor.Arquivos.Single().Situacao);
        }
        finally { Directory.Delete(pasta, true); }
    }
}
