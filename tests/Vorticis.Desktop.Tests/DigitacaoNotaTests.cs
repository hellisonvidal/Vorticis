using Avalonia.Controls;
using Avalonia.VisualTree;
using Vorticis.Application.Cadastros;
using Vorticis.Application.Seguranca;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Desktop.ViewModels;
using Vorticis.Fiscal.Desktop.Views;
using Vorticis.Infra;
using Vorticis.Infra.Tests;

namespace Vorticis.Desktop.Tests;

/// <summary>Contexto mínimo (administrador, empresa ativa) para exercitar uma tela isolada com o banco real.</summary>
internal sealed class ContextoTeste(VorticisApp app, long usuario, long empresa) : IContexto
{
    public VorticisApp? App => app;
    public SessaoUsuario? Sessao { get; } = new(usuario, "admin_teste", "Admin", true, false);
    public long? EmpresaId => empresa;
    public bool Pode(Permissao permissao) => true;
    public Task RecarregarEmpresasAsync(long? selecionar = null) => Task.CompletedTask;
    public void AoConcluirConfiguracaoInicial() { }
    public Task<bool> ConfirmarAsync(string titulo, string mensagem, string textoSim = "Sim", string textoNao = "Não") => Task.FromResult(true);
    public Task ReiniciarParaLoginAsync(string mensagem) => Task.CompletedTask;
    public Task<ModulosEmpresa?> EscolherModulosAsync(ModulosEmpresa inicial) => Task.FromResult<ModulosEmpresa?>(inicial);
    public ModulosEmpresa ModuloAtual => ModulosEmpresa.Fiscal;
    public Task<string?> SolicitarSenhaAsync(string titulo, string mensagem, string textoSim = "Confirmar", string textoNao = "Cancelar") => Task.FromResult<string?>(null);
    public string DescricaoEmpresa => "Empresa de teste";
    public void AbrirTela(string tela) { }
    public Task ConcluirImplantacaoAsync(string loginAdministrador) => Task.CompletedTask;
}

/// <summary>Lançamentos › Documentos fiscais › Entrada: numa nota nova, os campos do cabeçalho e do item ficam editáveis.</summary>
public class DigitacaoNotaTests(BancoDeTeste db, SessaoHeadless headless) : IClassFixture<BancoDeTeste>
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
    public async Task Nota_nova_tem_os_campos_editaveis()
    {
        BancoDeTeste.ExigirBanco();
        var adm = await db.AdminAsync();
        var empresa = await db.CriarEmpresaAsync("11444777000161");
        var r = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            var vm = new NotaEntradaViewModel(new ContextoTeste(db.App, adm, empresa));
            var view = new DigitacaoDocumentoView { DataContext = vm };
            var janela = new Window { Content = view, Width = 1366, Height = 768 };
            janela.Show();
            await vm.CarregarAsync();
            vm.NovoCommand.Execute(null);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var bloqueados = view.GetVisualDescendants().OfType<TextBox>().Where(t => t.IsEffectivelyVisible && !t.IsEffectivelyEnabled)
                                 .Select(t => t.Watermark ?? t.Name ?? t.Text ?? "?").ToList();
            bloqueados.InsertRange(0, vm.Erros);
            bloqueados.Insert(0, $"PodeEditar={vm.PodeEditar}");
            janela.Close();
            return bloqueados;
        }, TestContext.Current.CancellationToken));
        Assert.Equal(["PodeEditar=True"], r);
    }
}
