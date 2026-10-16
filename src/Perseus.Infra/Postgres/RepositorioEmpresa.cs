using Npgsql;
using Perseus.Application.Cadastros;

namespace Perseus.Infra.Postgres;

public sealed class RepositorioEmpresa(NpgsqlDataSource ds) : IRepositorioEmpresa
{
    public async Task<bool> CnpjExisteAsync(string cnpj, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("SELECT EXISTS (SELECT 1 FROM core.empresa WHERE cnpj = @c)").Com("c", cnpj);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public Task<long> CriarAsync(NovaEmpresa e, IReadOnlyList<ContaModelo> plano, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            await using var con = await ds.OpenConnectionAsync(ct);
            await using var tx = await con.BeginTransactionAsync(ct);
            await Mapeamentos.DefinirUsuarioAsync(con, tx, null, ct);

            long id;
            await using (var cmd = new NpgsqlCommand("""
                INSERT INTO core.empresa (cnpj, razao_social, nome_fantasia, natureza_juridica, cnae_principal, uf,
                                          cod_municipio_ibge, data_inicio_ativ, cep, logradouro, numero, complemento, bairro,
                                          municipio, email, telefone, situacao_cadastral, fonte_dados, dados_consultados_em)
                VALUES (@cnpj, @rs, @nf, @nj, @cnae, @uf, @mun, @ini, @cep, @log, @num, @compl, @bairro,
                        @municipio, @email, @tel, @sit, @fonte, @consultado) RETURNING id
                """, con, tx)
                .Com("cnpj", e.Cnpj).Com("rs", e.RazaoSocial).Com("nf", e.NomeFantasia).Com("nj", e.NaturezaJuridica)
                .Com("cnae", e.CnaePrincipal).Com("uf", e.Uf).Com("mun", e.CodMunicipioIbge).Com("ini", e.DataInicioAtividades)
                .ComTexto("cep", e.Complemento?.Cep).ComTexto("log", e.Complemento?.Logradouro).ComTexto("num", e.Complemento?.Numero)
                .ComTexto("compl", e.Complemento?.Complemento).ComTexto("bairro", e.Complemento?.Bairro)
                .ComTexto("municipio", e.Complemento?.Municipio).ComTexto("email", e.Complemento?.Email)
                .ComTexto("tel", e.Complemento?.Telefone).ComTexto("sit", e.Complemento?.SituacaoCadastral)
                .ComTexto("fonte", e.Complemento?.FonteDados)
                .Com("consultado", NpgsqlTypes.NpgsqlDbType.TimestampTz, e.Complemento?.DadosConsultadosEm))
                id = (long)(await cmd.ExecuteScalarAsync(ct))!;

            await using (var cmd = new NpgsqlCommand("""
                INSERT INTO core.estabelecimento (empresa_id, cnpj, matriz, uf, cod_municipio_ibge) VALUES (@id, @cnpj, TRUE, @uf, @mun);
                INSERT INTO core.empresa_regime (empresa_id, regime, vigencia_inicio)
                VALUES (@id, @reg::core.regime_tributario, GREATEST(make_date(@ano, 1, 1), @ini));
                """, con, tx)
                .Com("id", id).Com("cnpj", e.Cnpj).Com("uf", e.Uf).Com("mun", e.CodMunicipioIbge)
                .Com("reg", e.Regime.Sql()).Com("ano", e.AnoPrimeiroExercicio).Com("ini", e.DataInicioAtividades))
                await cmd.ExecuteNonQueryAsync(ct);

            var inicioExercicio = await CriarExercicioAsync(con, tx, id, e.AnoPrimeiroExercicio, ct);
            await InserirPlanoAsync(con, tx, id, plano, inicioExercicio, ct);

            await tx.CommitAsync(ct);
            return id;
        });

    public Task CriarExercicioAsync(long empresaId, int ano, CancellationToken ct)
        => Mapeamentos.TraduzirErrosAsync(async () =>
        {
            await using var con = await ds.OpenConnectionAsync(ct);
            await using var tx = await con.BeginTransactionAsync(ct);
            await CriarExercicioAsync(con, tx, empresaId, ano, ct);
            await tx.CommitAsync(ct);
            return 0;
        });

    /// <summary>Exercício = ano civil, começando no início das atividades se este cair no próprio ano.</summary>
    private static async Task<DateOnly> CriarExercicioAsync(NpgsqlConnection con, NpgsqlTransaction tx, long empresaId, int ano, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            WITH ex AS (
                INSERT INTO contabil.exercicio (empresa_id, ano, data_inicio, data_fim)
                SELECT @e, @ano, GREATEST(make_date(@ano, 1, 1), e.data_inicio_ativ), make_date(@ano, 12, 31)
                  FROM core.empresa e WHERE e.id = @e
                RETURNING id, data_inicio),
            per AS (
                INSERT INTO contabil.periodo (exercicio_id, competencia)
                SELECT ex.id, gs::date FROM ex,
                       generate_series(date_trunc('month', ex.data_inicio), make_date(@ano, 12, 1), INTERVAL '1 month') gs)
            SELECT data_inicio FROM ex
            """, con, tx).Com("e", empresaId).Com("ano", (short)ano);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        return r.GetFieldValue<DateOnly>(0);
    }

    private static async Task InserirPlanoAsync(NpgsqlConnection con, NpgsqlTransaction tx, long empresaId,
                                                IReadOnlyList<ContaModelo> plano, DateOnly dataInclusao, CancellationToken ct)
    {
        if (plano.Count == 0) return;
        var codigos = plano.Select(c => c.Codigo).ToHashSet();
        var ids = new Dictionary<string, long>();

        // Ordem por nível garante que o pai já exista ao inserir o filho.
        foreach (var c in plano.OrderBy(c => c.Codigo.Count(ch => ch == '.')).ThenBy(c => c.Codigo, StringComparer.Ordinal))
        {
            var ultimoPonto = c.Codigo.LastIndexOf('.');
            var codigoPai = ultimoPonto < 0 ? null : c.Codigo[..ultimoPonto];
            if (codigoPai is not null && !ids.ContainsKey(codigoPai))
                throw new InvalidOperationException($"Plano modelo inválido: conta {c.Codigo} sem a conta pai {codigoPai}.");
            var analitica = !codigos.Any(o => o.StartsWith(c.Codigo + ".", StringComparison.Ordinal));

            await using var cmd = new NpgsqlCommand("""
                INSERT INTO contabil.plano_contas (empresa_id, codigo, descricao, tipo, nivel, conta_pai_id, cod_nat, natureza_saldo, data_inclusao)
                VALUES (@e, @c, @d, @t::contabil.tipo_conta, @n, @pai, @nat, @sal::contabil.indicador_dc, @inc) RETURNING id
                """, con, tx)
                .Com("e", empresaId).Com("c", c.Codigo).Com("d", c.Descricao).Com("t", analitica ? "A" : "S")
                .Com("n", (short)(c.Codigo.Count(ch => ch == '.') + 1)).Com("pai", codigoPai is null ? null : ids[codigoPai])
                .Com("nat", c.CodNat).Com("sal", c.NaturezaSaldo.ToString()).Com("inc", dataInclusao);
            ids[c.Codigo] = (long)(await cmd.ExecuteScalarAsync(ct))!;
        }
    }

    public async Task<IReadOnlyList<EmpresaResumo>> ListarAsync(CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("SELECT id, cnpj, razao_social FROM core.empresa WHERE ativa ORDER BY razao_social");
        var res = new List<EmpresaResumo>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) res.Add(new EmpresaResumo(r.GetInt64(0), r.GetString(1), r.GetString(2)));
        return res;
    }
}
