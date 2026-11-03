using Avalonia.Controls;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Calendário de obrigações; as obrigações personalizadas abrem em janela (botão Obrigações personalizadas).</summary>
public partial class ObrigacoesView : UserControl
{
    public ObrigacoesView()
    {
        InitializeComponent();
        FichaPopup.Ligar(this, () => new ObrigacaoPersonalizadaFichaView(), 560, 640);
    }
}
