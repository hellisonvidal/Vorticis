using Vorticis.Application.Relatorios;

namespace Vorticis.Escritorio;

/// <summary>Relatórios › Relatório de honorários do Escritório (cobranças do período).</summary>
public static class TabelasEscritorio
{
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
        var I = TipoColuna.Inteiro;
        return new TabelaRelatorio("Produtividade da equipe",
            [new("Responsável"), new("Tarefas", I), new("Concluídas", I), new("No prazo", I), new("Com atraso", I), new("Atrasadas em aberto", I),
             new("A vencer", I), new("% no prazo", TipoColuna.Valor)], linhas,
            "Tarefas de todos os clientes com vencimento no período (canceladas ficam de fora). No prazo = concluída até o dia do vencimento.");
    }

    /// <summary>Certificados digitais com validade até <paramref name="ate"/> (inclusive os vencidos), com a empresa a que pertencem.</summary>
    public static TabelaRelatorio CertificadosAVencer(IEnumerable<Vorticis.Application.Sistema.CertificadoDigital> certificados, IReadOnlyDictionary<long, string> empresas,
                                                      DateOnly ate, DateTimeOffset agora)
    {
        var linhas = certificados.Where(c => DateOnly.FromDateTime(c.ValidoAte.LocalDateTime) <= ate).OrderBy(c => c.ValidoAte)
            .Select(c => new LinhaRelatorio([c.EmpresaId is { } e && empresas.TryGetValue(e, out var n) ? n : "(do escritório)", c.Titular, c.Tipo, c.Descricao,
                                             DateOnly.FromDateTime(c.ValidoAte.LocalDateTime), c.Situacao(agora)], Destaque: c.DiasParaVencer(agora) < 0)).ToList();
        return new TabelaRelatorio("Certificados digitais a vencer",
            [new("Empresa"), new("Titular"), new("Tipo", TipoColuna.Texto, 1.4), new("Descrição", TipoColuna.Texto, 4), new("Validade", TipoColuna.Data, 2.2),
             new("Situação", TipoColuna.Texto, 3)], linhas,
            "Renove o certificado antes do vencimento: sem ele, não há transmissão de SPED, eSocial, DCTFWeb nem notas fiscais. Em destaque, os vencidos.");
    }

    public const string TituloHonorarios = "Relatório de honorários";

    private static LinhaRelatorio L(bool destaque, params object?[] v) => new(v, destaque);

    public static TabelaRelatorio Honorarios(IReadOnlyList<CobrancaHonorario> cobrancas, DateOnly hoje)
    {
        var lista = cobrancas.OrderBy(c => c.Vencimento).ThenBy(c => c.PessoaNome).ToList();
        var linhas = lista.Select(c => L(false, c.Vencimento, c.PessoaNome ?? "", c.Descricao ?? c.Tipo.Rotulo(), c.Valor,
                                         c.Vencida(hoje) ? "Vencida" : c.Situacao.Rotulo(), c.RecebidoEm, c.ValorRecebido ?? 0m)).ToList();
        var validas = lista.Where(c => c.Situacao != SituacaoCobranca.Cancelada).ToList();
        linhas.Add(L(true, "TOTAL", $"{validas.Count} cobrança(s); em aberto {Vorticis.Domain.Comum.Formato.Moeda(validas.Where(c => c.Situacao == SituacaoCobranca.Aberta).Sum(c => c.Valor))}",
                     null, validas.Sum(c => c.Valor), "", null, lista.Sum(c => c.ValorRecebido ?? 0m)));
        return new TabelaRelatorio(TituloHonorarios,
        [
            new("Vencimento", TipoColuna.Data), new("Cliente"), new("Descrição"), new("Valor", TipoColuna.Valor), new("Situação", LarguraCm: 2),
            new("Recebido em", TipoColuna.Data), new("Valor recebido", TipoColuna.Valor),
        ], linhas);
    }
}
