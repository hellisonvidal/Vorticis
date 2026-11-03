using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Condominio.Desktop.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Nova, Abrir ficha, duplo clique ou Enter na lista).</summary>
public partial class DespesasView : UserControl
{
    public DespesasView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaDespesas, () => new DespesaFichaView(), 520, 460);
    }
}
