using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Condominio.Desktop.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Nova, Abrir ficha, duplo clique ou Enter na lista).</summary>
public partial class UnidadesView : UserControl
{
    public UnidadesView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaUnidades, () => new UnidadeFichaView(), 560, 480);
    }
}
