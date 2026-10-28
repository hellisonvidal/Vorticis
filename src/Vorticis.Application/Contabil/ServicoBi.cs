using Vorticis.Application.Relatorios;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Contabil;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Application.Contabil;

/// <summary>
/// Ferramentas de BI do Contábil: painel de indicadores, análise de indicadores, cubo (tabela dinâmica) e dados para BI externo.
/// Todas exigem a permissão de demonstrações (RELATORIO_DEMONSTRACOES); a exportação para arquivos exige também a de exportar relatórios.
/// </summary>
public sealed class ServicoBi(IConsultasContabeis consultas, ServicoFluxos fluxos, Autorizador autorizador)
{
    private const int MesesDaSerie = 12;

    private async Task<(IReadOnlyList<Indicador> Indicadores, DemonstracaoEbitda Resultado)?> CalcularAsync(long empresaId, DateOnly inicio, DateOnly fim,
        IReadOnlyList<ContaPlano> plano, long usuarioId, CancellationToken ct)
    {
        var e = await fluxos.EbitdaAsync(empresaId, inicio, fim, usuarioId, ct);
        if (!e.Sucesso) return null;
        var saldos = (await consultas.ObterMovimentosAsync(empresaId, fim.AddDays(1), fim, ct)).ToDictionary(m => m.ContaId, m => m.SaldoAnterior);
        var posicao = CalculoBi.Posicao(plano, saldos);
        return (CalculoBi.Indicadores(posicao, e.Valor, fim.DayNumber - inicio.DayNumber + 1), e.Valor);
    }

    public async Task<Resultado<PainelBi>> PainelAsync(long empresaId, DateOnly inicio, DateOnly fim, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<PainelBi>(usuarioId, empresaId, Permissao.RelatorioDemonstracoes, ct) is { } negado) return negado;
        if (fim < inicio) return Resultado<PainelBi>.Falha("Data final anterior à inicial.");
        var plano = await consultas.ObterPlanoAsync(empresaId, ct);
        var ant = Comparativo.Anterior(inicio, fim);
        var atual = await CalcularAsync(empresaId, inicio, fim, plano, usuarioId, ct);
        var anterior = await CalcularAsync(empresaId, ant.Inicio, ant.Fim, plano, usuarioId, ct);
        if (atual is null || anterior is null) return Resultado<PainelBi>.Falha("Não foi possível apurar o resultado do período.");

        var centros = await consultas.ObterCentrosAsync(empresaId, ct);
        var movs = await consultas.ObterMovimentosCentroAsync(empresaId, inicio, fim, ct);
        var primeiro = new DateOnly(fim.Year, fim.Month, 1).AddMonths(-(MesesDaSerie - 1));
        var movsSerie = await consultas.ObterMovimentosCentroAsync(empresaId, primeiro, fim, ct);
        return Resultado<PainelBi>.Ok(new PainelBi(inicio, fim, ant.Inicio, ant.Fim, atual.Value.Indicadores, anterior.Value.Indicadores, atual.Value.Resultado,
            CalculoBi.Serie(plano, movsSerie, primeiro, new DateOnly(fim.Year, fim.Month, 1)), CalculoBi.DespesasPorGrupo(plano, movs),
            CalculoBi.CustosPorCentro(plano, centros, movs), CalculoBi.Cascata(atual.Value.Resultado)));
    }

    /// <summary>Tabela com todos os indicadores (grupo, fórmula e interpretação) do período e, se pedido, dos períodos comparados.</summary>
    public async Task<Resultado<TabelaRelatorio>> AnaliseAsync(long empresaId, DateOnly inicio, DateOnly fim, OpcoesComparativo op, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<TabelaRelatorio>(usuarioId, empresaId, Permissao.RelatorioDemonstracoes, ct) is { } negado) return negado;
        if (fim < inicio) return Resultado<TabelaRelatorio>.Falha("Data final anterior à inicial.");
        if (op.TemPeriodos && Comparativo.Validar(op) is { } erro) return Resultado<TabelaRelatorio>.Falha(erro);
        var plano = await consultas.ObterPlanoAsync(empresaId, ct);
        var dados = new List<DadosPeriodo>();
        foreach (var p in Comparativo.Periodos(inicio, fim, op))
        {
            var r = await CalcularAsync(empresaId, p.Inicio, p.Fim, plano, usuarioId, ct);
            if (r is null) return Resultado<TabelaRelatorio>.Falha($"{p.Rotulo}: não foi possível apurar o resultado do período.");
            var linhas = new List<LinhaComparavel>();
            string? grupo = null;
            foreach (var i in r.Value.Indicadores)
            {
                if (i.Grupo != grupo) { grupo = i.Grupo; linhas.Add(new("g." + grupo, [grupo, "", ""], null, true)); }
                linhas.Add(new(i.Chave, [i.Nome, i.Formula, i.Interpretacao], i.Valor, false, 0, i.Formato != FormatoIndicador.Moeda));
            }
            dados.Add(new DadosPeriodo(p, linhas));
        }
        var colunas = new List<ColunaRelatorio> { new("Indicador", LarguraCm: 4.2), new("Fórmula", LarguraCm: 5), new("Como ler", LarguraCm: 6) };
        var tabela = Comparativo.Montar("Análise de indicadores", colunas.ToList(), dados, op with { AnaliseVertical = false },
            "Razões em vezes (1,50 = 1,5 vez), percentuais em %, prazos em dias. Variação dos indicadores em pontos (atual − comparado). Sem valor (—) quando faltam dados para calcular.");
        // Os indicadores não são só valores em R$: a variação é na unidade de cada indicador.
        return Resultado<TabelaRelatorio>.Ok(tabela with { Colunas = tabela.Colunas.Select(c => c.Titulo.StartsWith("Var. R$") ? c with { Titulo = "Variação" + c.Titulo["Var. R$".Length..] } : c).ToList() });
    }

    /// <summary>Carrega partidas, plano, centros e participantes do período para o cubo.</summary>
    public async Task<Resultado<DadosBi>> CarregarAsync(long empresaId, DateOnly inicio, DateOnly fim, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<DadosBi>(usuarioId, empresaId, Permissao.RelatorioDemonstracoes, ct) is { } negado) return negado;
        if (fim < inicio) return Resultado<DadosBi>.Falha("Data final anterior à inicial.");
        return Resultado<DadosBi>.Ok(new DadosBi(await consultas.ObterPlanoAsync(empresaId, ct), await consultas.ObterCentrosAsync(empresaId, ct),
            await consultas.ObterParticipantesAsync(empresaId, ct), await consultas.ObterPartidasBiAsync(empresaId, inicio, fim, ct)));
    }

    /// <summary>Cubo do período: monta a tabela dinâmica (para exibir e exportar).</summary>
    public async Task<Resultado<(DadosBi Dados, CuboResultado Cubo)>> CuboAsync(long empresaId, DateOnly inicio, DateOnly fim, ConsultaCubo consulta, long usuarioId, CancellationToken ct = default)
    {
        var d = await CarregarAsync(empresaId, inicio, fim, usuarioId, ct);
        return d.Sucesso ? Resultado<(DadosBi, CuboResultado)>.Ok((d.Valor, Domain.Contabil.Cubo.Montar(d.Valor, consulta))) : Resultado<(DadosBi, CuboResultado)>.Falha(d.Erros);
    }

    /// <summary>Fatos e dimensões prontos para o Power BI/Excel: partidas, plano de contas, centros de custo, participantes e calendário.</summary>
    public async Task<Resultado<IReadOnlyList<TabelaRelatorio>>> TabelasParaBiAsync(long empresaId, DateOnly inicio, DateOnly fim, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<IReadOnlyList<TabelaRelatorio>>(usuarioId, empresaId, Permissao.RelatorioExportar, ct) is { } negado) return negado;
        var d = await CarregarAsync(empresaId, inicio, fim, usuarioId, ct);
        return d.Sucesso ? Resultado<IReadOnlyList<TabelaRelatorio>>.Ok(TabelasBi.Montar(d.Valor, inicio, fim)) : Resultado<IReadOnlyList<TabelaRelatorio>>.Falha(d.Erros);
    }
}
