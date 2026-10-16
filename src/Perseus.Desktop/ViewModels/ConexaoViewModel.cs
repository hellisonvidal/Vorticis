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

    /// <summary>O programa foi instalado com o PostgreSQL embutido (pasta pgsql).</summary>
    public bool BancoLocalDisponivel => Configuracao.BancoLocalDisponivel;

    [ObservableProperty] private bool _usandoBancoLocal;

    public string DescricaoBancoLocal =>
        $"O Perseus inclui um banco de dados próprio, criado e iniciado automaticamente, sem nenhuma configuração. " +
        $"Os dados ficam em {Configuracao.PastaBancoLocal}. Use um servidor PostgreSQL abaixo apenas para compartilhar " +
        "o banco entre vários computadores da rede.";

    public void CarregarDe(string connectionString, string? falha)
    {
        var b = new NpgsqlConnectionStringBuilder(connectionString);
        (Servidor, Porta, Banco, Usuario, Senha) = (b.Host ?? "localhost", b.Port, b.Database ?? "perseus", b.Username ?? "", b.Password ?? "");
        Falha = falha;
        UsandoBancoLocal = main.App is not null && BancoDoAplicativo.EhBancoLocal(main.ConnectionString);
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

    /// <summary>Volta a usar o banco local embutido (criando-o, se ainda não existir) e reconecta.</summary>
    [RelayCommand]
    private async Task UsarBancoLocalAsync()
    {
        Erros.Clear();
        try
        {
            Configuracao.SalvarLocal();
        }
        catch (Exception ex)
        {
            Erros.Add($"Não foi possível gravar a configuração: {ex.Message}");
            return;
        }
        await main.IniciarAsync();
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
