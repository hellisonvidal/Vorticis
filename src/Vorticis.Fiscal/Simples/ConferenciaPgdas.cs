using Vorticis.Application.Relatorios;
using Vorticis.Domain.Comum;

namespace Vorticis.Fiscal.Simples;

/// <summary>Receita do mês de um anexo/atividade/tratamento, com o IBS e a CBS destacados nos documentos (para comparar com o PGDAS-D pré-preenchido).</summary>
public sealed record LinhaConferenciaPgdas(AnexoSimples Anexo, AtividadeSimples Atividade, TratamentoSimples Tratamento, decimal Receita, decimal Cbs, decimal Ibs, int Itens);

/// <summary>
/// Conferência do PGDAS-D: receitas da competência segregadas por anexo e atividade, com ou sem substituição tributária, monofásico, ISS retido e exportação, e o
/// IBS/CBS dos documentos. A Receita Federal pré-preenche o PGDAS-D com as notas até o dia 10; o IBS/CBS pré-preenchido só se corrige corrigindo o documento.
/// </summary>
public sealed record ConferenciaPgdas(DateOnly Competencia, bool RegimeRegular, decimal FatorR, IReadOnlyList<LinhaConferenciaPgdas> Linhas, int NotasSemIbsCbs,
                                      IReadOnlyList<string> Avisos)
{
    public decimal Receita => Linhas.Sum(l => l.Receita);
    public decimal Cbs => Linhas.Sum(l => l.Cbs);
    public decimal Ibs => Linhas.Sum(l => l.Ibs);

    private static string Trat(TratamentoSimples t) => t == TratamentoSimples.Normal ? "Sem substituição nem retenção" : string.Join(" + ", new[]
    {
        t.HasFlag(TratamentoSimples.SubstituicaoTributaria) ? "Com substituição tributária" : null,
        t.HasFlag(TratamentoSimples.Monofasico) ? "Monofásico" : null,
        t.HasFlag(TratamentoSimples.IssRetido) ? "ISS retido" : null,
        t.HasFlag(TratamentoSimples.Exportacao) ? "Exportação" : null,
    }.Where(x => x is not null));

    public IReadOnlyList<TabelaRelatorio> Tabelas()
    {
        var corpo = Linhas.Select(l => new LinhaRelatorio([$"Anexo {l.Anexo}", RotuloAtividade(l.Atividade), Trat(l.Tratamento), l.Receita, l.Cbs, l.Ibs, l.Itens])).ToList();
        corpo.Add(new LinhaRelatorio(["", "TOTAL", "", Receita, Cbs, Ibs, Linhas.Sum(l => l.Itens)], true));
        var principal = new TabelaRelatorio($"Conferência do PGDAS-D · {Competencia:MM/yyyy}",
            [new("Anexo", LarguraCm: 1.8), new("Atividade", LarguraCm: 5), new("Tratamento", LarguraCm: 6), new("Receita", TipoColuna.Valor), new("CBS dos documentos", TipoColuna.Valor),
             new("IBS dos documentos", TipoColuna.Valor), new("Itens", TipoColuna.Inteiro, 1.4)], corpo,
            $"Fator R {FatorR * 100:0.00}% · {(RegimeRegular ? "IBS/CBS no regime regular (fora do DAS)" : "IBS/CBS dentro do DAS")}");
        var avisos = new TabelaRelatorio("O que conferir no PGDAS-D", [new("Orientação")], Avisos.Select(a => new LinhaRelatorio([a])).ToList());
        return [principal, avisos];
    }

    public static string RotuloAtividade(AtividadeSimples a) => a switch
    {
        AtividadeSimples.ComercioRevenda => "Revenda de mercadorias",
        AtividadeSimples.IndustriaVenda => "Venda de produtos industrializados",
        AtividadeSimples.ServicosAnexoIII => "Serviços do Anexo III",
        AtividadeSimples.ServicosAnexoIV => "Serviços do Anexo IV",
        AtividadeSimples.ServicosAnexoV => "Serviços do Anexo V",
        _ => "Serviços com Fator R",
    };
}
