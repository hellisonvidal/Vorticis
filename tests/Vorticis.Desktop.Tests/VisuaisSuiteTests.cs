using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Desktop.Comum.Views;
using Vorticis.Suite.Desktop;

namespace Vorticis.Desktop.Tests;

/// <summary>Os dez visuais da Suite: moldura e detalhes de cada um, preferência gravada e as partes novas na janela.</summary>
public class VisuaisSuiteTests(SessaoHeadless headless)
{
    private static async Task<T> ForaDaSessao<T>(Task<T> tarefa)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = tarefa.ContinueWith(t => { if (t.IsFaulted) tcs.SetException(t.Exception!.InnerExceptions); else tcs.SetResult(t.Result); }, TaskScheduler.Default);
        return await tcs.Task;
    }

    private static string ArquivoTemporario() => Path.Combine(Directory.CreateTempSubdirectory("visuais").FullName, "suite-layout.txt");

    [Fact]
    public void Cada_visual_liga_a_sua_moldura_e_os_seus_detalhes_e_fica_gravado()
    {
        SuiteViewModel.ArquivoLayout = ArquivoTemporario();
        var suite = new SuiteViewModel(ModulosDaSuite.Criar(Path.GetTempPath()));
        try
        {
            Assert.Equal(10, SuiteViewModel.Visuais.Count);
            Assert.Equal(Enumerable.Range(1, 10), SuiteViewModel.Visuais.Select(v => v.Numero));
            Assert.Equal(Enum.GetValues<VisualSuite>(), SuiteViewModel.Visuais.Select(v => v.Valor));

            var esperado = new Dictionary<VisualSuite, (LayoutSuite Layout, bool Comandos, bool Caminho, bool Teclas, bool Compacta, bool ListaEsquerda)>
            {
                [VisualSuite.Classico] = (LayoutSuite.Abas, false, false, false, false, false),
                [VisualSuite.BarraLateral] = (LayoutSuite.Lateral, false, false, false, false, false),
                [VisualSuite.Comandos] = (LayoutSuite.Abas, true, true, false, false, false),
                [VisualSuite.ListaFicha] = (LayoutSuite.Abas, false, true, false, false, true),
                [VisualSuite.Faixa] = (LayoutSuite.Faixa, false, false, false, false, false),
                [VisualSuite.Mosaico] = (LayoutSuite.Painel, false, false, false, false, false),
                [VisualSuite.Trilho] = (LayoutSuite.Lateral, false, false, false, false, false),
                [VisualSuite.Paineis] = (LayoutSuite.Arvore, false, false, false, false, false),
                [VisualSuite.Busca] = (LayoutSuite.Busca, false, false, false, false, false),
                [VisualSuite.AltaDensidade] = (LayoutSuite.Abas, false, false, true, true, false),
            };
            foreach (var (visual, e) in esperado)
            {
                suite.EscolherVisualCommand.Execute(visual);
                Assert.Equal((e.Layout, e.Comandos, e.Caminho, e.Teclas, e.Compacta, e.ListaEsquerda),
                             (suite.Layout, suite.MostraComandos, suite.MostraCaminho, suite.MostraTeclas, suite.Compacta, suite.ListaEsquerda));
                Assert.Equal(e.Compacta, EstilosVisuais.Compacta);
                Assert.Equal(e.ListaEsquerda, EstilosVisuais.ListaEsquerda);
                Assert.Equal(visual, SuiteViewModel.LerVisual());                                       // gravado para a próxima abertura
                Assert.Equal(visual, new SuiteViewModel(ModulosDaSuite.Criar(Path.GetTempPath())).Visual);
            }

            // 2 deixa o menu da esquerda fixo; 7 (trilho) oculto; 10 passa para o tema escuro
            suite.EscolherVisualCommand.Execute(VisualSuite.BarraLateral);
            Assert.True(suite.MostraNavFixa);
            suite.EscolherVisualCommand.Execute(VisualSuite.Trilho);
            Assert.True(suite.MostraRail);
            suite.EscolherVisualCommand.Execute(VisualSuite.AltaDensidade);
            Assert.Equal(TemaSuite.Escuro, suite.Tema);
            Assert.StartsWith("10 – Alta densidade", suite.NomeVisual);

            // escolher a moldura direto (Exibir ou versões antigas) acompanha o visual equivalente
            suite.Layout = LayoutSuite.Faixa;
            Assert.Equal(VisualSuite.Faixa, suite.Visual);
            Assert.False(EstilosVisuais.Compacta);

            // 9: os menus ficam no botão Menus
            suite.EscolherVisualCommand.Execute(VisualSuite.Busca);
            Assert.True(suite.EhBusca);
            Assert.False(suite.MenusNaBusca);
            suite.AlternarMenusNaBuscaCommand.Execute(null);
            Assert.True(suite.MenusNaBusca);
            suite.EscolherVisualCommand.Execute(VisualSuite.Classico);
            Assert.False(suite.MenusNaBusca);

            // preferência das versões anteriores (só a moldura gravada): vira o visual equivalente
            SuiteViewModel.ArquivoLayout = ArquivoTemporario();
            File.WriteAllText(SuiteViewModel.ArquivoLayout, "Lateral");
            File.WriteAllText(SuiteViewModel.ArquivoFixa, "fixa");
            Assert.Equal(VisualSuite.BarraLateral, SuiteViewModel.LerVisual());
            File.WriteAllText(SuiteViewModel.ArquivoLayout, "Arvore");
            Assert.Equal(VisualSuite.Paineis, SuiteViewModel.LerVisual());
        }
        finally { suite.Tema = TemaSuite.Claro; EstilosVisuais.Aplicar(false, false); }
    }

    [Fact]
    public async Task Janela_mostra_as_partes_de_cada_visual_e_a_lista_vai_para_a_esquerda()
    {
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            await Task.CompletedTask;
            SuiteViewModel.ArquivoLayout = ArquivoTemporario();
            var suite = new SuiteViewModel(ModulosDaSuite.Criar(Path.GetTempPath()));
            var janela = new MainWindow { Width = 1400, Height = 860 };
            try
            {
                janela.UsarSuite(suite);
                janela.Show();
                Dispatcher.UIThread.RunJobs();
                // com VORTICIS_SCREENSHOTS, uma foto da janela em cada visual
                if (Environment.GetEnvironmentVariable("VORTICIS_SCREENSHOTS") is { } pasta)
                    foreach (var o in SuiteViewModel.Visuais)
                    {
                        suite.EscolherVisualCommand.Execute(o.Valor);
                        Dispatcher.UIThread.RunJobs();
                        janela.CaptureRenderedFrame();
                        Dispatcher.UIThread.RunJobs();
                        janela.CaptureRenderedFrame()?.Save(Path.Combine(pasta, $"visual_{o.Numero:00}.png"));
                    }
                suite.EscolherVisualCommand.Execute(VisualSuite.Classico);
                suite.Tema = TemaSuite.Claro;
                Dispatcher.UIThread.RunJobs();
                var topo = janela.FindDescendantOfType<SuiteTopoView>()!;
                Border B(string nome) => topo.FindControl<Border>(nome)!;

                Assert.True(B("BarraAbas").IsVisible);                                  // 1: abas e menus
                Assert.False(B("BarraComandos").IsVisible);
                suite.EscolherVisualCommand.Execute(VisualSuite.Comandos);
                Dispatcher.UIThread.RunJobs();
                Assert.True(B("BarraComandos").IsVisible);
                Assert.True(B("BarraCaminho").IsVisible);
                Assert.Equal("Nenhuma função aberta", topo.FindControl<TextBlock>("CaminhoFuncao")!.Text);

                suite.EscolherVisualCommand.Execute(VisualSuite.Busca);                 // 9: moldura mínima, menus escondidos
                Dispatcher.UIThread.RunJobs();
                Assert.True(B("BarraBusca").IsVisible);
                Assert.False(B("BarraAbas").IsVisible);
                Assert.False(janela.FindControl<StackPanel>("BarraMenuWrap")!.IsVisible);
                suite.AlternarMenusNaBuscaCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.True(janela.FindControl<StackPanel>("BarraMenuWrap")!.IsVisible);

                // seletor com as 10 miniaturas
                var seletor = new SuiteSeletorView { DataContext = suite };
                Assert.Equal(10, seletor.GetLogicalDescendants().OfType<Image>().Count(i => i.Source is not null));
                seletor.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "Visual4")
                       .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(VisualSuite.ListaFicha, suite.Visual);

                // 4: tela "ficha | lista" já aberta e as que abrirem depois passam a ter a lista à esquerda
                var ficha = new Border { Name = "Ficha" };
                var lista = new Border { Name = "Lista" };
                Grid.SetColumn(lista, 2);
                var grade = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,16,300"), Children = { ficha, lista } };
                var j3 = new Window { Content = grade, Width = 900, Height = 500 };
                j3.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal((2, 0), (Grid.GetColumn(ficha), Grid.GetColumn(lista)));
                Assert.Equal(300, grade.ColumnDefinitions[0].Width.Value);
                suite.EscolherVisualCommand.Execute(VisualSuite.Classico);                // volta ao normal
                Assert.Equal((0, 2), (Grid.GetColumn(ficha), Grid.GetColumn(lista)));
                Assert.True(grade.ColumnDefinitions[0].Width.IsStar);
                j3.Close();

                // Ctrl+K num visual sem busca própria abre a busca central por cima (precisa de login para aparecer)
                suite.BuscaRapidaAberta = true;
                Assert.False(suite.MostraBuscaCentral);
                suite.FecharBuscaRapidaCommand.Execute(null);
                Assert.False(suite.BuscaRapidaAberta);
                return true;
            }
            finally
            {
                EstilosVisuais.Aplicar(false, false);
                // fecha sem a pergunta "Deseja realmente fechar?" (ficaria aberta e travaria os testes seguintes)
                typeof(MainWindow).GetField("_fechamentoConfirmado", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(janela, true);
                janela.Close();
            }
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }
}
