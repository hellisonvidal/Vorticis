using Avalonia.Controls;
using Avalonia.Input;
using Vorticis.Desktop.Comum.Views;
using Vorticis.Desktop.ViewModels;

namespace Vorticis.Desktop.Views;

/// <summary>Lista das contas; a ficha da conta abre em janela (Incluir, Alterar, duplo clique ou Enter na lista).</summary>
public partial class PlanoContasView : UserControl
{
    public PlanoContasView()
    {
        InitializeComponent();
        FichaPopup.Ligar(this, () => new PlanoContaFichaView(), 680, 680);
        ListaContas.DoubleTapped += (_, _) => (DataContext as PlanoContasViewModel)?.AbrirFichaCommand.Execute(null);
        ListaContas.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && DataContext is PlanoContasViewModel vm && vm.AbrirFichaCommand.CanExecute(null)) { vm.AbrirFichaCommand.Execute(null); e.Handled = true; }
        };
    }
}
