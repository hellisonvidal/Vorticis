using System.Globalization;
using Vorticis.Domain.Contabil;

namespace Vorticis.Application.Relatorios;

/// <summary>Tabelas da Evolução patrimonial (BI): valores em cada período e variação sobre o período anterior.</summary>
public static class TabelasEvolucao
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Rotulo(DateOnly data, bool soFimDeAno) => soFimDeAno && data.Month == 12 ? data.Year.ToString(PtBr) : data.ToString("MM/yyyy", PtBr);

    public static string Percentual(VariacaoValor? v) => v?.Percentual is { } p ? p.ToString("N2", PtBr) + "%" : "—";

    public static TabelaRelatorio Valores(EvolucaoBi e)
    {
        var linhas = e.Pontos.Select(p => new LinhaRelatorio([Rotulo(p.Data, e.SoFimDeAno), p.Ativo, p.Passivo, p.PatrimonioLiquido, p.Capital, p.Reservas, p.Lucros, p.Resultado])).ToList();
        return new TabelaRelatorio("Evolução patrimonial",
            [new("Período", LarguraCm: 2.5), new("Ativo", TipoColuna.Valor), new("Passivo", TipoColuna.Valor), new("Patrimônio líquido", TipoColuna.Valor),
             new("Capital social", TipoColuna.Valor), new("Reservas", TipoColuna.Valor), new("Lucros/prejuízos acumulados", TipoColuna.Valor), new("Resultado do período", TipoColuna.Valor)],
            linhas, "Posição no último dia de cada período. Passivo = obrigações (circulante e não circulante); o patrimônio líquido soma capital social, reservas e lucros/prejuízos acumulados (com o resultado ainda não encerrado). Resultado do período = receitas menos custos e despesas do mês (ou dos meses desde a coluna anterior).");
    }

    public static TabelaRelatorio Variacoes(EvolucaoBi e)
    {
        (string Nome, Func<PontoPatrimonial, decimal> Campo)[] itens =
            [("Ativo", p => p.Ativo), ("Passivo", p => p.Passivo), ("Patrimônio líquido", p => p.PatrimonioLiquido), ("Resultado do período", p => p.Resultado)];
        var colunas = new List<ColunaRelatorio> { new("Período", LarguraCm: 2.5) };
        foreach (var (nome, _) in itens) { colunas.Add(new($"{nome} (variação R$)", TipoColuna.Valor)); colunas.Add(new($"{nome} (%)", TipoColuna.Texto)); }
        var linhas = new List<LinhaRelatorio>();
        for (var i = 1; i < e.Pontos.Count; i++)
        {
            var v = new List<object?> { Rotulo(e.Pontos[i].Data, e.SoFimDeAno) };
            foreach (var (_, campo) in itens) { var x = VariacaoValor.De(campo(e.Pontos[i]), campo(e.Pontos[i - 1])); v.Add(x.Absoluta); v.Add(Percentual(x)); }
            linhas.Add(new LinhaRelatorio(v));
        }
        if (e.Pontos.Count > 1)
        {
            var v = new List<object?> { $"Total: {Rotulo(e.Pontos[0].Data, e.SoFimDeAno)} a {Rotulo(e.Pontos[^1].Data, e.SoFimDeAno)}" };
            foreach (var (_, campo) in itens) { var x = VariacaoValor.De(campo(e.Pontos[^1]), campo(e.Pontos[0])); v.Add(x.Absoluta); v.Add(Percentual(x)); }
            linhas.Add(new LinhaRelatorio(v, true));
        }
        return new TabelaRelatorio("Variação sobre o período anterior", colunas, linhas,
            "Variação em R$ e em % sobre a linha anterior da tabela de valores; a última linha compara o primeiro com o último período. Sem % (—) quando o valor anterior é zero.");
    }
}
