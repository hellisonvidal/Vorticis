using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Folha.Desktop.Views;

/// <summary>Eventos variáveis da competência; o lançamento abre em janela (botão Novo lançamento).</summary>
public partial class EventosVariaveisView : UserControl
{
    public EventosVariaveisView()
    {
        InitializeComponent();
        FichaPopup.Ligar(this, () => new EventoVariavelFichaView(), 900, 300);
    }
}
