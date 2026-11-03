using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Fiscal.Desktop.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Novo, Abrir ficha, duplo clique ou Enter na lista).</summary>
public partial class ProdutosView : UserControl
{
    public ProdutosView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaProdutos, () => new ProdutoFichaView(), 900, 520);
    }
}
