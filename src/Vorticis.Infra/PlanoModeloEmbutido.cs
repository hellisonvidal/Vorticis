using System.Text.Json;
using Vorticis.Application.Cadastros;
using Vorticis.Domain.Contabil;

namespace Vorticis.Infra;

/// <summary>Plano de contas modelo (Dados/plano_modelo.json) para empresas comerciais e de serviços.</summary>
public sealed class PlanoModeloEmbutido : IFontePlanoModelo
{
    private sealed record Item(string Codigo, string Descricao, string CodNat, string Natureza);

    private static readonly Lazy<IReadOnlyList<ContaModelo>> Plano = new(() =>
    {
        using var s = typeof(PlanoModeloEmbutido).Assembly.GetManifestResourceStream("Dados.plano_modelo.json")
                      ?? throw new InvalidOperationException("Recurso plano_modelo.json não encontrado.");
        var itens = JsonSerializer.Deserialize<Item[]>(s, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return itens.Select(i => new ContaModelo(i.Codigo, i.Descricao, i.CodNat, Enum.Parse<IndicadorDC>(i.Natureza))).ToArray();
    });

    public IReadOnlyList<ContaModelo> Obter() => Plano.Value;
}
