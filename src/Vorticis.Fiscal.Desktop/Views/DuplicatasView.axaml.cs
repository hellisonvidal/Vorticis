using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Fiscal.Desktop.Views;

/// <summary>Lista das duplicatas; a ficha abre em janela (Nova duplicata, Abrir ficha ou duplo clique na lista).</summary>
public partial class DuplicatasView : UserControl
{
    public DuplicatasView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaDuplicatas, () => new DuplicataFichaView(), 900, 380);
    }
}
