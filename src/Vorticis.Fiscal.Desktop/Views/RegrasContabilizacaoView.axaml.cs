using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Fiscal.Desktop.Views;

/// <summary>Regras de contabilização: a lista fica na tela (abas Entradas e Saídas) e a ficha da regra abre em janela.</summary>
public partial class RegrasContabilizacaoView : UserControl
{
    public RegrasContabilizacaoView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaEntradas, () => new RegraContabilizacaoFichaView(), 900, 380);
        FichaPopup.LigarAbertura(this, ListaSaidas);
    }
}
