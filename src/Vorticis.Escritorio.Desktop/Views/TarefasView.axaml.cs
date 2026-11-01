using Avalonia.Controls;
using Avalonia.Input;
using Vorticis.Desktop.Comum.Views;
using Vorticis.Escritorio.Desktop.ViewModels;

namespace Vorticis.Escritorio.Desktop.Views;

/// <summary>Lista das tarefas; a ficha abre em janela (Nova tarefa, Abrir, duplo clique ou Enter na lista).</summary>
public partial class TarefasView : UserControl
{
    public TarefasView()
    {
        InitializeComponent();
        FichaPopup.Ligar(this, () => new TarefaFichaView(), 620, 560);
        ListaTarefas.DoubleTapped += (_, _) => (DataContext as TarefasViewModel)?.AbrirCommand.Execute(null);
        ListaTarefas.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && DataContext is TarefasViewModel { Selecionada: not null } vm) { vm.AbrirCommand.Execute(null); e.Handled = true; }
        };
    }
}
