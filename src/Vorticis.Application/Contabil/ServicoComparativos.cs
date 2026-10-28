using Vorticis.Application.Relatorios;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Contabil;

namespace Vorticis.Application.Contabil;

/// <summary>
/// Opção "Comparativo" de todos os relatórios contábeis. Cada método busca o relatório em cada período pelos serviços de sempre
/// (mesmas permissões e regras) e entrega as linhas a <see cref="Comparativo"/>, que monta as colunas de período, variação e
/// análise vertical de um jeito só para todos.
/// </summary>
public sealed class ServicoComparativos(ServicoRelatorios relatorios, ServicoDemonstracoes demonstracoes, ServicoGerenciais gerenciais, ServicoFluxos fluxos)
{
    private static readonly IReadOnlyList<ColunaRelatorio> CodigoDescricao = [new("Código", LarguraCm: 3.2), new("Descrição")];
    private static readonly IReadOnlyList<ColunaRelatorio> SoDescricao = [new("Descrição")];

    private static async Task<Resultado<TabelaRelatorio>> MontarAsync<T>(string titulo, IReadOnlyList<ColunaRelatorio> colunas, IReadOnlyList<PeriodoRelatorio> periodos,
        OpcoesComparativo op, Func<PeriodoRelatorio, Task<Resultado<T>>> obter, Func<T, (IReadOnlyList<LinhaComparavel> Linhas, decimal? Base)> linhas, string? rodape = null)
    {
        if (Comparativo.Validar(op) is { } erro) return Resultado<TabelaRelatorio>.Falha(erro);
        var dados = new List<DadosPeriodo>();
        foreach (var p in periodos)
        {
            var r = await obter(p);
            if (!r.Sucesso) return Resultado<TabelaRelatorio>.Falha(r.Erros.Select(e => periodos.Count > 1 ? $"{p.Rotulo}: {e}" : e).ToList());
            var (l, b) = linhas(r.Valor);
            dados.Add(new DadosPeriodo(p, l, b));
        }
        return Resultado<TabelaRelatorio>.Ok(Comparativo.Montar(titulo, colunas, dados, op, rodape));
    }

    private static LinhaComparavel Cd(string chave, string codigo, string descricao, decimal? valor, bool destaque = false, int recuo = 0) =>
        new(chave, [codigo, descricao], valor, destaque, recuo);

    public Task<Resultado<TabelaRelatorio>> BalanceteAsync(long empresaId, DateOnly inicio, DateOnly fim, OpcoesComparativo op, bool incluirZeradas, long usuarioId, CancellationToken ct = default) =>
        MontarAsync("Balancete de verificação — saldo final (D+ / C−)", CodigoDescricao, Comparativo.Periodos(inicio, fim, op), op,
            p => relatorios.BalanceteAsync(empresaId, p.Inicio, p.Fim, usuarioId, incluirZeradas, ct),
            (IReadOnlyList<LinhaBalancete> l) => (l.Select(x => Cd(x.Codigo, x.Codigo, x.Descricao, x.SaldoFinal, !x.Analitica, x.Nivel - 1)).ToList(), null),
            "Saldo final positivo = devedor (D); negativo = credor (C).");

    public Task<Resultado<TabelaRelatorio>> BalancoAsync(long empresaId, DateOnly data, short nivel, OpcoesComparativo op, long usuarioId, CancellationToken ct = default) =>
        MontarAsync("Balanço Patrimonial", CodigoDescricao, Comparativo.Periodos(data, data, op, pontual: true), op,
            p => demonstracoes.BalancoAsync(empresaId, p.Fim, nivel, usuarioId, ct),
            (BalancoPatrimonial b) =>
            {
                var l = new List<LinhaComparavel> { new("t.a", ["", "ATIVO"], null, true) };
                l.AddRange(b.Ativo.Select(x => Cd("A|" + (x.Codigo.Length > 0 ? x.Codigo : x.Descricao), x.Codigo, x.Descricao, x.Valor, x.Tipo != TipoLinhaDemonstracao.Conta, x.Nivel - 1)));
                l.Add(new("t.p", ["", "PASSIVO E PATRIMÔNIO LÍQUIDO"], null, true));
                l.AddRange(b.PassivoPl.Select(x => Cd("P|" + (x.Codigo.Length > 0 ? x.Codigo : x.Descricao), x.Codigo, x.Descricao, x.Valor, x.Tipo != TipoLinhaDemonstracao.Conta, x.Nivel - 1)));
                return (l, b.TotalAtivo);
            });

    public Task<Resultado<TabelaRelatorio>> DreAsync(long empresaId, DateOnly inicio, DateOnly fim, short nivel, OpcoesComparativo op, long usuarioId, CancellationToken ct = default) =>
        MontarAsync("Demonstração do Resultado do Exercício", CodigoDescricao, Comparativo.Periodos(inicio, fim, op), op,
            p => demonstracoes.DreAsync(empresaId, p.Inicio, p.Fim, nivel, usuarioId, ct),
            (DemonstracaoResultado d) => (d.Linhas.Select(x => Cd(x.Tipo == TipoLinhaDemonstracao.Total ? "total" : x.Codigo, x.Codigo, x.Descricao, x.Valor,
                                                                    x.Tipo != TipoLinhaDemonstracao.Conta, x.Nivel - 1)).ToList(),
                                          d.Linhas.Where(x => x.Tipo != TipoLinhaDemonstracao.Total && x.Nivel == 1 && x.Valor > 0).Sum(x => x.Valor)),
            "AV % = participação na receita (soma dos grupos de resultado de natureza credora).");

    public Task<Resultado<TabelaRelatorio>> DmplAsync(long empresaId, DateOnly inicio, DateOnly fim, OpcoesComparativo op, long usuarioId, CancellationToken ct = default) =>
        MontarAsync("DMPL — total do patrimônio líquido", SoDescricao, Comparativo.Periodos(inicio, fim, op), op,
            p => demonstracoes.MutacoesAsync(empresaId, p.Inicio, p.Fim, usuarioId, ct),
            (DemonstracaoMutacoes d) => (d.Dmpl.Select(x => new LinhaComparavel(Comparativo.ChaveSemDatas(x.Descricao), [x.Descricao], x.Total, x.Saldo)).ToList(), null),
            "No comparativo, a DMPL mostra a coluna Total de cada linha.");

    public Task<Resultado<TabelaRelatorio>> DlpaAsync(long empresaId, DateOnly inicio, DateOnly fim, OpcoesComparativo op, long usuarioId, CancellationToken ct = default) =>
        MontarAsync("DLPA — Demonstração de Lucros ou Prejuízos Acumulados", SoDescricao, Comparativo.Periodos(inicio, fim, op), op,
            p => demonstracoes.MutacoesAsync(empresaId, p.Inicio, p.Fim, usuarioId, ct),
            (DemonstracaoMutacoes d) => (d.Dlpa.Select(x => new LinhaComparavel(Comparativo.ChaveSemDatas(x.Descricao), [x.Descricao], x.SemValor ? null : x.Valor, x.Destaque)).ToList(), null));

    public Task<Resultado<TabelaRelatorio>> DfcAsync(long empresaId, DateOnly inicio, DateOnly fim, bool direto, short nivel, OpcoesComparativo op, long usuarioId, CancellationToken ct = default) =>
        MontarAsync(TabelasFluxos.TituloDfc, SoDescricao, Comparativo.Periodos(inicio, fim, op), op,
            p => fluxos.DfcAsync(empresaId, p.Inicio, p.Fim, direto, nivel, usuarioId, ct),
            (DfcApurada a) => (TabelasFluxos.LinhasDfc(a.Demonstracao), null),
            direto ? "Método direto, com a conciliação pelo método indireto." : "Método indireto (CPC 03).");

    public Task<Resultado<TabelaRelatorio>> EbitdaAsync(long empresaId, DateOnly inicio, DateOnly fim, OpcoesComparativo op, long usuarioId, CancellationToken ct = default) =>
        MontarAsync(TabelasFluxos.TituloEbitda, SoDescricao, Comparativo.Periodos(inicio, fim, op), op,
            p => fluxos.EbitdaAsync(empresaId, p.Inicio, p.Fim, usuarioId, ct),
            (DemonstracaoEbitda e) => (TabelasFluxos.LinhasEbitda(e), e.ReceitaLiquida),
            "EBITDA = lucro operacional + depreciação e amortização. AV % = participação na receita líquida.");

    /// <summary>Razão resumido da conta: saldo anterior, débitos, créditos e saldo final de cada período.</summary>
    public Task<Resultado<TabelaRelatorio>> RazaoResumidoAsync(long empresaId, string conta, DateOnly inicio, DateOnly fim, OpcoesComparativo op, long usuarioId, CancellationToken ct = default) =>
        MontarAsync($"Razão resumido — conta {conta}", SoDescricao, Comparativo.Periodos(inicio, fim, op), op,
            p => relatorios.RazaoAsync(empresaId, conta, p.Inicio, p.Fim, usuarioId, ct),
            (RazaoConta r) => (new List<LinhaComparavel>
            {
                new("ant", ["Saldo anterior (D+ / C−)"], r.SaldoAnterior),
                new("deb", ["Débitos do período"], r.Linhas.Where(l => l.DC == IndicadorDC.D).Sum(l => l.Valor)),
                new("cred", ["Créditos do período"], r.Linhas.Where(l => l.DC == IndicadorDC.C).Sum(l => l.Valor)),
                new("fim", ["Saldo final (D+ / C−)"], r.SaldoFinal, true),
            }, null));

    /// <summary>Contabilização por centro de custo (sem o detalhe dos lançamentos): saldo (débitos − créditos) de cada centro e conta.</summary>
    public Task<Resultado<TabelaRelatorio>> CentroCustoAsync(long empresaId, DateOnly inicio, DateOnly fim, long? centroId, string? prefixo, int nivel, OpcoesComparativo op, long usuarioId, CancellationToken ct = default) =>
        MontarAsync(TabelasGerenciais.TituloCentroCusto + " — saldo (D+ / C−)", CodigoDescricao, Comparativo.Periodos(inicio, fim, op), op,
            p => gerenciais.PorCentroCustoAsync(empresaId, new FiltroCentroCusto(p.Inicio, p.Fim, centroId, prefixo, nivel, false), usuarioId, ct),
            (IReadOnlyList<LinhaCentroConta> l) => (l.Select(x => Cd($"{x.Tipo}|{x.Codigo}|{x.Descricao}", x.Codigo, x.Descricao, x.Saldo,
                                                                    x.Tipo is TipoLinhaGerencial.Grupo or TipoLinhaGerencial.Total, x.Recuo)).ToList(), null),
            "Saldo = débitos − créditos (positivo = devedor).");

    private static readonly IReadOnlyList<ColunaRelatorio> CodigoDescricaoMedida = [new("Código", LarguraCm: 2.6), new("Descrição"), new("Medida", LarguraCm: 3.2)];

    /// <summary>Receita por centro de custo: receita, custos e despesas e margem de cada centro, em cada período.</summary>
    public Task<Resultado<TabelaRelatorio>> ReceitaCentroAsync(long empresaId, DateOnly inicio, DateOnly fim, long? centroId, int nivel, OpcoesComparativo op, long usuarioId, CancellationToken ct = default) =>
        MontarAsync(TabelasGerenciais.TituloReceitaCentro, CodigoDescricaoMedida, Comparativo.Periodos(inicio, fim, op), op,
            p => gerenciais.ReceitaPorCentroAsync(empresaId, new FiltroReceitaCentro(p.Inicio, p.Fim, centroId, nivel), usuarioId, ct),
            (ReceitaPorCentro r) =>
            {
                var l = new List<LinhaComparavel>();
                foreach (var x in r.Linhas)
                {
                    var d = x.Tipo is TipoLinhaGerencial.Grupo or TipoLinhaGerencial.Total;
                    var k = $"{x.Tipo}|{x.Codigo}|{x.Descricao}|";
                    l.Add(new(k + "R", [x.Codigo, x.Descricao, "Receita"], x.Receita, d, x.Recuo));
                    l.Add(new(k + "C", [x.Codigo, x.Descricao, "Custos e despesas"], x.CustosDespesas, d, x.Recuo));
                    l.Add(new(k + "M", [x.Codigo, x.Descricao, "Margem"], x.Margem, true, x.Recuo));
                }
                return (l, null);
            },
            "Margem = receita − custos − despesas. Os centros sintéticos somam os analíticos abaixo deles.");

    /// <summary>Orçado x realizado (modo acumulado) de cada período; períodos que cruzam anos somam o orçamento de cada ano.</summary>
    public Task<Resultado<TabelaRelatorio>> OrcadoRealizadoAsync(long empresaId, DateOnly inicio, DateOnly fim, long? centroId, string? prefixo, bool ignorarCentro,
        OpcoesComparativo op, long usuarioId, CancellationToken ct = default) =>
        MontarAsync(TabelasGerenciais.TituloOrcadoRealizado, CodigoDescricaoMedida, Comparativo.Periodos(inicio, fim, op), op,
            async p =>
            {
                var todas = new List<LinhaOrcadoRealizado>();
                for (var ano = p.Inicio.Year; ano <= p.Fim.Year; ano++)
                {
                    var mi = ano == p.Inicio.Year ? p.Inicio.Month : 1;
                    var mf = ano == p.Fim.Year ? p.Fim.Month : 12;
                    var r = await gerenciais.OrcadoRealizadoAsync(empresaId, new FiltroOrcadoRealizado(ano, mi, mf, centroId, prefixo, false, ignorarCentro), usuarioId, ct);
                    if (!r.Sucesso) return Resultado<IReadOnlyList<LinhaOrcadoRealizado>>.Falha(r.Erros.ToList());
                    todas.AddRange(r.Valor);
                }
                IReadOnlyList<LinhaOrcadoRealizado> somadas = todas
                    .GroupBy(l => (l.Tipo, l.Codigo, l.Descricao, l.Centro))
                    .Select(g => new LinhaOrcadoRealizado(g.Key.Tipo, g.Key.Codigo, g.Key.Descricao, g.Key.Centro, null, g.Sum(x => x.Orcado), g.Sum(x => x.Realizado),
                                                          g.Sum(x => x.Orcado), g.Sum(x => x.Realizado)))
                    .ToList();
                return Resultado<IReadOnlyList<LinhaOrcadoRealizado>>.Ok(somadas);
            },
            (IReadOnlyList<LinhaOrcadoRealizado> l) =>
            {
                var res = new List<LinhaComparavel>();
                foreach (var x in l)
                {
                    var d = x.Tipo is TipoLinhaGerencial.Grupo or TipoLinhaGerencial.Total;
                    var k = $"{x.Tipo}|{x.Codigo}|{x.Descricao}|{x.Centro}|";
                    var desc = x.Centro.Length > 0 ? $"{x.Descricao} — {x.Centro}" : x.Descricao;
                    res.Add(new(k + "O", [x.Codigo, desc, "Orçado"], x.Orcado, d));
                    res.Add(new(k + "R", [x.Codigo, desc, "Realizado"], x.Realizado, d));
                }
                return (res, null);
            },
            "Valores na natureza da conta, somando os meses do período. Compara o orçado e o realizado de cada período.");
}
