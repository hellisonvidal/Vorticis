using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Erp.Desktop.Views;

/// <summary>Posição do estoque; a ficha do produto (preço, mínimo e ajuste) abre em janela (Abrir ficha, duplo clique ou Enter na lista).</summary>
public partial class EstoqueView : UserControl
{
    public EstoqueView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaEstoque, () => new ProdutoEstoqueFichaView(), 480, 520);
    }
}
