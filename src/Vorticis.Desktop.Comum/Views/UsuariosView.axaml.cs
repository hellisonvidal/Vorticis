using Avalonia.Controls;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Incluir, Alterar, duplo clique ou Enter na lista).</summary>
public partial class UsuariosView : UserControl
{
    public UsuariosView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaUsuarios, () => new UsuarioFichaView(), 720, 760);
    }
}
