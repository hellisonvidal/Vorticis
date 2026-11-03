using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Folha.Desktop.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Incluir, Alterar, duplo clique ou Enter na lista).</summary>
public partial class CargosView : UserControl
{
    public CargosView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaCargos, () => new CargoFichaView(), 480, 380);
    }
}
