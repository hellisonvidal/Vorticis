using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Fiscal.Desktop.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Novo, Abrir ficha, duplo clique ou Enter na lista).</summary>
public partial class CodigosAjusteView : UserControl
{
    public CodigosAjusteView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaCodigos, () => new CodigoAjusteFichaView(), 760, 300);
    }
}
