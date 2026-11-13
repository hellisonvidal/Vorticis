using Vorticis.Application.Relatorios;
using Vorticis.Domain.Comum;
using Vorticis.Fiscal.Documentos;

namespace Vorticis.Fiscal.Relatorios;

/// <summary>Linha da curva ABC: produto ou participante, com a participação no total e a classe.</summary>
public sealed record LinhaCurvaAbc(char Classe, string Codigo, string Descricao, decimal Quantidade, int Notas, decimal Valor, decimal Percentual, decimal Acumulado);

/// <summary>
/// Curva ABC (análise de Pareto) das notas autorizadas: ordena produtos (ou clientes/fornecedores) pelo valor e classifica em
/// A (os que somam os primeiros 80% do valor), B (até 95%) e C (o restante).
/// </summary>
public static class CurvaAbc
{
    public const decimal LimiteA = 80m, LimiteB = 95m;
    public static IReadOnlyList<string> Agrupamentos { get; } = ["Por produto", "Por cliente/fornecedor"];

    public static IReadOnlyList<LinhaCurvaAbc> Montar(IEnumerable<LinhaItemFiscal> itens, bool porParticipante)
    {
        var grupos = itens.Where(i => i.Situacao == SituacaoDocumento.Autorizada)
            .GroupBy(i => porParticipante
                ? (Codigo: i.ParticipanteDocumento ?? "", Descricao: i.ParticipanteNome ?? "(sem identificação)")
                : (Codigo: i.Item.CodProduto ?? "", Descricao: (i.Item.Descricao ?? "(sem descrição)").Trim()))
            .Select(g => (g.Key.Codigo, g.Key.Descricao, Quantidade: g.Sum(i => i.Item.Quantidade), Notas: g.Select(i => i.DocumentoId).Distinct().Count(),
                          Valor: g.Sum(i => i.Item.ValorContabil)))
            .Where(g => g.Valor > 0)
            .OrderByDescending(g => g.Valor).ThenBy(g => g.Descricao, StringComparer.CurrentCulture).ToList();
        var total = grupos.Sum(g => g.Valor);
        var linhas = new List<LinhaCurvaAbc>();
        decimal acumulado = 0;
        foreach (var g in grupos)
        {
            var antes = acumulado;
            acumulado += g.Valor;
            var pctAntes = total == 0 ? 0 : antes / total * 100m;
            var classe = pctAntes < LimiteA ? 'A' : pctAntes < LimiteB ? 'B' : 'C';
            linhas.Add(new LinhaCurvaAbc(classe, g.Codigo, g.Descricao, g.Quantidade, g.Notas, g.Valor,
                                         Math.Round(total == 0 ? 0 : g.Valor / total * 100m, 2), Math.Round(total == 0 ? 0 : acumulado / total * 100m, 2)));
        }
        return linhas;
    }

    public static TabelaRelatorio Tabela(IReadOnlyList<LinhaCurvaAbc> linhas, TipoOperacao operacao, bool porParticipante)
    {
        var quem = operacao == TipoOperacao.Saida ? "Cliente" : "Fornecedor";
        var rows = linhas.Select(l => new LinhaRelatorio([l.Classe.ToString(), porParticipante ? FormatarDocumento(l.Codigo) : l.Codigo, l.Descricao,
                                                          porParticipante ? (object)(long)l.Notas : l.Quantidade, l.Valor, l.Percentual, l.Acumulado])).ToList();
        rows.Add(new LinhaRelatorio(["", "", "TOTAL", porParticipante ? (object)(long)linhas.Sum(l => l.Notas) : linhas.Sum(l => l.Quantidade), linhas.Sum(l => l.Valor),
                                     linhas.Count == 0 ? 0m : 100m, null], Destaque: true));
        var resumo = string.Join(" · ", "ABC".Select(c =>
        {
            var da = linhas.Where(l => l.Classe == c).ToList();
            var pct = linhas.Sum(l => l.Valor) is var t && t > 0 ? da.Sum(l => l.Valor) / t * 100m : 0;
            return $"Classe {c}: {da.Count} item(ns), {Formato.Moeda(da.Sum(l => l.Valor))} ({Formato.Num(pct)}%)";
        }));
        return new TabelaRelatorio($"Curva ABC de {(operacao == TipoOperacao.Saida ? "vendas" : "compras")} – {(porParticipante ? quem.ToLowerInvariant() + "s" : "produtos")}",
            [new("Classe", TipoColuna.Texto, 1.4), new(porParticipante ? "CNPJ/CPF" : "Código", TipoColuna.Texto, 3), new(porParticipante ? quem : "Produto"),
             new(porParticipante ? "Notas" : "Quantidade", porParticipante ? TipoColuna.Inteiro : TipoColuna.Valor), new("Valor", TipoColuna.Valor),
             new("% do total", TipoColuna.Valor), new("% acumulado", TipoColuna.Valor)],
            rows, $"{resumo}. Classe A: os itens que somam os primeiros {LimiteA:0}% do valor; B: até {LimiteB:0}%; C: o restante.");
    }

    private static string FormatarDocumento(string d) => d.Length switch
    {
        14 => Vorticis.Domain.Cadastros.Cnpj.Formatar(d),
        11 => Vorticis.Domain.Cadastros.Cpf.Formatar(d),
        _ => d,
    };
}
