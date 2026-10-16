using Perseus.Domain.Contabil;

namespace Perseus.Application.Contabil;

public sealed class ServicoRelatorios(IConsultasContabeis consultas)
{
    public async Task<IReadOnlyList<LinhaBalancete>> BalanceteAsync(long empresaId, DateOnly inicio, DateOnly fim,
                                                                    bool incluirZeradas = false, CancellationToken ct = default)
    {
        if (fim < inicio) throw new ArgumentException("Data final anterior à inicial.");
        var plano = await consultas.ObterPlanoAsync(empresaId, ct);
        var movimentos = await consultas.ObterMovimentosAsync(empresaId, inicio, fim, ct);
        return Balancete.Montar(plano, movimentos, incluirZeradas);
    }

    public Task<RazaoConta?> RazaoAsync(long empresaId, string codigoConta, DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => consultas.ObterRazaoAsync(empresaId, codigoConta, inicio, fim, ct);

    public Task<IReadOnlyList<LinhaDiario>> DiarioAsync(long empresaId, DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => consultas.ObterDiarioAsync(empresaId, inicio, fim, ct);
}
