using System.Text.Json;
using System.Text.Json.Serialization;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Comum;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Reforma;

namespace Vorticis.Fiscal.Simples;

/// <summary>Receita bruta e folha de um mês: a base do RBT12 e do Fator R.</summary>
public sealed record BaseMensal(DateOnly Competencia, decimal? Receita, decimal? Folha, string OrigemReceita = "MANUAL", string OrigemFolha = "MANUAL");

public sealed record ApuracaoSimplesSalva(long Id, DateOnly Competencia, decimal ValorDas, DateOnly Vencimento, long? LancamentoId, ResultadoSimples Resultado);

public sealed record PreparoSimples(IReadOnlyList<ReceitaSimples> Sugestao, decimal Rbt12, bool Rbt12Proporcional, decimal Folha12, IReadOnlyList<string> Avisos);

public interface IRepositorioSimples
{
    Task<IReadOnlyList<BaseMensal>> ListarBaseMensalAsync(long empresaId, DateOnly de, DateOnly ate, CancellationToken ct);
    Task GravarBaseMensalAsync(long empresaId, BaseMensal b, long usuarioId, CancellationToken ct);
    Task ExcluirBaseMensalAsync(long empresaId, DateOnly competencia, CancellationToken ct);

    /// <summary>Grava (substituindo a existente) a apuração e a receita do mês na base mensal.</summary>
    Task<long> GravarApuracaoAsync(long empresaId, ResultadoSimples r, long usuarioId, CancellationToken ct);
    Task<IReadOnlyList<ApuracaoSimplesSalva>> ListarApuracoesAsync(long empresaId, int ano, CancellationToken ct);
    Task<ApuracaoSimplesSalva?> ObterApuracaoAsync(long empresaId, DateOnly competencia, CancellationToken ct);
    Task ExcluirApuracaoAsync(long empresaId, long id, CancellationToken ct);
}

/// <summary>Serialização do resultado das apurações (JSONB), compartilhada pelos repositórios.</summary>
public static class JsonFiscal
{
    public static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    public static string Serializar<T>(T valor) => JsonSerializer.Serialize(valor, Opcoes);

    public static T Ler<T>(string json) => JsonSerializer.Deserialize<T>(json, Opcoes)!;
}

/// <summary>
/// Apuração mensal do Simples Nacional: segregação da receita (sugerida pelas notas), RBT12, Fator R, DAS por anexo com memória
/// de cálculo. Exige a função "apurar tributos" (consultas: relatórios fiscais).
/// </summary>
public sealed class ServicoSimplesNacional(IRepositorioSimples repositorio, IRepositorioDocumentos documentos, IParametrosFiscais parametros, Autorizador autorizador,
                                          ServicoOpcaoIbsCbs? opcaoIbsCbs = null)
{
    /// <summary>Regime regular do IBS/CBS no semestre da competência (de 2027 em diante): o IBS e a CBS saem do DAS.</summary>
    private async Task<bool> RegimeRegularAsync(long empresaId, DateOnly comp, CancellationToken ct) =>
        opcaoIbsCbs is not null && comp >= ServicoOpcaoIbsCbs.PrimeiraVigencia && await opcaoIbsCbs.NaDataAsync(empresaId, comp, ct) == OpcaoIbsCbs.RegimeRegular;

    public async Task<TabelaSimples> TabelaAsync(DateOnly data, CancellationToken ct = default) =>
        TabelaSimples.Carregar(await parametros.ObterJsonAsync(ChavesParametro.SimplesNacional, data, ct));

    private static DateOnly Mes(DateOnly d) => new(d.Year, d.Month, 1);

    // ------------------------------------------------------------------ base mensal (histórico de receita e folha)

    public async Task<Resultado<IReadOnlyList<BaseMensal>>> BaseMensalAsync(long empresaId, int ano, long usuarioId, CancellationToken ct = default)
    {
        var p = await autorizador.ObterAsync(usuarioId, empresaId, ct);
        if (!p.Pode(Permissao.FiscalApurar) && !p.Pode(Permissao.FiscalRetencoesManter) && !p.Pode(Permissao.FiscalRelatorios))
            return await autorizador.NegarAsync<IReadOnlyList<BaseMensal>>(usuarioId, empresaId, Permissao.FiscalApurar, ct) ?? Resultado<IReadOnlyList<BaseMensal>>.Falha("Acesso negado.");
        return Resultado<IReadOnlyList<BaseMensal>>.Ok(await repositorio.ListarBaseMensalAsync(empresaId, new DateOnly(ano, 1, 1), new DateOnly(ano, 12, 1), ct));
    }

    public async Task<Resultado> GravarBaseMensalAsync(long empresaId, BaseMensal b, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.FiscalRetencoesManter, ct) is { } negado) return Resultado.Falha(negado);
        if (b.Receita is < 0 || b.Folha is < 0) return Resultado.Falha("Receita e folha não podem ser negativas.");
        if (b.Receita is null && b.Folha is null) { await repositorio.ExcluirBaseMensalAsync(empresaId, Mes(b.Competencia), ct); return Resultado.Ok(); }
        await repositorio.GravarBaseMensalAsync(empresaId, b with { Competencia = Mes(b.Competencia), OrigemReceita = "MANUAL", OrigemFolha = "MANUAL" }, usuarioId, ct);
        return Resultado.Ok();
    }

    // ------------------------------------------------------------------ apuração

    /// <summary>Sugestão de receitas do mês (pelas notas), RBT12 e folha dos 12 meses (pela base mensal e início de atividade da empresa).</summary>
    public async Task<Resultado<PreparoSimples>> PrepararAsync(long empresaId, DateOnly competencia, AtividadeSimples atividadeServicos, long usuarioId,
                                                              CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<PreparoSimples>(usuarioId, empresaId, Permissao.FiscalApurar, ct) is { } negado) return negado;
        var comp = Mes(competencia);
        var regular = await RegimeRegularAsync(empresaId, comp, ct);
        var tabelaMes = await TabelaAsync(comp, ct);
        var itens = await ItensDaReceitaAsync(empresaId, comp, tabelaMes, ct);
        var excluir = regular && tabelaMes.ReceitaExcluiIbsCbsRegimeRegular;
        var sugestao = ClassificadorReceita.Sugerir(itens, atividadeServicos, excluir);
        var (rbt12, proporcional, folha12, avisos) = await Acumulados(empresaId, comp, sugestao.Sum(s => s.Valor), ct);
        if (excluir) avisos.Add("Empresa no regime regular do IBS/CBS neste semestre: a receita sugerida já desconta o IBS e a CBS destacados nas notas (não entram na receita bruta do Simples).");
        if (sugestao.Count == 0) avisos.Add("Não há notas fiscais de venda ou serviço no mês: informe as receitas manualmente.");
        return Resultado<PreparoSimples>.Ok(new PreparoSimples(sugestao, rbt12, proporcional, folha12, avisos));
    }

    private async Task<(decimal Rbt12, bool Proporcional, decimal Folha12, List<string> Avisos)> Acumulados(long empresaId, DateOnly comp, decimal receitaMes, CancellationToken ct)
    {
        var empresa = await documentos.ObterEmpresaAsync(empresaId, ct) ?? throw new InvalidOperationException("Empresa não encontrada.");
        var historico = await repositorio.ListarBaseMensalAsync(empresaId, comp.AddMonths(-12), comp, ct);
        var receitas = historico.Where(b => b.Receita is not null && b.Competencia < comp).ToDictionary(b => b.Competencia, b => b.Receita!.Value);
        var folhas = historico.Where(b => b.Folha is not null && b.Competencia < comp).ToDictionary(b => b.Competencia, b => b.Folha!.Value);
        var folhaMes = historico.FirstOrDefault(b => b.Competencia == comp)?.Folha ?? 0m;
        var r = CalculadoraSimples.Acumular12Meses(comp, empresa.InicioAtividade, receitas, receitaMes);
        var f = CalculadoraSimples.Acumular12Meses(comp, empresa.InicioAtividade, folhas, folhaMes);
        var avisos = new List<string>();
        if (r.MesesSemDado.Count > 0)
            avisos.Add($"Receita bruta sem lançamento em {string.Join(", ", r.MesesSemDado.Select(m => m.ToString("MM/yyyy")))}: o RBT12 pode estar subestimado. Complete em Histórico de receita e folha.");
        if (f.MesesSemDado.Count > 0)
            avisos.Add($"Folha sem lançamento em {string.Join(", ", f.MesesSemDado.Select(m => m.ToString("MM/yyyy")))}: o Fator R pode estar subestimado.");
        if (r.Proporcional) avisos.Add("Empresa com menos de 12 meses de atividade: RBT12 proporcional (média dos meses de atividade × 12).");
        return (r.Valor, r.Proporcional, f.Valor, avisos);
    }

    /// <summary>Calcula sem gravar. <paramref name="rbt12"/> e <paramref name="folha12"/> nulos são obtidos da base mensal.</summary>
    public async Task<Resultado<ResultadoSimples>> CalcularAsync(long empresaId, DateOnly competencia, IReadOnlyList<ReceitaSimples> receitas, decimal? rbt12,
                                                                decimal? folha12, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<ResultadoSimples>(usuarioId, empresaId, Permissao.FiscalApurar, ct) is { } negado) return negado;
        return await CalcularSemPermissaoAsync(empresaId, competencia, receitas, rbt12, folha12, ct);
    }

    private async Task<Resultado<ResultadoSimples>> CalcularSemPermissaoAsync(long empresaId, DateOnly competencia, IReadOnlyList<ReceitaSimples> receitas, decimal? rbt12,
                                                                             decimal? folha12, CancellationToken ct)
    {
        var comp = Mes(competencia);
        if (receitas.Count == 0 || receitas.All(r => r.Valor == 0)) return Resultado<ResultadoSimples>.Falha("Informe ao menos uma receita do mês.");
        if (rbt12 is < 0 || folha12 is < 0) return Resultado<ResultadoSimples>.Falha("RBT12 e folha não podem ser negativos.");
        var acumulado = await Acumulados(empresaId, comp, receitas.Sum(r => r.Valor), ct);
        var tabela = await TabelaAsync(comp, ct);
        var regular = await RegimeRegularAsync(empresaId, comp, ct);
        var resultado = CalculadoraSimples.Calcular(tabela, new EntradaSimples(comp, receitas, rbt12 ?? acumulado.Rbt12, folha12 ?? acumulado.Folha12, regular));
        var avisos = (rbt12 is null && folha12 is null ? resultado.Avisos.Concat(acumulado.Avisos) : resultado.Avisos).Concat(await AvisosLimitesAsync(empresaId, comp, tabela, receitas, ct)).ToList();
        return Resultado<ResultadoSimples>.Ok(resultado with { Avisos = avisos });
    }

    /// <summary>
    /// Itens do mês. Da tabela com IBS/CBS (2027 em diante) a receita é reconhecida na emissão do documento fiscal, inclusive entrega futura: vale o mês da
    /// emissão, não o da entrada/saída do livro. Antes, vale o mês do livro, como sempre foi.
    /// </summary>
    private async Task<IReadOnlyList<LinhaItemFiscal>> ItensDaReceitaAsync(long empresaId, DateOnly comp, TabelaSimples tabela, CancellationToken ct)
    {
        var fim = comp.AddMonths(1).AddDays(-1);
        if (!tabela.TemIbsCbs) return await documentos.ListarItensAsync(empresaId, comp, fim, null, ct);
        var itens = await documentos.ListarItensAsync(empresaId, comp.AddMonths(-1), comp.AddMonths(2).AddDays(-1), null, ct);
        return itens.Where(i => i.Emissao >= comp && i.Emissao <= fim).ToList();
    }

    /// <summary>Conferência do PGDAS-D: receitas da competência por anexo, atividade e tratamento, com o IBS/CBS dos documentos.</summary>
    public async Task<Resultado<ConferenciaPgdas>> ConferirPgdasAsync(long empresaId, DateOnly competencia, AtividadeSimples atividadeServicos, long usuarioId, CancellationToken ct = default)
    {
        var p = await autorizador.ObterAsync(usuarioId, empresaId, ct);
        if (!p.Pode(Permissao.FiscalApurar) && !p.Pode(Permissao.FiscalRelatorios))
            return await autorizador.NegarAsync<ConferenciaPgdas>(usuarioId, empresaId, Permissao.FiscalRelatorios, ct) ?? Resultado<ConferenciaPgdas>.Falha("Acesso negado.");
        var comp = Mes(competencia);
        var tabela = await TabelaAsync(comp, ct);
        var regular = await RegimeRegularAsync(empresaId, comp, ct);
        var itens = (await ItensDaReceitaAsync(empresaId, comp, tabela, ct)).Where(i => i.Situacao == SituacaoDocumento.Autorizada).ToList();
        var acumulado = await Acumulados(empresaId, comp, 0m, ct);
        var fatorR = CalculadoraSimples.FatorR(acumulado.Folha12, acumulado.Rbt12);
        var excluir = regular && tabela.ReceitaExcluiIbsCbsRegimeRegular;
        var linhas = new Dictionary<(AnexoSimples, AtividadeSimples, TratamentoSimples), (decimal Receita, decimal Cbs, decimal Ibs, int Itens)>();
        var semGrupo = new HashSet<long>();
        foreach (var i in itens)
        {
            if (ClassificadorReceita.Classificar(i, atividadeServicos, excluir) is not { } r) continue;
            var sinal = i.Operacao == TipoOperacao.Entrada ? -1m : 1m;   // devolução de venda
            if (i.Operacao == TipoOperacao.Saida && i.Item.Reforma is null) semGrupo.Add(i.DocumentoId);
            var chave = (CalculadoraSimples.Anexo(r.Atividade, fatorR, tabela.LimiteFatorR), r.Atividade, r.Tratamento);
            var v = linhas.GetValueOrDefault(chave);
            linhas[chave] = (v.Receita + r.Valor, v.Cbs + sinal * (i.Item.Reforma?.Cbs ?? 0m), v.Ibs + sinal * (i.Item.Reforma?.Ibs ?? 0m), v.Itens + 1);
        }
        var lista = linhas.OrderBy(x => x.Key.Item1).ThenBy(x => x.Key.Item2).ThenBy(x => x.Key.Item3)
            .Select(x => new LinhaConferenciaPgdas(x.Key.Item1, x.Key.Item2, x.Key.Item3, x.Value.Receita, x.Value.Cbs, x.Value.Ibs, x.Value.Itens)).ToList();
        var avisos = new List<string>
        {
            "A Receita Federal pode pré-preencher a receita do PGDAS-D com as NF-e, NFC-e e NFS-e até o dia 10: compare os totais acima com o que apareceu no PGDAS-D.",
            "O IBS e a CBS pré-preenchidos só se corrigem corrigindo o documento fiscal (cancelamento, nota de ajuste ou substituição), nunca direto no PGDAS-D.",
            "A receita vale pelo mês da emissão do documento, mesmo na entrega futura; notas de débito e de crédito que mudam valores anteriores entram no mês em que foram emitidas.",
        };
        if (semGrupo.Count > 0) avisos.Add($"{semGrupo.Count} nota(s) de saída sem o grupo IBS/CBS: o PGDAS-D não terá o IBS/CBS pré-preenchido para elas.");
        if (excluir) avisos.Add("Regime regular do IBS/CBS neste semestre: a receita acima já desconta o IBS e a CBS (não fazem parte da receita bruta do Simples).");
        if (lista.Count == 0) avisos.Add("Não há notas de venda ou serviço emitidas na competência.");
        if (tabela.TemIbsCbs && tabela.ReparticaoAConferir) avisos.Add("A repartição do DAS de 2027 ainda precisa ser conferida com a Resolução CGSN 190/2026 (anexos).");
        return Resultado<ConferenciaPgdas>.Ok(new ConferenciaPgdas(comp, regular, fatorR, lista, semGrupo.Count, avisos));
    }

    /// <summary>Limite de exportação e limite proporcional de início de atividade, com a receita do ano (base mensal e apurações salvas) mais a do mês.</summary>
    private async Task<IReadOnlyList<string>> AvisosLimitesAsync(long empresaId, DateOnly comp, TabelaSimples tabela, IReadOnlyList<ReceitaSimples> receitas, CancellationToken ct)
    {
        if (tabela.LimiteExportacao is null && tabela.LimiteInicioAtividadeMensal is null) return [];
        var empresa = await documentos.ObterEmpresaAsync(empresaId, ct);
        if (empresa is null) return [];
        var anoIni = new DateOnly(comp.Year, 1, 1);
        var base12 = await repositorio.ListarBaseMensalAsync(empresaId, anoIni, comp, ct);
        var receitaAno = base12.Where(b => b.Competencia < comp).Sum(b => b.Receita ?? 0m) + receitas.Sum(r => r.Valor);
        var exportacaoAno = (await repositorio.ListarApuracoesAsync(empresaId, comp.Year, ct)).Where(a => a.Competencia < comp)
                .SelectMany(a => a.Resultado.Segmentos).Where(s => s.Tratamento.HasFlag(TratamentoSimples.Exportacao)).Sum(s => s.Receita)
            + receitas.Where(r => r.Tratamento.HasFlag(TratamentoSimples.Exportacao)).Sum(r => r.Valor);
        return LimitesSimples.Avaliar(tabela, new EntradaLimitesSimples(comp, empresa.InicioAtividade, receitaAno, exportacaoAno));
    }

    public async Task<Resultado<ApuracaoSimplesSalva>> SalvarAsync(long empresaId, DateOnly competencia, IReadOnlyList<ReceitaSimples> receitas, decimal? rbt12,
                                                                  decimal? folha12, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<ApuracaoSimplesSalva>(usuarioId, empresaId, Permissao.FiscalApurar, ct) is { } negado) return negado;
        var comp = Mes(competencia);
        var existente = await repositorio.ObterApuracaoAsync(empresaId, comp, ct);
        if (existente?.LancamentoId is not null)
            return Resultado<ApuracaoSimplesSalva>.Falha($"A apuração de {comp:MM/yyyy} já tem a provisão contabilizada; desfaça a contabilização antes de recalcular.");
        var calculo = await CalcularSemPermissaoAsync(empresaId, comp, receitas, rbt12, folha12, ct);
        if (!calculo.Sucesso) return Resultado<ApuracaoSimplesSalva>.Falha(calculo.Erros);
        await repositorio.GravarApuracaoAsync(empresaId, calculo.Valor, usuarioId, ct);
        return Resultado<ApuracaoSimplesSalva>.Ok((await repositorio.ObterApuracaoAsync(empresaId, comp, ct))!);
    }

    public async Task<Resultado<IReadOnlyList<ApuracaoSimplesSalva>>> ListarAsync(long empresaId, int ano, long usuarioId, CancellationToken ct = default)
    {
        var p = await autorizador.ObterAsync(usuarioId, empresaId, ct);
        if (!p.Pode(Permissao.FiscalApurar) && !p.Pode(Permissao.FiscalRelatorios))
            return await autorizador.NegarAsync<IReadOnlyList<ApuracaoSimplesSalva>>(usuarioId, empresaId, Permissao.FiscalRelatorios, ct) ?? Resultado<IReadOnlyList<ApuracaoSimplesSalva>>.Falha("Acesso negado.");
        return Resultado<IReadOnlyList<ApuracaoSimplesSalva>>.Ok(await repositorio.ListarApuracoesAsync(empresaId, ano, ct));
    }

    public async Task<Resultado> ExcluirAsync(long empresaId, long id, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.FiscalApurar, ct) is { } negado) return Resultado.Falha(negado);
        var todas = await repositorio.ListarApuracoesAsync(empresaId, 0, ct);   // ano 0 = todas
        var alvo = todas.FirstOrDefault(a => a.Id == id);
        if (alvo is null) return Resultado.Falha("Apuração não encontrada.");
        if (alvo.LancamentoId is not null) return Resultado.Falha("A provisão desta apuração está contabilizada; desfaça-a antes de excluir.");
        await repositorio.ExcluirApuracaoAsync(empresaId, id, ct);
        return Resultado.Ok();
    }
}
