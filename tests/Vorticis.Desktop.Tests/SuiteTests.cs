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
        Assert.Equal(["Escritório", "Contábil", "Fiscal", "Folha", "Condomínio", "Gerencial"], modulos.Select(m => m.Modulo.NomeCurto));
        var suite = new SuiteViewModel(modulos);
        Assert.Equal(LayoutSuite.Lateral, suite.Layout);              // B é o padrão
        Assert.True(suite.EhLateral);
        Assert.Same(modulos[0], suite.Principal);                     // o Escritório conecta e faz o login
        Assert.Equal(["E", "C", "F", "FP", "CD", "G"], suite.Modulos.Select(m => m.Sigla));
        Assert.All(modulos, m => Assert.False(m.PerguntarAtivacaoAoEntrar));

        suite.Layout = LayoutSuite.Arvore;                            // a escolha fica gravada para a próxima abertura
        Assert.Equal(LayoutSuite.Arvore, SuiteViewModel.LerLayout());
        Assert.Equal(LayoutSuite.Arvore, new SuiteViewModel(ModulosDaSuite.Criar(Path.GetTempPath())).Layout);

        Assert.Equal(6, suite.Arvore.Count);
        suite.Busca = "ferias";                                       // sem acento e sem maiúscula
        Assert.Contains(suite.Resultados, n => n.Modulo.Nome == "Folha" && n.Titulo.Contains("Férias"));
        suite.Busca = "balancete";
        Assert.Contains(suite.Resultados, n => n.Modulo.Nome == "Contábil");
        suite.Busca = "";
        Assert.Empty(suite.Resultados);

        // antes do login só o principal (tela de entrada) fica à frente
        suite.Trocar(suite.Modulos[2]);
        Assert.Same(suite.Modulos[0], suite.Ativo);
        Assert.Contains("Entre com o seu usuário", suite.Modulos[0].Vm.Status);

        // instalação só com alguns módulos: entram os executáveis presentes, na ordem da Suite
        var pasta = Directory.CreateTempSubdirectory("suite").FullName;
        File.WriteAllText(Path.Combine(pasta, "VorticisFiscal.exe"), "");
        File.WriteAllText(Path.Combine(pasta, "VorticisEscritorio.exe"), "");
        Assert.Equal(["Escritório", "Fiscal"], ModulosDaSuite.Criar(pasta).Select(m => m.Modulo.NomeCurto));
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
            var janela = new MainWindow { Width = 1400, Height = 860 };
            janela.UsarSuite(suite);
            janela.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Vorticis Suite — Escritório", janela.Title);
            Assert.True(janela.FindDescendantOfType<SuiteLadoView>()!.FindControl<Border>("BarraLateral")!.IsVisible);
            Assert.NotNull(janela.CaptureRenderedFrame());

            // login uma vez, no principal: todos os módulos entram com o mesmo usuário
            await suite.Principal.InicializarAsync(db.ConnectionString);
            var login = Assert.IsType<LoginViewModel>(suite.Principal.Conteudo);
            (login.Usuario, login.Senha) = ("admin_teste", "Senha1234");
            await login.EntrarCommand.ExecuteAsync(null);
            for (var i = 0; i < 200 && suite.Modulos.Any(m => !m.Vm.Logado); i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
            Assert.All(suite.Modulos, m => Assert.True(m.Vm.Logado, m.Nome));
            Assert.All(suite.Modulos, m => Assert.Equal(adm, m.Vm.Sessao!.Id));
            Assert.All(suite.Modulos.Skip(1), m => Assert.True(m.Vm.ConexaoCompartilhada));
            Assert.Same(suite.Principal.App, suite.Modulos[1].Vm.App);

            // trocar de módulo: a janela passa a mostrar o Contábil; as funções do Escritório ficam escondidas
            var abertasEscritorio = suite.Principal.JanelasAbertas.Count;
            suite.Trocar(suite.Modulos[1]);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(suite.Modulos[1].Vm, janela.DataContext);
            Assert.Equal("Vorticis Suite — Contábil", janela.Title);
            Assert.Contains(janela.FindControl<Menu>("MenuPrincipal")!.Items.OfType<MenuItem>(), m => (m.Header as string) == "E_xibir");
            suite.Modulos[1].Vm.AbrirCommand.Execute(TelasComuns.Sobre);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, suite.Modulos[1].Abertas);
            Assert.Equal(abertasEscritorio, suite.Principal.JanelasAbertas.Count);

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
                Assert.Equal(layout == LayoutSuite.Abas, topo.FindControl<Border>("BarraAbas")!.IsVisible);
                Assert.Equal(layout == LayoutSuite.Painel, topo.FindControl<Border>("BarraPainel")!.IsVisible);
                Assert.Equal(layout == LayoutSuite.Painel, janela.FindControl<ContentControl>("SuitePainel")!.IsVisible);   // D abre no painel
            }

            // painel D: abrir um módulo pelo cartão sai do painel
            suite.TrocarCommand.Execute(suite.Modulos[2]);
            Dispatcher.UIThread.RunJobs();
            Assert.False(suite.MostraPainel);
            Assert.Same(suite.Modulos[2].Vm, janela.DataContext);

            // árvore C: abrir uma função de outro módulo troca o módulo e abre a tela
            suite.Layout = LayoutSuite.Arvore;
            suite.Busca = "sobre";
            var sobreFolha = suite.Resultados.First(n => n.Modulo.Nome == "Folha");
            suite.AbrirFuncaoCommand.Execute(sobreFolha);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(suite.Modulos[3], suite.Ativo);
            Assert.Equal(1, suite.Modulos[3].Abertas);

            // sair em qualquer módulo sai de todos e volta ao login do principal
            suite.Modulos[3].Vm.SairCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.All(suite.Modulos, m => Assert.False(m.Vm.Logado, m.Nome));
            Assert.Same(suite.Modulos[0], suite.Ativo);
            Assert.Same(suite.Principal, janela.DataContext);
            janela.Close();
            return true;
        }, ct));
        Assert.True(ok);
    }
}
