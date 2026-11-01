using Avalonia.Controls;
using Avalonia.Input;
using Vorticis.Desktop.Comum.Views;
using Vorticis.Escritorio.Desktop.ViewModels;

namespace Vorticis.Escritorio.Desktop.Views;

/// <summary>Lista dos clientes; a ficha abre em janela (Incluir, Alterar, duplo clique ou Enter na lista).</summary>
public partial class ClientesView : UserControl
{
    public ClientesView()
    {
        InitializeComponent();
        FichaPopup.Ligar(this, () => new ClienteFichaView(), 760, 720);
        ListaClientes.DoubleTapped += (_, _) => (DataContext as ClientesEscritorioViewModel)?.AbrirFichaCommand.Execute(null);
        ListaClientes.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && DataContext is ClientesEscritorioViewModel vm && vm.AbrirFichaCommand.CanExecute(null))
            {
                vm.AbrirFichaCommand.Execute(null);
                e.Handled = true;
            }
        };
    }
}
