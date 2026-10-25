using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra.Tests;
using Vorticis.Suite.Desktop;

namespace Vorticis.Desktop.Tests;

/// <summary>Vorticis Suite: os módulos numa janela só, com quatro layouts de navegação (B, a barra lateral, é o padrão) e login único.</summary>
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
        Assert.Equal(["Condomínio", "Contábil", "Escritório", "Fiscal", "Folha", "Frota", "Gerencial"], suite.Modulos.Select(m => m.Nome));
        Assert.Equal(["Vorticis Condomínio", "Vorticis Contábil", "Vorticis Escritório", "Vorticis Fiscal", "Vorticis Folha de Pagamento", "Vorticis Frota", "Vorticis Gerencial"],
                     suite.Arvore.Select(n => n.Titulo));
        Assert.Equal(LayoutSuite.Lateral, suite.Layout);              // B é o padrão
        Assert.True(suite.EhLateral);
        Assert.Equal("Escritório", suite.Principal.Modulo.NomeCurto);
        Assert.Same(suite.Principal, suite.Ativo.Vm);                 // começa pelo Escritório
        Assert.Equal(["CD", "C", "E", "F", "FP", "FR", "G"], suite.Modulos.Select(m => m.Sigla));
        Assert.All(modulos, m => Assert.False(m.PerguntarAtivacaoAoEntrar));

        suite.Layout = LayoutSuite.Arvore;                            // a escolha fica gravada para a próxima abertura
        Assert.Equal(LayoutSuite.Arvore, SuiteViewModel.LerLayout());
        Assert.Equal(LayoutSuite.Arvore, new SuiteViewModel(ModulosDaSuite.Criar(Path.GetTempPath())).Layout);

        Assert.Equal(7, suite.Arvore.Count);
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
            Assert.Equal("Vorticis Suite — Escritório", janela.Title);
            // B recolhida numa faixa fina até o mouse chegar; "Fixar" deixa aberta (e fica gravado)
            var ladoB = janela.FindDescendantOfType<SuiteLadoView>()!;
            Assert.True(ladoB.FindControl<Border>("FaixaLateral")!.IsVisible);
            Assert.False(ladoB.FindControl<Border>("BarraLateral")!.IsVisible);
            suite.LateralAberta = true;
            Assert.True(ladoB.FindControl<Border>("BarraLateral")!.IsVisible);
            suite.LateralAberta = false;
            suite.AlternarFixaCommand.Execute(null);
            Assert.True(ladoB.FindControl<Border>("BarraLateral")!.IsVisible);
            Assert.Equal("fixa", File.ReadAllText(SuiteViewModel.ArquivoFixa));
            suite.AlternarFixaCommand.Execute(null);
            suite.LateralAberta = true;
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
            Assert.Equal("Vorticis Suite — Contábil", janela.Title);
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

            // os quatro layouts renderizam
            foreach (var layout in Enum.GetValues<LayoutSuite>())
            {
                suite.Layout = layout;
                Dispatcher.UIThread.RunJobs();
                Dispatcher.UIThread.RunJobs();
                var quadro = janela.CaptureRenderedFrame();
                Assert.NotNull(quadro);
                if (Environment.GetEnvironmentVariable("VORTICIS_SCREENSHOTS") is { } pasta) quadro.Save(Path.Combine(pasta, $"suite_{layout}.png"));
                var lado = janela.FindDescendantOfType<SuiteLadoView>()!;
                var topo = janela.FindDescendantOfType<SuiteTopoView>()!;
                Assert.Equal(layout == LayoutSuite.Lateral, lado.FindControl<Border>("BarraLateral")!.IsVisible);
                Assert.Equal(layout == LayoutSuite.Arvore, lado.FindControl<Border>("PainelArvore")!.IsVisible);
                // o botão Layout aparece em todos os layouts
                Assert.Contains(janela.GetVisualDescendants().OfType<SuiteEscolhaLayoutView>(), b => b.IsEffectivelyVisible || layout == LayoutSuite.Lateral);
                Assert.Equal(layout == LayoutSuite.Abas, topo.FindControl<Border>("BarraAbas")!.IsVisible);
                Assert.Equal(layout == LayoutSuite.Painel, topo.FindControl<Border>("BarraPainel")!.IsVisible);
                Assert.Equal(layout == LayoutSuite.Painel, janela.FindControl<ContentControl>("SuitePainel")!.IsVisible);   // D abre no painel
            }

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
}
