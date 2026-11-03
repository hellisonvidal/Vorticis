using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Desktop.Views;

/// <summary>Lista dos históricos padrão; a ficha abre em janela (Incluir, Alterar, duplo clique ou Enter na lista).</summary>
public partial class HistoricosView : UserControl
{
    public HistoricosView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaHistoricos, () => new HistoricoFichaView(), 560, 560);
    }
}
