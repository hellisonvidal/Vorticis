using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;
using Vorticis.Erp.Desktop.ViewModels;

namespace Vorticis.Erp.Desktop.Views;

/// <summary>Veículos e motoristas; as fichas abrem em janela (Novo, Abrir ficha ou duplo clique na lista).</summary>
public partial class VeiculosView : UserControl
{
    public VeiculosView()
    {
        InitializeComponent();
        FichaPopup.Ligar(this, () => new VeiculoMotoristaFichaView(), 520, 440);
        ListaVeiculos.DoubleTapped += (_, _) => (DataContext as VeiculosViewModel)?.AbrirVeiculoCommand.Execute(null);
        ListaMotoristas.DoubleTapped += (_, _) => (DataContext as VeiculosViewModel)?.AbrirMotoristaCommand.Execute(null);
    }
}
