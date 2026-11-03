using Avalonia.Controls;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Incluir, Alterar, duplo clique ou Enter na lista).</summary>
public partial class PerfisView : UserControl
{
    public PerfisView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaPerfis, () => new PerfilFichaView(), 760, 760);
    }
}
