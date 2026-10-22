using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Vorticis.Folha.Desktop;
using Vorticis.Folha.Desktop.Views;

namespace Vorticis.Desktop.Tests;

/// <summary>Todas as telas do módulo Folha carregam o XAML, ligam os bindings compilados ao ViewModel e renderizam (sem banco).</summary>
public class FolhaTelasTests(SessaoHeadless headless)
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
    public async Task Cada_tela_da_folha_renderiza_com_o_seu_viewmodel()
    {
        var total = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            var vm = new MainViewModel();
            (UserControl View, object Contexto)[] telas =
            [
                (new FuncionariosView(), vm.Funcionarios), (new CargosView(), vm.Cargos), (new DepartamentosView(), vm.Departamentos),
                (new RubricasView(), vm.Rubricas), (new ParametrosFolhaView(), vm.Parametros), (new EventosVariaveisView(), vm.Eventos),
                (new FolhaMensalView(), vm.FolhaMensal), (new FeriasView(), vm.Ferias), (new DecimoTerceiroView(), vm.Decimo),
                (new RescisoesView(), vm.Rescisoes), (new ContabilizacaoFolhaView(), vm.Contabilizacao), (new ESocialView(), vm.ESocial),
                (new ResumoFolhaView(), vm.Resumo), (new EncargosFolhaView(), vm.Encargos), (new ProvisoesFolhaView(), vm.Provisoes),
            ];
            foreach (var (view, contexto) in telas)
            {
                view.DataContext = contexto;
                var janela = new Window { Content = view, Width = 1200, Height = 800 };
                janela.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.NotNull(janela.CaptureRenderedFrame());
                janela.Close();
            }
            await Task.Yield();
            return telas.Length;
        }, TestContext.Current.CancellationToken));
        Assert.Equal(15, total);
    }
}
