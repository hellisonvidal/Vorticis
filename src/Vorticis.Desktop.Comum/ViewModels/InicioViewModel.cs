using CommunityToolkit.Mvvm.ComponentModel;
using Vorticis.Domain.Comum;

namespace Vorticis.Desktop.Comum.ViewModels;

/// <summary>Tela de descanso: exibida após o login e quando nenhuma função está aberta.</summary>
public partial class InicioViewModel(MainViewModelBase main) : ModuloViewModel(main)
{
    /// <summary>Nome curto do módulo ("Contábil"), em maiúsculas ao fundo da tela inicial.</summary>
    public string NomeCurto => main.Modulo.NomeCurto.ToUpper(Formato.PtBr);

    /// <summary>Cor de destaque do módulo (nome ao fundo da tela inicial).</summary>
    public Avalonia.Media.IBrush PincelDestaque => main.PincelDestaque;

    public string Versao => $"versão {SobreViewModel.Versao}";

    [ObservableProperty] private string? _dica;

    public void Atualizar() =>
        Dica = main.EmpresasPermitidas.Count > 0 ? null
             : main.Sessao?.Administrador == true
                 ? "Nenhuma empresa cadastrada ainda. Quando quiser começar, use Cadastros › Empresas."
                 : "Nenhuma empresa liberada para o seu usuário. Procure o administrador do sistema.";
}
