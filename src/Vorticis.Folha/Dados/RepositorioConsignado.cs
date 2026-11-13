using Npgsql;
using Vorticis.Folha.Regras;

namespace Vorticis.Folha.Dados;

/// <summary>Contratos de empréstimo consignado e as parcelas descontadas (folha.consignado e folha.consignado_parcela).</summary>
public sealed class RepositorioConsignado(NpgsqlDataSource ds)
{
    public async Task<IReadOnlyList<ContratoConsignado>> ListarAsync(long empresaId, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT c.id, c.funcionario_id, c.instituicao, c.contrato, c.valor_parcela, c.total_parcelas, c.primeira_competencia, c.situacao, c.observacao,
                   f.nome, f.matricula, (SELECT count(*) FROM folha.consignado_parcela p WHERE p.consignado_id = c.id),
                   (SELECT max(p.competencia) FROM folha.consignado_parcela p WHERE p.consignado_id = c.id)
              FROM folha.consignado c JOIN folha.funcionario f ON f.id = c.funcionario_id
             WHERE c.empresa_id = @e
             ORDER BY c.situacao <> 'ATIVO', f.nome, c.primeira_competencia
            """).Com("e", empresaId);
        var res = new List<ContratoConsignado>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            res.Add(new ContratoConsignado(r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetDecimal(4), r.GetInt16(5), r.GetFieldValue<DateOnly>(6),
                                           r.GetString(7), r.Texto(8), r.GetString(9), r.GetString(10), (int)r.GetInt64(11), r.Data(12)));
        return res;
    }

    public Task<long> GravarAsync(long empresaId, ContratoConsignado c, CancellationToken ct) => Sql.TraduzirAsync(async () =>
    {
        await using var cmd = ds.CreateCommand(c.Id == 0
            ? """
              INSERT INTO folha.consignado (empresa_id, funcionario_id, instituicao, contrato, valor_parcela, total_parcelas, primeira_competencia, situacao, observacao)
              SELECT @e, f.id, @i, @c, @v, @t, @p, @s, @o FROM folha.funcionario f WHERE f.id = @f AND f.empresa_id = @e RETURNING id
              """
            : """
              UPDATE folha.consignado SET funcionario_id = @f, instituicao = @i, contrato = @c, valor_parcela = @v, total_parcelas = @t,
                     primeira_competencia = @p, situacao = @s, observacao = @o
               WHERE id = @id AND empresa_id = @e RETURNING id
              """).Com("e", empresaId).Com("id", c.Id).Com("f", c.FuncionarioId).Com("i", c.Instituicao.Trim()).Com("c", c.Contrato.Trim())
            .Com("v", c.ValorParcela).Com("t", (short)c.TotalParcelas).Com("p", c.PrimeiraCompetencia).Com("s", c.Situacao).ComTexto("o", c.Observacao);
        return await cmd.ExecuteScalarAsync(ct) is long id ? id : throw new Vorticis.Domain.Comum.ViolacaoRegraException("Contrato ou funcionário não encontrado.");
    });

    public async Task<bool> ExcluirAsync(long empresaId, long id, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            DELETE FROM folha.consignado c WHERE c.id = @id AND c.empresa_id = @e
               AND NOT EXISTS (SELECT 1 FROM folha.consignado_parcela p WHERE p.consignado_id = c.id)
            """).Com("id", id).Com("e", empresaId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task RegistrarParcelaAsync(long consignadoId, DateOnly competencia, int numero, decimal valor, long eventoId, bool quitou, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            INSERT INTO folha.consignado_parcela (consignado_id, competencia, numero, valor, evento_id) VALUES (@c, @m, @n, @v, @ev);
            UPDATE folha.consignado SET situacao = CASE WHEN @q THEN 'QUITADO' ELSE situacao END WHERE id = @c;
            """).Com("c", consignadoId).Com("m", competencia).Com("n", (short)numero).Com("v", valor).Com("ev", eventoId).Com("q", quitou);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Percentuais da margem consignável vigentes na data (core.parametro_legal CONSIGNADO_MARGEM).</summary>
    public async Task<(decimal Emprestimo, decimal Cartoes)> PercentuaisAsync(DateOnly data, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT valor::text FROM core.parametro_legal WHERE chave = 'CONSIGNADO_MARGEM' AND vigencia_inicio <= @d AND (vigencia_fim IS NULL OR vigencia_fim >= @d)
             ORDER BY vigencia_inicio DESC LIMIT 1
            """).Com("d", data);
        if (await cmd.ExecuteScalarAsync(ct) is not string json) return (35m, 10m);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return (doc.RootElement.GetProperty("margemEmprestimo").GetDecimal(), doc.RootElement.GetProperty("margemCartoes").GetDecimal());
    }
}
