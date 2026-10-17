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
    [ObservableProperty] private bool _mostrarSenha;

    /// <summary>Modo escolhido na tela: banco local do Perseus ou servidor PostgreSQL.</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(EditandoServidor))] private bool _usarBancoLocal;

    // banco local: porta e troca de senha
    [ObservableProperty] private decimal? _portaLocal = Perseus.Infra.Sistema.ServidorPostgresEmbutido.PortaPreferida;
    [ObservableProperty] private string _novaSenhaLocal = "";
    [ObservableProperty] private string _confirmacaoSenhaLocal = "";

    public bool EditandoServidor
    {
        get => !UsarBancoLocal;
        set => UsarBancoLocal = !value;
    }
    public char CaractereSenha => MostrarSenha ? '\0' : '●';
    partial void OnMostrarSenhaChanged(bool value) => OnPropertyChanged(nameof(CaractereSenha));

    public string ConexaoAtual => main.App is null || string.IsNullOrEmpty(main.ConnectionString)
        ? "Sem conexão."
        : $"Em uso: {Configuracao.Descrever(main.ConnectionString)}";

    public string PastaBancoLocal => Configuracao.PastaBancoLocal;

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
        UsarBancoLocal = BancoLocalDisponivel && (UsandoBancoLocal || Configuracao.Carregar().Modo == ModoBanco.Local);
        var cfg = Configuracao.Carregar();
        PortaLocal = BancoDoAplicativo.PortaLocal ?? (cfg.Modo == ModoBanco.Local && cfg.Porta > 0 ? cfg.Porta : Perseus.Infra.Sistema.ServidorPostgresEmbutido.PortaPreferida);
        (NovaSenhaLocal, ConfirmacaoSenhaLocal, MostrarSenha) = ("", "", false);
        OnPropertyChanged(nameof(ConexaoAtual));
        Erros.Clear();
        Mensagem = null;
    }

    /// <summary>Salva o modo escolhido (local ou servidor) e reconecta.</summary>
    [RelayCommand]
    private async Task SalvarConfiguracaoAsync()
    {
        if (UsarBancoLocal) await SalvarLocalAsync();
        else await SalvarEConectarAsync();
    }

    private async Task SalvarLocalAsync()
    {
        Erros.Clear();
        Mensagem = null;
        var porta = (int)(PortaLocal ?? 0);
        if (porta is < 1024 or > 65535) { Erros.Add("Porta do banco local deve estar entre 1024 e 65535."); return; }
        var trocarSenha = NovaSenhaLocal.Length > 0 || ConfirmacaoSenhaLocal.Length > 0;
        if (trocarSenha)
        {
            if (NovaSenhaLocal.Length < 8) { Erros.Add("A nova senha do banco local deve ter ao menos 8 caracteres."); return; }
            if (NovaSenhaLocal != ConfirmacaoSenhaLocal) { Erros.Add("A confirmação não confere com a nova senha."); return; }
            if (!UsandoBancoLocal) { Erros.Add("Para trocar a senha, conecte-se primeiro ao banco local."); return; }
        }
        try
        {
            if (trocarSenha) await BancoDoAplicativo.AlterarSenhaLocalAsync(main.ConnectionString, NovaSenhaLocal);
            var portaMudou = UsandoBancoLocal && BancoDoAplicativo.PortaLocal != porta;
            Configuracao.SalvarLocal(porta);
            if (portaMudou) BancoDoAplicativo.PararLocal();   // reinicia na nova porta ao reconectar
        }
        catch (Exception ex)
        {
            Erros.Add($"Não foi possível gravar a configuração: {ex.Message}");
            return;
        }
        await main.IniciarAsync();
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
