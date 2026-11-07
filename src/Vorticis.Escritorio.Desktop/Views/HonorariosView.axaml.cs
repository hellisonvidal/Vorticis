using Avalonia.Controls;
using Avalonia.Input;
using Vorticis.Desktop.Comum.Views;
using Vorticis.Escritorio.Desktop.ViewModels;

namespace Vorticis.Escritorio.Desktop.Views;

/// <summary>Financeiro › Honorários: duplo clique ou Enter numa cobrança em aberto abre a baixa.</summary>
public partial class HonorariosView : UserControl
{
    public HonorariosView()
    {
        InitializeComponent();
        ListaCobrancas.DoubleTapped += (_, _) => (DataContext as HonorariosViewModel)?.BaixarCommand.Execute(null);
        ListaCobrancas.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && DataContext is HonorariosViewModel { Selecionada: not null } vm) { vm.BaixarCommand.Execute(null); e.Handled = true; }
        };
    }
}

/// <summary>Ponto da tela que abre a janela "Novo honorário" / "Cobrança avulsa" (DataContext: LancamentoCobrancaViewModel).</summary>
public sealed class LancamentoCobrancaHost : UserControl
{
    public LancamentoCobrancaHost() => FichaPopup.Ligar(this, () => new LancamentoCobrancaView(), 640, 640);
}

/// <summary>Ponto da tela que abre a janela "Baixar cobrança" (DataContext: BaixaCobrancaViewModel).</summary>
public sealed class BaixaCobrancaHost : UserControl
{
    public BaixaCobrancaHost() => FichaPopup.Ligar(this, () => new BaixaCobrancaView(), 560, 380);
}
