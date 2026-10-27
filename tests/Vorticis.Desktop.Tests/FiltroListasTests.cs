using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vorticis.Desktop.Comum.Views;
using E = Vorticis.Escritorio.Desktop;
using R = Vorticis.Erp.Desktop;
using K = Vorticis.Condominio.Desktop;

namespace Vorticis.Desktop.Tests;

/// <summary>Filtros nas listas: lógica pura (texto sem acento, termos, número, data, lista) e telas (busca, painel por coluna, limpar, Ctrl+F).</summary>
public class FiltroListasTests(SessaoHeadless headless)
{
    private sealed record Linha(string Codigo, string Nome, decimal Valor, DateTime Data, bool Ativo, string Situacao);

    private static readonly Linha[] Dados =
    [
        new("001", "João da Conceição", 100m, new DateTime(2024, 1, 10), true, "Ativo"),
        new("002", "Maria Souza", 250.50m, new DateTime(2024, 2, 15), false, "Afastado"),
        new("003", "José Conceição Souza", 1500m, new DateTime(2024, 3, 20), true, "Ativo"),
    ];

    private static List<LeitorColuna> Leitores() =>
    [
        new("Código", o => ((Linha)o).Codigo), new("Nome", o => ((Linha)o).Nome), new("Valor", o => ((Linha)o).Valor),
        new("Data", o => ((Linha)o).Data), new("Ativo", o => ((Linha)o).Ativo), new("Situação", o => ((Linha)o).Situacao),
    ];

    private static int Cont(DataGrid g) => g.CollectionView!.Cast<object>().Count();

    private static int Quantas(EstadoFiltro e) => Dados.Count(l => e.Aceita(l, Leitores()));

    [Fact]
    public void Busca_ignora_acento_e_maiusculas()
    {
        Assert.Equal("joao conceicao", FiltroLinhas.Normalizar("JOÃO Conceição"));
        Assert.Equal(2, Quantas(new EstadoFiltro { Busca = "CONCEICAO" }));
        Assert.Equal(1, Quantas(new EstadoFiltro { Busca = "joão" }));
    }

    [Fact]
    public void Varios_termos_exigem_todos_em_qualquer_coluna()
    {
        Assert.Equal(1, Quantas(new EstadoFiltro { Busca = "conceicao souza" }));
        Assert.Equal(1, Quantas(new EstadoFiltro { Busca = "afastado maria" }));
        Assert.Equal(0, Quantas(new EstadoFiltro { Busca = "maria joao" }));
        Assert.Equal(3, Quantas(new EstadoFiltro { Busca = "  " }));
        Assert.Equal(1, Quantas(new EstadoFiltro { Busca = "250,50" }));     // número como aparece na tela
        Assert.Equal(1, Quantas(new EstadoFiltro { Busca = "15/02/2024" }));  // data como aparece na tela
    }

    [Fact]
    public void Filtro_de_texto_contem_comeca_e_igual()
    {
        var e = new EstadoFiltro();
        e.Coluna(1).Texto = "souza";
        Assert.Equal(2, Quantas(e));
        e.Coluna(1).Modo = ModoTexto.ComecaCom; e.Coluna(1).Texto = "jose";
        Assert.Equal(1, Quantas(e));
        e.Coluna(1).Modo = ModoTexto.Igual; e.Coluna(1).Texto = "maria souza";
        Assert.Equal(1, Quantas(e));
        e.Coluna(1).Texto = "maria";
        Assert.Equal(0, Quantas(e));
    }

    [Fact]
    public void Filtro_de_numero_data_sim_nao_e_lista()
    {
        var e = new EstadoFiltro();
        e.Coluna(2).Min = 200m; e.Coluna(2).Max = 1000m;
        Assert.Equal(1, Quantas(e));
        e.Limpar();
        e.Coluna(3).De = new DateTime(2024, 2, 1); e.Coluna(3).Ate = new DateTime(2024, 3, 20);
        Assert.Equal(2, Quantas(e));
        e.Limpar();
        e.Coluna(4).SimNao = true;
        Assert.Equal(2, Quantas(e));
        e.Coluna(5).Valores = ["Ativo"];
        Assert.Equal(2, Quantas(e));
        e.Coluna(5).Valores = ["Afastado"];
        Assert.Equal(0, Quantas(e));    // Sim/Não + lista combinam (E)
        Assert.Equal(2, e.ColunasAtivas);
        e.Limpar();
        Assert.False(e.Ativo);
    }

    [Fact]
    public void Textos_formatados_pt_br_valem_como_numero_e_data()
    {
        Assert.True(FiltroLinhas.TentarNumero("1.234,56", out var n)); Assert.Equal(1234.56m, n);
        Assert.True(FiltroLinhas.TentarNumero("R$ 10,00", out n)); Assert.Equal(10m, n);
        Assert.False(FiltroLinhas.TentarNumero("123.456.789-00", out _));   // CPF não é número
        Assert.True(FiltroLinhas.TentarData("05/03/2024", out var d)); Assert.Equal(new DateTime(2024, 3, 5), d);
        Assert.Equal(TipoDadoColuna.Numero, FiltroLinhas.DetectarTipo(["1,00", "20,50", ""]));
        Assert.Equal(TipoDadoColuna.Data, FiltroLinhas.DetectarTipo(["01/01/2024", "02/01/2024"]));
        Assert.Equal(TipoDadoColuna.SimNao, FiltroLinhas.DetectarTipo(["Sim", "Não"]));
        Assert.Equal(TipoDadoColuna.Texto, FiltroLinhas.DetectarTipo(["Caixa", "10"]));
    }

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

    private static T Achar<T>(Visual raiz, string nome) where T : Control
        => raiz.GetVisualDescendants().OfType<T>().First(c => c.Name == nome);

    private static (Window Janela, DataGrid Grade, BarraFiltro Barra, ObservableCollection<Linha> Itens) Montar()
    {
        var itens = new ObservableCollection<Linha>(Dados);
        var grade = new DataGrid { ItemsSource = itens, AutoGenerateColumns = false, IsReadOnly = true };
        foreach (var (h, p) in new[] { ("Código", "Codigo"), ("Nome", "Nome"), ("Valor", "Valor"), ("Data", "Data"), ("Ativo", "Ativo"), ("Situação", "Situacao") })
            grade.Columns.Add(new DataGridTextColumn { Header = h, Binding = new Binding(p) });
        var painel = new StackPanel();
        painel.Children.Add(grade);
        var janela = new Window { Content = painel, Width = 900, Height = 500 };
        janela.Show();
        Dispatcher.UIThread.RunJobs();
        var barra = FiltroGrade.ObterBarra(grade)!;
        Assert.NotNull(barra);
        return (janela, grade, barra, itens);
    }

    [Fact]
    public async Task Busca_reduz_linhas_limpar_restaura_e_selecao_continua()
    {
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            var (janela, grade, barra, itens) = Montar();
            var contador = Achar<TextBlock>(janela, "ContadorFiltro");
            Assert.Equal("3 registros", contador.Text);
            Assert.False(Achar<Button>(janela, "BotaoLimparFiltros").IsVisible);

            Achar<TextBox>(janela, "BuscaRapida").Text = "conceicao souza";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, Cont(grade));
            Assert.Equal("1 de 3 registros", contador.Text);
            Assert.True(Achar<Button>(janela, "BotaoLimparFiltros").IsVisible);

            grade.SelectedItem = grade.CollectionView.Cast<Linha>().First();   // seleção sobre a linha filtrada
            Assert.Equal("003", ((Linha)grade.SelectedItem!).Codigo);

            itens.Add(new Linha("004", "Ana Conceição Souza", 1m, DateTime.Today, true, "Ativo"));   // linha nova também passa pelo filtro
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, Cont(grade));
            Assert.Equal("2 de 4 registros", contador.Text);

            Achar<Button>(janela, "BotaoLimparFiltros").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(4, Cont(grade));
            Assert.Equal("", Achar<TextBox>(janela, "BuscaRapida").Text);
            Assert.Equal("4 registros", contador.Text);
            await Task.Yield();
            janela.Close();
            return true;
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }

    [Fact]
    public async Task Painel_filtra_por_coluna_e_conta_filtros_ativos()
    {
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            var (janela, grade, barra, _) = Montar();
            var painel = Achar<Border>(janela, "PainelFiltros");
            Assert.False(painel.IsVisible);
            Achar<Button>(janela, "BotaoFiltros").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.True(painel.IsVisible);

            Achar<TextBox>(painel, "Filtro1Texto").Text = "souza";          // Nome contém "souza"
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, Cont(grade));

            Achar<TextBox>(painel, "Filtro2De").Text = "1000";               // Valor de 1.000
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, Cont(grade));
            Assert.Equal("Filtros (2)", Achar<Button>(janela, "BotaoFiltros").Content);

            Achar<ComboBox>(painel, "Filtro4SimNao").SelectedIndex = 2;      // Ativo = Não
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, Cont(grade));
            Assert.Equal("0 de 3 registros", Achar<TextBlock>(janela, "ContadorFiltro").Text);

            barra.Limpar();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3, Cont(grade));
            Assert.Equal("Filtros", Achar<Button>(janela, "BotaoFiltros").Content);
            await Task.Yield();
            janela.Close();
            return true;
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }

    [Fact]
    public async Task Ctrl_F_foca_a_busca_e_o_filtro_fica_lembrado_na_sessao()
    {
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            var (janela, grade, barra, _) = Montar();
            grade.Focus();
            janela.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, "f");
            Dispatcher.UIThread.RunJobs();
            var busca = Achar<TextBox>(janela, "BuscaRapida");
            Assert.True(busca.IsFocused);

            busca.Text = "maria";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, Cont(grade));
            janela.Close();

            var (janela2, grade2, _, _) = Montar();                       // mesma tela reaberta: lembra a busca
            Assert.Equal("maria", Achar<TextBox>(janela2, "BuscaRapida").Text);
            Assert.Equal(1, Cont(grade2));
            Achar<Button>(janela2, "BotaoLimparFiltros").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Task.Yield();
            janela2.Close();
            return true;
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }

    [Fact]
    public async Task Grade_de_digitacao_nao_ganha_filtro()
    {
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            var grade = new DataGrid { ItemsSource = Dados, AutoGenerateColumns = false };   // não é somente leitura
            grade.Columns.Add(new DataGridTextColumn { Header = "Nome", Binding = new Binding("Nome") });
            var painel = new StackPanel();
            painel.Children.Add(grade);
            var janela = new Window { Content = painel, Width = 600, Height = 300 };
            janela.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.Null(FiltroGrade.ObterBarra(grade));
            await Task.Yield();
            janela.Close();
            return true;
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }

    [Fact]
    public async Task Telas_de_todos_os_modulos_ganham_a_barra_nas_listas()
    {
        var resultado = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            var montagens = new[]
            {
                typeof(Vorticis.Desktop.Views.BalanceteView).Assembly, typeof(Vorticis.Fiscal.Desktop.Views.DuplicatasView).Assembly,
                typeof(Vorticis.Folha.Desktop.Views.FuncionariosView).Assembly, typeof(R.Views.EstoqueView).Assembly,
                typeof(E.Views.CarteiraView).Assembly, typeof(K.Views.CobrancasView).Assembly,
            };
            var porModulo = new Dictionary<string, int>();
            foreach (var asm in montagens)
            {
                var n = 0;
                foreach (var t in asm.GetTypes().Where(t => typeof(UserControl).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) is not null && t.Name.EndsWith("View")))
                {
                    UserControl view;
                    try { view = (UserControl)Activator.CreateInstance(t)!; } catch (TargetInvocationException) { continue; }
                    var janela = new Window { Content = view, Width = 1000, Height = 700 };
                    janela.Show();
                    Dispatcher.UIThread.RunJobs();
                    foreach (var g in view.GetVisualDescendants().OfType<DataGrid>().Where(g => g.IsReadOnly))
                    {
                        Assert.NotNull(FiltroGrade.ObterBarra(g));   // toda lista somente leitura tem a barra
                        n++;
                    }
                    janela.Close();
                }
                porModulo[asm.GetName().Name!] = n;
            }
            await Task.Yield();
            return porModulo;
        }, TestContext.Current.CancellationToken));
        foreach (var (modulo, n) in resultado.Select(p => (p.Key, p.Value))) Assert.True(n > 0, $"{modulo}: nenhuma lista com filtro");
    }
}
