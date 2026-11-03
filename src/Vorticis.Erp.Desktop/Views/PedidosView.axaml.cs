using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Erp.Desktop.Views;

/// <summary>Lista dos pedidos; a ficha abre em janela (Novo pedido, Abrir ficha, duplo clique ou Enter na lista).</summary>
public partial class PedidosView : UserControl
{
    public PedidosView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaPedidos, () => new PedidoFichaView(), 620, 820);
    }
}
