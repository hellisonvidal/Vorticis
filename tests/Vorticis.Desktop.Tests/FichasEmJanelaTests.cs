using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Desktop.Comum.Views;
using C = Vorticis.Desktop.Comum;

namespace Vorticis.Desktop.Tests;

/// <summary>
/// Cadastros de todos os módulos: a tela mostra só a lista (consultas) e o formulário do registro abre numa janela por cima
/// (Incluir/Novo, Alterar, duplo clique ou Enter); Salvar fecha, Cancelar, Esc ou X desistem.
/// </summary>
public class FichasEmJanelaTests(SessaoHeadless headless)
{
    private static async Task<T> ForaDaSessao<T>(Task<T> tarefa)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = tarefa.ContinueWith(t => { if (t.IsFaulted) tcs.SetException(t.Exception!.InnerExceptions); else tcs.SetResult(t.Result); }, TaskScheduler.Default);
        return await tcs.Task;
    }

    private static Window Mostrar(UserControl tela, object vm)
    {
        tela.DataContext = vm;
        var j = new Window { Content = tela, Width = 1200, Height = 800 };
        j.Show();
        Dispatcher.UIThread.RunJobs();
        return j;
    }

    /// <summary>Tudo o que é campo de edição aparece inteiro dentro da ficha (nada cortado nem sem largura).</summary>
    private static void ConferirCampos(Window ficha)
    {
        ficha.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var raiz = (Control)ficha.Content!;
        foreach (var c in raiz.GetVisualDescendants().OfType<Control>().Where(x => x.IsEffectivelyVisible && x is TextBox or ComboBox or CalendarDatePicker or NumericUpDown))
        {
            Assert.True(c.Bounds.Width > 20 && c.Bounds.Height > 10, $"{c.GetType().Name} sem tamanho: {c.Bounds}");
            var p = c.TranslatePoint(new Point(0, 0), raiz);
            Assert.True(p is not null && p.Value.X >= 0 && p.Value.X + c.Bounds.Width <= raiz.Bounds.Width + 1, $"{c.GetType().Name} cortado: x={p?.X} largura={c.Bounds.Width} ficha={raiz.Bounds.Width}");
        }
    }

    [Fact]
    public async Task Cadastros_da_barra_padrao_abrem_a_ficha_em_janela_e_fecham_ao_cancelar_ou_pelo_X()
    {
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            await Task.CompletedTask;
            var cont = new Vorticis.Desktop.ViewModels.MainViewModel();
            var folha = new Vorticis.Folha.Desktop.MainViewModel();

            // (tela, vm, tipo esperado da ficha, título, controles que não podem ficar na lista)
            var casos = new (UserControl Tela, CadastroViewModel Vm, Type Ficha, string Titulo)[]
            {
                (new C.Views.CentrosCustoView(), new CentrosCustoViewModel(cont), typeof(C.Views.CentroCustoFichaView), "Incluir centro de custo"),
                (new C.Views.ContasBancariasView(), new ContasBancariasViewModel(cont), typeof(C.Views.ContaBancariaFichaView), "Incluir conta bancária"),
                (new C.Views.FormasPagamentoView(), new FormasPagamentoViewModel(cont), typeof(C.Views.FormaPagamentoFichaView), "Incluir forma de pagamento"),
                (new C.Views.UsuariosView(), new UsuariosViewModel(cont), typeof(C.Views.UsuarioFichaView), "Incluir usuário"),
                (new C.Views.PerfisView(), new PerfisViewModel(cont), typeof(C.Views.PerfilFichaView), "Incluir perfil"),
                (new C.Views.ParticipantesView(), new ClientesViewModel(cont), typeof(C.Views.ParticipanteFichaView), "Incluir cliente"),
                (new C.Views.CadastroEmpresaView(), new CadastroEmpresaViewModel(cont), typeof(C.Views.EmpresaFichaView), "Incluir empresa"),
                (new C.Views.SociosView(), new SociosViewModel(cont), typeof(C.Views.SocioFichaView), "Incluir sócio"),
                (new C.Views.ContabilidadeEmpresaView(), new ContabilidadeEmpresaViewModel(cont), typeof(C.Views.ContadorFichaView), "Incluir vínculo"),
                (new Vorticis.Desktop.Views.GruposDepreciacaoView(), new Vorticis.Desktop.ViewModels.GruposDepreciacaoViewModel(cont), typeof(Vorticis.Desktop.Views.GrupoDepreciacaoFichaView), "Incluir grupo"),
                (new Vorticis.Desktop.Views.BensView(), new Vorticis.Desktop.ViewModels.BensViewModel(cont), typeof(Vorticis.Desktop.Views.BemFichaView), "Incluir bem"),
                (new Vorticis.Desktop.Views.HistoricosView(), new Vorticis.Desktop.ViewModels.HistoricosViewModel(cont), typeof(Vorticis.Desktop.Views.HistoricoFichaView), "Incluir histórico"),
                (new Vorticis.Folha.Desktop.Views.CargosView(), new Vorticis.Folha.Desktop.ViewModels.CargosViewModel(folha), typeof(Vorticis.Folha.Desktop.Views.CargoFichaView), "Incluir cargo"),
                (new Vorticis.Folha.Desktop.Views.FuncionariosView(), new Vorticis.Folha.Desktop.ViewModels.FuncionariosViewModel(folha), typeof(Vorticis.Folha.Desktop.Views.FuncionarioFichaView), "Incluir funcionário"),
            };
            foreach (var (tela, vm, tipoFicha, titulo) in casos)
            {
                var nome = vm.GetType().Name;
                var janela = Mostrar(tela, vm);
                Assert.Null(FichaPopup.Aberta(tela));
                // a tela fica só com a lista: nenhum campo de edição (caixa de seleção, data, número, marcação) nela
                Assert.DoesNotContain(tela.GetVisualDescendants().OfType<Control>(), t => t.IsEffectivelyVisible && t is ComboBox or CalendarDatePicker or NumericUpDown or CheckBox);

                vm.Modo = ModoCadastro.Inclusao;
                Dispatcher.UIThread.RunJobs();
                var ficha = FichaPopup.Aberta(tela);
                Assert.True(ficha is not null, nome);
                Assert.IsType(tipoFicha, ficha.Content);
                Assert.StartsWith(titulo.Split(' ')[0], ficha.Title ?? "");
                ConferirCampos(ficha);

                vm.CancelarCommand.Execute(null);                                    // Cancelar fecha a janela
                Dispatcher.UIThread.RunJobs();
                Assert.True(FichaPopup.Aberta(tela) is null && !vm.FichaAberta, nome);

                vm.Modo = ModoCadastro.Inclusao;                                     // o X desiste da edição
                Dispatcher.UIThread.RunJobs();
                FichaPopup.Aberta(tela)!.Close();
                Dispatcher.UIThread.RunJobs();
                Assert.True(vm.Modo == ModoCadastro.Consulta && !vm.FichaAberta, nome);
                janela.Close();
            }
            return true;
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }

    [Fact]
    public async Task Contadores_incluir_mostra_um_tipo_por_vez_com_campos_alinhados_dentro_da_janela()
    {
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            await Task.CompletedTask;
            var cont = new Vorticis.Desktop.ViewModels.MainViewModel();
            var vm = new ContabilidadeEmpresaViewModel(cont);
            var tela = new C.Views.ContabilidadeEmpresaView();
            var janela = Mostrar(tela, vm);
            vm.Modo = ModoCadastro.Inclusao;
            Dispatcher.UIThread.RunJobs();
            var ficha = FichaPopup.Aberta(tela)!;
            Assert.IsType<C.Views.ContadorFichaView>(ficha.Content);
            var raiz = (Control)ficha.Content!;
            Assert.Equal(2, raiz.GetVisualDescendants().OfType<RadioButton>().Count(r => r.IsEffectivelyVisible));            // Contador | Escritório contábil

            // Contador: nome, CPF, CRC/UF, e-mail, telefone — os rótulos ficam alinhados na mesma coluna
            vm.IncluirContador = true;
            ficha.UpdateLayout();
            var rotulos = raiz.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Classes.Contains("rotulo")).ToList();
            Assert.NotEmpty(rotulos);
            var xs = rotulos.Select(t => Math.Round(t.TranslatePoint(new Point(0, 0), raiz)!.Value.X)).Distinct().ToList();
            Assert.Single(xs);
            ConferirCampos(ficha);
            var cnpjContador = raiz.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && t.Text == "CNPJ");
            Assert.False(cnpjContador);

            // Escritório contábil: só os campos do escritório
            vm.IncluirContador = false;
            ficha.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(raiz.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "CNPJ");
            Assert.DoesNotContain(raiz.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "CPF");
            ConferirCampos(ficha);
            vm.CancelarCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Null(FichaPopup.Aberta(tela));
            janela.Close();
            return true;
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }

    [Fact]
    public async Task Telas_com_botoes_proprios_abrem_a_ficha_em_janela_e_so_fecham_ao_gravar_sem_erro()
    {
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            async Task Calma() { for (var i = 0; i < 10; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); } }
            var fiscal = new Vorticis.Fiscal.Desktop.MainViewModel();
            var cond = new Vorticis.Condominio.Desktop.MainViewModel();

            // Fiscal › Produtos: Novo abre a ficha; Gravar sem banco dá erro e a janela continua aberta; Cancelar fecha
            var produtos = new Vorticis.Fiscal.Desktop.ViewModels.ProdutosViewModel(fiscal);
            var telaP = new Vorticis.Fiscal.Desktop.Views.ProdutosView();
            var j1 = Mostrar(telaP, produtos);
            Assert.Null(FichaPopup.Aberta(telaP));
            Assert.DoesNotContain(telaP.GetVisualDescendants().OfType<TextBox>(), t => t.IsEffectivelyVisible && t.Watermark is "GTIN/EAN");   // o formulário não fica na tela
            produtos.NovoCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            var ficha = FichaPopup.Aberta(telaP);
            Assert.IsType<Vorticis.Fiscal.Desktop.Views.ProdutoFichaView>(ficha!.Content);
            Assert.Equal("Novo produto", ficha.Title);
            ConferirCampos(ficha);
            await produtos.SalvarCommand.ExecuteAsync(null);
            await Calma();
            Assert.NotEmpty(produtos.Erros);
            Assert.NotNull(FichaPopup.Aberta(telaP));                                        // erro: a ficha continua aberta
            produtos.FecharFichaCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Null(FichaPopup.Aberta(telaP));
            j1.Close();

            // Abrir ficha sem selecionar nada avisa e não abre a janela
            var regras = new Vorticis.Fiscal.Desktop.ViewModels.RegrasFiscaisViewModel(fiscal);
            var telaR = new Vorticis.Fiscal.Desktop.Views.RegrasFiscaisView();
            var j2 = Mostrar(telaR, regras);
            regras.AbrirFichaCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Null(FichaPopup.Aberta(telaR));
            Assert.Contains("Selecione", Assert.Single(regras.Erros));
            regras.NovaCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.IsType<Vorticis.Fiscal.Desktop.Views.RegraFiscalFichaView>(FichaPopup.Aberta(telaR)!.Content);
            FichaPopup.Aberta(telaR)!.Close();                                               // X
            Dispatcher.UIThread.RunJobs();
            Assert.False(regras.FichaAberta);
            j2.Close();

            // Condomínio › Taxas
            var taxas = new Vorticis.Condominio.Desktop.ViewModels.TaxasViewModel(cond);
            var telaT = new Vorticis.Condominio.Desktop.Views.TaxasView();
            var j3 = Mostrar(telaT, taxas);
            taxas.NovaCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.IsType<Vorticis.Condominio.Desktop.Views.TaxaFichaView>(FichaPopup.Aberta(telaT)!.Content);
            taxas.FecharFichaCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Null(FichaPopup.Aberta(telaT));
            j3.Close();
            return true;
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }
}
