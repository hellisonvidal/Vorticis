using Avalonia.Controls;
using Avalonia.Layout;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Botão discreto "Layout" presente em todos os layouts; abre o seletor com as miniaturas.</summary>
public partial class SuiteEscolhaLayoutView : UserControl
{
    public SuiteEscolhaLayoutView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Seletor.DataContext = DataContext;
        BotaoLayout.Flyout!.Opened += (_, _) => Seletor.DataContext = DataContext;
    }

    /// <summary>Só o ícone (faixa de ícones à esquerda).</summary>
    public bool Compacto
    {
        get => !RotuloLayout.IsVisible;
        set { RotuloLayout.IsVisible = !value; BotaoLayout.HorizontalAlignment = HorizontalAlignment.Center; }
    }

    public void Abrir() => BotaoLayout.Flyout?.ShowAt(BotaoLayout);
}
