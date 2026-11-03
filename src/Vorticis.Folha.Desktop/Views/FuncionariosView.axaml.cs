using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Folha.Desktop.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Incluir, Alterar, duplo clique ou Enter na lista).</summary>
public partial class FuncionariosView : UserControl
{
    public FuncionariosView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaFuncionarios, () => new FuncionarioFichaView(), 760, 720);
    }
}
