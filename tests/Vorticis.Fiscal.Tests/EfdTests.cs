using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Vorticis.Application.Sped;
using Vorticis.Domain.Sped;
using Vorticis.Fiscal.Desktop.ViewModels;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Lucro;
using Vorticis.Fiscal.Sped;
using Vorticis.Infra.Sped;

namespace Vorticis.Fiscal.Tests;

/// <summary>EFD ICMS/IPI e EFD-Contribuições geradas de notas de entrada e saída: estrutura, contagens do bloco 9, apurações e assinatura.</summary>
public class EfdTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private static ArquivoXml Arq(string nome, string xml) => new(nome, XmlsDeTeste.Bytes(xml));
    private static readonly DateOnly Mar = new(2026, 3, 10);

    /// <summary>Chave de 44 dígitos com dígito verificador correto (módulo 11).</summary>
    private static string Chave(string cnpjEmit, int numero)
    {
        var b = $"352603{cnpjEmit}55001{numero:D9}1{numero:D8}";
        var soma = 0; var peso = 2;
        for (var i = b.Length - 1; i >= 0; i--) { soma += (b[i] - '0') * peso; peso = peso == 9 ? 2 : peso + 1; }
        var r = soma % 11;
        return b + (r < 2 ? 0 : 11 - r);
    }

    private static X509Certificate2 Certificado(string cn)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest($"CN={cn}, O=ICP-Brasil, C=BR", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx, "x"), "x", X509KeyStorageFlags.Exportable);
    }

    private static List<string[]> Regs(byte[] conteudo, string reg) =>
        FormatoSped.Latin1.GetString(conteudo).Split("\r\n").Where(l => l.StartsWith($"|{reg}|", StringComparison.Ordinal)).Select(l => l.Split('|')[2..^1]).ToList();

    private static string[] Reg(byte[] conteudo, string reg) => Regs(conteudo, reg).Single();

    private async Task<long> EmpresaCompletaAsync(int semente, string? ie = "123456789012")
    {
        var cnpj = CnpjsDeTeste.Valido(semente);
        var empresa = await db.CriarEmpresaAsync(cnpj);
        await db.EscalarAsync<long>("""
            UPDATE core.empresa SET inscricao_estadual = @ie, cep = '01310100', logradouro = 'Av. Paulista', numero = '1000', bairro = 'Bela Vista', email = 'a@b.com', telefone = '1130000000'
             WHERE id = @e RETURNING id
            """, ("ie", (object?)ie ?? DBNull.Value), ("e", empresa));
        var contador = await db.EscalarAsync<long>("INSERT INTO core.contador (nome, cpf, crc, uf_crc, email) VALUES ('Contador de Teste', @c, '1SP123456/O-7', 'SP', 'c@c.com') RETURNING id",
            ("c", $"{(123_456_000L + semente):D9}00"[..11]));
        await db.EscalarAsync<long>("INSERT INTO core.empresa_contabilidade (empresa_id, contador_id, inicio, responsavel_tecnico) VALUES (@e, @c, '2025-01-01', TRUE) RETURNING id",
            ("e", empresa), ("c", contador));
        return empresa;
    }

    [Fact]
    public async Task Efd_icms_ipi_gera_blocos_apuracao_e_assinatura_verificavel()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await EmpresaCompletaAsync(71);
        var cnpj = await db.EscalarAsync<string>("SELECT cnpj FROM core.empresa WHERE id = @e", ("e", empresa));
        var adm = await db.AdminAsync();
        var fiscal = db.App.Fiscal();
        var forn = XmlsDeTeste.CnpjFornecedor;

        var importou = await fiscal.Documentos.ImportarAsync(empresa,
        [
            Arq("v1.xml", XmlsDeTeste.Nfe(cnpj, XmlsDeTeste.CnpjCliente, 1, 1, Mar, [new ItemNfe("5102", 10_000m, 1_800m, Pis: 165m, Cofins: 760m)], chave: Chave(cnpj, 1))),
            Arq("v2.xml", XmlsDeTeste.Nfe(cnpj, XmlsDeTeste.CnpjCliente, 1, 2, Mar, [new ItemNfe("5102", 5_000m, 900m), new ItemNfe("5405", 2_000m, IcmsSt: 120m, AliqIcms: 0m)], chave: Chave(cnpj, 2))),
            Arq("c1.xml", XmlsDeTeste.Nfe(forn, cnpj, 0, 77, Mar, [new ItemNfe("1102", 6_000m, 1_080m)], chave: Chave(forn, 77))),
            Arq("v3.xml", XmlsDeTeste.Nfe(cnpj, XmlsDeTeste.CnpjCliente, 1, 3, Mar, [new ItemNfe("5102", 1_000m, 180m)], chave: Chave(cnpj, 3))),
            Arq("s.xml", XmlsDeTeste.Nfse(cnpj, XmlsDeTeste.CnpjCliente, 5, Mar, 10_000m, iss: 500m)),
        ], adm, ct: ct);
        Assert.True(importou.Sucesso, string.Join(";", importou.Erros));
        // cancela a venda 3 (evento de cancelamento depois da nota)
        await fiscal.Documentos.ImportarAsync(empresa, [Arq("x.xml", XmlsDeTeste.CancelamentoNfe(Chave(cnpj, 3)))], adm, ct: ct);

        var efd = fiscal.Efd;
        var opcoes = new OpcoesEfdIcms('A', '1', CodigoReceitaIcms: "046-2", CodigoReceitaIcmsSt: "063-2", DiaVencimento: 12);
        var previa = (await efd.ValidarIcmsAsync(empresa, Mar, opcoes, adm, ct)).Valor;
        Assert.True(previa.PodeGerar, string.Join(" | ", previa.Bloqueantes.Select(p => p.Mensagem)));
        Assert.Contains(previa.Pendencias, p => p.Codigo == "NFSE_FORA");
        Assert.Equal(4, previa.Documentos);

        var cert = Certificado("Contador Teste:12345678901");
        var gerado = await efd.GerarIcmsAsync(empresa, Mar, opcoes, adm, [new CertificadoAssinante(cert, "900")], ct);
        Assert.True(gerado.Sucesso, string.Join(";", gerado.Erros));
        var arq = gerado.Valor;
        var assinador = new AssinadorSpedCms();
        var sem = arq.Conteudo[..assinador.TamanhoSemAssinaturas(arq.Conteudo)];
        var linhas = FormatoSped.Latin1.GetString(sem).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        // cabeçalho e leiaute 020
        Assert.Equal(["020", "0", "01032026", "31032026"], Reg(sem, "0000")[..4]);
        Assert.Equal(cnpj, Reg(sem, "0000")[5]);
        Assert.Equal("A", Reg(sem, "0000")[12]);
        Assert.Equal("EFD_ICMS_IPI_" + cnpj + "_202603.txt", arq.NomeArquivo);

        // quantidade de campos de cada registro conforme o leiaute
        var esperados = new Dictionary<string, int> { ["0000"] = 14, ["0001"] = 1, ["0005"] = 9, ["0100"] = 13, ["0150"] = 12, ["0190"] = 2, ["0200"] = 12, ["C001"] = 1, ["C100"] = 28,
            ["C170"] = 37, ["C190"] = 11, ["E100"] = 2, ["E110"] = 14, ["E116"] = 9, ["E200"] = 3, ["E210"] = 14, ["E250"] = 9, ["1010"] = 13 };
        foreach (var (reg, n) in esperados)
            Assert.All(Regs(sem, reg), f => Assert.Equal(n, f.Length));
        Assert.NotEmpty(Regs(sem, "E250"));

        // notas: 3 vendas (uma cancelada) + 1 compra; NFS-e fora; C170 em todas as notas autorizadas (perfil A)
        var c100 = Regs(sem, "C100");
        Assert.Equal(4, c100.Count);
        Assert.Equal(["1", "1", "1", "0"], c100.OrderBy(f => int.Parse(f[6])).Select(f => f[0]).ToArray());
        var cancelada = c100.Single(f => f[4] == "02");
        Assert.All(cancelada[8..], v => Assert.Equal("", v));
        Assert.Equal(4, Regs(sem, "C170").Count);                 // v1(1) + v2(2) + compra(1)
        Assert.Equal(4, Regs(sem, "C190").Count);                 // v1, v2 (5102 e 5405) e a compra
        Assert.Equal(["000", "000"], Regs(sem, "C190").Where(f => f[1] == "5102").Select(f => f[0]).ToArray());

        // E110: débitos das saídas (1.800 + 900), créditos da entrada (1.080), a recolher 1.620
        var e110 = Reg(sem, "E110");
        Assert.Equal(("2700,00", "1080,00", "1620,00", "0,00"), (e110[0], e110[4], e110[11], e110[12]));
        var e116 = Reg(sem, "E116");
        Assert.Equal(("000", "1620,00", "12042026", "046-2", "032026"), (e116[0], e116[1], e116[2], e116[3], e116[8]));
        // ICMS-ST retido nas saídas para o RJ
        Assert.Equal("RJ", Reg(sem, "E200")[0]);
        Assert.Equal(("120,00", "120,00"), (Reg(sem, "E210")[6], Reg(sem, "E210")[11]));
        Assert.Equal(("002", "120,00", "063-2"), (Reg(sem, "E250")[0], Reg(sem, "E250")[1], Reg(sem, "E250")[3]));

        // bloco 9: contagens de cada registro, x990 por bloco e 9999
        var reais = linhas.GroupBy(l => l.Split('|')[1]).ToDictionary(g => g.Key, g => g.Count());
        foreach (var f in Regs(sem, "9900")) Assert.Equal(reais[f[0]], int.Parse(f[1]));
        Assert.Equal(reais.Count, Regs(sem, "9900").Count);
        Assert.Equal(linhas.Length, int.Parse(Reg(sem, "9999")[0]));
        foreach (var bloco in "0CDEGHK1")
        {
            var doBloco = linhas.Count(l => l[1] == bloco);
            Assert.Equal(doBloco, int.Parse(Reg(sem, $"{bloco}990")[0]));
        }
        Assert.Equal(reais["9900"] + 3, int.Parse(Reg(sem, "9990")[0]));
        Assert.Equal("|9999|" + linhas.Length + "|", linhas[^1]);

        // 0150/0190/0200 referenciados existem
        var participantes = Regs(sem, "0150").Select(f => f[0]).ToHashSet();
        Assert.All(c100.Where(f => f[4] == "00"), f => Assert.Contains(f[2], participantes));
        var unidades = Regs(sem, "0190").Select(f => f[0]).ToHashSet();
        var itens0200 = Regs(sem, "0200").Select(f => f[0]).ToHashSet();
        Assert.All(Regs(sem, "C170"), f => { Assert.Contains(f[4], unidades); Assert.Contains(f[1], itens0200); });

        // assinatura verificável (esquema SBRCAAEPDR)
        var assinaturas = assinador.LerAssinaturas(arq.Conteudo);
        var a = Assert.Single(assinaturas);
        Assert.True(a.Valida, a.Erro);
        Assert.Equal("SBRCAAEPDR01900", a.Etiqueta);
        Assert.Single(arq.Assinaturas);

        // histórico
        var hist = (await efd.ListarGeradasAsync(empresa, TipoEfd.IcmsIpi, adm, ct)).Valor;
        Assert.Equal((new DateOnly(2026, 3, 1), "A", arq.Linhas, 1), (hist[0].Inicio, hist[0].Perfil, hist[0].Linhas, hist[0].Assinaturas));
    }

    [Fact]
    public async Task Efd_icms_ipi_valida_ie_chave_cfop_e_perfil_b_limita_c170()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await EmpresaCompletaAsync(72, ie: null);
        var cnpj = await db.EscalarAsync<string>("SELECT cnpj FROM core.empresa WHERE id = @e", ("e", empresa));
        var adm = await db.AdminAsync();
        var fiscal = db.App.Fiscal();
        await fiscal.Documentos.ImportarAsync(empresa,
        [
            Arq("ruim.xml", XmlsDeTeste.Nfe(cnpj, XmlsDeTeste.CnpjCliente, 1, 9, Mar, [new ItemNfe("1102", 100m)])),     // CFOP de entrada em saída e chave sem DV
        ], adm, ct: ct);
        var previa = (await fiscal.Efd.ValidarIcmsAsync(empresa, Mar, new OpcoesEfdIcms(), adm, ct)).Valor;
        Assert.False(previa.PodeGerar);
        var codigos = previa.Bloqueantes.Select(p => p.Codigo).ToHashSet();
        Assert.Contains("SEM_IE", codigos);
        Assert.Contains("CHAVE_INVALIDA", codigos);
        Assert.Contains("CFOP_OPERACAO", codigos);
        Assert.False((await fiscal.Efd.GerarIcmsAsync(empresa, Mar, new OpcoesEfdIcms(), adm, null, ct)).Sucesso);

        // perfil B: C170 só nas entradas; sem código de receita não gera com ICMS a recolher
        var empresa2 = await EmpresaCompletaAsync(73);
        var cnpj2 = await db.EscalarAsync<string>("SELECT cnpj FROM core.empresa WHERE id = @e", ("e", empresa2));
        await fiscal.Documentos.ImportarAsync(empresa2,
        [
            Arq("v.xml", XmlsDeTeste.Nfe(cnpj2, XmlsDeTeste.CnpjCliente, 1, 1, Mar, [new ItemNfe("5102", 1_000m, 180m)], chave: Chave(cnpj2, 1))),
            Arq("c.xml", XmlsDeTeste.Nfe(XmlsDeTeste.CnpjFornecedor, cnpj2, 0, 5, Mar, [new ItemNfe("1102", 400m, 72m)], chave: Chave(XmlsDeTeste.CnpjFornecedor, 5))),
        ], adm, ct: ct);
        var semCodigo = (await fiscal.Efd.ValidarIcmsAsync(empresa2, Mar, new OpcoesEfdIcms('B'), adm, ct)).Valor;
        Assert.Contains(semCodigo.Bloqueantes, p => p.Codigo == "SEM_COD_REC_ICMS");
        var b = await fiscal.Efd.GerarIcmsAsync(empresa2, Mar, new OpcoesEfdIcms('B', CodigoReceitaIcms: "046-2", SaldoCredorAnterior: 8m), adm, null, ct);
        Assert.True(b.Sucesso, string.Join(";", b.Erros));
        Assert.Single(Regs(b.Valor.Conteudo, "C170"));
        Assert.Equal("100,00", Reg(b.Valor.Conteudo, "E110")[11]);   // 180 − 72 − saldo anterior 8
        Assert.Empty(b.Valor.Assinaturas);
    }

    [Fact]
    public async Task Livros_de_apuracao_do_icms_e_do_ipi_batem_com_o_e110_e_o_e520_da_efd()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await EmpresaCompletaAsync(77);
        var cnpj = await db.EscalarAsync<string>("SELECT cnpj FROM core.empresa WHERE id = @e", ("e", empresa));
        var adm = await db.AdminAsync();
        var fiscal = db.App.Fiscal();
        await fiscal.Documentos.ImportarAsync(empresa,
        [
            Arq("v.xml", XmlsDeTeste.Nfe(cnpj, XmlsDeTeste.CnpjCliente, 1, 1, Mar, [new ItemNfe("5102", 1_000m, 180m, Ipi: 50m), new ItemNfe("5101", 500m, 90m)], chave: Chave(cnpj, 1))),
            Arq("c.xml", XmlsDeTeste.Nfe(XmlsDeTeste.CnpjFornecedor, cnpj, 0, 5, Mar, [new ItemNfe("1102", 400m, 72m, Ipi: 20m)], chave: Chave(XmlsDeTeste.CnpjFornecedor, 5))),
        ], adm, ct: ct);

        var livro = await fiscal.Efd.LivroApuracaoAsync(empresa, Mar, 8m, adm, ct);
        Assert.True(livro.Sucesso, string.Join(";", livro.Erros));
        var icms = livro.Valor.Icms;
        Assert.Equal(new[] { "5101", "5102" }, icms.DebitosCfop!.Select(l => l.Cfop));
        Assert.Equal(270m, icms.DebitosCfop!.Sum(l => l.Imposto));
        Assert.Equal(1_550m, icms.DebitosCfop!.Sum(l => l.Operacao));   // valor contábil inclui o IPI
        Assert.Equal(72m, Assert.Single(icms.CreditosCfop!).Imposto);

        var efd = await fiscal.Efd.GerarIcmsAsync(empresa, Mar, new OpcoesEfdIcms('B', IndicadorAtividade: '0', CodigoReceitaIcms: "046-2", SaldoCredorAnterior: 8m), adm, null, ct);
        Assert.True(efd.Sucesso, string.Join(";", efd.Erros));
        var e110 = Reg(efd.Valor.Conteudo, "E110");
        string V(decimal v) => v.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        Assert.Equal(V(icms.Debitos), e110[0]);
        Assert.Equal(V(icms.Creditos), e110[4]);
        Assert.Equal(V(icms.SaldoCredorAnterior), e110[8]);
        Assert.Equal(V(icms.Recolher), e110[11]);
        Assert.Equal(V(icms.SaldoCredorTransportar), e110[12]);

        var ipi = Assert.IsType<ApuracaoIpiEfd>(livro.Valor.Ipi);
        var e520 = Reg(efd.Valor.Conteudo, "E520");
        Assert.Equal(V(ipi.Debitos), e520[1]);
        Assert.Equal(V(ipi.Creditos), e520[2]);
        Assert.Equal(V(ipi.SaldoCredor), e520[5]);
        Assert.Equal(V(ipi.SaldoDevedor), e520[6]);
        Assert.Equal(30m, ipi.SaldoDevedor);

        var tabela = Vorticis.Fiscal.Relatorios.TabelasFiscais.LivroApuracaoIcms(livro.Valor);
        Assert.Contains(tabela.Linhas, l => Equals(l.Valores[0], "Imposto a recolher") && Equals(l.Valores[4], icms.Recolher));
        var tIpi = Vorticis.Fiscal.Relatorios.TabelasFiscais.LivroApuracaoIpi(livro.Valor);
        Assert.Contains(tIpi.Linhas, l => Equals(l.Valores[0], "Saldo devedor (imposto a recolher)") && Equals(l.Valores[4], 30m));

        var semPermissao = await fiscal.Efd.LivroApuracaoAsync(empresa, Mar, 0, 0, ct);
        Assert.False(semPermissao.Sucesso);
    }

    [Fact]
    public async Task Efd_contribuicoes_gera_bloco_m_conforme_a_apuracao_e_as_notas()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await EmpresaCompletaAsync(74);
        var cnpj = await db.EscalarAsync<string>("SELECT cnpj FROM core.empresa WHERE id = @e", ("e", empresa));
        var adm = await db.AdminAsync();
        var fiscal = db.App.Fiscal();
        var forn = XmlsDeTeste.CnpjFornecedor;
        await fiscal.Documentos.ImportarAsync(empresa,
        [
            Arq("v1.xml", XmlsDeTeste.Nfe(cnpj, XmlsDeTeste.CnpjCliente, 1, 1, Mar, [new ItemNfe("5102", 10_000m, 1_800m)], chave: Chave(cnpj, 1))),
            Arq("v2.xml", XmlsDeTeste.Nfe(cnpj, XmlsDeTeste.CnpjCliente, 1, 2, Mar, [new ItemNfe("5102", 5_000m, 900m), new ItemNfe("5405", 2_000m, IcmsSt: 120m, AliqIcms: 0m)], chave: Chave(cnpj, 2))),
            Arq("c1.xml", XmlsDeTeste.Nfe(forn, cnpj, 0, 77, Mar, [new ItemNfe("1102", 6_000m, 1_080m)], chave: Chave(forn, 77))),
            Arq("s.xml", XmlsDeTeste.Nfse(cnpj, XmlsDeTeste.CnpjCliente, 5, Mar, 10_000m, iss: 500m)),
        ], adm, ct: ct);

        var efd = fiscal.Efd;
        var opcoes = new OpcoesEfdContribuicoes();
        var sem = await efd.ValidarContribuicoesAsync(empresa, Mar, opcoes, adm, ct);
        Assert.Contains(sem.Valor.Bloqueantes, p => p.Codigo == "SEM_APURACAO");

        var pc = (await fiscal.Lucro.PrepararPisCofinsAsync(empresa, Mar, RegimePisCofins.NaoCumulativo, false, adm, ct)).Valor;
        Assert.Equal((27_000m, 6_000m), (pc.Entrada.ReceitaBase, pc.Entrada.BaseCreditos));
        var salvo = (await fiscal.Lucro.SalvarPisCofinsAsync(empresa, pc.Entrada, adm, ct)).Valor.Resultado;
        Assert.Equal((445.50m, 2_052m, 99m, 456m), (salvo.PisDebito, salvo.CofinsDebito, salvo.PisCredito, salvo.CofinsCredito));

        var previa = (await efd.ValidarContribuicoesAsync(empresa, Mar, opcoes, adm, ct)).Valor;
        Assert.True(previa.PodeGerar, string.Join(" | ", previa.Bloqueantes.Select(p => p.Mensagem)));
        Assert.DoesNotContain(previa.Pendencias, p => p.Codigo is "RECEITA_DIVERGE" or "CREDITO_DIVERGE");

        var cert = Certificado("Contador Contribuicoes:12345678901");
        var gerado = await efd.GerarContribuicoesAsync(empresa, Mar, opcoes, adm, [new CertificadoAssinante(cert, "900")], ct);
        Assert.True(gerado.Sucesso, string.Join(";", gerado.Erros));
        var arq = gerado.Valor;
        var assinador = new AssinadorSpedCms();
        var sa = arq.Conteudo[..assinador.TamanhoSemAssinaturas(arq.Conteudo)];
        var linhas = FormatoSped.Latin1.GetString(sa).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(["006", "0", "", ""], Reg(sa, "0000")[..4]);
        Assert.Equal(("1", "1", "1", ""), (Reg(sa, "0110")[0], Reg(sa, "0110")[1], Reg(sa, "0110")[2], Reg(sa, "0110")[3]));
        var esperados = new Dictionary<string, int> { ["0000"] = 13, ["0100"] = 13, ["0110"] = 4, ["0140"] = 8, ["0150"] = 12, ["0190"] = 2, ["0200"] = 11, ["A010"] = 1, ["A100"] = 20,
            ["A170"] = 17, ["C010"] = 2, ["C100"] = 28, ["C170"] = 36, ["M100"] = 14, ["M105"] = 9, ["M200"] = 12, ["M210"] = 12, ["M500"] = 14, ["M505"] = 9, ["M600"] = 12, ["M610"] = 12 };
        foreach (var (reg, n) in esperados) Assert.All(Regs(sa, reg), f => Assert.Equal(n, f.Length));
        Assert.Single(Regs(sa, "A100"));
        Assert.Equal(3, Regs(sa, "C100").Count);
        Assert.Equal(4, Regs(sa, "C170").Count);

        // PIS (M200): contribuição 445,50, crédito 99,00, devida 346,50 = a recolher
        var m200 = Reg(sa, "M200");
        Assert.Equal(("445,50", "99,00", "0,00", "346,50", "0,00", "0,00", "346,50", "0,00", "0,00", "0,00", "0,00", "346,50"), (m200[0], m200[1], m200[2], m200[3], m200[4], m200[5], m200[6], m200[7], m200[8], m200[9], m200[10], m200[11]));
        Assert.Equal(("01", "27000,00", "27000,00", "1,65", "", "", "445,50"), (Reg(sa, "M210")[0], Reg(sa, "M210")[1], Reg(sa, "M210")[2], Reg(sa, "M210")[3], Reg(sa, "M210")[4], Reg(sa, "M210")[5], Reg(sa, "M210")[6]));
        Assert.Equal(("101", "6000,00", "1,65", "99,00"), (Reg(sa, "M100")[0] + "", Reg(sa, "M100")[2], Reg(sa, "M100")[3], Reg(sa, "M100")[6]));
        // COFINS (M600)
        var m600 = Reg(sa, "M600");
        Assert.Equal(("2052,00", "456,00", "1596,00", "1596,00"), (m600[0], m600[1], m600[3], m600[11]));
        // os totais de C170 de PIS/COFINS das saídas fecham com a apuração
        var pisC170 = Regs(sa, "C170").Where(f => f[23] == "01").Sum(f => decimal.Parse(f[28].Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture));
        var pisA170 = Regs(sa, "A170").Where(f => f[7] == "01").Sum(f => decimal.Parse(f[10].Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(445.50m, pisC170 + pisA170);

        // bloco 9
        var reais = linhas.GroupBy(l => l.Split('|')[1]).ToDictionary(g => g.Key, g => g.Count());
        foreach (var f in Regs(sa, "9900")) Assert.Equal(reais[f[0]], int.Parse(f[1]));
        Assert.Equal(linhas.Length, int.Parse(Reg(sa, "9999")[0]));
        foreach (var bloco in "0ACDFIMP1") Assert.Equal(linhas.Count(l => l[1] == bloco), int.Parse(Reg(sa, $"{bloco}990")[0]));

        Assert.True(Assert.Single(assinador.LerAssinaturas(arq.Conteudo)).Valida);
        Assert.Equal(TipoEfd.Contribuicoes, (await efd.ListarGeradasAsync(empresa, null, adm, ct)).Valor.Single().Tipo);
    }

    [Fact]
    public async Task Configuracao_da_efd_e_permissao_do_perfil_contador()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await EmpresaCompletaAsync(75);
        var adm = await db.AdminAsync();
        var efd = db.App.Fiscal().Efd;
        Assert.Equal('A', (await efd.ConfiguracaoAsync(empresa, adm, ct)).Valor.IcmsPerfil);
        Assert.True((await efd.SalvarConfiguracaoAsync(empresa, new ConfiguracaoEfd('C', '0', "046-2", "063-2", 15, "01", '1'), adm, ct)).Sucesso);
        var c = (await efd.ConfiguracaoAsync(empresa, adm, ct)).Valor;
        Assert.Equal(('C', '0', "046-2", 15, "01", '1'), (c.IcmsPerfil, c.IcmsIndicadorAtividade, c.IcmsCodigoReceita, c.IcmsDiaVencimento, c.ContribNaturezaPj, c.ContribIndicadorAtividade));
        Assert.False((await efd.SalvarConfiguracaoAsync(empresa, c with { IcmsPerfil = 'Z' }, adm, ct)).Sucesso);
        Assert.True(await db.EscalarAsync<long>("SELECT count(*) FROM core.perfil_permissao pp JOIN core.perfil p ON p.id = pp.perfil_id WHERE pp.permissao = 'FISCAL_SPED_GERAR' AND p.nome = 'Contador'") == 1);
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM core.perfil_permissao pp JOIN core.perfil p ON p.id = pp.perfil_id WHERE pp.permissao = 'FISCAL_SPED_GERAR' AND p.nome = 'Consulta'"));
    }

    [Fact]
    public async Task Assistente_percorre_os_passos_gera_salva_e_lista_o_historico()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await EmpresaCompletaAsync(76);
        var cnpj = await db.EscalarAsync<string>("SELECT cnpj FROM core.empresa WHERE id = @e", ("e", empresa));
        var adm = await db.AdminAsync();
        await db.App.Fiscal().Documentos.ImportarAsync(empresa,
            [Arq("v.xml", XmlsDeTeste.Nfe(cnpj, XmlsDeTeste.CnpjCliente, 1, 1, Mar, [new ItemNfe("5102", 1_000m, 180m)], chave: Chave(cnpj, 1)))], adm, ct: ct);

        var vm = new EfdIcmsAssistenteViewModel(new ContextoDeTeste(db.App, adm, empresa)) { Ano = 2026, Mes = 2 };
        await vm.CarregarAsync();
        vm.CodigoReceita = "046-2";   // a configuração da empresa (vazia) é carregada ao abrir
        Assert.Contains("Passo 1 de 5", vm.TituloPasso);
        await vm.AvancarCommand.ExecuteAsync(null);
        await vm.AvancarCommand.ExecuteAsync(null);
        await vm.AvancarCommand.ExecuteAsync(null);
        Assert.Empty(vm.Erros);
        Assert.Equal(4, vm.Passo);
        Assert.True(vm.PodeAvancarValidacao);
        Assert.Contains(vm.Resumo, r => r.Rotulo == "ICMS a recolher" && r.Valor == "180,00");
        await vm.AvancarCommand.ExecuteAsync(null);
        Assert.Equal(5, vm.Passo);

        var pasta = Path.Combine(Path.GetTempPath(), "efd_" + Guid.NewGuid().ToString("N"));
        vm.Pasta = pasta;
        await vm.GerarCommand.ExecuteAsync(null);
        Assert.True(vm.Gerado, string.Join(";", vm.Erros));
        vm.SalvarCommand.Execute(null);
        try
        {
            Assert.True(vm.Salvo);
            Assert.Equal(vm.Arquivo!.Conteudo, await File.ReadAllBytesAsync(vm.CaminhoSalvo, ct));
        }
        finally { Directory.Delete(pasta, true); }
        await vm.CarregarGeradasCommand.ExecuteAsync(null);
        Assert.Single(vm.Geradas);

        // EFD-Contribuições sem apuração: o assistente para na validação
        var contrib = new EfdContribuicoesAssistenteViewModel(new ContextoDeTeste(db.App, adm, empresa)) { Ano = 2026, Mes = 2 };
        await contrib.CarregarAsync();
        await contrib.AvancarCommand.ExecuteAsync(null);
        await contrib.AvancarCommand.ExecuteAsync(null);
        await contrib.AvancarCommand.ExecuteAsync(null);
        Assert.Equal(3, contrib.Passo);
        Assert.False(contrib.PodeAvancarValidacao);
        Assert.Contains(contrib.Pendencias, p => p.Bloqueante && p.Mensagem.Contains("apuração"));
    }
}
