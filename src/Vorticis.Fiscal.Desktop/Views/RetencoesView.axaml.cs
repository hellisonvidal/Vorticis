using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Fiscal.Desktop.Views;

/// <summary>Tela de consulta; o lançamento manual abre em janela (botão Nova retenção (manual)).</summary>
public partial class RetencoesView : UserControl
{
    public RetencoesView()
    {
        InitializeComponent();
        FichaPopup.Ligar(this, () => new RetencaoManualFichaView(), 900, 300);
    }
}
