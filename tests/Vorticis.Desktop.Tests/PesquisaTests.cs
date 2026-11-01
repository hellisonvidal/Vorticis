using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Desktop.Tests;

/// <summary>Pesquisa dentro dos campos (lupa / F4) e nas listas de escolha com muitas opções.</summary>
public class PesquisaTests(SessaoHeadless headless)
{
    private static readonly ItemPesquisa[] Contas =
    [
        new("1.1.01.001", "[10] Caixa geral"), new("1.1.02.001", "[20] Banco Itaú conta movimento"),
        new("2.1.01.001", "[30] Fornecedores nacionais"), new("3.1.01.001", "[40] Receita de serviços"),
    ];

    [Theory]
    [InlineData("caixa", "1.1.01.001")]
    [InlineData("SERVICOS", "3.1.01.001")]           // sem acento e sem diferença de maiúsculas
    [InlineData("banco itau", "1.1.02.001")]         // todas as palavras, em qualquer ordem e parte
    [InlineData("2101", "2.1.01.001")]               // código sem os pontos
    public void Filtra_pelo_codigo_ou_pela_descricao(string filtro, string esperado) =>
        Assert.Equal(esperado, Assert.Single(Pesquisa.Filtrar(Contas, filtro)).Codigo);

    [Fact]
    public void Filtro_vazio_mostra_tudo_e_fontes_comuns_estao_registradas()
    {
        Assert.Equal(4, Pesquisa.Filtrar(Contas, "").Count);
        Assert.Empty(Pesquisa.Filtrar(Contas, "inexistente"));
        Assert.All(new[] { "Conta", "Historico", "Participante", "ParticipanteCodigo", "CentroCusto" }, f => Assert.True(Pesquisa.Registrada(f)));
    }

    [Fact]
    public async Task Lista_de_escolha_com_muitas_opcoes_ganha_caixa_de_pesquisa_que_esconde_o_que_nao_combina()
    {
        var ok = await Task.Run(() => headless.Sessao.Dispatch(() =>
        {
            PesquisaCombo.Ativar();
            var meses = new[] { "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro" };
            var combo = new ComboBox { ItemsSource = meses, Width = 200 };
            var janela = new Window { Content = new StackPanel { Children = { combo } }, Width = 400, Height = 600 };
            janela.Show();
            combo.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            var popup = combo.GetVisualDescendants().OfType<Popup>().First(p => p.Name == "PART_Popup");
            var caixa = popup.Child!.GetVisualDescendants().OfType<TextBox>().Single();
            Assert.Equal("Pesquisar na lista", caixa.Watermark);

            caixa.Text = "marco";                                   // sem acento
            Dispatcher.UIThread.RunJobs();
            var visiveis = Enumerable.Range(0, meses.Length).Select(combo.ContainerFromIndex).Where(c => c is { IsVisible: true }).Count();
            Assert.Equal(1, visiveis);
            caixa.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            Assert.Equal("Março", combo.SelectedItem);
            Assert.False(combo.IsDropDownOpen);

            combo.IsDropDownOpen = true;                            // reabre limpa o filtro
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("", caixa.Text);
            Assert.All(Enumerable.Range(0, meses.Length).Select(combo.ContainerFromIndex).OfType<Control>(), c => Assert.True(c.IsVisible));
            janela.Close();

            // lista curta não ganha a caixa
            var curta = new ComboBox { ItemsSource = new[] { "Sim", "Não" } };
            var j2 = new Window { Content = curta };
            j2.Show();
            curta.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(curta.GetVisualDescendants().OfType<Popup>().First().Child!.GetVisualDescendants().OfType<TextBox>());
            j2.Close();
            return true;
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }

    [Fact]
    public async Task Campo_com_pesquisa_tem_lupa_dentro_do_campo()
    {
        var ok = await Task.Run(() => headless.Sessao.Dispatch(() =>
        {
            var campo = new TextBox();
            Pesquisa.SetFonte(campo, "Conta");
            Assert.IsType<Button>(campo.InnerRightContent);
            Assert.Equal("Pesquisar (F4)", ToolTip.GetTip((Button)campo.InnerRightContent!));
            Pesquisa.SetFonte(campo, null);
            Assert.Null(campo.InnerRightContent);
            return true;
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }
}
