using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Condominio.Desktop.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Nova, Abrir ficha, duplo clique ou Enter na lista).</summary>
public partial class AtasAssembleiaView : UserControl
{
    public AtasAssembleiaView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaAtas, () => new AtaAssembleiaFichaView(), 560, 720);
    }
}
