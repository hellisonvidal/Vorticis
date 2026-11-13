using Vorticis.Application.Cadastros;
using Vorticis.Escritorio;
using Vorticis.Infra.Tests;

namespace Vorticis.Modulos.Tests;

public class RegrasEscritorioTests
{
    [Fact]
    public void Recorrencia_mantem_o_dia_e_usa_o_ultimo_dia_do_mes_curto()
    {
        Assert.Equal(new DateOnly(2026, 2, 28), RegrasEscritorio.Proxima(new DateOnly(2026, 1, 31), Recorrencia.Mensal));
        Assert.Equal(new DateOnly(2027, 1, 20), RegrasEscritorio.Proxima(new DateOnly(2026, 1, 20), Recorrencia.Anual));
        Assert.Null(RegrasEscritorio.Proxima(new DateOnly(2026, 1, 20), Recorrencia.Nenhuma));
    }

    [Fact]
    public void Gera_cobranca_so_para_contratos_vigentes_e_sem_cobranca_no_mes()
    {
        var comp = new DateOnly(2026, 2, 1);
        ContratoHonorario[] contratos =
        [
            new(1, 10, 800m, 31, new DateOnly(2025, 1, 1), null, "NENHUM", null),
            new(2, 20, 500m, 5, new DateOnly(2026, 3, 1), null, "NENHUM", null),     // ainda não começou
            new(3, 30, 600m, 5, new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 31), "NENHUM", null), // já terminou
            new(4, 40, 700m, 10, new DateOnly(2025, 1, 1), null, "NENHUM", null),    // já cobrada
        ];
        var r = RegrasEscritorio.GerarCobrancas(contratos, comp, new HashSet<long> { 40 });
        var c = Assert.Single(r);
        Assert.Equal((10L, new DateOnly(2026, 2, 28), 800m), (c.PessoaId, c.Vencimento, c.Valor));            // regime de caixa: mesmo mês
        var seguinte = Assert.Single(RegrasEscritorio.GerarCobrancas(contratos, comp, new HashSet<long> { 40 }, vencimentoNoMesSeguinte: true));
        Assert.Equal((comp, new DateOnly(2026, 3, 31)), (seguinte.Competencia, seguinte.Vencimento));           // competência: mês seguinte
    }

    [Fact]
    public void Honorario_anual_so_no_mes_do_inicio_e_especial_nunca_gera_sozinho()
    {
        ContratoHonorario[] contratos =
        [
            new(1, 10, 800m, 10, new DateOnly(2025, 1, 1), null, "NENHUM", null),
            new(2, 20, 6000m, 15, new DateOnly(2025, 3, 1), null, "NENHUM", null, null, PeriodicidadeHonorario.Anual),
            new(3, 30, 900m, 5, new DateOnly(2025, 1, 1), null, "NENHUM", null, null, PeriodicidadeHonorario.Especial),
        ];
        Assert.Equal([10L], RegrasEscritorio.GerarCobrancas(contratos, new DateOnly(2026, 2, 1), new HashSet<long>()).Select(c => c.PessoaId));
        Assert.Equal([10L, 20L], RegrasEscritorio.GerarCobrancas(contratos, new DateOnly(2026, 3, 1), new HashSet<long>()).Select(c => c.PessoaId));
    }

    [Fact]
    public void Parcelas_seguem_a_frequencia_e_o_vencimento_se_ajusta_ao_fim_do_mes()
    {
        var p = RegrasEscritorio.Parcelas(7, new DateOnly(2026, 12, 1), 500m, 31, FrequenciaParcelas.Mensal, 3);
        Assert.Equal([new DateOnly(2026, 12, 1), new DateOnly(2027, 1, 1), new DateOnly(2027, 2, 1)], p.Select(x => x.Competencia));
        Assert.Equal([new DateOnly(2026, 12, 31), new DateOnly(2027, 1, 31), new DateOnly(2027, 2, 28)], p.Select(x => x.Vencimento));
        Assert.Equal(["1/3", "2/3", "3/3"], p.Select(x => x.ParcelaTexto));
        var tri = RegrasEscritorio.Parcelas(7, new DateOnly(2026, 1, 1), 300m, 10, FrequenciaParcelas.Trimestral, 4);
        Assert.Equal([1, 4, 7, 10], tri.Select(x => x.Competencia.Month));
        var unica = Assert.Single(RegrasEscritorio.Parcelas(7, new DateOnly(2026, 5, 1), 1200m, 20, FrequenciaParcelas.Unica, 9, TipoCobranca.Avulsa, "IRPF"));
        Assert.Equal(("", new DateOnly(2026, 5, 20), TipoCobranca.Avulsa, "IRPF"), (unica.ParcelaTexto, unica.Vencimento, unica.Tipo, unica.Descricao));
    }

    [Fact]
    public void Contrato_gera_parcelas_pelo_prazo_e_periodicidade()
    {
        Assert.Equal(new DateOnly(2028, 6, 30), RegrasEscritorio.FimPeloPrazo(new DateOnly(2027, 7, 1), 12));
        Assert.Null(RegrasEscritorio.FimPeloPrazo(new DateOnly(2027, 7, 1), 0));
        Assert.Equal(12, RegrasEscritorio.PrazoMeses(new DateOnly(2026, 9, 25), new DateOnly(2027, 9, 24)));
        Assert.Equal(24, RegrasEscritorio.PrazoMeses(new DateOnly(2027, 1, 1), new DateOnly(2028, 12, 31)));
        Assert.Null(RegrasEscritorio.PrazoMeses(new DateOnly(2027, 1, 1), null));
        var c = new ContratoEscritorio(0, 3, 7, ModeloContrato.Cliente, "x", 250m, Periodicidade.Mensal, 5, new DateOnly(2026, 9, 25), null, "NENHUM", null, "-");
        var mensal = RegrasEscritorio.ParcelasDoContrato(c, 12, vencimentoNoMesSeguinte: true);
        Assert.Equal(12, mensal.Count);
        Assert.Equal((new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 5), "Contrato nº 3", "1/12"), (mensal[0].Competencia, mensal[0].Vencimento, mensal[0].Descricao, mensal[0].ParcelaTexto));
        Assert.Equal(new DateOnly(2027, 8, 1), mensal[^1].Competencia);
        var anual = RegrasEscritorio.ParcelasDoContrato(c with { Periodicidade = Periodicidade.Anual }, 24, vencimentoNoMesSeguinte: false);
        Assert.Equal([new DateOnly(2026, 9, 5), new DateOnly(2027, 9, 5)], anual.Select(p => p.Vencimento));
        Assert.Single(RegrasEscritorio.ParcelasDoContrato(c with { Periodicidade = Periodicidade.Unico }, 12, false));
    }

    [Fact]
    public void Regime_de_competencia_vence_no_mes_seguinte_e_a_vista_ja_entra_recebida()
    {
        var comp = RegrasEscritorio.Parcelas(7, new DateOnly(2026, 12, 1), 500m, 31, FrequenciaParcelas.Mensal, 3, vencimentoNoMesSeguinte: true);
        Assert.Equal([new DateOnly(2026, 12, 1), new DateOnly(2027, 1, 1), new DateOnly(2027, 2, 1)], comp.Select(x => x.Competencia));
        Assert.Equal([new DateOnly(2027, 1, 31), new DateOnly(2027, 2, 28), new DateOnly(2027, 3, 31)], comp.Select(x => x.Vencimento));
        var avista = Assert.Single(RegrasEscritorio.Parcelas(7, new DateOnly(2026, 5, 1), 1200m, 20, FrequenciaParcelas.Mensal, 12, TipoCobranca.Avulsa, "IRPF",
                                                             pagoAVistaEm: new DateOnly(2026, 5, 8)));
        Assert.Equal((new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 8), SituacaoCobranca.Recebida, new DateOnly(2026, 5, 8), 1200m),
                     (avista.Competencia, avista.Vencimento, avista.Situacao, avista.RecebidoEm, (decimal)avista.ValorRecebido!));
    }

    [Fact]
    public void Gerador_de_contratos_qualifica_as_partes_e_numera_as_clausulas()
    {
        var esc = new DadosEscritorio("Escritório Modelo Ltda", "11222333000181", "Rua A, 10", "Campinas", "SP");
        var cli = new PessoaEscritorio(1, 1, true, false, "Padaria Boa", "11444777000161");
        var c = new ContratoEscritorio(0, 0, 1, ModeloContrato.Cliente, GeradorContratos.ObjetoPadrao(ModeloContrato.Cliente), 1500m, Periodicidade.Mensal, 10,
                                       new DateOnly(2026, 1, 1), null, "IPCA", null, "-");
        var texto = GeradorContratos.Gerar(esc, cli, c);
        Assert.StartsWith("CONTRATANTE: Padaria Boa, inscrita no CNPJ sob o nº 11.444.777/0001-61", texto);
        Assert.Contains("CONTRATADA: Escritório Modelo Ltda", texto);
        Assert.Contains("R$ 1.500,00 por mês, com vencimento no dia 10", texto);
        Assert.Contains("variação do IPCA", texto);
        Assert.Contains("pelo prazo de 12 meses", texto);                       // sem fim informado: o prazo do contrato
        Assert.Contains("comarca de Campinas", texto);
        var forn = GeradorContratos.Gerar(esc, cli with { Cliente = false, Fornecedor = true }, c with { Modelo = ModeloContrato.Fornecedor });
        Assert.StartsWith("CONTRATANTE: Escritório Modelo Ltda", forn);
        var doc = GeradorContratos.Documento(esc, cli, c with { Texto = texto, Numero = 7 });
        Assert.Equal("CONTRATO DE PRESTAÇÃO DE SERVIÇOS CONTÁBEIS Nº 7", doc.Titulo);
        Assert.Equal(10, doc.Blocos.Count);
        Assert.NotEmpty(RegrasEscritorio.Validar(new PessoaEscritorio(0, 1, false, false, "", "123")));
        Assert.Empty(RegrasEscritorio.Validar(cli));
    }
}

public class EscritorioIntegracaoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Cliente_com_modulos_marcados_vira_empresa_nesses_modulos()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var escritorio = await db.CriarEmpresaAsync("52938461000103");
        var svc = ServicosEscritorio.De(db.App);
        const string cnpj = "41827365000171";
        var basico = new PessoaEscritorio(0, 50, true, false, "Mercado Bom Preço Ltda", cnpj, "Simples Nacional", Modulos: ModulosEmpresa.Contabil | ModulosEmpresa.Fiscal);

        // sem os dados de empresa, não cadastra nos módulos (nem grava o cliente)
        var falta = await svc.GravarPessoaAsync(escritorio, basico, adm, ct);
        Assert.False(falta.Sucesso);
        Assert.Contains(falta.Erros, e => e.Contains("CNAE"));

        var completo = basico with
        {
            NaturezaJuridica = "2062", Cnae = "4711302", Uf = "SP", CodMunicipioIbge = "3550308", Municipio = "São Paulo", InicioAtividades = new DateOnly(2020, 5, 4),
            Logradouro = "Rua das Flores", Numero = "10", Bairro = "Centro", Cep = "01001000",
        };
        var r = await svc.GravarPessoaAsync(escritorio, completo, adm, ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        var p = Assert.Single(await svc.ListarPessoasAsync(escritorio, adm, ct), x => x.Id == r.Valor);
        Assert.NotNull(p.EmpresaVinculadaId);
        Assert.Equal(ModulosEmpresa.Contabil | ModulosEmpresa.Fiscal, p.Modulos);
        Assert.Equal("Rua das Flores, 10, Centro", p.Endereco);
        var empresa = await db.App.Empresas.ObterAsync(p.EmpresaVinculadaId!.Value, ct);
        Assert.NotNull(empresa);
        Assert.Equal(("Mercado Bom Preço Ltda", "4711302"), (empresa.RazaoSocial, empresa.CnaePrincipal));
        Assert.True(await db.EscalarAsync<bool>("SELECT modulo_contabil AND modulo_fiscal AND NOT modulo_folha AND NOT modulo_escritorio FROM core.empresa WHERE id = @e",
                                                ("e", p.EmpresaVinculadaId)));
        Assert.True(await db.EscalarAsync<long>("SELECT COUNT(*) FROM contabil.plano_contas WHERE empresa_id = @e", ("e", p.EmpresaVinculadaId)) > 0);   // plano modelo

        // incluir a Folha no contrato atualiza a mesma empresa (não cria outra)
        Assert.True((await svc.GravarPessoaAsync(escritorio, p with { Modulos = p.Modulos | ModulosEmpresa.Folha }, adm, ct)).Sucesso);
        Assert.Equal(1L, await db.EscalarAsync<long>("SELECT COUNT(*) FROM core.empresa WHERE cnpj = @c", ("c", cnpj)));
        Assert.True(await db.EscalarAsync<bool>("SELECT modulo_folha FROM core.empresa WHERE id = @e", ("e", p.EmpresaVinculadaId)));

        // só clientes vão para os módulos
        Assert.False((await svc.GravarPessoaAsync(escritorio, completo with { Codigo = 51, Cliente = false, Fornecedor = true, Documento = null }, adm, ct)).Sucesso);
    }

    [Fact]
    public async Task Escritorio_e_a_empresa_0001_e_ja_entra_como_o_cliente_0001()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        const string cnpj = "34028316000103";
        var cadastro = await db.App.Empresas.CadastrarAsync(new NovaEmpresa(cnpj, "Contabilidade Exemplo Ltda", "Exemplo Contábil", "2062", "6920601", "SP", "3550308",
            new DateOnly(2015, 1, 1), RegimeTributario.SimplesNacional, 2026, ImportarPlanoModelo: false, Modulos: ModulosEmpresa.Escritorio), ct);
        Assert.True(cadastro.Sucesso, string.Join("; ", cadastro.Erros));
        var escritorio = cadastro.Valor;
        Assert.True(escritorio < 9000);                                                                      // numeração normal (0001 no banco novo)
        Assert.True(await db.EscalarAsync<bool>("SELECT escritorio_proprio FROM core.empresa WHERE id = @e", ("e", escritorio)));

        // o escritório já é o primeiro cliente, ligado à própria empresa, com os dados do cadastro
        var svc = ServicosEscritorio.De(db.App);
        var clientes = await svc.ListarPessoasAsync(escritorio, adm, ct);
        var proprio = Assert.Single(clientes);
        Assert.Equal((1, "Contabilidade Exemplo Ltda", cnpj, "Simples Nacional", "Exemplo Contábil"), (proprio.Codigo, proprio.Nome, proprio.Documento, proprio.Regime, proprio.NomeFantasia));
        Assert.Equal(escritorio, proprio.EmpresaVinculadaId);

        // marcar Contabilidade e Folha no cliente 0001 vale para a própria empresa do escritório (não cria outra)
        var completo = proprio with { CodMunicipioIbge = "3550308", Uf = "SP", Municipio = "São Paulo", Modulos = ModulosEmpresa.Contabil | ModulosEmpresa.Folha };
        var r = await svc.GravarPessoaAsync(escritorio, completo, adm, ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        Assert.Equal(1L, await db.EscalarAsync<long>("SELECT COUNT(*) FROM core.empresa WHERE cnpj = @c", ("c", cnpj)));
        Assert.True(await db.EscalarAsync<bool>("SELECT modulo_escritorio AND modulo_contabil AND modulo_folha FROM core.empresa WHERE id = @e", ("e", escritorio)));

        // o seguinte vem depois: código 2 na lista (em primeiro, o 0001)
        var outro = new PessoaEscritorio(0, 2, true, false, "Padaria Boa", "11444777000161");
        Assert.True((await svc.GravarPessoaAsync(escritorio, outro, adm, ct)).Sucesso);
        Assert.Equal([1, 2], (await svc.ListarPessoasAsync(escritorio, adm, ct)).Select(p => p.Codigo));

        // o cliente do escritório não se exclui
        Assert.Contains("não pode ser excluído", (await svc.ExcluirPessoaAsync(escritorio, proprio.Id, adm, ct)).Erros[0]);

        // no Escritório, a escolha do topo é o escritório; no Contábil ele aparece como a empresa 0001
        var sessao = new Vorticis.Domain.Seguranca.SessaoUsuario(adm, "adm", "Adm", true, false);
        Assert.Contains(await db.App.Usuarios.EmpresasDoModuloAsync(sessao, ModulosEmpresa.Escritorio, ct), e => e.Id == escritorio);
        Assert.Contains(await db.App.Usuarios.EmpresasDoModuloAsync(sessao, ModulosEmpresa.Contabil, ct), e => e.Id == escritorio);
    }

    [Fact]
    public async Task Cadastro_proprio_tarefas_honorarios_protocolo_e_contratos()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var escritorio = await db.CriarEmpresaAsync("27865757000102");
        var svc = ServicosEscritorio.De(db.App);

        // cadastro próprio do escritório (não vira participante nem empresa dos outros módulos)
        var cli = await svc.GravarPessoaAsync(escritorio, new PessoaEscritorio(0, 1, true, false, "Padaria Boa", "11.444.777/0001-61"), adm, ct);
        Assert.True(cli.Sucesso, string.Join("; ", cli.Erros));
        var forn = (await svc.GravarPessoaAsync(escritorio, new PessoaEscritorio(0, 2, false, true, "Papelaria", null), adm, ct)).Valor;
        Assert.False((await svc.GravarPessoaAsync(escritorio, new PessoaEscritorio(0, 3, true, false, "Repetida", "11444777000161"), adm, ct)).Sucesso);
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT COUNT(*) FROM core.participante WHERE empresa_id = @e", ("e", escritorio)));
        Assert.Equal(["Padaria Boa"], (await svc.CarteiraAsync(escritorio, adm, new DateOnly(2026, 3, 1), ct)).Select(c => c.Nome));

        var t = await svc.GravarTarefaAsync(new Tarefa(0, escritorio, cli.Valor, "DCTFWeb", null, new DateOnly(2026, 3, 15), adm, 1, SituacaoTarefa.Pendente, Recorrencia.Mensal), adm, ct);
        Assert.True(t.Sucesso, string.Join("; ", t.Erros));
        var tarefa = Assert.Single(await svc.ListarTarefasAsync(escritorio, cli.Valor, true, adm, ct));
        Assert.Equal("Padaria Boa", tarefa.PessoaNome);
        var prox = await svc.ConcluirTarefaAsync(tarefa, adm, ct);
        Assert.NotNull(prox.Valor);
        Assert.Equal(new DateOnly(2026, 4, 15), Assert.Single(await svc.ListarTarefasAsync(escritorio, null, true, adm, ct)).Vencimento);

        // tarefas a partir das obrigações: cria as não cumpridas e não repete
        var def = new Vorticis.Domain.Obrigacoes.DefinicaoObrigacao("DAS", "DAS", "Receita Federal", default, default, default, 20, 1, 0, 0, default, default, false);
        var itens = new[] { new Vorticis.Application.Obrigacoes.ItemObrigacao(new Vorticis.Domain.Obrigacoes.OcorrenciaObrigacao(def, new(2026, 4), new DateOnly(2026, 5, 20)),
                                                                             Vorticis.Application.Obrigacoes.StatusObrigacao.Pendente, null, null, 10) };
        Assert.Equal(1, (await svc.GerarTarefasDeObrigacoesAsync(escritorio, cli.Valor, itens, adm, adm, ct)).Valor);
        Assert.Equal(0, (await svc.GerarTarefasDeObrigacoesAsync(escritorio, cli.Valor, itens, adm, adm, ct)).Valor);
        Assert.Contains(await svc.ListarTarefasAsync(escritorio, cli.Valor, true, adm, ct), x => x.Titulo.StartsWith("DAS – ") && x.Vencimento == new DateOnly(2026, 5, 20));

        Assert.True((await svc.GravarHonorarioAsync(escritorio, new ContratoHonorario(0, cli.Valor, 900m, 10, new DateOnly(2026, 1, 1), null, "IPCA", null), adm, ct)).Sucesso);
        Assert.Equal(1, (await svc.GerarCobrancasAsync(escritorio, new DateOnly(2026, 3, 1), adm, ct)).Valor);
        Assert.Equal(0, (await svc.GerarCobrancasAsync(escritorio, new DateOnly(2026, 3, 1), adm, ct)).Valor);
        var cob = Assert.Single(await svc.ListarCobrancasAsync(escritorio, new DateOnly(2026, 3, 1), false, adm, ct));
        Assert.True((await svc.ReceberAsync(escritorio, cob, new DateOnly(2026, 3, 10), 900m, adm, ct)).Sucesso);
        Assert.Empty(await svc.ListarCobrancasAsync(escritorio, null, true, adm, ct));
        Assert.Single(await svc.ListarCobrancasAsync(escritorio, new DateOnly(2026, 1, 1), false, adm, ct, ate: new DateOnly(2026, 12, 1)));

        // Financeiro › Honorários: parcelas (recusa a competência que já tem honorário), avulsas no mesmo mês, baixa com observação, estorno e exclusão
        var repetida = await svc.LancarCobrancasAsync(escritorio, RegrasEscritorio.Parcelas(cli.Valor, new DateOnly(2026, 2, 1), 900m, 10, FrequenciaParcelas.Mensal, 3), adm, ct);
        Assert.Contains("03/2026", Assert.Single(repetida.Erros));
        Assert.Equal(3, (await svc.LancarCobrancasAsync(escritorio, RegrasEscritorio.Parcelas(cli.Valor, new DateOnly(2026, 4, 1), 950m, 31, FrequenciaParcelas.Mensal, 3), adm, ct)).Valor);
        Assert.False((await svc.LancarCobrancasAsync(escritorio, RegrasEscritorio.Parcelas(cli.Valor, new DateOnly(2026, 3, 1), 300m, 10, FrequenciaParcelas.Unica, 1, TipoCobranca.Avulsa), adm, ct)).Sucesso);
        for (var i = 0; i < 2; i++)
            Assert.Equal(1, (await svc.LancarCobrancasAsync(escritorio, RegrasEscritorio.Parcelas(cli.Valor, new DateOnly(2026, 3, 1), 300m, 20, FrequenciaParcelas.Unica, 1, TipoCobranca.Avulsa, "Abertura de filial"), adm, ct)).Valor);
        var ano = await svc.ListarCobrancasAsync(escritorio, new DateOnly(2026, 1, 1), false, adm, ct, ate: new DateOnly(2026, 12, 1));
        Assert.Equal(6, ano.Count);
        Assert.Equal(new DateOnly(2026, 4, 30), ano.Single(c => c.Competencia == new DateOnly(2026, 4, 1)).Vencimento);
        Assert.Equal(["1/3", "2/3", "3/3"], ano.Where(c => c.Parcela is not null).OrderBy(c => c.Competencia).Select(c => c.ParcelaTexto));
        var avulsa = ano.First(c => c.Tipo == TipoCobranca.Avulsa);
        Assert.True((await svc.ReceberAsync(escritorio, avulsa, new DateOnly(2026, 3, 21), 310m, adm, ct, "Pix com juros")).Sucesso);
        avulsa = (await svc.ListarCobrancasAsync(escritorio, new DateOnly(2026, 3, 1), false, adm, ct)).Single(c => c.Id == avulsa.Id);
        Assert.Equal((SituacaoCobranca.Recebida, 310m, "Pix com juros"), (avulsa.Situacao, avulsa.ValorRecebido, avulsa.ObservacaoBaixa));
        Assert.False((await svc.ExcluirCobrancaAsync(escritorio, avulsa, adm, ct)).Sucesso);     // recebida: estorne antes
        Assert.True((await svc.AlterarSituacaoAsync(escritorio, avulsa, SituacaoCobranca.Aberta, adm, ct)).Sucesso);
        Assert.True((await svc.ExcluirCobrancaAsync(escritorio, avulsa with { Situacao = SituacaoCobranca.Aberta }, adm, ct)).Sucesso);
        Assert.Equal(5, (await svc.ListarCobrancasAsync(escritorio, new DateOnly(2026, 1, 1), false, adm, ct, ate: new DateOnly(2026, 12, 1))).Count);
        // alterar a cobrança em aberto (não pode ir para uma competência que já tem honorário)
        var abril = (await svc.ListarCobrancasAsync(escritorio, new DateOnly(2026, 4, 1), false, adm, ct)).Single(c => c.Tipo == TipoCobranca.Honorario);
        Assert.Contains("05/2026", Assert.Single((await svc.AlterarCobrancaAsync(escritorio, abril with { Competencia = new DateOnly(2026, 5, 1) }, adm, ct)).Erros));
        Assert.True((await svc.AlterarCobrancaAsync(escritorio, abril with { Vencimento = new DateOnly(2026, 5, 5), Valor = 990m, Descricao = "Reajustado" }, adm, ct)).Sucesso);
        abril = (await svc.ListarCobrancasAsync(escritorio, new DateOnly(2026, 4, 1), false, adm, ct)).Single(c => c.Id == abril.Id);
        Assert.Equal((new DateOnly(2026, 5, 5), 990m, "Reajustado"), (abril.Vencimento, abril.Valor, abril.Descricao));
        // pagamento à vista: já recebido, vencendo na data do pagamento
        Assert.Equal(1, (await svc.LancarCobrancasAsync(escritorio, RegrasEscritorio.Parcelas(cli.Valor, new DateOnly(2026, 8, 1), 150m, 10, FrequenciaParcelas.Unica, 1,
                                                                                              TipoCobranca.Avulsa, "Certidão", pagoAVistaEm: new DateOnly(2026, 8, 3)), adm, ct)).Valor);
        var paga = (await svc.ListarCobrancasAsync(escritorio, new DateOnly(2026, 8, 1), false, adm, ct)).Single();
        Assert.Equal((SituacaoCobranca.Recebida, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 3), 150m, "Pagamento à vista"),
                     (paga.Situacao, paga.Vencimento, paga.RecebidoEm, (decimal)paga.ValorRecebido!, paga.ObservacaoBaixa));
        Assert.False((await svc.AlterarCobrancaAsync(escritorio, paga with { Valor = 1m }, adm, ct)).Sucesso);   // recebida: estorne antes
        // exclusão em lote: as recebidas ficam; relançar as mesmas competências depois de excluir não acusa honorário já lançado
        var lote = await svc.ListarCobrancasAsync(escritorio, new DateOnly(2026, 1, 1), false, adm, ct, ate: new DateOnly(2026, 12, 1));
        Assert.Equal(lote.Count(c => c.Situacao != SituacaoCobranca.Recebida), (await svc.ExcluirCobrancasAsync(escritorio, lote.Select(c => c.Id).ToList(), adm, ct)).Valor);
        Assert.All(await svc.ListarCobrancasAsync(escritorio, null, false, adm, ct, vencimentoDe: new DateOnly(2026, 5, 1), vencimentoAte: new DateOnly(2026, 5, 31)),
                   x => Assert.Equal(5, x.Vencimento.Month));                                     // pesquisa pelo vencimento
        var sobra = await svc.ListarCobrancasAsync(escritorio, new DateOnly(2026, 1, 1), false, adm, ct, ate: new DateOnly(2026, 12, 1));
        Assert.All(sobra, c => Assert.Equal(SituacaoCobranca.Recebida, c.Situacao));
        Assert.Contains(sobra, c => c.Id == paga.Id);
        Assert.Equal(3, (await svc.LancarCobrancasAsync(escritorio, RegrasEscritorio.Parcelas(cli.Valor, new DateOnly(2026, 4, 1), 950m, 31, FrequenciaParcelas.Mensal, 3), adm, ct)).Valor);
        // honorário anual no contrato do cliente e retirada do contrato
        Assert.True((await svc.GravarHonorarioAsync(escritorio, new ContratoHonorario(0, cli.Valor, 9000m, 15, new DateOnly(2026, 1, 1), null, "NENHUM", null, null, PeriodicidadeHonorario.Anual), adm, ct)).Sucesso);
        Assert.Equal(PeriodicidadeHonorario.Anual, Assert.Single(await svc.ListarHonorariosAsync(escritorio, adm, ct)).Periodicidade);
        Assert.Equal(0, (await svc.GerarCobrancasAsync(escritorio, new DateOnly(2026, 8, 1), adm, ct)).Valor);
        Assert.True((await svc.ExcluirHonorarioAsync(escritorio, cli.Valor, adm, ct)).Sucesso);
        Assert.Empty(await svc.ListarHonorariosAsync(escritorio, adm, ct));

        Assert.Equal(1, (await svc.IncluirProtocoloAsync(new Protocolo(0, escritorio, cli.Valor, 0, new DateOnly(2026, 3, 10), TipoProtocolo.Entrega, "Guia DAS", null, "Maria"), adm, ct)).Valor);
        Assert.Equal("Padaria Boa", Assert.Single(await svc.ListarProtocolosAsync(escritorio, adm, ct)).ClienteNome);

        // contratos: numerados, cliente precisa estar marcado como cliente
        var pessoas = await svc.ListarPessoasAsync(escritorio, adm, ct);
        var esc = await svc.DadosEscritorioAsync(escritorio, ct);
        var novo = new ContratoEscritorio(0, 0, cli.Valor, ModeloContrato.Cliente, "Serviços contábeis", 900m, Periodicidade.Mensal, 10, new DateOnly(2026, 1, 1), null, "IPCA", null, "-");
        novo = novo with { Texto = GeradorContratos.Gerar(esc, pessoas.Single(p => p.Id == cli.Valor), novo) };
        Assert.Equal(1, (await svc.GravarContratoAsync(escritorio, novo, adm, ct)).Valor);
        Assert.Contains("não está marcado como cliente", (await svc.GravarContratoAsync(escritorio, novo with { PessoaId = forn }, adm, ct)).Erros[0]);
        Assert.Equal(2, (await svc.GravarContratoAsync(escritorio, novo with { PessoaId = forn, Modelo = ModeloContrato.Fornecedor }, adm, ct)).Valor);
        var contratos = await svc.ListarContratosAsync(escritorio, adm, ct);
        Assert.Equal([2, 1], contratos.Select(c => c.Numero));

        // parcelas do contrato: gerar (uma vez), estender e renovação automática ao vencer a última
        var c1 = contratos.Single(c => c.Numero == 1);
        Assert.Equal((12, false), (c1.PrazoMeses, c1.RenovacaoAutomatica));
        c1 = c1 with { Inicio = new DateOnly(2030, 1, 1), Fim = new DateOnly(2030, 12, 31), RenovacaoAutomatica = true };
        Assert.Equal(1, (await svc.GravarContratoAsync(escritorio, c1, adm, ct)).Valor);
        Assert.Contains("renovando-se automaticamente", GeradorContratos.Gerar(esc, pessoas.Single(p => p.Id == cli.Valor), c1));
        Assert.Equal(12, (await svc.GerarParcelasContratoAsync(escritorio, c1.Id, false, adm, ct)).Valor);
        Assert.Contains("Estender parcelas", Assert.Single((await svc.GerarParcelasContratoAsync(escritorio, c1.Id, false, adm, ct)).Erros));
        Assert.Equal(12, (await svc.EstenderParcelasContratoAsync(escritorio, c1.Id, adm, ct)).Valor);
        var ps = await svc.ParcelasDoContratoAsync(escritorio, c1.Id, adm, ct);
        Assert.Equal(24, ps.Count);
        Assert.Equal((new DateOnly(2031, 12, 1), new DateOnly(2031, 12, 10)), (ps[^1].Competencia, ps[^1].Vencimento));
        Assert.Equal(new DateOnly(2031, 12, 31), (await svc.ListarContratosAsync(escritorio, adm, ct)).Single(c => c.Id == c1.Id).Fim);
        Assert.Equal(0, (await svc.RenovarContratosAsync(escritorio, new DateOnly(2031, 12, 9), adm, ct)).Valor);   // a última ainda não venceu
        Assert.Equal(1, (await svc.RenovarContratosAsync(escritorio, new DateOnly(2031, 12, 10), adm, ct)).Valor);
        Assert.Equal(36, (await svc.ParcelasDoContratoAsync(escritorio, c1.Id, adm, ct)).Count);
        Assert.Equal(0, (await svc.RenovarContratosAsync(escritorio, new DateOnly(2032, 6, 1), adm, ct)).Valor);
        Assert.All(await svc.ListarCobrancasAsync(escritorio, null, false, adm, ct, vencimentoDe: new DateOnly(2030, 1, 1), pessoa: cli.Valor),
                   c => Assert.Equal((c1.Id, "Contrato nº 1"), (c.ContratoId!.Value, c.Descricao)));
        Assert.Empty(await svc.ListarCobrancasAsync(escritorio, null, false, adm, ct, vencimentoDe: new DateOnly(2030, 1, 1), pessoa: forn));   // não mistura clientes

        // cliente com movimento não é excluído
        Assert.Contains("desmarque Ativo", (await svc.ExcluirPessoaAsync(escritorio, cli.Valor, adm, ct)).Erros[0]);

        // usuário sem perfil no escritório não grava nem vê nada
        var outro = await db.EscalarAsync<long>("INSERT INTO core.usuario (login, nome, senha_hash) VALUES ('sem_acesso_esc', 'Sem acesso', 'x') RETURNING id");
        Assert.False((await svc.GravarHonorarioAsync(escritorio, new ContratoHonorario(0, cli.Valor, 1m, 1, new DateOnly(2026, 1, 1), null, "NENHUM", null), outro, ct)).Sucesso);
        Assert.Empty(await svc.ListarTarefasAsync(escritorio, null, false, outro, ct));
        Assert.Empty(await svc.ListarPessoasAsync(escritorio, outro, ct));
    }
}

public class TarefasDeObrigacoesTests
{
    [Fact]
    public void Gera_titulo_e_vencimento_das_nao_cumpridas()
    {
        var def = new Vorticis.Domain.Obrigacoes.DefinicaoObrigacao("DCTFWEB", "DCTFWeb", "Receita Federal", default, default, default, 15, 1, 0, 0, default, default, false);
        Vorticis.Application.Obrigacoes.ItemObrigacao Item(int mes, Vorticis.Application.Obrigacoes.StatusObrigacao st)
            => new(new Vorticis.Domain.Obrigacoes.OcorrenciaObrigacao(def, new Vorticis.Domain.Obrigacoes.Competencia(2027, mes), new DateOnly(2027, mes + 1, 15)), st, null, null, 10);
        var t = RegrasEscritorio.TarefasDeObrigacoes([Item(3, Vorticis.Application.Obrigacoes.StatusObrigacao.Pendente),
                                                      Item(4, Vorticis.Application.Obrigacoes.StatusObrigacao.Cumprida)]).ToList();
        var x = Assert.Single(t);
        Assert.StartsWith("DCTFWeb – ", x.Titulo);
        Assert.Equal(new DateOnly(2027, 4, 15), x.Vencimento);
    }
}

public class ProdutividadeECertificadosTests
{
    [Fact]
    public void Produtividade_por_responsavel()
    {
        Tarefa T(string resp, int dia, SituacaoTarefa s, int? concluidaDia = null)
            => new(0, 1, null, "T", null, new DateOnly(2026, 3, dia), 1, 2, s, Recorrencia.Nenhuma, null, resp,
                   concluidaDia is { } c ? new DateTimeOffset(2026, 3, c, 12, 0, 0, TimeSpan.Zero) : null);
        var tarefas = new[]
        {
            T("Ana", 10, SituacaoTarefa.Concluida, 9), T("Ana", 10, SituacaoTarefa.Concluida, 12), T("Ana", 5, SituacaoTarefa.Pendente),
            T("Bia", 25, SituacaoTarefa.Pendente), T("Bia", 20, SituacaoTarefa.Cancelada), T("Bia", 2, SituacaoTarefa.Concluida, 1),
            new Tarefa(0, 1, null, "fora", null, new DateOnly(2026, 5, 1), 1, 2, SituacaoTarefa.Pendente, Recorrencia.Nenhuma, null, "Ana"),
        };
        var t = TabelasEscritorio.Produtividade(tarefas, new(2026, 3, 1), new(2026, 3, 31), new(2026, 3, 15));
        var ana = t.Linhas.Single(l => (string)l.Valores[0]! == "Ana").Valores;
        Assert.Equal([3L, 2L, 1L, 1L, 1L, 0L, 50m], ana.Skip(1));
        var bia = t.Linhas.Single(l => (string)l.Valores[0]! == "Bia").Valores;
        Assert.Equal([2L, 1L, 1L, 0L, 0L, 1L, 100m], bia.Skip(1));
        Assert.Equal(5L, t.Linhas[^1].Valores[1]);
    }

    [Fact]
    public void Certificados_ate_a_data()
    {
        var agora = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        Vorticis.Application.Sistema.CertificadoDigital C(int dias, long? emp) => new(dias, "A1", "Cert", "Titular", null, "AC", agora.AddYears(-1), agora.AddDays(dias), "x", emp);
        var t = TabelasEscritorio.CertificadosAVencer([C(-5, 7), C(20, null), C(200, 7)], new Dictionary<long, string> { [7] = "Padaria" }, new DateOnly(2026, 5, 1), agora);
        Assert.Equal(2, t.Linhas.Count);
        Assert.Equal(("Padaria", true), ((string)t.Linhas[0].Valores[0]!, t.Linhas[0].Destaque));
        Assert.Equal("(do escritório)", t.Linhas[1].Valores[0]);
    }
}
