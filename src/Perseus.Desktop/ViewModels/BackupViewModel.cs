using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Application.Sistema;

namespace Perseus.Desktop.ViewModels;

public sealed record BackupLinha(ArquivoBackup Arquivo)
{
    public string Nome => Arquivo.Nome;
    public string Data => Arquivo.CriadoEm.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string Tamanho => Arquivo.Bytes >= 1_048_576 ? $"{Arquivo.Bytes / 1_048_576.0:N1} MB" : $"{Arquivo.Bytes / 1024.0:N0} KB";
}

/// <summary>Configurações › Backup e restauração (somente administrador).</summary>
public partial class BackupViewModel(MainViewModel main) : ModuloViewModel(main)
{
    public const string PalavraConfirmacao = "RESTAURAR";

    public ObservableCollection<BackupLinha> Arquivos { get; } = [];

    [ObservableProperty] private string _pasta = ServicoBackup.PastaPadrao;
    [ObservableProperty] private BackupLinha? _selecionado;
    [ObservableProperty] private string _confirmacao = "";

    [RelayCommand]
    private void AtualizarLista() => Atualizar();

    public void Atualizar()
    {
        Arquivos.Clear();
        foreach (var a in ServicoBackup.Listar(Pasta)) Arquivos.Add(new BackupLinha(a));
    }

    [RelayCommand]
    private async Task GerarAsync()
    {
        Erros.Clear();
        Mensagem = null;
        if (main.App is not { } app || main.Sessao is not { } sessao) return;
        try
        {
            Ocupado = true;
            var r = await app.Backup.GerarAsync(sessao, Pasta);
            MostrarResultado(r, r.Sucesso ? $"Backup gerado: {r.Valor}" : "");
            Atualizar();
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>Substitui o banco pelo backup selecionado; exige digitar a palavra de confirmação. Encerra a sessão ao final.</summary>
    [RelayCommand]
    private async Task RestaurarAsync()
    {
        Erros.Clear();
        Mensagem = null;
        if (main.App is not { } app || main.Sessao is not { } sessao) return;
        if (Selecionado is null) { Erros.Add("Selecione o backup a restaurar."); return; }
        if (Confirmacao.Trim() != PalavraConfirmacao) { Erros.Add($"Para confirmar, digite {PalavraConfirmacao} no campo de confirmação."); return; }
        try
        {
            Ocupado = true;
            var origem = Selecionado.Arquivo.Caminho;
            var r = await app.Backup.RestaurarAsync(sessao, origem, Pasta);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            Confirmacao = "";
            // Reconecta (reaplica migrações, se o backup for de versão anterior) e volta ao login.
            await main.InicializarAsync(main.ConnectionString);
            main.Login.Mensagem = $"Banco restaurado de {Path.GetFileName(origem)}. O estado anterior foi salvo em {Path.GetFileName(r.Valor)}. Entre novamente.";
        }
        finally
        {
            Ocupado = false;
        }
    }
}
