using Avalonia.Controls;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Incluir, Alterar, duplo clique ou Enter na lista).</summary>
public partial class ContasBancariasView : UserControl
{
    public ContasBancariasView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, Lista, () => new ContaBancariaFichaView(), 560, 500);
    }
}
