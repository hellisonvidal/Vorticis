using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Vorticis.Desktop.Comum.ViewModels;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Desenho esquemático (176 x 108) de cada layout da Suite, mostrado no seletor de layouts.</summary>
public sealed class LayoutMiniatura : Border
{
    private static readonly string[] CoresModulos = ["#1FA67A", "#F59E0B", "#7C5CE0", "#2B6CB0", "#E5484D", "#0EA5A5"];
    private const string Marinho = "#0F2A47";

    public LayoutMiniatura(LayoutSuite layout)
    {
        Width = 176; Height = 108; ClipToBounds = true;
        CornerRadius = new CornerRadius(8);
        Background = new SolidColorBrush(Color.Parse("#F4F6FA"));
        BorderBrush = new SolidColorBrush(Color.Parse("#CBD5E1"));
        BorderThickness = new Thickness(1);
        var c = new Canvas();
        Child = c;
        switch (layout)
        {
            case LayoutSuite.Abas:
                Abas(c, 0);
                Barra(c, 17);
                Conteudo(c, 3, 27, 170, 78);
                break;
            case LayoutSuite.Lateral:
                Barra(c, 0);
                Retangulo(c, 0, 8, 22, 100, "#FFFFFF", 0);
                for (var i = 0; i < 6; i++) Retangulo(c, 5, 15 + i * 14, 12, 10, CoresModulos[i], 3, 0.9);
                Conteudo(c, 26, 12, 146, 92);
                Retangulo(c, 22, 8, 1, 100, "#E2E8F0", 0);
                break;
            case LayoutSuite.Arvore:
                Barra(c, 0);
                Retangulo(c, 0, 8, 56, 100, "#FFFFFF", 0);
                Retangulo(c, 5, 13, 46, 8, "#EEF2F7", 4);
                for (var i = 0; i < 7; i++)
                {
                    var recuo = i % 3 == 0 ? 0 : 7;
                    if (recuo == 0) Retangulo(c, 5, 27 + i * 11, 6, 6, CoresModulos[i % 6], 2);
                    Retangulo(c, 14 + recuo, 29 + i * 11, 30 - recuo, 3, recuo == 0 ? "#64748B" : "#B6C2D2", 1.5);
                }
                Conteudo(c, 60, 12, 112, 92);
                Retangulo(c, 56, 8, 1, 100, "#E2E8F0", 0);
                break;
            case LayoutSuite.Painel:
                Retangulo(c, 0, 0, 176, 12, "#FFFFFF", 0);
                Retangulo(c, 6, 3, 6, 6, "#1FA67A", 3);
                Retangulo(c, 15, 4, 28, 4, Marinho, 2);
                Retangulo(c, 0, 12, 176, 1, "#E2E8F0", 0);
                Retangulo(c, 8, 19, 46, 5, "#0F172A", 2.5);
                Retangulo(c, 8, 27, 30, 3, "#94A3B8", 1.5);
                for (var i = 0; i < 6; i++)
                {
                    var x = 8 + (i % 3) * 55; var y = 36 + (i / 3) * 36;
                    Retangulo(c, x, y, 50, 32, "#FFFFFF", 5);
                    Retangulo(c, x, y, 50, 11, CoresModulos[i], 5);
                    Retangulo(c, x, y + 6, 50, 5, CoresModulos[i], 0);
                    Retangulo(c, x + 5, y + 17, 30, 3, "#CBD5E1", 1.5);
                    Retangulo(c, x + 5, y + 24, 22, 3, "#CBD5E1", 1.5);
                }
                break;
            case LayoutSuite.Faixa:
                Abas(c, 0, ribbon: true);
                Barra(c, 17);
                Retangulo(c, 0, 25, 176, 28, "#FFFFFF", 0);
                Retangulo(c, 0, 53, 176, 1, "#D5DCE6", 0);
                for (var g = 0; g < 4; g++)
                    for (var b = 0; b < 3; b++)
                    {
                        var x = 6 + g * 43 + b * 12;
                        Retangulo(c, x, 29, 9, 9, CoresModulos[(g + b) % 6], 2.5, 0.85);
                        Retangulo(c, x, 41, 9, 2, "#CBD5E1", 1);
                    }
                Conteudo(c, 3, 58, 170, 46);
                break;
        }
    }

    private static void Abas(Canvas c, double y, bool ribbon = false)
    {
        Retangulo(c, 0, y, 176, 17, "#E7ECF3", 0);
        for (var i = 0; i < 6; i++)
        {
            var x = 5 + i * 27;
            var ativo = i == 1;
            if (ativo) Retangulo(c, x, y + 4, 25, 13, "#FFFFFF", 4);
            Retangulo(c, x + 3, y + 7, 6, 6, CoresModulos[i], 2);
            Retangulo(c, x + 11, y + 9, 11, 2.5, ativo ? "#0F172A" : "#94A3B8", 1.2);
            if (ativo) Retangulo(c, x, y + 4, 25, 1.5, CoresModulos[i], 1);
        }
    }

    private static void Barra(Canvas c, double y)
    {
        Retangulo(c, 0, y, 176, 8, Marinho, 0);
        for (var i = 0; i < 6; i++) Retangulo(c, 5 + i * 17, y + 3, 11, 2, "#C9D6E8", 1);
        Retangulo(c, 0, y + 8, 176, 1.5, "#1FA67A", 0);
    }

    private static void Conteudo(Canvas c, double x, double y, double w, double h)
    {
        Retangulo(c, x, y, w, h, "#FFFFFF", 4);
        Retangulo(c, x + 5, y + 6, w * 0.35, 4, "#CBD5E1", 2);
        for (var i = 0; i < Math.Max(2, (int)((h - 20) / 9)); i++)
            Retangulo(c, x + 5, y + 16 + i * 9, w - 10, 5, i % 2 == 0 ? "#F1F5F9" : "#F8FAFC", 2);
    }

    private static void Retangulo(Canvas c, double x, double y, double w, double h, string cor, double raio, double opacidade = 1)
    {
        var r = new Rectangle { Width = w, Height = h, RadiusX = raio, RadiusY = raio, Fill = new SolidColorBrush(Color.Parse(cor)), Opacity = opacidade };
        Canvas.SetLeft(r, x); Canvas.SetTop(r, y);
        c.Children.Add(r);
    }
}
