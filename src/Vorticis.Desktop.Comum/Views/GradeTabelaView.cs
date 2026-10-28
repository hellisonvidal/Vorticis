using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Vorticis.Application.Relatorios;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Linha da grade: os textos já formatados, na ordem das colunas.</summary>
public sealed record LinhaGrade(string[] Valores, bool Destaque);

/// <summary>
/// Grade que exibe qualquer <see cref="TabelaRelatorio"/> (colunas montadas na hora): usada pelos relatórios comparativos,
/// cujas colunas dependem dos períodos escolhidos. É a mesma tabela que vai para o PDF, o Excel e o CSV.
/// </summary>
public sealed class GradeTabelaView : UserControl
{
    public static readonly StyledProperty<TabelaRelatorio?> TabelaProperty = AvaloniaProperty.Register<GradeTabelaView, TabelaRelatorio?>(nameof(Tabela));

    private readonly DataGrid _grade = new() { IsReadOnly = true, AutoGenerateColumns = false, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal };

    public GradeTabelaView()
    {
        Content = _grade;
        _grade.CellPointerPressed += (_, e) => CelulaClicada?.Invoke(this, (e.Row.Index, e.Column.DisplayIndex));
    }

    /// <summary>Clique numa célula: índice da linha e da coluna na grade (usado pelo cubo para detalhar os lançamentos).</summary>
    public event EventHandler<(int Linha, int Coluna)>? CelulaClicada;

    public TabelaRelatorio? Tabela
    {
        get => GetValue(TabelaProperty);
        set => SetValue(TabelaProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TabelaProperty) Montar(Tabela);
    }

    private void Montar(TabelaRelatorio? t)
    {
        _grade.Columns.Clear();
        _grade.ItemsSource = null;
        if (t is null) return;
        var indiceDescricao = Math.Max(0, t.Colunas.ToList().FindIndex(c => c.Titulo == "Descrição"));
        for (var i = 0; i < t.Colunas.Count; i++)
        {
            var indice = i;
            var coluna = t.Colunas[i];
            var numerica = coluna.Tipo is TipoColuna.Valor or TipoColuna.Inteiro;
            var larga = !numerica && i != indiceDescricao && coluna.LarguraCm >= 4.5;   // fórmulas e interpretações: coluna mais larga, com quebra de linha
            _grade.Columns.Add(new DataGridTemplateColumn
            {
                Header = new TextBlock { Text = coluna.Titulo, TextWrapping = TextWrapping.Wrap, TextAlignment = numerica ? TextAlignment.Right : TextAlignment.Left },
                Width = i == indiceDescricao ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(numerica ? 125 : larga ? coluna.LarguraCm * 40 : 110),
                MinWidth = i == indiceDescricao ? 240 : 70,
                CellTemplate = new FuncDataTemplate<LinhaGrade>((linha, _) => new TextBlock
                {
                    Text = linha is null ? "" : linha.Valores[indice],
                    FontWeight = linha?.Destaque == true ? FontWeight.SemiBold : FontWeight.Normal,
                    TextWrapping = larga ? TextWrapping.Wrap : TextWrapping.NoWrap,
                    HorizontalAlignment = numerica ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 2),
                }),
            });
        }
        _grade.ItemsSource = t.Linhas.Select(l =>
        {
            var textos = l.Valores.Select((v, i) => ExportacaoRelatorio.Texto(v, t.Colunas[i].Tipo)).ToArray();
            if (l.Recuo > 0) textos[indiceDescricao] = new string(' ', l.Recuo * 3) + textos[indiceDescricao];
            return new LinhaGrade(textos, l.Destaque);
        }).ToList();
    }
}
