using Avalonia.Headless;
using Avalonia.Threading;
using Perseus.Desktop.ViewModels;
using Perseus.Desktop.Views;

namespace Perseus.Desktop.Tests;

/// <summary>
/// Primeira execução sem nenhuma configuração: o programa cria e inicia o banco local sozinho e vai direto ao
/// primeiro acesso, sem pedir dados de conexão. Só no Linux (pastas do perfil redirecionadas por XDG_*) e fora do root.
/// </summary>
public class PrimeiraExecucaoTests(SessaoHeadless headless)
{
    private static string? Binarios() =>
        Directory.Exists("/usr/lib/postgresql")
            ? Directory.GetDirectories("/usr/lib/postgresql").OrderByDescending(d => d)
                .Select(d => Path.Combine(d, "bin")).FirstOrDefault(d => File.Exists(Path.Combine(d, "initdb")))
            : null;

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
    public async Task Abre_direto_no_primeiro_acesso_com_o_banco_local()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() && Environment.UserName != "root" && Binarios() is not null,
            "Requer Linux, usuário comum e PostgreSQL em /usr/lib/postgresql.");
        Assert.SkipWhen(Environment.GetEnvironmentVariable("PERSEUS_DB") is not null, "PERSEUS_DB definida.");

        var perfil = Path.Combine(Path.GetTempPath(), $"perseus-perfil-{Guid.NewGuid():N}");
        var anteriores = new[] { "XDG_CONFIG_HOME", "XDG_DATA_HOME", "PERSEUS_PG_EMBUTIDO_BIN" }
            .ToDictionary(v => v, Environment.GetEnvironmentVariable);
        // GetFolderPath devolve "" para pastas inexistentes
        Directory.CreateDirectory(Path.Combine(perfil, "config"));
        Directory.CreateDirectory(Path.Combine(perfil, "dados"));
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(perfil, "config"));
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(perfil, "dados"));
        Environment.SetEnvironmentVariable("PERSEUS_PG_EMBUTIDO_BIN", Binarios());
        var pastaFotos = Environment.GetEnvironmentVariable("PERSEUS_SCREENSHOTS");
        try
        {
            Assert.StartsWith(perfil, Configuracao.ArquivoConfiguracao);
            Assert.Equal(ModoBanco.Local, Configuracao.Carregar().Modo);

            var sessao = headless.Sessao;
            var ok = await ForaDaSessao(sessao.Dispatch(async () =>
            {
                // 1. Sem configuração: cria o banco local e abre o primeiro acesso
                var vm = new MainViewModel();
                await vm.IniciarAsync();
                var login = Assert.IsType<LoginViewModel>(vm.Conteudo);
                Assert.True(login.EmPrimeiroAcesso);
                Assert.Contains("banco local do Perseus", vm.Status);
                var cfg = Configuracao.Carregar();
                Assert.Equal(ModoBanco.Local, cfg.Modo);
                Assert.NotNull(cfg.Senha);
                Assert.True(File.Exists(Path.Combine(Configuracao.PastaBancoLocal, "PG_VERSION")));
                await vm.DisposeAsync();

                // 2. Configuração apontando para um servidor inexistente: tela de conexão oferece o banco local
                Configuracao.Salvar("Host=localhost;Port=1;Database=perseus;Username=x;Password=y;Timeout=2");
                vm = new MainViewModel();
                await vm.IniciarAsync();
                var conexao = Assert.IsType<ConexaoViewModel>(vm.Conteudo);
                Assert.True(conexao.BancoLocalDisponivel);
                Assert.False(conexao.UsandoBancoLocal);
                var janela = new MainWindow { DataContext = vm, Width = 1280, Height = 780 };
                janela.Show();
                Dispatcher.UIThread.RunJobs();
                var quadro = janela.CaptureRenderedFrame();
                if (pastaFotos is not null) quadro?.Save(Path.Combine(pastaFotos, "00_conexao_banco_local.png"));

                // 3. "Usar o banco local": volta ao mesmo banco (mesma senha), sem recriar
                await conexao.UsarBancoLocalCommand.ExecuteAsync(null);
                Assert.IsType<LoginViewModel>(vm.Conteudo);
                Assert.Equal(cfg.Senha, Configuracao.Carregar().Senha);
                janela.Close();
                await vm.DisposeAsync();
                return true;
            }, TestContext.Current.CancellationToken));
            Assert.True(ok);
        }
        finally
        {
            BancoDoAplicativo.Encerrar();
            foreach (var (v, valor) in anteriores) Environment.SetEnvironmentVariable(v, valor);
            try { Directory.Delete(perfil, recursive: true); } catch (IOException) { }
        }
    }
}
