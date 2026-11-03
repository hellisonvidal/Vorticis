using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Folha.Desktop.Views;

/// <summary>Lista dos eventos contábeis; a ficha abre em janela (Novo, Abrir ficha, duplo clique ou Enter na lista).</summary>
public partial class EventosContabeisView : UserControl
{
    public EventosContabeisView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaEventos, () => new EventoContabilFichaView(), 520, 440);
    }
}
