using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npgsql;

namespace Perseus.Desktop.ViewModels;

/// <summary>Configuração da conexão com o PostgreSQL (antes do login, se a conexão falhar; depois, só administrador).</summary>
public partial class ConexaoViewModel(MainViewModel main) : ModuloViewModel(main)
{
    [ObservableProperty] private string _servidor = "localhost";
    [ObservableProperty] private decimal? _porta = 5432;
    [ObservableProperty] private string _banco = "perseus";
    [ObservableProperty] private string _usuario = "perseus";
    [ObservableProperty] private string _senha = "";
    [ObservableProperty] private string? _falha;

    public string Arquivo => Configuracao.ArquivoConfiguracao;

    public void CarregarDe(string connectionString, string? falha)
    {
        var b = new NpgsqlConnectionStringBuilder(connectionString);
        (Servidor, Porta, Banco, Usuario, Senha) = (b.Host ?? "localhost", b.Port, b.Database ?? "perseus", b.Username ?? "", b.Password ?? "");
        Falha = falha;
        Erros.Clear();
        Mensagem = null;
    }

    private string Montar() => new NpgsqlConnectionStringBuilder
    {
        Host = Servidor.Trim(), Port = (int)(Porta ?? 5432), Database = Banco.Trim(), Username = Usuario.Trim(), Password = Senha,
    }.ConnectionString;

    [RelayCommand]
    private async Task TestarAsync()
    {
        Erros.Clear();
        Mensagem = null;
        try
        {
            await using var con = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(Montar()) { Database = "postgres", Timeout = 5 }.ConnectionString);
            await con.OpenAsync();
            Mensagem = $"Conexão com o servidor OK (PostgreSQL {con.PostgreSqlVersion}).";
        }
        catch (Exception ex)
        {
            Erros.Add($"Falha: {ex.Message}");
        }
    }

    /// <summary>Grava a configuração e reconecta (volta à tela de login).</summary>
    [RelayCommand]
    private async Task SalvarEConectarAsync()
    {
        Erros.Clear();
        try
        {
            Configuracao.Salvar(Montar());
        }
        catch (Exception ex)
        {
            Erros.Add($"Não foi possível gravar a configuração: {ex.Message}");
            return;
        }
        await main.InicializarAsync(Montar());
    }
}
