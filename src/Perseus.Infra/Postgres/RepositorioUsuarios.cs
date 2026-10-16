using Npgsql;
using NpgsqlTypes;
using Perseus.Application.Seguranca;
using Perseus.Domain.Seguranca;

namespace Perseus.Infra.Postgres;

public sealed class RepositorioUsuarios(NpgsqlDataSource ds) : IRepositorioUsuarios
{
    private const string ColunasCredenciais =
        "id, login, nome, senha_hash, ativo, administrador, deve_trocar_senha, tentativas_falhas, bloqueado_ate";

    public async Task<bool> ExisteAlgumAsync(CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("SELECT EXISTS (SELECT 1 FROM core.usuario)");
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public Task<CredenciaisUsuario?> ObterPorLoginAsync(string login, CancellationToken ct)
        => ObterCredenciais($"SELECT {ColunasCredenciais} FROM core.usuario WHERE lower(login) = lower(@v)", login, ct);

    public Task<CredenciaisUsuario?> ObterPorIdAsync(long id, CancellationToken ct)
        => ObterCredenciais($"SELECT {ColunasCredenciais} FROM core.usuario WHERE id = @v", id, ct);

    private async Task<CredenciaisUsuario?> ObterCredenciais(string sql, object valor, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand(sql).Com("v", valor);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new CredenciaisUsuario(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetBoolean(4), r.GetBoolean(5),
                                      r.GetBoolean(6), r.GetInt16(7), r.IsDBNull(8) ? null : r.GetFieldValue<DateTimeOffset>(8));
    }

    public async Task<bool> LoginExisteAsync(string login, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("SELECT EXISTS (SELECT 1 FROM core.usuario WHERE lower(login) = lower(@l))").Com("l", login);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public Task<long?> CriarAsync(string login, string nome, string senhaHash, bool administrador, bool deveTrocarSenha,
                                  bool somenteSeNaoHouverUsuarios, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            await using var con = await ds.OpenConnectionAsync(ct);
            await using var tx = await con.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO core.usuario (login, nome, senha_hash, administrador, deve_trocar_senha)
                SELECT @l, @n, @h, @a, @t
                 WHERE NOT @so OR NOT EXISTS (SELECT 1 FROM core.usuario)
                RETURNING id
                """, con, tx)
                .Com("l", login).Com("n", nome).Com("h", senhaHash).Com("a", administrador).Com("t", deveTrocarSenha)
                .Com("so", somenteSeNaoHouverUsuarios);
            var id = (long?)await cmd.ExecuteScalarAsync(ct);
            await tx.CommitAsync(ct);
            return id;
        });

    public async Task RegistrarFalhaAsync(long? usuarioId, string login, int tentativas, DateTimeOffset? bloquearAte, string motivo,
                                          string? estacao, CancellationToken ct)
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        if (usuarioId is not null)
            await using (var cmd = new NpgsqlCommand("""
                UPDATE core.usuario SET tentativas_falhas = @t, bloqueado_ate = COALESCE(@b, bloqueado_ate) WHERE id = @id
                """, con, tx).Com("t", (short)tentativas).Com("b", NpgsqlDbType.TimestampTz, bloquearAte).Com("id", usuarioId))
                await cmd.ExecuteNonQueryAsync(ct);
        await RegistrarAcesso(con, tx, usuarioId, login, false, motivo, estacao, ct);
        await tx.CommitAsync(ct);
    }

    public async Task RegistrarSucessoAsync(long usuarioId, string login, string? novoHash, string? estacao, CancellationToken ct)
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        await using (var cmd = new NpgsqlCommand("""
            UPDATE core.usuario
               SET tentativas_falhas = 0, bloqueado_ate = NULL, ultimo_acesso = now(),
                   senha_hash = COALESCE(@h, senha_hash)
             WHERE id = @id
            """, con, tx).Com("h", NpgsqlDbType.Text, novoHash).Com("id", usuarioId))
            await cmd.ExecuteNonQueryAsync(ct);
        await RegistrarAcesso(con, tx, usuarioId, login, true, null, estacao, ct);
        await tx.CommitAsync(ct);
    }

    private static async Task RegistrarAcesso(NpgsqlConnection con, NpgsqlTransaction tx, long? usuarioId, string login, bool sucesso,
                                              string? motivo, string? estacao, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO core.registro_acesso (login, usuario_id, sucesso, motivo, estacao) VALUES (@l, @u, @s, @m, @e)
            """, con, tx)
            .Com("l", login.Length > 60 ? login[..60] : login).Com("u", NpgsqlDbType.Bigint, usuarioId).Com("s", sucesso)
            .ComTexto("m", motivo).ComTexto("e", estacao);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task AlterarSenhaAsync(long usuarioId, string senhaHash, bool deveTrocarSenha, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            UPDATE core.usuario SET senha_hash = @h, deve_trocar_senha = @t, tentativas_falhas = 0, bloqueado_ate = NULL WHERE id = @id
            """).Com("h", senhaHash).Com("t", deveTrocarSenha).Com("id", usuarioId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public Task AlterarAsync(long usuarioId, string nome, bool administrador, bool ativo, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            await using var cmd = ds.CreateCommand("UPDATE core.usuario SET nome = @n, administrador = @a, ativo = @at WHERE id = @id")
                                    .Com("n", nome).Com("a", administrador).Com("at", ativo).Com("id", usuarioId);
            return await cmd.ExecuteNonQueryAsync(ct);
        });

    public async Task<IReadOnlyList<UsuarioResumo>> ListarAsync(CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("SELECT id, login, nome, administrador, ativo, ultimo_acesso FROM core.usuario ORDER BY nome");
        var res = new List<UsuarioResumo>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            res.Add(new UsuarioResumo(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetBoolean(3), r.GetBoolean(4),
                                      r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5)));
        return res;
    }

    public async Task DefinirAcessoAsync(long usuarioId, long empresaId, Perfil? perfil, CancellationToken ct)
    {
        await using var cmd = perfil is null
            ? ds.CreateCommand("DELETE FROM core.usuario_empresa WHERE usuario_id = @u AND empresa_id = @e")
            : ds.CreateCommand("""
                INSERT INTO core.usuario_empresa (usuario_id, empresa_id, perfil) VALUES (@u, @e, @p)
                ON CONFLICT (usuario_id, empresa_id) DO UPDATE SET perfil = EXCLUDED.perfil
                """).Com("p", perfil.Value.Sql());
        cmd.Com("u", usuarioId).Com("e", empresaId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<AcessoEmpresa>> ListarAcessosAsync(long usuarioId, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT e.id, e.razao_social, ue.perfil FROM core.usuario_empresa ue JOIN core.empresa e ON e.id = ue.empresa_id
             WHERE ue.usuario_id = @u ORDER BY e.razao_social
            """).Com("u", usuarioId);
        var res = new List<AcessoEmpresa>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) res.Add(new AcessoEmpresa(r.GetInt64(0), r.GetString(1), Permissoes.PerfilDeSql(r.GetString(2))));
        return res;
    }

    public async Task<IReadOnlyList<EmpresaPermitida>> ListarEmpresasPermitidasAsync(long usuarioId, bool administrador, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT e.id, e.cnpj, e.razao_social, ue.perfil
              FROM core.empresa e
              LEFT JOIN core.usuario_empresa ue ON ue.empresa_id = e.id AND ue.usuario_id = @u
             WHERE e.ativa AND (@adm OR ue.perfil IS NOT NULL)
             ORDER BY e.razao_social
            """).Com("u", usuarioId).Com("adm", administrador);
        var res = new List<EmpresaPermitida>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            res.Add(new EmpresaPermitida(r.GetInt64(0), r.GetString(1), r.GetString(2),
                                         r.IsDBNull(3) ? null : Permissoes.PerfilDeSql(r.GetString(3))));
        return res;
    }
}
