using Vorticis.Application.Cadastros;
using Vorticis.Application.Contabil;
using Vorticis.Domain.Contabil;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Infra.Tests;

/// <summary>Plano novo com máscara própria, modelo convertido, conversão do plano da empresa e conta automática de clientes e fornecedores.</summary>
public class PlanoMascaraETitulosTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Criar_novo_plano_vazio_com_nome_e_mascara_valida_tudo_antes_de_gravar()
    {
        BancoDeTeste.ExigirBanco();
        var empresa = await db.CriarEmpresaAsync("19131243000197");
        var adm = await db.AdminAsync();
        var svc = db.App.PlanoContas;

        Assert.Contains("nome", (await svc.CriarNovoPlanoAsync(empresa, " ", "99.99.999", adm, Ct)).Erros[0]);
        Assert.Contains("pelo menos 2 níveis", (await svc.CriarNovoPlanoAsync(empresa, "Meu plano", "999", adm, Ct)).Erros[0]);
        Assert.False((await svc.CriarNovoPlanoAsync(empresa, "Meu plano", "", adm, Ct)).Sucesso);
        Assert.NotEmpty(await svc.ListarAsync(empresa, adm, Ct));             // nada foi trocado pelas recusas

        var r = await svc.CriarNovoPlanoAsync(empresa, "Plano da casa", "99.99.999", adm, Ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        var plano = await svc.ListarAsync(empresa, adm, Ct);
        Assert.Equal(["01", "02", "03", "04", "05"], plano.Select(c => c.Codigo));
        Assert.All(plano, c => { Assert.False(c.Analitica); Assert.Equal(1, c.Nivel); });
        var cfg = await svc.ObterConfiguracaoAsync(empresa, adm, Ct);
        Assert.Equal(("Plano da casa", "99.99.999", null), (cfg.NomePlano, cfg.MascaraPlano, cfg.ModeloPlano));

        // pronto para receber contas na máscara
        var sub = await svc.IncluirAsync(empresa, new NovaConta("01.01", "CIRCULANTE", "01", IndicadorDC.D, false, false, new DateOnly(2026, 1, 1)), adm, Ct);
        Assert.True(sub.Sucesso, string.Join("; ", sub.Erros));

        // quem só consulta não cria plano
        var consulta = await db.App.Usuarios.CriarUsuarioAsync(new SessaoUsuario(0, "t", "T", true, false), "so_consulta_plano", "Consulta", "Inicial123",
            await db.PerfilIdAsync("Consulta"), Ct, empresaIds: [empresa]);
        Assert.Contains("não permite", (await svc.CriarNovoPlanoAsync(empresa, "X", "99.99", consulta.Valor, Ct)).Erros[0]);
    }

    [Fact]
    public async Task Modelo_convertido_para_outra_mascara_vira_copia_propria_com_contas_de_encerramento_acompanhando()
    {
        BancoDeTeste.ExigirBanco();
        var empresa = await db.CriarEmpresaAsync("11222333000181");
        var adm = await db.AdminAsync();
        var svc = db.App.PlanoContas;

        var ruim = svc.PreverModelo("basico", "9.9.9.9");
        Assert.True(ruim.Sucesso);
        Assert.False(ruim.Valor.Possivel);                                    // 99/999 não cabem em 1 dígito
        var recusa = await svc.AplicarModeloAsync(empresa, "basico", adm, Ct, mascara: "9.9.9.9");
        Assert.False(recusa.Sucesso);
        Assert.Contains("não cabe", string.Join(" ", recusa.Erros));

        var previa = svc.PreverModelo("basico", "9.99.999.9999");
        Assert.True(previa.Valor.Possivel, string.Join("; ", previa.Valor.Erros));
        Assert.Equal("1.01.002.0001", previa.Valor.Itens.Single(i => i.Antigo == "1.1.02.001").Novo);

        var ok = await svc.AplicarModeloAsync(empresa, "basico", adm, Ct, mascara: "9.99.999.9999");
        Assert.True(ok.Sucesso, string.Join("; ", ok.Erros));
        var plano = await svc.ListarAsync(empresa, adm, Ct);
        Assert.Equal(ok.Valor, plano.Count);
        var clientes = plano.Single(c => c.Codigo == "1.01.002.0001");
        Assert.True(clientes.Analitica);
        Assert.Equal("1.01.002", plano.Single(c => c.Id == clientes.ContaPaiId).Codigo);
        var cfg = await svc.ObterConfiguracaoAsync(empresa, adm, Ct);
        Assert.Null(cfg.ModeloPlano);
        Assert.Equal("9.99.999.9999", cfg.MascaraPlano);
        Assert.Contains("máscara 9.99.999.9999", cfg.NomePlano);
        Assert.Equal("5.01.001.0001", cfg.ContaApuracao);                     // 5.1.01.001 do modelo
        Assert.Contains(cfg.ContaLucros!, plano.Select(c => c.Codigo));
        Assert.Contains(cfg.ContaPrejuizos!, plano.Select(c => c.Codigo));
    }

    [Fact]
    public async Task Conversao_do_plano_da_empresa_mantem_lancamentos_vinculos_e_configuracoes_e_deixa_rastro_na_auditoria()
    {
        BancoDeTeste.ExigirBanco();
        var empresa = await db.CriarEmpresaAsync("42938862000100");
        var adm = await db.AdminAsync();
        var svc = db.App.PlanoContas;
        var antes = await svc.ListarAsync(empresa, adm, Ct);
        var caixa = antes.Single(c => c.Codigo == "1.1.01.001");
        var l = await db.App.Lancamentos.EfetivarAsync(new LancamentoProposto(empresa, new(2026, 2, 1), OrigemLancamento.Manual,
            [new("1.1.01.001", IndicadorDC.D, 80m, "x"), new("4.1.01.001", IndicadorDC.C, 80m, "x")]), adm, Ct);
        Assert.True(l.Sucesso, string.Join("; ", l.Erros));
        await db.EscalarAsync<long>("INSERT INTO fiscal.configuracao (empresa_id, conta_clientes, conta_fornecedores) VALUES (@e, '1.1.02.001', '2.1.01.001') RETURNING empresa_id", ("e", empresa));

        // prévia completa e sem gravar
        var m = MascaraPlano.Ler("9.99.999.9999").Valor;
        var previa = await svc.PreverMascaraAsync(empresa, m, adm, Ct);
        Assert.True(previa.Valor.Possivel, string.Join("; ", previa.Valor.Erros));
        Assert.Equal(antes.Count, previa.Valor.Itens.Count);
        Assert.Equal(caixa.Codigo, (await svc.ListarAsync(empresa, adm, Ct)).Single(c => c.Id == caixa.Id).Codigo);

        // máscara impossível: nada muda, nem as configurações
        Assert.False((await svc.AplicarMascaraAsync(empresa, MascaraPlano.Ler("9.9.9.9").Valor, adm, Ct)).Sucesso);
        var cfgRecusa = (await svc.ObterConfiguracaoAsync(empresa, adm, Ct)) with { MascaraPlano = "9.9.9.9" };
        var recusa = await svc.SalvarConfiguracaoAsync(empresa, cfgRecusa, null, adm, Ct, converterPlano: true);
        Assert.False(recusa.Sucesso);
        Assert.Contains("nada foi alterado", recusa.Erros[0]);
        Assert.Equal(caixa.Codigo, (await svc.ListarAsync(empresa, adm, Ct)).Single(c => c.Id == caixa.Id).Codigo);
        Assert.NotEqual("9.9.9.9", (await svc.ObterConfiguracaoAsync(empresa, adm, Ct)).MascaraPlano);

        // troca da máscara na configuração com conversão do plano, numa só gravação
        var cfg = (await svc.ObterConfiguracaoAsync(empresa, adm, Ct)) with { MascaraPlano = "9.99.999.9999" };
        var salvo = await svc.SalvarConfiguracaoAsync(empresa, cfg, null, adm, Ct, converterPlano: true);
        Assert.True(salvo.Sucesso, string.Join("; ", salvo.Erros));
        var depois = await svc.ListarAsync(empresa, adm, Ct);
        var caixaDepois = depois.Single(c => c.Id == caixa.Id);
        Assert.Equal(("1.01.001.0001", 80m, true), (caixaDepois.Codigo, caixaDepois.Saldo, caixaDepois.Analitica));
        Assert.Equal(caixa.ContaPaiId, caixaDepois.ContaPaiId);
        Assert.Equal(antes.Select(c => (c.Id, c.Analitica, c.CodNat, c.NaturezaSaldo, c.Nivel)),
                     depois.Select(c => (c.Id, c.Analitica, c.CodNat, c.NaturezaSaldo, c.Nivel)));
        var novaCfg = await svc.ObterConfiguracaoAsync(empresa, adm, Ct);
        Assert.Equal(("9.99.999.9999", "5.01.001.0001"), (novaCfg.MascaraPlano, novaCfg.ContaApuracao));
        Assert.Equal("1.01.002.0001", await db.EscalarAsync<string>("SELECT conta_clientes FROM fiscal.configuracao WHERE empresa_id = @e", ("e", empresa)));
        Assert.Equal("2.01.001.0001", await db.EscalarAsync<string>("SELECT conta_fornecedores FROM fiscal.configuracao WHERE empresa_id = @e", ("e", empresa)));
        Assert.True(await db.EscalarAsync<long>("SELECT count(*) FROM core.auditoria WHERE tabela = 'contabil.plano_contas' AND operacao = 'UPDATE' AND empresa_id = @e",
            ("e", empresa)) > 0);

        // o lançamento continua usando a mesma conta (agora com o código novo)
        var razao = await db.App.Lancamentos.EfetivarAsync(new LancamentoProposto(empresa, new(2026, 2, 2), OrigemLancamento.Manual,
            [new("1.01.001.0001", IndicadorDC.D, 20m, "novo"), new("4.01.001.0001", IndicadorDC.C, 20m, "novo")]), adm, Ct);
        Assert.True(razao.Sucesso, string.Join("; ", razao.Erros));
        Assert.Equal(100m, (await svc.ListarAsync(empresa, adm, Ct)).Single(c => c.Id == caixa.Id).Saldo);

        // converter de novo para a mesma máscara não muda nada
        Assert.Equal(0, (await svc.AplicarMascaraAsync(empresa, MascaraPlano.Ler("9.99.999.9999").Valor, adm, Ct)).Valor);
    }

    [Fact]
    public async Task Conta_do_cliente_e_do_fornecedor_nasce_sob_a_conta_titulo_sem_duplicar_e_respeita_permissoes()
    {
        BancoDeTeste.ExigirBanco();
        var empresa = await db.CriarEmpresaAsync("45723174000110");
        var adm = await db.AdminAsync();
        var plano = db.App.PlanoContas;
        var parts = db.App.Participantes;
        Participante Novo(string nome, string? doc, bool cli, bool forn) => new(0, "", nome, null, doc, null, "SP", null, null, null, null, cli, forn, null, true);

        // sem conta título escolhida: avisa e não grava
        var semTitulo = await parts.GravarCriandoContaAsync(empresa, Novo("Sem Titulo", null, true, false), true, adm, Ct);
        Assert.False(semTitulo.Sucesso);
        Assert.Contains("conta título de Clientes", semTitulo.Erros[0]);
        Assert.Empty(await parts.ListarAsync(empresa, adm, Ct));

        // a conta título precisa ser sintética do último nível
        var contas = await plano.ListarAsync(empresa, adm, Ct);
        var cfg = await plano.ObterConfiguracaoAsync(empresa, adm, Ct);
        var analitica = contas.Single(c => c.Codigo == "1.1.02.001");
        var raiz = contas.Single(c => c.Codigo == "1");
        Assert.Contains("analítica", string.Join(" ", (await plano.SalvarConfiguracaoAsync(empresa, cfg with { ContaTituloClientesId = analitica.Id }, null, adm, Ct)).Erros));
        Assert.Contains("último nível", string.Join(" ", (await plano.SalvarConfiguracaoAsync(empresa, cfg with { ContaTituloClientesId = raiz.Id }, null, adm, Ct)).Erros));
        var tCli = contas.Single(c => c.Codigo == "1.1.02");
        var tFor = contas.Single(c => c.Codigo == "2.1.01");
        var gravou = await plano.SalvarConfiguracaoAsync(empresa, cfg with { ContaTituloClientesId = tCli.Id, ContaTituloFornecedoresId = tFor.Id }, null, adm, Ct);
        Assert.True(gravou.Sucesso, string.Join("; ", gravou.Erros));

        // cliente: conta analítica com o nome, próximo código da máscara, ligada ao cadastro
        var a = await parts.GravarCriandoContaAsync(empresa, Novo("Cliente Alfa", "11222333000181", true, false), true, adm, Ct);
        Assert.True(a.Sucesso, string.Join("; ", a.Erros));
        Assert.Equal("1.1.02.005", a.Valor.ContaCriada);                     // o modelo já usa 001 a 004
        var conta = (await plano.ListarAsync(empresa, adm, Ct)).Single(c => c.Codigo == "1.1.02.005");
        Assert.Equal(("Cliente Alfa", true, tCli.Id, "01", IndicadorDC.D), (conta.Descricao, conta.Analitica, conta.ContaPaiId, conta.CodNat, conta.NaturezaSaldo));
        Assert.Equal("1.1.02.005", (await parts.ListarAsync(empresa, adm, Ct)).Single(p => p.Id == a.Valor.Id).CodigoConta);

        // fornecedor: sob o título de fornecedores; já ligado: salvar de novo não cria outra
        var f = await parts.GravarCriandoContaAsync(empresa, Novo("Fornecedor Beta", null, false, true), true, adm, Ct);
        Assert.Equal("2.1.01.002", f.Valor.ContaCriada);
        var ligado = (await parts.ListarAsync(empresa, adm, Ct)).Single(p => p.Id == a.Valor.Id);
        var denovo = await parts.GravarCriandoContaAsync(empresa, ligado with { Nome = "Cliente Alfa SA" }, true, adm, Ct);
        Assert.True(denovo.Sucesso);
        Assert.Null(denovo.Valor.ContaCriada);
        Assert.Single(await plano.ListarAsync(empresa, adm, Ct), c => c.Descricao.StartsWith("Cliente Alfa"));

        // sem a opção marcada, nada é criado
        var sem = await parts.GravarCriandoContaAsync(empresa, Novo("Cliente Gama", null, true, false), false, adm, Ct);
        Assert.Null(sem.Valor.ContaCriada);
        Assert.DoesNotContain(await plano.ListarAsync(empresa, adm, Ct), c => c.Descricao == "Cliente Gama");

        // permissão no serviço: quem só lança (sem manter o plano nem contabilizar) grava o cadastro, mas não cria contas no plano
        var perfil = await db.EscalarAsync<long>("INSERT INTO core.perfil (nome, descricao, padrao) VALUES ('So lanca', 'teste', FALSE) RETURNING id");
        await db.EscalarAsync<long>("INSERT INTO core.perfil_permissao (perfil_id, permissao) VALUES (@p, 'LANCAMENTO_INCLUIR') RETURNING perfil_id", ("p", perfil));
        var lanca = await db.App.Usuarios.CriarUsuarioAsync(new SessaoUsuario(0, "t", "T", true, false), "so_lanca_conta", "So lanca", "Inicial123",
            perfil, Ct, empresaIds: [empresa]);
        Assert.True(lanca.Sucesso, string.Join("; ", lanca.Erros));
        var negado = await parts.GravarCriandoContaAsync(empresa, Novo("Cliente Delta", null, true, false), true, lanca.Valor, Ct);
        Assert.False(negado.Sucesso);
        Assert.Contains("não permite criar contas", negado.Erros[0]);
        Assert.DoesNotContain(await parts.ListarAsync(empresa, adm, Ct), p => p.Nome == "Cliente Delta");
        Assert.True((await parts.GravarCriandoContaAsync(empresa, Novo("Cliente Delta", null, true, false), false, lanca.Valor, Ct)).Sucesso);

        // no módulo Fiscal (perfil com contabilização fiscal, como o Assistente) a opção funciona
        var assist = await db.App.Usuarios.CriarUsuarioAsync(new SessaoUsuario(0, "t", "T", true, false), "assistente_conta", "Assistente", "Inicial123",
            await db.PerfilIdAsync("Assistente"), Ct, empresaIds: [empresa]);
        var fiscal = await parts.GravarCriandoContaAsync(empresa, Novo("Cliente Épsilon", null, true, false), true, assist.Valor, Ct);
        Assert.True(fiscal.Sucesso, string.Join("; ", fiscal.Erros));
        Assert.Equal("1.1.02.006", fiscal.Valor.ContaCriada);
    }
}
