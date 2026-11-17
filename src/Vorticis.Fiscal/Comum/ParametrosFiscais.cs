using System.Reflection;

namespace Vorticis.Fiscal.Comum;

/// <summary>Chaves dos parâmetros legais do módulo Fiscal em core.parametro_legal.</summary>
public static class ChavesParametro
{
    public const string SimplesNacional = "SIMPLES_NACIONAL";
    public const string LucroPresumidoReal = "LUCRO_PRESUMIDO_REAL";
    public const string Retencoes = "RETENCOES_GUIAS";
    public const string Planejamento = "PLANEJAMENTO_TRIBUTARIO";
    /// <summary>Janelas de opção e cancelamento do IBS/CBS pelo regime regular, para o Simples Nacional (a partir de 2027).</summary>
    public const string SimplesOpcaoIbsCbs = "SIMPLES_OPCAO_IBSCBS";
}

/// <summary>Leitura dos parâmetros legais vigentes em uma data (tabelas, alíquotas, códigos de receita).</summary>
public interface IParametrosFiscais
{
    /// <summary>JSON do parâmetro vigente na data. Sem registro no banco, usa (e semeia) o parâmetro embutido no programa.</summary>
    Task<string> ObterJsonAsync(string chave, DateOnly data, CancellationToken ct = default);
}

/// <summary>Parâmetros embutidos no assembly (Parametros\*.json): a semente de core.parametro_legal e o padrão dos testes.</summary>
public static class ParametrosEmbutidos
{
    public static string Json(string chave)
    {
        var assembly = typeof(ParametrosEmbutidos).Assembly;
        var nome = $"Vorticis.Fiscal.Parametros.{chave}.json";
        using var s = assembly.GetManifestResourceStream(nome) ?? throw new InvalidOperationException($"Parâmetro embutido ausente: {nome}.");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    /// <summary>
    /// Todas as vigências embutidas da chave: o arquivo base (<c>CHAVE.json</c>) e as novas vigências (<c>CHAVE.AAAA.json</c>), ordenadas pela
    /// vigência declarada no próprio JSON.
    /// </summary>
    public static IReadOnlyList<(DateOnly Vigencia, string Json)> Vigencias(string chave)
    {
        var assembly = typeof(ParametrosEmbutidos).Assembly;
        var prefixo = $"Vorticis.Fiscal.Parametros.{chave}.";
        var nomes = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefixo, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal) && n.Length == prefixo.Length + 4 + 5
                        && n.Substring(prefixo.Length, 4).All(char.IsDigit))
            .Append($"Vorticis.Fiscal.Parametros.{chave}.json");
        var lista = new List<(DateOnly, string)>();
        foreach (var nome in nomes)
        {
            using var s = assembly.GetManifestResourceStream(nome)!;
            using var r = new StreamReader(s);
            var json = r.ReadToEnd();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            lista.Add((DateOnly.ParseExact(doc.RootElement.GetProperty("vigencia_inicio").GetString()!, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), json));
        }
        return lista.OrderBy(x => x.Item1).ToList();
    }

    /// <summary>JSON embutido vigente na data (o último com vigência até a data; sem nenhum, o mais antigo).</summary>
    public static string Json(string chave, DateOnly data)
    {
        var v = Vigencias(chave);
        return (v.LastOrDefault(x => x.Vigencia <= data) is { Json: not null } e ? e : v[0]).Json;
    }

    /// <summary>Provedor sem banco, só com os parâmetros embutidos (respeita as vigências).</summary>
    public sealed class Provedor : IParametrosFiscais
    {
        public Task<string> ObterJsonAsync(string chave, DateOnly data, CancellationToken ct = default) => Task.FromResult(Json(chave, data));
    }
}
