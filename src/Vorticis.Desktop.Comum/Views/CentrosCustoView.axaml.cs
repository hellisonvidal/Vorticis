using Avalonia.Controls;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Lista dos centros de custo; a ficha abre em janela (Incluir, Alterar, duplo clique ou Enter na lista).</summary>
public partial class CentrosCustoView : UserControl
{
    public CentrosCustoView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaCentros, () => new CentroCustoFichaView(), 520, 440);
    }
}
