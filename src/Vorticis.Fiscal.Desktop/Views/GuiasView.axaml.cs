using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Fiscal.Desktop.Views;

/// <summary>Tela de consulta; o lançamento manual abre em janela (botão Nova guia (manual)).</summary>
public partial class GuiasView : UserControl
{
    public GuiasView()
    {
        InitializeComponent();
        FichaPopup.Ligar(this, () => new GuiaManualFichaView(), 900, 300);
    }
}
