using Avalonia.Controls;
using Avalonia.Data;

namespace Vorticis.Escritorio.Desktop.Views;

/// <summary>Relatório de honorários do Escritório: colunas da grade montadas aqui.</summary>
public partial class RelatorioEscritorioView : UserControl
{
    public RelatorioEscritorioView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Montar();
    }

    private void Montar()
    {
        string[] titulos = ["Competência", "Cliente", "Vencimento", "Valor", "Situação", "Recebido em", "Valor recebido"];
        Grade.Columns.Clear();
        for (var i = 0; i < titulos.Length; i++)
            Grade.Columns.Add(new DataGridTextColumn
            {
                Header = titulos[i], Binding = new Binding($"V{i}"),
                Width = titulos[i] is "Cliente / fornecedor" or "Objeto" or "Cliente" ? new DataGridLength(1, DataGridLengthUnitType.Star) : DataGridLength.Auto,
            });
    }
}
