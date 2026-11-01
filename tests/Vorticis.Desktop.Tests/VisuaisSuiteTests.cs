using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Desktop.Comum.Views;
using Vorticis.Suite.Desktop;

namespace Vorticis.Desktop.Tests;

/// <summary>Os cinco visuais da Suite: moldura de cada um, preferência gravada (inclusive dos visuais retirados) e as partes na janela.</summary>
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
    public void Cada_visual_liga_a_sua_moldura_e_fica_gravado()
    {
        SuiteViewModel.ArquivoLayout = ArquivoTemporario();
        var suite = new SuiteViewModel(ModulosDaSuite.Criar(Path.GetTempPath()));
        try
        {
            Assert.Equal(5, SuiteViewModel.Visuais.Count);
            Assert.Equal(Enumerable.Range(1, 5), SuiteViewModel.Visuais.Select(v => v.Numero));
            Assert.Equal(Enum.GetValues<VisualSuite>(), SuiteViewModel.Visuais.Select(v => v.Valor));

            var esperado = new Dictionary<VisualSuite, LayoutSuite>
            {
                [VisualSuite.Classico] = LayoutSuite.Abas,
                [VisualSuite.BarraLateral] = LayoutSuite.Lateral,
                [VisualSuite.Faixa] = LayoutSuite.Faixa,
                [VisualSuite.Mosaico] = LayoutSuite.Painel,
                [VisualSuite.Busca] = LayoutSuite.Busca,
            };
            foreach (var (visual, layout) in esperado)
            {
                suite.EscolherVisualCommand.Execute(visual);
                Assert.Equal(layout, suite.Layout);
                Assert.Equal(visual, SuiteViewModel.LerVisual());                                       // gravado para a próxima abertura
                Assert.Equal(visual, new SuiteViewModel(ModulosDaSuite.Criar(Path.GetTempPath())).Visual);
            }

            suite.EscolherVisualCommand.Execute(VisualSuite.BarraLateral);                              // 2 deixa o menu da esquerda fixo
            Assert.True(suite.MostraNavFixa);
            Assert.StartsWith("5 – Busca primeiro", SuiteViewModel.Visuais[4] is var b ? $"{b.Numero} – {b.Nome}" : "");

            // escolher a moldura direto (Exibir ou versões antigas) acompanha o visual equivalente
            suite.Layout = LayoutSuite.Faixa;
            Assert.Equal(VisualSuite.Faixa, suite.Visual);

            // 5: os menus ficam no botão Menus
            suite.EscolherVisualCommand.Execute(VisualSuite.Busca);
            Assert.True(suite.EhBusca);
            Assert.False(suite.MenusNaBusca);
            suite.AlternarMenusNaBuscaCommand.Execute(null);
            Assert.True(suite.MenusNaBusca);
            suite.EscolherVisualCommand.Execute(VisualSuite.Classico);
            Assert.False(suite.MenusNaBusca);

            // visuais retirados da 1.0.1 (gravados pelo nome ou pelo número) viram o mais parecido
            foreach (var (nome, numero, vira) in new[] { ("Comandos", 2, VisualSuite.Classico), ("ListaFicha", 3, VisualSuite.Classico),
                                                         ("Trilho", 6, VisualSuite.BarraLateral), ("Paineis", 7, VisualSuite.BarraLateral),
                                                         ("AltaDensidade", 9, VisualSuite.Classico) })
            {
                File.WriteAllText(SuiteViewModel.ArquivoVisual, nome);
                Assert.Equal(vira, SuiteViewModel.LerVisual());
                Assert.Equal(vira, SuiteViewModel.Normalizar(numero));
            }

            // preferência das versões anteriores (só a moldura gravada): vira o visual equivalente
            SuiteViewModel.ArquivoLayout = ArquivoTemporario();
            File.WriteAllText(SuiteViewModel.ArquivoLayout, "Lateral");
            Assert.Equal(VisualSuite.BarraLateral, SuiteViewModel.LerVisual());
            File.WriteAllText(SuiteViewModel.ArquivoLayout, "Arvore");
            Assert.Equal(VisualSuite.BarraLateral, SuiteViewModel.LerVisual());
        }
        finally { suite.Tema = TemaSuite.Claro; }
    }

    [Fact]
    public async Task Janela_mostra_as_partes_de_cada_visual()
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
                Assert.Null(topo.FindControl<Border>("BarraComandos"));                 // a barra de comandos saiu com os visuais retirados
                suite.EscolherVisualCommand.Execute(VisualSuite.Busca);                 // 5: moldura mínima, menus escondidos
                Dispatcher.UIThread.RunJobs();
                Assert.True(B("BarraBusca").IsVisible);
                Assert.False(B("BarraAbas").IsVisible);
                Assert.False(janela.FindControl<StackPanel>("BarraMenuWrap")!.IsVisible);
                suite.AlternarMenusNaBuscaCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.True(janela.FindControl<StackPanel>("BarraMenuWrap")!.IsVisible);

                // seletor com as 5 miniaturas
                var seletor = new SuiteSeletorView { DataContext = suite };
                Assert.Equal(5, seletor.GetLogicalDescendants().OfType<Image>().Count(i => i.Source is not null));
                seletor.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "Visual4")
                       .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(VisualSuite.Mosaico, suite.Visual);

                // Ctrl+K num visual sem busca própria abre a busca central por cima (precisa de login para aparecer)
                suite.BuscaRapidaAberta = true;
                Assert.False(suite.MostraBuscaCentral);
                suite.FecharBuscaRapidaCommand.Execute(null);
                Assert.False(suite.BuscaRapidaAberta);
                return true;
            }
            finally
            {
                // fecha sem a pergunta "Deseja realmente fechar?" (ficaria aberta e travaria os testes seguintes)
                typeof(MainWindow).GetField("_fechamentoConfirmado", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(janela, true);
                janela.Close();
            }
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }
}
