using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Cadastros;
using Vorticis.Application.Contabil;
using Vorticis.Application.Sistema;
using Vorticis.Desktop.Comum.Modulos;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Desktop.Comum.Views;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Desktop.Tests;

/// <summary>
/// Cadastros › Menus do programa, preferências de menu (ocultar, bloquear,
/// itens essenciais) e a tela de Contabilização em lote.
/// </summary>
public class ParametrosMenusLoteTests(SessaoHeadless headless, Vorticis.Infra.Tests.BancoDeTeste db) : IClassFixture<Vorticis.Infra.Tests.BancoDeTeste>
{
    private static IEnumerable<ItemMenu> Folhas(IEnumerable<ItemMenu> itens) => itens.SelectMany(i => i.Filhos is null ? [i] : Folhas(i.Filhos));
    private static IEnumerable<ItemMenu> Todos(IEnumerable<ItemMenu> itens) => itens.SelectMany(i => (i.Filhos is null ? [] : Todos(i.Filhos)).Prepend(i));
    private static string Sem(ItemMenu m) => m.Titulo.Replace("_", "");

    private static async Task<T> ForaDaSessao<T>(Task<T> tarefa)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = tarefa.ContinueWith(t => { if (t.IsFaulted) tcs.SetException(t.Exception!.InnerExceptions); else tcs.SetResult(t.Result); }, TaskScheduler.Default);
        return await tcs.Task;
    }

    private static async Task Esperar(CancellationToken ct, int ciclos = 25)
    {
        for (var i = 0; i < ciclos; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
    }

    [Fact]
    public void No_Escritorio_Menus_do_programa_fica_dentro_de_Parametros()
    {
        var vm = new Vorticis.Escritorio.Desktop.MainViewModel();
        var cadastros = vm.Menus[0].Filhos!;
        Assert.DoesNotContain(cadastros, i => i.Chave == TelasComuns.Menus || i.Chave == TelasComuns.Parametros);
        var parametros = Assert.Single(cadastros, i => i.Titulo == "Parâmetros" && i.Filhos is not null);
        Assert.Equal([("Parâmetros do Escritório", TelasComuns.Parametros), ("Menus do programa", TelasComuns.Menus)],
                     parametros.Filhos!.Select(i => (i.Titulo, i.Chave!)));
        Assert.Equal("Cadastros › Parâmetros › Menus do programa", vm.CaminhoMenusDoPrograma);
        Assert.Contains(vm.ArvoreDeMenus().Single(n => n.Titulo == "Cadastros").Filhos, n => n.Titulo == "Parâmetros" && n.Essencial);
        var tela = new Vorticis.Escritorio.Desktop.ViewModels.ParametrosViewModelEscritorio(vm);
        Assert.Contains(tela.Grupos, g => g.Titulo == "Menus do programa");
    }

    [Fact]
    public void Cadastros_tem_Parametros_e_Menus_do_programa_e_a_contabilizacao_em_lote_so_com_a_empresa_em_lote()
    {
        var vm = new Vorticis.Desktop.ViewModels.MainViewModel();
        var cadastros = vm.Menus[0].Filhos!;
        Assert.Single(cadastros, i => i.Titulo == "Parâmetros" && i.Chave == TelasComuns.Parametros);
        Assert.Single(cadastros, i => i.Titulo == "Menus do programa" && i.Chave == TelasComuns.Menus);
        Assert.True(MainViewModelBase.ChaveEssencial(TelasComuns.Menus));
        var item = Folhas(vm.Menus).Single(i => i.Chave == TelasComuns.ContabilizacaoLote);
        Assert.Equal(MainViewModelBase.RecursoContabilizacaoLote, item.Recurso);
        Assert.True(vm.ConheceTela(TelasComuns.ContabilizacaoLote));
    }

    [Fact]
    public void Itens_ocultos_somem_do_menu_bloqueiam_a_tela_e_os_essenciais_nao_saem()
    {
        var vm = new Vorticis.Desktop.ViewModels.MainViewModel();
        var tudo = vm.ArvoreDeMenus();
        Assert.Equal(vm.Menus.Count, tudo.Count);
        var relatorios = tudo.Single(n => n.Titulo == "Relatórios");
        Assert.False(relatorios.Essencial);
        Assert.True(tudo.Single(n => n.Titulo == "Ajuda").Essencial);
        Assert.True(tudo.Single(n => n.Titulo == "Cadastros").Essencial);               // contém o Parâmetros
        Assert.True(tudo.Single(n => n.Titulo == "Configurações").Essencial);           // contém o Sair
        Assert.True(tudo.Single(n => n.Titulo == "Cadastros").Filhos.Single(f => f.Titulo == "Parâmetros").Essencial);
        var chavesRelatorios = Todos(vm.Menus.Single(m => Sem(m) == "Relatórios").Filhos!).Where(i => i.Chave is not null).Select(i => i.Chave!).ToList();
        Assert.NotEmpty(chavesRelatorios);

        // oculta Relatórios inteiro + tenta ocultar itens essenciais (ignorados) + um item solto de Cadastros
        var parametros = "Cadastros › Parâmetros";
        vm.AplicarPreferenciasMenu(new PreferenciasMenu(new HashSet<string> { "Relatórios", parametros, "Ajuda", "Configurações › Sair (trocar de usuário)" }, new HashSet<string>()));
        Assert.DoesNotContain(vm.MenusVisiveis, m => Sem(m) == "Relatórios");
        Assert.Equal(vm.Menus.Count - 1, vm.MenusVisiveis.Count);
        Assert.Contains(vm.MenusVisiveis, m => Sem(m) == "Ajuda");
        Assert.Contains(Folhas(vm.MenusVisiveis), i => i.Chave == TelasComuns.Parametros);
        Assert.Contains(Folhas(vm.MenusVisiveis), i => i.Chave == TelasComuns.Sair);
        // telas só alcançáveis pelo menu escondido ficam bloqueadas (atalho e chamada direta usam a mesma regra); as que têm outro acesso, não
        var visiveis = Folhas(vm.MenusVisiveis).Where(i => i.Chave is not null).Select(i => i.Chave!).ToHashSet();
        var bloqueadas = chavesRelatorios.Where(c => !visiveis.Contains(c)).ToList();
        Assert.NotEmpty(bloqueadas);
        Assert.All(bloqueadas, c => Assert.True(vm.TelaBloqueadaPorMenu(c), c));
        Assert.All(chavesRelatorios.Where(visiveis.Contains), c => Assert.False(vm.TelaBloqueadaPorMenu(c), c));
        Assert.False(vm.TelaBloqueadaPorMenu(TelasComuns.Parametros));
        Assert.False(vm.TelaBloqueadaPorMenu(TelasComuns.Sair));
        Assert.False(vm.TelaBloqueadaPorMenu(TelasComuns.Manual));

        // submenu que ficou sem nenhum item visível some; voltar tudo restaura o menu
        var operacao = vm.Menus.Single(m => Sem(m) == "Lançamentos");
        vm.AplicarPreferenciasMenu(new PreferenciasMenu(operacao.Filhos!.Where(f => !f.EhSeparador).Select(f => "Lançamentos › " + Sem(f)).ToHashSet(), new HashSet<string>()));
        Assert.DoesNotContain(vm.MenusVisiveis, m => Sem(m) == "Lançamentos");
        vm.AplicarPreferenciasMenu(PreferenciasMenu.Vazia);
        Assert.Same(vm.Menus, vm.MenusVisiveis);
        Assert.False(vm.TelaBloqueadaPorMenu(bloqueadas[0]));
    }

    [Fact]
    public async Task Os_parametros_gravam_o_modo_em_lote_e_a_tela_Menus_oculta_e_bloqueia_com_aviso()
    {
        Vorticis.Infra.Tests.BancoDeTeste.ExigirBanco();
        var adm = await db.AdminAsync();
        await db.App.Perfis.ConcluirConfiguracaoInicialAsync(new SessaoUsuario(adm, "admin_teste", "Admin", true, false), TestContext.Current.CancellationToken);
        var empresa = await db.CriarEmpresaAsync("27183946000359");
        var outro = await db.EscalarAsync<long>("INSERT INTO core.usuario (login, nome, senha_hash, administrador) VALUES ('outro_menu', 'Outro', 'x', FALSE) RETURNING id");
        var ct = TestContext.Current.CancellationToken;
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            var vm = new Vorticis.Desktop.ViewModels.MainViewModel();
            await vm.InicializarAsync(db.ConnectionString);
            var login = Assert.IsType<LoginViewModel>(vm.Conteudo);
            (login.Usuario, login.Senha) = ("admin_teste", "Senha1234");
            await login.EntrarCommand.ExecuteAsync(null);
            for (var i = 0; i < 200 && !vm.Logado; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
            Assert.True(vm.Logado);
            vm.EmpresaSelecionada = vm.EmpresasPermitidas.Single(e => e.Id == empresa);
            await Esperar(ct);

            try
            {
                // ---- Parâmetros do Contábil: tipo de contabilização em lote
                vm.AbrirCommand.Execute(TelasComuns.Parametros);
                await Esperar(ct);
                var contabil = Assert.IsAssignableFrom<ParametrosViewModel>(vm.Conteudo);
                static CampoParametro Campo(ParametrosViewModel p, string chave) => p.Grupos.SelectMany(g => g.Campos).Single(c => c.Chave == chave);
                Assert.True(contabil.PodeGravar);
                Assert.Equal("ONLINE", Campo(contabil, "tipoContabilizacao").ValorEscolha);   // padrão: on-line
                Campo(contabil, "tipoContabilizacao").DefinirEscolha("LOTE");
                await ((IAsyncRelayCommand)contabil.SalvarCommand).ExecuteAsync(null);
                Assert.Empty(contabil.Erros);
                Assert.Equal(ModoContabilizacao.Lote, (await db.App.Pendencias.ModoAsync(empresa, adm, ct)).Valor);
                Assert.Contains(MainViewModelBase.RecursoContabilizacaoLote, vm.RecursosAtivos);   // o item Exportar › Contabilização em lote aparece
                Assert.True(vm.AbrirCommand.CanExecute(TelasComuns.ContabilizacaoLote));

                // ---- aba Menus: árvore, essenciais e gravação por usuário
                vm.AbrirCommand.Execute(TelasComuns.Menus);
                await Esperar(ct);
                var menus = Assert.IsType<PreferenciasMenuViewModel>(vm.Conteudo);
                Assert.NotEmpty(menus.Raiz);
                var ajuda = menus.Raiz.Single(n => n.Titulo == "Ajuda");
                Assert.True(ajuda.Essencial && !ajuda.Editavel && ajuda.Marcado);
                var relatorios = menus.Raiz.Single(n => n.Titulo == "Relatórios");
                relatorios.Marcado = false;                                          // desmarcar o menu leva os itens junto
                Assert.All(relatorios.Todos(), n => Assert.False(n.Marcado));
                ajuda.Marcado = false;                                               // essencial: volta/continua marcado ao gravar
                Assert.True(menus.Alterado && menus.PodeParaTodos);
                await ((IAsyncRelayCommand)menus.SalvarCommand).ExecuteAsync(null);
                Assert.Empty(menus.Erros);
                Assert.DoesNotContain(vm.MenusVisiveis, m => m.Titulo.Replace("_", "") == "Relatórios");
                Assert.Contains(vm.MenusVisiveis, m => m.Titulo.Replace("_", "") == "Ajuda");
                var prefs = await db.App.PreferenciasMenu.ObterAsync(vm.Modulo.NomeExecutavel, adm, ct);
                Assert.Contains("Relatórios", prefs.DoUsuario);
                Assert.DoesNotContain("Ajuda", prefs.DoUsuario);
                Assert.Empty((await db.App.PreferenciasMenu.ObterAsync(vm.Modulo.NomeExecutavel, outro, ct)).Ocultos);   // cada usuário tem as suas

                // tela escondida: botão/atalho/chamada direta ficam bloqueados com mensagem
                var chaveEscondida = vm.Menus.Single(m => m.Titulo.Replace("_", "") == "Relatórios").Filhos!
                    .SelectMany(f => f.Filhos ?? [f]).First(i => i.Chave is not null && vm.TelaBloqueadaPorMenu(i.Chave)).Chave!;
                Assert.False(vm.AbrirCommand.CanExecute(chaveEscondida));
                vm.Status = "";
                vm.AbrirPorAtalho(chaveEscondida);
                Assert.Contains("oculta", vm.Status);
                Assert.Contains("Cadastros › Menus do programa", vm.Status);
                vm.Status = "";
                vm.AbrirTela(chaveEscondida);
                Assert.Contains("oculta", vm.Status);
                Assert.NotEqual(chaveEscondida, vm.ViewModelDaTela(chaveEscondida)?.GetType().Name);

                // a preferência acompanha o usuário no próximo login; Sair limpa o menu para o usuário seguinte
                vm.SairCommand.Execute(null);
                Assert.Same(vm.Menus, vm.MenusVisiveis);
                (login.Usuario, login.Senha) = ("admin_teste", "Senha1234");
                await login.EntrarCommand.ExecuteAsync(null);
                for (var i = 0; i < 200 && !vm.Logado; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
                Assert.DoesNotContain(vm.MenusVisiveis, m => m.Titulo.Replace("_", "") == "Relatórios");

                // administrador aplica para todos; quem não é administrador não consegue
                var essenciais = new HashSet<string>();
                var negado = await db.App.PreferenciasMenu.GravarAsync(new SessaoUsuario(outro, "outro_menu", "Outro", false, false), vm.Modulo.NomeExecutavel, ["Emissão"], essenciais, true);
                Assert.Contains("administradores", negado.Erros[0]);
                Assert.True((await vm.SalvarPreferenciasMenuAsync(["Emissão"], true)).Sucesso);
                var deTodos = await db.App.PreferenciasMenu.ObterAsync(vm.Modulo.NomeExecutavel, outro, ct);
                Assert.Contains("Emissão", deTodos.DeTodos);
                Assert.Contains("Emissão", deTodos.Ocultos);
                Assert.Contains("Relatórios", vm.PreferenciasDeMenu.DoUsuario);       // a escolha pessoal do administrador continua
                Assert.DoesNotContain(vm.MenusVisiveis, m => m.Titulo.Replace("_", "") == "Emissão");
                return true;
            }
            finally
            {
                await db.EscalarAsync<long>("WITH d AS (DELETE FROM core.preferencia_menu RETURNING 1) SELECT count(*) FROM d");
            }
        }, ct));
        Assert.True(ok);
    }

    [Fact]
    public async Task Telas_novas_renderizam()
    {
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(() =>
        {
            var vm = new Vorticis.Desktop.ViewModels.MainViewModel();
            var menus = Assert.IsType<PreferenciasMenuViewModel>(vm.ViewModelDaTela(TelasComuns.Menus));
            menus.Carregar();
            var janela = new Window { Width = 1100, Height = 800 };
            foreach (object t in new object[] { menus, vm.ContabilizacaoLote })
            {
                var view = Avalonia.Application.Current!.DataTemplates.First(d => d.Match(t)).Build(t)!;
                view.DataContext = t;
                janela.Content = view;
                if (!janela.IsVisible) janela.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.NotNull(janela.CaptureRenderedFrame());
            }
            return Task.FromResult(true);
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }

    [Fact]
    public async Task Tela_de_lote_lista_por_origem_contabiliza_e_desfaz_pelo_ViewModel()
    {
        Vorticis.Infra.Tests.BancoDeTeste.ExigirBanco();
        var adm = await db.AdminAsync();
        await db.App.Perfis.ConcluirConfiguracaoInicialAsync(new SessaoUsuario(adm, "admin_teste", "Admin", true, false), TestContext.Current.CancellationToken);
        var empresa = await db.CriarEmpresaAsync("33000167000101");
        var ct = TestContext.Current.CancellationToken;
        // pendência de folha sem regra: a tela mostra, tenta contabilizar e informa o motivo sem perder a pendência
        Assert.True((await db.App.Pendencias.RegistrarAsync(empresa, new NovaPendencia(OrigensPendencia.Fiscal, "NOTA", "999999", new DateOnly(2026, 3, 10), "NF 999 (saída) - Cliente", 100m), adm, ct)));
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            var vm = new Vorticis.Desktop.ViewModels.MainViewModel();
            await vm.InicializarAsync(db.ConnectionString);
            var login = Assert.IsType<LoginViewModel>(vm.Conteudo);
            (login.Usuario, login.Senha) = ("admin_teste", "Senha1234");
            await login.EntrarCommand.ExecuteAsync(null);
            for (var i = 0; i < 200 && !vm.Logado; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
            vm.EmpresaSelecionada = vm.EmpresasPermitidas.Single(e => e.Id == empresa);
            await Esperar(ct);
            var lote = vm.ContabilizacaoLote;
            await lote.CarregarAsync();
            Assert.Contains("On-line", lote.ModoTexto);
            var linha = Assert.Single(lote.Pendencias);
            Assert.Equal("03/2026", linha.Competencia);
            Assert.Single(lote.Resumo);
            Assert.Contains("03/2026", lote.Resumo[0]);
            lote.OrigemSelecionada = ContabilizacaoLoteViewModel.Origens.Single(o => o.Origem == OrigensPendencia.Folha);
            await ((IAsyncRelayCommand)lote.ConsultarCommand).ExecuteAsync(null);
            Assert.Empty(lote.Pendencias);                                          // filtro por origem
            lote.OrigemSelecionada = ContabilizacaoLoteViewModel.Origens[0];
            await ((IAsyncRelayCommand)lote.ConsultarCommand).ExecuteAsync(null);
            lote.MarcarTodasCommand.Execute(null);
            await ((IAsyncRelayCommand)lote.VerPreviaCommand).ExecuteAsync(null);
            Assert.NotEmpty(lote.Previa);
            return true;
        }, ct));
        Assert.True(ok);
    }
}
