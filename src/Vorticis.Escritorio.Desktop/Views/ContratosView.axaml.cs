using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Escritorio.Desktop.Views;

/// <summary>Lista dos contratos; a ficha abre em janela (Novo contrato, Abrir ficha, duplo clique ou Enter na lista).</summary>
public partial class ContratosView : UserControl
{
    public ContratosView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaContratos, () => new ContratoFichaView(), 640, 820);
    }
}
