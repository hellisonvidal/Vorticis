using Avalonia.Controls;
using Avalonia.Data;
using Vorticis.Escritorio.Desktop.ViewModels;

namespace Vorticis.Escritorio.Desktop.Views;

/// <summary>Relatórios do Escritório: as colunas da grade seguem o relatório (contratos ou honorários); o período só vale para honorários.</summary>
public partial class RelatorioEscritorioView : UserControl
{
    public RelatorioEscritorioView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Montar();
    }

    private void Montar()
    {
        var contratos = DataContext is RelContratosViewModel;
        foreach (var n in new[] { "RotuloPeriodo", "Inicio", "RotuloA", "Fim" }) this.FindControl<Control>(n)!.IsVisible = !contratos;
        this.FindControl<Control>("Ativos")!.IsVisible = contratos;
        string[] titulos = contratos
            ? ["Nº", "Tipo", "Cliente / fornecedor", "Objeto", "Valor", "Periodicidade", "Início", "Fim", "Situação"]
            : ["Competência", "Cliente", "Vencimento", "Valor", "Situação", "Recebido em", "Valor recebido"];
        Grade.Columns.Clear();
        for (var i = 0; i < titulos.Length; i++)
            Grade.Columns.Add(new DataGridTextColumn
            {
                Header = titulos[i], Binding = new Binding($"V{i}"),
                Width = titulos[i] is "Cliente / fornecedor" or "Objeto" or "Cliente" ? new DataGridLength(1, DataGridLengthUnitType.Star) : DataGridLength.Auto,
            });
    }
}
