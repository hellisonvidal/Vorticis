using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Desktop.Comum.Views;
using Vorticis.Infra.Tests;
using E = Vorticis.Escritorio.Desktop;

namespace Vorticis.Desktop.Tests;

/// <summary>Escritório no primeiro uso: Dados do escritório (só o escritório, código 0001) e o escritório já como cliente 0001, primeiro da lista.</summary>
public class DadosEscritorioTelaTests(BancoDeTeste db, SessaoHeadless headless) : IClassFixture<BancoDeTeste>
{
    private static async Task<T> ForaDaSessao<T>(Task<T> tarefa)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = tarefa.ContinueWith(t => { if (t.IsFaulted) tcs.SetException(t.Exception!.InnerExceptions); else tcs.SetResult(t.Result); }, TaskScheduler.Default);
        return await tcs.Task;
    }

    [Fact]
    public async Task Primeiro_uso_cadastra_o_escritorio_como_0001_e_ele_ja_e_o_primeiro_cliente()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        await db.App.Perfis.ConcluirConfiguracaoInicialAsync(new Vorticis.Domain.Seguranca.SessaoUsuario(adm, "admin_teste", "Admin", true, false), ct);
        var pasta = Environment.GetEnvironmentVariable("VORTICIS_SCREENSHOTS");
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            async Task Calma(int n = 25) { for (var i = 0; i < n; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); } }
            Vorticis.Suite.Desktop.ModulosDaSuite.IncluirTelas(Avalonia.Application.Current!);   // telas do Escritório
            var vm = new E.MainViewModel();
            var janela = new MainWindow { DataContext = vm, Width = 1366, Height = 820 };
            try
            {
                janela.Show();
                await vm.InicializarAsync(db.ConnectionString);
                var login = Assert.IsType<LoginViewModel>(vm.Conteudo);
                (login.Usuario, login.Senha) = ("admin_teste", "Senha1234");
                await login.EntrarCommand.ExecuteAsync(null);
                for (var i = 0; i < 200 && !vm.Logado; i++) await Calma(1);
                await Calma(20);

                // sem escritório: abre Dados do escritório (só o escritório, sem plano, sócios, contadores e módulos)
                var cad = Assert.IsType<CadastroEmpresaViewModel>(vm.Conteudo);
                Assert.True(cad.ModoEscritorio);
                Assert.False(cad.MostraAbasEmpresa);
                cad.IncluirCommand.Execute(null);
                Assert.Equal("Cadastrar o escritório", cad.TituloFormulario);
                Assert.False(cad.MostraPrimeiroExercicio);
                (cad.Cnpj, cad.RazaoSocial, cad.NomeFantasia, cad.Cnae, cad.MunicipioIbge, cad.Uf) =
                    ("34028316000103", "Contabilidade Exemplo Ltda", "Exemplo Contábil", "6920601", "3550308", "SP");
                await Calma(5);
                var w = janela.JanelaDa(cad)!;
                if (pasta is not null) { w.CaptureRenderedFrame(); Dispatcher.UIThread.RunJobs(); w.CaptureRenderedFrame()?.Save(Path.Combine(pasta, "escritorio_dados_do_escritorio.png")); }
                Assert.DoesNotContain(w.GetLogicalDescendants().OfType<TabItem>(), t => t.IsVisible && t.Header as string is "Sócios" or "Contadores" or "Contabilidade");
                await cad.SalvarCommand.ExecuteAsync(null);
                for (var i = 0; i < 100 && cad.EmInclusao; i++) await Calma(1);
                Assert.True(cad.EmConsulta, string.Join("; ", cad.Erros));
                Assert.Contains("0001", cad.Mensagem);
                await Calma(10);
                Assert.Equal(1L, vm.EmpresaSelecionada?.Id);

                // Cadastros › Clientes: o escritório é o cliente 0001, o primeiro da lista
                vm.AbrirCommand.Execute(E.ViewModels.EscritorioTelas.Clientes);
                await Calma(40);
                var cli = Assert.IsType<E.ViewModels.ClientesEscritorioViewModel>(vm.Conteudo);
                var primeiro = cli.Lista.First();
                Assert.Equal((1, "Contabilidade Exemplo Ltda"), (primeiro.Codigo, primeiro.Nome));
                cli.IncluirCommand.Execute(null);
                Assert.Equal("2", cli.Codigo);                                                          // o próximo cliente é o 0002
                cli.CancelarCommand.Execute(null);
                var wc = janela.JanelaDa(cli)!;
                await Calma(5);
                if (pasta is not null) { wc.CaptureRenderedFrame(); Dispatcher.UIThread.RunJobs(); wc.CaptureRenderedFrame()?.Save(Path.Combine(pasta, "escritorio_clientes_0001.png")); }
                return true;
            }
            finally
            {
                typeof(MainWindow).GetField("_fechamentoConfirmado", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(janela, true);
                janela.Close();
            }
        }, ct));
        Assert.True(ok);
    }
}
