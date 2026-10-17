using Perseus.Domain.Contabil;

namespace Perseus.Application.Contabil;

/// <summary>Persistência do módulo contábil. A implementação deve gravar cada operação em uma única transação.</summary>
public interface IRepositorioContabil
{
    Task<PeriodoInfo?> ObterPeriodoAsync(long empresaId, DateOnly data, CancellationToken ct);
    Task<IReadOnlyDictionary<string, ContaInfo>> ObterContasAsync(long empresaId, IEnumerable<string> codigos, CancellationToken ct);
    Task<IReadOnlyDictionary<string, long>> ObterCentrosCustoAsync(long empresaId, IEnumerable<string> codigos, CancellationToken ct);
    Task<IReadOnlyDictionary<string, long>> ObterParticipantesAsync(long empresaId, IEnumerable<string> codigos, CancellationToken ct);
    Task<LancamentoRegistrado?> ObterLancamentoAsync(long empresaId, long lancamentoId, CancellationToken ct);

    /// <summary>
    /// Grava cabeçalho e partidas, atribui o próximo número do exercício e efetiva.
    /// Se <see cref="LancamentoValidado.EstornoDeId"/> estiver preenchido, marca o original como ESTORNADO
    /// na mesma transação. Lança <c>ViolacaoRegraException</c> se o banco rejeitar.
    /// </summary>
    Task<LancamentoEfetivado> GravarEfetivadoAsync(LancamentoValidado lancamento, long? usuarioId, CancellationToken ct);

    /// <summary>Grava vários lançamentos na mesma transação (tudo ou nada), numerados em sequência.</summary>
    /// <param name="chaves">Origem de cada lançamento importado (mesma posição), gravada para evitar duplicidade; null = sem controle.</param>
    Task<IReadOnlyList<LancamentoEfetivado>> GravarEfetivadosAsync(IReadOnlyList<LancamentoValidado> lancamentos, long? usuarioId, CancellationToken ct,
                                                                  IReadOnlyList<string?>? chaves = null);

    /// <summary>Lançamentos efetivados (não estornados) de uma origem no exercício, com as partidas.</summary>
    Task<IReadOnlyList<LancamentoRegistrado>> ListarLancamentosAtivosAsync(long empresaId, long exercicioId, OrigemLancamento origem, CancellationToken ct);

    Task<IReadOnlyList<CompetenciaStatus>> ListarPeriodosAsync(long empresaId, int ano, CancellationToken ct);

    /// <summary>Número do lançamento efetivado (não estornado) com a origem dada no exercício, se houver.</summary>
    Task<long?> ObterNumeroLancamentoAtivoAsync(long empresaId, long exercicioId, OrigemLancamento origem, CancellationToken ct);
    Task<IReadOnlyList<Inconsistencia>> VerificarInvariantesAsync(long empresaId, DateOnly competencia, CancellationToken ct);
    Task AlterarStatusPeriodoAsync(long empresaId, DateOnly competencia, bool fechar, long? usuarioId, CancellationToken ct);
}

public sealed record CompetenciaStatus(DateOnly Competencia, bool Aberto);

public sealed record Inconsistencia(string Codigo, string Descricao);

/// <summary>Consultas de leitura (relatórios). Não passam pelas regras de escrita.</summary>
public interface IConsultasContabeis
{
    Task<IReadOnlyList<ContaPlano>> ObterPlanoAsync(long empresaId, CancellationToken ct);

    /// <summary>Saldo anterior a <paramref name="inicio"/> e movimento no intervalo, por conta analítica.</summary>
    /// <param name="excluirEncerramento">Ignora lançamentos de encerramento (IND_LCTO = E) no movimento do intervalo — usado na DRE.</param>
    Task<IReadOnlyList<MovimentoConta>> ObterMovimentosAsync(long empresaId, DateOnly inicio, DateOnly fim, CancellationToken ct,
                                                             bool excluirEncerramento = false);

    Task<RazaoConta?> ObterRazaoAsync(long empresaId, string codigoConta, DateOnly inicio, DateOnly fim, CancellationToken ct);
    /// <summary>Partidas dos lançamentos do período (para as mutações do PL), marcando as de encerramento.</summary>
    Task<IReadOnlyList<PartidaMutacao>> ObterPartidasAsync(long empresaId, DateOnly inicio, DateOnly fim, CancellationToken ct);
    /// <summary>Saldo anterior e movimento por conta e participante (só partidas com participante).</summary>
    Task<IReadOnlyList<MovimentoParticipante>> ObterMovimentosParticipantesAsync(long empresaId, DateOnly inicio, DateOnly fim, CancellationToken ct);
    Task<IReadOnlyList<LinhaDiario>> ObterDiarioAsync(long empresaId, DateOnly inicio, DateOnly fim, CancellationToken ct);
}

public sealed record LinhaRazao(DateOnly Data, long Numero, string Historico, IndicadorDC DC, decimal Valor, decimal SaldoAcumulado, bool Estornado);

public sealed record RazaoConta(string Codigo, string Descricao, decimal SaldoAnterior, IReadOnlyList<LinhaRazao> Linhas)
{
    public decimal SaldoFinal => Linhas.Count == 0 ? SaldoAnterior : Linhas[^1].SaldoAcumulado;
}

public sealed record LinhaDiario(long LancamentoId, long Numero, DateOnly Data, short Sequencia, string CodigoConta, string DescricaoConta,
                                 IndicadorDC DC, decimal Valor, string Historico, StatusLancamento Status, string? Documento = null,
                                 string? CentroCusto = null, string? Participante = null);
