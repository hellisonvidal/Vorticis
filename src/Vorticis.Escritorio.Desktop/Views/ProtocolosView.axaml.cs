using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Escritorio.Desktop.Views;

/// <summary>Lista dos protocolos; Registrar protocolo abre a ficha em janela.</summary>
public partial class ProtocolosView : UserControl
{
    public ProtocolosView()
    {
        InitializeComponent();
        FichaPopup.Ligar(this, () => new ProtocoloFichaView(), 560, 440);
    }
}
