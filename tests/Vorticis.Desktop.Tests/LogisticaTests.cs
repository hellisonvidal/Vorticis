using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Vorticis.Application.Cadastros;
using Vorticis.Desktop.Comum.Ajuda;
using Vorticis.Desktop.Comum.Modulos;
using Vorticis.Domain.Seguranca;
using Vorticis.Logistica.Desktop;

namespace Vorticis.Desktop.Tests;

/// <summary>Vorticis Logística: um programa só, com as telas de Frota, Gerencial, Contábil, Fiscal e Folha num menu único.</summary>
public class LogisticaTests(SessaoHeadless headless, Vorticis.Infra.Tests.BancoDeTeste db) : IClassFixture<Vorticis.Infra.Tests.BancoDeTeste>
{
    private static IEnumerable<ItemMenu> Folhas(IEnumerable<ItemMenu> itens) => itens.SelectMany(i => i.Filhos is null ? [i] : Folhas(i.Filhos));

    private static async Task<T> ForaDaSessao<T>(Task<T> tarefa)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = tarefa.ContinueWith(t => { if (t.IsFaulted) tcs.SetException(t.Exception!.InnerExceptions); else tcs.SetResult(t.Result); }, TaskScheduler.Default);
        return await tcs.Task;
    }

    [Fact]
    public void Um_menu_so_com_frota_manutencao_emissao_financeiro_fiscal_contabil_folha_e_relatorios()
    {
        var vm = new MainViewModel();
        Assert.Equal("Vorticis Logística", vm.Modulo.Nome);
        Assert.Equal(["_Cadastros", "_Operação", "_Manutenção", "_Emissão", "_Financeiro", "F_iscal", "_Contábil", "Fol_ha", "_Relatórios", "_Obrigações", "Con_figurações", "_Ajuda"],
                     vm.Menus.Select(m => m.Titulo));
        // toda função habilitada abre uma tela conhecida; o que ainda falta (emissão de CT-e, MDF-e e NFS-e) fica "em breve"
        var itens = Folhas(vm.Menus).Where(i => !i.EhSeparador).ToList();
        Assert.All(itens.Where(i => i.Habilitado && i.Chave is not null && i.Chave != TelasComuns.Sair), i => Assert.True(vm.ConheceTela(i.Chave!), $"{i.Titulo}: {i.Chave}"));
        Assert.Equal(3, Folhas(vm.Menus.Single(m => m.Titulo == "_Emissão").Filhos!).Count(i => !i.Habilitado));
        // funções de cada área no mesmo programa
        foreach (var chave in new[] { Frota.Desktop.ViewModels.FrotaTelas.Ordens, Frota.Desktop.ViewModels.FrotaTelas.Autorizacao, Fiscal.Desktop.ViewModels.Telas.ContasPagar,
                                      Fiscal.Desktop.ViewModels.Telas.ContasReceber, ViewModels.Telas.Lancamento, ViewModels.Telas.Balancete, Fiscal.Desktop.ViewModels.Telas.ImportacaoXml,
                                      Folha.Desktop.ViewModels.FolhaTelas.FolhaMensal })
            Assert.Contains(itens, i => i.Chave == chave);
        // cada módulo continua com os seus parâmetros, num submenu de Cadastros
        var parametros = Folhas(vm.Menus[0].Filhos!.Single(i => i.Titulo == "Parâmetros dos _módulos").Filhos!).Select(i => i.Chave!).ToList();
        Assert.Equal(4, parametros.Distinct().Count());
        Assert.All(parametros, c => Assert.True(vm.ConheceTela(c), c));
        Assert.Contains(vm.Menus[0].Filhos!, i => i.Titulo == "Parâmetros" && i.Chave == TelasComuns.Parametros);   // os do Frota
        // atalhos sem tecla repetida, todos para telas conhecidas
        Assert.Equal(vm.Atalhos.Count, vm.Atalhos.Select(a => a.Gesto).Distinct().Count());
        Assert.All(vm.Atalhos.Where(a => a.Chave is not null), a => Assert.True(vm.ConheceTela(a.Chave!), a.Chave));
    }

    [Fact]
    public void Transportadora_e_cadastrada_em_todos_os_modulos_reunidos_e_o_manual_mostra_as_areas_deles()
    {
        var m = new ModuloLogistica();
        Assert.Equal(ModulosEmpresa.Frota, m.ModuloEmpresa);
        Assert.Equal(ModulosEmpresa.Frota | ModulosEmpresa.Contabil | ModulosEmpresa.Fiscal | ModulosEmpresa.Folha | ModulosEmpresa.Erp,
                     ((IModuloDesktop)m).ModulosAoCadastrar);
        Assert.Equal("Transportadora", ((IModuloDesktop)m).RotuloEmpresaAtiva);
        var areas = ((IModuloDesktop)m).AreasDoManual;
        Assert.Contains("Contábil", areas);
        Assert.Contains("Folha", areas);
        Assert.Contains(ManualUsuario.Visiveis(areas, false), a => a.Nome == "03-contabil.md");
        Assert.NotNull(ManualUsuario.TopicoDaTela("VorticisContabil:" + TelasComuns.Parametros));   // F1 nos parâmetros de cada módulo
    }

    [Fact]
    public async Task Telas_de_todas_as_areas_abrem_no_programa_unico()
    {
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(() =>
        {
            HospedeiroSuite.IncluirTelas(Avalonia.Application.Current!, ModuloLogistica.Apps());
            var vm = new MainViewModel();
            var janela = new Window { Width = 1400, Height = 860 };
            object[] telas =
            [
                new Frota.Desktop.ViewModels.OrdensViewModel(vm), new Frota.Desktop.ViewModels.VeiculosViewModel(vm),
                new Fiscal.Desktop.ViewModels.ContasPagarViewModel(vm), new ViewModels.BalanceteViewModel(vm),
            ];
            foreach (var t in telas)
            {
                var view = Avalonia.Application.Current!.DataTemplates.First(d => d.Match(t)).Build(t)!;
                view.DataContext = t;
                janela.Content = view;
                if (!janela.IsVisible) janela.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.NotNull(janela.CaptureRenderedFrame());
            }
            return Task.FromResult(true);
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }

    [Fact]
    public async Task Com_a_transportadora_escolhida_todas_as_telas_do_menu_abrem()
    {
        Vorticis.Infra.Tests.BancoDeTeste.ExigirBanco();
        var adm = await db.AdminAsync();
        await db.App.Perfis.ConcluirConfiguracaoInicialAsync(new SessaoUsuario(adm, "admin_teste", "Admin", true, false), TestContext.Current.CancellationToken);
        var empresa = await db.CriarEmpresaAsync("27183946000359");
        await db.EscalarAsync<int>("UPDATE core.empresa SET modulo_frota = TRUE WHERE id = @e RETURNING 1", ("e", empresa));
        var ct = TestContext.Current.CancellationToken;
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            HospedeiroSuite.IncluirTelas(Avalonia.Application.Current!, ModuloLogistica.Apps());
            var vm = new MainViewModel();
            await vm.InicializarAsync(db.ConnectionString);
            var login = Assert.IsType<LoginViewModel>(vm.Conteudo);
            (login.Usuario, login.Senha) = ("admin_teste", "Senha1234");
            await login.EntrarCommand.ExecuteAsync(null);
            for (var i = 0; i < 200 && !vm.Logado; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
            Assert.True(vm.Logado);
            vm.EmpresaSelecionada = vm.EmpresasPermitidas.Single(e => e.Id == empresa);
            for (var i = 0; i < 50; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
            var chaves = Folhas(vm.Menus).Where(i => i.Habilitado && i.Chave is not null && i.Chave != TelasComuns.Sair).Select(i => i.Chave!).Distinct().ToList();
            Assert.True(chaves.Count > 100, $"{chaves.Count} telas");
            foreach (var chave in chaves)
            {
                Assert.True(vm.AbrirCommand.CanExecute(chave), chave);
                vm.AbrirCommand.Execute(chave);
                for (var i = 0; i < 5; i++) { await Task.Delay(10, ct); Dispatcher.UIThread.RunJobs(); }
                Assert.NotNull(vm.Conteudo);
                Assert.True(vm.Confirmacao is null, $"{chave}: {vm.Status}");     // nenhum erro inesperado ao abrir
            }
            return true;
        }, ct));
        Assert.True(ok);
    }

    [Fact]
    public void Instalador_proprio_com_um_programa_so()
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (raiz is not null && !File.Exists(Path.Combine(raiz.FullName, "instalador", "VorticisLogistica.iss"))) raiz = raiz.Parent;
        var iss = File.ReadAllText(Path.Combine(raiz!.FullName, "instalador", "VorticisLogistica.iss"));
        Assert.Contains("AppName=Vorticis Logística", iss);
        Assert.Contains("OutputBaseFilename=Setup-VorticisLogistica-{#Versao}", iss);
        Assert.Contains(@"Source: ""..\publish\VorticisLogistica.exe""; DestDir: ""{app}""; Flags: ignoreversion", iss);
        Assert.Contains(@"Filename: ""{app}\VorticisLogistica.exe""; Description: ""{cm:LaunchProgram,Vorticis Logística}""", iss);
        Assert.DoesNotContain("[Components]", iss);                         // nada para escolher: é um programa só
        Assert.DoesNotContain("VorticisFrota.exe", iss);
        Assert.Contains("AppId={{8C1F4A6D", iss);                           // produto próprio, não substitui o Vorticis
        foreach (var ico in new[] { "VorticisLogistica.ico", "VorticisLogisticaInstalador.ico" })
            Assert.True(File.Exists(Path.Combine(raiz.FullName, "instalador", "icone", ico)), ico);
        Assert.DoesNotContain("frota", File.ReadAllText(Path.Combine(raiz.FullName, "instalador", "Vorticis.iss")), StringComparison.OrdinalIgnoreCase);
        var ci = File.ReadAllText(Path.Combine(raiz.FullName, ".github", "workflows", "ci.yml"));
        Assert.Contains("dotnet publish src/Vorticis.Logistica.Desktop", ci);
        Assert.Contains("instalador/VorticisLogistica.iss", ci);
        Assert.Contains("-Programa VorticisLogistica.exe", ci);
        Assert.Contains("VorticisLogistica-portatil", ci);
    }
}
