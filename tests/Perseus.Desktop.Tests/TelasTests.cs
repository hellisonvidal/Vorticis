using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Perseus.Application.Cadastros;
using Perseus.Desktop.ViewModels;
using Perseus.Desktop.Views;
using Perseus.Infra.Tests;

namespace Perseus.Desktop.Tests;

public static class AppDeTeste
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>
/// Fluxo completo pela camada de apresentação (ViewModels + janela real renderizada sem tela).
/// Se PERSEUS_SCREENSHOTS apontar para uma pasta, salva PNGs de cada aba.
/// </summary>
public class TelasTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Cadastra_empresa_efetiva_lancamento_e_renderiza_todas_as_abas()
    {
        BancoDeTeste.ExigirBanco();
        using var sessao = HeadlessUnitTestSession.StartNew(typeof(AppDeTeste));
        // Retornar um valor força a sobrecarga Dispatch<T>(Func<Task<T>>), que aguarda o corpo assíncrono;
        // com Func<Task> a sobrecarga escolhida devolveria Task<Task> e as asserções não seriam aguardadas.
        var concluido = await sessao.Dispatch(async () =>
        {
            var vm = new MainViewModel();
            await vm.InicializarAsync(db.ConnectionString);
            Assert.True(vm.Conectado, vm.Status);

            var cad = vm.Cadastro;
            (cad.Cnpj, cad.RazaoSocial, cad.Cnae, cad.MunicipioIbge, cad.Regime, cad.AnoExercicio) =
                ("45.723.174/0001-10", "Comércio Exemplo LTDA", "4751201", "3550308", RegimeTributario.SimplesNacional, 2026);
            cad.InicioAtividades = new DateTime(2022, 3, 1);
            await cad.CadastrarCommand.ExecuteAsync(null);
            Assert.Empty(cad.Erros);
            Assert.Equal("Comércio Exemplo LTDA", vm.EmpresaSelecionada?.RazaoSocial);

            var l = vm.Lancamento;
            await l.AoTrocarEmpresaAsync();
            Assert.Contains(l.Contas, c => c.Codigo == "1.1.01.002");
            l.Data = new DateTime(2026, 3, 10);
            (l.Linhas[0].Conta, l.Linhas[0].Valor, l.Linhas[0].Historico) = ("1.1.01.002", "1.500,00", "Venda à vista NF 123");
            l.LinhaSelecionada = l.Linhas[1];
            l.FiltroConta = "Venda de Merc";
            l.ContaSelecionada = l.Contas.Single();                    // clique na lista preenche a linha
            (l.Linhas[1].Valor, l.Linhas[1].Historico) = ("1500.00", "Venda à vista NF 123");   // ponto decimal colado de planilha
            Assert.Equal("3.1.01.001", l.Linhas[1].Conta);
            Assert.True(l.Balanceado);
            await l.EfetivarCommand.ExecuteAsync(null);
            Assert.Empty(l.Erros);
            Assert.Equal("Lançamento nº 1 efetivado.", l.Mensagem);

            l.AdicionarLinhaCommand.Execute(null);                     // tela para o screenshot: lançamento em digitação
            (l.Linhas[0].Conta, l.Linhas[0].Valor, l.Linhas[0].Historico) = ("4.2.02.001", "2.300,00", "Aluguel de março");
            l.Linhas[1].Conta = "1.1.01.002";
            l.Linhas[1].Valor = "2.000,00";

            vm.Balancete.Inicio = new DateTime(2026, 3, 1);
            vm.Balancete.Fim = new DateTime(2026, 3, 31);
            await vm.Balancete.GerarCommand.ExecuteAsync(null);
            Assert.Contains(vm.Balancete.Linhas, x => x.Codigo == "1.1.01.002" && x.SaldoFinal == "1.500,00 D");
            Assert.Contains("✓ fechado", vm.Balancete.Totais);

            vm.Razao.Conta = "1.1.01.002";
            await vm.Razao.GerarCommand.ExecuteAsync(null);
            Assert.Single(vm.Razao.Linhas);

            vm.Diario.Inicio = new DateTime(2026, 1, 1);
            vm.Diario.Fim = new DateTime(2026, 12, 31);
            await vm.Diario.GerarCommand.ExecuteAsync(null);
            Assert.Equal(2, vm.Diario.Linhas.Count);

            vm.Periodos.Ano = 2026;
            await vm.Periodos.CarregarAsync();
            Assert.Equal(12, vm.Periodos.Periodos.Count);

            var janela = new MainWindow { DataContext = vm, Width = 1280, Height = 760 };
            janela.Show();
            var abas = janela.GetVisualDescendants().OfType<TabControl>().Single();
            var pasta = Environment.GetEnvironmentVariable("PERSEUS_SCREENSHOTS");
            string[] nomes = ["lancamentos", "balancete", "razao", "diario", "periodos", "nova_empresa"];
            for (int i = 0; i < nomes.Length; i++)
            {
                abas.SelectedIndex = i;
                Dispatcher.UIThread.RunJobs();
                var quadro = janela.CaptureRenderedFrame();
                Assert.NotNull(quadro);
                if (pasta is not null) quadro.Save(Path.Combine(pasta, $"{i + 1}_{nomes[i]}.png"));
            }
            janela.Close();
            await vm.DisposeAsync();
            return true;
        }, TestContext.Current.CancellationToken);
        Assert.True(concluido);
    }
}
