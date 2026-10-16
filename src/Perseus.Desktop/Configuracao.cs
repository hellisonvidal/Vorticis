using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;

namespace Perseus.Desktop;

/// <summary>
/// Conexão com o PostgreSQL local. Ordem: variável PERSEUS_DB → %APPDATA%\Perseus\perseus.json → padrão do instalador.
/// No Windows a senha é gravada cifrada com DPAPI (só o mesmo usuário do Windows consegue decifrar).
/// </summary>
public static class Configuracao
{
    public const string Padrao = "Host=localhost;Port=5432;Database=perseus;Username=perseus;Password=perseus";

    public static string ArquivoConfiguracao =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Perseus", "perseus.json");

    public static string ObterConnectionString()
    {
        var env = Environment.GetEnvironmentVariable("PERSEUS_DB");
        if (!string.IsNullOrWhiteSpace(env)) return env;

        try
        {
            if (File.Exists(ArquivoConfiguracao) && JsonNode.Parse(File.ReadAllText(ArquivoConfiguracao)) is JsonObject cfg
                && cfg["ConnectionString"]?.GetValue<string>() is { Length: > 0 } cs)
            {
                if (cfg["SenhaProtegida"]?.GetValue<string>() is { Length: > 0 } protegida && OperatingSystem.IsWindows())
                {
                    var senha = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protegida), null, DataProtectionScope.CurrentUser));
                    return new NpgsqlConnectionStringBuilder(cs) { Password = senha }.ConnectionString;
                }
                return cs;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or FormatException
                                       or CryptographicException or InvalidOperationException or ArgumentException)
        {
            // configuração ilegível: usa o padrão; a tela de conexão mostra o erro, se houver
        }
        return Padrao;
    }

    public static void Salvar(string connectionString)
    {
        var b = new NpgsqlConnectionStringBuilder(connectionString);
        var cfg = new JsonObject();
        if (OperatingSystem.IsWindows() && !string.IsNullOrEmpty(b.Password))
        {
            cfg["SenhaProtegida"] = Convert.ToBase64String(
                ProtectedData.Protect(Encoding.UTF8.GetBytes(b.Password), null, DataProtectionScope.CurrentUser));
            b.Password = null;
        }
        cfg["ConnectionString"] = b.ConnectionString;
        Directory.CreateDirectory(Path.GetDirectoryName(ArquivoConfiguracao)!);
        File.WriteAllText(ArquivoConfiguracao, cfg.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Descrição sem senha, para exibir na tela.</summary>
    public static string Descrever(string connectionString)
    {
        var b = new NpgsqlConnectionStringBuilder(connectionString);
        return $"{b.Host}:{b.Port}/{b.Database} (usuário {b.Username})";
    }
}
