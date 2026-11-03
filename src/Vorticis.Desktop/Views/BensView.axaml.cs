using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Desktop.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Incluir, Alterar, duplo clique ou Enter na lista).</summary>
public partial class BensView : UserControl
{
    public BensView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaBens, () => new BemFichaView(), 640, 760);
    }
}
