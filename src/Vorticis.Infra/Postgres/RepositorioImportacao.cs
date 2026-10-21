using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using NpgsqlTypes;
using Vorticis.Application.Contabil;
using Vorticis.Domain.Contabil;

namespace Vorticis.Infra.Postgres;

public sealed class RepositorioImportacao(NpgsqlDataSource ds) : IRepositorioImportacao
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<IReadOnlyList<LayoutSalvo>> ListarLayoutsAsync(CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("SELECT id, nome, configuracao::text FROM contabil.layout_importacao ORDER BY lower(nome)");
        var res = new List<LayoutSalvo>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var layout = JsonSerializer.Deserialize<LayoutImportacao>(r.GetString(2), Json) ?? new LayoutImportacao();
            res.Add(new LayoutSalvo(r.GetInt64(0), r.GetString(1), layout with { Nome = r.GetString(1) }));
        }
        return res;
    }

    public Task<long> SalvarLayoutAsync(long? id, LayoutImportacao layout, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            var json = JsonSerializer.Serialize(layout, Json);
            await using var cmd = id is null
                ? ds.CreateCommand("INSERT INTO contabil.layout_importacao (nome, configuracao) VALUES (@n, @c) RETURNING id")
                : ds.CreateCommand("UPDATE contabil.layout_importacao SET nome = @n, configuracao = @c, alterado_em = now() WHERE id = @id RETURNING id");
            cmd.Com("n", layout.Nome).Com("c", NpgsqlDbType.Jsonb, json);
            if (id is not null) cmd.Com("id", id.Value);
            return (long)(await cmd.ExecuteScalarAsync(ct)
                          ?? throw new Domain.Comum.ViolacaoRegraException("Layout não encontrado."));
        });

    public async Task ExcluirLayoutAsync(long id, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("DELETE FROM contabil.layout_importacao WHERE id = @id").Com("id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlySet<string>> ChavesImportadasAsync(long empresaId, IReadOnlyCollection<string> chaves, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT i.chave FROM contabil.lancamento_importado i
              JOIN contabil.lancamento l ON l.id = i.lancamento_id
             WHERE i.empresa_id = @e AND i.chave = ANY(@c) AND l.status = 'EFETIVADO'
            """).Com("e", empresaId).Com("c", chaves.ToArray());
        var res = new HashSet<string>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) res.Add(r.GetString(0));
        return res;
    }
}
