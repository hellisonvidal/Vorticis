using Avalonia.Controls;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Incluir, Alterar, duplo clique ou Enter na lista).</summary>
public partial class SociosView : UserControl
{
    public SociosView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaSocios, () => new SocioFichaView(), 520, 720);
    }
}
