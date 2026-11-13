using System.Text.Json;

namespace Vorticis.Fiscal.Comum;

/// <summary>Código de uma tabela fiscal (CST, CSOSN, origem, unidade...).</summary>
public sealed record CodigoTabela(string Codigo, string Descricao);

/// <summary>
/// Tabelas de códigos usadas no lançamento manual das notas (origem, CST do ICMS, CSOSN, CST do IPI, CST do PIS/COFINS e unidades), no parâmetro
/// legal TABELAS_FISCAIS (core.parametro_legal; semeado pelo programa e atualizável sem mudar o código).
/// </summary>
public sealed class ServicoTabelasFiscais(IParametrosFiscais parametros)
{
    public const string Chave = "TABELAS_FISCAIS";
    public const string Origem = "origem", CstIcms = "cst_icms", Csosn = "csosn", CstIpi = "cst_ipi", CstPisCofins = "cst_pis_cofins", Unidade = "unidade";

    public static IReadOnlyDictionary<string, IReadOnlyList<CodigoTabela>> Carregar(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("tabelas").EnumerateObject().ToDictionary(t => t.Name,
            t => (IReadOnlyList<CodigoTabela>)[.. t.Value.EnumerateArray().Select(x => new CodigoTabela(x.GetProperty("codigo").GetString()!, x.GetProperty("descricao").GetString()!))]);
    }

    /// <summary>Os códigos da tabela (vazio se não existir).</summary>
    public async Task<IReadOnlyList<CodigoTabela>> ListarAsync(string tabela, CancellationToken ct = default) =>
        Carregar(await parametros.ObterJsonAsync(Chave, DateOnly.FromDateTime(DateTime.Today), ct)).GetValueOrDefault(tabela) ?? [];
}
