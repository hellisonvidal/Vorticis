using Vorticis.Application.Relatorios;
using Vorticis.Fiscal.Postgres;

namespace Vorticis.Fiscal.Documentos;

/// <summary>Notas autorizadas do período somadas por centro de custo (o do item ou, na falta dele, o da nota — o mesmo que a contabilização usa).</summary>
public sealed record LinhaCentroCusto(string? Codigo, string? Descricao, int NotasEntrada, decimal Entradas, int NotasSaida, decimal Saidas, decimal Contabilizado)
{
    public string Centro => Codigo is null ? "Sem centro de custo" : $"{Codigo} – {Descricao}";
}

public static class TabelaNotasPorCentro
{
    public const string Titulo = "Notas por centro de custo";

    public static TabelaRelatorio Montar(IReadOnlyList<LinhaCentroCusto> linhas)
    {
        var res = linhas.Select(l => new LinhaRelatorio([l.Centro, (long)l.NotasEntrada, l.Entradas, (long)l.NotasSaida, l.Saidas, l.Contabilizado])).ToList();
        res.Add(new LinhaRelatorio(["Total", (long)linhas.Sum(l => l.NotasEntrada), linhas.Sum(l => l.Entradas), (long)linhas.Sum(l => l.NotasSaida),
                                    linhas.Sum(l => l.Saidas), linhas.Sum(l => l.Contabilizado)], true));
        return new TabelaRelatorio(Titulo,
            [new("Centro de custo", TipoColuna.Texto, 7), new("Notas de entrada", TipoColuna.Inteiro, 2.5), new("Entradas", TipoColuna.Valor, 3),
             new("Notas de saída", TipoColuna.Inteiro, 2.5), new("Saídas", TipoColuna.Valor, 3), new("Já contabilizado", TipoColuna.Valor, 3)], res,
            "Valor contábil dos itens das notas autorizadas. \"Já contabilizado\" é o que está nas notas contabilizadas: no Contábil, veja Relatórios › Gerenciais › Contabilização por centro de custo.");
    }
}

/// <summary>Relatórios › Notas por centro de custo (FISCAL_RELATORIOS): valor contábil dos itens das notas autorizadas do período por centro.</summary>
public sealed class ServicoNotasPorCentro(Npgsql.NpgsqlDataSource ds, Vorticis.Application.Seguranca.Autorizador autorizador)
{
    public async Task<Vorticis.Domain.Comum.Resultado<IReadOnlyList<LinhaCentroCusto>>> ListarAsync(long empresa, DateOnly inicio, DateOnly fim, long usuario,
                                                                                                CancellationToken ct = default)
    {
        if (await Vorticis.Application.Seguranca.AutorizadorExtensions.NegarAsync<IReadOnlyList<LinhaCentroCusto>>(autorizador, usuario, empresa,
                Vorticis.Domain.Seguranca.Permissao.FiscalRelatorios, ct) is { } negado) return negado;
        if (fim < inicio) return Vorticis.Domain.Comum.Resultado<IReadOnlyList<LinhaCentroCusto>>.Falha("Data final anterior à inicial.");
        await using var cmd = ds.CreateCommand("""
            SELECT cc.codigo, cc.descricao,
                   count(DISTINCT d.id) FILTER (WHERE d.tipo_operacao = 'E')::int, COALESCE(sum(i.valor_contabil) FILTER (WHERE d.tipo_operacao = 'E'), 0),
                   count(DISTINCT d.id) FILTER (WHERE d.tipo_operacao = 'S')::int, COALESCE(sum(i.valor_contabil) FILTER (WHERE d.tipo_operacao = 'S'), 0),
                   COALESCE(sum(i.valor_contabil) FILTER (WHERE d.lancamento_id IS NOT NULL), 0)
              FROM fiscal.documento d
              JOIN fiscal.documento_item i ON i.documento_id = d.id
              LEFT JOIN contabil.centro_custo cc ON cc.id = COALESCE(i.centro_custo_id, d.centro_custo_id)
             WHERE d.empresa_id = @e AND d.situacao = 'AUTORIZADA' AND d.data_entrada_saida BETWEEN @i AND @f
             GROUP BY cc.codigo, cc.descricao
             ORDER BY cc.codigo NULLS LAST
            """).Com("e", empresa).Com("i", inicio).Com("f", fim);
        var res = new List<LinhaCentroCusto>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            res.Add(new LinhaCentroCusto(r.IsDBNull(0) ? null : r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1), r.GetInt32(2), r.GetDecimal(3),
                                         r.GetInt32(4), r.GetDecimal(5), r.GetDecimal(6)));
        return Vorticis.Domain.Comum.Resultado<IReadOnlyList<LinhaCentroCusto>>.Ok(res);
    }
}
