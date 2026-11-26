using Vorticis.Application.Relatorios;

namespace Vorticis.Escritorio;

/// <summary>Tabelas dos relatórios do módulo Escritório (exibidas na tela e exportadas em PDF, Excel e CSV).</summary>
public static class TabelasEscritorio
{
    public const string TituloHonorarios = "Relatório de honorários";
    public const string TituloProdutividade = "Produtividade da equipe";
    public const string TituloCertificados = "Certificados digitais a vencer";

    public static IReadOnlyList<ColunaRelatorio> ColunasProdutividade { get; } =
    [
        new("Responsável"), new("Tarefas", TipoColuna.Inteiro), new("Concluídas", TipoColuna.Inteiro), new("No prazo", TipoColuna.Inteiro),
        new("Com atraso", TipoColuna.Inteiro), new("Atrasadas em aberto", TipoColuna.Inteiro), new("A vencer", TipoColuna.Inteiro), new("% no prazo", TipoColuna.Valor),
    ];

    public static IReadOnlyList<ColunaRelatorio> ColunasCertificados { get; } =
    [
        new("Empresa"), new("Titular"), new("Tipo", TipoColuna.Texto, 1.4), new("Descrição", TipoColuna.Texto, 4),
        new("Validade", TipoColuna.Data, 2.2), new("Situação", TipoColuna.Texto, 3),
    ];

    public static IReadOnlyList<ColunaRelatorio> ColunasHonorarios { get; } =
    [
        new("Competência", TipoColuna.Texto, 2.2), new("Vencimento", TipoColuna.Data, 2.2), new("Cliente"), new("Descrição"),
        new("Valor", TipoColuna.Valor), new("Juros / multa", TipoColuna.Valor, 2.6), new("Situação", TipoColuna.Texto, 2.2),
        new("Recebido em", TipoColuna.Data, 2.2), new("Valor recebido", TipoColuna.Valor),
    ];

    /// <summary>
    /// Produtividade da equipe: tarefas com vencimento no período, por responsável: concluídas no prazo (até o dia do vencimento),
    /// concluídas com atraso, em aberto atrasadas e em aberto no prazo, e o percentual no prazo sobre as concluídas.
    /// </summary>
    public static TabelaRelatorio Produtividade(IEnumerable<Tarefa> tarefas, DateOnly inicio, DateOnly fim, DateOnly hoje)
    {
        var doPeriodo = tarefas.Where(t => t.Vencimento >= inicio && t.Vencimento <= fim && t.Situacao != SituacaoTarefa.Cancelada).ToList();
        LinhaRelatorio Linha(string nome, IReadOnlyCollection<Tarefa> ts, bool total = false)
        {
            var concl = ts.Where(t => t.Situacao == SituacaoTarefa.Concluida).ToList();
            var noPrazo = concl.Count(t => t.ConcluidaEm is { } c && DateOnly.FromDateTime(c.LocalDateTime) <= t.Vencimento);
            var atrasadas = ts.Count(t => t.Atrasada(hoje));
            return new LinhaRelatorio([nome, (long)ts.Count, (long)concl.Count, (long)noPrazo, (long)(concl.Count - noPrazo), (long)atrasadas,
                                       (long)(ts.Count - concl.Count - atrasadas), concl.Count == 0 ? 0m : Math.Round(noPrazo * 100m / concl.Count, 1)], Destaque: total);
        }
        var linhas = doPeriodo.GroupBy(t => t.ResponsavelNome ?? "(sem responsável)").OrderBy(g => g.Key, StringComparer.CurrentCulture)
                              .Select(g => Linha(g.Key, g.ToList())).ToList();
        linhas.Add(Linha("TOTAL", doPeriodo, true));
        return new TabelaRelatorio(TituloProdutividade, ColunasProdutividade, linhas,
            "Tarefas de todos os clientes com vencimento no período (canceladas ficam de fora). No prazo = concluída até o dia do vencimento.");
    }

    /// <summary>Certificados digitais com validade até <paramref name="ate"/> (inclusive os vencidos), com a empresa a que pertencem.</summary>
    public static TabelaRelatorio CertificadosAVencer(IEnumerable<Vorticis.Application.Sistema.CertificadoDigital> certificados, IReadOnlyDictionary<long, string> empresas,
                                                      DateOnly ate, DateTimeOffset agora)
    {
        var linhas = certificados.Where(c => DateOnly.FromDateTime(c.ValidoAte.LocalDateTime) <= ate).OrderBy(c => c.ValidoAte)
            .Select(c => new LinhaRelatorio([c.EmpresaId is { } e && empresas.TryGetValue(e, out var n) ? n : "(do escritório)", c.Titular, c.Tipo, c.Descricao,
                                             DateOnly.FromDateTime(c.ValidoAte.LocalDateTime), c.Situacao(agora)], Destaque: c.DiasParaVencer(agora) < 0)).ToList();
        return new TabelaRelatorio(TituloCertificados, ColunasCertificados, linhas,
            "Renove o certificado antes do vencimento: sem ele, não há transmissão de SPED, eSocial, DCTFWeb nem notas fiscais. Em destaque, os vencidos.");
    }

    private static LinhaRelatorio L(bool destaque, params object?[] v) => new(v, destaque);

    public static TabelaRelatorio Honorarios(IReadOnlyList<CobrancaHonorario> cobrancas, DateOnly hoje)
    {
        var lista = cobrancas.OrderBy(c => c.Vencimento).ThenBy(c => c.PessoaNome).ToList();
        var linhas = lista.Select(c =>
        {
            var desc = (c.Descricao ?? (c.Tipo == TipoCobranca.Honorario ? "Honorários" : c.Tipo.Rotulo()))
                       + (c.ParcelaTexto is { Length: > 0 } p ? $" ({p})" : "");
            return L(c.Vencida(hoje), c.Competencia.ToString("MM/yyyy"), c.Vencimento, c.PessoaNome ?? "", desc, c.Valor,
                     c.Encargos > 0 ? c.Encargos : null, c.Vencida(hoje) ? "Vencida" : c.Situacao.Rotulo(),
                     c.RecebidoEm, c.Situacao == SituacaoCobranca.Recebida ? c.ValorRecebido : null);
        }).ToList();
        var validas = lista.Where(c => c.Situacao != SituacaoCobranca.Cancelada).ToList();
        var emAberto = validas.Where(c => c.Situacao == SituacaoCobranca.Aberta).Sum(c => c.Valor);
        var vencido = lista.Where(c => c.Vencida(hoje)).Sum(c => c.Valor);
        var encargos = lista.Sum(c => c.Encargos);
        var resumo = $"{validas.Count} cobrança(s) · em aberto R$ {Vorticis.Domain.Comum.Formato.Moeda(emAberto)}"
                     + (vencido > 0 ? $" · vencido R$ {Vorticis.Domain.Comum.Formato.Moeda(vencido)}" : "");
        linhas.Add(L(true, "TOTAL", null, resumo, null, validas.Sum(c => c.Valor), encargos > 0 ? encargos : null,
                     "", null, lista.Sum(c => c.ValorRecebido ?? 0m)));
        return new TabelaRelatorio(TituloHonorarios, ColunasHonorarios, linhas,
            "Cobranças canceladas não entram nos totais. Em Juros / multa constam os encargos informados na baixa; em destaque, os títulos vencidos e a linha de total.");
    }
}
