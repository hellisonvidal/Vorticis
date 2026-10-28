using System.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vorticis.Application.Cadastros;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Domain.Contabil;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra.Tests;
using Vorticis.Suite.Desktop;
using Npgsql;

namespace Vorticis.Desktop.Tests;

/// <summary>
/// Varredura: abre, na Suite, TODAS as telas registradas de todos os módulos, com uma empresa de dados mínimos, e falha diante de exceção
/// (inclusive assíncrona), mensagem de erro, tela sem DataTemplate (vazia) ou conteúdo com tamanho zero. Depois abre uma tela de cada
/// módulo em cada um dos cinco layouts.
/// </summary>
public class VarreduraTelasTests(BancoDeTeste db, SessaoHeadless headless) : IClassFixture<BancoDeTeste>
{
    private static async Task<T> ForaDaSessao<T>(Task<T> tarefa)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = tarefa.ContinueWith(t => { if (t.IsFaulted) tcs.SetException(t.Exception!.InnerExceptions); else tcs.SetResult(t.Result); }, TaskScheduler.Default);
        return await tcs.Task;
    }

    private async Task SemearAsync(long empresa, long adm, CancellationToken ct)
    {
        var p = await db.App.Participantes.GravarAsync(empresa, new Participante(0, "", "Cliente Teste", null, "11222333000181", null, "SP", null, null, null, null, true, false, null, true), adm, ct);
        Assert.True(p.Sucesso, string.Join("; ", p.Erros));
        var l = await db.App.Lancamentos.EfetivarAsync(new LancamentoProposto(empresa, new DateOnly(2026, 3, 5), OrigemLancamento.Manual,
            [new("1.1.01.001", IndicadorDC.D, 10m, "Teste"), new("3.1.01.001", IndicadorDC.C, 10m, "Teste")]), adm, ct);
        Assert.True(l.Sucesso, string.Join("; ", l.Erros));
        await Sql($"INSERT INTO fiscal.produto (empresa_id, codigo, descricao, unidade) VALUES ({empresa}, 1, 'Parafuso', 'UN')");
        await Sql($"INSERT INTO folha.funcionario (empresa_id, matricula, nome, cpf, data_nascimento, sexo, data_admissao, salario) VALUES ({empresa}, '1', 'Funcionario Teste', '52998224725', '1990-01-01', 'M', '2026-01-01', 2000)");
    }

    private async Task Sql(string sql)
    {
        await using var con = new NpgsqlConnection(db.ConnectionString);
        await con.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, con);
        await cmd.ExecuteNonQueryAsync();
    }

    private static IEnumerable<string> ErrosDa(object? vm)
    {
        if (vm?.GetType().GetProperty("Erros")?.GetValue(vm) is IEnumerable e)
            foreach (var x in e) if (x is string s && s.Length > 0) yield return s;
    }

    private static async Task Calma(int rodadas = 6)
    {
        for (var i = 0; i < rodadas; i++) { await Task.Delay(40); Dispatcher.UIThread.RunJobs(); }
    }

    [Fact]
    public async Task Abre_todas_as_telas_de_todos_os_modulos_e_em_todos_os_layouts()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        SuiteViewModel.ArquivoLayout = Path.Combine(Path.GetTempPath(), $"suite-layout-{Guid.NewGuid():N}.txt");
        var adm = await db.AdminAsync();
        await db.App.Perfis.ConcluirConfiguracaoInicialAsync(new SessaoUsuario(adm, "admin_teste", "Admin", true, false), ct);
        var empresa = await db.CriarEmpresaAsync("11444777000161");
        await SemearAsync(empresa, adm, ct);
        var falhas = new List<string>();
        var excecoes = new List<string>();
        void Excecao(object? s, Exception e) { lock (excecoes) excecoes.Add(e.ToString()); }
        UnhandledExceptionEventHandler dominio = (s, e) => Excecao(s, (Exception)e.ExceptionObject);
        EventHandler<UnobservedTaskExceptionEventArgs> tarefas = (s, e) => { Excecao(s, e.Exception); e.SetObserved(); };
        AppDomain.CurrentDomain.UnhandledException += dominio;
        TaskScheduler.UnobservedTaskException += tarefas;
        try
        {
            await ForaDaSessao(headless.Sessao.Dispatch(async () =>
            {
                Dispatcher.UIThread.UnhandledException += (s, e) => { Excecao(s, e.Exception); e.Handled = true; };
                ModulosDaSuite.IncluirTelas(Avalonia.Application.Current!);
                var suite = new SuiteViewModel(ModulosDaSuite.Criar(Path.GetTempPath()));
                var janela = new MainWindow { Width = 1400, Height = 860 };
                janela.UsarSuite(suite);
                janela.Show();
                Dispatcher.UIThread.RunJobs();
                await suite.Principal.InicializarAsync(db.ConnectionString);
                var login = Assert.IsType<LoginViewModel>(suite.Principal.Conteudo);
                (login.Usuario, login.Senha) = ("admin_teste", "Senha1234");
                await login.EntrarCommand.ExecuteAsync(null);
                for (var i = 0; i < 300 && suite.Modulos.Any(m => !m.Vm.Logado); i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
                Assert.All(suite.Modulos, m => Assert.True(m.Vm.Logado, m.Nome));

                foreach (var modulo in suite.Modulos)
                {
                    var vm = modulo.Vm;
                    suite.Trocar(modulo);
                    await Calma();
                    var emp = vm.EmpresasPermitidas.FirstOrDefault(e => e.Id == empresa);
                    if (emp is not null) vm.EmpresaSelecionada = emp;
                    await Calma(10);
                    foreach (var chave in vm.ChavesDeTelas.ToList())
                    {
                        string Onde() => $"{modulo.Nome}/{chave}";
                        if (chave is TelasComuns.Sair or TelasComuns.Inicio) continue;   // início não é função (vira a tela de fundo)
                        if (!vm.PodeAbrir(chave)) { falhas.Add($"{Onde()}: administrador não pode abrir"); continue; }
                        vm.Status = "";
                        vm.AbrirCommand.Execute(chave);
                        await Calma();
                        if (vm.Confirmacao is { } c)
                        {
                            if (c.Titulo.Contains("rro")) falhas.Add($"{Onde()}: diálogo de erro: {c.Mensagem}");
                            vm.ResponderConfirmacaoCommand.Execute(false);
                            await Calma(2);
                        }
                        if (vm.Status.StartsWith("Erro", StringComparison.OrdinalIgnoreCase)) falhas.Add($"{Onde()}: status: {vm.Status}");
                        foreach (var e in ErrosDa(vm.Conteudo)) falhas.Add($"{Onde()}: Erros: {e}");
                        var f = vm.JanelasAbertas.FirstOrDefault(j => ReferenceEquals(j.ViewModel, vm.Conteudo));
                        if (f is null) { falhas.Add($"{Onde()}: não abriu função (conteúdo {vm.Conteudo?.GetType().Name})"); continue; }
                        var w = janela.JanelaDa(f.ViewModel);
                        if (w is null) { falhas.Add($"{Onde()}: sem janela"); continue; }
                        w.UpdateLayout();
                        Dispatcher.UIThread.RunJobs();
                        var cc = w.GetVisualDescendants().OfType<ContentControl>().First(x => ReferenceEquals(x.Content, f.ViewModel));
                        var view = (cc.Presenter as ContentPresenter)?.Child;
                        if (view is null || view is TextBlock) falhas.Add($"{Onde()}: sem view/DataTemplate para {f.ViewModel.GetType().Name}");
                        else if (view.Bounds.Width < 1 || view.Bounds.Height < 1) falhas.Add($"{Onde()}: view com tamanho zero {view.Bounds}");
                        else
                        {
                            var zeros = view.GetVisualDescendants().OfType<Control>().Where(x => x.IsEffectivelyVisible && (x is TextBox or TabControl)
                                && (x.Bounds.Width < 1 || x.Bounds.Height < 1)).Select(x => x.GetType().Name).Distinct().ToList();
                            if (zeros.Count > 0) falhas.Add($"{Onde()}: controles com tamanho zero: {string.Join(", ", zeros)}");
                        }
                        vm.FecharFuncao(f);
                        Dispatcher.UIThread.RunJobs();
                        if (vm.Confirmacao is not null) vm.ResponderConfirmacaoCommand.Execute(true);
                    }
                    lock (excecoes) foreach (var e in excecoes) falhas.Add($"{modulo.Nome}: exceção: {e}");
                    lock (excecoes) excecoes.Clear();
                }

                // uma tela de cada módulo em cada layout
                foreach (var layout in Enum.GetValues<LayoutSuite>())
                {
                    suite.Layout = layout;
                    foreach (var modulo in suite.Modulos)
                    {
                        suite.Trocar(modulo);
                        Dispatcher.UIThread.RunJobs();
                        modulo.Vm.AbrirCommand.Execute(TelasComuns.Sobre);
                        await Calma(3);
                        var f = modulo.Vm.JanelasAbertas.FirstOrDefault(j => j.Tela == TelasComuns.Sobre);
                        if (f is null || janela.JanelaDa(f.ViewModel) is not { IsVisible: true }) falhas.Add($"layout {layout}/{modulo.Nome}: tela não visível");
                        if (janela.CaptureRenderedFrame() is null) falhas.Add($"layout {layout}/{modulo.Nome}: sem quadro");
                        if (f is not null) modulo.Vm.FecharFuncao(f);
                    }
                }
                janela.Close();
                return true;
            }, ct));
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            lock (excecoes) foreach (var e in excecoes) falhas.Add($"exceção assíncrona: {e}");
        }
        finally
        {
            AppDomain.CurrentDomain.UnhandledException -= dominio;
            TaskScheduler.UnobservedTaskException -= tarefas;
        }
        Assert.True(falhas.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, falhas));
    }
}
