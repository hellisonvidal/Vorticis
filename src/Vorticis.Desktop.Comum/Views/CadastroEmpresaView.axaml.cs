using Avalonia.Controls;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Cadastro de empresas: a aba Dados cadastrais mostra a lista e a ficha abre em janela (Incluir, Alterar, duplo clique ou Enter).</summary>
public partial class CadastroEmpresaView : UserControl
{
    public CadastroEmpresaView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaEmpresas, () => new EmpresaFichaView(), 780, 760);
    }
}
