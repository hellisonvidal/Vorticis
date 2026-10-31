using Npgsql;
using NpgsqlTypes;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Comum;
using Vorticis.Fiscal.Lucro;
using Vorticis.Fiscal.Postgres;
using Vorticis.Fiscal.Simples;

namespace Vorticis.Fiscal.Planejamento;

/// <summary>Simulação gravada (fiscal.planejamento_tributario).</summary>
public sealed record SimulacaoPlanejamento(long Id, int Ano, string Descricao, EntradaPlanejamento Dados, DateTimeOffset AtualizadoEm)
{
    public override string ToString() => $"{Ano} · {Descricao}";
}

/// <summary>Movimento do ano no histórico de receita e folha, para sugerir os valores da simulação.</summary>
public sealed record MovimentoAno(decimal Receita, decimal Folha, int Meses);

/// <summary>
/// Apuração › Planejamento tributário e Relatórios › Planejamento tributário. Calcular e ler pedem FISCAL_APURAR ou FISCAL_RELATORIOS;
/// gravar e excluir simulações, FISCAL_APURAR. Tabelas do Simples, presunções e alíquotas vêm de core.parametro_legal (vigentes em 1º de janeiro do ano).
/// </summary>
public sealed class ServicoPlanejamento(NpgsqlDataSource ds, IRepositorioSimples simples, IParametrosFiscais parametros, Autorizador autorizador)
{
    private async Task<Resultado<T>?> PodeVerAsync<T>(long empresa, long usuario, CancellationToken ct)
    {
        var p = await autorizador.ObterAsync(usuario, empresa, ct);
        return p.Pode(Permissao.FiscalApurar) || p.Pode(Permissao.FiscalRelatorios)
            ? null
            : await autorizador.NegarAsync<T>(usuario, empresa, Permissao.FiscalApurar, ct) ?? Resultado<T>.Falha("Acesso negado.");
    }

    public async Task<Resultado<ResultadoPlanejamento>> CompararAsync(long empresa, EntradaPlanejamento e, long usuario, CancellationToken ct = default)
    {
        if (await PodeVerAsync<ResultadoPlanejamento>(empresa, usuario, ct) is { } negado) return negado;
        var erros = PlanejamentoTributario.Validar(e);
        if (erros.Count > 0) return Resultado<ResultadoPlanejamento>.Falha(string.Join(" ", erros));
        var data = new DateOnly(e.Ano, 1, 1);
        var tabela = TabelaSimples.Carregar(await parametros.ObterJsonAsync(ChavesParametro.SimplesNacional, data, ct));
        var lucro = ParametrosLucro.Carregar(await parametros.ObterJsonAsync(ChavesParametro.LucroPresumidoReal, data, ct));
        var plan = ParametrosPlanejamento.Carregar(await parametros.ObterJsonAsync(ChavesParametro.Planejamento, data, ct));
        return Resultado<ResultadoPlanejamento>.Ok(PlanejamentoTributario.Comparar(e, tabela, lucro, plan));
    }

    /// <summary>Alíquota patronal padrão (CPP) para começar a simulação.</summary>
    public async Task<decimal> EncargoPadraoAsync(int ano, CancellationToken ct = default) =>
        ParametrosPlanejamento.Carregar(await parametros.ObterJsonAsync(ChavesParametro.Planejamento, new DateOnly(ano, 1, 1), ct)).CppAliquota;

    /// <summary>Receita e folha do ano em Apuração › Histórico de receita e folha.</summary>
    public async Task<Resultado<MovimentoAno>> MovimentoAsync(long empresa, int ano, long usuario, CancellationToken ct = default)
    {
        if (await PodeVerAsync<MovimentoAno>(empresa, usuario, ct) is { } negado) return negado;
        var meses = await simples.ListarBaseMensalAsync(empresa, new DateOnly(ano, 1, 1), new DateOnly(ano, 12, 1), ct);
        return Resultado<MovimentoAno>.Ok(new MovimentoAno(meses.Sum(m => m.Receita ?? 0), meses.Sum(m => m.Folha ?? 0), meses.Count(m => m.Receita is not null)));
    }

    public async Task<Resultado<IReadOnlyList<SimulacaoPlanejamento>>> ListarAsync(long empresa, long usuario, CancellationToken ct = default)
    {
        if (await PodeVerAsync<IReadOnlyList<SimulacaoPlanejamento>>(empresa, usuario, ct) is { } negado) return negado;
        await using var cmd = ds.CreateCommand("""
            SELECT id, ano, descricao, dados::text, atualizado_em FROM fiscal.planejamento_tributario
             WHERE empresa_id = @e ORDER BY ano DESC, descricao
            """).Com("e", empresa);
        var res = new List<SimulacaoPlanejamento>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            res.Add(new SimulacaoPlanejamento(r.GetInt64(0), r.GetInt32(1), r.GetString(2), JsonFiscal.Ler<EntradaPlanejamento>(r.GetString(3)), r.GetFieldValue<DateTimeOffset>(4)));
        return Resultado<IReadOnlyList<SimulacaoPlanejamento>>.Ok(res);
    }

    /// <summary>Grava a simulação (a de mesmo ano e descrição é substituída). Devolve o id.</summary>
    public async Task<Resultado<long>> GravarAsync(long empresa, string descricao, EntradaPlanejamento e, long usuario, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<long>(usuario, empresa, Permissao.FiscalApurar, ct) is { } negado) return negado;
        var erros = PlanejamentoTributario.Validar(e).ToList();
        if (string.IsNullOrWhiteSpace(descricao)) erros.Insert(0, "Informe a descrição da simulação.");
        else if (descricao.Trim().Length > 80) erros.Insert(0, "A descrição pode ter até 80 caracteres.");
        if (erros.Count > 0) return Resultado<long>.Falha(string.Join(" ", erros));
        await using var cmd = ds.CreateCommand("""
            INSERT INTO fiscal.planejamento_tributario (empresa_id, ano, descricao, dados, atualizado_por) VALUES (@e, @a, @d, @j, @u)
            ON CONFLICT (empresa_id, ano, lower(descricao)) DO UPDATE SET dados = EXCLUDED.dados, descricao = EXCLUDED.descricao,
                atualizado_em = now(), atualizado_por = EXCLUDED.atualizado_por
            RETURNING id
            """).Com("e", empresa).Com("a", e.Ano).Com("d", descricao.Trim()).Com("j", NpgsqlDbType.Jsonb, JsonFiscal.Serializar(e)).Com("u", usuario);
        return Resultado<long>.Ok((long)(await cmd.ExecuteScalarAsync(ct))!);
    }

    public async Task<Resultado> ExcluirAsync(long empresa, long id, long usuario, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuario, empresa, Permissao.FiscalApurar, ct) is { } motivo) return Resultado.Falha(motivo);
        await using var cmd = ds.CreateCommand("DELETE FROM fiscal.planejamento_tributario WHERE id = @i AND empresa_id = @e").Com("i", id).Com("e", empresa);
        return await cmd.ExecuteNonQueryAsync(ct) > 0 ? Resultado.Ok() : Resultado.Falha("Simulação não encontrada.");
    }
}
