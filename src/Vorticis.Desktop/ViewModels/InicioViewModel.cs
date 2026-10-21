using CommunityToolkit.Mvvm.ComponentModel;

namespace Vorticis.Desktop.ViewModels;

/// <summary>Tela de descanso: exibida após o login e quando nenhuma função está aberta.</summary>
public partial class InicioViewModel(MainViewModel main) : ModuloViewModel(main)
{
    public string Versao => $"versão {SobreViewModel.Versao}";

    [ObservableProperty] private string? _dica;

    public void Atualizar() =>
        Dica = main.EmpresasPermitidas.Count > 0 ? null
             : main.Sessao?.Administrador == true
                 ? "Nenhuma empresa cadastrada ainda. Quando quiser começar, use Cadastros › Empresas."
                 : "Nenhuma empresa liberada para o seu usuário. Procure o administrador do sistema.";
}
