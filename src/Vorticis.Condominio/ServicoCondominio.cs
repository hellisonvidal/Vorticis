using System.Text.Json;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra;
using Vorticis.Infra.Postgres;
using P = Vorticis.Domain.Seguranca.Permissao;

namespace Vorticis.Condominio;

/// <summary>
/// Módulo Condomínio (a empresa ativa é o condomínio). Cadastros com CONDOMINIO_CADASTROS, cobranças e despesas com CONDOMINIO_FINANCEIRO,
/// prestação de contas e inadimplência com CONDOMINIO_RELATORIOS. Condôminos são participantes (cadastro comum).
/// </summary>
public sealed class ServicoCondominio(VorticisApp app)
{
    private readonly BancoModulo _db = new(app.DataSource);
    private readonly Autorizador _aut = app.Autorizador;

    private Task<string?> Negar(long u, long e, P p, CancellationToken ct) => _aut.VerificarAsync(u, e, p, ct);

    private async Task<bool> PodeVerAsync(long u, long e, CancellationToken ct)
    {
        var p = await _aut.ObterAsync(u, e, ct);
        return p.Administrador || new[] { P.CondominioCadastros, P.CondominioFinanceiro, P.CondominioRelatorios }.Any(p.Pode);
    }

    private static async Task<Resultado> Tentar(Func<Task> acao)
    {
        try { await acao(); return Resultado.Ok(); }
        catch (ViolacaoRegraException ex) { return Resultado.Falha(ex.Message); }
    }

    // ------------------------------------------------------------------ configuração

    public async Task<ConfiguracaoCondominio> ConfiguracaoAsync(long empresa, CancellationToken ct = default)
        => (await _db.ListarAsync("SELECT dia_vencimento, multa_percentual, juros_mensal FROM condominio.configuracao WHERE empresa_id = @e",
                                  r => new ConfiguracaoCondominio(r.GetInt16(0), r.GetDecimal(1), r.GetDecimal(2)), ct, ("e", empresa))).FirstOrDefault()
           ?? new ConfiguracaoCondominio();

    /// <summary>Multa máxima legal (core.parametro_legal CONDOMINIO_ENCARGOS).</summary>
    public async Task<decimal> MultaMaximaAsync(DateOnly data, CancellationToken ct = default)
    {
        var json = await _db.EscalarAsync<string>("""
            SELECT valor::text FROM core.parametro_legal WHERE chave = 'CONDOMINIO_ENCARGOS' AND vigencia_inicio <= @d AND (vigencia_fim IS NULL OR vigencia_fim >= @d)
             ORDER BY vigencia_inicio DESC LIMIT 1
            """, ct, ("d", data));
        return json is null ? 2m : JsonDocument.Parse(json).RootElement.GetProperty("multaMaxima").GetDecimal();
    }

    public async Task<Resultado> GravarConfiguracaoAsync(long empresa, ConfiguracaoCondominio c, long usuario, CancellationToken ct = default)
    {
        if (await Negar(usuario, empresa, P.CondominioFinanceiro, ct) is { } n) return Resultado.Falha(n);
        var max = await MultaMaximaAsync(DateOnly.FromDateTime(DateTime.Today), ct);
        if (c.MultaPercentual > max) return Resultado.Falha($"A multa por atraso não pode passar de {Formato.Num(max)}% (Código Civil, art. 1.336, § 1º).");
        if (c.DiaVencimento is < 1 or > 28) return Resultado.Falha("Dia de vencimento: de 1 a 28.");
        if (c.JurosMensal < 0) return Resultado.Falha("Os juros não podem ser negativos.");
        return await Tentar(() => _db.ExecutarAsync("""
            INSERT INTO condominio.configuracao (empresa_id, dia_vencimento, multa_percentual, juros_mensal) VALUES (@e, @d, @m, @j)
            ON CONFLICT (empresa_id) DO UPDATE SET dia_vencimento = EXCLUDED.dia_vencimento, multa_percentual = EXCLUDED.multa_percentual, juros_mensal = EXCLUDED.juros_mensal
            """, ct, ("e", empresa), ("d", (short)c.DiaVencimento), ("m", c.MultaPercentual), ("j", c.JurosMensal)));
    }

    // ------------------------------------------------------------------ unidades e condôminos

    public async Task<List<(long Id, string Nome)>> CondominosAsync(long empresa, long usuario, CancellationToken ct = default)
        => await PodeVerAsync(usuario, empresa, ct)
            ? await _db.ListarAsync("SELECT id, nome FROM core.participante WHERE empresa_id = @e AND ativo ORDER BY nome", r => (r.GetInt64(0), r.GetString(1)), ct, ("e", empresa))
            : [];

    public async Task<List<Unidade>> UnidadesAsync(long empresa, long usuario, CancellationToken ct = default)
        => await PodeVerAsync(usuario, empresa, ct) ? await ListarUnidadesAsync(empresa, ct) : [];

    private Task<List<Unidade>> ListarUnidadesAsync(long empresa, CancellationToken ct)
        => _db.ListarAsync("""
            SELECT u.id, u.bloco, u.numero, u.tipo, u.fracao_ideal, u.area_m2, u.proprietario_id, u.inquilino_id, u.cobrar_inquilino, u.ativa, p.nome, i.nome
              FROM condominio.unidade u LEFT JOIN core.participante p ON p.id = u.proprietario_id LEFT JOIN core.participante i ON i.id = u.inquilino_id
             WHERE u.empresa_id = @e ORDER BY u.bloco, lpad(u.numero, 10, '0')
            """, r => new Unidade(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetDecimal(4), r.Valor(5), r.Id(6), r.Id(7), r.GetBoolean(8),
                                  r.GetBoolean(9), r.Texto(10), r.Texto(11)), ct, ("e", empresa));

    public async Task<Resultado> GravarUnidadeAsync(long empresa, Unidade u, long usuario, CancellationToken ct = default)
    {
        if (await Negar(usuario, empresa, P.CondominioCadastros, ct) is { } n) return Resultado.Falha(n);
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(u.Numero)) erros.Add("Informe o número da unidade.");
        if (u.FracaoIdeal <= 0) erros.Add("Informe a fração ideal (maior que zero).");
        if (u.CobrarInquilino && u.InquilinoId is null) erros.Add("Para cobrar o inquilino, escolha o inquilino.");
        if (erros.Count > 0) return Resultado.Falha(erros);
        return await Tentar(() => _db.ExecutarAsync(u.Id == 0
            ? """
              INSERT INTO condominio.unidade (empresa_id, bloco, numero, tipo, fracao_ideal, area_m2, proprietario_id, inquilino_id, cobrar_inquilino, ativa)
              VALUES (@e, @b, @n, @t, @f, @a, @p, @i, @c, @at)
              """
            : """
              UPDATE condominio.unidade SET bloco = @b, numero = @n, tipo = @t, fracao_ideal = @f, area_m2 = @a, proprietario_id = @p, inquilino_id = @i,
                     cobrar_inquilino = @c, ativa = @at WHERE id = @id AND empresa_id = @e
              """, ct, ("e", empresa), ("b", u.Bloco.Trim()), ("n", u.Numero.Trim()), ("t", u.Tipo), ("f", u.FracaoIdeal), ("a", u.AreaM2), ("p", u.ProprietarioId),
            ("i", u.InquilinoId), ("c", u.CobrarInquilino), ("at", u.Ativa), ("id", u.Id)));
    }

    // ------------------------------------------------------------------ taxas

    public async Task<List<Taxa>> TaxasAsync(long empresa, long usuario, CancellationToken ct = default)
        => await PodeVerAsync(usuario, empresa, ct) ? await ListarTaxasAsync(empresa, ct) : [];

    private Task<List<Taxa>> ListarTaxasAsync(long empresa, CancellationToken ct)
        => _db.ListarAsync("SELECT id, descricao, rateio, valor, inicio, fim, ativa FROM condominio.taxa WHERE empresa_id = @e ORDER BY ativa DESC, descricao",
            r => new Taxa(r.GetInt64(0), r.GetString(1), RegrasCondominio.RateioDe(r.GetString(2)), r.GetDecimal(3), r.GetFieldValue<DateOnly>(4), r.Data(5), r.GetBoolean(6)),
            ct, ("e", empresa));

    public async Task<Resultado> GravarTaxaAsync(long empresa, Taxa t, long usuario, CancellationToken ct = default)
    {
        if (await Negar(usuario, empresa, P.CondominioFinanceiro, ct) is { } n) return Resultado.Falha(n);
        if (string.IsNullOrWhiteSpace(t.Descricao)) return Resultado.Falha("Informe a descrição da taxa.");
        if (t.Valor <= 0) return Resultado.Falha("Informe o valor da taxa.");
        if (t.Fim is { } f && f < t.Inicio) return Resultado.Falha("O fim não pode ser anterior ao início.");
        return await Tentar(() => _db.ExecutarAsync(t.Id == 0
            ? "INSERT INTO condominio.taxa (empresa_id, descricao, rateio, valor, inicio, fim, ativa) VALUES (@e, @d, @r, @v, @i, @f, @a)"
            : "UPDATE condominio.taxa SET descricao = @d, rateio = @r, valor = @v, inicio = @i, fim = @f, ativa = @a WHERE id = @id AND empresa_id = @e",
            ct, ("e", empresa), ("d", t.Descricao.Trim()), ("r", t.Rateio.Sql()), ("v", t.Valor), ("i", t.Inicio), ("f", t.Fim), ("a", t.Ativa), ("id", t.Id)));
    }

    // ------------------------------------------------------------------ cobranças

    public async Task<List<Cobranca>> CobrancasAsync(long empresa, DateOnly? competencia, bool somenteAbertas, long usuario, CancellationToken ct = default)
    {
        if (!await PodeVerAsync(usuario, empresa, ct)) return [];
        var itens = (await _db.ListarAsync("""
            SELECT i.cobranca_id, i.taxa_id, i.descricao, i.valor FROM condominio.cobranca_item i JOIN condominio.cobranca c ON c.id = i.cobranca_id
             WHERE c.empresa_id = @e AND (@c::date IS NULL OR c.competencia = @c) ORDER BY i.id
            """, r => (Id: r.GetInt64(0), Item: new ItemCobranca(r.Id(1), r.GetString(2), r.GetDecimal(3))), ct, ("e", empresa), ("c", competencia)))
            .ToLookup(x => x.Id, x => x.Item);
        return await _db.ListarAsync("""
            SELECT c.id, c.unidade_id, CASE WHEN u.bloco = '' THEN u.numero ELSE u.bloco || '-' || u.numero END, c.competencia, c.vencimento, c.valor, c.situacao, c.pago_em,
                   c.multa, c.juros, c.desconto, c.valor_pago, CASE WHEN u.cobrar_inquilino THEN i.nome ELSE p.nome END
              FROM condominio.cobranca c JOIN condominio.unidade u ON u.id = c.unidade_id
              LEFT JOIN core.participante p ON p.id = u.proprietario_id LEFT JOIN core.participante i ON i.id = u.inquilino_id
             WHERE c.empresa_id = @e AND (@c::date IS NULL OR c.competencia = @c) AND (NOT @a OR c.situacao = 'ABERTA')
             ORDER BY c.competencia, u.bloco, lpad(u.numero, 10, '0')
            """, r => new Cobranca(r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetFieldValue<DateOnly>(3), r.GetFieldValue<DateOnly>(4), r.GetDecimal(5), r.GetString(6),
                                   r.Data(7), r.GetDecimal(8), r.GetDecimal(9), r.GetDecimal(10), r.Valor(11), r.Texto(12), [.. itens[r.GetInt64(0)]]),
            ct, ("e", empresa), ("c", competencia), ("a", somenteAbertas));
    }

    /// <summary>Gera as cobranças da competência para as unidades ativas que ainda não têm cobrança nela; devolve quantas foram geradas.</summary>
    public async Task<Resultado<int>> GerarCobrancasAsync(long empresa, DateOnly competencia, long usuario, CancellationToken ct = default)
    {
        if (await Negar(usuario, empresa, P.CondominioFinanceiro, ct) is { } n) return Resultado<int>.Falha(n);
        competencia = RegrasCondominio.Mes(competencia);
        var unidades = await ListarUnidadesAsync(empresa, ct);
        if (unidades.Count(u => u.Ativa) == 0) return Resultado<int>.Falha("Cadastre as unidades antes de gerar as cobranças.");
        var taxas = await ListarTaxasAsync(empresa, ct);
        var composicao = RegrasCondominio.Compor(taxas, unidades, competencia);
        if (composicao.Values.All(l => l.Count == 0)) return Resultado<int>.Falha("Nenhuma taxa vigente nesta competência. Cadastre as taxas.");
        var ja = (await _db.ListarAsync("SELECT unidade_id FROM condominio.cobranca WHERE empresa_id = @e AND competencia = @c", r => r.GetInt64(0), ct,
                                        ("e", empresa), ("c", competencia))).ToHashSet();
        var cfg = await ConfiguracaoAsync(empresa, ct);
        var venc = RegrasCondominio.Vencimento(competencia, cfg.DiaVencimento);
        var geradas = 0;
        foreach (var (unidade, itens) in composicao.Where(c => !ja.Contains(c.Key) && c.Value.Sum(i => i.Valor) > 0))
        {
            var id = await _db.EscalarAsync<long>("INSERT INTO condominio.cobranca (empresa_id, unidade_id, competencia, vencimento, valor) VALUES (@e, @u, @c, @v, @val) RETURNING id",
                                                  ct, ("e", empresa), ("u", unidade), ("c", competencia), ("v", venc), ("val", itens.Sum(i => i.Valor)));
            foreach (var i in itens)
                await _db.ExecutarAsync("INSERT INTO condominio.cobranca_item (cobranca_id, taxa_id, descricao, valor) VALUES (@c, @t, @d, @v)",
                                        ct, ("c", id), ("t", i.TaxaId), ("d", i.Descricao), ("v", i.Valor));
            geradas++;
        }
        return Resultado<int>.Ok(geradas);
    }

    /// <summary>Encargos de atraso calculados para pagar a cobrança na data.</summary>
    public async Task<(decimal Multa, decimal Juros)> EncargosAsync(long empresa, Cobranca c, DateOnly pagamento, CancellationToken ct = default)
        => RegrasCondominio.Encargos(c.Valor, c.Vencimento, pagamento, await ConfiguracaoAsync(empresa, ct), await MultaMaximaAsync(pagamento, ct));

    public async Task<Resultado> ReceberAsync(long empresa, Cobranca c, DateOnly pagamento, decimal multa, decimal juros, decimal desconto, long usuario, CancellationToken ct = default)
    {
        if (await Negar(usuario, empresa, P.CondominioFinanceiro, ct) is { } n) return Resultado.Falha(n);
        if (c.Situacao != "ABERTA") return Resultado.Falha("Só cobranças em aberto podem ser recebidas.");
        if (multa < 0 || juros < 0 || desconto < 0 || desconto > c.Valor) return Resultado.Falha("Multa, juros e desconto inválidos.");
        var max = Dinheiro.Arredondar(c.Valor * await MultaMaximaAsync(pagamento, ct) / 100m);
        if (multa > max) return Resultado.Falha($"A multa não pode passar de {Formato.Moeda(max)} (limite legal).");
        return await Tentar(() => _db.ExecutarAsync("""
            UPDATE condominio.cobranca SET situacao = 'PAGA', pago_em = @d, multa = @m, juros = @j, desconto = @ds, valor_pago = valor + @m + @j - @ds
             WHERE id = @id AND empresa_id = @e AND situacao = 'ABERTA'
            """, ct, ("d", pagamento), ("m", multa), ("j", juros), ("ds", desconto), ("id", c.Id), ("e", empresa)));
    }

    public async Task<Resultado> EstornarOuCancelarAsync(long empresa, Cobranca c, bool cancelar, long usuario, CancellationToken ct = default)
    {
        if (await Negar(usuario, empresa, P.CondominioFinanceiro, ct) is { } n) return Resultado.Falha(n);
        if (cancelar && c.Situacao != "ABERTA") return Resultado.Falha("Só cobranças em aberto podem ser canceladas.");
        return await Tentar(() => _db.ExecutarAsync("""
            UPDATE condominio.cobranca SET situacao = @s, pago_em = NULL, valor_pago = NULL, multa = 0, juros = 0, desconto = 0 WHERE id = @id AND empresa_id = @e
            """, ct, ("s", cancelar ? "CANCELADA" : "ABERTA"), ("id", c.Id), ("e", empresa)));
    }

    // ------------------------------------------------------------------ rateio entre unidades

    /// <summary>Prévia do rateio de um valor avulso entre as unidades ativas (fração ideal ou partes iguais).</summary>
    public async Task<Resultado<IReadOnlyList<ParcelaRateio>>> PreviaRateioAsync(long empresa, decimal valor, Rateio criterio, long usuario, CancellationToken ct = default)
    {
        if (!await PodeVerAsync(usuario, empresa, ct)) return Resultado<IReadOnlyList<ParcelaRateio>>.Falha("Você não tem acesso a este condomínio.");
        if (valor <= 0) return Resultado<IReadOnlyList<ParcelaRateio>>.Falha("Informe o valor a ratear.");
        var unidades = await ListarUnidadesAsync(empresa, ct);
        if (unidades.Count(u => u.Ativa) == 0) return Resultado<IReadOnlyList<ParcelaRateio>>.Falha("Cadastre as unidades antes de ratear.");
        return Resultado<IReadOnlyList<ParcelaRateio>>.Ok(RegrasCondominio.RatearValor(valor, criterio, unidades));
    }

    /// <summary>
    /// Lança o rateio nas cobranças da competência: entra como item da cobrança em aberto de cada unidade (somando ao valor) ou cria a
    /// cobrança se a unidade ainda não tem. Cobranças já pagas ou canceladas na competência ficam de fora (devolvidas na lista de avisos).
    /// </summary>
    public async Task<Resultado<(int Lancadas, IReadOnlyList<string> Avisos)>> LancarRateioAsync(long empresa, DateOnly competencia, string descricao, decimal valor,
                                                                                                Rateio criterio, long usuario, CancellationToken ct = default)
    {
        if (await Negar(usuario, empresa, P.CondominioFinanceiro, ct) is { } n) return Resultado<(int, IReadOnlyList<string>)>.Falha(n);
        if (string.IsNullOrWhiteSpace(descricao)) return Resultado<(int, IReadOnlyList<string>)>.Falha("Informe a descrição do rateio (ex.: Rateio da pintura da fachada).");
        var previa = await PreviaRateioAsync(empresa, valor, criterio, usuario, ct);
        if (!previa.Sucesso) return Resultado<(int, IReadOnlyList<string>)>.Falha(previa.Erros);
        competencia = RegrasCondominio.Mes(competencia);
        var cfg = await ConfiguracaoAsync(empresa, ct);
        var existentes = (await _db.ListarAsync("SELECT unidade_id, id, situacao FROM condominio.cobranca WHERE empresa_id = @e AND competencia = @c",
                                                r => (Unidade: r.GetInt64(0), Id: r.GetInt64(1), Situacao: r.GetString(2)), ct, ("e", empresa), ("c", competencia)))
                         .ToDictionary(x => x.Unidade);
        var desc = descricao.Trim().Length > 100 ? descricao.Trim()[..100] : descricao.Trim();
        var avisos = new List<string>();
        var lancadas = 0;
        foreach (var p in previa.Valor.Where(p => p.Valor > 0))
        {
            long cobranca;
            if (existentes.TryGetValue(p.UnidadeId, out var c))
            {
                if (c.Situacao != "ABERTA") { avisos.Add($"Unidade {p.Unidade}: a cobrança de {competencia:MM/yyyy} já está {c.Situacao.ToLowerInvariant()}; o rateio dela ({Formato.Moeda(p.Valor)}) não foi lançado."); continue; }
                cobranca = c.Id;
                await _db.ExecutarAsync("UPDATE condominio.cobranca SET valor = valor + @v WHERE id = @id", ct, ("v", p.Valor), ("id", cobranca));
            }
            else
                cobranca = await _db.EscalarAsync<long>("INSERT INTO condominio.cobranca (empresa_id, unidade_id, competencia, vencimento, valor) VALUES (@e, @u, @c, @v, @val) RETURNING id",
                    ct, ("e", empresa), ("u", p.UnidadeId), ("c", competencia), ("v", RegrasCondominio.Vencimento(competencia, cfg.DiaVencimento)), ("val", p.Valor));
            await _db.ExecutarAsync("INSERT INTO condominio.cobranca_item (cobranca_id, taxa_id, descricao, valor) VALUES (@c, NULL, @d, @v)",
                                    ct, ("c", cobranca), ("d", desc), ("v", p.Valor));
            lancadas++;
        }
        return Resultado<(int, IReadOnlyList<string>)>.Ok((lancadas, avisos));
    }

    // ------------------------------------------------------------------ assembleias

    public async Task<List<Assembleia>> AssembleiasAsync(long empresa, long usuario, CancellationToken ct = default)
        => await PodeVerAsync(usuario, empresa, ct)
            ? await _db.ListarAsync("""
                SELECT id, numero, data, horario, tipo, convocacao, local, presidente, secretario, unidades_presentes, pauta, deliberacoes
                  FROM condominio.assembleia WHERE empresa_id = @e ORDER BY numero DESC
                """, r => new Assembleia(r.GetInt64(0), r.GetInt32(1), r.GetFieldValue<DateOnly>(2), r.GetString(3),
                                         r.GetString(4) == "EXTRAORDINARIA" ? TipoAssembleia.Extraordinaria : TipoAssembleia.Ordinaria, r.GetInt16(5), r.GetString(6),
                                         r.GetString(7), r.GetString(8), r.GetInt32(9), r.GetString(10), r.GetString(11)), ct, ("e", empresa))
            : [];

    /// <summary>Grava a assembleia; na inclusão recebe o próximo número do condomínio. Devolve o número.</summary>
    public async Task<Resultado<int>> GravarAssembleiaAsync(long empresa, Assembleia a, long usuario, CancellationToken ct = default)
    {
        if (await Negar(usuario, empresa, P.CondominioCadastros, ct) is { } n) return Resultado<int>.Falha(n);
        if (AtaAssembleia.Validar(a) is { Count: > 0 } erros) return Resultado<int>.Falha(erros);
        (string, object?)[] campos =
        [
            ("e", empresa), ("id", a.Id), ("d", a.Data), ("h", a.Horario), ("t", a.Tipo == TipoAssembleia.Extraordinaria ? "EXTRAORDINARIA" : "ORDINARIA"),
            ("cv", (short)a.Convocacao), ("l", a.Local.Trim()), ("p", a.Presidente.Trim()), ("s", a.Secretario.Trim()), ("up", a.UnidadesPresentes),
            ("pa", a.Pauta.Trim()), ("de", a.Deliberacoes?.Trim() ?? ""),
        ];
        try
        {
            var numero = a.Id == 0
                ? await _db.EscalarAsync<int>("""
                    INSERT INTO condominio.assembleia (empresa_id, numero, data, horario, tipo, convocacao, local, presidente, secretario, unidades_presentes, pauta, deliberacoes)
                    VALUES (@e, (SELECT COALESCE(MAX(numero), 0) + 1 FROM condominio.assembleia WHERE empresa_id = @e), @d, @h, @t, @cv, @l, @p, @s, @up, @pa, @de) RETURNING numero
                    """, ct, campos)
                : await _db.EscalarAsync<int?>("""
                    UPDATE condominio.assembleia SET data = @d, horario = @h, tipo = @t, convocacao = @cv, local = @l, presidente = @p, secretario = @s,
                           unidades_presentes = @up, pauta = @pa, deliberacoes = @de
                     WHERE id = @id AND empresa_id = @e RETURNING numero
                    """, ct, campos) ?? throw new ViolacaoRegraException("Assembleia não encontrada.");
            return Resultado<int>.Ok(numero);
        }
        catch (ViolacaoRegraException ex) { return Resultado<int>.Falha(ex.Message); }
    }

    public async Task<Resultado> ExcluirAssembleiaAsync(long empresa, long id, long usuario, CancellationToken ct = default)
    {
        if (await Negar(usuario, empresa, P.CondominioCadastros, ct) is { } n) return Resultado.Falha(n);
        return await Tentar(() => _db.ExecutarAsync("DELETE FROM condominio.assembleia WHERE id = @id AND empresa_id = @e", ct, ("id", id), ("e", empresa)));
    }

    /// <summary>Dados do cabeçalho da ata: nome e CNPJ do condomínio, unidades e o síndico cadastrado.</summary>
    public async Task<(string Nome, string Cnpj, List<Unidade> Unidades, string? Sindico)> DadosAtaAsync(long empresa, CancellationToken ct = default)
    {
        var (nome, cnpj) = (await _db.ListarAsync("SELECT razao_social, btrim(cnpj) FROM core.empresa WHERE id = @e", r => (r.GetString(0), r.GetString(1)), ct, ("e", empresa)))
                           .FirstOrDefault();
        var sindico = await _db.EscalarAsync<string?>("SELECT nome FROM core.empresa_sindico WHERE empresa_id = @e", ct, ("e", empresa));
        return (nome ?? "", cnpj is { Length: 14 } c ? Vorticis.Domain.Cadastros.Cnpj.Formatar(c) : cnpj ?? "", await ListarUnidadesAsync(empresa, ct), sindico);
    }

    // ------------------------------------------------------------------ despesas

    public async Task<List<Despesa>> DespesasAsync(long empresa, DateOnly inicio, DateOnly fim, long usuario, CancellationToken ct = default)
        => await PodeVerAsync(usuario, empresa, ct) ? await _db.ListarAsync("""
            SELECT d.id, d.data, d.vencimento, d.fornecedor_id, d.categoria, d.descricao, d.valor, d.pago_em, f.nome
              FROM condominio.despesa d LEFT JOIN core.participante f ON f.id = d.fornecedor_id
             WHERE d.empresa_id = @e AND d.vencimento BETWEEN @i AND @f ORDER BY d.vencimento, d.id
            """, r => new Despesa(r.GetInt64(0), r.GetFieldValue<DateOnly>(1), r.GetFieldValue<DateOnly>(2), r.Id(3), r.GetString(4), r.GetString(5), r.GetDecimal(6),
                                  r.Data(7), r.Texto(8)), ct, ("e", empresa), ("i", inicio), ("f", fim)) : [];

    public async Task<Resultado> GravarDespesaAsync(long empresa, Despesa d, long usuario, CancellationToken ct = default)
    {
        if (await Negar(usuario, empresa, P.CondominioFinanceiro, ct) is { } n) return Resultado.Falha(n);
        if (string.IsNullOrWhiteSpace(d.Descricao)) return Resultado.Falha("Informe a descrição da despesa.");
        if (d.Valor <= 0) return Resultado.Falha("Informe o valor da despesa.");
        return await Tentar(() => _db.ExecutarAsync(d.Id == 0
            ? "INSERT INTO condominio.despesa (empresa_id, data, vencimento, fornecedor_id, categoria, descricao, valor, pago_em) VALUES (@e, @d, @v, @f, @c, @ds, @val, @p)"
            : "UPDATE condominio.despesa SET data = @d, vencimento = @v, fornecedor_id = @f, categoria = @c, descricao = @ds, valor = @val, pago_em = @p WHERE id = @id AND empresa_id = @e",
            ct, ("e", empresa), ("d", d.Data), ("v", d.Vencimento), ("f", d.FornecedorId), ("c", d.Categoria), ("ds", d.Descricao.Trim()), ("val", d.Valor), ("p", d.PagoEm), ("id", d.Id)));
    }

    public async Task<Resultado> ExcluirDespesaAsync(long empresa, long id, long usuario, CancellationToken ct = default)
    {
        if (await Negar(usuario, empresa, P.CondominioFinanceiro, ct) is { } n) return Resultado.Falha(n);
        return await Tentar(() => _db.ExecutarAsync("DELETE FROM condominio.despesa WHERE id = @id AND empresa_id = @e", ct, ("id", id), ("e", empresa)));
    }

    // ------------------------------------------------------------------ áreas comuns, reservas e ocorrências

    public async Task<List<AreaComum>> AreasAsync(long empresa, long usuario, CancellationToken ct = default)
        => await PodeVerAsync(usuario, empresa, ct) ? await _db.ListarAsync("SELECT id, nome, capacidade, taxa_reserva, ativa FROM condominio.area_comum WHERE empresa_id = @e ORDER BY nome",
            r => new AreaComum(r.GetInt64(0), r.GetString(1), r.GetInt32(2), r.GetDecimal(3), r.GetBoolean(4)), ct, ("e", empresa)) : [];

    public async Task<Resultado> GravarAreaAsync(long empresa, AreaComum a, long usuario, CancellationToken ct = default)
    {
        if (await Negar(usuario, empresa, P.CondominioCadastros, ct) is { } n) return Resultado.Falha(n);
        if (string.IsNullOrWhiteSpace(a.Nome)) return Resultado.Falha("Informe o nome da área comum.");
        return await Tentar(() => _db.ExecutarAsync(a.Id == 0
            ? "INSERT INTO condominio.area_comum (empresa_id, nome, capacidade, taxa_reserva, ativa) VALUES (@e, @n, @c, @t, @a)"
            : "UPDATE condominio.area_comum SET nome = @n, capacidade = @c, taxa_reserva = @t, ativa = @a WHERE id = @id AND empresa_id = @e",
            ct, ("e", empresa), ("n", a.Nome.Trim()), ("c", a.Capacidade), ("t", a.TaxaReserva), ("a", a.Ativa), ("id", a.Id)));
    }

    public async Task<List<Reserva>> ReservasAsync(long empresa, DateOnly inicio, DateOnly fim, long usuario, CancellationToken ct = default)
        => await PodeVerAsync(usuario, empresa, ct) ? await _db.ListarAsync("""
            SELECT r.id, r.area_id, a.nome, r.unidade_id, CASE WHEN u.bloco = '' THEN u.numero ELSE u.bloco || '-' || u.numero END, r.data, r.situacao, r.observacao
              FROM condominio.reserva r JOIN condominio.area_comum a ON a.id = r.area_id JOIN condominio.unidade u ON u.id = r.unidade_id
             WHERE a.empresa_id = @e AND r.data BETWEEN @i AND @f ORDER BY r.data, a.nome
            """, r => new Reserva(r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetInt64(3), r.GetString(4), r.GetFieldValue<DateOnly>(5), r.GetString(6), r.Texto(7)),
            ct, ("e", empresa), ("i", inicio), ("f", fim)) : [];

    public async Task<Resultado> ReservarAsync(long empresa, long areaId, long unidadeId, DateOnly data, string? observacao, long usuario, CancellationToken ct = default)
    {
        if (await Negar(usuario, empresa, P.CondominioCadastros, ct) is { } n) return Resultado.Falha(n);
        try
        {
            var r = await _db.ExecutarAsync("""
                INSERT INTO condominio.reserva (area_id, unidade_id, data, observacao)
                SELECT a.id, u.id, @d, @o FROM condominio.area_comum a JOIN condominio.unidade u ON u.empresa_id = a.empresa_id
                 WHERE a.id = @a AND u.id = @u AND a.empresa_id = @e AND a.ativa
                """, ct, ("d", data), ("o", observacao), ("a", areaId), ("u", unidadeId), ("e", empresa));
            return r == 0 ? Resultado.Falha("Área ou unidade não encontrada.") : Resultado.Ok();
        }
        catch (ViolacaoRegraException) { return Resultado.Falha($"A área já está reservada em {data:dd/MM/yyyy}."); }
    }

    public async Task<Resultado> CancelarReservaAsync(long empresa, long id, long usuario, CancellationToken ct = default)
    {
        if (await Negar(usuario, empresa, P.CondominioCadastros, ct) is { } n) return Resultado.Falha(n);
        return await Tentar(() => _db.ExecutarAsync("""
            UPDATE condominio.reserva r SET situacao = 'CANCELADA' FROM condominio.area_comum a WHERE a.id = r.area_id AND a.empresa_id = @e AND r.id = @id
            """, ct, ("e", empresa), ("id", id)));
    }

    public async Task<List<Ocorrencia>> OcorrenciasAsync(long empresa, bool somenteAbertas, long usuario, CancellationToken ct = default)
        => await PodeVerAsync(usuario, empresa, ct) ? await _db.ListarAsync("""
            SELECT o.id, o.unidade_id, CASE WHEN u.bloco = '' THEN u.numero ELSE u.bloco || '-' || u.numero END, o.data, o.tipo, o.titulo, o.descricao, o.situacao, o.resolvida_em
              FROM condominio.ocorrencia o LEFT JOIN condominio.unidade u ON u.id = o.unidade_id
             WHERE o.empresa_id = @e AND (NOT @a OR o.situacao = 'ABERTA') ORDER BY o.data DESC, o.id DESC
            """, r => new Ocorrencia(r.GetInt64(0), r.Id(1), r.Texto(2), r.GetFieldValue<DateOnly>(3), r.GetString(4), r.GetString(5), r.Texto(6), r.GetString(7), r.Data(8)),
            ct, ("e", empresa), ("a", somenteAbertas)) : [];

    public async Task<Resultado> GravarOcorrenciaAsync(long empresa, Ocorrencia o, long usuario, CancellationToken ct = default)
    {
        if (await Negar(usuario, empresa, P.CondominioCadastros, ct) is { } n) return Resultado.Falha(n);
        if (string.IsNullOrWhiteSpace(o.Titulo)) return Resultado.Falha("Informe o título da ocorrência.");
        return await Tentar(() => _db.ExecutarAsync(o.Id == 0
            ? "INSERT INTO condominio.ocorrencia (empresa_id, unidade_id, data, tipo, titulo, descricao, situacao, resolvida_em) VALUES (@e, @u, @d, @t, @ti, @ds, @s, @r)"
            : "UPDATE condominio.ocorrencia SET unidade_id = @u, data = @d, tipo = @t, titulo = @ti, descricao = @ds, situacao = @s, resolvida_em = @r WHERE id = @id AND empresa_id = @e",
            ct, ("e", empresa), ("u", o.UnidadeId), ("d", o.Data), ("t", o.Tipo), ("ti", o.Titulo.Trim()), ("ds", o.Descricao), ("s", o.Situacao),
            ("r", o.Situacao == "RESOLVIDA" ? o.ResolvidaEm ?? DateOnly.FromDateTime(DateTime.Today) : null), ("id", o.Id)));
    }

    // ------------------------------------------------------------------ relatórios

    /// <summary>Prestação de contas do mês: arrecadação (cobranças pagas no mês), encargos, despesas pagas por categoria e inadimplência acumulada.</summary>
    public async Task<Resultado<PrestacaoContas>> PrestacaoContasAsync(long empresa, DateOnly mes, long usuario, CancellationToken ct = default)
    {
        if (await Negar(usuario, empresa, P.CondominioRelatorios, ct) is { } n) return Resultado<PrestacaoContas>.Falha(n);
        var ini = RegrasCondominio.Mes(mes);
        var fim = ini.AddMonths(1).AddDays(-1);
        (string, object?)[] p = [("e", empresa), ("i", ini), ("f", fim)];
        var arrec = await _db.EscalarAsync<decimal>("SELECT COALESCE(SUM(valor - desconto), 0) FROM condominio.cobranca WHERE empresa_id = @e AND situacao = 'PAGA' AND pago_em BETWEEN @i AND @f", ct, p);
        var enc = await _db.EscalarAsync<decimal>("SELECT COALESCE(SUM(multa + juros), 0) FROM condominio.cobranca WHERE empresa_id = @e AND situacao = 'PAGA' AND pago_em BETWEEN @i AND @f", ct, p);
        var emit = await _db.EscalarAsync<decimal>("SELECT COALESCE(SUM(valor), 0) FROM condominio.cobranca WHERE empresa_id = @e AND situacao <> 'CANCELADA' AND competencia = @i", ct, p);
        var inad = await _db.EscalarAsync<decimal>("SELECT COALESCE(SUM(valor), 0) FROM condominio.cobranca WHERE empresa_id = @e AND situacao = 'ABERTA' AND vencimento <= @f", ct, p);
        var desp = await _db.ListarAsync("""
            SELECT categoria, SUM(valor) FROM condominio.despesa WHERE empresa_id = @e AND pago_em BETWEEN @i AND @f GROUP BY categoria ORDER BY 2 DESC
            """, r => (r.GetString(0), r.GetDecimal(1)), ct, p);
        return Resultado<PrestacaoContas>.Ok(new PrestacaoContas(ini, arrec, enc, desp, emit, inad));
    }

    /// <summary>Cobranças vencidas e em aberto, com multa e juros atualizados até a data.</summary>
    public async Task<Resultado<List<(Cobranca C, decimal Multa, decimal Juros)>>> InadimplenciaAsync(long empresa, DateOnly hoje, long usuario, CancellationToken ct = default)
    {
        if (await Negar(usuario, empresa, P.CondominioRelatorios, ct) is { } n) return Resultado<List<(Cobranca, decimal, decimal)>>.Falha(n);
        var cfg = await ConfiguracaoAsync(empresa, ct);
        var max = await MultaMaximaAsync(hoje, ct);
        var abertas = (await CobrancasAsync(empresa, null, true, usuario, ct)).Where(c => c.Vencimento < hoje).ToList();
        return Resultado<List<(Cobranca, decimal, decimal)>>.Ok(abertas.Select(c =>
        {
            var (m, j) = RegrasCondominio.Encargos(c.Valor, c.Vencimento, hoje, cfg, max);
            return (c, m, j);
        }).ToList());
    }
}

public static class ServicosCondominio
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VorticisApp, ServicoCondominio> Instancias = new();
    public static ServicoCondominio De(VorticisApp app) => Instancias.GetValue(app, a => new ServicoCondominio(a));
}
