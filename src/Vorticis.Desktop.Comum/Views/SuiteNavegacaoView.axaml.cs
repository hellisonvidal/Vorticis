using Avalonia.Controls;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Conteúdo da navegação à esquerda (B e C); a mesma tela serve à versão fixa e à que abre por cima.</summary>
public partial class SuiteNavegacaoView : UserControl
{
    public SuiteNavegacaoView() => InitializeComponent();

    /// <summary>Versão que abre por cima das telas: cantos arredondados à direita e sombra.</summary>
    public bool Flutuante
    {
        get => Raiz.Classes.Contains("su-flutuante");
        set
        {
            Raiz.Classes.Set("su-flutuante", value);
            if (value) Raiz.BorderThickness = new Avalonia.Thickness(1);
        }
    }

    public TextBox Busca => CampoBusca;
}
