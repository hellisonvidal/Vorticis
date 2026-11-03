using Avalonia.Controls;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Lista do cadastro; a ficha abre em janela (Incluir, Alterar, duplo clique ou Enter na lista).</summary>
public partial class ParticipantesView : UserControl
{
    public ParticipantesView()
    {
        InitializeComponent();
        FichaPopup.LigarCadastro(this, ListaParticipantes, () => new ParticipanteFichaView(), 760, 720);
    }
}
