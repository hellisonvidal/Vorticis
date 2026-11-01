using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Vorticis.Desktop.Comum.ViewModels;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Uma opção da pesquisa: o <see cref="Codigo"/> é o que vai para o campo.</summary>
public sealed record ItemPesquisa(string Codigo, string Descricao)
{
    public override string ToString() => Descricao.Length == 0 ? Codigo : $"{Codigo} — {Descricao}";
}

/// <summary>Lista completa de uma fonte (contas, históricos, participantes...) da empresa ativa.</summary>
public delegate Task<IReadOnlyList<ItemPesquisa>> FontePesquisa(IContexto contexto, CancellationToken ct);

/// <summary>Tela (view model) que dá à pesquisa o acesso ao banco e à empresa ativa.</summary>
public interface IComContexto
{
    IContexto Contexto { get; }
}

/// <summary>
/// Pesquisa dentro do campo: <c>v:Pesquisa.Fonte="Conta|Historico|Participante|ParticipanteCodigo|CentroCusto|Produto"</c> num TextBox põe uma lupa
/// dentro do campo; a lupa (ou F4) abre a lista da fonte com uma caixa de filtro (parte do código ou da descrição, sem acentos) e a
/// opção escolhida (Enter ou duplo clique) entra no campo. As fontes comuns ficam aqui; cada módulo registra as suas (Produto, no Fiscal).
/// </summary>
public static class Pesquisa
{
    public const int MaximoNaLista = 300;
    private static readonly Dictionary<string, FontePesquisa> Fontes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<(string, long), (DateTime Em, IReadOnlyList<ItemPesquisa> Itens)> Cache = [];

    public static readonly AttachedProperty<string?> FonteProperty =
        AvaloniaProperty.RegisterAttached<TextBox, string?>("Fonte", typeof(Pesquisa));

    public static string? GetFonte(TextBox c) => c.GetValue(FonteProperty);
    public static void SetFonte(TextBox c, string? v) => c.SetValue(FonteProperty, v);

    public static void Registrar(string nome, FontePesquisa fonte) => Fontes[nome] = fonte;
    public static bool Registrada(string nome) => Fontes.ContainsKey(nome);

    static Pesquisa()
    {
        Registrar("Conta", async (c, ct) => c.App is { } app && c.EmpresaId is { } e && c.Sessao is { } s
            ? (await app.PlanoContas.ListarAsync(e, s.Id, ct)).Where(x => x.Ativa)
                .Select(x => new ItemPesquisa(x.Codigo, (x.CodigoReduzido is { } r ? $"[{r}] " : "") + x.Descricao + (x.Analitica ? "" : " (sintética)"))).ToList()
            : []);
        Registrar("Historico", async (c, ct) => c.App is { } app && c.EmpresaId is { } e && c.Sessao is { } s
            ? (await app.Historicos.ListarAsync(e, s.Id, ct)).Select(x => new ItemPesquisa(x.Codigo, x.Descricao)).ToList()
            : []);
        Registrar("Participante", async (c, ct) => c.App is { } app && c.EmpresaId is { } e && c.Sessao is { } s
            ? (await app.Participantes.ListarAsync(e, s.Id, ct)).Where(x => x.Ativo)
                .Select(x => new ItemPesquisa(x.CpfCnpj is { Length: > 0 } d ? d : x.Codigo, x.Nome + (x.Municipio is { Length: > 0 } m ? $" · {m}/{x.Uf}" : ""))).ToList()
            : []);
        Registrar("ParticipanteCodigo", async (c, ct) => c.App is { } app && c.EmpresaId is { } e && c.Sessao is { } s
            ? (await app.Participantes.ListarAsync(e, s.Id, ct)).Where(x => x.Ativo)
                .Select(x => new ItemPesquisa(x.Codigo, x.Nome + (x.CpfCnpj is { Length: > 0 } d ? $" · {d}" : ""))).ToList()
            : []);
        Registrar("CentroCusto", async (c, ct) => c.App is { } app && c.EmpresaId is { } e && c.Sessao is { } s
            ? (await app.PlanoContas.ListarCentrosAsync(e, s.Id, ct)).Where(x => x.Ativo && x.Analitico).Select(x => new ItemPesquisa(x.Codigo, x.Descricao)).ToList()
            : []);

        FonteProperty.Changed.AddClassHandler<TextBox>((tb, e) =>
        {
            tb.KeyDown -= AoTeclar;
            if (e.NewValue is not string) { if (tb.InnerRightContent is Button { Tag: "pesquisa" }) tb.InnerRightContent = null; return; }
            tb.KeyDown += AoTeclar;
            tb.InnerRightContent = CriarLupa(tb);
        });
    }

    private static Button CriarLupa(TextBox tb)
    {
        var lupa = new Button
        {
            Tag = "pesquisa", Focusable = false, Padding = new Thickness(4, 0), Margin = new Thickness(0), MinHeight = 0,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Stretch,
            Content = new Avalonia.Controls.Shapes.Path { Data = Icones.Magnify, Width = 13, Height = 13, Stretch = Stretch.Uniform, Fill = Brushes.Gray },
        };
        ToolTip.SetTip(lupa, "Pesquisar (F4)");
        lupa.Click += (_, _) => _ = AbrirAsync(tb);
        return lupa;
    }

    private static void AoTeclar(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.F4 || sender is not TextBox tb) return;
        e.Handled = true;
        _ = AbrirAsync(tb);
    }

    /// <summary>Texto sem acentos e em minúsculas, para comparar.</summary>
    public static string Normalizar(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        foreach (var ch in texto.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }

    /// <summary>Opções que têm todas as palavras do filtro no código ou na descrição (no máximo <see cref="MaximoNaLista"/>).</summary>
    public static IReadOnlyList<ItemPesquisa> Filtrar(IEnumerable<ItemPesquisa> itens, string? filtro)
    {
        var palavras = Normalizar(filtro ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return itens.Where(i =>
        {
            var alvo = Normalizar(i.ToString());
            var digitos = new string(i.Codigo.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            return palavras.All(p => alvo.Contains(p) || digitos.Contains(new string(p.Where(char.IsLetterOrDigit).ToArray())) && p.Any(char.IsLetterOrDigit));
        }).Take(MaximoNaLista).ToList();
    }

    private static IContexto? ContextoDe(Visual v) =>
        v.GetSelfAndVisualAncestors().OfType<StyledElement>().Select(x => x.DataContext).OfType<IComContexto>().FirstOrDefault()?.Contexto;

    private static async Task<IReadOnlyList<ItemPesquisa>> CarregarAsync(string fonte, IContexto ctx)
    {
        var chave = (fonte.ToLowerInvariant(), ctx.EmpresaId ?? 0);
        if (Cache.TryGetValue(chave, out var c) && DateTime.UtcNow - c.Em < TimeSpan.FromMinutes(1)) return c.Itens;
        var itens = Fontes.TryGetValue(fonte, out var f) ? await f(ctx, CancellationToken.None) : [];
        Cache[chave] = (DateTime.UtcNow, itens);
        return itens;
    }

    /// <summary>Esquece as listas guardadas (depois de incluir uma conta, um participante...).</summary>
    public static void Limpar() => Cache.Clear();

    public static async Task AbrirAsync(TextBox tb)
    {
        if (GetFonte(tb) is not { } fonte || tb.IsReadOnly || !tb.IsEffectivelyEnabled) return;
        var filtro = new TextBox { Watermark = "Digite parte do código ou da descrição", Text = Normalizar(tb.Text ?? "").Any(char.IsLetter) ? tb.Text : "" };
        var lista = new ListBox { Height = 280, MinWidth = Math.Max(tb.Bounds.Width, 460) };
        var situacao = new TextBlock { FontSize = 11, Foreground = Brushes.Gray, Text = "Carregando..." };
        var painel = new StackPanel { Spacing = 6, Children = { filtro, lista, situacao } };
        var flyout = new Flyout { Content = painel, Placement = PlacementMode.BottomEdgeAlignedLeft };
        IReadOnlyList<ItemPesquisa> todos = [];

        void Atualizar()
        {
            var achados = Filtrar(todos, filtro.Text);
            lista.ItemsSource = achados;
            if (achados.Count > 0) lista.SelectedIndex = 0;
            situacao.Text = todos.Count == 0 ? "Nada cadastrado para pesquisar."
                : achados.Count == 0 ? "Nada encontrado com esse texto."
                : achados.Count >= MaximoNaLista ? $"Mostrando os {MaximoNaLista} primeiros; digite mais para refinar. Enter escolhe, Esc fecha."
                : $"{achados.Count} encontrado(s). Enter ou duplo clique escolhe; Esc fecha.";
        }

        void Escolher()
        {
            if (lista.SelectedItem is not ItemPesquisa item) return;
            flyout.Hide();
            tb.Text = item.Codigo;
            tb.Focus();
            tb.CaretIndex = tb.Text.Length;
        }

        filtro.TextChanged += (_, _) => Atualizar();
        filtro.KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Enter: Escolher(); e.Handled = true; break;
                case Key.Down when lista.ItemCount > 0: lista.SelectedIndex = Math.Min(lista.SelectedIndex + 1, lista.ItemCount - 1); lista.ScrollIntoView(lista.SelectedIndex); e.Handled = true; break;
                case Key.Up when lista.ItemCount > 0: lista.SelectedIndex = Math.Max(lista.SelectedIndex - 1, 0); lista.ScrollIntoView(lista.SelectedIndex); e.Handled = true; break;
            }
        };
        lista.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Escolher(); e.Handled = true; } };
        lista.DoubleTapped += (_, _) => Escolher();
        flyout.Opened += (_, _) => { filtro.Focus(); filtro.CaretIndex = filtro.Text?.Length ?? 0; };
        flyout.ShowAt(tb);

        if (ContextoDe(tb) is not { } ctx) { situacao.Text = "Pesquisa indisponível nesta tela."; return; }
        try { todos = await CarregarAsync(fonte, ctx); }
        catch (Exception ex) { situacao.Text = "Não foi possível carregar a lista: " + ex.Message; return; }
        Atualizar();
    }
}
