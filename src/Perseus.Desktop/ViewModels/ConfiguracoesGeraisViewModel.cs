using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Application.Sistema;

namespace Perseus.Desktop.ViewModels;

/// <summary>Configurações › Configurações gerais (administrador): tipo de contabilização da tela de lançamento.</summary>
public partial class ConfiguracoesGeraisViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PartidaSimples))] private bool _partidaDobrada = true;

    public bool PartidaSimples
    {
        get => !PartidaDobrada;
        set => PartidaDobrada = !value;
    }

    public bool PodeAlterar => Contexto.Sessao?.Administrador == true;

    public async Task CarregarAsync()
    {
        OnPropertyChanged(nameof(PodeAlterar));
        if (Contexto.App is not { } app) return;
        var c = await app.ConfiguracoesGerais.ObterAsync();
        PartidaDobrada = c.ModoLancamento == ModoLancamento.PartidaDobrada;
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Erros.Clear();
        Mensagem = null;
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao) return;
        var r = await app.ConfiguracoesGerais.SalvarAsync(sessao,
            new ConfiguracoesGerais(PartidaDobrada ? ModoLancamento.PartidaDobrada : ModoLancamento.PartidaSimples));
        MostrarResultado(r, "Configurações salvas. A tela de lançamento passa a usar o novo modo na próxima abertura.");
    }
}
