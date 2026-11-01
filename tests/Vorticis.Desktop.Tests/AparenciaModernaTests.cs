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
}
