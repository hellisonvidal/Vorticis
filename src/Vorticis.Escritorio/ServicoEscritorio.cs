using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra.Postgres;

namespace Vorticis.Escritorio;

/// <summary>
/// Módulo Escritório: gerenciador do escritório contábil, com cadastros próprios (clientes e fornecedores do escritório, em
/// escritorio.pessoa), tarefas e prazos, honorários, protocolos, contratos e a carteira de clientes. Nada aqui é compartilhado com os
/// demais módulos: <c>escritorio</c> é a empresa ativa no módulo (o próprio escritório), dona dos dados. Toda operação confere a
/// permissão do usuário (ESCRITORIO_*) nessa empresa.
/// </summary>
public sealed class ServicoEscritorio
{
    private readonly BancoModulo _db;
    private readonly Autorizador _autorizador;
    private readonly Vorticis.Application.Cadastros.ServicoEmpresa? _empresas;

    public ServicoEscritorio(Npgsql.NpgsqlDataSource ds, Autorizador autorizador, Vorticis.Application.Cadastros.ServicoEmpresa? empresas = null)
        => (_db, _autorizador, _empresas) = (new BancoModulo(ds), autorizador, empresas);

    private Task<string?> NegarAsync(long usuario, long escritorio, Permissao p, CancellationToken ct) => _autorizador.VerificarAsync(usuario, escritorio, p, ct);

    private Task<bool> PodeVerAsync(long usuario, long escritorio, CancellationToken ct) => _autorizador.PodeVerEscritorioAsync(usuario, escritorio, ct);

    private static async Task<Resultado<T>> Tentar<T>(Func<Task<T>> acao)
    {
        try { return Resultado<T>.Ok(await acao()); }
        catch (ViolacaoRegraException ex) { return Resultado<T>.Falha(ex.Message); }
    }

    // ------------------------------------------------------------------ clientes e fornecedores do escritório

    public async Task<List<PessoaEscritorio>> ListarPessoasAsync(long escritorio, long usuarioId, CancellationToken ct = default)
        => await PodeVerAsync(usuarioId, escritorio, ct)
            ? await _db.ListarAsync("""
                SELECT p.id, p.codigo, p.cliente, p.fornecedor, p.nome, p.documento, p.regime, p.contato, p.email, p.telefone, p.endereco, p.municipio, p.uf, p.ativo,
                       p.observacao, p.nome_fantasia, p.natureza_juridica, p.cnae, p.inscricao_estadual, p.data_inicio_atividades, p.cep, p.logradouro, p.numero,
                       p.complemento, p.bairro, p.cod_municipio_ibge, p.empresa_vinculada_id,
                       (CASE WHEN e.modulo_contabil THEN 1 ELSE 0 END) | (CASE WHEN e.modulo_fiscal THEN 2 ELSE 0 END) | (CASE WHEN e.modulo_folha THEN 4 ELSE 0 END)
                     | (CASE WHEN e.modulo_erp THEN 16 ELSE 0 END) | (CASE WHEN e.modulo_condominio THEN 32 ELSE 0 END)
                     | (CASE WHEN e.modulo_frota THEN 64 ELSE 0 END)
                  FROM escritorio.pessoa p LEFT JOIN core.empresa e ON e.id = p.empresa_vinculada_id
                 WHERE p.empresa_id = @e ORDER BY p.nome
                """, r => new PessoaEscritorio(r.GetInt64(0), r.GetInt32(1), r.GetBoolean(2), r.GetBoolean(3), r.GetString(4), r.Texto(5), r.Texto(6), r.Texto(7),
                                               r.Texto(8), r.Texto(9), r.Texto(10), r.Texto(11), r.Texto(12)?.Trim(), r.GetBoolean(13), r.Texto(14),
                                               r.Texto(15), r.Texto(16)?.Trim(), r.Texto(17)?.Trim(), r.Texto(18), r.Data(19), r.Texto(20)?.Trim(), r.Texto(21), r.Texto(22),
                                               r.Texto(23), r.Texto(24), r.Texto(25)?.Trim(), r.IsDBNull(26) ? null : r.GetInt64(26),
                                               (Vorticis.Application.Cadastros.ModulosEmpresa)(r.IsDBNull(27) ? 0 : r.GetInt32(27))),
                ct, ("e", escritorio))
            : [];

    public async Task<Resultado<long>> GravarPessoaAsync(long escritorio, PessoaEscritorio p, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, escritorio, Permissao.EscritorioTarefas, ct) is { } negado) return Resultado<long>.Falha(negado);
        static string? N(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        static string? D(string? s) => N(new string((s ?? "").Where(char.IsAsciiDigit).ToArray()));
        p = p with
        {
            Nome = p.Nome?.Trim() ?? "", Documento = D(p.Documento), Regime = N(p.Regime), Contato = N(p.Contato),
            Email = N(p.Email), Telefone = N(p.Telefone), Endereco = N(p.Endereco), Municipio = N(p.Municipio), Uf = N(p.Uf)?.ToUpperInvariant(), Observacao = N(p.Observacao),
            NomeFantasia = N(p.NomeFantasia), NaturezaJuridica = D(p.NaturezaJuridica), Cnae = D(p.Cnae), InscricaoEstadual = N(p.InscricaoEstadual)?.ToUpperInvariant(),
            Cep = D(p.Cep), Logradouro = N(p.Logradouro), Numero = N(p.Numero), Complemento = N(p.Complemento), Bairro = N(p.Bairro), CodMunicipioIbge = D(p.CodMunicipioIbge),
        };
        // endereço de uma linha (relatórios e contratos) montado do endereço completo, quando informado
        if (p.Logradouro is not null)
            p = p with { Endereco = string.Join(", ", new[] { p.Logradouro, p.Numero, p.Complemento, p.Bairro }.Where(x => !string.IsNullOrWhiteSpace(x))) };
        var erros = RegrasEscritorio.Validar(p).Concat(RegrasEscritorio.ValidarParaModulos(p)).ToList();
        if (p.Modulos != Vorticis.Application.Cadastros.ModulosEmpresa.Nenhum && !p.Cliente) erros.Add("Só clientes são cadastrados nos módulos.");
        if (erros.Count > 0) return Resultado<long>.Falha(erros);
        (string, object?)[] campos =
        [
            ("e", escritorio), ("id", p.Id), ("cod", p.Codigo), ("cli", p.Cliente), ("for", p.Fornecedor), ("nome", p.Nome), ("doc", p.Documento), ("reg", p.Regime),
            ("con", p.Contato), ("email", p.Email), ("tel", p.Telefone), ("end", p.Endereco), ("mun", p.Municipio), ("uf", p.Uf), ("at", p.Ativo), ("obs", p.Observacao),
            ("fan", p.NomeFantasia), ("nat", p.NaturezaJuridica), ("cnae", p.Cnae), ("ie", p.InscricaoEstadual), ("ini", p.InicioAtividades), ("cep", p.Cep),
            ("lgr", p.Logradouro), ("num", p.Numero), ("cpl", p.Complemento), ("bai", p.Bairro), ("ibge", p.CodMunicipioIbge),
        ];
        var gravado = await Tentar(async () => p.Id == 0
            ? await _db.EscalarAsync<long>("""
                INSERT INTO escritorio.pessoa (empresa_id, codigo, cliente, fornecedor, nome, documento, regime, contato, email, telefone, endereco, municipio, uf, ativo, observacao,
                                               nome_fantasia, natureza_juridica, cnae, inscricao_estadual, data_inicio_atividades, cep, logradouro, numero, complemento, bairro,
                                               cod_municipio_ibge)
                VALUES (@e, @cod, @cli, @for, @nome, @doc, @reg, @con, @email, @tel, @end, @mun, @uf, @at, @obs, @fan, @nat, @cnae, @ie, @ini, @cep, @lgr, @num, @cpl, @bai, @ibge)
                RETURNING id
                """, ct, campos)
            : await _db.EscalarAsync<long?>("""
                UPDATE escritorio.pessoa SET cliente = @cli, fornecedor = @for, nome = @nome, documento = @doc, regime = @reg, contato = @con, email = @email,
                       telefone = @tel, endereco = @end, municipio = @mun, uf = @uf, ativo = @at, observacao = @obs, nome_fantasia = @fan, natureza_juridica = @nat,
                       cnae = @cnae, inscricao_estadual = @ie, data_inicio_atividades = @ini, cep = @cep, logradouro = @lgr, numero = @num, complemento = @cpl,
                       bairro = @bai, cod_municipio_ibge = @ibge
                 WHERE id = @id AND empresa_id = @e RETURNING id
                """, ct, campos) ?? throw new ViolacaoRegraException("Cadastro não encontrado."));
        if (!gravado.Sucesso || p.Modulos == Vorticis.Application.Cadastros.ModulosEmpresa.Nenhum) return gravado;
        var sinc = await SincronizarEmpresaAsync(escritorio, p with { Id = gravado.Valor }, usuarioId, ct);
        return sinc.Sucesso ? gravado
             : Resultado<long>.Falha(sinc.Erros.Prepend("O cliente foi gravado no Escritório, mas não foi cadastrado nos módulos:").ToList());
    }

    /// <summary>
    /// Cadastra (ou atualiza) o cliente como empresa nos módulos do contrato: cria a empresa com os dados do cliente ou, se já existe uma
    /// com o mesmo CNPJ (ou já vinculada), atualiza os dados e os módulos. Só o administrador cria empresas, como no cadastro de empresas.
    /// </summary>
    private async Task<Resultado> SincronizarEmpresaAsync(long escritorio, PessoaEscritorio p, long usuarioId, CancellationToken ct)
    {
        if (_empresas is null) return Resultado.Falha("Cadastro de empresas indisponível.");
        if (!(await _autorizador.ObterAsync(usuarioId, escritorio, ct)).Administrador)
            return Resultado.Falha("Só o administrador cadastra empresas nos módulos (Contábil, Fiscal, Folha, Condomínio, Gerencial).");
        var regime = RegrasEscritorio.RegimeDe(p.Regime)!.Value;
        var complemento = new Vorticis.Application.Cadastros.ComplementoEmpresa(p.Cep, p.Logradouro, p.Numero, p.Complemento, p.Bairro, p.Municipio, p.Email, p.Telefone,
                                                                              FonteDados: "Cadastro de clientes do Escritório");
        var vinculada = p.EmpresaVinculadaId
            ?? await _db.EscalarAsync<long?>("SELECT id FROM core.empresa WHERE cnpj = @c", ct, ("c", p.Documento));
        if (vinculada is { } id && await _empresas.ObterAsync(id, ct) is { } atual)
        {
            var r = await _empresas.AlterarAsync(new Vorticis.Application.Cadastros.AlteracaoEmpresa(id, p.Nome, p.NomeFantasia, p.NaturezaJuridica!, p.Cnae!, p.Uf!,
                p.CodMunicipioIbge!, p.InicioAtividades!.Value, p.InscricaoEstadual, complemento, atual.Nire, p.Modulos), ct);
            if (!r.Sucesso) return r;
        }
        else
        {
            var r = await _empresas.CadastrarAsync(new Vorticis.Application.Cadastros.NovaEmpresa(p.Documento!, p.Nome, p.NomeFantasia, p.NaturezaJuridica!, p.Cnae!, p.Uf!,
                p.CodMunicipioIbge!, p.InicioAtividades!.Value, regime, Math.Max(p.InicioAtividades.Value.Year, DateTime.Today.Year),
                ImportarPlanoModelo: p.Modulos.HasFlag(Vorticis.Application.Cadastros.ModulosEmpresa.Contabil), Complemento: complemento,
                InscricaoEstadual: p.InscricaoEstadual, Modulos: p.Modulos), ct);
            if (!r.Sucesso) return Resultado.Falha(r.Erros);
            vinculada = r.Valor;
        }
        await _db.ExecutarAsync("UPDATE escritorio.pessoa SET empresa_vinculada_id = @v WHERE id = @id AND empresa_id = @e", ct,
                                ("v", vinculada), ("id", p.Id), ("e", escritorio));
        return Resultado.Ok();
    }

    public async Task<Resultado> ExcluirPessoaAsync(long escritorio, long id, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, escritorio, Permissao.EscritorioTarefas, ct) is { } negado) return Resultado.Falha(negado);
        var r = await Tentar(() => _db.ExecutarAsync("DELETE FROM escritorio.pessoa WHERE id = @id AND empresa_id = @e", ct, ("id", id), ("e", escritorio)));
        return r.Sucesso ? Resultado.Ok() : Resultado.Falha("O cadastro tem tarefas, honorários, protocolos ou contratos: desmarque Ativo em vez de excluir.");
    }

    // ------------------------------------------------------------------ tarefas

    /// <summary>Tarefas do escritório (de um cliente ou de todos), opcionalmente só as em aberto.</summary>
    public async Task<List<Tarefa>> ListarTarefasAsync(long escritorio, long? pessoaId, bool somenteAbertas, long usuarioId, CancellationToken ct = default)
        => await PodeVerAsync(usuarioId, escritorio, ct)
            ? await _db.ListarAsync("""
                SELECT t.id, t.empresa_id, t.pessoa_id, t.titulo, t.descricao, t.vencimento, t.responsavel_id, t.prioridade, t.situacao, t.recorrencia,
                       p.nome, u.nome, t.concluida_em
                  FROM escritorio.tarefa t LEFT JOIN escritorio.pessoa p ON p.id = t.pessoa_id LEFT JOIN core.usuario u ON u.id = t.responsavel_id
                 WHERE t.empresa_id = @e AND (@p::bigint IS NULL OR t.pessoa_id = @p) AND (NOT @a OR t.situacao IN ('PENDENTE', 'ANDAMENTO'))
                 ORDER BY t.situacao IN ('CONCLUIDA', 'CANCELADA'), t.vencimento, t.prioridade, p.nome
                """, r => new Tarefa(r.GetInt64(0), r.GetInt64(1), r.Id(2), r.GetString(3), r.Texto(4), r.GetFieldValue<DateOnly>(5), r.Id(6), r.GetInt16(7),
                                     RegrasEscritorio.SituacaoTarefaDe(r.GetString(8)), RegrasEscritorio.RecorrenciaDe(r.GetString(9)), r.Texto(10), r.Texto(11),
                                     r.IsDBNull(12) ? null : r.GetFieldValue<DateTimeOffset>(12)),
                ct, ("e", escritorio), ("p", pessoaId), ("a", somenteAbertas))
            : [];

    public Task<List<(long Id, string Nome)>> ListarUsuariosAsync(CancellationToken ct = default)
        => _db.ListarAsync("SELECT id, nome FROM core.usuario WHERE ativo ORDER BY nome", r => (r.GetInt64(0), r.GetString(1)), ct);

    public async Task<Resultado<long>> GravarTarefaAsync(Tarefa t, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, t.EmpresaId, Permissao.EscritorioTarefas, ct) is { } negado) return Resultado<long>.Falha(negado);
        if (string.IsNullOrWhiteSpace(t.Titulo)) return Resultado<long>.Falha("Informe o título da tarefa.");
        if (t.PessoaId is { } pid && !await DoEscritorioAsync(t.EmpresaId, pid, ct)) return Resultado<long>.Falha("Cliente não encontrado no cadastro do escritório.");
        (string, object?)[] campos =
        [
            ("id", t.Id), ("e", t.EmpresaId), ("p", t.PessoaId), ("t", t.Titulo.Trim()), ("d", t.Descricao), ("v", t.Vencimento), ("r", t.ResponsavelId),
            ("pr", (short)t.Prioridade), ("s", t.Situacao.Sql()), ("rec", t.Recorrencia.Sql()),
        ];
        return await Tentar(async () => t.Id == 0
            ? await _db.EscalarAsync<long>("""
                INSERT INTO escritorio.tarefa (empresa_id, pessoa_id, titulo, descricao, vencimento, responsavel_id, prioridade, situacao, recorrencia)
                VALUES (@e, @p, @t, @d, @v, @r, @pr, @s, @rec) RETURNING id
                """, ct, campos)
            : await _db.EscalarAsync<long?>("""
                UPDATE escritorio.tarefa SET pessoa_id = @p, titulo = @t, descricao = @d, vencimento = @v, responsavel_id = @r, prioridade = @pr, situacao = @s, recorrencia = @rec
                 WHERE id = @id AND empresa_id = @e RETURNING id
                """, ct, campos) ?? throw new ViolacaoRegraException("Tarefa não encontrada."));
    }

    private async Task<bool> DoEscritorioAsync(long escritorio, long pessoaId, CancellationToken ct)
        => await _db.EscalarAsync<long?>("SELECT id FROM escritorio.pessoa WHERE id = @p AND empresa_id = @e", ct, ("p", pessoaId), ("e", escritorio)) is not null;

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

    public async Task<Resultado> ExcluirTarefaAsync(long escritorio, long id, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, escritorio, Permissao.EscritorioTarefas, ct) is { } negado) return Resultado.Falha(negado);
        await _db.ExecutarAsync("DELETE FROM escritorio.tarefa WHERE id = @id AND empresa_id = @e", ct, ("id", id), ("e", escritorio));
        return Resultado.Ok();
    }

    // ------------------------------------------------------------------ honorários

    public async Task<List<ContratoHonorario>> ListarHonorariosAsync(long escritorio, long usuarioId, CancellationToken ct = default)
        => await PodeVerAsync(usuarioId, escritorio, ct)
            ? await _db.ListarAsync("""
                SELECT c.id, c.pessoa_id, c.valor, c.dia_vencimento, c.inicio, c.fim, c.indice_reajuste, c.observacao, p.nome
                  FROM escritorio.honorario_contrato c JOIN escritorio.pessoa p ON p.id = c.pessoa_id
                 WHERE p.empresa_id = @e ORDER BY p.nome
                """, r => new ContratoHonorario(r.GetInt64(0), r.GetInt64(1), r.GetDecimal(2), r.GetInt16(3), r.GetFieldValue<DateOnly>(4), r.Data(5),
                                                r.GetString(6), r.Texto(7), r.GetString(8)),
                ct, ("e", escritorio))
            : [];

    public async Task<Resultado> GravarHonorarioAsync(long escritorio, ContratoHonorario c, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, escritorio, Permissao.EscritorioHonorarios, ct) is { } negado) return Resultado.Falha(negado);
        var erros = new List<string>();
        if (!await DoEscritorioAsync(escritorio, c.PessoaId, ct)) erros.Add("Escolha o cliente.");
        if (c.Valor <= 0) erros.Add("Informe o valor mensal dos honorários.");
        if (c.DiaVencimento is < 1 or > 31) erros.Add("Dia de vencimento: de 1 a 31.");
        if (c.Fim is { } f && f < c.Inicio) erros.Add("O fim do contrato não pode ser anterior ao início.");
        if (erros.Count > 0) return Resultado.Falha(erros);
        var r = await Tentar(() => _db.ExecutarAsync("""
            INSERT INTO escritorio.honorario_contrato (empresa_id, pessoa_id, valor, dia_vencimento, inicio, fim, indice_reajuste, observacao)
            VALUES (@e, @p, @v, @d, @i, @f, @ind, @o)
            ON CONFLICT (pessoa_id) DO UPDATE SET valor = EXCLUDED.valor, dia_vencimento = EXCLUDED.dia_vencimento, inicio = EXCLUDED.inicio,
                   fim = EXCLUDED.fim, indice_reajuste = EXCLUDED.indice_reajuste, observacao = EXCLUDED.observacao
            """, ct, ("e", escritorio), ("p", c.PessoaId), ("v", c.Valor), ("d", (short)c.DiaVencimento), ("i", c.Inicio), ("f", c.Fim), ("ind", c.IndiceReajuste),
            ("o", c.Observacao)));
        return r.Sucesso ? Resultado.Ok() : Resultado.Falha(r.Erros);
    }

    public async Task<List<CobrancaHonorario>> ListarCobrancasAsync(long escritorio, DateOnly? competencia, bool somenteAbertas, long usuarioId, CancellationToken ct = default,
                                                                    DateOnly? ate = null)
        => await PodeVerAsync(usuarioId, escritorio, ct)
            ? await _db.ListarAsync("""
                SELECT c.id, c.pessoa_id, c.competencia, c.valor, c.vencimento, c.situacao, c.recebido_em, c.valor_recebido, p.nome
                  FROM escritorio.honorario_cobranca c JOIN escritorio.pessoa p ON p.id = c.pessoa_id
                 WHERE p.empresa_id = @e AND (@c::date IS NULL OR (CASE WHEN @ate::date IS NULL THEN c.competencia = @c ELSE c.competencia BETWEEN @c AND @ate END))
                   AND (NOT @a OR c.situacao = 'ABERTA')
                 ORDER BY c.vencimento, p.nome
                """, r => new CobrancaHonorario(r.GetInt64(0), r.GetInt64(1), r.GetFieldValue<DateOnly>(2), r.GetDecimal(3), r.GetFieldValue<DateOnly>(4),
                                                RegrasEscritorio.SituacaoCobrancaDe(r.GetString(5)), r.Data(6), r.Valor(7), r.GetString(8)),
                ct, ("e", escritorio), ("c", competencia is { } d ? RegrasEscritorio.Mes(d) : null), ("ate", ate is { } x ? RegrasEscritorio.Mes(x) : null), ("a", somenteAbertas))
            : [];

    /// <summary>Gera as cobranças da competência para todos os contratos de honorários vigentes; devolve quantas foram geradas.</summary>
    public async Task<Resultado<int>> GerarCobrancasAsync(long escritorio, DateOnly competencia, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, escritorio, Permissao.EscritorioHonorarios, ct) is { } negado) return Resultado<int>.Falha(negado);
        competencia = RegrasEscritorio.Mes(competencia);
        var contratos = await ListarHonorariosAsync(escritorio, usuarioId, ct);
        if (contratos.Count == 0) return Resultado<int>.Falha("Nenhum contrato de honorários cadastrado.");
        var ja = (await ListarCobrancasAsync(escritorio, competencia, false, usuarioId, ct)).Select(c => c.PessoaId).ToHashSet();
        var novas = RegrasEscritorio.GerarCobrancas(contratos, competencia, ja);
        foreach (var n in novas)
            await _db.ExecutarAsync("""
                INSERT INTO escritorio.honorario_cobranca (empresa_id, pessoa_id, competencia, valor, vencimento) VALUES (@e, @p, @c, @v, @venc) ON CONFLICT DO NOTHING
                """, ct, ("e", escritorio), ("p", n.PessoaId), ("c", n.Competencia), ("v", n.Valor), ("venc", n.Vencimento));
        return Resultado<int>.Ok(novas.Count);
    }

    public async Task<Resultado> ReceberAsync(long escritorio, CobrancaHonorario c, DateOnly data, decimal valor, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, escritorio, Permissao.EscritorioHonorarios, ct) is { } negado) return Resultado.Falha(negado);
        if (c.Situacao != SituacaoCobranca.Aberta) return Resultado.Falha("Só cobranças em aberto podem ser recebidas.");
        if (valor <= 0) return Resultado.Falha("Informe o valor recebido.");
        await _db.ExecutarAsync("""
            UPDATE escritorio.honorario_cobranca SET situacao = 'RECEBIDA', recebido_em = @d, valor_recebido = @v WHERE id = @id AND empresa_id = @e AND situacao = 'ABERTA'
            """, ct, ("d", data), ("v", valor), ("id", c.Id), ("e", escritorio));
        return Resultado.Ok();
    }

    /// <summary>Volta a cobrança para "em aberto" (estorno do recebimento) ou cancela uma cobrança em aberto.</summary>
    public async Task<Resultado> AlterarSituacaoAsync(long escritorio, CobrancaHonorario c, SituacaoCobranca nova, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, escritorio, Permissao.EscritorioHonorarios, ct) is { } negado) return Resultado.Falha(negado);
        if (nova == SituacaoCobranca.Recebida) return Resultado.Falha("Use Receber.");
        await _db.ExecutarAsync("UPDATE escritorio.honorario_cobranca SET situacao = @s, recebido_em = NULL, valor_recebido = NULL WHERE id = @id AND empresa_id = @e",
                                ct, ("s", nova.Sql()), ("id", c.Id), ("e", escritorio));
        return Resultado.Ok();
    }

    // ------------------------------------------------------------------ protocolos

    public async Task<List<Protocolo>> ListarProtocolosAsync(long escritorio, long usuarioId, CancellationToken ct = default)
        => await PodeVerAsync(usuarioId, escritorio, ct)
            ? await _db.ListarAsync("""
                SELECT p.id, p.empresa_id, p.pessoa_id, p.numero, p.data, p.tipo, p.documento, p.descricao, p.pessoa, u.nome, c.nome
                  FROM escritorio.protocolo p LEFT JOIN core.usuario u ON u.id = p.usuario_id LEFT JOIN escritorio.pessoa c ON c.id = p.pessoa_id
                 WHERE p.empresa_id = @e ORDER BY p.numero DESC
                """, r => new Protocolo(r.GetInt64(0), r.GetInt64(1), r.Id(2), r.GetInt32(3), r.GetFieldValue<DateOnly>(4),
                                        r.GetString(5) == "ENTREGA" ? TipoProtocolo.Entrega : TipoProtocolo.Recebimento, r.GetString(6), r.Texto(7), r.GetString(8),
                                        r.Texto(9), r.Texto(10)),
                ct, ("e", escritorio))
            : [];

    public async Task<Resultado<int>> IncluirProtocoloAsync(Protocolo p, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, p.EmpresaId, Permissao.EscritorioTarefas, ct) is { } negado) return Resultado<int>.Falha(negado);
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(p.Documento)) erros.Add("Informe o documento.");
        if (string.IsNullOrWhiteSpace(p.Pessoa)) erros.Add(p.Tipo == TipoProtocolo.Entrega ? "Informe quem recebeu." : "Informe quem entregou.");
        if (p.PessoaId is { } pid && !await DoEscritorioAsync(p.EmpresaId, pid, ct)) erros.Add("Cliente não encontrado no cadastro do escritório.");
        if (erros.Count > 0) return Resultado<int>.Falha(erros);
        var numero = await _db.EscalarAsync<int>("""
            INSERT INTO escritorio.protocolo (empresa_id, pessoa_id, numero, data, tipo, documento, descricao, pessoa, usuario_id)
            VALUES (@e, @pid, (SELECT COALESCE(MAX(numero), 0) + 1 FROM escritorio.protocolo WHERE empresa_id = @e), @d, @t, @doc, @desc, @p, @u) RETURNING numero
            """, ct, ("e", p.EmpresaId), ("pid", p.PessoaId), ("d", p.Data), ("t", p.Tipo == TipoProtocolo.Entrega ? "ENTREGA" : "RECEBIMENTO"), ("doc", p.Documento.Trim()),
            ("desc", p.Descricao), ("p", p.Pessoa.Trim()), ("u", usuarioId));
        return Resultado<int>.Ok(numero);
    }

    public async Task<Resultado> ExcluirProtocoloAsync(long escritorio, long id, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, escritorio, Permissao.EscritorioTarefas, ct) is { } negado) return Resultado.Falha(negado);
        await _db.ExecutarAsync("DELETE FROM escritorio.protocolo WHERE id = @id AND empresa_id = @e", ct, ("id", id), ("e", escritorio));
        return Resultado.Ok();
    }

    // ------------------------------------------------------------------ contratos (gerador)

    /// <summary>Dados do escritório (a empresa ativa) para a qualificação no contrato.</summary>
    public async Task<DadosEscritorio> DadosEscritorioAsync(long escritorio, CancellationToken ct = default)
        => (await _db.ListarAsync("""
            SELECT razao_social, btrim(cnpj), concat_ws(', ', NULLIF(logradouro, ''), NULLIF(numero, ''), NULLIF(bairro, '')), municipio, uf FROM core.empresa WHERE id = @e
            """, r => new DadosEscritorio(r.GetString(0), r.GetString(1), r.Texto(2) is { Length: > 0 } e ? e : null, r.Texto(3), r.Texto(4)?.Trim()), ct, ("e", escritorio)))
           .FirstOrDefault() ?? new DadosEscritorio("", "", null, null, null);

    public async Task<List<ContratoEscritorio>> ListarContratosAsync(long escritorio, long usuarioId, CancellationToken ct = default)
        => await PodeVerAsync(usuarioId, escritorio, ct)
            ? await _db.ListarAsync("""
                SELECT c.id, c.numero, c.pessoa_id, c.modelo, c.objeto, c.valor, c.periodicidade, c.dia_vencimento, c.inicio, c.fim, c.indice_reajuste, c.foro, c.texto,
                       c.situacao = 'ATIVO', p.nome
                  FROM escritorio.contrato c JOIN escritorio.pessoa p ON p.id = c.pessoa_id
                 WHERE c.empresa_id = @e ORDER BY c.numero DESC
                """, r => new ContratoEscritorio(r.GetInt64(0), r.GetInt32(1), r.GetInt64(2), r.GetString(3) == "CLIENTE" ? ModeloContrato.Cliente : ModeloContrato.Fornecedor,
                                                 r.GetString(4), r.GetDecimal(5), RegrasEscritorio.PeriodicidadeDe(r.GetString(6)), r.IsDBNull(7) ? null : r.GetInt16(7),
                                                 r.GetFieldValue<DateOnly>(8), r.Data(9), r.GetString(10), r.Texto(11), r.GetString(12), r.GetBoolean(13), r.GetString(14)),
                ct, ("e", escritorio))
            : [];

    /// <summary>Grava o contrato (o número é o próximo do escritório na inclusão) e devolve o número.</summary>
    public async Task<Resultado<int>> GravarContratoAsync(long escritorio, ContratoEscritorio c, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, escritorio, Permissao.EscritorioHonorarios, ct) is { } negado) return Resultado<int>.Falha(negado);
        var erros = RegrasEscritorio.Validar(c).ToList();
        var pessoa = (await ListarPessoasAsync(escritorio, usuarioId, ct)).FirstOrDefault(p => p.Id == c.PessoaId);
        if (c.PessoaId > 0 && pessoa is null) erros.Add("Cadastro não encontrado no escritório.");
        else if (pessoa is not null && (c.Modelo == ModeloContrato.Cliente ? !pessoa.Cliente : !pessoa.Fornecedor))
            erros.Add(c.Modelo == ModeloContrato.Cliente ? $"{pessoa.Nome} não está marcado como cliente." : $"{pessoa.Nome} não está marcado como fornecedor.");
        if (erros.Count > 0) return Resultado<int>.Falha(erros);
        (string, object?)[] campos =
        [
            ("e", escritorio), ("id", c.Id), ("p", c.PessoaId), ("m", c.Modelo == ModeloContrato.Cliente ? "CLIENTE" : "FORNECEDOR"), ("obj", c.Objeto.Trim()), ("v", c.Valor),
            ("per", c.Periodicidade.Sql()), ("dia", c.DiaVencimento is { } d ? (short)d : null), ("i", c.Inicio), ("f", c.Fim), ("ind", c.IndiceReajuste),
            ("foro", string.IsNullOrWhiteSpace(c.Foro) ? null : c.Foro.Trim()), ("txt", c.Texto), ("sit", c.Ativo ? "ATIVO" : "ENCERRADO"),
        ];
        return await Tentar(async () => c.Id == 0
            ? await _db.EscalarAsync<int>("""
                INSERT INTO escritorio.contrato (empresa_id, numero, pessoa_id, modelo, objeto, valor, periodicidade, dia_vencimento, inicio, fim, indice_reajuste, foro, texto, situacao)
                VALUES (@e, (SELECT COALESCE(MAX(numero), 0) + 1 FROM escritorio.contrato WHERE empresa_id = @e), @p, @m, @obj, @v, @per, @dia, @i, @f, @ind, @foro, @txt, @sit)
                RETURNING numero
                """, ct, campos)
            : await _db.EscalarAsync<int?>("""
                UPDATE escritorio.contrato SET pessoa_id = @p, modelo = @m, objeto = @obj, valor = @v, periodicidade = @per, dia_vencimento = @dia, inicio = @i, fim = @f,
                       indice_reajuste = @ind, foro = @foro, texto = @txt, situacao = @sit
                 WHERE id = @id AND empresa_id = @e RETURNING numero
                """, ct, campos) ?? throw new ViolacaoRegraException("Contrato não encontrado."));
    }

    public async Task<Resultado> ExcluirContratoAsync(long escritorio, long id, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(usuarioId, escritorio, Permissao.EscritorioHonorarios, ct) is { } negado) return Resultado.Falha(negado);
        await _db.ExecutarAsync("DELETE FROM escritorio.contrato WHERE id = @id AND empresa_id = @e", ct, ("id", id), ("e", escritorio));
        return Resultado.Ok();
    }

    // ------------------------------------------------------------------ carteira

    /// <summary>Carteira: clientes ativos do cadastro próprio do escritório, com tarefas e honorários.</summary>
    public async Task<List<ClienteEscritorio>> CarteiraAsync(long escritorio, long usuarioId, DateOnly hoje, CancellationToken ct = default)
        => await PodeVerAsync(usuarioId, escritorio, ct)
            ? await _db.ListarAsync("""
                SELECT p.id, p.codigo, p.nome, p.documento, p.regime,
                       (SELECT COUNT(*)::int FROM escritorio.tarefa t WHERE t.pessoa_id = p.id AND t.situacao IN ('PENDENTE', 'ANDAMENTO')),
                       (SELECT COUNT(*)::int FROM escritorio.tarefa t WHERE t.pessoa_id = p.id AND t.situacao IN ('PENDENTE', 'ANDAMENTO') AND t.vencimento < @h),
                       COALESCE((SELECT c.valor FROM escritorio.honorario_contrato c WHERE c.pessoa_id = p.id AND (c.fim IS NULL OR c.fim >= @h)), 0),
                       COALESCE((SELECT SUM(c.valor) FROM escritorio.honorario_cobranca c WHERE c.pessoa_id = p.id AND c.situacao = 'ABERTA'), 0),
                       concat_ws(' · ', p.contato, p.telefone, p.email)
                  FROM escritorio.pessoa p
                 WHERE p.empresa_id = @e AND p.cliente AND p.ativo
                 ORDER BY p.nome
                """, r => new ClienteEscritorio(r.GetInt64(0), r.GetInt32(1), r.GetString(2), r.Texto(3), r.Texto(4), r.GetInt32(5), r.GetInt32(6), r.GetDecimal(7),
                                                r.GetDecimal(8), r.Texto(9)),
                ct, ("e", escritorio), ("h", hoje))
            : [];
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

    public static ServicoEscritorio De(Vorticis.Infra.VorticisApp app) => Instancias.GetValue(app, a => new ServicoEscritorio(a.DataSource, a.Autorizador, a.Empresas));
}
