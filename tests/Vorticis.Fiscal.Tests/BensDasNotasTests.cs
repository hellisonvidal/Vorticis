using Vorticis.Application.Imobilizado;
using Vorticis.Domain.Imobilizado;
using Vorticis.Fiscal.Documentos;

namespace Vorticis.Fiscal.Tests;

/// <summary>Itens de notas de entrada que são ativo imobilizado viram "bens a cadastrar" (qualquer caminho de entrada) e o bem fica vinculado ao item.</summary>
public class BensDasNotasTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private static readonly DateOnly Mar = new(2026, 3, 10);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static BemImobilizado Bem(BemACadastrar p, GrupoBem g) =>
        new(0, 0, p.Descricao, g.Id, "P-1", "Sala 1", p.NotaTexto, p.CodigoFornecedor, p.DataAquisicao, p.InicioUso, p.Valor, 0, g.VidaUtilFiscalMeses,
            MetodoDepreciacao.Linear, 1, 0, g.ContaBem!, g.ContaAcumulada, g.ContaDespesa, null, null);

    [Fact]
    public async Task Nota_de_entrada_importada_ou_digitada_gera_pendencia_e_o_bem_fica_vinculado_ao_item()
    {
        BancoDeTeste.ExigirBanco();
        var cnpj = CnpjsDeTeste.Valido(7301);
        var empresa = await db.CriarEmpresaAsync(cnpj);
        var adm = await db.AdminAsync();
        var imo = db.App.Imobilizado;
        var fiscal = db.App.Fiscal();

        // XML de entrada: só o item de CFOP 1551 (compra para o ativo) vira pendência
        var xml = XmlsDeTeste.Nfe(XmlsDeTeste.CnpjFornecedor, cnpj, 1, 910, Mar, [new ItemNfe("1551", 4000m, Produto: "Notebook"), new ItemNfe("1102", 500m, Produto: "Papel")],
                                  nomeEmit: "Fornecedor Alfa");
        var imp = await fiscal.Documentos.ImportarAsync(empresa, [new ArquivoXml("ativo.xml", XmlsDeTeste.Bytes(xml))], adm, ct: Ct);
        Assert.Equal(1, imp.Valor.Importados);
        var pendentes = await imo.ListarBensACadastrarAsync(empresa, adm, ct: Ct);
        var p = Assert.Single(pendentes);
        Assert.Equal(("Notebook", 4000m, "910", "CFOP", BemACadastrar.Pendente), (p.Descricao, p.Valor, p.Nota, p.Motivo, p.Situacao));
        Assert.Equal(Mar, p.DataAquisicao);
        Assert.NotNull(p.Chave);
        Assert.Equal("Fornecedor Alfa", p.Fornecedor);
        Assert.NotNull(p.GrupoSugeridoId);            // NCM 8471: computadores

        // reimportar o mesmo XML não duplica
        await fiscal.Documentos.ImportarAsync(empresa, [new ArquivoXml("ativo.xml", XmlsDeTeste.Bytes(xml))], adm, ct: Ct);
        Assert.Single(await imo.ListarBensACadastrarAsync(empresa, adm, ct: Ct));
        Assert.Single(await imo.ListarBensACadastrarAsync(empresa, adm, true, null, null, [p.Chave!], Ct));   // filtro pela chave da nota
        Assert.Empty(await imo.ListarBensACadastrarAsync(empresa, adm, true, null, null, [new string('1', 44)], Ct));

        // nota digitada de entrada com CFOP 2551
        var part = await db.EscalarAsync<long>("INSERT INTO core.participante (empresa_id, codigo, nome, cpf_cnpj, uf, cliente, fornecedor) VALUES (@e, '55', 'Móveis Beta', @d, 'SP', FALSE, TRUE) RETURNING id",
            ("e", empresa), ("d", XmlsDeTeste.CnpjFornecedor));
        var item = new ItemFiscal(0, null, "Mesa de reunião", null, null, "2551", "UN", 1, 3000m, 0, 0, 0, 0, 0, 0, "00", "0", 0, 0, 0, 0, 0, null, 0, 0, 0, null, 0, 0, 0, null, 0, 0, 0, 0, 0, 0);
        var totais = CalculadoraNota.Totalizar([CalculadoraNota.Completar(item)], new TotaisDocumento(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, 0, 0, 0, 0));
        var gravada = await fiscal.Digitacao.GravarAsync(empresa, new DocumentoDigitado(0, TipoOperacao.Entrada, ModeloDocumento.NFe55, "1", "911", null, Mar, Mar.AddDays(2), "Compra", part, null, totais, [item]), adm, Ct);
        Assert.True(gravada.Sucesso, string.Join(";", gravada.Erros));
        var doc = Assert.Single(await imo.ListarBensACadastrarAsync(empresa, adm, true, null, [gravada.Valor], null, Ct));
        Assert.Equal((3000m, Mar.AddDays(2), "55"), (doc.Valor, doc.InicioUso, doc.CodigoFornecedor));
        Assert.Equal(2, (await imo.ListarBensACadastrarAsync(empresa, adm, ct: Ct)).Count);

        // grava o bem do primeiro item: vira CADASTRADO, vinculado, e não aparece mais como pendente
        var grupo = (await imo.ListarGruposAsync(Ct)).Single(g => g.Codigo == "08");
        var r = await imo.GravarBemDaNotaAsync(empresa, p.Id, Bem(p, grupo), adm, Ct);
        Assert.True(r.Sucesso, string.Join(";", r.Erros));
        var bem = Assert.Single(await imo.ListarBensAsync(empresa, adm, Ct));
        Assert.Equal(("Notebook", 4000m, "910/1"), (bem.Descricao, bem.Valor, bem.NotaFiscal));
        var tratado = Assert.Single((await imo.ListarBensACadastrarAsync(empresa, adm, false, [p.Id], ct: Ct)));
        Assert.Equal((BemACadastrar.Cadastrado, r.Valor), (tratado.Situacao, tratado.BemId));
        Assert.DoesNotContain(await imo.ListarBensACadastrarAsync(empresa, adm, ct: Ct), x => x.Id == p.Id);

        // o mesmo item não vira dois bens, nem depois de reprocessar a nota
        var repetido = await imo.GravarBemDaNotaAsync(empresa, p.Id, Bem(p, grupo), adm, Ct);
        Assert.Contains("já foi tratado", repetido.Erros[0]);
        Assert.Single(await imo.ListarBensAsync(empresa, adm, Ct));

        // permissão: quem não tem acesso à empresa não grava, não descarta e não enxerga
        var sem = await db.EscalarAsync<long>("INSERT INTO core.usuario (login, nome, senha_hash, administrador) VALUES ('sem_acesso_bens', 'Sem acesso', 'x', FALSE) RETURNING id");
        Assert.False((await imo.GravarBemDaNotaAsync(empresa, doc.Id, Bem(doc, grupo), sem, Ct)).Sucesso);
        Assert.False((await imo.DescartarBemACadastrarAsync(empresa, doc.Id, sem, Ct)).Sucesso);
        Assert.Empty(await imo.ListarBensACadastrarAsync(empresa, sem, ct: Ct));

        // descartar: sai da lista e não pode ser tratado de novo
        Assert.True((await imo.DescartarBemACadastrarAsync(empresa, doc.Id, adm, Ct)).Sucesso);
        Assert.False((await imo.DescartarBemACadastrarAsync(empresa, doc.Id, adm, Ct)).Sucesso);
        Assert.Contains("já foi tratado", (await imo.GravarBemDaNotaAsync(empresa, doc.Id, Bem(doc, grupo), adm, Ct)).Erros[0]);
        Assert.Empty(await imo.ListarBensACadastrarAsync(empresa, adm, ct: Ct));
        Assert.Equal(2, (await imo.ListarBensACadastrarAsync(empresa, adm, false, ct: Ct)).Count);

        // excluir o bem devolve o item à lista de pendências
        Assert.True((await imo.ExcluirBemAsync(empresa, bem.Id, adm, Ct)).Sucesso);
        Assert.Equal(p.Id, Assert.Single(await imo.ListarBensACadastrarAsync(empresa, adm, ct: Ct)).Id);
    }

    [Fact]
    public void Grupo_e_sugerido_pela_conta_da_regra_ou_pelo_ncm()
    {
        var comp = new GrupoBem(1, "08", "Computadores", 20m, true, false, "1.2.03.004", "1.2.03.007", "4.2.02.007", null);
        var moveis = new GrupoBem(2, "04", "Móveis", 10m, true, false, "1.2.03.001", "1.2.03.007", "4.2.02.007", null);
        GrupoBem[] todos = [comp, moveis];
        Assert.Same(moveis, SugestaoGrupoBem.Sugerir(todos, "1.2.03.001", "84714900"));   // a conta da regra vence o NCM
        Assert.Same(comp, SugestaoGrupoBem.Sugerir(todos, null, "8471.49.00"));
        Assert.Same(moveis, SugestaoGrupoBem.Sugerir(todos, null, "94036000"));
        Assert.Null(SugestaoGrupoBem.Sugerir(todos, null, "10063021"));
        Assert.Null(SugestaoGrupoBem.Sugerir(todos, "9.9.9", null));
    }
}
