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
                    new TextBlock { Text = o.Nome, FontSize = 12.5, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center }.TextoRecurso("TxForte"),
                },
            };
            var conteudo = new StackPanel
            {
                Margin = new Thickness(8),
                Children =
                {
                    new Panel { Children = { new LayoutMiniatura(o.Valor), marca } },
                    titulo,
                    new TextBlock { Text = o.Descricao, FontSize = 11, TextWrapping = TextWrapping.Wrap, Width = 176, MinHeight = 44 }.TextoRecurso("TxSutil"),
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
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 10, 12, 8),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "Layout da Suite", FontSize = 15, FontWeight = FontWeight.Bold, Margin = new Thickness(4, 0, 0, 0) }.TextoRecurso("TxTitulo"),
                    new TextBlock { Text = "Escolha como navegar entre os módulos. A troca é imediata e fica gravada para o seu usuário.", FontSize = 11.5,
                                    Margin = new Thickness(4, 2, 0, 8) }.TextoRecurso("TxSutil"),
                    painel,
                    LinhaDeTema(),
                },
            },
        }.FundoRecurso("FundoCartao").BordaRecurso("BordaForte");
        DataContextChanged += (_, _) => Ligar();
    }

    /// <summary>Tema de cores (claro, escuro ou automático), junto das miniaturas: vale para todos os layouts e fica gravado para o usuário.</summary>
    private Control LinhaDeTema()
    {
        var linha = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(4, 6, 0, 2) };
        linha.Children.Add(new TextBlock { Text = "Tema de cores:", FontSize = 12.5, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }.TextoRecurso("TxForte"));
        foreach (var (valor, nome) in SuiteViewModel.Temas)
        {
            var rotulo = valor == TemaSuite.Automatico ? "Automático" : nome;
            var botao = new Button
            {
                Classes = { "su" }, Padding = new Thickness(10, 6), Name = "Tema" + valor,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 6,
                    Children =
                    {
                        new PathIcon { Data = valor switch { TemaSuite.Claro => Icones.WhiteBalanceSunny, TemaSuite.Escuro => Icones.WeatherNight, _ => Icones.ThemeLightDark }, Width = 15, Height = 15 }.TextoRecurso("TxSutil"),
                        new TextBlock { Text = rotulo, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }.TextoRecurso("TxForte"),
                    },
                },
            };
            botao.SetValue(ToolTip.TipProperty, valor == TemaSuite.Automatico ? "Acompanha o tema do Windows (claro ou escuro)" : $"Usar o tema {nome.ToLowerInvariant()}");
            botao.Click += (_, _) => _suite?.EscolherTemaCommand.Execute(valor);
            _botoesTema[valor] = botao;
            linha.Children.Add(botao);
        }
        return linha;
    }

    private readonly Dictionary<TemaSuite, Button> _botoesTema = [];

    private void Ligar()
    {
        if (_suite is not null) _suite.PropertyChanged -= AoMudar;
        _suite = DataContext as SuiteViewModel;
        if (_suite is not null) _suite.PropertyChanged += AoMudar;
        Marcar();
    }

    private void AoMudar(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SuiteViewModel.Layout) or nameof(SuiteViewModel.Tema)) Marcar();
    }

    private void Marcar()
    {
        foreach (var (tema, botao) in _botoesTema) botao.Classes.Set("sel", _suite?.Tema == tema);
        foreach (var (layout, (botao, marca)) in _cartoes)
        {
            var atual = _suite?.Layout == layout;
            botao.Classes.Set("sel", atual);
            marca.IsVisible = atual;
        }
    }
}
