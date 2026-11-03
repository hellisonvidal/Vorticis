using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Sped.Importacao;
using Vorticis.Folha.Esocial.Importacao;
using Vorticis.Infra.Sped;

namespace Vorticis.Folha.Dados;

public interface IRepositorioImportacaoEsocial
{
    Task<SituacaoFolha?> SituacaoAsync(long empresaId, CancellationToken ct);
    Task<RelatorioImportacao> ImportarAsync(long empresaId, PlanoEsocial plano, string arquivo, string sha256, long usuarioId, IProgress<ProgressoImportacao>? progresso, CancellationToken ct);
    Task<RelatorioImportacao> DesfazerAsync(long empresaId, long importacaoId, long usuarioId, CancellationToken ct);
}

/// <summary>
/// Grava a importação do eSocial numa única transação (funcionários, rubricas, histórico de holerites, férias, afastamentos...) e registra cada
/// linha criada em core.importacao_sped_item para a opção "Desfazer importação".
/// </summary>
public sealed class RepositorioImportacaoEsocial(NpgsqlDataSource ds, IRepositorioCadastrosFolha cadastros) : IRepositorioImportacaoEsocial
{
    public const string TipoHistorico = "ESOCIAL";
    private const string TFunc = "folha.funcionario", TRub = "folha.rubrica", TDep = "folha.departamento", TCargo = "folha.cargo", TAlt = "folha.alteracao_contratual",
        TAfast = "folha.afastamento", TFerias = "folha.ferias", TCalc = "folha.calculo", TComp = "folha.competencia", TParam = "folha.parametro_empresa";

    private sealed record ParametroAnterior(decimal Rat, decimal Fap, string Fpas, string CodTerceiros, string ClassTrib);
    private sealed record DadosDesfazer(ParametroAnterior? Parametros, List<string> Reabrir);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ------------------------------------------------------------------ situação atual

    public async Task<SituacaoFolha?> SituacaoAsync(long empresaId, CancellationToken ct)
    {
        var empresa = await cadastros.ObterEmpresaAsync(empresaId, ct);
        if (empresa is null) return null;
        await using var con = await ds.OpenConnectionAsync(ct);
        async Task<List<T>> Ler<T>(string sql, Func<NpgsqlDataReader, T> mapa)
        {
            await using var c = new NpgsqlCommand(sql, con).Com("e", empresaId);
            var res = new List<T>();
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) res.Add(mapa(r));
            return res;
        }
        var funcionarios = await Ler("SELECT id, cpf, matricula, data_admissao FROM folha.funcionario WHERE empresa_id = @e",
            r => new FuncionarioExistente(r.GetInt64(0), r.GetString(1).Trim(), r.GetString(2), r.GetFieldValue<DateOnly>(3)));
        var departamentos = (await Ler("SELECT codigo FROM folha.departamento WHERE empresa_id = @e", r => r.GetString(0))).ToHashSet(StringComparer.Ordinal);
        var cpfs = funcionarios.ToDictionary(f => f.Id, f => f.Cpf);
        var calculos = (await Ler("SELECT funcionario_id, competencia, tipo FROM folha.calculo WHERE empresa_id = @e",
            r => (Cpf: cpfs.GetValueOrDefault(r.GetInt64(0), ""), Comp: r.GetFieldValue<DateOnly>(1), Tipo: r.GetString(2)))).ToHashSet();
        var fechadas = (await Ler("SELECT competencia FROM folha.competencia WHERE empresa_id = @e AND status = 'FECHADA'", r => r.GetFieldValue<DateOnly>(0))).ToHashSet();
        var ferias = (await Ler("SELECT funcionario_id, gozo_inicio FROM folha.ferias WHERE empresa_id = @e AND status = 'CALCULADA'",
            r => (Cpf: cpfs.GetValueOrDefault(r.GetInt64(0), ""), Gozo: r.GetFieldValue<DateOnly>(1)))).ToHashSet();
        var afast = (await Ler("SELECT funcionario_id, data_inicio, codigo_motivo FROM folha.afastamento WHERE empresa_id = @e",
            r => (Cpf: cpfs.GetValueOrDefault(r.GetInt64(0), ""), Ini: r.GetFieldValue<DateOnly>(1), Motivo: r.GetString(2).Trim()))).ToHashSet();
        bool temParametros;
        await using (var c = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM folha.parametro_empresa WHERE empresa_id = @e)", con).Com("e", empresaId))
            temParametros = (bool)(await c.ExecuteScalarAsync(ct))!;
        return new SituacaoFolha
        {
            Cnpj = empresa.Cnpj,
            Funcionarios = funcionarios,
            Rubricas = await cadastros.ListarRubricasAsync(empresaId, ct),
            Departamentos = departamentos,
            Cargos = await cadastros.ListarCargosAsync(empresaId, ct),
            Parametros = temParametros ? await cadastros.ObterParametrosAsync(empresaId, ct) : null,
            Calculos = calculos,
            CompetenciasFechadas = fechadas,
            Ferias = ferias,
            Afastamentos = afast,
            ParametrosLegais = await cadastros.ListarParametrosLegaisAsync(ct),
            Hoje = DateOnly.FromDateTime(DateTime.Today),
        };
    }

    // ------------------------------------------------------------------ importar

    public async Task<RelatorioImportacao> ImportarAsync(long empresaId, PlanoEsocial plano, string arquivo, string sha256, long usuarioId,
                                                         IProgress<ProgressoImportacao>? progresso, CancellationToken ct)
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        await Sql.DefinirUsuarioAsync(con, tx, usuarioId, ct);
        NpgsqlCommand Cmd(string sql) => new(sql, con, tx);
        long importacaoId;
        await using (var c = Cmd("""
            INSERT INTO core.importacao_sped (empresa_id, tipo, modo, arquivo, sha256, periodo_inicio, periodo_fim, importado_por)
            VALUES (@e, 'ESOCIAL', 'COMPLETO', @a, @h, @i, @f, @u) RETURNING id
            """).Com("e", empresaId).Com("a", arquivo.Length > 260 ? arquivo[^260..] : arquivo).Com("h", sha256).ComData("i", plano.Inicio).ComData("f", plano.Fim).Com("u", usuarioId))
            importacaoId = (long)(await c.ExecuteScalarAsync(ct))!;

        var total = plano.Rubricas.Count + plano.Departamentos.Count + plano.Cargos.Count + plano.Funcionarios.Count + plano.Afastamentos.Count + plano.Ferias.Count + plano.Calculos.Count + 2;
        var feito = 0;
        void Passo(string etapa, int n = 1) { feito += n; progresso?.Report(new ProgressoImportacao(etapa, Math.Min(feito, total), total)); }
        var ids = new Dictionary<string, List<long>>();
        void Registra(string tabela, long id) { if (!ids.TryGetValue(tabela, out var l)) ids[tabela] = l = []; l.Add(id); }
        var desfazer = new DadosDesfazer(null, []);
        var parametrosAtualizados = false;

        // parâmetros de encargos
        if (plano.Parametros is { } par)
        {
            ct.ThrowIfCancellationRequested();
            if (plano.ParametrosNovos)
            {
                await using var c = Cmd("""
                    INSERT INTO folha.parametro_empresa (empresa_id, rat_aliquota, fap, fpas, cod_terceiros, class_trib) VALUES (@e, @r, @f, @p, @c, @t)
                    ON CONFLICT (empresa_id) DO NOTHING
                    """).Com("e", empresaId).Com("r", par.RatAliquota).Com("f", par.Fap).Com("p", par.Fpas).Com("c", par.CodTerceiros).Com("t", par.ClassTrib);
                if (await c.ExecuteNonQueryAsync(ct) > 0) Registra(TParam, empresaId);
            }
            else
            {
                ParametroAnterior? ant = null;
                await using (var c = Cmd("SELECT rat_aliquota, fap, fpas, cod_terceiros, class_trib FROM folha.parametro_empresa WHERE empresa_id = @e FOR UPDATE").Com("e", empresaId))
                await using (var r = await c.ExecuteReaderAsync(ct))
                    if (await r.ReadAsync(ct)) ant = new ParametroAnterior(r.GetDecimal(0), r.GetDecimal(1), r.GetString(2).Trim(), r.GetString(3).Trim(), r.GetString(4).Trim());
                await using (var c = Cmd("UPDATE folha.parametro_empresa SET rat_aliquota = @r, fap = @f, fpas = @p, cod_terceiros = @c, class_trib = @t WHERE empresa_id = @e")
                                 .Com("e", empresaId).Com("r", par.RatAliquota).Com("f", par.Fap).Com("p", par.Fpas).Com("c", par.CodTerceiros).Com("t", par.ClassTrib))
                    await c.ExecuteNonQueryAsync(ct);
                desfazer = desfazer with { Parametros = ant };
                parametrosAtualizados = true;
            }
        }
        Passo("Parâmetros de encargos");

        // rubricas
        foreach (var r in plano.Rubricas)
        {
            ct.ThrowIfCancellationRequested();
            await using var c = Cmd("""
                INSERT INTO folha.rubrica (empresa_id, codigo, descricao, tipo, natureza_esocial, inc_inss, inc_irrf, inc_fgts, variavel, sistema, ativa)
                VALUES (@e, @c, @d, @t, @n, @i, @ir, @f, @v, FALSE, @a) RETURNING id
                """).Com("e", empresaId).Com("c", r.Codigo).Com("d", r.Descricao).Com("t", r.Tipo.ToString()).Com("n", r.Natureza).Com("i", r.IncInss).Com("ir", r.IncIrrf)
                    .Com("f", r.IncFgts).Com("v", r.Variavel).Com("a", r.Ativa);
            Registra(TRub, (long)(await c.ExecuteScalarAsync(ct))!);
            Passo("Rubricas");
        }

        // departamentos
        var deptos = new Dictionary<string, long>(StringComparer.Ordinal);
        await using (var c = Cmd("SELECT codigo, id FROM folha.departamento WHERE empresa_id = @e").Com("e", empresaId))
        await using (var r = await c.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) deptos[r.GetString(0)] = r.GetInt64(1);
        foreach (var dep in plano.Departamentos)
        {
            ct.ThrowIfCancellationRequested();
            await using var c = Cmd("INSERT INTO folha.departamento (empresa_id, codigo, nome) VALUES (@e, @c, @n) RETURNING id").Com("e", empresaId).Com("c", dep.Codigo).Com("n", dep.Nome);
            var id = (long)(await c.ExecuteScalarAsync(ct))!;
            deptos[dep.Codigo] = id;
            Registra(TDep, id);
            Passo("Departamentos");
        }

        // cargos
        var cargos = new Dictionary<string, long>(StringComparer.Ordinal);
        long proximoCodigo;
        await using (var c = Cmd("SELECT nome, id FROM folha.cargo WHERE empresa_id = @e").Com("e", empresaId))
        await using (var r = await c.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) cargos[r.GetString(0).Trim().ToUpperInvariant()] = r.GetInt64(1);
        await using (var c = Cmd("SELECT COALESCE(MAX(codigo::bigint), 0) + 1 FROM folha.cargo WHERE empresa_id = @e AND codigo ~ '^[0-9]{1,15}$'").Com("e", empresaId))
            proximoCodigo = (long)(await c.ExecuteScalarAsync(ct))!;
        foreach (var cg in plano.Cargos)
        {
            ct.ThrowIfCancellationRequested();
            await using var c = Cmd("INSERT INTO folha.cargo (empresa_id, codigo, nome, cbo) VALUES (@e, @c, @n, @b) RETURNING id")
                .Com("e", empresaId).Com("c", (proximoCodigo++).ToString("000")).Com("n", cg.Nome).ComTexto("b", cg.Cbo);
            var id = (long)(await c.ExecuteScalarAsync(ct))!;
            cargos[cg.Nome.Trim().ToUpperInvariant()] = id;
            Registra(TCargo, id);
            Passo("Cargos");
        }

        // funcionários
        var funcionarios = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var f in plano.Funcionarios)
        {
            ct.ThrowIfCancellationRequested();
            long? cargoId = f.Cargo is not null && cargos.TryGetValue(f.Cargo.Trim().ToUpperInvariant(), out var cid) ? cid : null;
            long? deptoId = f.Departamento is not null && deptos.TryGetValue(f.Departamento, out var did) ? did : null;
            var e = f.Endereco;
            long funcId;
            await using (var c = Cmd("""
                INSERT INTO folha.funcionario (empresa_id, matricula, nome, cpf, data_nascimento, sexo, raca_cor, estado_civil, grau_instrucao, email, telefone, cep, logradouro, numero,
                    complemento, bairro, cod_municipio_ibge, uf, data_admissao, categoria_esocial, tipo_contrato, data_termino_contrato, cargo_id, departamento_id, cbo, tipo_salario, salario,
                    jornada_semanal_horas, jornada_mensal_horas, periculosidade, situacao, data_desligamento, motivo_desligamento, tipo_logradouro, cnpj_sindicato, tipo_jornada, descricao_jornada)
                VALUES (@e, @mat, @nome, @cpf, @nasc, @sexo, @raca, @civ, @grau, @email, @tel, @cep, @log, @num, @comp, @bai, @mun, @uf, @adm, @cat, @tc, @term, @cargo, @dep, @cbo, @ts, @sal,
                    @js, @jm, @peri, @sit, @desl, @mot, @tl, @sind, @tj, @dj) RETURNING id
                """).Com("e", empresaId).Com("mat", f.Matricula).Com("nome", f.Nome).Com("cpf", f.Cpf).ComData("nasc", f.Nascimento).Com("sexo", f.Sexo).Com("raca", (short)f.RacaCor)
                    .Com("civ", (short)f.EstadoCivil).Com("grau", f.GrauInstrucao).ComTexto("email", f.Email).ComTexto("tel", f.Telefone).ComTexto("cep", e?.Cep).ComTexto("log", e?.Logradouro)
                    .ComTexto("num", e?.Numero).ComTexto("comp", e?.Complemento).ComTexto("bai", e?.Bairro).ComTexto("mun", e?.CodMunicipio).ComTexto("uf", e?.Uf).ComData("adm", f.Admissao)
                    .Com("cat", f.Categoria).Com("tc", f.TipoContrato).ComData("term", f.Termino).ComId("cargo", cargoId).ComId("dep", deptoId).ComTexto("cbo", f.Cbo).Com("ts", f.TipoSalario)
                    .Com("sal", f.Salario).Com("js", f.JornadaSemanal).Com("jm", f.JornadaMensal).Com("peri", f.Periculosidade).Com("sit", f.Situacao).ComData("desl", f.Desligamento)
                    .ComTexto("mot", f.MotivoDesligamento).ComTexto("tl", e?.Tipo).ComTexto("sind", f.CnpjSindicato).Com("tj", (short)f.TipoJornada).ComTexto("dj", f.DescricaoJornada))
                funcId = (long)(await c.ExecuteScalarAsync(ct))!;
            funcionarios[f.Cpf] = funcId;
            Registra(TFunc, funcId);
            foreach (var d in f.Dependentes)
            {
                await using var c = Cmd("""
                    INSERT INTO folha.dependente (funcionario_id, nome, cpf, data_nascimento, tipo_dependente, incapaz, dep_irrf, dep_salario_familia)
                    VALUES (@f, @n, @c, @d, @t, @i, @ir, @sf)
                    """).Com("f", funcId).Com("n", d.Nome.Length > 150 ? d.Nome[..150] : d.Nome).ComTexto("c", d.Cpf).ComData("d", d.Nascimento)
                        .Com("t", d.Tipo.Length == 2 ? d.Tipo : d.Tipo.PadLeft(2, '0')[..2]).Com("i", d.Incapaz).Com("ir", d.DepIrrf).Com("sf", d.DepSalarioFamilia);
                await c.ExecuteNonQueryAsync(ct);
            }
            foreach (var a in f.Alteracoes)
            {
                await using var c = Cmd("""
                    INSERT INTO folha.alteracao_contratual (empresa_id, funcionario_id, data_alteracao, descricao, salario_anterior, salario_novo, cargo_anterior, cargo_novo)
                    VALUES (@e, @f, @d, @ds, @sa, @sn, @ca, @cn) RETURNING id
                    """).Com("e", empresaId).Com("f", funcId).ComData("d", a.Data).Com("ds", a.Descricao).Com("sa", NpgsqlDbType.Numeric, a.SalarioAnterior)
                        .Com("sn", NpgsqlDbType.Numeric, a.SalarioNovo).ComTexto("ca", a.CargoAnterior).ComTexto("cn", a.CargoNovo);
                Registra(TAlt, (long)(await c.ExecuteScalarAsync(ct))!);
            }
            Passo("Funcionários");
        }
        await using (var c = Cmd("SELECT cpf, id FROM folha.funcionario WHERE empresa_id = @e").Com("e", empresaId))
        await using (var r = await c.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) funcionarios[r.GetString(0).Trim()] = r.GetInt64(1);

        // afastamentos e férias
        foreach (var a in plano.Afastamentos)
        {
            ct.ThrowIfCancellationRequested();
            if (!funcionarios.TryGetValue(a.Cpf, out var fid)) continue;
            await using var c = Cmd("""
                INSERT INTO folha.afastamento (empresa_id, funcionario_id, data_inicio, data_fim, codigo_motivo, observacao) VALUES (@e, @f, @i, @fi, @m, @o) RETURNING id
                """).Com("e", empresaId).Com("f", fid).ComData("i", a.Inicio).ComData("fi", a.Fim).Com("m", a.Motivo).ComTexto("o", a.Observacao);
            Registra(TAfast, (long)(await c.ExecuteScalarAsync(ct))!);
            Passo("Afastamentos");
        }
        foreach (var fe in plano.Ferias)
        {
            ct.ThrowIfCancellationRequested();
            if (!funcionarios.TryGetValue(fe.Cpf, out var fid)) continue;
            await using var c = Cmd("""
                INSERT INTO folha.ferias (empresa_id, funcionario_id, aquisitivo_inicio, aquisitivo_fim, gozo_inicio, dias_gozo, abono_dias, faltas_injustificadas, data_pagamento)
                VALUES (@e, @f, @ai, @af, @g, @d, 0, 0, @g) RETURNING id
                """).Com("e", empresaId).Com("f", fid).ComData("ai", fe.AquisitivoInicio).ComData("af", fe.AquisitivoFim).ComData("g", fe.Gozo).Com("d", (short)fe.Dias);
            Registra(TFerias, (long)(await c.ExecuteScalarAsync(ct))!);
            Passo("Férias");
        }

        // histórico de holerites
        var n = 0;
        foreach (var calc in plano.Calculos)
        {
            ct.ThrowIfCancellationRequested();
            if (!funcionarios.TryGetValue(calc.Cpf, out var fid)) continue;
            long calcId;
            await using (var c = Cmd("""
                INSERT INTO folha.calculo (empresa_id, funcionario_id, competencia, tipo, data_pagamento, total_proventos, total_descontos, liquido, base_inss, base_irrf, base_fgts,
                    inss, irrf, fgts, salario_familia, calculado_por)
                VALUES (@e, @f, @c, @t, @dp, @tp, @td, @l, @bi, @br, @bf, @i, @ir, @fg, @sf, @u) RETURNING id
                """).Com("e", empresaId).Com("f", fid).ComData("c", calc.Competencia).Com("t", calc.Tipo).ComData("dp", calc.Pagamento).Com("tp", calc.TotalProventos).Com("td", calc.TotalDescontos)
                    .Com("l", calc.TotalProventos - calc.TotalDescontos).Com("bi", calc.BaseInss).Com("br", calc.BaseIrrf).Com("bf", calc.BaseFgts).Com("i", calc.Inss).Com("ir", calc.Irrf)
                    .Com("fg", calc.Fgts).Com("sf", calc.SalarioFamilia).Com("u", usuarioId))
                calcId = (long)(await c.ExecuteScalarAsync(ct))!;
            Registra(TCalc, calcId);
            await using (var c = Cmd("""
                INSERT INTO folha.calculo_item (calculo_id, sequencia, rubrica_codigo, descricao, tipo, referencia, valor, memoria)
                SELECT @c, s, r, d, t::char(1), ref, v, @m FROM unnest(@seq, @rub, @desc, @tipo, @ref, @val) AS x(s, r, d, t, ref, v)
                """).Com("c", calcId).Com("m", calc.Memoria.Length > 500 ? calc.Memoria[..500] : calc.Memoria))
            {
                c.Parameters.AddWithValue("seq", NpgsqlDbType.Array | NpgsqlDbType.Smallint, Enumerable.Range(1, calc.Itens.Count).Select(i => (short)i).ToArray());
                c.Parameters.AddWithValue("rub", NpgsqlDbType.Array | NpgsqlDbType.Varchar, calc.Itens.Select(i => i.Rubrica).ToArray());
                c.Parameters.AddWithValue("desc", NpgsqlDbType.Array | NpgsqlDbType.Varchar, calc.Itens.Select(i => i.Descricao).ToArray());
                c.Parameters.AddWithValue("tipo", NpgsqlDbType.Array | NpgsqlDbType.Varchar, calc.Itens.Select(i => i.Tipo.ToString()).ToArray());
                c.Parameters.AddWithValue("ref", NpgsqlDbType.Array | NpgsqlDbType.Numeric, calc.Itens.Select(i => i.Referencia).ToArray());
                c.Parameters.AddWithValue("val", NpgsqlDbType.Array | NpgsqlDbType.Numeric, calc.Itens.Select(i => i.Valor).ToArray());
                await c.ExecuteNonQueryAsync(ct);
            }
            if (++n % 20 == 0) Passo("Histórico de holerites", 20);
        }
        Passo("Histórico de holerites", n % 20);

        // fechamento das competências importadas: o histórico não deve ser recalculado por engano
        foreach (var comp in plano.CompetenciasAFechar)
        {
            await using var c = Cmd("""
                INSERT INTO folha.competencia (empresa_id, competencia, status, fechada_em, fechada_por) VALUES (@e, @c, 'FECHADA', now(), @u)
                ON CONFLICT (empresa_id, competencia) DO NOTHING
                """).Com("e", empresaId).ComData("c", comp).Com("u", usuarioId);
            if (await c.ExecuteNonQueryAsync(ct) > 0) Registra(TComp, comp.Year * 100L + comp.Month);
            else
            {
                await using var u = Cmd("UPDATE folha.competencia SET status = 'FECHADA', fechada_em = now(), fechada_por = @u WHERE empresa_id = @e AND competencia = @c AND status = 'ABERTA'")
                    .Com("e", empresaId).ComData("c", comp).Com("u", usuarioId);
                if (await u.ExecuteNonQueryAsync(ct) > 0) desfazer.Reabrir.Add(comp.ToString("yyyy-MM-dd"));
            }
        }
        Passo("Competências");

        foreach (var (tabela, lista) in ids) await HistoricoImportacaoSql.ItensAsync(con, tx, importacaoId, tabela, lista, ct);

        int Qtd(string tabela) => ids.TryGetValue(tabela, out var l) ? l.Count : 0;
        var criados = new Dictionary<string, int>
        {
            ["S-1000"] = Qtd(TParam) + (parametrosAtualizados ? 1 : 0), ["S-1010"] = Qtd(TRub), ["S-1020"] = Qtd(TDep), ["CARGO"] = Qtd(TCargo), ["S-2200"] = Qtd(TFunc),
            ["S-2206"] = Qtd(TAlt), ["S-2230"] = Qtd(TAfast), ["FERIAS"] = Qtd(TFerias), ["S-1200"] = Qtd(TCalc),
            ["S-2299"] = plano.Funcionarios.Count(f => f.Desligamento is not null), ["S-1210"] = plano.Calculos.Count(c => c.Pagamento is not null),
        };
        var linhas = plano.Linhas.Select(l => new LinhaRelatorio(l.Registro, l.Descricao, criados.GetValueOrDefault(l.Registro), plano.Existentes.GetValueOrDefault(l.Registro),
                                                                  plano.Erros.GetValueOrDefault(l.Registro))).ToList();
        var relatorio = new RelatorioImportacao(importacaoId, arquivo, "eSocial", "COMPLETO", linhas, plano.Ocorrencias);
        await HistoricoImportacaoSql.FecharAsync(con, tx, importacaoId, ids.Values.Sum(l => l.Count), relatorio, desfazer, ct);
        await tx.CommitAsync(ct);
        return relatorio;
    }

    // ------------------------------------------------------------------ desfazer

    public async Task<RelatorioImportacao> DesfazerAsync(long empresaId, long importacaoId, long usuarioId, CancellationToken ct)
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        await Sql.DefinirUsuarioAsync(con, tx, usuarioId, ct);
        NpgsqlCommand Cmd(string sql) => new(sql, con, tx);
        var reg = await HistoricoImportacaoSql.CarregarAsync(con, tx, empresaId, importacaoId, ct, travar: true) ?? throw new ViolacaoRegraException("Importação não encontrada nesta empresa.");
        if (reg.Tipo != TipoHistorico) throw new ViolacaoRegraException("Esta importação não é do eSocial.");
        if (reg.Desfeita) throw new ViolacaoRegraException("Esta importação já foi desfeita.");
        var desfazer = reg.DesfazerJson is null ? new DadosDesfazer(null, []) : JsonSerializer.Deserialize<DadosDesfazer>(reg.DesfazerJson, Json) ?? new DadosDesfazer(null, []);

        var calcs = await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TCalc, ct);
        var funcs = await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TFunc, ct);
        var rubs = await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TRub, ct);
        var ferias = await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TFerias, ct);
        var afast = await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TAfast, ct);
        var alts = await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TAlt, ct);
        var cargosImp = await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TCargo, ct);
        var deptosImp = await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TDep, ct);
        var motivos = new List<string>();

        if (calcs.Count > 0)
        {
            await using var c = Cmd("""
                SELECT string_agg(DISTINCT to_char(x.competencia, 'MM/YYYY'), ', ') FROM (
                    SELECT k.competencia FROM folha.contabilizacao k
                     WHERE k.empresa_id = @e AND k.desfeita_em IS NULL AND k.competencia IN (SELECT competencia FROM folha.calculo WHERE id = ANY(@ids))
                    UNION
                    SELECT k.competencia FROM folha.contabilizacao_evento k
                     WHERE k.empresa_id = @e AND k.desfeita_em IS NULL AND k.competencia IN (SELECT competencia FROM folha.calculo WHERE id = ANY(@ids))) x
                """).Com("e", empresaId);
            c.Parameters.AddWithValue("ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint, calcs.ToArray());
            if (await c.ExecuteScalarAsync(ct) is string comps) motivos.Add($"A folha de {comps} já foi contabilizada; desfaça a contabilização antes.");
        }
        if (funcs.Count > 0)
        {
            var verificacoes = new (string Tabela, string Rotulo, List<long>? Importados)[]
            {
                ("folha.calculo", "cálculo(s) feitos depois da importação", calcs), ("folha.ferias", "férias calculadas depois da importação", ferias),
                ("folha.rescisao", "rescisão(ões)", null), ("folha.evento_variavel", "evento(s) variável(is)", null), ("folha.provisao", "provisão(ões)", null),
                ("folha.afastamento", "afastamento(s) lançados depois da importação", afast), ("folha.alteracao_contratual", "alteração(ões) contratual(is) lançadas depois da importação", alts),
            };
            foreach (var (tabela, rotulo, importados) in verificacoes)
            {
                await using var c = Cmd($"SELECT count(*) FROM {tabela} WHERE funcionario_id = ANY(@f)" + (importados is { Count: > 0 } && tabela != "folha.provisao" ? " AND NOT id = ANY(@x)" : ""));
                c.Parameters.AddWithValue("f", NpgsqlDbType.Array | NpgsqlDbType.Bigint, funcs.ToArray());
                if (importados is { Count: > 0 } && tabela != "folha.provisao") c.Parameters.AddWithValue("x", NpgsqlDbType.Array | NpgsqlDbType.Bigint, importados.ToArray());
                var q = (long)(await c.ExecuteScalarAsync(ct))!;
                if (q > 0) motivos.Add($"Os funcionários importados já têm {q} {rotulo}; não dá para desfazer a importação sem perder esse trabalho.");
            }
        }
        if (rubs.Count > 0)
        {
            await using var c = Cmd("SELECT count(*) FROM folha.evento_variavel WHERE rubrica_id = ANY(@a)");
            c.Parameters.AddWithValue("a", NpgsqlDbType.Array | NpgsqlDbType.Bigint, rubs.ToArray());
            var q = (long)(await c.ExecuteScalarAsync(ct))!;
            if (q > 0) motivos.Add($"As rubricas importadas já foram usadas em {q} evento(s) variável(is).");
        }
        if (motivos.Count > 0) throw new ViolacaoRegraException(string.Join('\n', motivos.Distinct()));

        var removidos = new Dictionary<string, int>();
        var avisos = new List<OcorrenciaImportacao>();
        async Task<int> Remover(string sql, IReadOnlyCollection<long> lista)
        {
            if (lista.Count == 0) return 0;
            await using var c = Cmd(sql);
            c.Parameters.AddWithValue("ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint, lista.ToArray());
            return await c.ExecuteNonQueryAsync(ct);
        }
        removidos["S-1200"] = await Remover("DELETE FROM folha.calculo WHERE id = ANY(@ids)", calcs);
        foreach (var yyyymm in await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TComp, ct))
        {
            await using var c = Cmd("DELETE FROM folha.competencia WHERE empresa_id = @e AND competencia = @c AND status = 'FECHADA'").Com("e", empresaId)
                .ComData("c", new DateOnly((int)(yyyymm / 100), (int)(yyyymm % 100), 1));
            await c.ExecuteNonQueryAsync(ct);
        }
        foreach (var s in desfazer.Reabrir)
        {
            await using var c = Cmd("UPDATE folha.competencia SET status = 'ABERTA', fechada_em = NULL, fechada_por = NULL WHERE empresa_id = @e AND competencia = @c")
                .Com("e", empresaId).ComData("c", DateOnly.Parse(s));
            await c.ExecuteNonQueryAsync(ct);
        }
        removidos["FERIAS"] = await Remover("DELETE FROM folha.ferias WHERE id = ANY(@ids)", ferias);
        removidos["S-2230"] = await Remover("DELETE FROM folha.afastamento WHERE id = ANY(@ids)", afast);
        removidos["S-2206"] = await Remover("DELETE FROM folha.alteracao_contratual WHERE id = ANY(@ids)", alts);
        removidos["S-2200"] = await Remover("DELETE FROM folha.funcionario WHERE id = ANY(@ids)", funcs);
        removidos["CARGO"] = await Remover("DELETE FROM folha.cargo c WHERE c.id = ANY(@ids) AND NOT EXISTS (SELECT 1 FROM folha.funcionario f WHERE f.cargo_id = c.id)", cargosImp);
        removidos["S-1020"] = await Remover("DELETE FROM folha.departamento d WHERE d.id = ANY(@ids) AND NOT EXISTS (SELECT 1 FROM folha.funcionario f WHERE f.departamento_id = d.id)", deptosImp);
        removidos["S-1010"] = await Remover("""
            DELETE FROM folha.rubrica r WHERE r.id = ANY(@ids)
               AND NOT EXISTS (SELECT 1 FROM folha.calculo_item i JOIN folha.calculo c ON c.id = i.calculo_id WHERE c.empresa_id = r.empresa_id AND i.rubrica_codigo = r.codigo)
            """, rubs);
        if (removidos["S-1010"] < rubs.Count)
            avisos.Add(OcorrenciaImportacao.Aviso(0, "S-1010", $"{rubs.Count - removidos["S-1010"]} rubrica(s) importada(s) continuam no cadastro porque já aparecem em cálculos."));
        if (removidos["CARGO"] < cargosImp.Count || removidos["S-1020"] < deptosImp.Count)
            avisos.Add(OcorrenciaImportacao.Aviso(0, "cadastros", "Cargos ou departamentos importados continuam no cadastro porque outros funcionários os usam."));
        if (await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TParam, ct) is { Count: > 0 })
        {
            await using var c = Cmd("DELETE FROM folha.parametro_empresa WHERE empresa_id = @e").Com("e", empresaId);
            removidos["S-1000"] = await c.ExecuteNonQueryAsync(ct);
        }
        else if (desfazer.Parametros is { } p)
        {
            await using var c = Cmd("UPDATE folha.parametro_empresa SET rat_aliquota = @r, fap = @f, fpas = @fp, cod_terceiros = @c, class_trib = @t WHERE empresa_id = @e")
                .Com("e", empresaId).Com("r", p.Rat).Com("f", p.Fap).Com("fp", p.Fpas).Com("c", p.CodTerceiros).Com("t", p.ClassTrib);
            removidos["S-1000"] = await c.ExecuteNonQueryAsync(ct);
        }
        await HistoricoImportacaoSql.MarcarDesfeitaAsync(con, tx, importacaoId, usuarioId, ct);
        var original = HistoricoImportacaoSql.Relatorio(reg, true);
        var linhas = removidos.Where(x => x.Value > 0).Select(x => new LinhaRelatorio(x.Key, original.Linhas.FirstOrDefault(l => l.Registro == x.Key)?.Descricao ?? x.Key, x.Value, 0, 0)).ToList();
        await tx.CommitAsync(ct);
        return new RelatorioImportacao(importacaoId, reg.Arquivo, "eSocial", reg.Modo, linhas, avisos, true);
    }
}
