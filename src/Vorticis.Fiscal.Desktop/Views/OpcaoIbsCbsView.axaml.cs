using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Fiscal.Desktop.Views;

/// <summary>Lista do histórico da opção IBS/CBS; a ficha abre em janela (Nova, Abrir ficha, duplo clique ou Enter na lista).</summary>
public partial class OpcaoIbsCbsView : UserControl
{
    public OpcaoIbsCbsView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaOpcoes, () => new OpcaoIbsCbsFichaView(), 760, 420);
    }
}
