using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra.Postgres;

namespace Vorticis.Escritorio;

/// <summary>
/// Módulo Escritório: tarefas e prazos, honorários, protocolos e a carteira de clientes. Toda operação confere a permissão do usuário
/// (ESCRITORIO_*) na empresa envolvida; as consultas de "todas as empresas" trazem só as empresas em que ele tem a função.
/// </summary>
public sealed class ServicoEscritorio
{
    private readonly BancoModulo _db;
    private readonly Autorizador _autorizador;

    public ServicoEscritorio(Npgsql.NpgsqlDataSource ds, Autorizador autorizador) => (_db, _autorizador) = (new BancoModulo(ds), autorizador);

    private Task<string?> NegarAsync(long usuario, long empresa, Permissao p, CancellationToken ct) => _autorizador.VerificarAsync(usuario, empresa, p, ct);

    // ------------------------------------------------------------------ tarefas

    /// <summary>Tarefas de uma empresa (ou de todas as permitidas, se <paramref name="empresaId"/> for nulo), opcionalmente só as em aberto.</summary>
    public Task<List<Tarefa>> ListarTarefasAsync(long? empresaId, bool somenteAbertas, long usuarioId, CancellationToken ct = default)
        => _db.ListarAsync($"""
            SELECT t.id, t.empresa_id, t.titulo, t.descricao, t.vencimento, t.responsavel_id, t.prioridade, t.situacao, t.recorrencia,
                   e.razao_social, u.nome, t.concluida_em
              FROM escritorio.tarefa t JOIN core.empresa e ON e.id = t.empresa_id LEFT JOIN core.usuario u ON u.id = t.responsavel_id
             WHERE (@e::bigint IS NULL OR t.empresa_id = @e) AND (NOT @a OR t.situacao IN ('PENDENTE', 'ANDAMENTO'))
               AND {BancoModulo.Permitida("t.empresa_id")}
             ORDER BY t.situacao IN ('CONCLUIDA', 'CANCELADA'), t.vencimento, t.prioridade, e.razao_social
            """, r => new Tarefa(r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.Texto(3), r.GetFieldValue<DateOnly>(4), r.Id(5), r.GetInt16(6),
                                 RegrasEscritorio.SituacaoTarefaDe(r.GetString(7)), RegrasEscritorio.RecorrenciaDe(r.GetString(8)), r.GetString(9), r.Texto(10),
                                 r.IsDBNull(11) ? null : r.GetFieldValue<DateTimeOffset>(11)),
            ct, ("e", empresaId), ("a", somenteAbertas), ("u", usuarioId), ("perm", Permissao.EscritorioTarefas.Codigo()));

    public Task<List<(long Id, string Nome)>> ListarUsuariosAsync(CancellationToken ct = default)
        => _db.ListarAsync("SELECT id, nome FROM core.usuario WHERE ativo ORDER BY nome", r => (r.GetInt64(0), r.GetString(1)), ct);

    public async Task<Resultado<long>> GravarTarefaAsync(Tarefa t, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, t.EmpresaId, Permissao.EscritorioTarefas, ct) is { } negado) return Resultado<long>.Falha(negado);
        if (string.IsNullOrWhiteSpace(t.Titulo)) return Resultado<long>.Falha("Informe o título da tarefa.");
        try
        {
            var id = t.Id == 0
                ? await _db.EscalarAsync<long>("""
                    INSERT INTO escritorio.tarefa (empresa_id, titulo, descricao, vencimento, responsavel_id, prioridade, situacao, recorrencia)
                    VALUES (@e, @t, @d, @v, @r, @p, @s, @rec) RETURNING id
                    """, ct, ("e", t.EmpresaId), ("t", t.Titulo.Trim()), ("d", t.Descricao), ("v", t.Vencimento), ("r", t.ResponsavelId),
                    ("p", (short)t.Prioridade), ("s", t.Situacao.Sql()), ("rec", t.Recorrencia.Sql()))
                : await _db.EscalarAsync<long>("""
                    UPDATE escritorio.tarefa SET titulo = @t, descricao = @d, vencimento = @v, responsavel_id = @r, prioridade = @p, situacao = @s, recorrencia = @rec
                     WHERE id = @id AND empresa_id = @e RETURNING id
                    """, ct, ("id", t.Id), ("e", t.EmpresaId), ("t", t.Titulo.Trim()), ("d", t.Descricao), ("v", t.Vencimento), ("r", t.ResponsavelId),
                    ("p", (short)t.Prioridade), ("s", t.Situacao.Sql()), ("rec", t.Recorrencia.Sql()));
            return Resultado<long>.Ok(id);
        }
        catch (ViolacaoRegraException ex) { return Resultado<long>.Falha(ex.Message); }
    }

    /// <summary>Conclui a tarefa; se for recorrente, cria a próxima (mesmo título e responsável, novo vencimento) e devolve o id dela.</summary>
    public async Task<Resultado<long?>> ConcluirTarefaAsync(Tarefa t, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, t.EmpresaId, Permissao.EscritorioTarefas, ct) is { } negado) return Resultado<long?>.Falha(negado);
        if (t.Situacao is SituacaoTarefa.Concluida or SituacaoTarefa.Cancelada) return Resultado<long?>.Falha("A tarefa já está encerrada.");
        await _db.ExecutarAsync("UPDATE escritorio.tarefa SET situacao = 'CONCLUIDA', concluida_em = now(), concluida_por = @u WHERE id = @id AND empresa_id = @e",
                                ct, ("u", usuarioId), ("id", t.Id), ("e", t.EmpresaId));
        if (RegrasEscritorio.Proxima(t.Vencimento, t.Recorrencia) is not { } prox) return Resultado<long?>.Ok(null);
        var nova = await GravarTarefaAsync(t with { Id = 0, Vencimento = prox, Situacao = SituacaoTarefa.Pendente }, usuarioId, ct);
        return nova.Sucesso ? Resultado<long?>.Ok(nova.Valor) : Resultado<long?>.Falha(nova.Erros);
    }

    public async Task<Resultado> ExcluirTarefaAsync(long empresaId, long id, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, empresaId, Permissao.EscritorioTarefas, ct) is { } negado) return Resultado.Falha(negado);
        await _db.ExecutarAsync("DELETE FROM escritorio.tarefa WHERE id = @id AND empresa_id = @e", ct, ("id", id), ("e", empresaId));
        return Resultado.Ok();
    }

    // ------------------------------------------------------------------ honorários

    public Task<List<ContratoHonorario>> ListarContratosAsync(long usuarioId, CancellationToken ct = default)
        => _db.ListarAsync($"""
            SELECT c.id, c.empresa_id, c.valor, c.dia_vencimento, c.inicio, c.fim, c.indice_reajuste, c.observacao, e.razao_social
              FROM escritorio.honorario_contrato c JOIN core.empresa e ON e.id = c.empresa_id
             WHERE {BancoModulo.Permitida("c.empresa_id")} ORDER BY e.razao_social
            """, r => new ContratoHonorario(r.GetInt64(0), r.GetInt64(1), r.GetDecimal(2), r.GetInt16(3), r.GetFieldValue<DateOnly>(4), r.Data(5),
                                            r.GetString(6), r.Texto(7), r.GetString(8)),
            ct, ("u", usuarioId), ("perm", Permissao.EscritorioHonorarios.Codigo()));

    public async Task<Resultado> GravarContratoAsync(ContratoHonorario c, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, c.EmpresaId, Permissao.EscritorioHonorarios, ct) is { } negado) return Resultado.Falha(negado);
        var erros = new List<string>();
        if (c.Valor <= 0) erros.Add("Informe o valor mensal dos honorários.");
        if (c.DiaVencimento is < 1 or > 31) erros.Add("Dia de vencimento: de 1 a 31.");
        if (c.Fim is { } f && f < c.Inicio) erros.Add("O fim do contrato não pode ser anterior ao início.");
        if (erros.Count > 0) return Resultado.Falha(erros);
        try
        {
            await _db.ExecutarAsync("""
                INSERT INTO escritorio.honorario_contrato (empresa_id, valor, dia_vencimento, inicio, fim, indice_reajuste, observacao)
                VALUES (@e, @v, @d, @i, @f, @ind, @o)
                ON CONFLICT (empresa_id) DO UPDATE SET valor = EXCLUDED.valor, dia_vencimento = EXCLUDED.dia_vencimento, inicio = EXCLUDED.inicio,
                       fim = EXCLUDED.fim, indice_reajuste = EXCLUDED.indice_reajuste, observacao = EXCLUDED.observacao
                """, ct, ("e", c.EmpresaId), ("v", c.Valor), ("d", (short)c.DiaVencimento), ("i", c.Inicio), ("f", c.Fim), ("ind", c.IndiceReajuste), ("o", c.Observacao));
            return Resultado.Ok();
        }
        catch (ViolacaoRegraException ex) { return Resultado.Falha(ex.Message); }
    }

    public Task<List<CobrancaHonorario>> ListarCobrancasAsync(DateOnly? competencia, bool somenteAbertas, long usuarioId, CancellationToken ct = default)
        => _db.ListarAsync($"""
            SELECT c.id, c.empresa_id, c.competencia, c.valor, c.vencimento, c.situacao, c.recebido_em, c.valor_recebido, e.razao_social
              FROM escritorio.honorario_cobranca c JOIN core.empresa e ON e.id = c.empresa_id
             WHERE (@c::date IS NULL OR c.competencia = @c) AND (NOT @a OR c.situacao = 'ABERTA') AND {BancoModulo.Permitida("c.empresa_id")}
             ORDER BY c.vencimento, e.razao_social
            """, r => new CobrancaHonorario(r.GetInt64(0), r.GetInt64(1), r.GetFieldValue<DateOnly>(2), r.GetDecimal(3), r.GetFieldValue<DateOnly>(4),
                                            RegrasEscritorio.SituacaoCobrancaDe(r.GetString(5)), r.Data(6), r.Valor(7), r.GetString(8)),
            ct, ("c", competencia is { } d ? RegrasEscritorio.Mes(d) : null), ("a", somenteAbertas), ("u", usuarioId), ("perm", Permissao.EscritorioHonorarios.Codigo()));

    /// <summary>Gera as cobranças da competência para todos os contratos vigentes das empresas permitidas; devolve quantas foram geradas.</summary>
    public async Task<Resultado<int>> GerarCobrancasAsync(DateOnly competencia, long usuarioId, CancellationToken ct = default)
    {
        competencia = RegrasEscritorio.Mes(competencia);
        var contratos = await ListarContratosAsync(usuarioId, ct);
        if (contratos.Count == 0) return Resultado<int>.Falha("Nenhum contrato de honorários cadastrado (ou seu perfil não tem a função de honorários).");
        var ja = (await ListarCobrancasAsync(competencia, false, usuarioId, ct)).Select(c => c.EmpresaId).ToHashSet();
        var novas = RegrasEscritorio.GerarCobrancas(contratos, competencia, ja);
        foreach (var n in novas)
            await _db.ExecutarAsync("INSERT INTO escritorio.honorario_cobranca (empresa_id, competencia, valor, vencimento) VALUES (@e, @c, @v, @venc) ON CONFLICT DO NOTHING",
                                    ct, ("e", n.EmpresaId), ("c", n.Competencia), ("v", n.Valor), ("venc", n.Vencimento));
        return Resultado<int>.Ok(novas.Count);
    }

    public async Task<Resultado> ReceberAsync(CobrancaHonorario c, DateOnly data, decimal valor, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, c.EmpresaId, Permissao.EscritorioHonorarios, ct) is { } negado) return Resultado.Falha(negado);
        if (c.Situacao != SituacaoCobranca.Aberta) return Resultado.Falha("Só cobranças em aberto podem ser recebidas.");
        if (valor <= 0) return Resultado.Falha("Informe o valor recebido.");
        await _db.ExecutarAsync("UPDATE escritorio.honorario_cobranca SET situacao = 'RECEBIDA', recebido_em = @d, valor_recebido = @v WHERE id = @id AND situacao = 'ABERTA'",
                                ct, ("d", data), ("v", valor), ("id", c.Id));
        return Resultado.Ok();
    }

    /// <summary>Volta a cobrança para "em aberto" (estorno do recebimento) ou cancela uma cobrança em aberto.</summary>
    public async Task<Resultado> AlterarSituacaoAsync(CobrancaHonorario c, SituacaoCobranca nova, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, c.EmpresaId, Permissao.EscritorioHonorarios, ct) is { } negado) return Resultado.Falha(negado);
        if (nova == SituacaoCobranca.Recebida) return Resultado.Falha("Use Receber.");
        await _db.ExecutarAsync("UPDATE escritorio.honorario_cobranca SET situacao = @s, recebido_em = NULL, valor_recebido = NULL WHERE id = @id",
                                ct, ("s", nova.Sql()), ("id", c.Id));
        return Resultado.Ok();
    }

    // ------------------------------------------------------------------ protocolos

    public async Task<List<Protocolo>> ListarProtocolosAsync(long empresaId, long usuarioId, CancellationToken ct = default)
        => await _autorizador.PodeVerEscritorioAsync(usuarioId, empresaId, ct)
            ? await _db.ListarAsync("""
                SELECT p.id, p.empresa_id, p.numero, p.data, p.tipo, p.documento, p.descricao, p.pessoa, u.nome
                  FROM escritorio.protocolo p LEFT JOIN core.usuario u ON u.id = p.usuario_id
                 WHERE p.empresa_id = @e ORDER BY p.numero DESC
                """, r => new Protocolo(r.GetInt64(0), r.GetInt64(1), r.GetInt32(2), r.GetFieldValue<DateOnly>(3),
                                        r.GetString(4) == "ENTREGA" ? TipoProtocolo.Entrega : TipoProtocolo.Recebimento, r.GetString(5), r.Texto(6), r.GetString(7), r.Texto(8)),
                ct, ("e", empresaId))
            : [];

    public async Task<Resultado<int>> IncluirProtocoloAsync(Protocolo p, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, p.EmpresaId, Permissao.EscritorioTarefas, ct) is { } negado) return Resultado<int>.Falha(negado);
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(p.Documento)) erros.Add("Informe o documento.");
        if (string.IsNullOrWhiteSpace(p.Pessoa)) erros.Add(p.Tipo == TipoProtocolo.Entrega ? "Informe quem recebeu." : "Informe quem entregou.");
        if (erros.Count > 0) return Resultado<int>.Falha(erros);
        var numero = await _db.EscalarAsync<int>("""
            INSERT INTO escritorio.protocolo (empresa_id, numero, data, tipo, documento, descricao, pessoa, usuario_id)
            VALUES (@e, (SELECT COALESCE(MAX(numero), 0) + 1 FROM escritorio.protocolo WHERE empresa_id = @e), @d, @t, @doc, @desc, @p, @u) RETURNING numero
            """, ct, ("e", p.EmpresaId), ("d", p.Data), ("t", p.Tipo == TipoProtocolo.Entrega ? "ENTREGA" : "RECEBIMENTO"), ("doc", p.Documento.Trim()),
            ("desc", p.Descricao), ("p", p.Pessoa.Trim()), ("u", usuarioId));
        return Resultado<int>.Ok(numero);
    }

    public async Task<Resultado> ExcluirProtocoloAsync(long empresaId, long id, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, empresaId, Permissao.EscritorioTarefas, ct) is { } negado) return Resultado.Falha(negado);
        await _db.ExecutarAsync("DELETE FROM escritorio.protocolo WHERE id = @id AND empresa_id = @e", ct, ("id", id), ("e", empresaId));
        return Resultado.Ok();
    }

    // ------------------------------------------------------------------ carteira

    /// <summary>Carteira de clientes: empresas permitidas ativas no Escritório, com tarefas, honorários e certificado mais próximo do vencimento.</summary>
    public Task<List<ClienteEscritorio>> CarteiraAsync(long usuarioId, DateOnly hoje, CancellationToken ct = default)
        => _db.ListarAsync($"""
            SELECT e.id, e.cnpj, e.razao_social,
                   (SELECT r.regime::text FROM core.empresa_regime r WHERE r.empresa_id = e.id ORDER BY r.vigencia_inicio DESC LIMIT 1),
                   (SELECT COUNT(*)::int FROM escritorio.tarefa t WHERE t.empresa_id = e.id AND t.situacao IN ('PENDENTE', 'ANDAMENTO')),
                   (SELECT COUNT(*)::int FROM escritorio.tarefa t WHERE t.empresa_id = e.id AND t.situacao IN ('PENDENTE', 'ANDAMENTO') AND t.vencimento < @h),
                   COALESCE((SELECT c.valor FROM escritorio.honorario_contrato c WHERE c.empresa_id = e.id AND (c.fim IS NULL OR c.fim >= @h)), 0),
                   COALESCE((SELECT SUM(c.valor) FROM escritorio.honorario_cobranca c WHERE c.empresa_id = e.id AND c.situacao = 'ABERTA'), 0),
                   (SELECT MIN(cd.valido_ate) FROM core.certificado_digital cd WHERE cd.empresa_id = e.id AND cd.valido_ate >= now()),
                   concat_ws(', ', CASE WHEN e.modulo_contabil THEN 'Contábil' END, CASE WHEN e.modulo_fiscal THEN 'Fiscal' END,
                             CASE WHEN e.modulo_folha THEN 'Folha' END, CASE WHEN e.modulo_erp THEN 'ERP' END, CASE WHEN e.modulo_condominio THEN 'Condomínio' END)
              FROM core.empresa e
             WHERE e.ativa AND e.modulo_escritorio AND {BancoModulo.Permitida("e.id")}
             ORDER BY e.razao_social
            """, r => new ClienteEscritorio(r.GetInt64(0), r.GetString(1).Trim(), r.GetString(2), r.Texto(3), r.GetInt32(4), r.GetInt32(5), r.GetDecimal(6),
                                            r.GetDecimal(7), r.IsDBNull(8) ? null : r.GetFieldValue<DateTimeOffset>(8), r.GetString(9)),
            ct, ("h", hoje), ("u", usuarioId), ("perm", Permissao.EscritorioRelatorios.Codigo()));
}

internal static class AutorizadorEscritorio
{
    private static readonly Permissao[] Todas = [Permissao.EscritorioTarefas, Permissao.EscritorioHonorarios, Permissao.EscritorioRelatorios];

    public static async Task<bool> PodeVerEscritorioAsync(this Autorizador a, long usuarioId, long empresaId, CancellationToken ct)
    {
        var p = await a.ObterAsync(usuarioId, empresaId, ct);
        return p.Administrador || Todas.Any(p.Pode);
    }
}

/// <summary>Uma instância do serviço por conexão (<see cref="Vorticis.Infra.VorticisApp"/>).</summary>
public static class ServicosEscritorio
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Vorticis.Infra.VorticisApp, ServicoEscritorio> Instancias = new();

    public static ServicoEscritorio De(Vorticis.Infra.VorticisApp app) => Instancias.GetValue(app, a => new ServicoEscritorio(a.DataSource, a.Autorizador));
}
