using Perseus.Application.Seguranca;
using Perseus.Domain.Comum;
using Perseus.Domain.Contabil;
using Perseus.Domain.Seguranca;

namespace Perseus.Application.Contabil;

public sealed class ServicoRelatorios(IConsultasContabeis consultas, Autorizador autorizador)
{
    public async Task<Resultado<IReadOnlyList<LinhaBalancete>>> BalanceteAsync(long empresaId, DateOnly inicio, DateOnly fim, long usuarioId,
                                                                              bool incluirZeradas = false, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<IReadOnlyList<LinhaBalancete>>(usuarioId, empresaId, Permissao.RelatorioBalancete, ct) is { } negado) return negado;
        if (fim < inicio) return Resultado<IReadOnlyList<LinhaBalancete>>.Falha("Data final anterior à inicial.");
        var plano = await consultas.ObterPlanoAsync(empresaId, ct);
        var movimentos = await consultas.ObterMovimentosAsync(empresaId, inicio, fim, ct);
        return Resultado<IReadOnlyList<LinhaBalancete>>.Ok(Balancete.Montar(plano, movimentos, incluirZeradas));
    }

    public async Task<Resultado<RazaoConta>> RazaoAsync(long empresaId, string codigoConta, DateOnly inicio, DateOnly fim, long usuarioId,
                                                       CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<RazaoConta>(usuarioId, empresaId, Permissao.RelatorioRazao, ct) is { } negado) return negado;
        try
        {
            return await consultas.ObterRazaoAsync(empresaId, codigoConta, inicio, fim, ct) is { } razao
                ? Resultado<RazaoConta>.Ok(razao)
                : Resultado<RazaoConta>.Falha($"Conta {codigoConta} não encontrada.");
        }
        catch (ArgumentException ex)
        {
            return Resultado<RazaoConta>.Falha(ex.Message);
        }
    }

    public async Task<Resultado<IReadOnlyList<LinhaDiario>>> DiarioAsync(long empresaId, DateOnly inicio, DateOnly fim, long usuarioId,
                                                                        CancellationToken ct = default)
    {
        // A tela de estorno lista o Diário: quem pode estornar também pode consultá-lo.
        var p = await autorizador.ObterAsync(usuarioId, empresaId, ct);
        if (!p.Pode(Permissao.RelatorioDiario) && !p.Pode(Permissao.LancamentoEstornar))
            return await autorizador.NegarAsync<IReadOnlyList<LinhaDiario>>(usuarioId, empresaId, Permissao.RelatorioDiario, ct)
                   ?? Resultado<IReadOnlyList<LinhaDiario>>.Falha("Acesso negado.");
        return Resultado<IReadOnlyList<LinhaDiario>>.Ok(await consultas.ObterDiarioAsync(empresaId, inicio, fim, ct));
    }
}
