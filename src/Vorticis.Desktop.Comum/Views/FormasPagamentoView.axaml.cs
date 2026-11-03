using Avalonia.Controls;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Incluir, Alterar, duplo clique ou Enter na lista).</summary>
public partial class FormasPagamentoView : UserControl
{
    public FormasPagamentoView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, Lista, () => new FormaPagamentoFichaView(), 580, 600);
    }
}
