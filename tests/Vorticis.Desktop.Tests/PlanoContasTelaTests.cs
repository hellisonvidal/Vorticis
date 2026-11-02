using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vorticis.Application.Relatorios;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Desktop.Comum.Views;
using Vorticis.Desktop.ViewModels;
using Vorticis.Desktop.Views;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra.Tests;

namespace Vorticis.Desktop.Tests;

/// <summary>Contábil › Plano de contas: a tela mostra as contas; a ficha da conta e o "Importar contas do Excel" abrem em janela por cima.</summary>
public class PlanoContasTelaTests(BancoDeTeste db, SessaoHeadless headless) : IClassFixture<BancoDeTeste>
{
    private static async Task<T> ForaDaSessao<T>(Task<T> tarefa)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = tarefa.ContinueWith(t => { if (t.IsFaulted) tcs.SetException(t.Exception!.InnerExceptions); else tcs.SetResult(t.Result); }, TaskScheduler.Default);
        return await tcs.Task;
    }

    [Fact]
    public async Task Lista_de_contas_ficha_em_janela_e_importacao_so_das_contas_marcadas()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        await db.App.Perfis.ConcluirConfiguracaoInicialAsync(new SessaoUsuario(adm, "admin_teste", "Admin", true, false), ct);
        var empresa = await db.CriarEmpresaAsync("11444777000161");
        var grupo = (await db.App.PlanoContas.ListarAsync(empresa, adm, ct)).First(c => !c.Analitica && c.Codigo.Count(ch => ch == '.') == 2 && c.Codigo.StartsWith("1."));
        // planilha com uma conta que já existe (o grupo) e duas novas
        var planilha = Path.Combine(Directory.CreateTempSubdirectory("plano").FullName, "contas.xlsx");
        File.WriteAllBytes(planilha, Vorticis.Infra.Relatorios.ExportadorExcel.Gerar([new TabelaRelatorio("Contas",
            [new ColunaRelatorio("Código"), new ColunaRelatorio("Descrição")],
            [new LinhaRelatorio([grupo.Codigo, grupo.Descricao]), new LinhaRelatorio([grupo.Codigo + ".97", "Aplicações novas"]),
             new LinhaRelatorio([grupo.Codigo + ".97.001", "CDB Banco X"])])]));
        var pasta = Environment.GetEnvironmentVariable("VORTICIS_SCREENSHOTS");

        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            async Task Calma(int n = 25) { for (var i = 0; i < n; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); } }
            void Foto(Window? w, string nome)
            {
                if (pasta is null || w is null) return;
                w.CaptureRenderedFrame();
                Dispatcher.UIThread.RunJobs();
                w.CaptureRenderedFrame()?.Save(Path.Combine(pasta, nome));
            }
            var vm = new Vorticis.Desktop.ViewModels.MainViewModel();
            var janela = new MainWindow { DataContext = vm, Width = 1366, Height = 820 };
            try
            {
                janela.Show();
                await vm.InicializarAsync(db.ConnectionString);
                var login = Assert.IsType<LoginViewModel>(vm.Conteudo);
                (login.Usuario, login.Senha) = ("admin_teste", "Senha1234");
                await login.EntrarCommand.ExecuteAsync(null);
                for (var i = 0; i < 200 && !vm.Logado; i++) await Calma(1);
                vm.EmpresaSelecionada = vm.EmpresasPermitidas.First(e => e.Id == empresa);
                await Calma(30);

                vm.AbrirCommand.Execute(Telas.PlanoContas);
                await Calma(40);
                var plano = Assert.IsType<PlanoContasViewModel>(vm.Conteudo);
                var w = janela.JanelaDa(plano)!;
                w.Width = 1200; w.Height = 720;
                await Calma(5);
                var tela = w.GetVisualDescendants().OfType<PlanoContasView>().Single();
                Assert.True(plano.Lista.Count > 20);
                Assert.Null(FichaPopup.Aberta(tela));
                Foto(w, "plano_de_contas_lista.png");

                // Incluir abre a ficha da conta em janela
                plano.Selecionada = plano.Lista.First(l => l.Conta.Codigo == grupo.Codigo);
                plano.IncluirCommand.Execute(null);
                await Calma(10);
                var ficha = FichaPopup.Aberta(tela);
                Assert.IsType<PlanoContaFichaView>(ficha?.Content);
                Assert.StartsWith(grupo.Codigo + ".", plano.Codigo);                                     // sugere a subconta
                Foto(ficha, "plano_de_contas_ficha.png");
                plano.CancelarCommand.Execute(null);
                await Calma(5);
                Assert.Null(FichaPopup.Aberta(tela));

                // Importar contas do Excel: a que já existe vem desmarcada; acrescenta só as marcadas
                plano.Importar.AbrirCommand.Execute(null);
                await Calma(5);
                var host = w.GetVisualDescendants().OfType<ImportarContasHost>().Single();
                Assert.IsType<ImportarContasView>(FichaPopup.Aberta(host)?.Content);
                plano.Importar.Arquivo = planilha;
                for (var i = 0; i < 100 && plano.Importar.Contas.Count < 3; i++) await Calma(1);
                Assert.Equal([false, true, true], plano.Importar.Contas.Select(c => c.Marcada));
                Assert.Equal("Já está no plano", plano.Importar.Contas[0].Situacao);
                Foto(FichaPopup.Aberta(host), "plano_de_contas_importar_excel.png");
                await plano.Importar.ImportarCommand.ExecuteAsync(null);
                await Calma(20);
                Assert.StartsWith("2 conta(s) acrescentada(s)", plano.Importar.Mensagem);
                Assert.Contains(plano.Lista, l => l.Conta.Codigo == grupo.Codigo + ".97.001");
                plano.Importar.FecharFichaCommand.Execute(null);
                await Calma(5);
                Assert.Null(FichaPopup.Aberta(host));
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
