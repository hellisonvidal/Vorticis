using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vorticis.Desktop.Comum.ViewModels;

/// <summary>
/// Configurações › Atualizações do sistema: versão instalada, a publicada (site do Vorticis ou GitHub), o que mudou e Atualizar agora, que baixa o
/// instalador e o executa por cima da instalação (os dados ficam; o banco é atualizado na primeira abertura).
/// </summary>
public partial class AtualizacoesViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public string VersaoInstalada => SobreViewModel.Versao;
    public string? Site => Atualizacoes.Origem();

    [ObservableProperty] private string _situacao = "";
    [ObservableProperty] private string _notas = "";
    [ObservableProperty] private string? _instalador;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PodeAtualizar))] private bool _temNova;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PodeAtualizar))] private bool _baixando;
    [ObservableProperty] private double _progresso;
    public bool PodeAtualizar => TemNova && Instalador is not null && !Baixando;

    public async Task VerificarAsync()
    {
        Erros.Clear();
        (TemNova, Instalador, Notas) = (false, null, "");
        if (Site is null)
        {
            Situacao = "Não há onde procurar atualizações nesta instalação: as atualizações vêm pelo instalador novo.";
            return;
        }
        Situacao = "Procurando atualizações...";
        try
        {
            var info = await Atualizacoes.ConsultarAsync();
            if (info is null) return;
            TemNova = Atualizacoes.MaisNova(info.Versao, VersaoInstalada);
            Instalador = info.Instalador;
            OnPropertyChanged(nameof(PodeAtualizar));
            Notas = info.Notas ?? "";
            Situacao = TemNova
                ? $"Há uma versão nova: {info.Versao}{(info.Publicada is { } d ? $" (publicada em {d})" : "")}. Você usa a {VersaoInstalada}."
                : $"Você já usa a versão mais nova ({VersaoInstalada}).";
        }
        catch (Exception ex)
        {
            Situacao = $"Não foi possível consultar as atualizações ({Site}): {ex.Message}";
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

    /// <summary>
    /// Atualizar agora: baixa o instalador da versão nova (com o andamento) e o executa por cima da instalação; o Vorticis fecha e o instalador o reabre
    /// ao terminar. Feche o programa nos outros computadores antes. Fora do Windows, só baixa e mostra onde ficou o arquivo.
    /// </summary>
    [RelayCommand]
    private async Task AtualizarAgoraAsync()
    {
        Erros.Clear();
        if (Instalador is not { } url || !TemNova) { Erros.Add("Não há versão nova para instalar."); return; }
        if (!await Contexto.ConfirmarAsync("Atualizar o Vorticis",
                "O instalador da versão nova será baixado e executado: o Vorticis fecha durante a atualização e abre de novo ao terminar. "
                + "Os dados são preservados. Feche o programa nos outros computadores antes de continuar.", "Atualizar agora", "Cancelar")) return;
        Baixando = true;
        Progresso = 0;
        try
        {
            Situacao = "Baixando a versão nova...";
            var arquivo = await Atualizacoes.BaixarAsync(url, new Progress<double>(p => Progresso = p));
            if (!Atualizacoes.Executar(arquivo))
            {
                Situacao = $"Instalador baixado em {arquivo}. Feche o Vorticis e execute-o para atualizar.";
                return;
            }
            Situacao = "Atualizando: o Vorticis será fechado e aberto de novo ao terminar.";
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime vida) vida.Shutdown();
        }
        catch (Exception ex)
        {
            Erros.Add($"Não foi possível baixar a atualização: {ex.Message}. Use \"Baixar no navegador\".");
            Situacao = "";
        }
        finally { Baixando = false; }
    }
}
