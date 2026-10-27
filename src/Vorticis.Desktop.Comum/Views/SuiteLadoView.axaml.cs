using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Vorticis.Desktop.Comum.ViewModels;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>
/// Navegação à esquerda da Suite (B e C): a faixa de ícones abre a navegação por cima das telas quando o mouse chega e a fecha pouco
/// depois que ele sai (ou quando uma função é escolhida). Fixada, fica aberta no lugar da faixa.
/// </summary>
public partial class SuiteLadoView : UserControl
{
    private readonly DispatcherTimer _recolher = new() { Interval = TimeSpan.FromMilliseconds(380) };

    public SuiteLadoView()
    {
        InitializeComponent();
        _recolher.Tick += (_, _) =>
        {
            _recolher.Stop();
            if (DataContext is SuiteViewModel s && !Rail.IsPointerOver && !NavFlutuante.IsPointerOver && !MenuAberto()) s.LateralAberta = false;
        };
        Rail.PointerEntered += (_, _) => Abrir();
        Rail.PointerPressed += (_, _) => Abrir();
        Rail.PointerExited += (_, _) => _recolher.Start();
        NavFlutuante.PointerEntered += (_, _) => _recolher.Stop();
        NavFlutuante.PointerExited += (_, _) => _recolher.Start();
    }

    private void Abrir()
    {
        _recolher.Stop();
        if (DataContext is SuiteViewModel s) s.LateralAberta = true;
    }

    /// <summary>Com algum menu da navegação aberto, ela não recolhe.</summary>
    private bool MenuAberto() => this.GetVisualDescendants().OfType<Button>().Any(b => b.Flyout?.IsOpen == true);

    /// <summary>Ctrl+K: abre a navegação (se estiver oculta) e põe o cursor na busca.</summary>
    public void FocarBusca()
    {
        if (DataContext is SuiteViewModel s && s.MostraRail) s.LateralAberta = true;
        Dispatcher.UIThread.Post(() => (NavFixa.IsVisible ? NavFixa : NavFlutuante).Busca.Focus());
    }
}
