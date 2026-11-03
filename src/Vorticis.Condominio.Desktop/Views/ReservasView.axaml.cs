using Avalonia.Controls;
using Vorticis.Condominio.Desktop.ViewModels;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Condominio.Desktop.Views;

/// <summary>Áreas comuns e reservas; as fichas abrem em janela (Nova área, Abrir ficha, duplo clique na área, Nova reserva).</summary>
public partial class ReservasView : UserControl
{
    public ReservasView()
    {
        InitializeComponent();
        FichaPopup.Ligar(this, () => new ReservaFichaView(), 520, 340);
        ListaAreas.DoubleTapped += (_, _) => (DataContext as ReservasViewModel)?.AbrirAreaCommand.Execute(null);
    }
}
