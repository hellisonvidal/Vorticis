using Perseus.Domain.Comum;
using Perseus.Domain.Contabil;

namespace Perseus.Application.Contabil;

/// <summary>
/// Único ponto de gravação de lançamentos contábeis. Módulos integradores (fiscal, banco, folha)
/// produzem <see cref="LancamentoProposto"/> e chamam <see cref="EfetivarAsync"/>.
/// </summary>
public sealed class ServicoLancamento(IRepositorioContabil repositorio)
{
    public async Task<Resultado<LancamentoEfetivado>> EfetivarAsync(LancamentoProposto proposta, long? usuarioId, CancellationToken ct = default)
    {
        var ctx = await CarregarContextoAsync(proposta, ct);
        var validacao = ValidadorLancamento.Validar(proposta, ctx);
        if (!validacao.Sucesso) return Resultado<LancamentoEfetivado>.Falha(validacao.Erros);

        try
        {
            return Resultado<LancamentoEfetivado>.Ok(await repositorio.GravarEfetivadoAsync(validacao.Valor, usuarioId, ct));
        }
        catch (ViolacaoRegraException ex)
        {
            return Resultado<LancamentoEfetivado>.Falha(ex.Message);
        }
    }

    public async Task<Resultado<LancamentoEfetivado>> EstornarAsync(long empresaId, long lancamentoId, DateOnly data, string motivo,
                                                                   long? usuarioId, CancellationToken ct = default)
    {
        var original = await repositorio.ObterLancamentoAsync(empresaId, lancamentoId, ct);
        if (original is null) return Resultado<LancamentoEfetivado>.Falha("Lançamento não encontrado.");

        var estorno = Estorno.Gerar(original, data, motivo);
        if (!estorno.Sucesso) return Resultado<LancamentoEfetivado>.Falha(estorno.Erros);

        return await EfetivarAsync(estorno.Valor, usuarioId, ct);
    }

    private async Task<ContextoValidacao> CarregarContextoAsync(LancamentoProposto p, CancellationToken ct)
    {
        var contas = p.Partidas.Select(x => x.CodigoConta).Distinct().ToArray();
        var centros = p.Partidas.Select(x => x.CodigoCentroCusto).OfType<string>().Where(s => s.Length > 0).Distinct().ToArray();
        var participantes = p.Partidas.Select(x => x.CodigoParticipante).OfType<string>().Where(s => s.Length > 0).Distinct().ToArray();

        return new ContextoValidacao(
            await repositorio.ObterContasAsync(p.EmpresaId, contas, ct),
            centros.Length == 0 ? new Dictionary<string, long>() : await repositorio.ObterCentrosCustoAsync(p.EmpresaId, centros, ct),
            participantes.Length == 0 ? new Dictionary<string, long>() : await repositorio.ObterParticipantesAsync(p.EmpresaId, participantes, ct),
            await repositorio.ObterPeriodoAsync(p.EmpresaId, p.Data, ct));
    }
}
