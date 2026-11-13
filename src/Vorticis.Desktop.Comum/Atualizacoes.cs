using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vorticis.Desktop.Comum;

/// <summary>O que o site do Vorticis publica em atualizacao.json (deploy/publicar.sh gera o arquivo).</summary>
public sealed record InfoAtualizacao(
    [property: JsonPropertyName("versao")] string Versao,
    [property: JsonPropertyName("instalador")] string? Instalador = null,
    [property: JsonPropertyName("notas")] string? Notas = null,
    [property: JsonPropertyName("publicada")] string? Publicada = null);

/// <summary>
/// Configurações › Atualizações do sistema: compara a versão instalada com a publicada no site do Vorticis (VorticisSite no Directory.Build.props,
/// ou o arquivo site.txt na pasta do programa, que tem prioridade) ou, sem site, com a última versão publicada no GitHub (Releases do
/// repositório do Vorticis). Baixa o instalador e o executa por cima da instalação, sem perder os dados.
/// </summary>
public static class Atualizacoes
{
    /// <summary>Endereço do site; substituível nos testes.</summary>
    public static Func<string?> Site { get; set; } = SiteConfigurado;

    public static string? SiteConfigurado()
    {
        try
        {
            var arquivo = Path.Combine(AppContext.BaseDirectory, "site.txt");
            if (File.Exists(arquivo) && File.ReadAllText(arquivo).Trim() is { Length: > 0 } local) return local.TrimEnd('/');
        }
        catch (Exception) { /* sem acesso à pasta: usa o do build */ }
        var doBuild = typeof(Atualizacoes).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "VorticisSite")?.Value;
        return string.IsNullOrWhiteSpace(doBuild) ? null : doBuild.Trim().TrimEnd('/');
    }

    public static HttpMessageHandler? Handler { get; set; }

    /// <summary>Repositório do GitHub com as versões publicadas (Releases), usado quando não há site configurado; substituível nos testes.</summary>
    public static Func<string?> RepositorioGitHub { get; set; } = () => "hellisonvidal/Vorticis";

    /// <summary>Onde as atualizações são procuradas (para mostrar na tela).</summary>
    public static string? Origem() => Site() ?? (RepositorioGitHub() is { } r ? $"https://github.com/{r}/releases" : null);

    private static HttpClient Cliente(TimeSpan timeout)
    {
        var http = Handler is null ? new HttpClient() : new HttpClient(Handler, disposeHandler: false);
        http.Timeout = timeout;
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Vorticis/{ViewModels.SobreViewModel.Versao}");
        return http;
    }

    /// <summary>
    /// Lê o atualizacao.json do site (10 s no máximo) ou, sem site, a última versão publicada no GitHub (com o Setup-Vorticis-x.exe).
    /// Null se não houver onde procurar.
    /// </summary>
    public static async Task<InfoAtualizacao?> ConsultarAsync(CancellationToken ct = default)
    {
        using var http = Cliente(TimeSpan.FromSeconds(10));
        if (Site() is { } site) return await http.GetFromJsonAsync<InfoAtualizacao>($"{site}/atualizacao.json", ct);
        if (RepositorioGitHub() is not { } repo) return null;
        using var doc = JsonDocument.Parse(await http.GetStringAsync($"https://api.github.com/repos/{repo}/releases/latest", ct));
        var r = doc.RootElement;
        var versao = r.GetProperty("tag_name").GetString()!.TrimStart('v');
        string? instalador = null;
        if (r.TryGetProperty("assets", out var assets))
            foreach (var a in assets.EnumerateArray())
                if (a.GetProperty("name").GetString() is { } nome && nome.StartsWith("Setup-Vorticis", StringComparison.OrdinalIgnoreCase)
                    && nome.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    instalador = a.GetProperty("browser_download_url").GetString();
        var notas = r.TryGetProperty("body", out var b) ? b.GetString() : null;
        if (notas is { } n && n.IndexOf("\n---", StringComparison.Ordinal) is var corte and > 0) notas = n[..corte].Trim();   // só a versão nova
        var publicada = r.TryGetProperty("published_at", out var p) && p.ValueKind == JsonValueKind.String && DateTime.TryParse(p.GetString(), out var d)
            ? d.ToString("dd/MM/yyyy") : null;
        return new InfoAtualizacao(versao, instalador, notas, publicada);
    }

    /// <summary>Baixa o instalador para a pasta temporária do Vorticis, informando o andamento (0 a 100). Devolve o caminho do arquivo.</summary>
    public static async Task<string> BaixarAsync(string url, IProgress<double>? progresso = null, CancellationToken ct = default)
    {
        var pasta = Path.Combine(Path.GetTempPath(), "VorticisAtualizacao");
        Directory.CreateDirectory(pasta);
        var nome = Path.GetFileName(new Uri(url).LocalPath);
        var destino = Path.Combine(pasta, string.IsNullOrWhiteSpace(nome) ? "Setup-Vorticis.exe" : nome);
        using var http = Cliente(TimeSpan.FromMinutes(30));
        using var resposta = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resposta.EnsureSuccessStatusCode();
        var total = resposta.Content.Headers.ContentLength;
        await using (var origem = await resposta.Content.ReadAsStreamAsync(ct))
        await using (var arquivo = File.Create(destino + ".parcial"))
        {
            var buffer = new byte[81920];
            long lido = 0;
            int n;
            while ((n = await origem.ReadAsync(buffer, ct)) > 0)
            {
                await arquivo.WriteAsync(buffer.AsMemory(0, n), ct);
                lido += n;
                if (total is > 0) progresso?.Report(lido * 100.0 / total.Value);
            }
        }
        File.Move(destino + ".parcial", destino, overwrite: true);
        progresso?.Report(100);
        return destino;
    }

    /// <summary>
    /// Executa o instalador baixado por cima da instalação, sem perguntas (/SILENT), fechando e reabrindo o Vorticis. Só no Windows;
    /// substituível nos testes. Devolve false se não foi possível executar.
    /// </summary>
    public static Func<string, bool> Executar { get; set; } = arquivo =>
    {
        if (!OperatingSystem.IsWindows()) return false;
        Process.Start(new ProcessStartInfo(arquivo, "/SILENT /SUPPRESSMSGBOXES /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /NORESTART") { UseShellExecute = true });
        return true;
    };

    /// <summary>A versão publicada é mais nova que a instalada?</summary>
    public static bool MaisNova(string publicada, string instalada)
        => Version.TryParse(publicada.Trim().TrimStart('v'), out var p) && Version.TryParse(instalada.Trim().TrimStart('v'), out var i) && p > i;
}
