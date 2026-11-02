using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Vorticis.Desktop.Comum;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Desktop.Tests;

/// <summary>Bloqueio por inatividade: depois dos minutos sem uso pede a senha; senha certa volta, errada não; 0 desliga.</summary>
public class BloqueioInatividadeTests(SessaoHeadless headless)
{
    [Fact]
    public async Task Bloqueia_depois_do_tempo_sem_uso_e_so_volta_com_a_senha()
    {
        var b = new BloqueioViewModel();
        var agora = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
        b.Agora = () => agora;
        b.Registrar();
        b.Verificar = s => Task.FromResult(s == "Certa123");

        agora = agora.AddMinutes(14);
        Assert.False(b.Conferir(15, "Maria"));
        Assert.False(b.Conferir(0, "Maria"));                       // 0 = desligado
        agora = agora.AddMinutes(2);
        Assert.False(b.Conferir(15, null));                         // sem sessão (tela de login) não bloqueia
        Assert.True(b.Conferir(15, "Maria"));
        Assert.True(b.Ativo);
        Assert.Equal("Maria", b.Usuario);

        b.Registrar();                                              // mexer no mouse com a tela bloqueada não desbloqueia
        b.Senha = "errada";
        await b.DesbloquearCommand.ExecuteAsync(null);
        Assert.True(b.Ativo);
        Assert.StartsWith("Senha incorreta", b.Erro);
        Assert.Equal("", b.Senha);
        b.Senha = "Certa123";
        await b.DesbloquearCommand.ExecuteAsync(null);
        Assert.False(b.Ativo);
        Assert.False(b.Conferir(15, "Maria"));                      // o tempo recomeça

        var saiu = false;
        b.AoSair = () => saiu = true;
        b.Bloquear("Maria");
        b.SairCommand.Execute(null);
        Assert.True(saiu);
        Assert.False(b.Ativo);
    }

    [Fact]
    public void Preferencia_de_minutos_e_gravada_e_limitada()
    {
        var anterior = PreferenciasGerais.Arquivo;
        PreferenciasGerais.Arquivo = Path.Combine(Directory.CreateTempSubdirectory("pref").FullName, "preferencias.json");
        try
        {
            Assert.Equal(15, PreferenciasGerais.Atual.MinutosBloqueio);                    // padrão
            File.WriteAllText(PreferenciasGerais.Arquivo, """{"ConfirmarAoFechar":false,"DiasVencimentos":20}""");   // arquivo de versão anterior
            Assert.Equal(15, PreferenciasGerais.Atual.MinutosBloqueio);
            PreferenciasGerais.Gravar(new Preferencias(true, 15, 999));
            Assert.Equal(Preferencias.BloqueioMaximo, PreferenciasGerais.Atual.MinutosBloqueio);
            PreferenciasGerais.Gravar(new Preferencias(true, 15, 0));
            Assert.Equal(0, PreferenciasGerais.Atual.MinutosBloqueio);
        }
        finally { PreferenciasGerais.Arquivo = anterior; }
    }

    [Fact]
    public async Task Janela_mostra_a_tela_de_bloqueio_por_cima_de_tudo()
    {
        var ok = await Task.Run(() => headless.Sessao.Dispatch(() =>
        {
            var vm = new Vorticis.Desktop.ViewModels.MainViewModel();
            var janela = new MainWindow { DataContext = vm, Width = 1100, Height = 700 };
            try
            {
                janela.Show();
                Dispatcher.UIThread.RunJobs();
                var tela = janela.FindControl<Border>("TelaBloqueio")!;
                Assert.False(tela.IsVisible);
                BloqueioViewModel.Atual.Bloquear("Maria");
                Dispatcher.UIThread.RunJobs();
                Assert.True(tela.IsVisible);
                BloqueioViewModel.Atual.SairCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.False(tela.IsVisible);
                return true;
            }
            finally
            {
                typeof(MainWindow).GetField("_fechamentoConfirmado", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(janela, true);
                janela.Close();
            }
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }
}
