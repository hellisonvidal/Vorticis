using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Vorticis.Desktop.Comum.ViewModels;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Seletor dos cinco layouts da Suite, com a miniatura de cada um; a troca é imediata e fica gravada para o usuário.</summary>
public sealed class SuiteSeletorView : UserControl
{
    private readonly Dictionary<LayoutSuite, (Button Botao, Border Marca)> _cartoes = [];
    private SuiteViewModel? _suite;

    public SuiteSeletorView()
    {
        var painel = new WrapPanel { Width = 612, ItemWidth = 196 };
        foreach (var o in SuiteViewModel.Opcoes)
        {
            var marca = new Border
            {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(Color.Parse("#1FA67A")),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 6, 6, 0), IsVisible = false,
                Child = new PathIcon { Data = Icones.CheckCircle, Width = 14, Height = 14, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            };
            var titulo = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 8, 0, 2),
                Children =
                {
                    new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(5), Background = new SolidColorBrush(Color.Parse("#0F2A47")),
                                 Child = new TextBlock { Text = o.Letra, FontSize = 10.5, FontWeight = FontWeight.Bold, Foreground = Brushes.White,
                                                         HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } },
                    new TextBlock { Text = o.Nome, FontSize = 12.5, FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(Color.Parse("#0F172A")), VerticalAlignment = VerticalAlignment.Center },
                },
            };
            var conteudo = new StackPanel
            {
                Margin = new Thickness(8),
                Children =
                {
                    new Panel { Children = { new LayoutMiniatura(o.Valor), marca } },
                    titulo,
                    new TextBlock { Text = o.Descricao, FontSize = 11, Foreground = new SolidColorBrush(Color.Parse("#64748B")), TextWrapping = TextWrapping.Wrap, Width = 176, MinHeight = 44 },
                },
            };
            var botao = new Button { Content = conteudo, Margin = new Thickness(3), Padding = new Thickness(0), Classes = { "su" }, VerticalContentAlignment = VerticalAlignment.Top };
            botao.SetValue(ToolTip.TipProperty, $"Usar o layout {o.Letra} – {o.Nome}");
            var valor = o.Valor;
            botao.Click += (_, _) => _suite?.EscolherLayoutCommand.Execute(valor);
            _cartoes[o.Valor] = (botao, marca);
            painel.Children.Add(botao);
        }
        Content = new Border
        {
            Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.Parse("#CBD5E1")), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 10, 12, 8),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "Layout da Suite", FontSize = 15, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#0F2A47")), Margin = new Thickness(4, 0, 0, 0) },
                    new TextBlock { Text = "Escolha como navegar entre os módulos. A troca é imediata e fica gravada para o seu usuário.", FontSize = 11.5,
                                    Foreground = new SolidColorBrush(Color.Parse("#64748B")), Margin = new Thickness(4, 2, 0, 8) },
                    painel,
                },
            },
        };
        DataContextChanged += (_, _) => Ligar();
    }

    private void Ligar()
    {
        if (_suite is not null) _suite.PropertyChanged -= AoMudar;
        _suite = DataContext as SuiteViewModel;
        if (_suite is not null) _suite.PropertyChanged += AoMudar;
        Marcar();
    }

    private void AoMudar(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SuiteViewModel.Layout)) Marcar();
    }

    private void Marcar()
    {
        foreach (var (layout, (botao, marca)) in _cartoes)
        {
            var atual = _suite?.Layout == layout;
            botao.Classes.Set("sel", atual);
            marca.IsVisible = atual;
        }
    }
}
