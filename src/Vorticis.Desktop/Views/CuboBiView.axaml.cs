using Avalonia.Controls;
using Vorticis.Desktop.ViewModels;

namespace Vorticis.Desktop.Views;

public partial class CuboBiView : UserControl
{
    public CuboBiView()
    {
        InitializeComponent();
        GradeCubo.CelulaClicada += (_, c) => (DataContext as CuboBiViewModel)?.Detalhar(c.Linha, c.Coluna);
    }
}
