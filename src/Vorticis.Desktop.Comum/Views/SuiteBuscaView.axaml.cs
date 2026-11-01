using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Vorticis.Desktop.Comum.ViewModels;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Busca central da Suite (visual 9, e Ctrl+K nos outros visuais): Enter abre o primeiro resultado; Esc fecha a busca rápida.</summary>
public partial class SuiteBuscaView : UserControl
{
    public SuiteBuscaView()
    {
        InitializeComponent();
        CampoBuscaCentral.KeyDown += (_, e) =>
        {
            if (DataContext is not SuiteViewModel s) return;
            if (e.Key == Key.Enter) { s.AbrirPrimeiroResultadoCommand.Execute(null); e.Handled = true; }
            else if (e.Key == Key.Escape && !s.EhBusca) { s.FecharBuscaRapidaCommand.Execute(null); e.Handled = true; }
            else if (e.Key == Key.Escape) { s.Busca = ""; e.Handled = true; }
        };
    }

    public void Focar() => Dispatcher.UIThread.Post(() => CampoBuscaCentral.Focus(), DispatcherPriority.Input);
}
