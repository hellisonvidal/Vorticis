using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Folha.Desktop.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Incluir, Alterar, duplo clique ou Enter na lista).</summary>
public partial class DepartamentosView : UserControl
{
    public DepartamentosView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaDepartamentos, () => new DepartamentoFichaView(), 480, 360);
    }
}
