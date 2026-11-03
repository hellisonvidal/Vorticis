using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Fiscal.Desktop.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Novo, Abrir ficha, duplo clique ou Enter na lista).</summary>
public partial class ContasConsumoView : UserControl
{
    public ContasConsumoView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaContas, () => new ContaConsumoFichaView(), 900, 520);
    }
}
