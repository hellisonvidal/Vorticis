using Vorticis.Application.Relatorios;

namespace Vorticis.Escritorio;

/// <summary>Relatórios › Relatório de honorários do Escritório (cobranças do período).</summary>
public static class TabelasEscritorio
{
    public const string TituloHonorarios = "Relatório de honorários";

    private static LinhaRelatorio L(bool destaque, params object?[] v) => new(v, destaque);

    public static TabelaRelatorio Honorarios(IReadOnlyList<CobrancaHonorario> cobrancas, DateOnly hoje)
    {
        var lista = cobrancas.OrderBy(c => c.Competencia).ThenBy(c => c.PessoaNome).ToList();
        var linhas = lista.Select(c => L(false, c.Competencia.ToString("MM/yyyy"), c.PessoaNome ?? "", c.Vencimento, c.Valor,
                                         c.Vencida(hoje) ? "Vencida" : c.Situacao.Rotulo(), c.RecebidoEm, c.ValorRecebido ?? 0m)).ToList();
        var validas = lista.Where(c => c.Situacao != SituacaoCobranca.Cancelada).ToList();
        linhas.Add(L(true, "TOTAL", $"{validas.Count} cobrança(s); em aberto {Vorticis.Domain.Comum.Formato.Moeda(validas.Where(c => c.Situacao == SituacaoCobranca.Aberta).Sum(c => c.Valor))}",
                     null, validas.Sum(c => c.Valor), "", null, lista.Sum(c => c.ValorRecebido ?? 0m)));
        return new TabelaRelatorio(TituloHonorarios,
        [
            new("Competência", LarguraCm: 2), new("Cliente"), new("Vencimento", TipoColuna.Data), new("Valor", TipoColuna.Valor), new("Situação", LarguraCm: 2),
            new("Recebido em", TipoColuna.Data), new("Valor recebido", TipoColuna.Valor),
        ], linhas);
    }
}
