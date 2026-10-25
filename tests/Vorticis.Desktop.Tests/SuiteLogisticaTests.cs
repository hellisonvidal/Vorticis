using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Vorticis.SuiteLogistica.Desktop;

namespace Vorticis.Desktop.Tests;

/// <summary>Vorticis Suite Logística: Frota, Gerencial, Contábil, Fiscal e Folha numa janela; a Frota faz o login; instalador próprio.</summary>
public class SuiteLogisticaTests(SessaoHeadless headless)
{
    private static async Task<T> ForaDaSessao<T>(Task<T> tarefa)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = tarefa.ContinueWith(t => { if (t.IsFaulted) tcs.SetException(t.Exception!.InnerExceptions); else tcs.SetResult(t.Result); }, TaskScheduler.Default);
        return await tcs.Task;
    }

    private static DirectoryInfo Raiz()
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (raiz is not null && !File.Exists(Path.Combine(raiz.FullName, "instalador", "VorticisLogistica.iss"))) raiz = raiz.Parent;
        return raiz!;
    }

    [Fact]
    public void Modulos_da_logistica_em_ordem_alfabetica_com_a_Frota_no_login()
    {
        var anterior = SuiteViewModel.ArquivoLayout;
        SuiteViewModel.ArquivoLayout = Path.Combine(Path.GetTempPath(), $"suite-layout-{Guid.NewGuid():N}.txt");
        try
        {
            var suite = ModulosDaLogistica.CriarSuite(Path.GetTempPath());   // pasta sem executáveis: entram todos
            Assert.Equal("Vorticis Suite Logística", suite.Nome);
            Assert.Equal(["Contábil", "Fiscal", "Folha", "Frota", "Gerencial"], suite.Modulos.Select(m => m.Nome));
            Assert.Equal(["C", "F", "FP", "FR", "G"], suite.Modulos.Select(m => m.Sigla));
            Assert.Equal("Frota", suite.Principal.Modulo.NomeCurto);
            Assert.Same(suite.Principal, suite.Ativo.Vm);
            Assert.DoesNotContain(suite.Modulos, m => m.Nome is "Escritório" or "Condomínio");

            // instalação personalizada: entram só os executáveis presentes; sem a Frota, o login fica com o primeiro
            var pasta = Directory.CreateTempSubdirectory("logistica").FullName;
            File.WriteAllText(Path.Combine(pasta, "VorticisFolha.exe"), "");
            File.WriteAllText(Path.Combine(pasta, "VorticisFrota.exe"), "");
            var parcial = ModulosDaLogistica.CriarSuite(pasta);
            Assert.Equal(["Folha", "Frota"], parcial.Modulos.Select(m => m.Nome));
            Assert.Equal("Frota", parcial.Principal.Modulo.NomeCurto);
            File.Delete(Path.Combine(pasta, "VorticisFrota.exe"));
            File.WriteAllText(Path.Combine(pasta, "VorticisContabil.exe"), "");
            Assert.Equal("Contábil", ModulosDaLogistica.CriarSuite(pasta).Principal.Modulo.NomeCurto);
        }
        finally { SuiteViewModel.ArquivoLayout = anterior; }
    }

    [Fact]
    public async Task Janela_da_suite_logistica_abre_na_Frota_com_as_telas_dos_modulos()
    {
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(() =>
        {
            HospedeiroSuite.IncluirTelas(Avalonia.Application.Current!, ModulosDaLogistica.Apps());
            var suite = ModulosDaLogistica.CriarSuite(Path.GetTempPath());
            var janela = new MainWindow { Width = 1400, Height = 860 };
            janela.UsarSuite(suite);
            janela.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Vorticis Suite Logística — Frota", janela.Title);
            Assert.NotNull(janela.CaptureRenderedFrame());
            // as telas do Frota (Manutenção) estão disponíveis na Suite
            var vm = new Vorticis.Frota.Desktop.ViewModels.OrdensViewModel(suite.Principal);
            var view = Avalonia.Application.Current!.DataTemplates.First(t => t.Match(vm)).Build(vm);
            Assert.IsType<Vorticis.Frota.Desktop.Views.OrdensView>(view);
            return Task.FromResult(true);
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }

    [Fact]
    public void Instalador_proprio_com_Frota_Gerencial_Contabil_Fiscal_e_Folha_e_o_Vorticis_sem_a_Frota()
    {
        var raiz = Raiz();
        var iss = File.ReadAllText(Path.Combine(raiz.FullName, "instalador", "VorticisLogistica.iss"));
        Assert.Contains("AppName=Vorticis Suite Logística", iss);
        Assert.Contains("OutputBaseFilename=Setup-VorticisLogistica-{#Versao}", iss);
        Assert.Contains(@"SetupIconFile=icone\VorticisLogistica.ico", iss);
        foreach (var (comp, exe) in new[] { ("frota", "VorticisFrota"), ("erp", "VorticisGerencial"), ("contabil", "VorticisContabil"), ("fiscal", "VorticisFiscal"), ("folha", "VorticisFolha") })
        {
            Assert.Contains($"Name: \"{comp}\";", iss);
            Assert.Contains($"Source: \"..\\publish\\{exe}.exe\"; DestDir: \"{{app}}\"; Flags: ignoreversion; Components: {comp}", iss);
        }
        Assert.DoesNotContain("Escritorio", iss);
        Assert.DoesNotContain("Condominio", iss);
        Assert.Contains("VorticisSuiteLogistica.exe\"; Description: \"{cm:LaunchProgram,Vorticis Suite Logística}\"; Flags: nowait postinstall skipifsilent;", iss);
        Assert.Contains("AppId={{8C1F4A6D", iss);                                   // produto próprio, não substitui o Vorticis
        Assert.True(File.Exists(Path.Combine(raiz.FullName, "instalador", "icone", "VorticisLogistica.ico")));
        // o Vorticis (escritório contábil) não leva mais a Frota
        Assert.DoesNotContain("frota", File.ReadAllText(Path.Combine(raiz.FullName, "instalador", "Vorticis.iss")), StringComparison.OrdinalIgnoreCase);
        // o CI gera os dois instaladores e testa a instalação dos dois
        var ci = File.ReadAllText(Path.Combine(raiz.FullName, ".github", "workflows", "ci.yml"));
        Assert.Contains("instalador/VorticisLogistica.iss", ci);
        Assert.Contains("src/Vorticis.SuiteLogistica.Desktop", ci);
        Assert.Contains("Setup-VorticisLogistica-*.exe", ci);
    }
}
