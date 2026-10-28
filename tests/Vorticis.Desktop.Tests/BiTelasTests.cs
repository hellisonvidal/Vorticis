using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Vorticis.Application.Contabil;
using Vorticis.Desktop.Comum.Views;
using Vorticis.Domain.Contabil;
using Vorticis.Desktop.ViewModels;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra.Tests;

namespace Vorticis.Desktop.Tests;

/// <summary>Telas de Relatórios › BI e o comparativo dos relatórios gerenciais, abertas numa janela real sem tela. VORTICIS_BI_PNG = pasta para salvar as capturas.</summary>
public class BiTelasTests(BancoDeTeste db, SessaoHeadless headless) : IClassFixture<BancoDeTeste>
{
    private static readonly string? PastaPng = Environment.GetEnvironmentVariable("VORTICIS_BI_PNG");

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

    private async Task Lancar(long empresa, long adm, DateOnly data, string debito, string credito, decimal valor, string? centro = null)
    {
        var r = await db.App.Lancamentos.EfetivarAsync(new LancamentoProposto(empresa, data, OrigemLancamento.Manual,
            [new(debito, IndicadorDC.D, valor, "Movimento de teste", debito[0] is '3' or '4' ? centro : null), new(credito, IndicadorDC.C, valor, "Movimento de teste", credito[0] == '3' ? centro : null)]),
            adm, TestContext.Current.CancellationToken);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
    }

    private async Task<(long Empresa, long Adm)> SemearAsync(string cnpj)
    {
        var ct = TestContext.Current.CancellationToken;
        var empresa = await db.CriarEmpresaAsync(cnpj);
        var adm = await db.AdminAsync();
        foreach (var (cod, desc) in new[] { ("1", "Administrativo"), ("2", "Comercial"), ("3", "Operação") })
            Assert.True((await db.App.PlanoContas.IncluirCentroAsync(empresa, cod, desc, null, adm, ct: ct)).Sucesso, cod);
        await Lancar(empresa, adm, new(2026, 1, 1), "1.1.01.001", "2.3.01.001", 100_000);
        await Lancar(empresa, adm, new(2026, 1, 3), "1.2.03.003", "1.1.01.002", 30_000);
        await Lancar(empresa, adm, new(2026, 2, 3), "1.1.04.001", "2.1.01.001", 15_000);
        await Lancar(empresa, adm, new(2026, 3, 3), "1.1.01.002", "2.1.05.001", 40_000);
        await Lancar(empresa, adm, new(2026, 4, 3), "1.1.01.002", "2.2.01.001", 30_000);
        for (var m = 1; m <= 9; m++)
        {
            var venda = 30_000m + m * 3_000;
            await Lancar(empresa, adm, new(2026, m, 5), "1.1.02.001", "3.1.01.003", venda, m % 2 == 0 ? "2" : "3");
            await Lancar(empresa, adm, new(2026, m, 6), "3.2.01.005", "2.1.04.003", Math.Round(venda * 0.03m, 2), m % 2 == 0 ? "2" : "3");
            await Lancar(empresa, adm, new(2026, m, 10), "1.1.01.002", "1.1.02.001", venda - 4_000);
            await Lancar(empresa, adm, new(2026, m, 12), "4.2.01.001", "1.1.01.001", 8_000 + m * 200, "1");
            await Lancar(empresa, adm, new(2026, m, 14), "4.2.02.001", "1.1.01.001", 2_500, "1");
            await Lancar(empresa, adm, new(2026, m, 15), "4.2.02.002", "1.1.01.001", 700 + m * 30, "3");
            await Lancar(empresa, adm, new(2026, m, 16), "4.2.03.002", "1.1.01.001", 1_500, "2");
            await Lancar(empresa, adm, new(2026, m, 18), "4.1.01.003", "1.1.01.001", 6_000 + m * 400, "3");
            await Lancar(empresa, adm, new(2026, m, 28), "4.2.02.007", "1.2.03.007", 500, "1");
            await Lancar(empresa, adm, new(2026, m, 28), "4.3.01.001", "1.1.01.002", 400, "1");
        }
        return (empresa, adm);
    }

    private static void Capturar(Window janela, string nome)
    {
        Dispatcher.UIThread.RunJobs();
        janela.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
        var quadro = janela.CaptureRenderedFrame();
        Assert.NotNull(quadro);
        if (PastaPng is not null) { Directory.CreateDirectory(PastaPng); quadro.Save(Path.Combine(PastaPng, nome + ".png")); }
    }

    [Fact]
    public async Task Painel_de_indicadores_abre_com_cartoes_e_graficos_em_tema_claro_e_escuro()
    {
        BancoDeTeste.ExigirBanco();
        var (empresa, adm) = await SemearAsync("44778899000658");
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            var ctx = new ContextoDeTeste(db.App, new SessaoUsuario(adm, "admin_teste", "Admin de Teste", true, false), empresa);
            var vm = new PainelBiViewModel(ctx) { Inicio = new DateTime(2026, 7, 1), Fim = new DateTime(2026, 9, 30) };
            await vm.GerarCommand.ExecuteAsync(null);
            Assert.Empty(vm.Erros);
            Assert.True(vm.Cartoes.Count >= 12);
            var receita = vm.Cartoes.Single(c => c.Titulo == "Receita líquida");
            Assert.StartsWith("R$ ", receita.Valor);
            Assert.Equal(12, vm.RotulosMeses.Count);
            Assert.Equal(2, vm.SeriesMensais.Count);
            Assert.NotEmpty(vm.DespesasGrupo);
            Assert.NotEmpty(vm.CustosCentro);
            Assert.Equal(7, vm.Cascata.Count);
            Assert.True(vm.TemDados);

            var janela = new Window { Width = 1280, Height = 1760, Content = new ContentControl { Content = vm }, RequestedThemeVariant = ThemeVariant.Light };
            janela.Show();
            Dispatcher.UIThread.RunJobs();
            Capturar(janela, "painel");
            janela.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();
            Capturar(janela, "painel_escuro");
            janela.Close();

            // exportação do painel (PDF) e das telas irmãs
            ArquivosGerados.AbrirAposGerar = false;
            vm.ExportarPdfCommand.Execute(null);
            Assert.Empty(vm.Erros);
            return true;
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }

    [Fact]
    public async Task Analise_cubo_e_exportacao_funcionam_pelas_telas_e_o_comparativo_vale_nos_dois_relatorios_gerenciais()
    {
        BancoDeTeste.ExigirBanco();
        var (empresa, adm) = await SemearAsync("44778899000739");
        var pasta = Path.Combine(Path.GetTempPath(), "vorticis_bi_tela_" + Guid.NewGuid().ToString("N"));
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            ArquivosGerados.AbrirAposGerar = false;
            var ctx = new ContextoDeTeste(db.App, new SessaoUsuario(adm, "admin_teste", "Admin de Teste", true, false), empresa);

            // Análise de indicadores com comparativo
            var analise = new AnaliseIndicadoresViewModel(ctx) { Inicio = new DateTime(2026, 4, 1), Fim = new DateTime(2026, 6, 30) };
            await analise.GerarCommand.ExecuteAsync(null);
            Assert.Empty(analise.Erros);
            Assert.NotNull(analise.TabelaExibida);
            analise.Comparacao.Ativo = true;
            analise.Comparacao.PeriodoAnterior = true;
            await analise.GerarCommand.ExecuteAsync(null);
            Assert.Empty(analise.Erros);
            Assert.NotNull(analise.TabelaComparativa);
            Assert.Contains(analise.TabelaComparativa!.Colunas, c => c.Titulo.StartsWith("Var. %"));
            var janelaAnalise = new Window { Width = 1280, Height = 900, Content = new ContentControl { Content = analise } };
            janelaAnalise.Show();
            Capturar(janelaAnalise, "analise");
            janelaAnalise.Close();

            // Cubo: conta x mês e drill-down
            var cubo = new CuboBiViewModel(ctx) { Inicio = new DateTime(2026, 1, 1), Fim = new DateTime(2026, 9, 30), SoResultado = true };
            cubo.Medida = cubo.Medidas.Single(m => m.Valor == MedidaCubo.Resultado);
            await cubo.GerarCommand.ExecuteAsync(null);
            Assert.Empty(cubo.Erros);
            Assert.NotNull(cubo.TabelaTela);
            var linhaVendas = cubo.TabelaTela!.Linhas.ToList().FindIndex(l => (string?)l.Valores[0] == "3.1.01.003 — Prestação de Serviços");
            Assert.True(linhaVendas >= 0);
            cubo.Detalhar(linhaVendas, 2);   // fevereiro
            Assert.True(cubo.TemDetalhe);
            Assert.Equal([36_000m], cubo.TabelaDetalhe!.Linhas.Select(l => (decimal)l.Valores[6]!));
            var janelaCubo = new Window { Width = 1280, Height = 900, Content = new ContentControl { Content = cubo } };
            janelaCubo.Show();
            Capturar(janelaCubo, "cubo");
            janelaCubo.Close();

            // Exportar dados para BI
            var exportar = new ExportarBiViewModel(ctx) { Inicio = new DateTime(2026, 1, 1), Fim = new DateTime(2026, 9, 30), Pasta = pasta, Formato = 2 };
            await exportar.ExportarCommand.ExecuteAsync(null);
            Assert.Empty(exportar.Erros);
            Assert.True(File.Exists(Path.Combine(pasta, "fato_partidas.csv")) && File.Exists(Path.Combine(pasta, "dados_bi.xlsx")));
            var semPermissao = new ExportarBiViewModel(new ContextoDeTeste(db.App, new SessaoUsuario(adm, "admin_teste", "Admin de Teste", true, false), empresa, pode: false)) { Pasta = pasta + "_x" };
            await semPermissao.ExportarCommand.ExecuteAsync(null);
            Assert.NotEmpty(semPermissao.Erros);
            Assert.False(Directory.Exists(pasta + "_x"));

            // Comparativo nos dois relatórios gerenciais
            var receitaCentro = new ReceitaCentroViewModel(ctx) { Inicio = new DateTime(2026, 6, 1), Fim = new DateTime(2026, 6, 30) };
            receitaCentro.Comparacao.Ativo = true;
            receitaCentro.Comparacao.PeriodoAnterior = true;
            await receitaCentro.GerarCommand.ExecuteAsync(null);
            Assert.Empty(receitaCentro.Erros);
            Assert.NotNull(receitaCentro.TabelaComparativa);
            Assert.Contains(receitaCentro.TabelaComparativa!.Colunas, c => c.Titulo.StartsWith("Var. R$"));
            receitaCentro.ExportarPdfCommand.Execute(null);
            Assert.Empty(receitaCentro.Erros);
            var janelaRc = new Window { Width = 1280, Height = 800, Content = new ContentControl { Content = receitaCentro } };
            janelaRc.Show();
            Capturar(janelaRc, "receita_centro_comparativo");
            janelaRc.Close();

            var orcado = new OrcadoRealizadoViewModel(ctx) { Ano = 2026, MesInicial = 4, MesFinal = 6 };
            orcado.Comparacao.Ativo = true;
            orcado.Comparacao.PeriodoAnterior = true;
            await orcado.GerarCommand.ExecuteAsync(null);
            Assert.Empty(orcado.Erros);
            Assert.NotNull(orcado.TabelaComparativa);
            orcado.Mensal = true;
            await orcado.GerarCommand.ExecuteAsync(null);
            Assert.Empty(orcado.Erros);
            Assert.Null(orcado.TabelaComparativa);   // o mês a mês não tem comparativo
            return true;
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
        if (Directory.Exists(pasta)) Directory.Delete(pasta, true);
    }
}
