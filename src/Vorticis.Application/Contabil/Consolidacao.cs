using Vorticis.Application.Relatorios;
using Vorticis.Domain.Contabil;

namespace Vorticis.Application.Contabil;

/// <summary>Linha do balancete consolidado: o saldo final da conta em cada empresa, as eliminações e o consolidado (devedor +, credor −).</summary>
public sealed record LinhaConsolidada(string Codigo, string Descricao, short Nivel, bool Analitica, IReadOnlyList<decimal> Saldos, decimal Eliminacao)
{
    public decimal Consolidado => Saldos.Sum() + Eliminacao;
}

/// <summary>
/// Consolidação de um grupo de empresas que usam o mesmo plano de contas (contas casadas pelo código): soma o saldo final de cada conta e
/// elimina por completo as contas informadas (por exemplo, investimentos em controladas, mútuos e contas correntes entre as empresas),
/// refletindo a eliminação nas contas sintéticas acima delas.
/// </summary>
public static class Consolidacao
{
    public static IReadOnlyList<LinhaConsolidada> Consolidar(IReadOnlyList<IReadOnlyList<LinhaBalancete>> balancetes, IReadOnlyCollection<string> contasEliminar)
    {
        var contas = new SortedDictionary<string, (string Descricao, short Nivel, bool Analitica)>(StringComparer.Ordinal);
        foreach (var b in balancetes)
            foreach (var l in b) contas.TryAdd(l.Codigo, (l.Descricao, l.Nivel, l.Analitica));
        var saldos = balancetes.Select(b => b.ToDictionary(l => l.Codigo, l => l.SaldoFinal)).ToList();
        decimal Total(string cod) => saldos.Sum(s => s.GetValueOrDefault(cod));
        var eliminar = contasEliminar.Select(c => c.Trim()).Where(c => c.Length > 0 && contas.ContainsKey(c)).ToList();
        // a eliminação de uma conta (e de tudo o que está abaixo dela) sobe para as sintéticas acima
        var elim = new Dictionary<string, decimal>();
        foreach (var e in eliminar.Where(e => !eliminar.Any(o => o != e && e.StartsWith(o + ".", StringComparison.Ordinal))))
            foreach (var cod in contas.Keys)
            {
                if (cod == e || cod.StartsWith(e + ".", StringComparison.Ordinal)) elim[cod] = elim.GetValueOrDefault(cod) - Total(cod);   // a conta e as de baixo zeram
                else if (e.StartsWith(cod + ".", StringComparison.Ordinal)) elim[cod] = elim.GetValueOrDefault(cod) - Total(e);         // as sintéticas acima perdem o saldo dela
            }
        return contas.Select(c => new LinhaConsolidada(c.Key, c.Value.Descricao, c.Value.Nivel, c.Value.Analitica,
                                                       saldos.Select(s => s.GetValueOrDefault(c.Key)).ToList(), elim.GetValueOrDefault(c.Key)))
                     .Where(l => l.Saldos.Any(s => s != 0) || l.Eliminacao != 0).ToList();
    }

    public static TabelaRelatorio Tabela(IReadOnlyList<LinhaConsolidada> linhas, IReadOnlyList<string> empresas, DateOnly data)
    {
        var colunas = new List<ColunaRelatorio> { new("Código", TipoColuna.Texto, 2.4), new("Conta") };
        colunas.AddRange(empresas.Select(e => new ColunaRelatorio(e, TipoColuna.Valor)));
        colunas.Add(new("Eliminações", TipoColuna.Valor));
        colunas.Add(new("Consolidado", TipoColuna.Valor));
        var rows = linhas.Select(l => new LinhaRelatorio([l.Codigo, l.Descricao, .. l.Saldos.Cast<object?>(), l.Eliminacao, l.Consolidado],
                                                         Destaque: !l.Analitica, Recuo: Math.Max(0, l.Nivel - 1))).ToList();
        return new TabelaRelatorio($"Balancete consolidado em {data:dd/MM/yyyy}", colunas, rows,
            "Saldo devedor positivo e credor negativo. Contas casadas pelo código: as empresas precisam usar o mesmo plano de contas.");
    }
}
