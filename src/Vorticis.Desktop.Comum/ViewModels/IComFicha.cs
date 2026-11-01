using System.ComponentModel;

namespace Vorticis.Desktop.Comum.ViewModels;

/// <summary>
/// Tela de lista cuja ficha (o formulário do registro) abre numa janela por cima da lista: a tela fica mostrando todos os
/// registros e a ficha aparece ao incluir, alterar ou abrir um registro (duplo clique ou Enter). Ver <c>Views.FichaPopup</c>.
/// </summary>
public interface IComFicha : INotifyPropertyChanged
{
    /// <summary>A ficha está aberta (a janela aparece enquanto for verdadeiro).</summary>
    bool FichaAberta { get; }

    /// <summary>Título da janela da ficha.</summary>
    string TituloFicha { get; }

    /// <summary>O usuário fechou a janela (X ou Esc): desiste da edição em andamento e fecha a ficha.</summary>
    void FecharFicha();
}
