using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Styling;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>
/// Detalhes de tela dos visuais da Suite que valem para todas as janelas, inclusive as das funções:
/// densidade alta (visual 10: letras, campos e linhas das grades menores) e lista à esquerda nos cadastros (visual 4: as telas
/// montadas como "ficha | lista" — grade de três colunas com a lista à direita — passam a mostrar a lista à esquerda).
/// </summary>
public static class EstilosVisuais
{
    private static Styles? _compacta;
    private static bool _ligado;
    private static readonly ConditionalWeakTable<Grid, object> Invertidas = [];
    /// <summary>Grades "ficha | lista" já carregadas (para trocar o lado nas telas abertas quando o visual muda).</summary>
    private static readonly List<WeakReference<Grid>> Vistas = [];

    /// <summary>Densidade alta ligada (o estilo só é aplicado quando há aplicação Avalonia em execução).</summary>
    public static bool Compacta { get; private set; }
    public static bool ListaEsquerda { get; private set; }

    public static void Aplicar(bool compacta, bool listaEsquerda)
    {
        Compacta = compacta;
        if (Avalonia.Application.Current is not { } app) { ListaEsquerda = listaEsquerda; return; }
        if (compacta && _compacta is null) app.Styles.Add(_compacta = CriarCompacta());
        else if (!compacta && _compacta is not null) { app.Styles.Remove(_compacta); _compacta = null; }

        if (!_ligado)
        {
            _ligado = true;
            Control.LoadedEvent.AddClassHandler<Grid>((g, _e) =>
            {
                if (!EhFichaLista(g)) return;
                lock (Vistas) { Vistas.RemoveAll(w => !w.TryGetTarget(out var _vivo)); Vistas.Add(new WeakReference<Grid>(g)); }
                if (ListaEsquerda) Ajustar(g, true);
            });
        }
        if (ListaEsquerda == listaEsquerda) return;
        ListaEsquerda = listaEsquerda;
        List<Grid> abertas;
        lock (Vistas) abertas = [.. Vistas.Select(w => w.TryGetTarget(out var g) ? g : null).OfType<Grid>()];
        foreach (var g in abertas) Ajustar(g, listaEsquerda);
    }

    private static Styles CriarCompacta() =>
    [
        new Style(x => x.OfType<Window>()) { Setters = { new Setter(TemplatedControl.FontSizeProperty, 12.0) } },
        new Style(x => x.OfType<DataGrid>())
        {
            Setters = { new Setter(DataGrid.RowHeightProperty, 22.0), new Setter(TemplatedControl.FontSizeProperty, 11.5), new Setter(DataGrid.ColumnHeaderHeightProperty, 26.0) },
        },
        new Style(x => x.OfType<TextBox>()) { Setters = { new Setter(Layoutable.MinHeightProperty, 24.0), new Setter(TemplatedControl.PaddingProperty, new Thickness(6, 2)) } },
        new Style(x => x.OfType<ComboBox>()) { Setters = { new Setter(Layoutable.MinHeightProperty, 24.0) } },
    ];

    /// <summary>A grade é do tipo "conteúdo | espaço | lista"? (primeira coluna proporcional, segunda estreita, terceira larga e fixa)</summary>
    public static bool EhFichaLista(Grid g)
    {
        var c = g.ColumnDefinitions;
        if (c.Count != 3) return false;
        var (a, b, d) = Invertidas.TryGetValue(g, out _) ? (c[2], c[1], c[0]) : (c[0], c[1], c[2]);
        return a.Width.IsStar && b.Width.IsAbsolute && b.Width.Value is >= 8 and <= 24 && d.Width.IsAbsolute && d.Width.Value >= 280;
    }

    /// <summary>Põe a lista à esquerda (ou de volta à direita) numa grade "ficha | lista".</summary>
    public static void Ajustar(Grid g, bool esquerda)
    {
        if (!EhFichaLista(g) || Invertidas.TryGetValue(g, out _) == esquerda) return;
        var colunas = g.ColumnDefinitions.Select(c => new ColumnDefinition(c.Width) { MinWidth = c.MinWidth, MaxWidth = c.MaxWidth }).Reverse().ToList();
        g.ColumnDefinitions = [.. colunas];
        foreach (var filho in g.Children)
            if (Grid.GetColumnSpan(filho) == 1 && Grid.GetColumn(filho) is var col && col != 1) Grid.SetColumn(filho, 2 - col);
        if (esquerda) Invertidas.Add(g, true);
        else Invertidas.Remove(g);
    }
}
