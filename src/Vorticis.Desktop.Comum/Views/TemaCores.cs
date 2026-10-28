using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>
/// Cores do tema em controles montados por código: ligam o pincel a um recurso de Estilos.axaml (TxSutil, Linha, FundoCartao...)
/// para que ele troque sozinho entre os temas claro e escuro, como o <c>{DynamicResource}</c> do XAML.
/// </summary>
public static class TemaCores
{
    /// <summary>Cor do texto/ícone (TextBlock, PathIcon, Button...) vinda do recurso.</summary>
    public static T TextoRecurso<T>(this T c, string chave) where T : Control
    {
        var p = c is TextBlock ? TextBlock.ForegroundProperty : TemplatedControl.ForegroundProperty;
        c.Bind(p, c.GetResourceObservable(chave));
        return c;
    }

    public static Border FundoRecurso(this Border b, string chave)
    {
        b.Bind(Border.BackgroundProperty, b.GetResourceObservable(chave));
        return b;
    }

    public static Border BordaRecurso(this Border b, string chave)
    {
        b.Bind(Border.BorderBrushProperty, b.GetResourceObservable(chave));
        return b;
    }

    /// <summary>O tema escuro está em uso? (para quem desenha com pincéis fixos, como o manual e os gráficos)</summary>
    public static bool Escuro => Avalonia.Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
}
