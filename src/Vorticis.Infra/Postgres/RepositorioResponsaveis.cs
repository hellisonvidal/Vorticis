using Npgsql;
using NpgsqlTypes;
using Vorticis.Application.Cadastros;

namespace Vorticis.Infra.Postgres;

public sealed class RepositorioResponsaveis(NpgsqlDataSource ds) : IRepositorioResponsaveis
{
    public async Task<IReadOnlyList<Contador>> ListarContadoresAsync(CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("SELECT id, nome, cpf, crc, uf_crc, email, telefone, escritorio_id, assina_ecd_ecf FROM core.contador ORDER BY nome");
        var res = new List<Contador>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            res.Add(new Contador(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), T(r, 5), T(r, 6), r.IsDBNull(7) ? null : r.GetInt64(7),
                                     r.GetBoolean(8)));
        return res;
    }

    public async Task<IReadOnlyList<EscritorioContabil>> ListarEscritoriosAsync(CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("SELECT id, cnpj, razao_social, crc, uf_crc, email, telefone FROM core.escritorio_contabil ORDER BY razao_social");
        var res = new List<EscritorioContabil>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            res.Add(new EscritorioContabil(r.GetInt64(0), r.GetString(1), r.GetString(2), T(r, 3), T(r, 4), T(r, 5), T(r, 6)));
        return res;
    }

    public Task<long> SalvarContadorAsync(Contador c, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            await using var cmd = ds.CreateCommand("""
                INSERT INTO core.contador (nome, cpf, crc, uf_crc, email, telefone, escritorio_id, assina_ecd_ecf)
                VALUES (@n, @cpf, @crc, @uf, @em, @tel, @esc, @ass)
                ON CONFLICT (cpf) DO UPDATE SET nome = EXCLUDED.nome, crc = EXCLUDED.crc, uf_crc = EXCLUDED.uf_crc,
                       email = COALESCE(EXCLUDED.email, core.contador.email), telefone = COALESCE(EXCLUDED.telefone, core.contador.telefone),
                       escritorio_id = COALESCE(EXCLUDED.escritorio_id, core.contador.escritorio_id), assina_ecd_ecf = EXCLUDED.assina_ecd_ecf
                RETURNING id
                """).Com("n", c.Nome).Com("cpf", c.Cpf).Com("crc", c.Crc).Com("uf", c.UfCrc).ComTexto("em", c.Email).ComTexto("tel", c.Telefone)
                    .Com("esc", NpgsqlDbType.Bigint, c.EscritorioId).Com("ass", c.AssinaEcdEcf);
            return (long)(await cmd.ExecuteScalarAsync(ct))!;
        });

    public Task<long> SalvarEscritorioAsync(EscritorioContabil e, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            await using var cmd = ds.CreateCommand("""
                INSERT INTO core.escritorio_contabil (cnpj, razao_social, crc, uf_crc, email, telefone) VALUES (@cnpj, @r, @crc, @uf, @em, @tel)
                ON CONFLICT (cnpj) DO UPDATE SET razao_social = EXCLUDED.razao_social,
                       crc = COALESCE(EXCLUDED.crc, core.escritorio_contabil.crc), uf_crc = COALESCE(EXCLUDED.uf_crc, core.escritorio_contabil.uf_crc),
                       email = COALESCE(EXCLUDED.email, core.escritorio_contabil.email), telefone = COALESCE(EXCLUDED.telefone, core.escritorio_contabil.telefone)
                RETURNING id
                """).Com("cnpj", e.Cnpj).Com("r", e.RazaoSocial).ComTexto("crc", e.Crc).Com("uf", NpgsqlDbType.Char, e.UfCrc)
                    .ComTexto("em", e.Email).ComTexto("tel", e.Telefone);
            return (long)(await cmd.ExecuteScalarAsync(ct))!;
        });

    public async Task<IReadOnlyList<VinculoContabil>> ListarVinculosAsync(long empresaId, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT v.id, v.contador_id, v.escritorio_id, COALESCE(c.nome, e.razao_social), COALESCE(c.cpf, e.cnpj),
                   CASE WHEN c.id IS NOT NULL
                        THEN CASE WHEN position(c.uf_crc IN c.crc) > 0 THEN c.crc ELSE c.uf_crc || '-' || c.crc END
                        ELSE CASE WHEN e.crc IS NULL OR position(COALESCE(e.uf_crc, '##') IN e.crc) > 0 THEN e.crc ELSE e.uf_crc || '-' || e.crc END
                   END,
                   v.inicio, v.fim, v.responsavel_tecnico, COALESCE(c.assina_ecd_ecf, FALSE)
              FROM core.empresa_contabilidade v
              LEFT JOIN core.contador c ON c.id = v.contador_id
              LEFT JOIN core.escritorio_contabil e ON e.id = v.escritorio_id
             WHERE v.empresa_id = @e
             ORDER BY v.fim IS NOT NULL, v.responsavel_tecnico DESC, v.inicio DESC
            """).Com("e", empresaId);
        var res = new List<VinculoContabil>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            res.Add(new VinculoContabil(r.GetInt64(0), r.IsDBNull(1) ? null : r.GetInt64(1), r.IsDBNull(2) ? null : r.GetInt64(2), r.GetString(3),
                r.GetString(4), T(r, 5), r.GetFieldValue<DateOnly>(6), r.IsDBNull(7) ? null : r.GetFieldValue<DateOnly>(7), r.GetBoolean(8),
                r.GetBoolean(9)));
        return res;
    }

    public Task<long> IncluirVinculoAsync(long empresaId, long? contadorId, long? escritorioId, DateOnly inicio, bool responsavel, long usuarioId,
                                          CancellationToken ct)
        => ComUsuario(usuarioId, async (con, tx) =>
        {
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO core.empresa_contabilidade (empresa_id, contador_id, escritorio_id, inicio, responsavel_tecnico)
                VALUES (@e, @c, @esc, @i, @r) RETURNING id
                """, con, tx).Com("e", empresaId).Com("c", NpgsqlDbType.Bigint, contadorId).Com("esc", NpgsqlDbType.Bigint, escritorioId)
                             .Com("i", inicio).Com("r", responsavel);
            return (long)(await cmd.ExecuteScalarAsync(ct))!;
        }, ct);

    public Task EncerrarVinculoAsync(long empresaId, long vinculoId, DateOnly fim, long usuarioId, CancellationToken ct)
        => Executar(usuarioId, "UPDATE core.empresa_contabilidade SET fim = @f WHERE id = @id AND empresa_id = @e",
                    c => c.Com("f", fim).Com("id", vinculoId).Com("e", empresaId), ct);

    public Task DefinirResponsavelAsync(long empresaId, long vinculoId, long usuarioId, CancellationToken ct)
        => Executar(usuarioId, """
            UPDATE core.empresa_contabilidade SET responsavel_tecnico = FALSE WHERE empresa_id = @e AND responsavel_tecnico AND id <> @id;
            UPDATE core.empresa_contabilidade SET responsavel_tecnico = TRUE WHERE id = @id AND empresa_id = @e;
            """, c => c.Com("id", vinculoId).Com("e", empresaId), ct);

    public Task DefinirAssinaEcdEcfAsync(long contadorId, bool assina, long usuarioId, CancellationToken ct)
        => Executar(usuarioId, "UPDATE core.contador SET assina_ecd_ecf = @a WHERE id = @id",
                    c => c.Com("id", contadorId).Com("a", assina), ct);

    public Task ExcluirVinculoAsync(long empresaId, long vinculoId, long usuarioId, CancellationToken ct)
        => Executar(usuarioId, "DELETE FROM core.empresa_contabilidade WHERE id = @id AND empresa_id = @e",
                    c => c.Com("id", vinculoId).Com("e", empresaId), ct);

    public async Task<IReadOnlyList<Socio>> ListarSociosAsync(long empresaId, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT id, nome, tipo_pessoa, cpf_cnpj, documento_mascarado, qualificacao, qualificacao_codigo, participacao, data_entrada, data_saida,
                   representante_legal, email, telefone, origem
              FROM core.socio WHERE empresa_id = @e ORDER BY data_saida IS NOT NULL, representante_legal DESC, nome
            """).Com("e", empresaId);
        var res = new List<Socio>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            res.Add(new Socio(r.GetInt64(0), r.GetString(1), r.GetString(2)[0], T(r, 3), T(r, 4), r.GetString(5), r.IsDBNull(6) ? null : r.GetInt16(6),
                r.IsDBNull(7) ? null : r.GetDecimal(7), r.IsDBNull(8) ? null : r.GetFieldValue<DateOnly>(8), r.IsDBNull(9) ? null : r.GetFieldValue<DateOnly>(9),
                r.GetBoolean(10), T(r, 11), T(r, 12), r.GetString(13)));
        return res;
    }

    public Task<long> SalvarSocioAsync(long empresaId, Socio s, long usuarioId, CancellationToken ct)
        => ComUsuario(usuarioId, async (con, tx) =>
        {
            await using var cmd = new NpgsqlCommand(s.Id == 0
                ? """
                  INSERT INTO core.socio (empresa_id, nome, tipo_pessoa, cpf_cnpj, documento_mascarado, qualificacao, qualificacao_codigo, participacao,
                                          data_entrada, data_saida, representante_legal, email, telefone, origem)
                  VALUES (@e, @n, @t, @doc, @mask, @q, @qc, @p, @de, @ds, @rep, @em, @tel, @o) RETURNING id
                  """
                : """
                  UPDATE core.socio SET nome = @n, tipo_pessoa = @t, cpf_cnpj = @doc, documento_mascarado = @mask, qualificacao = @q,
                         qualificacao_codigo = @qc, participacao = @p, data_entrada = @de, data_saida = @ds, representante_legal = @rep,
                         email = @em, telefone = @tel
                   WHERE id = @id AND empresa_id = @e RETURNING id
                  """, con, tx)
                .Com("id", s.Id).Com("e", empresaId).Com("n", s.Nome).Com("t", s.TipoPessoa.ToString()).ComTexto("doc", s.CpfCnpj)
                .ComTexto("mask", s.DocumentoMascarado).Com("q", s.Qualificacao).Com("qc", NpgsqlDbType.Smallint, (short?)s.QualificacaoCodigo)
                .Com("p", NpgsqlDbType.Numeric, s.Participacao).Com("de", NpgsqlDbType.Date, s.DataEntrada).Com("ds", NpgsqlDbType.Date, s.DataSaida)
                .Com("rep", s.RepresentanteLegal).ComTexto("em", s.Email).ComTexto("tel", s.Telefone).Com("o", s.Origem);
            return await cmd.ExecuteScalarAsync(ct) is long id ? id : throw new Domain.Comum.ViolacaoRegraException("Sócio não encontrado.");
        }, ct);

    public Task ExcluirSocioAsync(long empresaId, long socioId, long usuarioId, CancellationToken ct)
        => Executar(usuarioId, "DELETE FROM core.socio WHERE id = @id AND empresa_id = @e", c => c.Com("id", socioId).Com("e", empresaId), ct);

    private static string? T(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);

    private Task<T> ComUsuario<T>(long usuarioId, Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> acao, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            await using var con = await ds.OpenConnectionAsync(ct);
            await using var tx = await con.BeginTransactionAsync(ct);
            await Mapeamentos.DefinirUsuarioAsync(con, tx, usuarioId, ct);
            var res = await acao(con, tx);
            await tx.CommitAsync(ct);
            return res;
        });

    private Task Executar(long usuarioId, string sql, Action<NpgsqlCommand> parametros, CancellationToken ct)
        => ComUsuario(usuarioId, async (con, tx) =>
        {
            await using var cmd = new NpgsqlCommand(sql, con, tx);
            parametros(cmd);
            return await cmd.ExecuteNonQueryAsync(ct);
        }, ct);
}
