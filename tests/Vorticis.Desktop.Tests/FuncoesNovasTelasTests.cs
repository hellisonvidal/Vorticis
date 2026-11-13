using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Vorticis.Desktop.Comum;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Desktop.Comum.Views;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra.Tests;

namespace Vorticis.Desktop.Tests;

/// <summary>
/// Funções de todos os módulos (casco): Histórico de alterações, Backup automático, Bloqueio por inatividade, Verificar atualizações e
/// Exportar a lista abrem com o administrador logado. Com VORTICIS_SCREENSHOTS, grava a foto de cada uma.
/// </summary>
public class FuncoesNovasTelasTests(BancoDeTeste db, SessaoHeadless headless) : IClassFixture<BancoDeTeste>
{
    private static async Task<T> ForaDaSessao<T>(Task<T> tarefa)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = tarefa.ContinueWith(t => { if (t.IsFaulted) tcs.SetException(t.Exception!.InnerExceptions); else tcs.SetResult(t.Result); }, TaskScheduler.Default);
        return await tcs.Task;
    }

    [Fact]
    public async Task Telas_das_funcoes_novas_abrem_com_o_administrador()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        await db.App.Perfis.ConcluirConfiguracaoInicialAsync(new SessaoUsuario(adm, "admin_teste", "Admin", true, false), ct);
        var empresa = await db.CriarEmpresaAsync("33000167000101");
        await db.EscalarAsync<int>("UPDATE core.empresa SET nome_fantasia = 'Loja Centro' WHERE id = @e RETURNING 1", ("e", empresa));
        var pasta = Environment.GetEnvironmentVariable("VORTICIS_SCREENSHOTS");
        if (pasta is not null) Directory.CreateDirectory(pasta);
        var (site, handler) = (Atualizacoes.Site, Atualizacoes.Handler);

        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            async Task Calma(int n = 25) { for (var i = 0; i < n; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); } }
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
                Assert.True(vm.Logado);
                vm.EmpresaSelecionada = vm.EmpresasPermitidas.First(e => e.Id == empresa);
                await Calma(30);

                void Foto(Window w, string nome)
                {
                    if (pasta is null) return;
                    w.CaptureRenderedFrame();
                    Dispatcher.UIThread.RunJobs();
                    w.CaptureRenderedFrame()?.Save(Path.Combine(pasta, nome));
                }
                async Task<Window> Abrir(string tela)
                {
                    Assert.True(vm.PodeAbrir(tela), tela);
                    vm.AbrirCommand.Execute(tela);
                    await Calma(40);
                    var f = vm.JanelasAbertas.First(j => ReferenceEquals(j.ViewModel, vm.Conteudo));
                    var w = janela.JanelaDa(f.ViewModel)!;
                    w.Width = 1200; w.Height = 700;
                    await Calma(5);
                    return w;
                }

                // Histórico de alterações: a inclusão e a alteração da empresa aparecem; o que mudou, campo a campo
                var wa = await Abrir(TelasComuns.Auditoria);
                var aud = Assert.IsType<AuditoriaViewModel>(vm.Conteudo);
                Assert.Contains(aud.Lista, l => l.Cadastro == "Empresas" && l.Operacao == "Alteração");
                aud.Selecionado = aud.Lista.First(l => l.Cadastro == "Empresas" && l.Operacao == "Alteração");
                await aud.VerificarCommand.ExecuteAsync(null);
                Assert.StartsWith("Trilha íntegra", aud.Integridade);
                await Calma(5);
                Foto(wa, "nova_historico_de_alteracoes.png");

                // Exportar a lista: o botão está na barra de busca da lista do histórico
                var grade = wa.GetLogicalDescendants().OfType<DataGrid>().First(g => g.Name == "ListaAuditoria");
                var barra = FiltroGrade.ObterBarra(grade)!;
                var botao = barra.GetLogicalDescendants().OfType<Button>().First(b => b.Name == "BotaoExportarLista");
                botao.Flyout!.ShowAt(botao);
                await Calma(5);
                Foto(wa, "nova_exportar_lista.png");
                botao.Flyout.Hide();

                // Backup e restauração: quadro do backup automático
                var wb = await Abrir(TelasComuns.Backup);
                var bkp = Assert.IsType<BackupViewModel>(vm.Conteudo);
                Assert.True(bkp.AutomaticoLigado);
                Assert.Equal(7m, bkp.AutomaticoManter);
                Foto(wb, "nova_backup_automatico.png");

                // Preferências: minutos do bloqueio por inatividade
                var wp = await Abrir(TelasComuns.Preferencias);
                Assert.NotNull(wp.GetLogicalDescendants().OfType<NumericUpDown>().FirstOrDefault(n => n.Name == "MinutosBloqueio"));
                Foto(wp, "nova_preferencias_bloqueio.png");

                // Verificar atualizações (site de teste)
                (Atualizacoes.Site, Atualizacoes.Handler) = (() => "https://vorticis.exemplo", new SiteDeTeste());
                var wu = await Abrir(TelasComuns.Atualizacoes);
                await Calma(10);
                Assert.True(Assert.IsType<AtualizacoesViewModel>(vm.Conteudo).TemNova);
                Foto(wu, "nova_verificar_atualizacoes.png");

                // Tela de bloqueio por cima de tudo
                BloqueioViewModel.Atual.Bloquear(vm.Sessao!.Nome);
                await Calma(5);
                Assert.True(janela.FindControl<Border>("TelaBloqueio")!.IsVisible);
                Assert.False(wu.IsVisible);                                                  // as janelas das funções somem
                Foto(janela, "nova_tela_bloqueada.png");
                BloqueioViewModel.Atual.SairCommand.Execute(null);
                await Calma(5);
                return true;
            }
            finally
            {
                (Atualizacoes.Site, Atualizacoes.Handler) = (site, handler);
                typeof(MainWindow).GetField("_fechamentoConfirmado", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(janela, true);
                janela.Close();
            }
        }, ct));
        Assert.True(ok);
    }

    private sealed class SiteDeTeste : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("""{"versao":"9.9.0","publicada":"10/11/2026","instalador":"https://vorticis.exemplo/downloads/Setup-Vorticis-9.9.0.exe","notas":"- Histórico de alterações\n- Backup automático diário\n- Bloqueio por inatividade\n- Exportar qualquer lista"}""",
                                        System.Text.Encoding.UTF8, "application/json"),
        });
    }
}
