using Npgsql;
using Vorticis.Application.Sistema;

namespace Vorticis.Infra.Postgres;

public sealed class RepositorioAuditoria(NpgsqlDataSource ds) : IRepositorioAuditoria
{
    public async Task<IReadOnlyList<RegistroAuditoria>> ConsultarAsync(FiltroAuditoria f, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT a.id, a.ocorrido_em, a.usuario_login, a.tabela, a.registro_id, a.operacao, a.empresa_id, e.razao_social,
                   a.dados_antes::text, a.dados_depois::text
              FROM core.auditoria a LEFT JOIN core.empresa e ON e.id = a.empresa_id
             WHERE a.ocorrido_em >= @de AND a.ocorrido_em < @ate
               AND (@u = '' OR a.usuario_login ILIKE '%' || @u || '%')
               AND (@t = '' OR a.tabela = @t)
               AND (@e::bigint IS NULL OR a.empresa_id = @e)
             ORDER BY a.id DESC
             LIMIT @lim
            """)
            .Com("de", NpgsqlTypes.NpgsqlDbType.Date, f.De).Com("ate", NpgsqlTypes.NpgsqlDbType.Date, f.Ate.AddDays(1))
            .Com("u", (f.Usuario ?? "").Trim()).Com("t", (f.Tabela ?? "").Trim())
            .Com("e", NpgsqlTypes.NpgsqlDbType.Bigint, f.EmpresaId).Com("lim", f.Limite);
        var res = new List<RegistroAuditoria>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            res.Add(new RegistroAuditoria(r.GetInt64(0), r.GetFieldValue<DateTimeOffset>(1), r.GetString(2), r.GetString(3),
                                          r.IsDBNull(4) ? null : r.GetInt64(4), r.GetString(5).Trim(), r.IsDBNull(6) ? null : r.GetInt64(6),
                                          r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? null : r.GetString(9)));
        return res;
    }

    public async Task<IReadOnlyList<string>> TabelasAsync(CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT DISTINCT event_object_schema || '.' || event_object_table FROM information_schema.triggers
             WHERE action_statement LIKE '%fn_auditoria()%' ORDER BY 1
            """);
        var res = new List<string>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) res.Add(r.GetString(0));
        return res;
    }

    public async Task<long?> VerificarIntegridadeAsync(CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("SELECT core.fn_verificar_auditoria()");
        cmd.CommandTimeout = 600;
        return await cmd.ExecuteScalarAsync(ct) is long id ? id : null;
    }
}
