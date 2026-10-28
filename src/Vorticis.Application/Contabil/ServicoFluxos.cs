using Vorticis.Application.Relatorios;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Contabil;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Application.Contabil;

/// <summary>Persistência da classificação das contas para a DFC e o EBITDA.</summary>
public interface IRepositorioClassificacao
{
    Task<IReadOnlyList<ClassificacaoConta>> ListarAsync(long empresaId, CancellationToken ct);
    /// <summary>Substitui toda a classificação gravada da empresa (numa transação).</summary>
    Task SubstituirAsync(long empresaId, IReadOnlyList<ClassificacaoConta> itens, CancellationToken ct);
    /// <summary>Conta → código do plano referencial vigente no ano (vazio se a empresa não mapeou).</summary>
    Task<IReadOnlyDictionary<long, string>> ListarReferenciaisAsync(long empresaId, int ano, CancellationToken ct);
}

/// <summary>Linha da tela "Classificação da DFC e do EBITDA".</summary>
public sealed record LinhaClassificacao(long ContaId, string Codigo, string Descricao, bool Patrimonial, ClasseConta Sugerida, ClasseConta Classe,
                                        bool Personalizada, bool NaoRecorrente);

/// <summary>DFC com a conferência dos saldos de caixa contra o Balanço Patrimonial nas duas datas.</summary>
public sealed record DfcApurada(DemonstracaoFluxoCaixa Demonstracao, decimal CaixaBalancoInicio, decimal CaixaBalancoFim)
{
    public bool ConfereBalanco => Demonstracao.SaldoInicial == CaixaBalancoInicio && Demonstracao.SaldoFinal == CaixaBalancoFim;
}

/// <summary>EBITDA de um recorte (mês ou centro de custo).</summary>
public sealed record EbitdaRecorte(string Rotulo, DemonstracaoEbitda Demonstracao);

/// <summary>Relatórios › Contábeis › DFC e EBITDA, e a classificação das contas que alimenta os dois.</summary>
public sealed class ServicoFluxos(IConsultasContabeis consultas, IRepositorioClassificacao classificacoes, Autorizador autorizador)
{
    private async Task<IReadOnlyDictionary<long, ClasseConta>> ClassesAsync(long empresaId, IReadOnlyList<ContaPlano> plano, int ano, CancellationToken ct)
    {
        var referencial = await classificacoes.ListarReferenciaisAsync(empresaId, ano, ct);
        return SugestaoClasse.Efetivas(plano, await classificacoes.ListarAsync(empresaId, ct), referencial);
    }

    public async Task<Resultado<IReadOnlyList<LinhaClassificacao>>> ListarClassificacaoAsync(long empresaId, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<IReadOnlyList<LinhaClassificacao>>(usuarioId, empresaId, Permissao.RelatorioDemonstracoes, ct) is { } negado) return negado;
        var plano = await consultas.ObterPlanoAsync(empresaId, ct);
        var referencial = await classificacoes.ListarReferenciaisAsync(empresaId, DateTime.Today.Year, ct);
        var sugestao = SugestaoClasse.Sugerir(plano, referencial);
        var gravadas = (await classificacoes.ListarAsync(empresaId, ct)).ToDictionary(g => g.ContaId);
        var efetivas = SugestaoClasse.Efetivas(plano, gravadas.Values.ToList(), referencial);
        var linhas = plano.Where(c => sugestao.ContainsKey(c.Id)).OrderBy(c => c.Codigo, StringComparer.Ordinal)
            .Select(c => new LinhaClassificacao(c.Id, c.Codigo, c.Descricao, c.CodNat != "04", sugestao[c.Id], efetivas[c.Id], efetivas[c.Id] != sugestao[c.Id],
                                                gravadas.TryGetValue(c.Id, out var g) && g.NaoRecorrente)).ToList();
        return Resultado<IReadOnlyList<LinhaClassificacao>>.Ok(linhas);
    }

    /// <summary>Grava a classificação (substitui a anterior). Só entram as contas que diferem da sugestão ou marcadas como não recorrentes.</summary>
    public async Task<Resultado> SalvarClassificacaoAsync(long empresaId, IReadOnlyList<ClassificacaoConta> itens, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.PlanoContasManter, ct) is { } negado) return Resultado.Falha(negado);
        var plano = await consultas.ObterPlanoAsync(empresaId, ct);
        var porId = plano.ToDictionary(c => c.Id);
        var sugestao = SugestaoClasse.Sugerir(plano, await classificacoes.ListarReferenciaisAsync(empresaId, DateTime.Today.Year, ct));
        var erros = new List<string>();
        var gravar = new Dictionary<long, ClassificacaoConta>();
        foreach (var i in itens)
        {
            if (!porId.TryGetValue(i.ContaId, out var c) || !sugestao.ContainsKey(i.ContaId)) { erros.Add("Há uma conta que não pode ser classificada (só contas analíticas de ativo, passivo, patrimônio líquido e resultado)."); continue; }
            if (i.Classe.EhPatrimonial() != (c.CodNat != "04")) { erros.Add($"Conta {c.Codigo}: a classe \"{i.Classe.Nome()}\" não vale para {(c.CodNat == "04" ? "conta de resultado" : "conta patrimonial")}."); continue; }
            if (i.NaoRecorrente && c.CodNat != "04") { erros.Add($"Conta {c.Codigo}: só contas de resultado podem ser marcadas como não recorrentes."); continue; }
            if (i.Classe != sugestao[i.ContaId] || i.NaoRecorrente) gravar[i.ContaId] = i;
        }
        if (erros.Count > 0) return Resultado.Falha(erros.Distinct().ToList());
        await classificacoes.SubstituirAsync(empresaId, gravar.Values.ToList(), ct);
        return Resultado.Ok();
    }

    private async Task<decimal> CaixaNaDataAsync(long empresaId, DateOnly data, IReadOnlyDictionary<long, ClasseConta> classes, CancellationToken ct)
    {
        var saldos = await consultas.ObterMovimentosAsync(empresaId, data.AddDays(1), data, ct);
        return saldos.Where(m => classes.TryGetValue(m.ContaId, out var c) && c == ClasseConta.Caixa).Sum(m => m.SaldoAnterior);
    }

    public async Task<Resultado<DfcApurada>> DfcAsync(long empresaId, DateOnly inicio, DateOnly fim, bool direto, short nivelMaximo, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<DfcApurada>(usuarioId, empresaId, Permissao.RelatorioDemonstracoes, ct) is { } negado) return negado;
        if (fim < inicio) return Resultado<DfcApurada>.Falha("Data final anterior à inicial.");
        var plano = await consultas.ObterPlanoAsync(empresaId, ct);
        var classes = await ClassesAsync(empresaId, plano, fim.Year, ct);
        if (!classes.Values.Contains(ClasseConta.Caixa)) return Resultado<DfcApurada>.Falha("Nenhuma conta está classificada como \"Caixa e equivalentes\". Ajuste em Relatórios › Contábeis › Classificação da DFC.");
        var movimentos = await consultas.ObterMovimentosAsync(empresaId, inicio, fim, ct, excluirEncerramento: true);
        var partidas = direto ? await consultas.ObterPartidasAsync(empresaId, inicio, fim, ct) : null;
        var dfc = FluxoCaixa.Montar(inicio, fim, plano, movimentos, classes, direto, partidas, nivelMaximo);
        return Resultado<DfcApurada>.Ok(new DfcApurada(dfc, await CaixaNaDataAsync(empresaId, inicio.AddDays(-1), classes, ct), await CaixaNaDataAsync(empresaId, fim, classes, ct)));
    }

    private static IReadOnlySet<long> NaoRecorrentes(IReadOnlyList<ClassificacaoConta> g) => g.Where(x => x.NaoRecorrente).Select(x => x.ContaId).ToHashSet();

    public async Task<Resultado<DemonstracaoEbitda>> EbitdaAsync(long empresaId, DateOnly inicio, DateOnly fim, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<DemonstracaoEbitda>(usuarioId, empresaId, Permissao.RelatorioDemonstracoes, ct) is { } negado) return negado;
        if (fim < inicio) return Resultado<DemonstracaoEbitda>.Falha("Data final anterior à inicial.");
        var plano = await consultas.ObterPlanoAsync(empresaId, ct);
        var classes = await ClassesAsync(empresaId, plano, fim.Year, ct);
        var gravadas = await classificacoes.ListarAsync(empresaId, ct);
        var movimentos = await consultas.ObterMovimentosAsync(empresaId, inicio, fim, ct, excluirEncerramento: true);
        return Resultado<DemonstracaoEbitda>.Ok(CalculoEbitda.Montar(inicio, fim, plano, movimentos, classes, NaoRecorrentes(gravadas)));
    }

    /// <summary>EBITDA de cada mês do ano (e o total do ano na última posição).</summary>
    public async Task<Resultado<IReadOnlyList<EbitdaRecorte>>> EbitdaMensalAsync(long empresaId, int ano, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<IReadOnlyList<EbitdaRecorte>>(usuarioId, empresaId, Permissao.RelatorioDemonstracoes, ct) is { } negado) return negado;
        if (ano is < 2000 or > 2100) return Resultado<IReadOnlyList<EbitdaRecorte>>.Falha("Informe um ano entre 2000 e 2100.");
        var plano = await consultas.ObterPlanoAsync(empresaId, ct);
        var classes = await ClassesAsync(empresaId, plano, ano, ct);
        var nr = NaoRecorrentes(await classificacoes.ListarAsync(empresaId, ct));
        var movs = await consultas.ObterMovimentosCentroAsync(empresaId, new DateOnly(ano, 1, 1), new DateOnly(ano, 12, 31), ct);
        var lista = new List<EbitdaRecorte>();
        for (var m = 1; m <= 12; m++)
        {
            var ini = new DateOnly(ano, m, 1);
            var fim = new DateOnly(ano, m, DateTime.DaysInMonth(ano, m));
            lista.Add(new(Comparativo.Rotular(ini, fim).Rotulo, CalculoEbitda.Montar(ini, fim, plano, Agregar(movs.Where(x => x.Mes.Year == ano && x.Mes.Month == m)), classes, nr)));
        }
        lista.Add(new($"Total {ano}", CalculoEbitda.Montar(new(ano, 1, 1), new(ano, 12, 31), plano, Agregar(movs), classes, nr)));
        return Resultado<IReadOnlyList<EbitdaRecorte>>.Ok(lista);
    }

    /// <summary>EBITDA do período por centro de custo analítico (e as partidas sem centro, em "Sem centro de custo").</summary>
    public async Task<Resultado<IReadOnlyList<EbitdaRecorte>>> EbitdaPorCentroAsync(long empresaId, DateOnly inicio, DateOnly fim, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<IReadOnlyList<EbitdaRecorte>>(usuarioId, empresaId, Permissao.RelatorioDemonstracoes, ct) is { } negado) return negado;
        if (fim < inicio) return Resultado<IReadOnlyList<EbitdaRecorte>>.Falha("Data final anterior à inicial.");
        var plano = await consultas.ObterPlanoAsync(empresaId, ct);
        var centros = (await consultas.ObterCentrosAsync(empresaId, ct)).ToDictionary(c => c.Id);
        var classes = await ClassesAsync(empresaId, plano, fim.Year, ct);
        var nr = NaoRecorrentes(await classificacoes.ListarAsync(empresaId, ct));
        var movs = await consultas.ObterMovimentosCentroAsync(empresaId, inicio, fim, ct);
        var lista = movs.GroupBy(m => m.CentroId)
            .Select(g => (Codigo: g.Key == 0 ? "~" : centros.TryGetValue(g.Key, out var c) ? c.Codigo : g.Key.ToString(),
                          Rotulo: g.Key == 0 ? "Sem centro de custo" : centros.TryGetValue(g.Key, out var c2) ? $"{c2.Codigo} — {c2.Descricao}" : $"Centro {g.Key}", Itens: g))
            .OrderBy(x => x.Codigo, StringComparer.Ordinal)
            .Select(x => new EbitdaRecorte(x.Rotulo, CalculoEbitda.Montar(inicio, fim, plano, Agregar(x.Itens), classes, nr))).ToList();
        lista.Add(new("Total", CalculoEbitda.Montar(inicio, fim, plano, Agregar(movs), classes, nr)));
        return Resultado<IReadOnlyList<EbitdaRecorte>>.Ok(lista);
    }

    private static IReadOnlyList<MovimentoConta> Agregar(IEnumerable<MovimentoCentro> movs) =>
        movs.GroupBy(m => m.ContaId).Select(g => new MovimentoConta(g.Key, 0, g.Sum(x => x.Debitos), g.Sum(x => x.Creditos))).ToList();
}
