using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Vorticis.Desktop.Comum.ViewModels;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>
/// Caixa de pesquisa de conta do plano: digite parte do código, o reduzido ou parte do nome e escolha na lista; o texto vira o código
/// da conta. <c>ItemsSource</c> recebe <see cref="ContaPesquisavel"/>.
/// </summary>
public sealed class CaixaContaPlano : AutoCompleteBox
{
    protected override Type StyleKeyOverride => typeof(AutoCompleteBox);

    public CaixaContaPlano()
    {
        Watermark = "Código, reduzido ou nome";
        FilterMode = AutoCompleteFilterMode.Custom;
        ItemFilter = (texto, item) => item is ContaPesquisavel c && ContaPesquisavel.Combina(c, texto);
        ValueMemberBinding = new Binding(nameof(ContaPesquisavel.Codigo));
        MinimumPrefixLength = 1;
        MaxDropDownHeight = 260;
        MinWidth = 220;
        ItemTemplate = new FuncDataTemplate<ContaPesquisavel>((c, _) => new TextBlock { Text = c?.Rotulo, FontFamily = new Avalonia.Media.FontFamily("Consolas, Menlo, monospace") });
        ToolTip.SetTip(this, "Pesquise a conta pelo código, pelo reduzido ou por parte do nome, e escolha na lista.");
    }
}
