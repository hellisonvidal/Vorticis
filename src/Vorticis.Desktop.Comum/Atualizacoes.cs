using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Vorticis.Desktop.Comum;

/// <summary>O que o site do Vorticis publica em atualizacao.json (deploy/publicar.sh gera o arquivo).</summary>
public sealed record InfoAtualizacao(
    [property: JsonPropertyName("versao")] string Versao,
    [property: JsonPropertyName("instalador")] string? Instalador = null,
    [property: JsonPropertyName("notas")] string? Notas = null,
    [property: JsonPropertyName("publicada")] string? Publicada = null);

/// <summary>
/// Ajuda › Verificar atualizações: compara a versão instalada com a publicada no site do Vorticis (VorticisSite no Directory.Build.props,
/// ou o arquivo site.txt na pasta do programa, que tem prioridade). Sem endereço configurado, não procura.
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

    /// <summary>Lê o atualizacao.json do site (10 s no máximo). Null se não houver site configurado.</summary>
    public static async Task<InfoAtualizacao?> ConsultarAsync(CancellationToken ct = default)
    {
        if (Site() is not { } site) return null;
        using var http = Handler is null ? new HttpClient() : new HttpClient(Handler, disposeHandler: false);
        http.Timeout = TimeSpan.FromSeconds(10);
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Vorticis/{ViewModels.SobreViewModel.Versao}");
        return await http.GetFromJsonAsync<InfoAtualizacao>($"{site}/atualizacao.json", ct);
    }

    /// <summary>A versão publicada é mais nova que a instalada?</summary>
    public static bool MaisNova(string publicada, string instalada)
        => Version.TryParse(publicada.Trim().TrimStart('v'), out var p) && Version.TryParse(instalada.Trim().TrimStart('v'), out var i) && p > i;
}
