using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Condominio.Desktop.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Nova, Abrir ficha, duplo clique ou Enter na lista).</summary>
public partial class TaxasView : UserControl
{
    public TaxasView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaTaxas, () => new TaxaFichaView(), 520, 460);
    }
}
