using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Vorticis.Application.Relatorios;
using Vorticis.Escritorio.Desktop.ViewModels;

namespace Vorticis.Escritorio.Desktop.Views;

/// <summary>Relatórios do Escritório: monta as colunas da grade a partir das colunas reais de cada relatório, com alinhamento numérico e destaque.</summary>
public partial class RelatorioEscritorioView : UserControl
{
    private RelatorioEscritorioViewModel? _vm;

    public RelatorioEscritorioView()
    {
        InitializeComponent();
        Grade.LoadingRow += (_, e) =>
        {
            e.Row.FontWeight = e.Row.DataContext is LinhaRelatorioEscritorio { Destaque: true } ? FontWeight.SemiBold : FontWeight.Normal;
        };
        DataContextChanged += (_, _) => Conectar(DataContext as RelatorioEscritorioViewModel);
        AttachedToVisualTree += (_, _) =>
        {
            if (DataContext is RelatorioEscritorioViewModel vm && !ReferenceEquals(_vm, vm)) Conectar(vm);
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (_vm is not null) { _vm.TabelaAlterada -= Montar; _vm = null; }
        };
    }

    private void Conectar(RelatorioEscritorioViewModel? novo)
    {
        if (_vm is not null) _vm.TabelaAlterada -= Montar;
        _vm = novo;
        if (_vm is not null) _vm.TabelaAlterada += Montar;
        Montar();
    }

    private void Montar()
    {
        Grade.Columns.Clear();
        var colunas = (_vm ?? DataContext as RelatorioEscritorioViewModel)?.ColunasAtuais ?? TabelasEscritorio.ColunasHonorarios;
        for (var i = 0; i < colunas.Count; i++)
        {
            var c = colunas[i];
            var numerica = c.Tipo is TipoColuna.Valor or TipoColuna.Inteiro;
            var expansivel = c.Tipo == TipoColuna.Texto && (c.LarguraCm == 0 || c.LarguraCm >= 3.5);
            var largura = expansivel ? new DataGridLength(1, DataGridLengthUnitType.Star)
                        : c.Tipo == TipoColuna.Valor ? new DataGridLength(120)
                        : c.Tipo == TipoColuna.Inteiro ? new DataGridLength(95)
                        : c.Tipo == TipoColuna.Data ? new DataGridLength(105)
                        : new DataGridLength(Math.Max(95, c.LarguraCm * 44));
            var col = new DataGridTextColumn
            {
                Header = c.Titulo,
                Binding = new Binding($"V{i}"),
                Width = largura,
                MinWidth = expansivel ? 130 : 75,
            };
            if (numerica)
            {
                col.CellStyleClasses.Add("num");
                col.CellStyleClasses.Add("numerica");
            }
            Grade.Columns.Add(col);
        }
    }
}
