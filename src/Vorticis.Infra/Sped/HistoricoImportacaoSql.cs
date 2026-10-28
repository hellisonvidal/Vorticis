using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Vorticis.Application.Sped.Importacao;
using Vorticis.Domain.Sped.Importacao;

namespace Vorticis.Infra.Sped;

/// <summary>
/// Histórico das importações SPED (core.importacao_sped e core.importacao_sped_item), usado pelos importadores do Contábil e do Fiscal
/// dentro da transação da própria importação: o que foi criado fica registrado para a opção "Desfazer importação".
/// </summary>
public static class HistoricoImportacaoSql
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static NpgsqlCommand Cmd(NpgsqlConnection con, NpgsqlTransaction? tx, string sql, params (string, object?)[] p)
    {
        var c = new NpgsqlCommand(sql, con, tx);
        foreach (var (n, v) in p) c.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return c;
    }

    public static async Task<long> AbrirAsync(NpgsqlConnection con, NpgsqlTransaction tx, long empresaId, string tipo, MetaImportacao meta, long usuarioId, CancellationToken ct)
    {
        await using var c = Cmd(con, tx, """
            INSERT INTO core.importacao_sped (empresa_id, tipo, modo, arquivo, sha256, periodo_inicio, periodo_fim, importado_por)
            VALUES (@e, @t, @m, @a, @h, @i, @f, @u) RETURNING id
            """, ("e", empresaId), ("t", tipo), ("m", meta.Modo), ("a", meta.Arquivo.Length > 260 ? meta.Arquivo[^260..] : meta.Arquivo), ("h", meta.Sha256),
            ("i", meta.Cabecalho.Inicio == default ? null : meta.Cabecalho.Inicio), ("f", meta.Cabecalho.Fim == default ? null : meta.Cabecalho.Fim), ("u", usuarioId));
        return (long)(await c.ExecuteScalarAsync(ct))!;
    }

    public static async Task ItensAsync(NpgsqlConnection con, NpgsqlTransaction tx, long importacaoId, string tabela, IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        await using var c = Cmd(con, tx, """
            INSERT INTO core.importacao_sped_item (importacao_id, tabela, registro_id)
            SELECT @i, @t, x FROM unnest(@ids) AS x ON CONFLICT DO NOTHING
            """, ("i", importacaoId), ("t", tabela));
        c.Parameters.AddWithValue("ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint, ids.ToArray());
        await c.ExecuteNonQueryAsync(ct);
    }

    public static Task ItemAsync(NpgsqlConnection con, NpgsqlTransaction tx, long importacaoId, string tabela, long id, CancellationToken ct)
        => ItensAsync(con, tx, importacaoId, tabela, [id], ct);

    public static async Task<List<long>> IdsAsync(NpgsqlConnection con, NpgsqlTransaction tx, long importacaoId, string tabela, CancellationToken ct)
    {
        await using var c = Cmd(con, tx, "SELECT registro_id FROM core.importacao_sped_item WHERE importacao_id = @i AND tabela = @t ORDER BY registro_id", ("i", importacaoId), ("t", tabela));
        var res = new List<long>();
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) res.Add(r.GetInt64(0));
        return res;
    }

    public static async Task FecharAsync(NpgsqlConnection con, NpgsqlTransaction tx, long importacaoId, int importados, RelatorioImportacao relatorio, object? desfazer, CancellationToken ct)
    {
        var rel = new RelatorioGravado(relatorio.Linhas, relatorio.Ocorrencias.Take(1000).ToList(), relatorio.Modo, relatorio.Tipo);
        await using var c = Cmd(con, tx, "UPDATE core.importacao_sped SET importados = @n, relatorio = @r::jsonb, desfazer = @d::jsonb WHERE id = @i",
                                ("n", importados), ("r", JsonSerializer.Serialize(rel, Json)), ("d", desfazer is null ? null : JsonSerializer.Serialize(desfazer, Json)), ("i", importacaoId));
        await c.ExecuteNonQueryAsync(ct);
    }

    public sealed record RelatorioGravado(IReadOnlyList<LinhaRelatorio> Linhas, IReadOnlyList<OcorrenciaImportacao> Ocorrencias, string Modo, string Tipo);

    public sealed record Registro(long Id, long EmpresaId, string Tipo, string Modo, string Arquivo, bool Desfeita, string? DesfazerJson, string RelatorioJson,
                                  DateOnly? Inicio, DateOnly? Fim);

    public static async Task<Registro?> CarregarAsync(NpgsqlConnection con, NpgsqlTransaction? tx, long empresaId, long importacaoId, CancellationToken ct, bool travar = false)
    {
        await using var c = Cmd(con, tx, $"""
            SELECT id, empresa_id, tipo, modo, arquivo, situacao = 'DESFEITA', desfazer::text, relatorio::text, periodo_inicio, periodo_fim
              FROM core.importacao_sped WHERE id = @i AND empresa_id = @e {(travar ? "FOR UPDATE" : "")}
            """, ("i", importacaoId), ("e", empresaId));
        await using var r = await c.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new Registro(r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetBoolean(5), r.IsDBNull(6) ? null : r.GetString(6), r.GetString(7),
                            r.IsDBNull(8) ? null : r.GetFieldValue<DateOnly>(8), r.IsDBNull(9) ? null : r.GetFieldValue<DateOnly>(9));
    }

    public static async Task MarcarDesfeitaAsync(NpgsqlConnection con, NpgsqlTransaction tx, long importacaoId, long usuarioId, CancellationToken ct)
    {
        await using var c = Cmd(con, tx, "UPDATE core.importacao_sped SET situacao = 'DESFEITA', desfeito_em = now(), desfeito_por = @u WHERE id = @i", ("u", usuarioId), ("i", importacaoId));
        await c.ExecuteNonQueryAsync(ct);
    }

    public static RelatorioImportacao Relatorio(Registro reg, bool desfeita)
    {
        var rel = JsonSerializer.Deserialize<RelatorioGravado>(reg.RelatorioJson, Json) ?? new RelatorioGravado([], [], reg.Modo, reg.Tipo);
        return new RelatorioImportacao(reg.Id, reg.Arquivo, rel.Tipo, rel.Modo, rel.Linhas, rel.Ocorrencias, desfeita);
    }

    /// <summary>Participante da empresa pelo CPF/CNPJ (ou pelo código); cria se não existir. Devolve (id, código no sistema, criado).</summary>
    public static async Task<(long Id, string Codigo, bool Criado)> GarantirParticipanteAsync(NpgsqlConnection con, NpgsqlTransaction tx, long empresaId, string nome,
        string? documento, string? uf, string? codMunicipio, string? ie, bool cliente, bool fornecedor, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(documento))
        {
            await using var q = Cmd(con, tx, "SELECT id, codigo FROM core.participante WHERE empresa_id = @e AND cpf_cnpj = @d ORDER BY id LIMIT 1", ("e", empresaId), ("d", documento));
            await using var r = await q.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct)) return (r.GetInt64(0), r.GetString(1), false);
        }
        else
        {
            await using var q = Cmd(con, tx, "SELECT id, codigo FROM core.participante WHERE empresa_id = @e AND cpf_cnpj IS NULL AND upper(nome) = upper(@n) ORDER BY id LIMIT 1", ("e", empresaId), ("n", nome.Trim()));
            await using var r = await q.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct)) return (r.GetInt64(0), r.GetString(1), false);
        }
        string codigo;
        await using (var q = Cmd(con, tx, "SELECT core.fn_proximo_participante(@e)::text", ("e", empresaId)))
            codigo = (string)(await q.ExecuteScalarAsync(ct))!;
        await using var ins = Cmd(con, tx, """
            INSERT INTO core.participante (empresa_id, codigo, nome, cpf_cnpj, uf, cod_municipio_ibge, inscricao_estadual, cliente, fornecedor)
            VALUES (@e, @c, @n, @d, @uf, @m, @ie, @cli, @for) RETURNING id
            """, ("e", empresaId), ("c", codigo), ("n", nome.Length > 150 ? nome[..150] : (nome.Length == 0 ? "Participante sem nome" : nome)),
            ("d", string.IsNullOrEmpty(documento) ? null : documento),
            ("uf", uf is { Length: 2 } ? uf : null), ("m", codMunicipio is { Length: 7 } ? codMunicipio : null),
            ("ie", string.IsNullOrWhiteSpace(ie) ? null : (ie.Length > 20 ? ie[..20] : ie)), ("cli", cliente), ("for", fornecedor));
        return ((long)(await ins.ExecuteScalarAsync(ct))!, codigo, true);
    }
}

/// <summary>Consultas do histórico de importações (listagem e relatório gravado).</summary>
public sealed class RepositorioHistoricoSped(NpgsqlDataSource ds) : IRepositorioHistoricoSped
{
    public async Task<IReadOnlyList<HistoricoImportacao>> ListarAsync(long empresaId, IReadOnlyList<string> tipos, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT i.id, i.tipo, i.arquivo, i.periodo_inicio, i.periodo_fim, i.modo, i.importado_em, COALESCE(u.nome, u.login, ''), i.situacao = 'DESFEITA', i.importados
              FROM core.importacao_sped i LEFT JOIN core.usuario u ON u.id = i.importado_por
             WHERE i.empresa_id = @e AND i.tipo = ANY(@t) ORDER BY i.importado_em DESC, i.id DESC
            """);
        cmd.Parameters.AddWithValue("e", empresaId);
        cmd.Parameters.AddWithValue("t", tipos.ToArray());
        var res = new List<HistoricoImportacao>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            res.Add(new HistoricoImportacao(r.GetInt64(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetFieldValue<DateOnly>(3), r.IsDBNull(4) ? null : r.GetFieldValue<DateOnly>(4),
                                            r.GetString(5), r.GetDateTime(6), r.GetString(7), r.GetBoolean(8), r.GetInt32(9)));
        return res;
    }

    public async Task<long?> ImportacaoDoArquivoAsync(long empresaId, string tipo, string sha256, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("SELECT id FROM core.importacao_sped WHERE empresa_id = @e AND tipo = @t AND sha256 = @h AND situacao = 'IMPORTADA' ORDER BY id DESC LIMIT 1");
        cmd.Parameters.AddWithValue("e", empresaId); cmd.Parameters.AddWithValue("t", tipo); cmd.Parameters.AddWithValue("h", sha256);
        return await cmd.ExecuteScalarAsync(ct) as long?;
    }

    public async Task<RelatorioImportacao?> ObterRelatorioAsync(long empresaId, long importacaoId, CancellationToken ct)
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        return await HistoricoImportacaoSql.CarregarAsync(con, null, empresaId, importacaoId, ct) is { } r ? HistoricoImportacaoSql.Relatorio(r, r.Desfeita) : null;
    }
}
