using Vorticis.Domain.Sped;
using Vorticis.Fiscal.Desktop.ViewModels;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Relatorios;
using Vorticis.Fiscal.Sped;

namespace Vorticis.Fiscal.Tests;

public class AjustesRegrasTests
{
    [Theory]
    [InlineData("SP020001", "ICMS", 'E', true)]    // outros créditos na entrada
    [InlineData("SP030001", "ICMS", 'E', true)]    // estorno de débito na entrada
    [InlineData("SP000001", "ICMS", 'E', false)]   // outros débitos não vale na entrada
    [InlineData("SP000001", "ICMS", 'S', true)]
    [InlineData("SP040001", "ICMS", 'S', true)]    // dedução na saída
    [InlineData("SP020001", "ICMS", 'S', false)]
    [InlineData("SP120001", "ICMS_ST", 'E', true)]
    [InlineData("SP020001", "ICMS_ST", 'E', false)] // código de ICMS próprio não vale para ST
    [InlineData("A01", "IPI", 'S', true)]
    [InlineData("A0", "IPI", 'S', false)]
    public void Valida_codigo_por_imposto_e_sentido(string codigo, string imposto, char sentido, bool valido) =>
        Assert.Equal(valido, OutrosLancamentosEfd.ValidarAjuste(codigo, "desc", 10m, "SP", imposto, sentido).Count == 0);
}

public class AjustesApuracaoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private static readonly DateOnly Mar = new(2026, 3, 10);

    private static string Chave(string cnpjEmit, int numero)
    {
        var b = $"352603{cnpjEmit}55001{numero:D9}1{numero:D8}";
        var soma = 0; var peso = 2;
        for (var i = b.Length - 1; i >= 0; i--) { soma += (b[i] - '0') * peso; peso = peso == 9 ? 2 : peso + 1; }
        var r = soma % 11;
        return b + (r < 2 ? 0 : 11 - r);
    }

    private async Task<long> EmpresaCompletaAsync(int semente)
    {
        var empresa = await db.CriarEmpresaAsync(CnpjsDeTeste.Valido(semente));
        await db.EscalarAsync<long>("""
            UPDATE core.empresa SET inscricao_estadual = '123456789012', cep = '01310100', logradouro = 'Av. Paulista', numero = '1000', bairro = 'Bela Vista', email = 'a@b.com', telefone = '1130000000'
             WHERE id = @e RETURNING id
            """, ("e", empresa));
        var contador = await db.EscalarAsync<long>("INSERT INTO core.contador (nome, cpf, crc, uf_crc, email) VALUES ('Contador de Teste', @c, '1SP123456/O-7', 'SP', 'c@c.com') RETURNING id",
            ("c", $"{(123_456_000L + semente):D9}00"[..11]));
        await db.EscalarAsync<long>("INSERT INTO core.empresa_contabilidade (empresa_id, contador_id, inicio, responsavel_tecnico) VALUES (@e, @c, '2025-01-01', TRUE) RETURNING id", ("e", empresa), ("c", contador));
        return empresa;
    }

    [Fact]
    public async Task Ajustes_de_entrada_e_saida_codigos_efd_e_relatorios_de_apuracao()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await EmpresaCompletaAsync(81);
        var cnpj = await db.EscalarAsync<string>("SELECT cnpj FROM core.empresa WHERE id = @e", ("e", empresa));
        var adm = await db.AdminAsync();
        var fiscal = db.App.Fiscal();
        var ctx = new ContextoDeTeste(db.App, adm, empresa);
        var imp = await fiscal.Documentos.ImportarAsync(empresa,
            [new ArquivoXml("v.xml", XmlsDeTeste.Bytes(XmlsDeTeste.Nfe(cnpj, XmlsDeTeste.CnpjCliente, 1, 1, Mar, [new ItemNfe("5102", 10_000m, 1_800m)], chave: Chave(cnpj, 1))))], adm, ct: ct);
        Assert.True(imp.Sucesso, string.Join(";", imp.Erros));

        // códigos de ajuste (cadastro simples)
        var codigos = new CodigosAjusteViewModel(ctx);
        await codigos.CarregarAsync();
        codigos.Codigo = "SP020001"; codigos.Descricao = "Crédito de teste";
        await codigos.GravarCommand.ExecuteAsync(null);
        Assert.Empty(codigos.Erros);
        Assert.Single(codigos.Lista);
        codigos.Codigo = "XX"; codigos.Descricao = "inválido";
        await codigos.GravarCommand.ExecuteAsync(null);
        Assert.NotEmpty(codigos.Erros);

        // ajustes de entrada: aceita crédito, recusa débito
        var entrada = new AjustesEntradaViewModel(ctx) { Competencia = new DateTime(2026, 3, 1) };
        await entrada.CarregarAsync();
        Assert.Single(entrada.Codigos);
        (entrada.Codigo, entrada.Descricao, entrada.Valor) = ("SP000001", "débito na tela errada", "10,00");
        await entrada.GravarCommand.ExecuteAsync(null);
        Assert.Contains(entrada.Erros, e => e.Contains("Ajustes de entrada"));
        (entrada.Codigo, entrada.Descricao, entrada.Valor) = ("SP020001", "Crédito de teste", "50,00");
        await entrada.GravarCommand.ExecuteAsync(null);
        Assert.Empty(entrada.Erros);
        entrada.Imposto = 1;   // ICMS-ST: outros créditos ST
        (entrada.Codigo, entrada.Descricao, entrada.Valor) = ("SP120001", "Crédito ST de teste", "10,00");
        await entrada.GravarCommand.ExecuteAsync(null);
        Assert.Empty(entrada.Erros);
        Assert.Equal(2, entrada.Lista.Count);

        // ajustes de saída: outros débitos com observação e documento relacionado (E112/E113), e IPI
        var saida = new AjustesSaidaViewModel(ctx) { Competencia = new DateTime(2026, 3, 1) };
        await saida.CarregarAsync();
        Assert.Empty(saida.Lista);
        (saida.Codigo, saida.Descricao, saida.Valor) = ("SP020001", "crédito na tela errada", "5,00");
        await saida.GravarCommand.ExecuteAsync(null);
        Assert.Contains(saida.Erros, e => e.Contains("Ajustes de saída"));
        (saida.Codigo, saida.Descricao, saida.Valor, saida.Observacao) = ("SP000010", "Débito de teste", "100,00", "Conforme processo 123");
        (saida.DocParticipante, saida.DocNumero, saida.DocSerie, saida.DocData) = (XmlsDeTeste.CnpjCliente, "1", "1", new DateTime(2026, 3, 10));
        await saida.GravarCommand.ExecuteAsync(null);
        Assert.Empty(saida.Erros);
        saida.Imposto = 2;
        (saida.Codigo, saida.Descricao, saida.Valor, saida.Observacao) = ("A01", "Débito de IPI", "7,00", "");
        saida.DocParticipante = ""; saida.DocNumero = ""; saida.DocData = null;
        await saida.GravarCommand.ExecuteAsync(null);
        Assert.Empty(saida.Erros);
        Assert.Equal(2, saida.Lista.Count);
        Assert.Contains("Débito", saida.Lista.Single(l => l.Imposto == "IPI").Tipo);

        // EFD: E111/E112/E113, E220 (ST), E520/E530 (IPI) e a apuração com os ajustes
        var efd = await fiscal.Efd.GerarIcmsAsync(empresa, Mar, new OpcoesEfdIcms('A', '0', CodigoReceitaIcms: "046-2"), adm, null, ct);
        Assert.True(efd.Sucesso, string.Join(";", efd.Erros));
        var reg = efd.Valor.Registros;
        Assert.Equal((2, 1, 1, 1, 1), (reg["E111"], reg["E112"], reg["E113"], reg["E220"], reg["E530"]));
        var linhas = FormatoSped.Latin1.GetString(efd.Valor.Conteudo).Split("\r\n");
        var e110 = linhas.Single(l => l.StartsWith("|E110|")).Split('|');
        Assert.Equal(("1800,00", "100,00", "50,00", "1850,00"), (e110[2], e110[4], e110[8], e110[13]));   // débitos, outros débitos, outros créditos, recolher
        Assert.Contains(linhas, l => l.StartsWith("|E530|0|7,00|A01|"));

        // relatórios de apuração
        var sped = (await fiscal.Efd.ApuracaoSpedAsync(empresa, Mar, 0m, adm, ct)).Valor;
        var resumo = TabelasFiscais.ResumoApuracaoIcms(sped);
        Assert.Equal(1850m, resumo.Linhas.Single(l => (string?)l.Valores[0] == "13").Valores[2]);
        var conf = TabelasFiscais.ConferenciaEfd(sped);
        Assert.Contains(conf.Linhas, l => l.Valores[0]?.ToString()!.Contains("Bloco E") == true);
        Assert.Contains(conf.Linhas, l => l.Valores[9]?.ToString() == "Confere");
        Assert.DoesNotContain(conf.Linhas, l => l.Valores[9]?.ToString()!.StartsWith("DIVERGE") == true);
        var ipi = TabelasFiscais.ApuracaoIpi(sped);
        Assert.Contains(ipi.Linhas, l => l.Valores[0]?.ToString()!.Contains("Débito de IPI") == true);
        Assert.Equal(7m, sped.Ipi!.SaldoDevedor);
        var st = TabelasFiscais.ApuracaoIcmsSt(sped);
        Assert.Contains(st.Linhas, l => l.Valores[0]?.ToString()!.Contains("Crédito ST de teste") == true);
        var relacao = (await fiscal.Efd.RelacaoAjustesAsync(empresa, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 1), null, adm, ct)).Valor;
        Assert.Equal(4, relacao.Count);
        Assert.Single((await fiscal.Efd.RelacaoAjustesAsync(empresa, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 1), "A01", adm, ct)).Valor);
        var tabRel = TabelasFiscais.RelacaoAjustes(relacao, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 1));
        Assert.All(tabRel.Linhas, l => Assert.Equal(tabRel.Colunas.Count, l.Valores.Count));
        Assert.Equal(167m, tabRel.Linhas[^1].Valores[^1]);

        // livro de apuração mostra os ajustes
        var livro = TabelasFiscais.LivroApuracaoIcms((await fiscal.Efd.LivroApuracaoAsync(empresa, Mar, 0m, adm, ct)).Valor);
        Assert.Equal(100m, livro.Linhas.Single(l => l.Valores[0]!.ToString()!.Contains("Outros débitos")).Valores[4]);

        // telas
        foreach (var vm in new ApuracaoSpedViewModel[] { new ResumoApuracaoIcmsViewModel(ctx), new ApuracaoIpiSpedViewModel(ctx), new ApuracaoIcmsStViewModel(ctx), new ConferenciaEfdViewModel(ctx) })
        {
            vm.Mes = 2; vm.Ano = "2026";
            await vm.GerarCommand.ExecuteAsync(null);
            Assert.Empty(vm.Erros);
            Assert.Single(vm.Exibidas);
        }
        var rel = new RelacaoAjustesViewModel(ctx) { Inicio = new DateTime(2026, 1, 1), Fim = new DateTime(2026, 12, 1) };
        await rel.GerarCommand.ExecuteAsync(null);
        Assert.Single(rel.Exibidas);
        Assert.Equal(4 + 4 + 1, rel.Exibidas[0].Linhas.Count);   // 4 ajustes, 4 totais por código e o total geral
    }
}
