using Npgsql;
using Vorticis.Application.Contabil;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Contabil;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra.Postgres;

namespace Vorticis.Infra.Contabil;

/// <summary>Lançamento que se repete todo mês: débito, crédito, valor, histórico, dia e vigência (competências).</summary>
public sealed record LancamentoProgramado(long Id, string Descricao, string ContaDebito, string ContaCredito, decimal Valor, string Historico, int Dia,
                                          DateOnly Inicio, DateOnly? Fim = null, string? CentroCusto = null, bool Ativo = true, DateOnly? UltimaGerada = null)
{
    /// <summary>Data do lançamento na competência (o dia é ajustado ao último dia nos meses mais curtos).</summary>
    public DateOnly DataEm(DateOnly competencia) => new(competencia.Year, competencia.Month, Math.Min(Dia, DateTime.DaysInMonth(competencia.Year, competencia.Month)));

    public bool VigenteEm(DateOnly competencia) => Ativo && competencia >= Inicio && (Fim is null || competencia <= Fim);

    /// <summary>Histórico com a competência no lugar de {competencia} (por exemplo, "Aluguel {competencia}" vira "Aluguel 03/2027").</summary>
    public string HistoricoEm(DateOnly competencia) => Historico.Replace("{competencia}", competencia.ToString("MM/yyyy"), StringComparison.OrdinalIgnoreCase);
}

/// <summary>Contábil › Lançamentos programados: cadastro e geração dos lançamentos da competência (sem duplicar).</summary>
public sealed class ServicoLancamentosProgramados(NpgsqlDataSource ds, ServicoLancamento lancamentos, Autorizador autorizador)
{
    private readonly BancoModulo _db = new(ds);
    private static DateOnly Mes(DateOnly d) => new(d.Year, d.Month, 1);

    public async Task<IReadOnlyList<LancamentoProgramado>> ListarAsync(long empresaId, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.LancamentoIncluir, ct) is not null) return [];
        return await _db.ListarAsync("""
            SELECT p.id, p.descricao, p.conta_debito, p.conta_credito, p.valor, p.historico, p.dia, p.inicio, p.fim, p.centro_custo, p.ativo,
                   (SELECT max(g.competencia) FROM contabil.lancamento_programado_gerado g WHERE g.programado_id = p.id)
              FROM contabil.lancamento_programado p WHERE p.empresa_id = @e ORDER BY NOT p.ativo, p.dia, p.descricao
            """, r => new LancamentoProgramado(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetDecimal(4), r.GetString(5), r.GetInt16(6),
                                               r.GetFieldValue<DateOnly>(7), r.Data(8), r.Texto(9), r.GetBoolean(10), r.Data(11)), ct, ("e", empresaId));
    }

    public async Task<Resultado<long>> GravarAsync(long empresaId, LancamentoProgramado p, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<long>(usuarioId, empresaId, Permissao.LancamentoIncluir, ct) is { } negado) return negado;
        if (string.IsNullOrWhiteSpace(p.Descricao)) return Resultado<long>.Falha("Informe a descrição.");
        if (string.IsNullOrWhiteSpace(p.ContaDebito) || string.IsNullOrWhiteSpace(p.ContaCredito)) return Resultado<long>.Falha("Informe as contas de débito e de crédito.");
        if (p.ContaDebito.Trim() == p.ContaCredito.Trim()) return Resultado<long>.Falha("As contas de débito e de crédito devem ser diferentes.");
        if (p.Valor <= 0) return Resultado<long>.Falha("Informe o valor.");
        if (string.IsNullOrWhiteSpace(p.Historico)) return Resultado<long>.Falha("Informe o histórico.");
        if (p.Dia is < 1 or > 31) return Resultado<long>.Falha("Dia do lançamento: de 1 a 31.");
        var ini = Mes(p.Inicio); DateOnly? fim = p.Fim is { } f ? Mes(f) : null;
        if (fim < ini) return Resultado<long>.Falha("A última competência é anterior à primeira.");
        (string, object?)[] campos =
        [
            ("id", p.Id), ("e", empresaId), ("d", p.Descricao.Trim()), ("cd", p.ContaDebito.Trim()), ("cc", p.ContaCredito.Trim()), ("v", p.Valor),
            ("h", p.Historico.Trim()), ("dia", (short)p.Dia), ("i", ini), ("f", fim), ("ccu", string.IsNullOrWhiteSpace(p.CentroCusto) ? null : p.CentroCusto.Trim()), ("a", p.Ativo),
        ];
        try
        {
            var id = p.Id == 0
                ? await _db.EscalarAsync<long>("""
                    INSERT INTO contabil.lancamento_programado (empresa_id, descricao, conta_debito, conta_credito, valor, historico, dia, inicio, fim, centro_custo, ativo)
                    VALUES (@e, @d, @cd, @cc, @v, @h, @dia, @i, @f::date, @ccu, @a) RETURNING id
                    """, ct, campos)
                : await _db.EscalarAsync<long?>("""
                    UPDATE contabil.lancamento_programado SET descricao = @d, conta_debito = @cd, conta_credito = @cc, valor = @v, historico = @h, dia = @dia,
                           inicio = @i, fim = @f::date, centro_custo = @ccu, ativo = @a WHERE id = @id AND empresa_id = @e RETURNING id
                    """, ct, campos) ?? throw new ViolacaoRegraException("Lançamento programado não encontrado.");
            return Resultado<long>.Ok(id);
        }
        catch (ViolacaoRegraException ex) { return Resultado<long>.Falha(ex.Message); }
    }

    public async Task<Resultado> ExcluirAsync(long empresaId, long id, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.LancamentoIncluir, ct) is { } negado) return Resultado.Falha(negado);
        await _db.ExecutarAsync("DELETE FROM contabil.lancamento_programado WHERE id = @id AND empresa_id = @e", ct, ("id", id), ("e", empresaId));
        return Resultado.Ok();
    }

    /// <summary>
    /// Gera os lançamentos dos programados vigentes na competência que ainda não foram gerados nela. Cada um é validado e gravado
    /// pelo serviço de lançamentos (contas, período fechado, bloqueio); os que falham voltam como avisos.
    /// </summary>
    public async Task<Resultado<(int Gerados, decimal Total, IReadOnlyList<string> Avisos)>> GerarAsync(long empresaId, DateOnly competencia, long usuarioId,
                                                                                                    CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<(int, decimal, IReadOnlyList<string>)>(usuarioId, empresaId, Permissao.LancamentoIncluir, ct) is { } negado) return negado;
        competencia = Mes(competencia);
        var jaGerados = (await _db.ListarAsync("""
            SELECT g.programado_id FROM contabil.lancamento_programado_gerado g JOIN contabil.lancamento_programado p ON p.id = g.programado_id
             WHERE p.empresa_id = @e AND g.competencia = @c
            """, r => r.GetInt64(0), ct, ("e", empresaId), ("c", competencia))).ToHashSet();
        var avisos = new List<string>();
        int n = 0; decimal total = 0;
        foreach (var p in (await ListarAsync(empresaId, usuarioId, ct)).Where(p => p.VigenteEm(competencia) && !jaGerados.Contains(p.Id)))
        {
            var hist = p.HistoricoEm(competencia);
            var proposta = new LancamentoProposto(empresaId, p.DataEm(competencia), OrigemLancamento.Manual,
            [
                new PartidaProposta(p.ContaDebito, IndicadorDC.D, p.Valor, hist, p.CentroCusto),
                new PartidaProposta(p.ContaCredito, IndicadorDC.C, p.Valor, hist, p.CentroCusto),
            ]);
            var r = await lancamentos.EfetivarAsync(proposta, usuarioId, ct);
            if (!r.Sucesso) { avisos.Add($"{p.Descricao}: {string.Join("; ", r.Erros)}"); continue; }
            await _db.ExecutarAsync("INSERT INTO contabil.lancamento_programado_gerado (programado_id, competencia, lancamento_id) VALUES (@p, @c, @l)",
                                    ct, ("p", p.Id), ("c", competencia), ("l", r.Valor.Id));
            n++; total += p.Valor;
        }
        return Resultado<(int, decimal, IReadOnlyList<string>)>.Ok((n, total, avisos));
    }
}
