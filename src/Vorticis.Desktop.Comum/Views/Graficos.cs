using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;

namespace Vorticis.Desktop.Comum.Views;

public sealed record ItemGrafico(string Rotulo, double Valor);
public sealed record ItemCascata(string Rotulo, double Valor, bool Total);
public sealed record SerieGrafico(string Nome, IReadOnlyList<double> Valores, int Cor);

/// <summary>Base dos gráficos do painel de BI: desenhados direto no controle (sem biblioteca externa), com cores que seguem o tema claro/escuro.</summary>
public abstract class GraficoBase : Control
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] CategoricasClaro = ["#2563EB", "#E67E22", "#0F9D8A", "#8E44AD", "#D6336C", "#6B7C8C", "#B8920F", "#2E7D32"];
    private static readonly string[] CategoricasEscuro = ["#6EA8FE", "#F5A25D", "#4FD1B8", "#C39BD3", "#F78FB3", "#A9B8C6", "#E6C84F", "#7BC67E"];

    protected GraficoBase() => ActualThemeVariantChanged += (_, _) => InvalidateVisual();

    protected bool Escuro => ActualThemeVariant == ThemeVariant.Dark;
    protected Color CorTexto => (TextElement.GetForeground(this) as ISolidColorBrush)?.Color ?? (Escuro ? Colors.White : Color.Parse("#1F2937"));
    protected IBrush Texto => new SolidColorBrush(CorTexto);
    protected IBrush TextoSuave => new SolidColorBrush(CorTexto, 0.65);
    protected IPen Grade => new Pen(new SolidColorBrush(CorTexto, 0.16), 1);
    protected IPen Eixo => new Pen(new SolidColorBrush(CorTexto, 0.55), 1);
    protected Color Categorica(int i) => Color.Parse((Escuro ? CategoricasEscuro : CategoricasClaro)[i % CategoricasClaro.Length]);
    protected Color Positiva => Color.Parse(Escuro ? "#4FD1B8" : "#0F9D8A");
    protected Color Negativa => Color.Parse(Escuro ? "#FF8A80" : "#D64545");
    protected Color Total => Color.Parse(Escuro ? "#6EA8FE" : "#2563EB");

    protected static string Abreviar(double v)
    {
        var a = Math.Abs(v);
        return a switch
        {
            >= 1e9 => (v / 1e9).ToString("0.##", PtBr) + " bi",
            >= 1e6 => (v / 1e6).ToString("0.##", PtBr) + " mi",
            >= 1e4 => (v / 1e3).ToString("0.#", PtBr) + " mil",
            _ => v.ToString("#,##0", PtBr),
        };
    }

    protected FormattedText Formatar(string texto, double tamanho, IBrush pincel, double larguraMaxima = double.PositiveInfinity, FontWeight peso = FontWeight.Normal)
    {
        var t = new FormattedText(texto, PtBr, FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, peso), tamanho, pincel);
        if (!double.IsInfinity(larguraMaxima)) { t.MaxTextWidth = Math.Max(1, larguraMaxima); t.MaxLineCount = 1; t.Trimming = TextTrimming.CharacterEllipsis; }
        return t;
    }

    protected void SemDados(DrawingContext ctx)
    {
        var t = Formatar("Sem dados no período", 12, TextoSuave);
        ctx.DrawText(t, new Point((Bounds.Width - t.Width) / 2, (Bounds.Height - t.Height) / 2));
    }

    /// <summary>Limites "redondos" do eixo (passo 1, 2 ou 5 × 10^n), sempre incluindo o zero.</summary>
    protected static (double Min, double Max, double Passo) EscalaEixo(double min, double max, int divisoes = 4)
    {
        min = Math.Min(0, min);
        max = Math.Max(0, max);
        if (max - min < 1e-9) max = min + 1;
        var bruto = (max - min) / divisoes;
        var pot = Math.Pow(10, Math.Floor(Math.Log10(bruto)));
        var f = bruto / pot;
        var passo = (f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10) * pot;
        return (Math.Floor(min / passo) * passo, Math.Ceiling(max / passo) * passo, passo);
    }

    protected override Size MeasureOverride(Size disponivel) =>
        new(double.IsInfinity(disponivel.Width) ? 300 : disponivel.Width, double.IsInfinity(disponivel.Height) ? AlturaPadrao : Math.Min(disponivel.Height, Math.Max(AlturaPadrao, 0)));

    protected virtual double AlturaPadrao => 220;
}

/// <summary>Linhas (uma por série) ao longo de rótulos, como os últimos 12 meses.</summary>
public sealed class GraficoLinhas : GraficoBase
{
    public static readonly StyledProperty<IReadOnlyList<string>?> RotulosProperty = AvaloniaProperty.Register<GraficoLinhas, IReadOnlyList<string>?>(nameof(Rotulos));
    public static readonly StyledProperty<IReadOnlyList<SerieGrafico>?> SeriesProperty = AvaloniaProperty.Register<GraficoLinhas, IReadOnlyList<SerieGrafico>?>(nameof(Series));

    static GraficoLinhas() => AffectsRender<GraficoLinhas>(RotulosProperty, SeriesProperty);

    public IReadOnlyList<string>? Rotulos { get => GetValue(RotulosProperty); set => SetValue(RotulosProperty, value); }
    public IReadOnlyList<SerieGrafico>? Series { get => GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }

    protected override double AlturaPadrao => 260;

    public override void Render(DrawingContext ctx)
    {
        var series = Series ?? [];
        var rotulos = Rotulos ?? [];
        if (series.Count == 0 || rotulos.Count == 0 || series.All(s => s.Valores.Count == 0)) { SemDados(ctx); return; }
        const double esq = 58, dir = 14, topo = 28, baixo = 26;
        var area = new Rect(esq, topo, Math.Max(10, Bounds.Width - esq - dir), Math.Max(10, Bounds.Height - topo - baixo));
        var todos = series.SelectMany(s => s.Valores).ToList();
        var (min, max, passo) = EscalaEixo(todos.Min(), todos.Max());
        double Y(double v) => area.Bottom - (v - min) / (max - min) * area.Height;
        double X(int i) => rotulos.Count == 1 ? area.Center.X : area.Left + i * area.Width / (rotulos.Count - 1);

        for (var v = min; v <= max + passo / 2; v += passo)
        {
            ctx.DrawLine(Math.Abs(v) < 1e-9 ? Eixo : Grade, new Point(area.Left, Y(v)), new Point(area.Right, Y(v)));
            var t = Formatar(Abreviar(v), 10.5, TextoSuave);
            ctx.DrawText(t, new Point(area.Left - 6 - t.Width, Y(v) - t.Height / 2));
        }
        var cada = Math.Max(1, (int)Math.Ceiling(rotulos.Count * 46.0 / area.Width));
        for (var i = 0; i < rotulos.Count; i += cada)
        {
            var t = Formatar(rotulos[i], 10.5, TextoSuave);
            ctx.DrawText(t, new Point(Math.Clamp(X(i) - t.Width / 2, 0, Bounds.Width - t.Width), area.Bottom + 6));
        }
        var lx = esq;
        foreach (var s in series)
        {
            var cor = Categorica(s.Cor);
            var pincel = new SolidColorBrush(cor);
            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                for (var i = 0; i < s.Valores.Count && i < rotulos.Count; i++)
                {
                    var p = new Point(X(i), Y(s.Valores[i]));
                    if (i == 0) g.BeginFigure(p, false); else g.LineTo(p);
                }
                g.EndFigure(false);
            }
            ctx.DrawGeometry(null, new Pen(pincel, 2.5, lineJoin: PenLineJoin.Round), geo);
            for (var i = 0; i < s.Valores.Count && i < rotulos.Count; i++) ctx.DrawEllipse(pincel, null, new Point(X(i), Y(s.Valores[i])), 3.5, 3.5);
            ctx.DrawRectangle(pincel, null, new Rect(lx, 8, 12, 12), 3);
            var nome = Formatar(s.Nome, 11.5, Texto);
            ctx.DrawText(nome, new Point(lx + 17, 7));
            lx += 17 + nome.Width + 20;
        }
    }
}

/// <summary>Barras horizontais com rótulo à esquerda e valor ao fim da barra (ranking de despesas).</summary>
public sealed class GraficoBarras : GraficoBase
{
    public static readonly StyledProperty<IReadOnlyList<ItemGrafico>?> ItensProperty = AvaloniaProperty.Register<GraficoBarras, IReadOnlyList<ItemGrafico>?>(nameof(Itens));
    static GraficoBarras() => AffectsRender<GraficoBarras>(ItensProperty);
    public IReadOnlyList<ItemGrafico>? Itens { get => GetValue(ItensProperty); set => SetValue(ItensProperty, value); }

    protected override double AlturaPadrao => Math.Max(80, (Itens?.Count ?? 0) * 30 + 12);

    public override void Render(DrawingContext ctx)
    {
        var itens = Itens ?? [];
        if (itens.Count == 0) { SemDados(ctx); return; }
        var rotuloL = Math.Clamp(Bounds.Width * 0.38, 90, 230);
        const double valorL = 62;
        var max = itens.Max(i => Math.Abs(i.Valor));
        if (max <= 0) max = 1;
        var area = Math.Max(10, Bounds.Width - rotuloL - valorL - 8);
        var linha = Math.Min(30, (Bounds.Height - 8) / itens.Count);
        for (var k = 0; k < itens.Count; k++)
        {
            var y = 4 + k * linha;
            var t = Formatar(itens[k].Rotulo, 12, Texto, rotuloL - 10);
            ctx.DrawText(t, new Point(0, y + (linha - t.Height) / 2));
            var larg = Math.Max(2, Math.Abs(itens[k].Valor) / max * area);
            ctx.DrawRectangle(new SolidColorBrush(Categorica(0)), null, new Rect(rotuloL, y + 4, larg, linha - 8), 3);
            var v = Formatar(Abreviar(itens[k].Valor), 11.5, TextoSuave);
            ctx.DrawText(v, new Point(rotuloL + larg + 6, y + (linha - v.Height) / 2));
        }
    }
}

/// <summary>Rosca com legenda (composição por centro de custo).</summary>
public sealed class GraficoRosca : GraficoBase
{
    public static readonly StyledProperty<IReadOnlyList<ItemGrafico>?> ItensProperty = AvaloniaProperty.Register<GraficoRosca, IReadOnlyList<ItemGrafico>?>(nameof(Itens));
    static GraficoRosca() => AffectsRender<GraficoRosca>(ItensProperty);
    public IReadOnlyList<ItemGrafico>? Itens { get => GetValue(ItensProperty); set => SetValue(ItensProperty, value); }

    protected override double AlturaPadrao => 230;

    public override void Render(DrawingContext ctx)
    {
        var itens = (Itens ?? []).Where(i => i.Valor > 0).ToList();
        var total = itens.Sum(i => i.Valor);
        if (itens.Count == 0 || total <= 0) { SemDados(ctx); return; }
        var lado = Math.Min(Bounds.Height - 8, Bounds.Width * 0.5);
        var centro = new Point(lado / 2 + 4, Bounds.Height / 2);
        var raioE = lado / 2;
        var raioI = raioE * 0.58;
        var ang = -Math.PI / 2;
        for (var k = 0; k < itens.Count; k++)
        {
            var passo = itens[k].Valor / total * Math.PI * 2;
            var pincel = new SolidColorBrush(Categorica(k));
            if (itens.Count == 1)
            {
                var g = new GeometryGroup { FillRule = FillRule.EvenOdd };
                g.Children.Add(new EllipseGeometry(new Rect(centro.X - raioE, centro.Y - raioE, raioE * 2, raioE * 2)));
                g.Children.Add(new EllipseGeometry(new Rect(centro.X - raioI, centro.Y - raioI, raioI * 2, raioI * 2)));
                ctx.DrawGeometry(pincel, null, g);
            }
            else
            {
                Point P(double a, double r) => new(centro.X + Math.Cos(a) * r, centro.Y + Math.Sin(a) * r);
                var fim = ang + passo - 0.012;   // fresta entre as fatias
                var geo = new StreamGeometry();
                using (var c = geo.Open())
                {
                    c.BeginFigure(P(ang, raioE), true);
                    c.ArcTo(P(fim, raioE), new Size(raioE, raioE), 0, passo > Math.PI, SweepDirection.Clockwise);
                    c.LineTo(P(fim, raioI));
                    c.ArcTo(P(ang, raioI), new Size(raioI, raioI), 0, passo > Math.PI, SweepDirection.CounterClockwise);
                    c.EndFigure(true);
                }
                ctx.DrawGeometry(pincel, null, geo);
            }
            ang += passo;
        }
        var tt = Formatar(Abreviar(total), 14, Texto, raioI * 1.8, FontWeight.SemiBold);
        ctx.DrawText(tt, new Point(centro.X - tt.Width / 2, centro.Y - tt.Height / 2 - 6));
        var sub = Formatar("total", 10.5, TextoSuave);
        ctx.DrawText(sub, new Point(centro.X - sub.Width / 2, centro.Y + 6));

        var lx = lado + 22;
        var linha = Math.Min(24, (Bounds.Height - 8) / itens.Count);
        var y0 = Math.Max(4, (Bounds.Height - linha * itens.Count) / 2);
        for (var k = 0; k < itens.Count; k++)
        {
            var y = y0 + k * linha;
            ctx.DrawRectangle(new SolidColorBrush(Categorica(k)), null, new Rect(lx, y + (linha - 11) / 2, 11, 11), 3);
            var pct = $"{itens[k].Valor / total * 100:0.#}%";
            var p = Formatar(pct, 11.5, TextoSuave);
            var rot = Formatar(itens[k].Rotulo, 11.5, Texto, Math.Max(40, Bounds.Width - lx - 18 - p.Width - 40));
            ctx.DrawText(rot, new Point(lx + 18, y + (linha - rot.Height) / 2));
            ctx.DrawText(p, new Point(Bounds.Width - p.Width - 4, y + (linha - p.Height) / 2));
        }
    }
}

/// <summary>Cascata: totais partem do zero; os demais degraus sobem ou descem a partir do acumulado.</summary>
public sealed class GraficoCascata : GraficoBase
{
    public static readonly StyledProperty<IReadOnlyList<ItemCascata>?> ItensProperty = AvaloniaProperty.Register<GraficoCascata, IReadOnlyList<ItemCascata>?>(nameof(Itens));
    static GraficoCascata() => AffectsRender<GraficoCascata>(ItensProperty);
    public IReadOnlyList<ItemCascata>? Itens { get => GetValue(ItensProperty); set => SetValue(ItensProperty, value); }

    protected override double AlturaPadrao => 260;

    public override void Render(DrawingContext ctx)
    {
        var itens = Itens ?? [];
        if (itens.Count == 0) { SemDados(ctx); return; }
        // (início, fim) de cada barra
        var barras = new List<(double De, double Ate)>();
        double acumulado = 0;
        foreach (var i in itens)
        {
            if (i.Total) { barras.Add((0, i.Valor)); acumulado = i.Valor; }
            else { barras.Add((acumulado, acumulado + i.Valor)); acumulado += i.Valor; }
        }
        const double esq = 58, dir = 10, topo = 22, baixo = 38;
        var area = new Rect(esq, topo, Math.Max(10, Bounds.Width - esq - dir), Math.Max(10, Bounds.Height - topo - baixo));
        var (min, max, passo) = EscalaEixo(barras.Min(b => Math.Min(b.De, b.Ate)), barras.Max(b => Math.Max(b.De, b.Ate)));
        double Y(double v) => area.Bottom - (v - min) / (max - min) * area.Height;
        for (var v = min; v <= max + passo / 2; v += passo)
        {
            ctx.DrawLine(Math.Abs(v) < 1e-9 ? Eixo : Grade, new Point(area.Left, Y(v)), new Point(area.Right, Y(v)));
            var t = Formatar(Abreviar(v), 10.5, TextoSuave);
            ctx.DrawText(t, new Point(area.Left - 6 - t.Width, Y(v) - t.Height / 2));
        }
        var slot = area.Width / itens.Count;
        var larg = Math.Min(54, slot * 0.64);
        for (var k = 0; k < itens.Count; k++)
        {
            var (de, ate) = barras[k];
            var x = area.Left + k * slot + (slot - larg) / 2;
            var cor = itens[k].Total ? Total : itens[k].Valor >= 0 ? Positiva : Negativa;
            var topoBarra = Y(Math.Max(de, ate));
            var altura = Math.Max(2, Math.Abs(Y(de) - Y(ate)));
            ctx.DrawRectangle(new SolidColorBrush(cor), null, new Rect(x, topoBarra, larg, altura), 3);
            if (k < itens.Count - 1)
                ctx.DrawLine(Grade, new Point(x + larg, Y(barras[k].Ate)), new Point(x + slot, Y(barras[k].Ate)));
            var sinal = !itens[k].Total && itens[k].Valor > 0 ? "+" : "";
            var v = Formatar(sinal + Abreviar(itens[k].Valor), 10.5, Texto, slot, FontWeight.SemiBold);
            var acima = itens[k].Valor >= 0 || itens[k].Total;
            ctx.DrawText(v, new Point(x + larg / 2 - v.Width / 2, acima ? topoBarra - v.Height - 2 : topoBarra + altura + 2));
            var r = Formatar(itens[k].Rotulo, 10.5, TextoSuave, slot - 2);
            ctx.DrawText(r, new Point(area.Left + k * slot + (slot - r.Width) / 2, area.Bottom + 6 + (Y(0) > area.Bottom - 1 ? 0 : 0)));
        }
    }
}
