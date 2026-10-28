using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra.Tests;
using Vorticis.Suite.Desktop;

namespace Vorticis.Desktop.Tests;

/// <summary>Vorticis Suite Empresarial: os módulos numa janela só, com quatro layouts de navegação (B, a barra lateral, é o padrão) e login único.</summary>
public class SuiteTests(BancoDeTeste db, SessaoHeadless headless) : IClassFixture<BancoDeTeste>
{
    private static string ArquivoTemporario() => Path.Combine(Path.GetTempPath(), $"suite-layout-{Guid.NewGuid():N}.txt");

    private static async Task<T> ForaDaSessao<T>(Task<T> tarefa)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = tarefa.ContinueWith(t => { if (t.IsFaulted) tcs.SetException(t.Exception!.InnerExceptions); else tcs.SetResult(t.Result); }, TaskScheduler.Default);
        return await tcs.Task;
    }

    [Fact]
    public void Modulos_layout_padrao_B_arvore_e_busca()
    {
        SuiteViewModel.ArquivoLayout = ArquivoTemporario();
        var modulos = ModulosDaSuite.Criar(Path.GetTempPath());   // pasta sem executáveis: entram todos
        var suite = new SuiteViewModel(modulos);
        // em ordem alfabética em todos os layouts; o Escritório (que administra a suíte) faz o login
        Assert.Equal(["Condomínio", "Contábil", "Escritório", "Fiscal", "Folha", "Gerencial"], suite.Modulos.Select(m => m.Nome));
        Assert.Equal(["Condomínio", "Contábil", "Escritório", "Fiscal", "Folha", "Gerencial"], suite.Arvore.Select(n => n.Titulo));
        Assert.Equal(LayoutSuite.Lateral, suite.Layout);              // B é o padrão
        Assert.True(suite.EhLateral);
        Assert.Equal("Escritório", suite.Principal.Modulo.NomeCurto);
        Assert.Same(suite.Principal, suite.Ativo.Vm);                 // começa pelo Escritório
        Assert.Equal(["CD", "C", "E", "F", "FP", "G"], suite.Modulos.Select(m => m.Sigla));
        Assert.All(modulos, m => Assert.False(m.PerguntarAtivacaoAoEntrar));

        suite.Layout = LayoutSuite.Arvore;                            // a escolha fica gravada para a próxima abertura
        Assert.Equal(LayoutSuite.Arvore, SuiteViewModel.LerLayout());
        Assert.Equal(LayoutSuite.Arvore, new SuiteViewModel(ModulosDaSuite.Criar(Path.GetTempPath())).Layout);
        suite.Layout = LayoutSuite.Faixa;                             // E, a faixa de opções, também é gravada
        Assert.Equal(LayoutSuite.Faixa, new SuiteViewModel(ModulosDaSuite.Criar(Path.GetTempPath())).Layout);
        suite.Layout = LayoutSuite.Arvore;
        Assert.Equal(5, SuiteViewModel.Opcoes.Count);
        Assert.Equal(Enum.GetValues<LayoutSuite>(), SuiteViewModel.Opcoes.Select(o => o.Valor));

        Assert.Equal(6, suite.Arvore.Count);
        suite.Busca = "ferias";                                       // sem acento e sem maiúscula
        Assert.Contains(suite.Resultados, n => n.Modulo.Nome == "Folha" && n.Titulo.Contains("Férias"));
        suite.Busca = "balancete";
        Assert.Contains(suite.Resultados, n => n.Modulo.Nome == "Contábil");
        suite.Busca = "";
        Assert.Empty(suite.Resultados);

        // antes do login só o principal (tela de entrada) fica à frente
        suite.Trocar(suite.Modulos[3]);
        Assert.Same(suite.Principal, suite.Ativo.Vm);
        Assert.Contains("Entre com o seu usuário", suite.Principal.Status);

        // instalação só com alguns módulos: entram os executáveis presentes, na ordem da Suite
        var pasta = Directory.CreateTempSubdirectory("suite").FullName;
        File.WriteAllText(Path.Combine(pasta, "VorticisFiscal.exe"), "");
        File.WriteAllText(Path.Combine(pasta, "VorticisEscritorio.exe"), "");
        Assert.Equal(["Escritório", "Fiscal"], new SuiteViewModel(ModulosDaSuite.Criar(pasta)).Modulos.Select(m => m.Nome));
    }

    [Fact]
    public void Troca_de_layout_e_preferencias_por_usuario()
    {
        SuiteViewModel.ArquivoLayout = ArquivoTemporario();
        var suite = new SuiteViewModel(ModulosDaSuite.Criar(Path.GetTempPath()));
        // oculto por padrão: só a faixa de ícones; a navegação abre por cima ao passar o mouse e fecha ao escolher
        Assert.False(suite.LateralFixa);
        Assert.True(suite.MostraRail);
        Assert.False(suite.MostraNavFixa);
        suite.LateralAberta = true;
        Assert.True(suite.NavFlutuanteAberta);
        suite.LateralAberta = false;
        // fixada, a navegação ocupa o lugar da faixa (empurra o conteúdo)
        suite.AlternarFixaCommand.Execute(null);
        Assert.True(suite.MostraNavFixa);
        Assert.False(suite.MostraRail);
        Assert.Equal("Ocultar", suite.TextoFixar);
        suite.AlternarFixaCommand.Execute(null);
        Assert.Equal("Fixar", suite.TextoFixar);

        // faixa de cima (A e E): fixa por padrão; oculta, só os títulos das abas
        suite.Layout = LayoutSuite.Faixa;
        Assert.True(suite.MostraCorpoFaixa);
        suite.AlternarFaixaCommand.Execute(null);
        Assert.False(suite.MostraCorpoFaixa);
        suite.FaixaAberta = true;
        Assert.True(suite.FaixaFlutuanteAberta);
        suite.Layout = LayoutSuite.Abas;
        Assert.True(suite.AbasCompactas);                 // A oculta: abas só com o ícone
        Assert.False(suite.FaixaFlutuanteAberta);

        // cada usuário tem as suas preferências
        const long ana = 101, beto = 102;
        suite.UsarPreferenciasDe(ana);
        suite.Layout = LayoutSuite.Painel;
        suite.LateralFixa = true;
        suite.FaixaFixa = false;
        var fiscal = suite.Modulos.Single(m => m.Nome == "Fiscal");
        var folha = suite.Arvore.SelectMany(n => n.Filhos).Count();
        Assert.True(folha > 0);
        suite.UsarPreferenciasDe(beto);                    // sem arquivo ainda: vale a última escolha do computador
        suite.Layout = LayoutSuite.Arvore;
        suite.LateralFixa = false;
        suite.FaixaFixa = true;
        var no = fiscal.Raiz.Filhos.SelectMany(Folhas).First();
        suite.AlternarFavoritoCommand.Execute(no);
        Assert.Contains(suite.Favoritos, f => f.Id == no.Id);

        var outra = new SuiteViewModel(ModulosDaSuite.Criar(Path.GetTempPath()));   // nova abertura do programa
        outra.UsarPreferenciasDe(ana);
        Assert.Equal(LayoutSuite.Painel, outra.Layout);
        Assert.True(outra.LateralFixa);
        Assert.False(outra.FaixaFixa);
        Assert.Empty(outra.Favoritos);
        outra.UsarPreferenciasDe(beto);
        Assert.Equal(LayoutSuite.Arvore, outra.Layout);
        Assert.False(outra.LateralFixa);
        Assert.True(outra.FaixaFixa);
        Assert.Contains(outra.Favoritos, f => f.Id == no.Id);
        Assert.True(File.Exists(SuiteViewModel.ArquivoDoUsuario(ana)));

        // troca imediata entre os cinco layouts (flags coerentes)
        foreach (var l in Enum.GetValues<LayoutSuite>())
        {
            outra.EscolherLayoutCommand.Execute(l);
            Assert.Equal(l, outra.Layout);
            Assert.Equal(l == LayoutSuite.Abas || l == LayoutSuite.Faixa, outra.MostraAbas);
            Assert.Equal(l == LayoutSuite.Lateral || l == LayoutSuite.Arvore, outra.NavLateral);
        }
    }

    private static IEnumerable<NoSuite> Folhas(NoSuite n) => n.Filhos.Count == 0 ? (n.Chave is null ? [] : [n]) : n.Filhos.SelectMany(Folhas);

    [Fact]
    public async Task Todas_as_funcoes_acessiveis_nas_listas_da_navegacao()
    {
        await ForaDaSessao(headless.Sessao.Dispatch(() => { Todas_as_funcoes_corpo(); return true; }, TestContext.Current.CancellationToken));
    }

    private static void Todas_as_funcoes_corpo()
    {
        SuiteViewModel.ArquivoLayout = ArquivoTemporario();
        var suite = new SuiteViewModel(ModulosDaSuite.Criar(Path.GetTempPath()));
        foreach (var m in suite.Modulos)
        {
            var esperadas = Chaves(m.Vm).ToHashSet();
            Assert.NotEmpty(esperadas);
            Assert.Equal(esperadas, Folhas(m.Raiz).Select(n => n.Chave!).ToHashSet());                  // árvore (C)
            Assert.Equal(esperadas, m.Linhas.Where(n => n.EhFuncao).Select(n => n.Chave!).ToHashSet());  // barra lateral (B)
            Assert.All(m.Linhas.Where(n => n.EhGrupo), n => Assert.NotNull(n.Icone));                    // categorias com ícone
        }
    }

    private static IEnumerable<string> ChavesDoMenu(IEnumerable<object?> itens) =>
        itens.OfType<MenuItem>().Where(i => i.IsVisible).SelectMany(i => i.ItemCount > 0 ? ChavesDoMenu(i.Items.Cast<object?>())
                                                 : (i.CommandParameter as string) is { } c && c != TelasComuns.Sair ? [c] : []);

    private static IEnumerable<string> Chaves(MainViewModelBase vm) => Chaves(vm, vm.MenusVisiveis);

    private static IEnumerable<string> Chaves(MainViewModelBase vm, IEnumerable<Vorticis.Desktop.Comum.Modulos.ItemMenu> itens) =>
        itens.Where(i => i.Recurso is null || vm.RecursoAtivo(i.Recurso))
             .SelectMany(i => i.Filhos is null ? (i.Chave is null || i.EhSeparador ? [] : [i.Chave]) : Chaves(vm, i.Filhos));

    [Fact]
    public async Task Icones_dos_modulos_e_das_categorias()
    {
        await ForaDaSessao(headless.Sessao.Dispatch(() => { Icones_corpo(); return true; }, TestContext.Current.CancellationToken));
    }

    private static void Icones_corpo()
    {
        Assert.NotNull(Icones.Salvar);
        Assert.NotNull(Icones.Obter("FilePdfBox"));
        Assert.Null(Icones.Obter("NaoExiste"));
        string[] exe = ["VorticisEscritorio", "VorticisContabil", "VorticisFiscal", "VorticisFolha", "VorticisGerencial", "VorticisCondominio"];
        Assert.Equal(exe.Length, exe.Select(e => IconesSuite.DoModulo(e)).Distinct().Count());   // um desenho diferente por módulo
        foreach (var c in new[] { "_Cadastros", "_Lançamentos", "C_onsultas", "_Relatórios", "_Integrações", "_Obrigações", "Con_figurações", "_Ajuda" })
            Assert.NotSame(Icones.FolderOutline, IconesSuite.DaCategoria(c));
        Assert.Same(Icones.FolderOutline, IconesSuite.DaCategoria("Qualquer coisa"));
    }

    [Fact]
    public async Task Janela_unica_com_os_quatro_layouts_e_login_unico()
    {
        BancoDeTeste.ExigirBanco();
        SuiteViewModel.ArquivoLayout = ArquivoTemporario();
        var adm = await db.AdminAsync();
        await db.App.Perfis.ConcluirConfiguracaoInicialAsync(new SessaoUsuario(adm, "admin_teste", "Admin", true, false), TestContext.Current.CancellationToken);
        var ct = TestContext.Current.CancellationToken;
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            ModulosDaSuite.IncluirTelas(Avalonia.Application.Current!);
            var suite = new SuiteViewModel(ModulosDaSuite.Criar(Path.GetTempPath()));
            ModuloSuite M(string nome) => suite.Modulos.Single(m => m.Nome == nome);
            var janela = new MainWindow { Width = 1400, Height = 860 };
            janela.UsarSuite(suite);
            janela.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Vorticis Suite Empresarial — Escritório", janela.Title);
            // B oculta: só a faixa de ícones; a navegação abre por cima ao passar o mouse; "Fixar" deixa aberta (e fica gravado)
            var ladoB = janela.FindDescendantOfType<SuiteLadoView>()!;
            Assert.True(ladoB.FindControl<Border>("Rail")!.IsVisible);
            Assert.False(ladoB.FindControl<SuiteNavegacaoView>("NavFixa")!.IsVisible);
            Assert.False(ladoB.FindControl<Avalonia.Controls.Primitives.Popup>("Flutuante")!.IsOpen);
            suite.LateralAberta = true;
            Assert.True(ladoB.FindControl<Avalonia.Controls.Primitives.Popup>("Flutuante")!.IsOpen);
            suite.LateralAberta = false;
            suite.AlternarFixaCommand.Execute(null);
            Assert.True(ladoB.FindControl<SuiteNavegacaoView>("NavFixa")!.IsVisible);
            Assert.False(ladoB.FindControl<Border>("Rail")!.IsVisible);
            Assert.Equal("fixa", File.ReadAllText(SuiteViewModel.ArquivoFixa));
            suite.AlternarFixaCommand.Execute(null);
            Assert.NotNull(janela.CaptureRenderedFrame());

            // login uma vez, no principal: todos os módulos entram com o mesmo usuário
            await suite.Principal.InicializarAsync(db.ConnectionString);
            var login = Assert.IsType<LoginViewModel>(suite.Principal.Conteudo);
            (login.Usuario, login.Senha) = ("admin_teste", "Senha1234");
            await login.EntrarCommand.ExecuteAsync(null);
            for (var i = 0; i < 200 && suite.Modulos.Any(m => !m.Vm.Logado); i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
            Assert.All(suite.Modulos, m => Assert.True(m.Vm.Logado, m.Nome));
            Assert.All(suite.Modulos, m => Assert.Equal(adm, m.Vm.Sessao!.Id));
            Assert.All(suite.Modulos.Where(m => m.Vm != suite.Principal), m => Assert.True(m.Vm.ConexaoCompartilhada));
            Assert.Same(suite.Principal.App, M("Contábil").Vm.App);

            // trocar de módulo: a janela passa a mostrar o Contábil; as funções do Escritório ficam escondidas
            var abertasEscritorio = suite.Principal.JanelasAbertas.Count;
            suite.Trocar(M("Contábil"));
            Dispatcher.UIThread.RunJobs();
            Assert.Same(M("Contábil").Vm, janela.DataContext);
            Assert.Equal("Vorticis Suite Empresarial — Contábil", janela.Title);
            var menus = janela.FindControl<Menu>("MenuPrincipal")!.Items.OfType<MenuItem>().ToList();
            Assert.Contains(menus, m => (m.Header as string) == "E_xibir");
            var config = Assert.Single(menus, m => (m.Header as string) == "Con_figurações");
            Assert.Equal("Layout da Suite", Assert.IsType<MenuItem>(config.Items[0]).Header);   // o layout também em Configurações
            M("Contábil").Vm.AbrirCommand.Execute(TelasComuns.Sobre);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, M("Contábil").Abertas);
            Assert.Equal(abertasEscritorio, suite.Principal.JanelasAbertas.Count);
            // ida e volta entre módulos: a função continua com a janela principal como dona (fica sempre por cima dela)
            suite.Trocar(M("Fiscal"));
            Dispatcher.UIThread.RunJobs();
            Assert.False(janela.JanelaDa(M("Contábil").Vm.JanelasAbertas[0].ViewModel)!.IsVisible);
            suite.Trocar(M("Contábil"));
            Dispatcher.UIThread.RunJobs();
            var funcao = janela.JanelaDa(M("Contábil").Vm.JanelasAbertas[0].ViewModel)!;
            Assert.True(funcao.IsVisible);
            Assert.Same(janela, funcao.Owner);
            // a barra fixa de funções abertas fica acima da área das janelas: nenhuma função a cobre
            var barra = janela.FindControl<Border>("BarraFuncoes")!;
            Assert.True(barra.IsVisible);
            var fimBarra = janela.PointToScreen(barra.TranslatePoint(new Avalonia.Point(0, barra.Bounds.Height), janela)!.Value).Y;
            Assert.True(funcao.Position.Y >= fimBarra, $"função em {funcao.Position.Y}, barra termina em {fimBarra}");

            // os cinco layouts renderizam e mostram só o que é deles
            foreach (var layout in Enum.GetValues<LayoutSuite>())
            {
                suite.Layout = layout;
                Dispatcher.UIThread.RunJobs();
                Dispatcher.UIThread.RunJobs();
                Assert.NotNull(janela.CaptureRenderedFrame());
                var lado = janela.FindDescendantOfType<SuiteLadoView>()!;
                var topo = janela.FindDescendantOfType<SuiteTopoView>()!;
                Assert.Equal(layout is LayoutSuite.Lateral or LayoutSuite.Arvore, lado.FindControl<Border>("Rail")!.IsVisible);
                Assert.Equal(layout is LayoutSuite.Abas or LayoutSuite.Faixa, topo.FindControl<Border>("BarraAbas")!.IsVisible);
                Assert.Equal(layout == LayoutSuite.Faixa, topo.FindControl<ContentControl>("CorpoFixo")!.IsVisible);
                Assert.Equal(layout == LayoutSuite.Painel, topo.FindControl<Border>("BarraPainel")!.IsVisible);
                Assert.Equal(layout == LayoutSuite.Painel, janela.FindControl<ContentControl>("SuitePainel")!.IsVisible);   // D abre no painel
                Assert.Contains(janela.GetVisualDescendants().OfType<SuiteEscolhaLayoutView>(), b => b.IsEffectivelyVisible);   // o botão Layout está em todos
                // o menu do módulo (todas as funções) continua à mostra em qualquer layout
                var menu = janela.FindControl<Menu>("MenuPrincipal")!;
                if (layout != LayoutSuite.Faixa)
                    Assert.Equal(Chaves(M("Contábil").Vm).Count(c => c != TelasComuns.Sair), ChavesDoMenu(menu.Items).Count());
                else
                {
                    Assert.False(janela.FindControl<StackPanel>("BarraMenuWrap")!.IsVisible);   // a faixa substitui a barra de menus
                    Assert.True(topo.FindControl<Button>("BotaoExibir")!.IsVisible);
                }
                if (layout != LayoutSuite.Faixa) Assert.True(janela.FindControl<StackPanel>("BarraMenuWrap")!.IsVisible);
                if (layout == LayoutSuite.Faixa)
                {
                    // faixa de opções: todas as funções do módulo estão em algum botão ou no "Mais"
                    var ativo = suite.Ativo;
                    Assert.Equal(Chaves(ativo.Vm).Order(), topo.ChavesDaFaixa().Order());
                    // oculta, só as abas ficam à mostra; abre por cima ao clicar
                    suite.AlternarFaixaCommand.Execute(null);
                    Assert.False(topo.FindControl<ContentControl>("CorpoFixo")!.IsVisible);
                    suite.FaixaAberta = true;
                    Assert.True(topo.FindControl<Avalonia.Controls.Primitives.Popup>("CorpoFlutuante")!.IsOpen);
                    suite.AlternarFaixaCommand.Execute(null);
                    Assert.True(topo.FindControl<ContentControl>("CorpoFixo")!.IsVisible);
                }
            }

            // navegação fixa à esquerda: a barra de cima fica só com Exibir e Ajuda (as funções estão na lateral)
            suite.Layout = LayoutSuite.Lateral;
            suite.LateralFixa = true;
            Dispatcher.UIThread.RunJobs();
            var visiveis = janela.FindControl<Menu>("MenuPrincipal")!.Items.OfType<MenuItem>().Where(m => m.IsVisible).Select(m => m.Header as string).ToList();
            Assert.Equal(["E_xibir", "_Ajuda"], visiveis);
            Assert.Equal(Chaves(M("Contábil").Vm).ToHashSet(), M("Contábil").Linhas.Where(n => n.EhFuncao).Select(n => n.Chave!).ToHashSet());
            suite.LateralFixa = false;
            Dispatcher.UIThread.RunJobs();
            // a barra de funções abertas só aparece com função aberta
            Assert.Equal(M("Contábil").Vm.JanelasAbertas.Count > 0, janela.FindControl<Border>("BarraFuncoes")!.IsVisible);

            // painel D: abrir um módulo pelo cartão sai do painel
            suite.TrocarCommand.Execute(M("Fiscal"));
            Dispatcher.UIThread.RunJobs();
            Assert.False(suite.MostraPainel);
            Assert.Same(M("Fiscal").Vm, janela.DataContext);

            // árvore C: abrir uma função de outro módulo troca o módulo e abre a tela
            suite.Layout = LayoutSuite.Arvore;
            suite.Busca = "sobre";
            var sobreFolha = suite.Resultados.First(n => n.Modulo.Nome == "Folha");
            suite.AbrirFuncaoCommand.Execute(sobreFolha);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(M("Folha"), suite.Ativo);
            Assert.Equal(1, M("Folha").Abertas);

            // sair em qualquer módulo sai de todos e volta ao login do principal
            M("Folha").Vm.SairCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.All(suite.Modulos, m => Assert.False(m.Vm.Logado, m.Nome));
            Assert.Same(M("Escritório"), suite.Ativo);
            Assert.Same(suite.Principal, janela.DataContext);
            janela.Close();
            return true;
        }, ct));
        Assert.True(ok);
    }

    /// <summary>Layout E: clicar de verdade (mouse) num botão da faixa e num item de menu suspenso da faixa abre a tela correspondente.</summary>
    [Fact]
    public async Task Faixa_de_opcoes_abre_a_tela_ao_clicar_no_botao_e_no_item_do_menu_suspenso()
    {
        BancoDeTeste.ExigirBanco();
        SuiteViewModel.ArquivoLayout = ArquivoTemporario();
        var adm = await db.AdminAsync();
        await db.App.Perfis.ConcluirConfiguracaoInicialAsync(new SessaoUsuario(adm, "admin_teste", "Admin", true, false), TestContext.Current.CancellationToken);
        var ct = TestContext.Current.CancellationToken;
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            ModulosDaSuite.IncluirTelas(Avalonia.Application.Current!);
            var suite = new SuiteViewModel(ModulosDaSuite.Criar(Path.GetTempPath()));
            var janela = new MainWindow { Width = 1400, Height = 860 };
            janela.UsarSuite(suite);
            janela.Show();
            Dispatcher.UIThread.RunJobs();
            await suite.Principal.InicializarAsync(db.ConnectionString);
            var login = Assert.IsType<LoginViewModel>(suite.Principal.Conteudo);
            (login.Usuario, login.Senha) = ("admin_teste", "Senha1234");
            await login.EntrarCommand.ExecuteAsync(null);
            for (var i = 0; i < 200 && suite.Modulos.Any(m => !m.Vm.Logado); i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
            suite.Trocar(suite.Modulos.Single(m => m.Nome == "Contábil"));
            suite.FaixaFixa = true;
            suite.Layout = LayoutSuite.Faixa;
            for (var i = 0; i < 4; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
            var contabil = suite.Ativo;
            var topo = janela.FindDescendantOfType<SuiteTopoView>()!;
            var fixo = topo.FindControl<ContentControl>("CorpoFixo")!;

            void Clicar(Control alvo)
            {
                var topLevel = TopLevel.GetTopLevel(alvo)!;
                var centro = alvo.TranslatePoint(new Avalonia.Point(alvo.Bounds.Width / 2, alvo.Bounds.Height / 2), topLevel)!.Value;
                topLevel.MouseMove(centro);
                topLevel.MouseDown(centro, Avalonia.Input.MouseButton.Left);
                topLevel.MouseUp(centro, Avalonia.Input.MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
            }

            // botão direto da faixa
            var direto = fixo.GetVisualDescendants().OfType<Button>().First(b => (b.Tag as NoSuite)?.Chave is not null && b.IsEffectivelyVisible);
            var antes = contabil.Abertas;
            Clicar(direto);
            for (var i = 0; i < 5; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
            Assert.True(contabil.Abertas > antes, "o clique no botão da faixa não abriu a tela");

            // item de um menu suspenso da faixa
            var comMenu = fixo.GetVisualDescendants().OfType<Button>().First(b => b.Flyout is MenuFlyout && b.IsEffectivelyVisible);
            var fly = (MenuFlyout)comMenu.Flyout!;
            antes = contabil.Abertas;
            Clicar(comMenu);
            Assert.True(fly.IsOpen, "o menu suspenso da faixa não abriu");
            var folha = fly.Items.OfType<MenuItem>().First(i => (i.CommandParameter as NoSuite)?.Chave is not null
                                                                && !contabil.Vm.JanelasAbertas.Any(j => j.Tela == ((NoSuite)i.CommandParameter!).Chave));
            var alvo = (NoSuite)folha.CommandParameter!;
            Clicar(folha);
            for (var i = 0; i < 5; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
            // a função é pedida ao módulo (a tela em si pode exigir empresa; o registro de uso prova que o comando foi executado)
            Assert.Same(alvo, suite.Recentes.FirstOrDefault());
            Assert.False(fly.IsOpen, "o menu suspenso deve fechar depois do clique");

            // sem empresa a função não abre, mas o clique agora explica o motivo (antes não fazia nada) e o botão aparece apagado
            var semEmpresa = contabil.Vm.EmpresaSelecionada is null;
            if (semEmpresa)
            {
                var folhaNo = contabil.Raiz.Filhos.SelectMany(Folhas).FirstOrDefault(n => n.Chave is not null && !contabil.Vm.AbrirCommand.CanExecute(n.Chave));
                Assert.NotNull(folhaNo);
                Assert.False(suite.PodeAbrirFuncao(folhaNo));
                contabil.Vm.Status = "";
                suite.AbrirFuncaoCommand.Execute(folhaNo);
                Assert.Contains("não está disponível agora", contabil.Vm.Status);
            }

            // submenu dentro do menu suspenso da faixa (quando existe): abre ao clicar e o item de dentro executa
            Button? comSub = null;
            foreach (var mod in suite.Modulos)
            {
                suite.Trocar(mod);
                for (var i = 0; i < 3; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
                comSub = fixo.GetVisualDescendants().OfType<Button>().Where(b => b.Flyout is MenuFlyout && b.IsEffectivelyVisible)
                             .FirstOrDefault(b => ((MenuFlyout)b.Flyout!).Items.OfType<MenuItem>().Any(m => m.Items.Count > 0));
                if (comSub is not null) break;
            }
            if (comSub is not null)
            {
                var f2 = (MenuFlyout)comSub.Flyout!;
                Clicar(comSub);
                var pai = f2.Items.OfType<MenuItem>().First(m => m.Items.Count > 0);
                Clicar(pai);
                Assert.True(pai.IsSubMenuOpen, "o submenu da faixa não abriu ao clicar");
                var filho = pai.Items.OfType<MenuItem>().First(m => (m.CommandParameter as NoSuite)?.Chave is not null);
                var alvoFilho = (NoSuite)filho.CommandParameter!;
                Clicar(filho);
                for (var i = 0; i < 5; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
                Assert.Same(alvoFilho, suite.Recentes.FirstOrDefault());
            }

            // faixa oculta: passar o mouse nas abas abre a faixa por cima e o clique no botão também abre a tela
            suite.FaixaFixa = false;
            for (var i = 0; i < 3; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
            var barra = topo.FindControl<Border>("BarraAbas")!;
            var popup = topo.FindControl<Avalonia.Controls.Primitives.Popup>("CorpoFlutuante")!;
            var pontoAba = barra.TranslatePoint(new Avalonia.Point(barra.Bounds.Width / 2, 6), janela)!.Value;
            janela.MouseMove(pontoAba);
            Dispatcher.UIThread.RunJobs();
            Assert.True(popup.IsOpen, "a faixa oculta não abriu ao passar o mouse nas abas");
            var corpo = topo.FindControl<ContentControl>("CorpoPopup")!;
            var outro = corpo.GetVisualDescendants().OfType<Button>().Where(b => (b.Tag as NoSuite)?.Chave is not null && b.IsEffectivelyVisible)
                             .First(b => !contabil.Vm.JanelasAbertas.Any(j => j.Tela == ((NoSuite)b.Tag!).Chave));
            var alvoFlutuante = (NoSuite)outro.Tag!;
            Clicar(outro);
            for (var i = 0; i < 5; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
            Assert.Same(alvoFlutuante, suite.Recentes.FirstOrDefault());
            janela.Close();
            return true;
        }, ct));
        Assert.True(ok);
    }

    /// <summary>
    /// Verificação visual: renderiza os cinco layouts (e o seletor) em 1366x768 com dados de exemplo. Com VORTICIS_SCREENSHOTS apontando para uma
    /// pasta, grava os PNGs (layout_A_*.png ... seletor.png) para conferir a olho.
    /// </summary>
    [Fact]
    public async Task Capturas_dos_cinco_layouts_e_do_seletor()
    {
        BancoDeTeste.ExigirBanco();
        SuiteViewModel.ArquivoLayout = ArquivoTemporario();
        var adm = await db.AdminAsync();
        await db.App.Perfis.ConcluirConfiguracaoInicialAsync(new SessaoUsuario(adm, "admin_teste", "Admin", true, false), TestContext.Current.CancellationToken);
        var ct = TestContext.Current.CancellationToken;
        var pasta = Environment.GetEnvironmentVariable("VORTICIS_SCREENSHOTS");
        if (pasta is not null) Directory.CreateDirectory(pasta);
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            ModulosDaSuite.IncluirTelas(Avalonia.Application.Current!);
            var suite = new SuiteViewModel(ModulosDaSuite.Criar(Path.GetTempPath()));
            var janela = new MainWindow();
            janela.UsarSuite(suite);
            janela.Show();
            Dispatcher.UIThread.RunJobs();
            await suite.Principal.InicializarAsync(db.ConnectionString);
            var login = Assert.IsType<LoginViewModel>(suite.Principal.Conteudo);
            (login.Usuario, login.Senha) = ("admin_teste", "Senha1234");
            await login.EntrarCommand.ExecuteAsync(null);
            for (var i = 0; i < 200 && suite.Modulos.Any(m => !m.Vm.Logado); i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
            janela.WindowState = WindowState.Normal;
            janela.Width = 1366; janela.Height = 768;
            for (var i = 0; i < 5; i++) { await Task.Delay(30, ct); Dispatcher.UIThread.RunJobs(); }

            // dados de exemplo: vencimentos, favoritos e funções usadas
            ModuloSuite M(string nome) => suite.Modulos.Single(m => m.Nome == nome);
            var inicio = suite.Vencimentos!;
            inicio.Proximas.Clear();
            var amarelo = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FDE68A"));
            var vermelho = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FECACA"));
            var verde = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#BBF7D0"));
            inicio.Proximas.Add(new("10/11", "GPS/INSS (10/2026)", "vence em 14 dias", amarelo));
            inicio.Proximas.Add(new("20/11", "DCTFWeb (10/2026)", "vence em 24 dias", verde));
            inicio.Proximas.Add(new("25/10", "EFD-Reinf (09/2026)", "atrasada", vermelho));
            inicio.TemProximas = true;
            NoSuite Achar(string modulo, string trecho) => M(modulo).Raiz.Filhos.SelectMany(Folhas).First(n => n.Titulo.Contains(trecho, StringComparison.OrdinalIgnoreCase));
            suite.AlternarFavoritoCommand.Execute(Achar("Contábil", "Balancete"));
            suite.AlternarFavoritoCommand.Execute(Achar("Folha", "Férias"));
            suite.AlternarFavoritoCommand.Execute(Achar("Fiscal", "Notas"));
            suite.AbrirFuncaoCommand.Execute(Achar("Contábil", "Plano de contas"));
            suite.AbrirFuncaoCommand.Execute(Achar("Contábil", "Razão"));
            suite.AbrirFuncaoCommand.Execute(Achar("Folha", "Funcionários"));
            for (var i = 0; i < 3; i++) { await Task.Delay(30, ct); Dispatcher.UIThread.RunJobs(); }
            foreach (var m in suite.Modulos) foreach (var f in m.Vm.JanelasAbertas.ToList()) m.Vm.FecharFuncaoCommand.Execute(f);
            suite.Trocar(M("Contábil"));
            Dispatcher.UIThread.RunJobs();

            void Foto(string nome)
            {
                for (var i = 0; i < 3; i++) Dispatcher.UIThread.RunJobs();
                var quadro = janela.CaptureRenderedFrame();
                Assert.NotNull(quadro);
                if (pasta is not null) quadro.Save(Path.Combine(pasta, nome + ".png"));
            }

            suite.LateralFixa = false; suite.FaixaFixa = true;
            suite.Layout = LayoutSuite.Abas; Foto("layout_A_abas");
            suite.FaixaFixa = false; Foto("layout_A_abas_ocultas"); suite.FaixaFixa = true;
            suite.Layout = LayoutSuite.Lateral; Foto("layout_B_lateral_oculta");
            suite.LateralFixa = true; Foto("layout_B_lateral_fixa"); suite.LateralFixa = false;
            suite.Layout = LayoutSuite.Arvore; Foto("layout_C_arvore_oculta");
            suite.LateralFixa = true;
            suite.Busca = ""; Foto("layout_C_arvore_fixa");
            suite.Busca = "balan"; Foto("layout_C_arvore_busca"); suite.Busca = ""; suite.LateralFixa = false;
            suite.Layout = LayoutSuite.Painel;
            for (var i = 0; i < 8; i++) { await Task.Delay(40, ct); Dispatcher.UIThread.RunJobs(); }   // a consulta de vencimentos termina antes dos dados de exemplo
            inicio = suite.Vencimentos!;
            inicio.Proximas.Clear();
            inicio.Proximas.Add(new("10/11", "GPS/INSS (10/2026)", "vence em 14 dias", amarelo));
            inicio.Proximas.Add(new("20/11", "DCTFWeb (10/2026)", "vence em 24 dias", verde));
            inicio.Proximas.Add(new("25/10", "EFD-Reinf (09/2026)", "atrasada", vermelho));
            inicio.TemProximas = true;
            Foto("layout_D_painel");
            suite.Trocar(M("Folha")); Foto("layout_D_modulo"); suite.Trocar(M("Contábil"));
            suite.Layout = LayoutSuite.Faixa; Foto("layout_E_faixa");
            suite.FaixaFixa = false; Foto("layout_E_faixa_recolhida"); suite.FaixaFixa = true;
            suite.Trocar(M("Folha")); Foto("layout_E_faixa_folha"); suite.Trocar(M("Contábil"));

            // seletor de layouts (em cima da janela, como o botão Layout o abre)
            suite.Layout = LayoutSuite.Lateral;
            var fundo = new Avalonia.Controls.Border { Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#6F94A3B8")) };
            var seletor = new SuiteSeletorView { DataContext = suite, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            var tela = new Window { Width = 1366, Height = 768, Content = new Avalonia.Controls.Panel { Children = { new Avalonia.Controls.Border { Background = Avalonia.Media.Brushes.White }, fundo, seletor } } };
            tela.Show();
            for (var i = 0; i < 3; i++) Dispatcher.UIThread.RunJobs();
            var q = tela.CaptureRenderedFrame();
            Assert.NotNull(q);
            if (pasta is not null) q.Save(Path.Combine(pasta, "seletor.png"));
            tela.Close();
            janela.Close();
            return true;
        }, ct));
        Assert.True(ok);
    }
}
