using Npgsql;
using Perseus.Infra.Sistema;

namespace Perseus.Desktop;

/// <summary>Resolve a conexão a usar ao abrir o programa e, no modo local, cria/inicia/para o PostgreSQL embutido.</summary>
public static class BancoDoAplicativo
{
    private static ServidorPostgresEmbutido? _servidor;
    private static int? _portaLocal;

    /// <summary>Connection string pronta para uso (no modo local, com o servidor já em execução).</summary>
    public static async Task<string> PrepararAsync(Action<string> status, CancellationToken ct = default)
    {
        var cfg = Configuracao.Carregar();
        return cfg.Modo == ModoBanco.Servidor ? cfg.ConnectionString! : await PrepararLocalAsync(cfg, status, ct);
    }

    private static async Task<string> PrepararLocalAsync(ConfiguracaoBanco cfg, Action<string> status, CancellationToken ct)
    {
        var bin = ServidorPostgresEmbutido.LocalizarBinarios()
                  ?? throw new InvalidOperationException(
                      "Esta cópia do Perseus não contém o banco de dados local (pasta pgsql ao lado do programa). " +
                      "Instale pelo instalador Setup-PerseusContabil.exe ou informe abaixo um servidor PostgreSQL.");
        _servidor ??= new ServidorPostgresEmbutido(bin, Configuracao.PastaBancoLocal, Configuracao.LogBancoLocal);

        var senha = cfg.Senha;
        if (!_servidor.Inicializado)
        {
            status("Primeira execução: criando o banco de dados local (pode levar até um minuto)...");
            senha ??= ServidorPostgresEmbutido.GerarSenha();
            Configuracao.SalvarLocal(senha: senha);
            await _servidor.InicializarAsync(senha, ct);
        }
        else if (senha is null)
        {
            throw new InvalidOperationException(
                $"O banco local existe em {Configuracao.PastaBancoLocal}, mas a senha dele não está em {Configuracao.ArquivoConfiguracao} " +
                "(arquivo apagado ou copiado de outro usuário do Windows). Restaure o arquivo de configuração a partir de um backup.");
        }

        status("Iniciando o banco de dados local...");
        var porta = await _servidor.IniciarAsync(cfg.Porta > 0 ? cfg.Porta : ServidorPostgresEmbutido.PortaPreferida, ct);
        if (porta != cfg.Porta) Configuracao.SalvarLocal(porta);
        _portaLocal = porta;

        return new NpgsqlConnectionStringBuilder
        {
            Host = "localhost", Port = porta, Database = Configuracao.NomeBanco,
            Username = ServidorPostgresEmbutido.Usuario, Password = senha,
        }.ConnectionString;
    }

    public static bool EhBancoLocal(string connectionString)
    {
        var b = new NpgsqlConnectionStringBuilder(connectionString);
        return _portaLocal == b.Port && b.Host is "localhost" or "127.0.0.1";
    }

    public static int? PortaLocal => _portaLocal;

    /// <summary>
    /// Troca a senha do superusuário do banco local (ALTER ROLE) e grava a nova senha na configuração.
    /// A conexão informada precisa ser a do banco local, ainda com a senha antiga.
    /// </summary>
    public static async Task AlterarSenhaLocalAsync(string connectionString, string novaSenha, CancellationToken ct = default)
    {
        if (!EhBancoLocal(connectionString)) throw new InvalidOperationException("O programa não está usando o banco local.");
        await using (var con = new NpgsqlConnection(connectionString))
        {
            await con.OpenAsync(ct);
            // ALTER ROLE não aceita parâmetro: literal com aspas duplicadas.
            await using var cmd = new NpgsqlCommand($"ALTER ROLE {ServidorPostgresEmbutido.Usuario} PASSWORD '{novaSenha.Replace("'", "''")}'", con);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        Configuracao.SalvarLocal(senha: novaSenha);
        NpgsqlConnection.ClearAllPools();
    }

    /// <summary>Para o banco local (para reiniciar em outra porta); a próxima preparação o inicia de novo.</summary>
    public static void PararLocal()
    {
        Encerrar();
        _portaLocal = null;
    }

    /// <summary>Ao sair do programa: para o servidor local, se foi este processo que o iniciou.</summary>
    public static void Encerrar()
    {
        if (_servidor is { IniciadoPorEsteProcesso: true })
        {
            NpgsqlConnection.ClearAllPools();
            _servidor.Parar();
        }
    }
}
