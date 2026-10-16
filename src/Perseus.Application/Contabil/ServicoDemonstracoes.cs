using Perseus.Application.Seguranca;
using Perseus.Domain.Comum;
using Perseus.Domain.Contabil;
using Perseus.Domain.Seguranca;

namespace Perseus.Application.Contabil;

public sealed class ServicoDemonstracoes(IConsultasContabeis consultas, Autorizador autorizador)
{
    /// <summary>Balanço Patrimonial na data (saldos acumulados até ela).</summary>
    public async Task<Resultado<BalancoPatrimonial>> BalancoAsync(long empresaId, DateOnly data, short nivelMaximo, long usuarioId,
                                                                 CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<BalancoPatrimonial>(usuarioId, empresaId, Permissao.RelatorioDemonstracoes, ct) is { } negado) return negado;
        var plano = await consultas.ObterPlanoAsync(empresaId, ct);
        var saldos = await consultas.ObterMovimentosAsync(empresaId, data.AddDays(1), data, ct);   // saldo anterior ao dia seguinte = saldo na data
        var balancete = Balancete.Montar(plano, saldos.Select(s => s with { Debitos = 0, Creditos = 0 }).ToArray());
        return Resultado<BalancoPatrimonial>.Ok(Demonstracoes.MontarBalanco(data, plano, balancete, nivelMaximo));
    }

    /// <summary>DRE do período, desconsiderando os lançamentos de encerramento (que zeram as contas de resultado).</summary>
    public async Task<Resultado<DemonstracaoResultado>> DreAsync(long empresaId, DateOnly inicio, DateOnly fim, short nivelMaximo, long usuarioId,
                                                                CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<DemonstracaoResultado>(usuarioId, empresaId, Permissao.RelatorioDemonstracoes, ct) is { } negado) return negado;
        if (fim < inicio) return Resultado<DemonstracaoResultado>.Falha("Data final anterior à inicial.");
        var plano = await consultas.ObterPlanoAsync(empresaId, ct);
        var movimentos = await consultas.ObterMovimentosAsync(empresaId, inicio, fim, ct, excluirEncerramento: true);
        var balancete = Balancete.Montar(plano, movimentos.Select(m => m with { SaldoAnterior = 0 }).ToArray());
        return Resultado<DemonstracaoResultado>.Ok(Demonstracoes.MontarDre(inicio, fim, plano, balancete, nivelMaximo));
    }
}
