using Perseus.Domain.Comum;
using Perseus.Domain.Seguranca;

namespace Perseus.Application.Sistema;

public sealed record ArquivoBackup(string Caminho, DateTimeOffset CriadoEm, long Bytes)
{
    public string Nome => Path.GetFileName(Caminho);
}

/// <summary>Cópia de segurança do banco inteiro (todas as empresas).</summary>
public interface IBackupBanco
{
    /// <summary>Gera um arquivo de backup em <paramref name="pasta"/> e devolve o caminho.</summary>
    Task<string> GerarAsync(string pasta, string prefixo, CancellationToken ct);

    /// <summary>Substitui o conteúdo do banco pelo do arquivo.</summary>
    Task RestaurarAsync(string arquivo, CancellationToken ct);
}

/// <summary>
/// Configurações › Backup e restauração (somente administrador). Antes de restaurar, gera automaticamente
/// um backup de segurança do estado atual, para que a restauração possa ser desfeita.
/// </summary>
public sealed class ServicoBackup(IBackupBanco backup)
{
    public const string Extensao = ".backup";

    public static string PastaPadrao =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Perseus Contabil", "Backups");

    public static IReadOnlyList<ArquivoBackup> Listar(string pasta) =>
        Directory.Exists(pasta)
            ? new DirectoryInfo(pasta).GetFiles("*" + Extensao)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => new ArquivoBackup(f.FullName, new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero), f.Length))
                .ToArray()
            : [];

    public async Task<Resultado<string>> GerarAsync(SessaoUsuario sessao, string pasta, CancellationToken ct = default)
    {
        if (!sessao.Administrador) return Resultado<string>.Falha("Somente administradores fazem backup.");
        try
        {
            Directory.CreateDirectory(pasta);
            return Resultado<string>.Ok(await backup.GerarAsync(pasta, "perseus", ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Resultado<string>.Falha($"Falha no backup: {ex.Message}");
        }
    }

    /// <returns>Caminho do backup de segurança gerado antes da restauração.</returns>
    public async Task<Resultado<string>> RestaurarAsync(SessaoUsuario sessao, string arquivo, string pastaSeguranca, CancellationToken ct = default)
    {
        if (!sessao.Administrador) return Resultado<string>.Falha("Somente administradores restauram backups.");
        if (!File.Exists(arquivo)) return Resultado<string>.Falha($"Arquivo não encontrado: {arquivo}");
        string seguranca;
        try
        {
            Directory.CreateDirectory(pastaSeguranca);
            seguranca = await backup.GerarAsync(pastaSeguranca, "antes_da_restauracao", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Resultado<string>.Falha($"Restauração cancelada: não foi possível gerar o backup de segurança ({ex.Message}).");
        }
        try
        {
            await backup.RestaurarAsync(arquivo, ct);
            return Resultado<string>.Ok(seguranca);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Resultado<string>.Falha($"Falha na restauração: {ex.Message}. O estado anterior está salvo em {seguranca}.");
        }
    }
}
