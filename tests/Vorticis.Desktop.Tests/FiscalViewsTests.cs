using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Vorticis.Application.Relatorios;
using Vorticis.Fiscal.Desktop.ViewModels;
using Vorticis.Fiscal.Desktop.Views;

namespace Vorticis.Desktop.Tests;

/// <summary>As telas do módulo Fiscal carregam (XAML com bindings compilados), recebem o ViewModel e renderizam sem exceção.</summary>
public class FiscalViewsTests(SessaoHeadless headless)
{
    private static async Task<T> ForaDaSessao<T>(Task<T> tarefa)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = tarefa.ContinueWith(t =>
        {
            if (t.IsFaulted) tcs.SetException(t.Exception!.InnerExceptions);
            else if (t.IsCanceled) tcs.SetCanceled();
            else tcs.SetResult(t.Result);
        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        return await tcs.Task;
    }

    [Fact]
    public async Task Todas_as_telas_fiscais_renderizam()
    {
        var concluido = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            var ctx = new Vorticis.Fiscal.Desktop.MainViewModel();
            var pares = new (object Vm, UserControl View)[]
            {
                (new ImportacaoXmlViewModel(ctx), new ImportacaoXmlView()),
                (new ImportacaoNfseViewModel(ctx), new ImportacaoXmlView()),
                (new ImportacaoCteViewModel(ctx), new ImportacaoXmlView()),
                (new ConsultaCteViewModel(ctx), new ConsultaCteView()),
                (new FechamentoFiscalViewModel(ctx), new FechamentoFiscalView()),
                (new AjustesEntradaViewModel(ctx), new AjustesIcmsView()),
                (new AjustesSaidaViewModel(ctx), new AjustesIcmsView()),
                (new CodigosAjusteViewModel(ctx), new CodigosAjusteView()),
                (new ContasConsumoViewModel(ctx), new ContasConsumoView()),
                (new InventarioViewModel(ctx), new InventarioView()),
                (new DocumentosViewModel(ctx), new DocumentosView()),
                (new LivroEntradasViewModel(ctx), new LivroFiscalView()),
                (new LivroSaidasViewModel(ctx), new LivroFiscalView()),
                (new LivroApuracaoIcmsViewModel(ctx), new LivroApuracaoView()),
                (new LivroApuracaoIpiViewModel(ctx), new LivroApuracaoView()),
                (new ResumoCfopViewModel(ctx), new ResumoCfopView()),
                (new NotaEntradaViewModel(ctx), new DigitacaoDocumentoView()),
                (new NotaSaidaViewModel(ctx), new DigitacaoDocumentoView()),
                (new ProdutosViewModel(ctx), new ProdutosView()),
                (new RelatorioProdutosViewModel(ctx), new RelatorioProdutosView()),
                (new RelatorioItensNotasViewModel(ctx), new RelatorioItensNotasView()),
                (new RegrasContabilizacaoViewModel(ctx), new RegrasContabilizacaoView()),
                (new ContabilizarNotasViewModel(ctx), new ContabilizarNotasView()),
                (new ApuracaoSimplesViewModel(ctx), new ApuracaoSimplesView()),
                (new BaseMensalViewModel(ctx), new BaseMensalView()),
                (new ApuracaoPresumidoViewModel(ctx), new ApuracaoPresumidoView()),
                (new ApuracaoRealViewModel(ctx), new ApuracaoRealView()),
                (new ApuracaoPisCofinsViewModel(ctx), new ApuracaoPisCofinsView()),
                (new PlanejamentoTributarioViewModel(ctx), new PlanejamentoTributarioView()),
                (new RetencoesViewModel(ctx), new RetencoesView()),
                (new GuiasViewModel(ctx), new GuiasView()),
                (new DctfWebViewModel(ctx), new DctfWebView()),
                (new ApuracaoIbsCbsViewModel(ctx), new ApuracaoIbsCbsView()),
                (new SimplesReformaViewModel(ctx), new SimplesReformaView()),
                (new OpcaoIbsCbsViewModel(ctx), new OpcaoIbsCbsView()),
                (new ConferenciaPgdasViewModel(ctx), new ConferenciaPgdasView()),
                (new NotasPorCentroViewModel(ctx), new NotasPorCentroView()),
            };
            foreach (var (vm, view) in pares)
            {
                view.DataContext = vm;
                var janela = new Window { Content = view, Width = 1280, Height = 760 };
                janela.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.NotNull(janela.CaptureRenderedFrame());
                janela.Close();
            }

            // Grade montada em código para as tabelas dos relatórios
            var tabela = new TabelaRelatorio("Teste", [new("Nome"), new("Valor", TipoColuna.Valor)],
                [new LinhaRelatorio(["A", 10m]), new LinhaRelatorio(["TOTAL", 10m], true)], "rodapé");
            var exibida = new TabelaExibidaView { DataContext = new TabelaExibida(tabela) };
            var jt = new Window { Content = exibida, Width = 600, Height = 300 };
            jt.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(jt.CaptureRenderedFrame());
            jt.Close();
            await Task.CompletedTask;
            return true;
        }, TestContext.Current.CancellationToken));
        Assert.True(concluido);
    }

    [Fact]
    public async Task Opcao_IBS_CBS_do_Simples_abre_a_ficha_em_janela_com_as_duas_opcoes()
    {
        var concluido = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            var vm = new OpcaoIbsCbsViewModel(new Vorticis.Fiscal.Desktop.MainViewModel());
            Assert.Equal(["Dentro do DAS", "Regime regular (IBS/CBS fora do DAS)"], OpcaoIbsCbsViewModel.Opcoes);
            Assert.Equal(1, vm.Vigencia!.Value.Day);
            Assert.Contains(vm.Vigencia.Value.Month, new[] { 1, 7 });   // sempre o início de um semestre
            vm.AbrirFichaCommand.Execute(null);                          // sem seleção: avisa e não abre
            Assert.False(vm.FichaAberta);
            Assert.Contains("Selecione", vm.Erros.Single());
            vm.NovaCommand.Execute(null);
            Assert.True(vm.FichaAberta);
            Assert.Equal("Nova opção do IBS/CBS", vm.TituloFicha);
            vm.FecharFicha();
            Assert.False(vm.FichaAberta);
            await Task.CompletedTask;
            return true;
        }, TestContext.Current.CancellationToken));
        Assert.True(concluido);
    }
}
