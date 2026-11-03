using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Desktop.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Incluir, Alterar, duplo clique ou Enter na lista).</summary>
public partial class GruposDepreciacaoView : UserControl
{
    public GruposDepreciacaoView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaGrupos, () => new GrupoDepreciacaoFichaView(), 560, 560);
    }
}
