using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Erp.Desktop.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Nova, Abrir ficha, duplo clique ou Enter na lista).</summary>
public partial class TransporteView : UserControl
{
    public TransporteView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaTransporte, () => new TransporteFichaView(), 560, 740);
    }
}
