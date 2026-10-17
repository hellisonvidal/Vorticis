using Npgsql;
using Perseus.Application.Cadastros;

namespace Perseus.Infra.Postgres;

public sealed class RepositorioParticipantes(NpgsqlDataSource ds) : IRepositorioParticipantes
{
    public async Task<IReadOnlyList<Participante>> ListarAsync(long empresaId, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT p.id, p.codigo, p.nome, p.nome_fantasia, p.cpf_cnpj, p.inscricao_estadual, p.uf, p.cod_municipio_ibge, p.municipio,
                   p.email, p.telefone, p.cliente, p.fornecedor, c.codigo, p.ativo,
                   EXISTS (SELECT 1 FROM contabil.partida x WHERE x.participante_id = p.id)
              FROM core.participante p LEFT JOIN contabil.plano_contas c ON c.id = p.conta_id
             WHERE p.empresa_id = @e
            """).Com("e", empresaId);
        var res = new List<Participante>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        string? T(int i) => r.IsDBNull(i) ? null : r.GetString(i).Trim();
        while (await r.ReadAsync(ct))
            res.Add(new Participante(r.GetInt64(0), r.GetString(1), r.GetString(2), T(3), T(4), T(5), T(6), T(7), T(8), T(9), T(10),
                                     r.GetBoolean(11), r.GetBoolean(12), T(13), r.GetBoolean(14), r.GetBoolean(15)));
        return res;
    }

    public Task<long> GravarAsync(long empresaId, Participante p, long? contaId, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            await using var cmd = p.Id == 0
                ? ds.CreateCommand("""
                    INSERT INTO core.participante (empresa_id, codigo, nome, nome_fantasia, cpf_cnpj, inscricao_estadual, uf, cod_municipio_ibge,
                                                   municipio, email, telefone, cliente, fornecedor, conta_id, ativo)
                    VALUES (@e, @c, @n, @nf, @doc, @ie, @uf, @ibge, @mun, @email, @tel, @cli, @for, @conta, @ativo) RETURNING id
                    """)
                : ds.CreateCommand("""
                    UPDATE core.participante SET codigo = @c, nome = @n, nome_fantasia = @nf, cpf_cnpj = @doc, inscricao_estadual = @ie, uf = @uf,
                           cod_municipio_ibge = @ibge, municipio = @mun, email = @email, telefone = @tel, cliente = @cli, fornecedor = @for,
                           conta_id = @conta, ativo = @ativo
                     WHERE id = @id AND empresa_id = @e RETURNING id
                    """);
            cmd.Com("e", empresaId).Com("c", p.Codigo).Com("n", p.Nome).ComTexto("nf", p.NomeFantasia).ComTexto("doc", p.CpfCnpj)
               .ComTexto("ie", p.InscricaoEstadual).ComTexto("uf", p.Uf).ComTexto("ibge", p.CodMunicipioIbge).ComTexto("mun", p.Municipio)
               .ComTexto("email", p.Email).ComTexto("tel", p.Telefone).Com("cli", p.Cliente).Com("for", p.Fornecedor)
               .Com("conta", NpgsqlTypes.NpgsqlDbType.Bigint, contaId).Com("ativo", p.Ativo);
            if (p.Id != 0) cmd.Com("id", p.Id);
            return (long)(await cmd.ExecuteScalarAsync(ct))!;
        });

    public Task ExcluirAsync(long id, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            await using var cmd = ds.CreateCommand("DELETE FROM core.participante WHERE id = @id").Com("id", id);
            return await cmd.ExecuteNonQueryAsync(ct);
        });
}
