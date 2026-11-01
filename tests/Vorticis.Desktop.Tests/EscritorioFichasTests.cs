using Avalonia.Controls;
using Avalonia.Threading;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Desktop.Comum.Views;
using E = Vorticis.Escritorio.Desktop;

namespace Vorticis.Desktop.Tests;

/// <summary>Escritório: Clientes, Tarefas e Protocolos mostram a lista na tela e a ficha abre numa janela por cima.</summary>
public class EscritorioFichasTests(SessaoHeadless headless)
{
    private static async Task<T> ForaDaSessao<T>(Task<T> tarefa)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = tarefa.ContinueWith(t => { if (t.IsFaulted) tcs.SetException(t.Exception!.InnerExceptions); else tcs.SetResult(t.Result); }, TaskScheduler.Default);
        return await tcs.Task;
    }

    [Fact]
    public async Task Ficha_abre_em_janela_e_fecha_ao_salvar_cancelar_ou_pelo_X()
    {
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            await Task.CompletedTask;
            var main = new E.MainViewModel();

            // Clientes: Incluir abre a ficha; Cancelar fecha; a tela só tem a lista
            var clientes = new E.ViewModels.ClientesEscritorioViewModel(main);
            var telaClientes = new E.Views.ClientesView { DataContext = clientes };
            var j1 = new Window { Content = telaClientes, Width = 1200, Height = 800 };
            j1.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.Null(FichaPopup.Aberta(telaClientes));
            Assert.Null(telaClientes.FindControl<TabControl>("AbasCliente"));                 // a ficha não fica mais na tela
            clientes.Modo = ModoCadastro.Inclusao;
            Dispatcher.UIThread.RunJobs();
            var ficha = FichaPopup.Aberta(telaClientes);
            Assert.NotNull(ficha);
            Assert.IsType<E.Views.ClienteFichaView>(ficha.Content);
            Assert.Equal("Incluir cliente", ficha.Title);
            Assert.True(ficha.IsVisible);
            clientes.CancelarCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Null(FichaPopup.Aberta(telaClientes));
            Assert.False(clientes.FichaAberta);

            // fechar a janela pelo X durante a edição desiste da edição
            clientes.Modo = ModoCadastro.Inclusao;
            Dispatcher.UIThread.RunJobs();
            FichaPopup.Aberta(telaClientes)!.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(ModoCadastro.Consulta, clientes.Modo);
            Assert.False(clientes.FichaAberta);
            j1.Close();

            // Tarefas: Nova tarefa abre a ficha; Fechar fecha; Abrir exige uma tarefa selecionada
            var tarefas = new E.ViewModels.TarefasViewModel(main);
            var telaTarefas = new E.Views.TarefasView { DataContext = tarefas };
            var j2 = new Window { Content = telaTarefas, Width = 1200, Height = 800 };
            j2.Show();
            tarefas.AbrirCommand.Execute(null);
            Assert.False(tarefas.FichaAberta);
            tarefas.NovaCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Nova tarefa", FichaPopup.Aberta(telaTarefas)?.Title);
            Assert.IsType<E.Views.TarefaFichaView>(FichaPopup.Aberta(telaTarefas)!.Content);
            tarefas.FecharFichaCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Null(FichaPopup.Aberta(telaTarefas));
            j2.Close();

            // Protocolos: Registrar protocolo abre a ficha; o X fecha
            var protocolos = new E.ViewModels.ProtocolosViewModel(main);
            var telaProt = new E.Views.ProtocolosView { DataContext = protocolos };
            var j3 = new Window { Content = telaProt, Width = 1200, Height = 800 };
            j3.Show();
            protocolos.NovoCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.IsType<E.Views.ProtocoloFichaView>(FichaPopup.Aberta(telaProt)?.Content);
            FichaPopup.Aberta(telaProt)!.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.False(protocolos.FichaAberta);

            // ficha aberta quando a tela sai da janela: a janela da ficha fecha junto
            protocolos.NovoCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            var aberta = FichaPopup.Aberta(telaProt)!;
            j3.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.False(aberta.IsVisible);
            return true;
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }
}
