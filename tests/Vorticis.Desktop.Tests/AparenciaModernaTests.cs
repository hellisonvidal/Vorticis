using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Desktop.Tests;

/// <summary>Módulo aberto sozinho (fora da Suite): cabeçalho moderno com o selo do módulo, empresa e usuário, e barra de menus clara.</summary>
public class AparenciaModernaTests(SessaoHeadless headless)
{
    [Fact]
    public async Task Modulo_sozinho_tem_cabecalho_moderno_e_barra_de_menus_clara()
    {
        var ok = await Task.Run(() => headless.Sessao.Dispatch(() =>
        {
            var vm = new Vorticis.Fiscal.Desktop.MainViewModel();
            var janela = new MainWindow { DataContext = vm, Width = 1280, Height = 760 };
            try
            {
                janela.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.Contains("moderna", janela.Classes);
                var cabecalho = janela.FindControl<Border>("CabecalhoModulo")!;
                Assert.False(cabecalho.IsVisible);                                   // antes de entrar, só a tela de login
                vm.Logado = true;
                Dispatcher.UIThread.RunJobs();
                Assert.True(cabecalho.IsVisible);
                Assert.False(janela.FindControl<StackPanel>("EmpresaNaBarra")!.IsVisible);   // empresa e usuário passam para o cabeçalho
                Assert.NotEqual(vm.PincelBarra, janela.FindControl<Border>("BarraMenu")!.Background);
                Assert.NotNull(vm.IconeModulo);
                if (Environment.GetEnvironmentVariable("VORTICIS_SCREENSHOTS") is { } pasta)
                {
                    janela.CaptureRenderedFrame();
                    Dispatcher.UIThread.RunJobs();
                    janela.CaptureRenderedFrame()?.Save(Path.Combine(pasta, "modulo_sozinho_moderno.png"));
                }
                vm.Logado = false;
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

    [Fact]
    public async Task Suite_usa_a_mesma_aparencia_moderna_dos_modulos()
    {
        Vorticis.Desktop.Comum.ViewModels.SuiteViewModel.ArquivoLayout = Path.Combine(Path.GetTempPath(), $"suite-{Guid.NewGuid():N}.json");
        var ok = await Task.Run(() => headless.Sessao.Dispatch(() =>
        {
            var suite = new Vorticis.Desktop.Comum.ViewModels.SuiteViewModel(Vorticis.Suite.Desktop.ModulosDaSuite.Criar(Path.GetTempPath()));
            var janela = new MainWindow { Width = 1366, Height = 768 };
            try
            {
                janela.UsarSuite(suite);
                janela.Show();
                Dispatcher.UIThread.RunJobs();
                foreach (var m in suite.Modulos) m.Vm.Logado = true;
                Dispatcher.UIThread.RunJobs();
                Assert.Contains("moderna", janela.Classes);                                         // barra clara, cantos arredondados
                Assert.False(janela.FindControl<Border>("CabecalhoModulo")!.IsVisible);           // o módulo aparece na navegação da Suite
                Assert.True(janela.FindControl<StackPanel>("EmpresaNaBarra")!.IsVisible);         // empresa e usuário ficam na barra de menus
                Assert.True(janela.FindControl<StackPanel>("UsuarioNaBarra")!.IsVisible);
                Assert.NotEqual(suite.Ativo.Vm.PincelBarra, janela.FindControl<Border>("BarraMenu")!.Background);
                foreach (var m in suite.Modulos) m.Vm.Logado = false;
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
