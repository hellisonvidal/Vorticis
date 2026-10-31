using System.Text.Json;
using Npgsql;
using Vorticis.Application.Relatorios;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Obrigacoes;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Folha.Servicos;

public sealed record LinhaFgts(string Matricula, string Nome, string Cpf, decimal Base, decimal Fgts);

/// <summary>Guia do FGTS Digital (GFD) emitida no portal: identificador, valor, vencimento e pagamento.</summary>
public sealed record GuiaFgts(string Identificador, decimal Valor, DateOnly Vencimento, DateOnly? PagoEm, decimal? ValorPago)
{
    public bool Paga => PagoEm is not null;
}

public sealed record DemonstrativoFgts(DateOnly Competencia, bool CompetenciaFechada, IReadOnlyList<LinhaFgts> Linhas, DateOnly Vencimento, GuiaFgts? Guia)
{
    public decimal Total => Linhas.Sum(l => l.Fgts);

    public string Situacao => Guia switch
    {
        { Paga: true } g => $"Guia {g.Identificador} paga em {g.PagoEm:dd/MM/yyyy} ({Formato.Moeda(g.ValorPago ?? 0)}).",
        { } g => $"Guia {g.Identificador} registrada: {Formato.Moeda(g.Valor)}, vence em {g.Vencimento:dd/MM/yyyy}. Falta registrar o pagamento.",
        _ when !CompetenciaFechada => "Folha da competência em aberto: feche a competência antes de emitir a guia no FGTS Digital.",
        _ when Linhas.Count == 0 => "Sem FGTS calculado nesta competência.",
        _ => $"Pronto para emitir a guia no portal do FGTS Digital: {Formato.Moeda(Total)}, vencimento {Vencimento:dd/MM/yyyy}.",
    };

    /// <summary>Tabela do demonstrativo (tela, PDF, Excel e CSV): FGTS por trabalhador e o total.</summary>
    public TabelaRelatorio Tabela()
    {
        var linhas = Linhas.Select(l => new LinhaRelatorio([l.Matricula, l.Nome, l.Cpf.Length == 11 ? $"{l.Cpf[..3]}.{l.Cpf[3..6]}.{l.Cpf[6..9]}-{l.Cpf[9..]}" : l.Cpf, l.Base, l.Fgts])).ToList();
        linhas.Add(new LinhaRelatorio(["", "Total da competência", "", Linhas.Sum(l => l.Base), Total], true));
        return new TabelaRelatorio($"FGTS Digital · competência {Competencia:MM/yyyy}",
            [new("Matrícula", TipoColuna.Texto, 2.5), new("Trabalhador", TipoColuna.Texto, 7), new("CPF", TipoColuna.Texto, 3.2),
             new("Base do FGTS", TipoColuna.Valor, 3), new("FGTS", TipoColuna.Valor, 3)], linhas,
            $"Vencimento: {Vencimento:dd/MM/yyyy}. {Situacao}");
    }
}

/// <summary>
/// Obrigações › FGTS Digital. As remunerações chegam ao FGTS Digital pelos eventos do eSocial (S-1200 e S-1299, em Obrigações › eSocial);
/// a guia (GFD) é emitida no portal do FGTS Digital com o certificado ou o gov.br. Aqui: o demonstrativo do FGTS da competência, calculado
/// pela folha, para conferir com a guia; o registro da guia (só com a competência fechada) e do pagamento. Ver: acesso à empresa;
/// registrar: FOLHA_CALCULAR. Vencimento pelo parâmetro legal FOLHA_FGTS_DIGITAL.
/// </summary>
public sealed class ServicoFgtsDigital(NpgsqlDataSource ds, Autorizador autorizador)
{
    public const string ChaveParametro = "FOLHA_FGTS_DIGITAL";

    private static DateOnly Mes(DateOnly d) => new(d.Year, d.Month, 1);

    /// <summary>Dia do vencimento no mês seguinte, antecipado para o dia útil anterior quando cair em fim de semana ou feriado.</summary>
    public static DateOnly Vencimento(DateOnly competencia, int dia = 20, bool antecipa = true)
    {
        var seguinte = Mes(competencia).AddMonths(1);
        var d = new DateOnly(seguinte.Year, seguinte.Month, Math.Min(dia, DateTime.DaysInMonth(seguinte.Year, seguinte.Month)));
        return antecipa ? CalendarioDiasUteis.DiaUtilAnterior(d) : d;
    }

    private async Task<DateOnly> VencimentoAsync(DateOnly competencia, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT valor::text FROM core.parametro_legal WHERE chave = @k AND vigencia_inicio <= @c AND (vigencia_fim IS NULL OR vigencia_fim >= @c)
             ORDER BY vigencia_inicio DESC LIMIT 1
            """);
        cmd.Parameters.AddWithValue("k", ChaveParametro);
        cmd.Parameters.AddWithValue("c", competencia);
        if (await cmd.ExecuteScalarAsync(ct) is not string json) return Vencimento(competencia);
        using var doc = JsonDocument.Parse(json);
        return Vencimento(competencia, doc.RootElement.GetProperty("diaVencimento").GetInt32(), doc.RootElement.GetProperty("antecipaParaDiaUtil").GetBoolean());
    }

    public async Task<Resultado<DemonstrativoFgts>> DemonstrativoAsync(long empresa, DateOnly competencia, long usuario, CancellationToken ct = default)
    {
        if (!await autorizador.PodeVerAsync(usuario, empresa, ct)) return Resultado<DemonstrativoFgts>.Falha("Você não tem acesso a esta empresa.");
        var comp = Mes(competencia);
        var linhas = new List<LinhaFgts>();
        await using (var cmd = ds.CreateCommand("""
            SELECT f.matricula, f.nome, f.cpf, sum(c.base_fgts), sum(c.fgts)
              FROM folha.calculo c JOIN folha.funcionario f ON f.id = c.funcionario_id
             WHERE c.empresa_id = @e AND c.competencia = @c
             GROUP BY f.matricula, f.nome, f.cpf HAVING sum(c.fgts) <> 0
             ORDER BY f.nome
            """))
        {
            cmd.Parameters.AddWithValue("e", empresa);
            cmd.Parameters.AddWithValue("c", comp);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) linhas.Add(new LinhaFgts(r.GetString(0), r.GetString(1), r.GetString(2), r.GetDecimal(3), r.GetDecimal(4)));
        }
        bool fechada;
        GuiaFgts? guia = null;
        await using (var cmd = ds.CreateCommand("""
            SELECT EXISTS (SELECT 1 FROM folha.competencia WHERE empresa_id = @e AND competencia = @c AND status = 'FECHADA'),
                   g.identificador, g.valor, g.vencimento, g.pago_em, g.valor_pago
              FROM (SELECT 1) x LEFT JOIN folha.fgts_guia g ON g.empresa_id = @e AND g.competencia = @c
            """))
        {
            cmd.Parameters.AddWithValue("e", empresa);
            cmd.Parameters.AddWithValue("c", comp);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            await r.ReadAsync(ct);
            fechada = r.GetBoolean(0);
            if (!r.IsDBNull(1))
                guia = new GuiaFgts(r.GetString(1), r.GetDecimal(2), r.GetFieldValue<DateOnly>(3), r.IsDBNull(4) ? null : r.GetFieldValue<DateOnly>(4),
                                    r.IsDBNull(5) ? null : r.GetDecimal(5));
        }
        return Resultado<DemonstrativoFgts>.Ok(new DemonstrativoFgts(comp, fechada, linhas, await VencimentoAsync(comp, ct), guia));
    }

    /// <summary>Registra a guia emitida no FGTS Digital (o valor é o total do demonstrativo). Exige a competência fechada.</summary>
    public async Task<Resultado> RegistrarGuiaAsync(long empresa, DateOnly competencia, string identificador, long usuario, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuario, empresa, Permissao.FolhaCalcular, ct) is { } motivo) return Resultado.Falha(motivo);
        if (string.IsNullOrWhiteSpace(identificador)) return Resultado.Falha("Informe o identificador da guia emitida no FGTS Digital.");
        if (identificador.Trim().Length > 40) return Resultado.Falha("O identificador da guia pode ter até 40 caracteres.");
        var d = await DemonstrativoAsync(empresa, competencia, usuario, ct);
        if (!d.Sucesso) return Resultado.Falha(d.Erros);
        if (!d.Valor!.CompetenciaFechada) return Resultado.Falha($"A folha de {d.Valor.Competencia:MM/yyyy} não está fechada: feche a competência antes de registrar a guia do FGTS Digital.");
        if (d.Valor.Total <= 0) return Resultado.Falha("Não há FGTS calculado nesta competência.");
        if (d.Valor.Guia is { Paga: true }) return Resultado.Falha("A guia desta competência já está paga.");
        await using var cmd = ds.CreateCommand("""
            INSERT INTO folha.fgts_guia (empresa_id, competencia, identificador, valor, vencimento, emitida_por) VALUES (@e, @c, @i, @v, @d, @u)
            ON CONFLICT (empresa_id, competencia) DO UPDATE SET identificador = EXCLUDED.identificador, valor = EXCLUDED.valor, vencimento = EXCLUDED.vencimento,
                emitida_em = now(), emitida_por = EXCLUDED.emitida_por
            """);
        cmd.Parameters.AddWithValue("e", empresa);
        cmd.Parameters.AddWithValue("c", d.Valor.Competencia);
        cmd.Parameters.AddWithValue("i", identificador.Trim());
        cmd.Parameters.AddWithValue("v", d.Valor.Total);
        cmd.Parameters.AddWithValue("d", d.Valor.Vencimento);
        cmd.Parameters.AddWithValue("u", usuario);
        await cmd.ExecuteNonQueryAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> RegistrarPagamentoAsync(long empresa, DateOnly competencia, DateOnly data, decimal valor, long usuario, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuario, empresa, Permissao.FolhaCalcular, ct) is { } motivo) return Resultado.Falha(motivo);
        if (valor <= 0) return Resultado.Falha("Informe o valor pago.");
        await using var cmd = ds.CreateCommand("UPDATE folha.fgts_guia SET pago_em = @d, valor_pago = @v WHERE empresa_id = @e AND competencia = @c");
        cmd.Parameters.AddWithValue("e", empresa);
        cmd.Parameters.AddWithValue("c", Mes(competencia));
        cmd.Parameters.AddWithValue("d", data);
        cmd.Parameters.AddWithValue("v", valor);
        return await cmd.ExecuteNonQueryAsync(ct) > 0 ? Resultado.Ok() : Resultado.Falha("Registre a guia da competência antes do pagamento.");
    }

    /// <summary>Exclui a guia registrada (para emitir outra); guia paga não sai.</summary>
    public async Task<Resultado> ExcluirGuiaAsync(long empresa, DateOnly competencia, long usuario, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuario, empresa, Permissao.FolhaCalcular, ct) is { } motivo) return Resultado.Falha(motivo);
        await using var cmd = ds.CreateCommand("DELETE FROM folha.fgts_guia WHERE empresa_id = @e AND competencia = @c AND pago_em IS NULL");
        cmd.Parameters.AddWithValue("e", empresa);
        cmd.Parameters.AddWithValue("c", Mes(competencia));
        return await cmd.ExecuteNonQueryAsync(ct) > 0 ? Resultado.Ok() : Resultado.Falha("Não há guia em aberto nesta competência (guia paga não pode ser excluída).");
    }
}
