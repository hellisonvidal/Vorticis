using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vorticis.Desktop.Comum.ViewModels;

/// <summary>Ajuda › Verificar atualizações: versão instalada, a publicada no site e o link para baixar o instalador.</summary>
public partial class AtualizacoesViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public string VersaoInstalada => SobreViewModel.Versao;
    public string? Site => Atualizacoes.Site();

    [ObservableProperty] private string _situacao = "";
    [ObservableProperty] private string _notas = "";
    [ObservableProperty] private string? _instalador;
    [ObservableProperty] private bool _temNova;

    public async Task VerificarAsync()
    {
        Erros.Clear();
        (TemNova, Instalador, Notas) = (false, null, "");
        if (Site is null)
        {
            Situacao = "O endereço do site do Vorticis ainda não foi configurado nesta instalação: as atualizações vêm pelo instalador novo.";
            return;
        }
        Situacao = "Procurando atualizações...";
        try
        {
            var info = await Atualizacoes.ConsultarAsync();
            if (info is null) return;
            TemNova = Atualizacoes.MaisNova(info.Versao, VersaoInstalada);
            Instalador = info.Instalador;
            Notas = info.Notas ?? "";
            Situacao = TemNova
                ? $"Há uma versão nova: {info.Versao}{(info.Publicada is { } d ? $" (publicada em {d})" : "")}. Você usa a {VersaoInstalada}."
                : $"Você já usa a versão mais nova ({VersaoInstalada}).";
        }
        catch (Exception ex)
        {
            Situacao = $"Não foi possível consultar o site ({Site}): {ex.Message}";
        }
    }

    [RelayCommand]
    private Task VerificarNovamenteAsync() => VerificarAsync();

    /// <summary>Abre o download do instalador no navegador; ao terminar, rode-o por cima da instalação (os dados ficam).</summary>
    [RelayCommand]
    private void Baixar()
    {
        if (Instalador is null) return;
        try { Process.Start(new ProcessStartInfo(Instalador) { UseShellExecute = true }); }
        catch (Exception ex) { Erros.Add($"Não foi possível abrir o navegador: {ex.Message}. Endereço: {Instalador}"); }
    }
}
