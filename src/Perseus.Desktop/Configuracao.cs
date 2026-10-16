using System.Text.Json;

namespace Perseus.Desktop;

/// <summary>
/// Conexão com o PostgreSQL local. Ordem: variável PERSEUS_DB → %APPDATA%\Perseus\perseus.json
/// ({"ConnectionString": "..."}) → padrão do instalador.
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
            if (File.Exists(ArquivoConfiguracao))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(ArquivoConfiguracao));
                if (doc.RootElement.TryGetProperty("ConnectionString", out var cs) && cs.GetString() is { Length: > 0 } valor)
                    return valor;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // configuração ilegível: usa o padrão e a tela mostra o erro de conexão, se houver
        }
        return Padrao;
    }
}
