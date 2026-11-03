using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vorticis.Desktop.ViewModels;
using Vorticis.Desktop.Views;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra.Tests;

namespace Vorticis.Desktop.Tests;

/// <summary>Lançamento contábil: "+ Mais partidas" (linhas compactas), rodapé com a diferença e centro de custo só quando a empresa usa.</summary>
public class LancamentoPartidasTests(BancoDeTeste db, SessaoHeadless headless) : IClassFixture<BancoDeTeste>
{
    private async Task<(LancamentoViewModel Vm, long Empresa, long Adm)> CriarAsync()
    {
        BancoDeTeste.ExigirBanco();
        var empresa = await db.CriarEmpresaAsync(CnpjsDeTeste.Novo(), 2026);
        var adm = await db.AdminAsync();
        var ctx = new ContextoDeTeste(db.App, new SessaoUsuario(adm, "admin_teste", "Admin de Teste", true, false), empresa);
        var vm = new LancamentoViewModel(ctx);
        await vm.CarregarAsync();
        return (vm, empresa, adm);
    }

    [Fact]
    public async Task Mais_partidas_viram_linhas_compactas_e_um_unico_lancamento_que_precisa_fechar()
    {
        var (vm, empresa, adm) = await CriarAsync();
        var receitas = vm.TodasContas.Where(c => c.Codigo.StartsWith("3.1.01")).Select(c => c.Codigo).Take(2).ToList();
        Assert.Equal(2, receitas.Count);
        Assert.NotNull(vm.Principal);
        Assert.Empty(vm.Extras);
        Assert.False(vm.TemExtras);

        var p = vm.Principal!;
        (p.Data, p.Conta, p.Valor, p.Documento, p.Historico) = ("10/03/2026", "1.1.01.001", "300,00", "NF 77", "Venda");
        vm.NovaPartidaCommand.Execute(null);
        Assert.Equal(2, vm.Linhas.Count);
        var primeira = Assert.Single(vm.Extras);
        Assert.True(vm.TemExtras);
        Assert.Same(p, vm.Principal);
        Assert.Equal("300,00", primeira.Valor);                         // sugere o valor que fecha
        Assert.Equal("NF 77", primeira.Documento);                      // repete o documento e o histórico
        Assert.Equal("Venda", primeira.Historico);
        (primeira.ContaCredito, primeira.Valor) = (receitas[0], "200,00");
        Assert.Equal(("300,00", "200,00", "100,00"), (vm.TotalDebito, vm.TotalCredito, vm.Diferenca));
        Assert.False(vm.Balanceado);

        await vm.SalvarCommand.ExecuteAsync(null);                       // não fecha: nada é gravado
        Assert.Contains("não fecham", Assert.Single(vm.Erros));
        Assert.Empty(vm.Efetuados);

        vm.NovaPartidaCommand.Execute(null);
        var segunda = vm.Extras[1];
        Assert.Equal("100,00", segunda.Valor);
        segunda.ContaCredito = receitas[1];
        Assert.Equal("0,00", vm.Diferenca);
        Assert.True(vm.Balanceado);

        await vm.SalvarCommand.ExecuteAsync(null);
        Assert.Empty(vm.Erros);
        Assert.Equal("Lançamento nº 1 salvo.", vm.Mensagem);
        var efetuado = Assert.Single(vm.Efetuados);                      // um só lançamento, com as três partidas
        var salvo = (await db.App.Lancamentos.ObterAsync(empresa, efetuado.Id, adm, TestContext.Current.CancellationToken)).Valor;
        Assert.Equal(3, salvo.Partidas.Count);
        Assert.Equal(300m, salvo.Partidas.Where(x => x.DC == Domain.Contabil.IndicadorDC.D).Sum(x => x.Valor));
        Assert.All(salvo.Partidas, x => Assert.Equal("NF 77", x.NumeroArquivo));
        Assert.Single(vm.Linhas);                                        // formulário limpo para o próximo
        Assert.Empty(vm.Extras);
    }

    [Fact]
    public async Task Remover_partida_tira_a_linha_compacta_mas_nunca_o_formulario()
    {
        var (vm, _, _) = await CriarAsync();
        vm.NovaPartidaCommand.Execute(null);
        vm.NovaPartidaCommand.Execute(null);
        Assert.Equal(2, vm.Extras.Count);
        var primeira = vm.Extras[0];
        vm.RemoverPartidaCommand.Execute(primeira);
        Assert.Single(vm.Extras);
        Assert.DoesNotContain(primeira, vm.Linhas);
        vm.RemoverPartidaCommand.Execute(vm.Principal);                  // o formulário não sai
        Assert.Equal(2, vm.Linhas.Count);
        vm.LimparCommand.Execute(null);
        Assert.Empty(vm.Extras);
        Assert.Single(vm.Linhas);
    }

    [Fact]
    public async Task Uma_linha_com_debito_e_credito_continua_sendo_um_lancamento_simples()
    {
        var (vm, empresa, adm) = await CriarAsync();
        var p = vm.Principal!;
        (p.Data, p.Conta, p.ContaCredito, p.Valor, p.Historico) = ("11/03/2026", "1.1.01.001", "3.1.01.001", "50,00", "Venda à vista");
        await vm.SalvarCommand.ExecuteAsync(null);
        Assert.Empty(vm.Erros);
        var efetuado = Assert.Single(vm.Efetuados);
        Assert.Equal(2, (await db.App.Lancamentos.ObterAsync(empresa, efetuado.Id, adm, TestContext.Current.CancellationToken)).Valor.Partidas.Count);
    }

    [Fact]
    public async Task Centro_de_custo_so_aparece_quando_a_empresa_usa()
    {
        var (vm, empresa, _) = await CriarAsync();
        Assert.False(vm.UsaCentroCusto);
        await db.EscalarAsync<int>("UPDATE core.empresa SET usa_centro_custo = TRUE WHERE id = @e RETURNING 1", ("e", empresa));
        await vm.CarregarAsync();
        Assert.True(vm.UsaCentroCusto);
    }

    [Fact]
    public async Task Pop_up_de_digitacao_sem_painel_de_contas_com_lupas_nas_contas_e_mais_partidas_compactas()
    {
        var (vm, empresa, _) = await CriarAsync();
        var ok = await Task.Run(() => headless.Sessao.Dispatch(() =>
        {
            var view = new LancamentoDigitacaoView { DataContext = vm };
            var janela = new Window { Content = view, Width = 1100, Height = 660 };
            janela.Show();
            Dispatcher.UIThread.RunJobs();
            List<AutoCompleteBox> Contas() => view.GetVisualDescendants().OfType<AutoCompleteBox>().Where(a => a.Tag is "conta" or "contacredito").ToList();
            bool Visivel(string tag) => view.GetVisualDescendants().OfType<TextBox>().Any(t => t.Tag as string == tag && t.IsEffectivelyVisible);

            Assert.Equal(2, Contas().Count);                                              // débito e crédito do formulário
            Assert.All(Contas(), c => Assert.IsType<Button>(c.GetVisualDescendants().OfType<TextBox>().First().InnerRightContent));   // lupa em cada um
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Contas analíticas");                  // sem o painel do plano
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Partidas deste lançamento");          // sem a grade grande
            Assert.False(Visivel("centrocusto"));                                         // empresa sem centros de custo
            Assert.Contains(view.GetVisualDescendants().OfType<Button>(), b => b.Content as string == "+ Mais partidas");

            vm.NovaPartidaCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(4, Contas().Count);                                              // mais uma linha compacta (débito e crédito)
            Assert.All(Contas(), c => Assert.IsType<Button>(c.GetVisualDescendants().OfType<TextBox>().First().InnerRightContent));

            vm.UsaCentroCusto = true;
            Dispatcher.UIThread.RunJobs();
            Assert.True(Visivel("centrocusto"));
            janela.Close();
            return true;
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
        Assert.NotEqual(0, empresa);
    }
}
