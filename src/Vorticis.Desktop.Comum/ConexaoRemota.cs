using Npgsql;

namespace Vorticis.Desktop.Comum;

/// <summary>
/// Conexão com um servidor PostgreSQL na rede ou na nuvem: conexão segura (SSL/TLS), tempo limite para abrir a conexão e
/// keepalive (mantém a conexão viva em redes que derrubam conexões paradas, comum na nuvem).
/// </summary>
public static class ConexaoRemota
{
    public const int TempoLimitePadrao = 15;

    public static IReadOnlyList<(SslMode Modo, string Nome)> Modos { get; } =
    [
        (SslMode.Prefer, "Usar se o servidor oferecer (padrão)"),
        (SslMode.Require, "Exigir conexão segura (nuvem)"),
        (SslMode.VerifyFull, "Exigir e conferir o certificado do servidor"),
        (SslMode.Disable, "Desligada (só rede interna)"),
    ];

    public static string Montar(string servidor, int porta, string banco, string usuario, string senha, int modoSsl, int tempoLimite, int keepAlive)
    {
        var b = new NpgsqlConnectionStringBuilder
        {
            Host = servidor.Trim(), Port = porta, Database = banco.Trim(), Username = usuario.Trim(), Password = senha,
            SslMode = Modos[Math.Clamp(modoSsl, 0, Modos.Count - 1)].Modo,
            Timeout = Math.Clamp(tempoLimite, 1, 1024),
        };
        if (keepAlive > 0) b.KeepAlive = Math.Min(keepAlive, 3600);
        return b.ConnectionString;
    }

    /// <summary>Índice do modo SSL, tempo limite e keepalive de uma conexão já gravada.</summary>
    public static (int ModoSsl, decimal TempoLimite, decimal? KeepAlive) Ler(NpgsqlConnectionStringBuilder b)
    {
        var i = Modos.ToList().FindIndex(m => m.Modo == b.SslMode);
        return (i < 0 ? 0 : i, b.Timeout, b.KeepAlive > 0 ? b.KeepAlive : null);
    }

    /// <summary>Servidor fora deste computador e da rede interna: recomenda-se exigir a conexão segura.</summary>
    public static bool PareceNuvem(string servidor)
    {
        var s = servidor.Trim().ToLowerInvariant();
        if (s is "" or "localhost" or "127.0.0.1" or "::1" || !s.Contains('.')) return false;
        return !(s.StartsWith("10.") || s.StartsWith("192.168.") || System.Text.RegularExpressions.Regex.IsMatch(s, @"^172\.(1[6-9]|2\d|3[01])\.") || s.EndsWith(".local"));
    }
}
