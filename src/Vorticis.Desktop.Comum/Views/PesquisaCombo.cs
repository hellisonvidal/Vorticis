using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>
/// Pesquisa nas listas de escolha (ComboBox) de todos os módulos: ao abrir uma lista com <see cref="MinimoItens"/> opções ou mais,
/// aparece no alto a caixa "Pesquisar na lista"; o que se digita esconde as opções que não têm o texto (sem acentos, qualquer parte)
/// e Enter escolhe a primeira que sobrou. Ligada uma vez pelo casco (<see cref="Ativar"/>).
/// </summary>
public static class PesquisaCombo
{
    public const int MinimoItens = 8;
    private static bool _ativa;

    private static readonly AttachedProperty<string?> FiltroProperty =
        AvaloniaProperty.RegisterAttached<ComboBox, string?>("FiltroPesquisa", typeof(PesquisaCombo));

    public static void Ativar()
    {
        if (_ativa) return;
        _ativa = true;
        ComboBox.IsDropDownOpenProperty.Changed.AddClassHandler<ComboBox>((cb, e) =>
        {
            if (e.NewValue is true) Abriu(cb);
            else Filtrar(cb, null);
        });
    }

    /// <summary>A opção fica visível com o filtro? (todas as palavras em qualquer parte do texto, sem acentos)</summary>
    public static bool Combina(object? item, string? filtro)
    {
        if (string.IsNullOrWhiteSpace(filtro)) return true;
        var texto = Pesquisa.Normalizar(TextoDe(item));
        return Pesquisa.Normalizar(filtro).Split(' ', StringSplitOptions.RemoveEmptyEntries).All(texto.Contains);
    }

    private static string TextoDe(object? item) => item switch
    {
        null => "",
        ComboBoxItem { Content: var c } => TextoDe(c),
        ContentControl { Content: var c } => TextoDe(c),
        TextBlock t => t.Text ?? "",
        _ => item.ToString() ?? "",
    };

    private static void Abriu(ComboBox cb)
    {
        if (cb.ItemCount < MinimoItens) return;
        var popup = cb.GetVisualDescendants().OfType<Popup>().FirstOrDefault(p => p.Name == "PART_Popup");
        if (popup?.Child is not Decorator { Child: { } conteudo } moldura) return;
        TextBox caixa;
        if (conteudo is DockPanel { Tag: "pesquisa" } existente) caixa = existente.Children.OfType<TextBox>().First();
        else
        {
            caixa = new TextBox { Watermark = "Pesquisar na lista", Margin = new Thickness(4, 4, 4, 2) };
            DockPanel.SetDock(caixa, Dock.Top);
            moldura.Child = null;
            moldura.Child = new DockPanel { Tag = "pesquisa", LastChildFill = true, Children = { caixa, conteudo } };
            caixa.TextChanged += (_, _) => Filtrar(cb, caixa.Text);
            caixa.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter && PrimeiroVisivel(cb) is { } indice)
                {
                    cb.SelectedIndex = indice;
                    cb.IsDropDownOpen = false;
                    e.Handled = true;
                }
                else if (e.Key == Key.Down && PrimeiroVisivel(cb) is { } i && cb.ContainerFromIndex(i) is { } c)
                {
                    c.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                }
            };
            cb.ContainerPrepared += (_, e) => e.Container.IsVisible = Combina(cb.Items.Count > e.Index ? cb.Items[e.Index] : null, cb.GetValue(FiltroProperty));
        }
        caixa.Text = "";
        Dispatcher.UIThread.Post(() => caixa.Focus(), DispatcherPriority.Input);
    }

    private static int? PrimeiroVisivel(ComboBox cb)
    {
        var filtro = cb.GetValue(FiltroProperty);
        for (var i = 0; i < cb.ItemCount; i++)
            if (Combina(cb.Items[i], filtro)) return i;
        return null;
    }

    private static void Filtrar(ComboBox cb, string? filtro)
    {
        cb.SetValue(FiltroProperty, string.IsNullOrWhiteSpace(filtro) ? null : filtro);
        for (var i = 0; i < cb.ItemCount; i++)
            if (cb.ContainerFromIndex(i) is { } c) c.IsVisible = Combina(cb.Items[i], filtro);
    }
}
