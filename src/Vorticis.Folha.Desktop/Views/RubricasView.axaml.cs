using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Folha.Desktop.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Incluir, Alterar, duplo clique ou Enter na lista).</summary>
public partial class RubricasView : UserControl
{
    public RubricasView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaRubricas, () => new RubricaFichaView(), 560, 560);
    }
}
