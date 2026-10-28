using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Vorticis.Application.Sped.Importacao;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Contabil;
using Vorticis.Domain.Sped.Importacao;
using Vorticis.Infra.Sped;

namespace Vorticis.Infra.Postgres;

/// <summary>Grava a importação da ECD numa única transação e desfaz o que ela criou (histórico em core.importacao_sped).</summary>
public sealed class RepositorioImportacaoEcd(NpgsqlDataSource ds) : IRepositorioImportacaoEcd
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string TLanc = "contabil.lancamento", TConta = "contabil.plano_contas", TCentro = "contabil.centro_custo", THist = "contabil.historico_padrao",
                         TMap = "contabil.mapeamento_referencial", TPart = "core.participante", TExerc = "contabil.exercicio", TContador = "core.contador",
                         TVinculo = "core.empresa_contabilidade";

    // ------------------------------------------------------------------ situação da empresa
    public async Task<SituacaoContabil?> SituacaoAsync(long empresaId, CancellationToken ct)
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        string cnpj, nome; string? mascara; DateOnly inicio;
        await using (var cmd = new NpgsqlCommand("SELECT cnpj, razao_social, mascara_plano, data_inicio_ativ FROM core.empresa WHERE id = @e", con).Com("e", empresaId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            if (!await r.ReadAsync(ct)) return null;
            (cnpj, nome, mascara, inicio) = (r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetFieldValue<DateOnly>(3));
        }
        var contas = new List<ContaExistente>();
        await using (var cmd = new NpgsqlCommand("""
            SELECT c.id, c.codigo, c.descricao, c.tipo = 'A', c.cod_nat, EXISTS (SELECT 1 FROM contabil.partida p WHERE p.conta_id = c.id)
              FROM contabil.plano_contas c WHERE c.empresa_id = @e
            """, con).Com("e", empresaId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) contas.Add(new ContaExistente(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetBoolean(3), r.GetString(4), r.GetBoolean(5)));
        var centros = await Textos(con, "SELECT codigo FROM contabil.centro_custo WHERE empresa_id = @e", empresaId, ct);
        var hist = await Textos(con, "SELECT codigo FROM contabil.historico_padrao WHERE empresa_id = @e", empresaId, ct);
        var parts = new List<ParticipanteExistente>();
        await using (var cmd = new NpgsqlCommand("SELECT id, codigo, cpf_cnpj FROM core.participante WHERE empresa_id = @e", con).Com("e", empresaId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) parts.Add(new ParticipanteExistente(r.GetInt64(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2)));
        bool temLanc, temSaldo; DateOnly? primeiro;
        await using (var cmd = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM contabil.lancamento WHERE empresa_id = @e), MIN(data_lancamento),
                   COALESCE(bool_or(origem = 'SALDO_INICIAL' AND status = 'EFETIVADO'), FALSE)
              FROM contabil.lancamento WHERE empresa_id = @e
            """, con).Com("e", empresaId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            await r.ReadAsync(ct);
            (temLanc, primeiro, temSaldo) = (r.GetBoolean(0), r.IsDBNull(1) ? null : r.GetFieldValue<DateOnly>(1), r.GetBoolean(2));
        }
        var exercicios = new List<ExercicioExistente>();
        await using (var cmd = new NpgsqlCommand("SELECT ano, data_inicio, data_fim, encerrado FROM contabil.exercicio WHERE empresa_id = @e ORDER BY ano", con).Com("e", empresaId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) exercicios.Add(new ExercicioExistente(r.GetInt16(0), r.GetFieldValue<DateOnly>(1), r.GetFieldValue<DateOnly>(2), r.GetBoolean(3)));
        var fechados = new HashSet<DateOnly>();
        await using (var cmd = new NpgsqlCommand("""
            SELECT p.competencia FROM contabil.periodo p JOIN contabil.exercicio e ON e.id = p.exercicio_id WHERE e.empresa_id = @e AND p.status <> 'ABERTO'
            """, con).Com("e", empresaId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) fechados.Add(r.GetFieldValue<DateOnly>(0));
        return new SituacaoContabil(cnpj.Trim(), nome, mascara, inicio, contas, centros.ToHashSet(StringComparer.Ordinal), hist.ToHashSet(StringComparer.Ordinal), parts,
                                    temLanc, primeiro, temSaldo, exercicios, fechados, false);
    }

    private static async Task<List<string>> Textos(NpgsqlConnection con, string sql, long empresaId, CancellationToken ct)
    {
        var res = new List<string>();
        await using var cmd = new NpgsqlCommand(sql, con).Com("e", empresaId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) res.Add(r.GetString(0));
        return res;
    }

    public async Task<(long Id, string Nome)?> EmpresaPorCnpjAsync(string cnpj, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("SELECT id, razao_social FROM core.empresa WHERE cnpj = @c").Com("c", cnpj);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? (r.GetInt64(0), r.GetString(1)) : null;
    }

    // ------------------------------------------------------------------ importação
    private sealed record Anterior(string Codigo, string Descricao, bool Analitica, short Nivel, string? PaiCodigo, string CodNat, string Natureza, DateOnly DataInclusao,
                                   bool ExigeCentro, bool ExigeParticipante, bool Ativa);
    private sealed record DesfazerEcd(List<Anterior>? PlanoAnterior, string? ModeloAnterior);

    public async Task<ResultadoGravacao> ImportarAsync(long empresaId, PlanoImportacaoEcd plano, MetaImportacao meta, bool ignorarComErro, long usuarioId,
                                                       IProgress<ProgressoImportacao>? progresso, CancellationToken ct)
    {
        var importados = new Dictionary<string, int>();
        void Soma(string reg, int n = 1) => importados[reg] = importados.GetValueOrDefault(reg) + n;
        var oc = new List<OcorrenciaImportacao>();
        await using var con = await ds.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        await Mapeamentos.DefinirUsuarioAsync(con, tx, usuarioId, ct);
        await using (var c = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext('vorticis_importacao_sped'), @e::int)", con, tx).Com("e", empresaId)) await c.ExecuteNonQueryAsync(ct);
        var id = await HistoricoImportacaoSql.AbrirAsync(con, tx, empresaId, "ECD", meta, usuarioId, ct);
        progresso?.Report(new ProgressoImportacao("Preparando a empresa", 0, 100));

        // exercícios que faltam
        foreach (var ano in plano.ExerciciosACriar)
        {
            await using var existe = new NpgsqlCommand("SELECT id FROM contabil.exercicio WHERE empresa_id = @e AND ano = @a", con, tx).Com("e", empresaId).Com("a", (short)ano);
            if (await existe.ExecuteScalarAsync(ct) is not null) continue;
            await RepositorioEmpresa.CriarExercicioAsync(con, tx, empresaId, ano, ct);
            await using var q = new NpgsqlCommand("SELECT id FROM contabil.exercicio WHERE empresa_id = @e AND ano = @a", con, tx).Com("e", empresaId).Com("a", (short)ano);
            await HistoricoImportacaoSql.ItemAsync(con, tx, id, TExerc, (long)(await q.ExecuteScalarAsync(ct))!, ct);
            oc.Add(OcorrenciaImportacao.Info(0, "", $"Exercício {ano} criado."));
        }

        // plano de contas
        DesfazerEcd? desfazer = null;
        if (plano.SubstituirPlano)
        {
            var anterior = new List<Anterior>();
            await using (var q = new NpgsqlCommand("""
                SELECT c.codigo, c.descricao, c.tipo = 'A', c.nivel, p.codigo, c.cod_nat, c.natureza_saldo::text, c.data_inclusao, c.exige_centro_custo, c.exige_participante, c.ativa
                  FROM contabil.plano_contas c LEFT JOIN contabil.plano_contas p ON p.id = c.conta_pai_id WHERE c.empresa_id = @e ORDER BY c.nivel, c.codigo
                """, con, tx).Com("e", empresaId))
            await using (var r = await q.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct))
                    anterior.Add(new Anterior(r.GetString(0), r.GetString(1), r.GetBoolean(2), r.GetInt16(3), r.IsDBNull(4) ? null : r.GetString(4), r.GetString(5), r.GetString(6),
                                              r.GetFieldValue<DateOnly>(7), r.GetBoolean(8), r.GetBoolean(9), r.GetBoolean(10)));
            string? modelo;
            await using (var q = new NpgsqlCommand("SELECT plano_modelo FROM core.empresa WHERE id = @e", con, tx).Com("e", empresaId)) modelo = await q.ExecuteScalarAsync(ct) as string;
            desfazer = new DesfazerEcd(anterior, modelo);
            await using var del = new NpgsqlCommand("""
                DELETE FROM contabil.mapeamento_referencial WHERE conta_id IN (SELECT id FROM contabil.plano_contas WHERE empresa_id = @e);
                DELETE FROM contabil.saldo_mensal WHERE conta_id IN (SELECT id FROM contabil.plano_contas WHERE empresa_id = @e);
                DELETE FROM contabil.plano_contas WHERE empresa_id = @e;
                UPDATE core.participante SET controle_conta = FALSE WHERE empresa_id = @e;
                UPDATE core.empresa SET plano_modelo = NULL WHERE id = @e;
                """, con, tx).Com("e", empresaId);
            await del.ExecuteNonQueryAsync(ct);
        }
        progresso?.Report(new ProgressoImportacao("Importando o plano de contas", 5, 100));
        var idsConta = new Dictionary<string, long>(StringComparer.Ordinal);   // código final → id
        await using (var q = new NpgsqlCommand("SELECT codigo, id FROM contabil.plano_contas WHERE empresa_id = @e", con, tx).Com("e", empresaId))
        await using (var r = await q.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) idsConta[r.GetString(0)] = r.GetInt64(1);
        var novasContas = new List<long>();
        foreach (var c in plano.Contas.Where(c => !c.Existente && c.Valida).OrderBy(c => c.Nivel).ThenBy(c => c.Origem.Linha))
        {
            ct.ThrowIfCancellationRequested();
            long? pai = c.CodigoPai is null ? null : idsConta.TryGetValue(c.CodigoPai, out var pid) ? pid : throw new ViolacaoRegraException($"Conta superior {c.CodigoPai} não encontrada para a conta {c.Codigo}.");
            await using var ins = new NpgsqlCommand("""
                INSERT INTO contabil.plano_contas (empresa_id, codigo, descricao, tipo, nivel, conta_pai_id, cod_nat, natureza_saldo, data_inclusao)
                VALUES (@e, @c, @d, @t::contabil.tipo_conta, @n, @pai, @nat, @sal::contabil.indicador_dc, @inc) RETURNING id
                """, con, tx).Com("e", empresaId).Com("c", c.Codigo).Com("d", Cortar(c.Origem.Descricao, 200)).Com("t", c.Analitica ? "A" : "S").Com("n", c.Nivel)
                             .Com("pai", NpgsqlDbType.Bigint, pai).Com("nat", c.Origem.CodNat).Com("sal", c.Natureza.ToString()).Com("inc", c.DataInclusao);
            var nid = (long)(await ins.ExecuteScalarAsync(ct))!;
            idsConta[c.Codigo] = nid;
            novasContas.Add(nid);
        }
        await HistoricoImportacaoSql.ItensAsync(con, tx, id, TConta, novasContas, ct);
        Soma("I050", novasContas.Count);

        // centros de custo e históricos
        var novosCentros = new List<long>();
        foreach (var c in plano.CentrosNovos)
        {
            await using var ins = new NpgsqlCommand("INSERT INTO contabil.centro_custo (empresa_id, codigo, descricao, data_inclusao) VALUES (@e, @c, @d, @i) RETURNING id", con, tx)
                .Com("e", empresaId).Com("c", c.Codigo).Com("d", c.Descricao).Com("i", c.DataInclusao);
            novosCentros.Add((long)(await ins.ExecuteScalarAsync(ct))!);
        }
        await HistoricoImportacaoSql.ItensAsync(con, tx, id, TCentro, novosCentros, ct);
        Soma("I100", novosCentros.Count);
        if (novosCentros.Count > 0)
            await using (var u = new NpgsqlCommand("UPDATE core.empresa SET usa_centro_custo = TRUE WHERE id = @e", con, tx).Com("e", empresaId)) await u.ExecuteNonQueryAsync(ct);
        var novosHist = new List<long>();
        foreach (var h in plano.HistoricosNovos)
        {
            await using var ins = new NpgsqlCommand("INSERT INTO contabil.historico_padrao (empresa_id, codigo, descricao) VALUES (@e, @c, @d) RETURNING id", con, tx)
                .Com("e", empresaId).Com("c", h.Codigo).Com("d", h.Descricao);
            novosHist.Add((long)(await ins.ExecuteScalarAsync(ct))!);
        }
        await HistoricoImportacaoSql.ItensAsync(con, tx, id, THist, novosHist, ct);
        Soma("I075", novosHist.Count);

        // vínculo com o plano referencial
        if (plano.Referencias.Count > 0) await VincularReferencialAsync(con, tx, empresaId, id, plano, idsConta, oc, Soma, ct);

        // participantes
        var partCodigo = new Dictionary<string, string>(StringComparer.Ordinal);   // código no SPED → código no sistema
        var novosPart = new List<long>();
        foreach (var p in plano.Participantes)
        {
            ct.ThrowIfCancellationRequested();
            var (pid, codigo, criado) = await HistoricoImportacaoSql.GarantirParticipanteAsync(con, tx, empresaId, p.Nome, p.Documento, p.Uf, p.CodMunicipio, null, false, false, ct);
            partCodigo[p.CodigoSped] = codigo;
            if (criado) novosPart.Add(pid);
        }
        await HistoricoImportacaoSql.ItensAsync(con, tx, id, TPart, novosPart, ct);
        Soma("0150", novosPart.Count);

        // contexto para validar saldo inicial e lançamentos com as contas já gravadas
        var ctx = await CarregarContextoAsync(con, tx, empresaId, partCodigo, ct);
        var lancamentosGravados = new List<long>();
        if (plano.SaldoInicial is { } si)
        {
            var prop = new LancamentoProposto(empresaId, si.Data, OrigemLancamento.SaldoInicial, si.Partidas);
            var v = ValidadorLancamento.Validar(prop, ctx.Para(si.Data));
            if (!v.Sucesso) oc.Add(OcorrenciaImportacao.Erro(0, "I155", "Saldos iniciais não importados: " + string.Join(" ", v.Erros.Take(3))));
            else
            {
                var g = await RepositorioContabil.GravarNaTransacaoAsync(con, tx, v.Valor, usuarioId, ct);
                lancamentosGravados.Add(g.Id);
                Soma("I155", si.Partidas.Count);
            }
        }

        // lançamentos
        var total = plano.Lancamentos.Count;
        var feitos = 0;
        foreach (var l in plano.Lancamentos)
        {
            ct.ThrowIfCancellationRequested();
            if (++feitos % 25 == 0 || feitos == total) progresso?.Report(new ProgressoImportacao("Importando lançamentos", feitos, total));
            if (!l.Valido) continue;                    // já relatado na prévia
            var partidas = l.Proposta.Partidas.Select(p => p with { CodigoParticipante = p.CodigoParticipante is { } cp && cp.StartsWith("SPED:") ? partCodigo.GetValueOrDefault(cp[5..]) : p.CodigoParticipante }).ToList();
            var v = ValidadorLancamento.Validar(l.Proposta with { EmpresaId = empresaId, Partidas = partidas }, ctx.Para(l.Proposta.Data));
            if (!v.Sucesso)
            {
                var motivo = $"Lançamento {l.Numero}: {string.Join(" ", v.Erros.Take(3))}";
                if (!ignorarComErro) throw new ViolacaoRegraException(motivo);
                oc.Add(OcorrenciaImportacao.ErroPontual(l.Linha, "I200", motivo));
                continue;
            }
            var g = await RepositorioContabil.GravarNaTransacaoAsync(con, tx, v.Valor, usuarioId, ct);
            lancamentosGravados.Add(g.Id);
            Soma("I200"); Soma("I250", l.Proposta.Partidas.Count);
        }
        await HistoricoImportacaoSql.ItensAsync(con, tx, id, TLanc, lancamentosGravados, ct);

        // contador signatário
        foreach (var s in plano.SignatariosNovos)
        {
            await using var ins = new NpgsqlCommand("""
                INSERT INTO core.contador (nome, cpf, crc, uf_crc, email, telefone, assina_ecd_ecf)
                VALUES (@n, @cpf, @crc, @uf, @em, @tel, TRUE) ON CONFLICT (cpf) DO NOTHING RETURNING id
                """, con, tx).Com("n", Cortar(s.Nome, 150)).Com("cpf", s.Documento).Com("crc", Cortar(s.Crc ?? "", 20)).Com("uf", s.UfCrc!.ToUpperInvariant())
                             .ComTexto("em", s.Email is { Length: <= 150 } ? s.Email : null).ComTexto("tel", s.Telefone);
            if (await ins.ExecuteScalarAsync(ct) is long cid)
            {
                await HistoricoImportacaoSql.ItemAsync(con, tx, id, TContador, cid, ct);
                await using var vinc = new NpgsqlCommand("""
                    INSERT INTO core.empresa_contabilidade (empresa_id, contador_id, inicio)
                    SELECT @e, @c, @i WHERE NOT EXISTS (SELECT 1 FROM core.empresa_contabilidade WHERE empresa_id = @e AND fim IS NULL AND contador_id IS NOT NULL) RETURNING id
                    """, con, tx).Com("e", empresaId).Com("c", cid).Com("i", plano.Ecd.Cabecalho.Inicio);
                if (await vinc.ExecuteScalarAsync(ct) is long vid) await HistoricoImportacaoSql.ItemAsync(con, tx, id, TVinculo, vid, ct);
                Soma("J930");
            }
        }

        progresso?.Report(new ProgressoImportacao("Gravando", 100, 100));
        var gravacao = new ResultadoGravacao(id, importados, oc);
        var rel = AuxiliarImportacao.Relatorio(id, meta.Arquivo, TipoArquivoSped.Ecd, meta.Modo, plano.Linhas, gravacao, plano.Ocorrencias);
        await HistoricoImportacaoSql.FecharAsync(con, tx, id, importados.Where(x => x.Key is "I050" or "I100" or "I075" or "I200" or "0150" or "J930").Sum(x => x.Value), rel, desfazer, ct);
        await tx.CommitAsync(ct);      // as partidas dobradas são conferidas pelo banco aqui
        return gravacao;
    }

    private static string Cortar(string s, int max) => s.Length > max ? s[..max] : s;

    private async Task VincularReferencialAsync(NpgsqlConnection con, NpgsqlTransaction tx, long empresaId, long importacaoId, PlanoImportacaoEcd plano,
                                                Dictionary<string, long> idsConta, List<OcorrenciaImportacao> oc, Action<string, int> soma, CancellationToken ct)
    {
        var codPlan = plano.Ecd.CodigoPlanoReferencial;
        var ano = plano.Ecd.Cabecalho.Fim.Year;
        var refs = new Dictionary<string, long>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(codPlan))
            await using (var q = new NpgsqlCommand("SELECT codigo, id FROM contabil.plano_referencial WHERE cod_plan_ref = @p AND ano_vigencia = @a", con, tx).Com("p", codPlan).Com("a", (short)ano))
            await using (var r = await q.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct)) refs[r.GetString(0)] = r.GetInt64(1);
        if (refs.Count == 0)
        {
            oc.Add(OcorrenciaImportacao.Aviso(0, "I051", $"O plano referencial {codPlan ?? "(não informado)"} de {ano} não está instalado; os {plano.Referencias.Count} vínculos do arquivo (I051) não foram importados. " +
                                                       "Importe o plano referencial em Integrações › Importar › Plano referencial da RFB e vincule as contas em Cadastros › Vínculo ao plano referencial."));
            return;
        }
        var criados = new List<long>();
        var semRef = 0;
        foreach (var r in plano.Referencias)
        {
            if (!refs.TryGetValue(r.CodigoReferencial, out var rid) || !idsConta.TryGetValue(r.CodigoConta, out var cid)) { semRef++; continue; }
            await using var sp = new NpgsqlCommand("SAVEPOINT vinculo", con, tx);
            await sp.ExecuteNonQueryAsync(ct);
            try
            {
                await using var ins = new NpgsqlCommand("""
                    INSERT INTO contabil.mapeamento_referencial (empresa_id, conta_id, plano_referencial_id, ano_vigencia) VALUES (@e, @c, @r, @a)
                    ON CONFLICT DO NOTHING RETURNING id
                    """, con, tx).Com("e", empresaId).Com("c", cid).Com("r", rid).Com("a", (short)ano);
                if (await ins.ExecuteScalarAsync(ct) is long mid) criados.Add(mid);
            }
            catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.RaiseException or PostgresErrorCodes.CheckViolation)
            {
                await using var rb = new NpgsqlCommand("ROLLBACK TO SAVEPOINT vinculo", con, tx);
                await rb.ExecuteNonQueryAsync(ct);
                semRef++;
            }
        }
        await HistoricoImportacaoSql.ItensAsync(con, tx, importacaoId, TMap, criados, ct);
        soma("I051", criados.Count);
        if (semRef > 0) oc.Add(OcorrenciaImportacao.Aviso(0, "I051", $"{semRef} vínculo(s) com o plano referencial não puderam ser importados (conta referencial inexistente no plano instalado ou natureza incompatível)."));
        await using (var u = new NpgsqlCommand("UPDATE contabil.exercicio SET cod_plan_ref = @p WHERE empresa_id = @e AND ano = @a AND cod_plan_ref IS NULL", con, tx)
            .Com("p", codPlan).Com("e", empresaId).Com("a", (short)ano)) await u.ExecuteNonQueryAsync(ct);
    }

    // ------------------------------------------------------------------ contexto de validação dentro da transação
    private sealed class Contexto
    {
        public required Dictionary<string, ContaInfo> Contas { get; init; }
        public required Dictionary<string, long> Centros { get; init; }
        public required Dictionary<string, long> Participantes { get; init; }
        public required List<(long Id, DateOnly Inicio, DateOnly Fim, bool Encerrado, Dictionary<DateOnly, string> Periodos, DateOnly? Bloqueio, bool ExigeDoc)> Exercicios { get; init; }

        public ContextoValidacao Para(DateOnly data)
        {
            var ex = Exercicios.FirstOrDefault(e => data >= e.Inicio && data <= e.Fim);
            PeriodoInfo? p = null;
            if (ex.Id != 0)
            {
                var comp = new DateOnly(data.Year, data.Month, 1);
                var tem = ex.Periodos.TryGetValue(comp, out var status);
                p = new PeriodoInfo(ex.Id, ex.Inicio, ex.Fim, ex.Encerrado, tem, tem && status == "ABERTO", ex.Bloqueio, ex.ExigeDoc);
            }
            return new ContextoValidacao(Contas, Centros, Participantes, p);
        }
    }

    private static async Task<Contexto> CarregarContextoAsync(NpgsqlConnection con, NpgsqlTransaction tx, long empresaId, Dictionary<string, string> partCodigo, CancellationToken ct)
    {
        var contas = new Dictionary<string, ContaInfo>(StringComparer.Ordinal);
        await using (var q = new NpgsqlCommand("""
            SELECT id, codigo, descricao, tipo = 'A', ativa, data_inclusao, exige_centro_custo, exige_participante, cod_nat FROM contabil.plano_contas WHERE empresa_id = @e
            """, con, tx).Com("e", empresaId))
        await using (var r = await q.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
                contas[r.GetString(1)] = new ContaInfo(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetBoolean(3), r.GetBoolean(4), r.GetFieldValue<DateOnly>(5),
                                                       r.GetBoolean(6), r.GetBoolean(7), r.GetString(8));
        var centros = new Dictionary<string, long>(StringComparer.Ordinal);
        await using (var q = new NpgsqlCommand("SELECT codigo, id FROM contabil.centro_custo WHERE empresa_id = @e AND tipo = 'A' AND ativo", con, tx).Com("e", empresaId))
        await using (var r = await q.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) centros[r.GetString(0)] = r.GetInt64(1);
        var parts = new Dictionary<string, long>(StringComparer.Ordinal);
        await using (var q = new NpgsqlCommand("SELECT codigo, id FROM core.participante WHERE empresa_id = @e", con, tx).Com("e", empresaId))
        await using (var r = await q.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) parts[r.GetString(0)] = r.GetInt64(1);
        var exs = new List<(long, DateOnly, DateOnly, bool, Dictionary<DateOnly, string>, DateOnly?, bool)>();
        await using (var q = new NpgsqlCommand("""
            SELECT e.id, e.data_inicio, e.data_fim, e.encerrado, emp.data_bloqueio, emp.exige_documento FROM contabil.exercicio e JOIN core.empresa emp ON emp.id = e.empresa_id
             WHERE e.empresa_id = @e ORDER BY e.data_inicio
            """, con, tx).Com("e", empresaId))
        await using (var r = await q.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
                exs.Add((r.GetInt64(0), r.GetFieldValue<DateOnly>(1), r.GetFieldValue<DateOnly>(2), r.GetBoolean(3), [], r.IsDBNull(4) ? null : r.GetFieldValue<DateOnly>(4), r.GetBoolean(5)));
        foreach (var e in exs)
            await using (var q = new NpgsqlCommand("SELECT competencia, status::text FROM contabil.periodo WHERE exercicio_id = @x", con, tx).Com("x", e.Item1))
            await using (var r = await q.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct)) e.Item5[r.GetFieldValue<DateOnly>(0)] = r.GetString(1);
        return new Contexto { Contas = contas, Centros = centros, Participantes = parts, Exercicios = exs };
    }

    // ------------------------------------------------------------------ desfazer
    public async Task<RelatorioImportacao> DesfazerAsync(long empresaId, long importacaoId, long usuarioId, CancellationToken ct)
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        await Mapeamentos.DefinirUsuarioAsync(con, tx, usuarioId, ct);
        await RepositorioContabil.DeclararManutencaoAsync(con, tx, empresaId, ct);
        var reg = await HistoricoImportacaoSql.CarregarAsync(con, tx, empresaId, importacaoId, ct, travar: true)
                  ?? throw new ViolacaoRegraException("Importação não encontrada nesta empresa.");
        if (reg.Tipo != "ECD") throw new ViolacaoRegraException("Esta importação não é de uma ECD.");
        if (reg.Desfeita) throw new ViolacaoRegraException("Esta importação já foi desfeita.");
        var motivos = new List<string>();
        var lancs = await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TLanc, ct);

        // 1. os lançamentos ainda podem ser removidos?
        var numeros = new Dictionary<long, long?>();
        foreach (var lid in lancs)
        {
            var s = await RepositorioContabil.SituacaoAsync(con, tx, empresaId, lid, ct);
            if (s is null) continue;
            numeros[lid] = s.Numero;
            if (s.Status != StatusLancamento.Efetivado || s.Vinculados.Count > 0) motivos.Add($"O lançamento nº {s.Numero} foi estornado ou corrigido depois da importação.");
            else if (s.ExercicioEncerrado) motivos.Add($"O exercício de {s.Ano} já foi encerrado (lançamento nº {s.Numero}).");
            else if (!s.PeriodoAberto) motivos.Add($"O período {s.Data:MM/yyyy} está fechado (lançamento nº {s.Numero}).");
            else if (s.EcdGerada) motivos.Add($"A ECD de {s.Ano} já foi gerada neste sistema (lançamento nº {s.Numero}).");
            else if (s.Conciliado) motivos.Add($"O lançamento nº {s.Numero} já foi conciliado.");
            if (motivos.Count >= 15) break;
        }
        if (lancs.Count > 0 && motivos.Count == 0)
        {
            var usos = new List<(string Tabela, string Coluna)>();
            await using (var q = new NpgsqlCommand("""
                SELECT n.nspname || '.' || c.relname, a.attname
                  FROM pg_constraint k JOIN pg_class c ON c.oid = k.conrelid JOIN pg_namespace n ON n.oid = c.relnamespace
                  JOIN pg_attribute a ON a.attrelid = k.conrelid AND a.attnum = k.conkey[1]
                 WHERE k.contype = 'f' AND k.confrelid = 'contabil.lancamento'::regclass AND array_length(k.conkey, 1) = 1
                   AND NOT (n.nspname = 'contabil' AND c.relname IN ('partida', 'lancamento', 'lancamento_importado'))
                """, con, tx))
            await using (var r = await q.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct)) usos.Add((r.GetString(0), r.GetString(1)));
            foreach (var (tabela, coluna) in usos)
            {
                await using var q = new NpgsqlCommand($"SELECT count(*) FROM {tabela} WHERE {coluna} = ANY(@ids)", con, tx);
                q.Parameters.AddWithValue("ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint, lancs.ToArray());
                var n = (long)(await q.ExecuteScalarAsync(ct))!;
                if (n > 0) motivos.Add($"{n} lançamento(s) importado(s) já foram usados em {tabela.Replace("fiscal.documento", "notas fiscais contabilizadas").Replace("_", " ")}.");
            }
        }
        if (motivos.Count > 0) throw new ViolacaoRegraException(string.Join('\n', motivos.Distinct().Take(15)));

        // 2. remove lançamentos (do último para o primeiro), depois o resto
        var removidos = new Dictionary<string, int>();
        foreach (var (lid, numero) in numeros.OrderByDescending(x => x.Value ?? 0))
        {
            ct.ThrowIfCancellationRequested();
            await RepositorioContabil.AuditarAsync(con, tx, empresaId, lid, "EXCLUI", "jsonb_build_object('acao', 'desfazer_importacao_sped', 'lancamento', contabil.fn_retrato_lancamento(@id))", ct);
            await RepositorioContabil.AjustarSaldoAsync(con, tx, lid, -1, ct);
            await using (var c = new NpgsqlCommand("DELETE FROM contabil.partida WHERE lancamento_id = @id", con, tx).Com("id", lid)) await c.ExecuteNonQueryAsync(ct);
            await using (var c = new NpgsqlCommand("DELETE FROM contabil.lancamento WHERE id = @id", con, tx).Com("id", lid)) await c.ExecuteNonQueryAsync(ct);
        }
        removidos["I200"] = numeros.Count;
        var avisos = new List<OcorrenciaImportacao>();

        var maps = await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TMap, ct);
        if (maps.Count > 0) await Excluir(con, tx, "DELETE FROM contabil.mapeamento_referencial WHERE id = ANY(@ids)", maps, ct);
        removidos["I051"] = maps.Count;

        var contas = await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TConta, ct);
        if (contas.Count > 0)
        {
            await Excluir(con, tx, "DELETE FROM contabil.saldo_mensal WHERE conta_id = ANY(@ids) AND total_debito = 0 AND total_credito = 0", contas, ct);
            var emUso = new List<string>();
            await using (var q = new NpgsqlCommand("""
                SELECT c.codigo FROM contabil.plano_contas c WHERE c.id = ANY(@ids) AND EXISTS (SELECT 1 FROM contabil.partida p WHERE p.conta_id = c.id) ORDER BY c.codigo LIMIT 10
                """, con, tx))
            {
                q.Parameters.AddWithValue("ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint, contas.ToArray());
                await using var r = await q.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) emUso.Add(r.GetString(0));
            }
            if (emUso.Count > 0) throw new ViolacaoRegraException($"As contas {string.Join(", ", emUso)} já receberam lançamentos depois da importação e não podem ser removidas.");
            try { await Excluir(con, tx, "DELETE FROM contabil.plano_contas WHERE id = ANY(@ids)", contas, ct); }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                throw new ViolacaoRegraException("Algumas contas importadas já estão em uso (regras de contabilização, participantes, imobilizado ou outros cadastros) e não podem ser removidas. " +
                                                 "Remova esses usos antes de desfazer a importação.");
            }
        }
        removidos["I050"] = contas.Count;

        foreach (var (tabela, reg4, sql) in new[] { (TCentro, "I100", "DELETE FROM contabil.centro_custo WHERE id = ANY(@ids)"), (THist, "I075", "DELETE FROM contabil.historico_padrao WHERE id = ANY(@ids)"),
                                                    (TPart, "0150", "DELETE FROM core.participante WHERE id = ANY(@ids)") })
        {
            var ids = await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, tabela, ct);
            var ok = 0;
            foreach (var x in ids)
            {
                await using (var sp = new NpgsqlCommand("SAVEPOINT rem", con, tx)) await sp.ExecuteNonQueryAsync(ct);
                try { await Excluir(con, tx, sql, [x], ct); ok++; }
                catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
                {
                    await using var rb = new NpgsqlCommand("ROLLBACK TO SAVEPOINT rem", con, tx);
                    await rb.ExecuteNonQueryAsync(ct);
                }
            }
            removidos[reg4] = ok;
            if (ok < ids.Count) avisos.Add(OcorrenciaImportacao.Aviso(0, reg4, $"{ids.Count - ok} cadastro(s) criado(s) pela importação já estão em uso e foram mantidos."));
        }

        var vinculos = await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TVinculo, ct);
        if (vinculos.Count > 0) await Excluir(con, tx, "DELETE FROM core.empresa_contabilidade WHERE id = ANY(@ids)", vinculos, ct);
        var contadores = await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TContador, ct);
        foreach (var x in contadores)
        {
            await using (var sp = new NpgsqlCommand("SAVEPOINT rem", con, tx)) await sp.ExecuteNonQueryAsync(ct);
            try { await Excluir(con, tx, "DELETE FROM core.contador WHERE id = ANY(@ids)", [x], ct); }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            { await using var rb = new NpgsqlCommand("ROLLBACK TO SAVEPOINT rem", con, tx); await rb.ExecuteNonQueryAsync(ct); }
        }
        removidos["J930"] = contadores.Count;

        // exercícios criados pela importação, se continuam sem lançamentos
        foreach (var ex in await HistoricoImportacaoSql.IdsAsync(con, tx, importacaoId, TExerc, ct))
        {
            await using var sem = new NpgsqlCommand("SELECT NOT EXISTS (SELECT 1 FROM contabil.lancamento WHERE exercicio_id = @x)", con, tx).Com("x", ex);
            if ((bool)(await sem.ExecuteScalarAsync(ct))!)
            {
                await using var d = new NpgsqlCommand("DELETE FROM contabil.periodo WHERE exercicio_id = @x; DELETE FROM contabil.exercicio WHERE id = @x", con, tx).Com("x", ex);
                await d.ExecuteNonQueryAsync(ct);
            }
        }

        // plano anterior (quando a importação o substituiu)
        if (reg.DesfazerJson is { } dj && JsonSerializer.Deserialize<DesfazerEcd>(dj, Json) is { PlanoAnterior: { Count: > 0 } anterior } d0)
        {
            var ids = new Dictionary<string, long>(StringComparer.Ordinal);
            await using (var q = new NpgsqlCommand("SELECT codigo, id FROM contabil.plano_contas WHERE empresa_id = @e", con, tx).Com("e", empresaId))
            await using (var r = await q.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct)) ids[r.GetString(0)] = r.GetInt64(1);
            foreach (var a in anterior.OrderBy(a => a.Nivel))
            {
                if (ids.ContainsKey(a.Codigo)) continue;
                await using var ins = new NpgsqlCommand("""
                    INSERT INTO contabil.plano_contas (empresa_id, codigo, descricao, tipo, nivel, conta_pai_id, cod_nat, natureza_saldo, exige_centro_custo, exige_participante, data_inclusao, ativa)
                    VALUES (@e, @c, @d, @t::contabil.tipo_conta, @n, @pai, @nat, @sal::contabil.indicador_dc, @xc, @xp, @inc, @at) RETURNING id
                    """, con, tx).Com("e", empresaId).Com("c", a.Codigo).Com("d", a.Descricao).Com("t", a.Analitica ? "A" : "S").Com("n", a.Nivel)
                                 .Com("pai", NpgsqlDbType.Bigint, a.PaiCodigo is null ? null : ids[a.PaiCodigo]).Com("nat", a.CodNat).Com("sal", a.Natureza)
                                 .Com("xc", a.ExigeCentro).Com("xp", a.ExigeParticipante).Com("inc", a.DataInclusao).Com("at", a.Ativa);
                ids[a.Codigo] = (long)(await ins.ExecuteScalarAsync(ct))!;
            }
            if (d0.ModeloAnterior is not null)
                await using (var u = new NpgsqlCommand("UPDATE core.empresa SET plano_modelo = @m WHERE id = @e", con, tx).Com("m", d0.ModeloAnterior).Com("e", empresaId)) await u.ExecuteNonQueryAsync(ct);
            avisos.Add(OcorrenciaImportacao.Info(0, "I050", $"O plano de contas anterior da empresa ({anterior.Count} contas) foi devolvido."));
        }

        await HistoricoImportacaoSql.MarcarDesfeitaAsync(con, tx, importacaoId, usuarioId, ct);
        await tx.CommitAsync(ct);
        var linhas = removidos.Where(x => x.Value > 0).Select(x => new LinhaRelatorio(x.Key, NomeRegistro(x.Key), x.Value, 0, 0)).ToList();
        return new RelatorioImportacao(importacaoId, reg.Arquivo, TipoArquivoSped.Ecd.Nome(), reg.Modo, linhas, avisos, Desfeita: true);
    }

    private static string NomeRegistro(string r) => r switch
    {
        "I050" => "Contas do plano", "I051" => "Vínculos com o plano referencial", "I075" => "Históricos padronizados", "I100" => "Centros de custo",
        "I200" => "Lançamentos", "0150" => "Participantes", "J930" => "Contador", _ => r,
    };

    private static async Task Excluir(NpgsqlConnection con, NpgsqlTransaction tx, string sql, IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        await using var c = new NpgsqlCommand(sql, con, tx);
        c.Parameters.AddWithValue("ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint, ids.ToArray());
        await c.ExecuteNonQueryAsync(ct);
    }
}
