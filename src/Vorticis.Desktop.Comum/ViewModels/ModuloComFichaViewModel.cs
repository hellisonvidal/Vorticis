using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vorticis.Desktop.Comum.ViewModels;

/// <summary>
/// Base das telas de lista cujo formulário do registro (a ficha) abre numa janela por cima da lista, mas que não usam a barra
/// padrão de cadastro (Novo, Salvar, Excluir próprios). A tela fica mostrando as consultas; o formulário aparece ao incluir
/// ou ao abrir um registro (duplo clique ou Enter). Ver <c>Views.FichaPopup</c>.
/// </summary>
public abstract partial class ModuloComFichaViewModel(IContexto contexto) : ModuloViewModel(contexto), IComFicha
{
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TituloFicha))] private bool _fichaAberta;

    /// <summary>Título da janela da ficha.</summary>
    public virtual string TituloFicha => "Ficha";

    /// <summary>Há um registro selecionado na lista para abrir (duplo clique, Enter ou o botão Abrir ficha).</summary>
    protected virtual bool HaRegistroParaAbrir => false;

    /// <summary>Mensagem quando se tenta abrir sem selecionar nada.</summary>
    protected virtual string AvisoSelecione => "Selecione o registro na lista.";

    /// <summary>Chamado antes de a ficha do registro selecionado abrir.</summary>
    protected virtual void AoAbrirFicha() { }

    /// <summary>Chamado quando a ficha fecha sem gravar (X, Esc ou Cancelar): devolve o formulário ao que está gravado.</summary>
    protected virtual void AoDescartarFicha() { }

    /// <summary>Abre a ficha do registro selecionado.</summary>
    [RelayCommand]
    public void AbrirFicha()
    {
        Erros.Clear();
        Mensagem = null;
        if (!HaRegistroParaAbrir) { Erros.Add(AvisoSelecione); return; }
        AoAbrirFicha();
        FichaAberta = true;
    }

    /// <summary>Abre a ficha para um registro novo (já limpa pela tela).</summary>
    protected void AbrirFichaNova()
    {
        Erros.Clear();
        Mensagem = null;
        FichaAberta = true;
    }

    /// <summary>Fecha a ficha desistindo da edição em andamento.</summary>
    [RelayCommand]
    public void FecharFicha()
    {
        if (!FichaAberta) return;
        FichaAberta = false;
        AoDescartarFicha();
    }

    /// <summary>Fecha a ficha depois de gravar com sucesso (sem descartar nada).</summary>
    protected void FecharFichaGravada()
    {
        if (Erros.Count == 0) FichaAberta = false;
    }
}
