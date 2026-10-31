using Npgsql;
using NpgsqlTypes;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Guias;
using Vorticis.Fiscal.Postgres;
using Vorticis.Fiscal.Simples;

namespace Vorticis.Fiscal.Dctf;

/// <summary>Débito declarado na DCTFWeb pelo MIT (IRPJ, CSLL, PIS, COFINS... por código de receita).</summary>
public sealed record DebitoDctf(string Tributo, string? CodigoReceita, string Descricao, decimal Valor);

/// <summary>Situação de um mês: apuração encerrada (período fechado), declaração preparada ou transmitida (com o recibo).</summary>
public sealed record MesDctf(DateOnly Competencia, bool Fechado, string? Situacao, decimal Total, string? Recibo, DateOnly? TransmitidaEm)
{
    public string SituacaoTexto => Situacao switch
    {
        "TRANSMITIDA" => $"Transmitida (recibo {Recibo}, {TransmitidaEm:dd/MM/yyyy})",
        "PREPARADA" => "Preparada: transmita na DCTFWeb e registre o recibo",
        _ => Fechado ? "Apuração encerrada: pronta para preparar" : "Apuração em aberto: feche o mês antes",
    };
}

/// <summary>Declaração do mês: débitos do MIT (das guias DARF das apurações) e, informativos, os que chegam pela EFD-Reinf e pelo eSocial.</summary>
public sealed record DeclaracaoDctf(DateOnly Competencia, IReadOnlyList<DebitoDctf> Debitos, IReadOnlyList<DebitoDctf> Informativos)
{
    public decimal Total => Debitos.Sum(d => d.Valor);
    public bool SemMovimento => Debitos.Count == 0;
}

/// <summary>
/// Obrigações › DCTFWeb (MIT). Depois da apuração encerrada (Apuração › Fechamento do período fiscal), prepara a declaração do mês com os
/// débitos das guias DARF geradas das apurações (IRPJ, CSLL, PIS, COFINS e DARF manuais) — os mesmos valores que vão para o MIT —, grava a
/// declaração preparada e, depois de transmitida na DCTFWeb (e-CAC), o número do recibo. As retenções (EFD-Reinf) e o INSS da folha (eSocial)
/// entram na DCTFWeb sozinhos e aparecem só como informação. Só para empresas do regime normal; permissão FISCAL_APURAR.
/// </summary>
public sealed class ServicoDctfWeb(NpgsqlDataSource ds, IRepositorioGuias guias, Fechamento.ServicoFechamentoFiscal fechamento, Autorizador autorizador)
{
    private static DateOnly Mes(DateOnly d) => new(d.Year, d.Month, 1);

    /// <summary>Separa as guias do mês: DARF das apurações (MIT) e as que a DCTFWeb recebe de outras escriturações.</summary>
    public static DeclaracaoDctf Montar(DateOnly competencia, IEnumerable<Guia> doMes)
    {
        var lista = doMes.Where(g => g.Valor > 0 && Mes(g.Competencia) == Mes(competencia)).ToList();
        var mit = lista.Where(g => g.Documento == DocumentoGuia.Darf && g.Origem != "RETENCAO")
            .GroupBy(g => (g.Tributo, g.CodigoReceita)).OrderBy(g => g.Key.Tributo).ThenBy(g => g.Key.CodigoReceita)
            .Select(g => new DebitoDctf(g.Key.Tributo, g.Key.CodigoReceita, g.First().Descricao, g.Sum(x => x.Valor))).ToList();
        var info = lista.Where(g => g.Origem == "RETENCAO" || g.Documento is DocumentoGuia.Gps or DocumentoGuia.DctfWeb)
            .Select(g => new DebitoDctf(g.Tributo, g.CodigoReceita, g.Descricao + (g.Origem == "RETENCAO" ? " (pela EFD-Reinf)" : " (pelo eSocial/EFD-Reinf)"), g.Valor)).ToList();
        return new DeclaracaoDctf(Mes(competencia), mit, info);
    }

    public async Task<Resultado<IReadOnlyList<MesDctf>>> ListarAsync(long empresa, int ano, long usuario, CancellationToken ct = default)
    {
        var p = await autorizador.ObterAsync(usuario, empresa, ct);
        if (!p.Pode(Permissao.FiscalApurar) && !p.Pode(Permissao.FiscalRelatorios))
            return await autorizador.NegarAsync<IReadOnlyList<MesDctf>>(usuario, empresa, Permissao.FiscalApurar, ct) ?? Resultado<IReadOnlyList<MesDctf>>.Falha("Acesso negado.");
        await using var cmd = ds.CreateCommand("""
            SELECT m::date, f.competencia IS NOT NULL, d.situacao, COALESCE(d.total, 0), d.recibo, d.transmitida_em
              FROM generate_series(make_date(@a, 1, 1), make_date(@a, 12, 1), interval '1 month') m
              LEFT JOIN fiscal.periodo_fechado f ON f.empresa_id = @e AND f.competencia = m::date
              LEFT JOIN fiscal.dctfweb d ON d.empresa_id = @e AND d.competencia = m::date
             ORDER BY m
            """).Com("e", empresa).Com("a", ano);
        var res = new List<MesDctf>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            res.Add(new MesDctf(r.GetFieldValue<DateOnly>(0), r.GetBoolean(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetDecimal(3),
                                r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetFieldValue<DateOnly>(5)));
        return Resultado<IReadOnlyList<MesDctf>>.Ok(res);
    }

    /// <summary>Débitos do mês como ficariam na declaração (sem gravar), para conferir antes de preparar.</summary>
    public async Task<Resultado<DeclaracaoDctf>> PreverAsync(long empresa, DateOnly competencia, long usuario, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<DeclaracaoDctf>(usuario, empresa, Permissao.FiscalApurar, ct) is { } negado) return negado;
        var mes = Mes(competencia);
        return Resultado<DeclaracaoDctf>.Ok(Montar(mes, await guias.ListarAsync(empresa, new FiltroGuias(mes, mes), ct)));
    }

    /// <summary>Prepara (ou refaz, enquanto não transmitida) a declaração do mês. Exige a apuração encerrada e o regime normal.</summary>
    public async Task<Resultado<DeclaracaoDctf>> PrepararAsync(long empresa, DateOnly competencia, long usuario, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<DeclaracaoDctf>(usuario, empresa, Permissao.FiscalApurar, ct) is { } negado) return negado;
        var mes = Mes(competencia);
        if (!Fechamento.ServicoFechamentoFiscal.RegimeNormal(await fechamento.RegimeAsync(empresa, mes, ct)))
            return Resultado<DeclaracaoDctf>.Falha("A DCTFWeb com os débitos do MIT é das empresas do regime normal (Lucro Presumido ou Real). No Simples Nacional, os tributos vão no DAS (PGDAS-D).");
        await using (var c = ds.CreateCommand("SELECT fiscal.fn_mes_fechado(@e, @c), (SELECT situacao FROM fiscal.dctfweb WHERE empresa_id = @e AND competencia = @c)")
                         .Com("e", empresa).Com("c", mes))
        await using (var r = await c.ExecuteReaderAsync(ct))
        {
            await r.ReadAsync(ct);
            if (!r.GetBoolean(0))
                return Resultado<DeclaracaoDctf>.Falha($"A apuração de {mes:MM/yyyy} não está encerrada: feche o mês em Apuração › Fechamento do período fiscal antes de preparar a DCTFWeb.");
            if (!r.IsDBNull(1) && r.GetString(1) == "TRANSMITIDA")
                return Resultado<DeclaracaoDctf>.Falha($"A DCTFWeb de {mes:MM/yyyy} já foi transmitida. Para retificar, desfaça o registro da transmissão primeiro.");
        }
        var d = Montar(mes, await guias.ListarAsync(empresa, new FiltroGuias(mes, mes), ct));
        await using var cmd = ds.CreateCommand("""
            INSERT INTO fiscal.dctfweb (empresa_id, competencia, debitos, total, preparada_por) VALUES (@e, @c, @j, @t, @u)
            ON CONFLICT (empresa_id, competencia) DO UPDATE SET debitos = EXCLUDED.debitos, total = EXCLUDED.total, preparada_em = now(),
                preparada_por = EXCLUDED.preparada_por
            """).Com("e", empresa).Com("c", mes).Com("j", NpgsqlDbType.Jsonb, JsonFiscal.Serializar(d.Debitos)).Com("t", d.Total).Com("u", usuario);
        await cmd.ExecuteNonQueryAsync(ct);
        return Resultado<DeclaracaoDctf>.Ok(d);
    }

    /// <summary>Registra a transmissão feita na DCTFWeb (e-CAC): número do recibo e data.</summary>
    public async Task<Resultado> RegistrarTransmissaoAsync(long empresa, DateOnly competencia, string recibo, DateOnly data, long usuario, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuario, empresa, Permissao.FiscalApurar, ct) is { } motivo) return Resultado.Falha(motivo);
        if (string.IsNullOrWhiteSpace(recibo)) return Resultado.Falha("Informe o número do recibo da transmissão.");
        if (recibo.Trim().Length > 40) return Resultado.Falha("O número do recibo pode ter até 40 caracteres.");
        if (data < Mes(competencia)) return Resultado.Falha("A data da transmissão não pode ser anterior ao mês declarado.");
        await using var cmd = ds.CreateCommand("""
            UPDATE fiscal.dctfweb SET situacao = 'TRANSMITIDA', recibo = @r, transmitida_em = @d WHERE empresa_id = @e AND competencia = @c AND situacao = 'PREPARADA'
            """).Com("e", empresa).Com("c", Mes(competencia)).Com("r", recibo.Trim()).Com("d", data);
        return await cmd.ExecuteNonQueryAsync(ct) > 0 ? Resultado.Ok() : Resultado.Falha("Prepare a declaração do mês antes de registrar a transmissão.");
    }

    /// <summary>Volta a declaração transmitida para preparada (para reabrir o mês e transmitir a retificadora).</summary>
    public async Task<Resultado> DesfazerTransmissaoAsync(long empresa, DateOnly competencia, long usuario, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuario, empresa, Permissao.FiscalApurar, ct) is { } motivo) return Resultado.Falha(motivo);
        await using var cmd = ds.CreateCommand("""
            UPDATE fiscal.dctfweb SET situacao = 'PREPARADA', recibo = NULL, transmitida_em = NULL WHERE empresa_id = @e AND competencia = @c AND situacao = 'TRANSMITIDA'
            """).Com("e", empresa).Com("c", Mes(competencia));
        return await cmd.ExecuteNonQueryAsync(ct) > 0 ? Resultado.Ok() : Resultado.Falha("Não há transmissão registrada neste mês.");
    }
}
