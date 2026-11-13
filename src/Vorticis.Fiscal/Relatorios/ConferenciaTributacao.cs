using Vorticis.Application.Relatorios;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Produtos;

namespace Vorticis.Fiscal.Relatorios;

/// <summary>Divergência entre o item da nota e o cadastro do produto.</summary>
public sealed record DivergenciaTributacao(DateOnly Data, string Numero, TipoOperacao Operacao, string? Participante, string Produto, string Campo,
                                           string NaNota, string NoCadastro);

/// <summary>
/// Conferência de tributação: compara os itens das notas autorizadas com o cadastro do produto (pelo código ou, sem código, pela descrição):
/// NCM, origem, CST do ICMS (de entrada ou de saída), natureza do CFOP (os três últimos dígitos) e, nas saídas internas, as alíquotas de ICMS e IPI.
/// </summary>
public static class ConferenciaTributacao
{
    /// <summary>CST iguais: com e sem o dígito da origem na frente ("000" e "00") contam como o mesmo.</summary>
    public static bool MesmoCst(string a, string b)
        => a == b || (a.Length != b.Length && Math.Min(a.Length, b.Length) == 2 && a[^2..] == b[^2..]);

    private static string Num(decimal v) => v.ToString("0.##", Formato.PtBr);

    public static IReadOnlyList<DivergenciaTributacao> Conferir(IEnumerable<LinhaItemFiscal> itens, IReadOnlyList<Produto> produtos)
    {
        var porCodigo = new Dictionary<string, Produto>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in produtos) { porCodigo.TryAdd(p.Codigo.ToString(), p); porCodigo.TryAdd(p.Codigo.ToString("D6"), p); }
        var porDescricao = produtos.GroupBy(p => p.Descricao.Trim().ToUpperInvariant()).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());
        var lista = new List<DivergenciaTributacao>();
        foreach (var l in itens.Where(i => i.Situacao == SituacaoDocumento.Autorizada))
        {
            var it = l.Item;
            var p = it.CodProduto is { } cod && porCodigo.TryGetValue(cod.Trim(), out var pc) ? pc
                  : it.Descricao is { } d && porDescricao.TryGetValue(d.Trim().ToUpperInvariant(), out var pd) ? pd : null;
            if (p is null) continue;
            void Add(string campo, string? nota, string? cad)
            {
                if (string.IsNullOrWhiteSpace(cad) || string.IsNullOrWhiteSpace(nota)) return;
                if (!string.Equals(nota.Trim(), cad.Trim(), StringComparison.OrdinalIgnoreCase))
                    lista.Add(new(l.DataLivro, l.Numero, l.Operacao, l.ParticipanteNome, $"{p.Codigo} – {p.Descricao}", campo, nota.Trim(), cad.Trim()));
            }
            var saida = l.Operacao == TipoOperacao.Saida;
            Add("NCM", it.Ncm, p.Ncm);
            if (saida) Add("Origem", it.Origem, p.Origem);
            var cstCad = (saida ? p.CstIcmsSaida : p.CstIcmsEntrada)?.Trim();
            if (it.CstIcms?.Trim() is { Length: > 0 } cstNota && !string.IsNullOrEmpty(cstCad) && !MesmoCst(cstNota, cstCad))
                lista.Add(new(l.DataLivro, l.Numero, l.Operacao, l.ParticipanteNome, $"{p.Codigo} – {p.Descricao}", saida ? "CST do ICMS (saída)" : "CST do ICMS (entrada)",
                              cstNota, cstCad));
            var cfopCad = saida ? p.CfopSaida : p.CfopEntrada;
            if (cfopCad is { Length: 4 } && it.Cfop is { Length: 4 } && it.Cfop[1..] != cfopCad[1..])
                lista.Add(new(l.DataLivro, l.Numero, l.Operacao, l.ParticipanteNome, $"{p.Codigo} – {p.Descricao}", "Natureza do CFOP", it.Cfop, cfopCad));
            if (saida && it.Cfop.StartsWith('5'))
            {
                if (p.AliqIcms > 0 && it.AliqIcms > 0 && it.AliqIcms != p.AliqIcms) Add("Alíquota do ICMS %", Num(it.AliqIcms), Num(p.AliqIcms));
                if (p.AliqIpi > 0 && it.AliqIpi != p.AliqIpi) Add("Alíquota do IPI %", Num(it.AliqIpi), Num(p.AliqIpi));
            }
        }
        return lista.OrderBy(d => d.Data).ThenBy(d => d.Numero).ThenBy(d => d.Produto).ToList();
    }

    public static TabelaRelatorio Tabela(IReadOnlyList<DivergenciaTributacao> lista)
        => new("Conferência de tributação (nota x cadastro do produto)",
            [new("Data", TipoColuna.Data, 2.1), new("Nota", TipoColuna.Texto, 1.8), new("Operação", TipoColuna.Texto, 1.8), new("Cliente/fornecedor", TipoColuna.Texto, 4.5),
             new("Produto"), new("Campo", TipoColuna.Texto, 3.5), new("Na nota", TipoColuna.Texto, 2.2), new("No cadastro", TipoColuna.Texto, 2.2)],
            lista.Select(d => new LinhaRelatorio([d.Data, d.Numero, d.Operacao == TipoOperacao.Saida ? "Saída" : "Entrada", d.Participante ?? "", d.Produto, d.Campo,
                                                  d.NaNota, d.NoCadastro])).ToList(),
            $"{lista.Count} divergência(s) em {lista.Select(d => (d.Numero, d.Operacao)).Distinct().Count()} nota(s). Corrija a nota ou o cadastro do produto.");
}

public sealed class ServicoConferenciaTributacao(IRepositorioDocumentos documentos, IRepositorioProdutos produtos, Autorizador autorizador)
{
    public async Task<Resultado<IReadOnlyList<DivergenciaTributacao>>> ConferirAsync(long empresaId, DateOnly inicio, DateOnly fim, TipoOperacao? operacao,
                                                                                    long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<IReadOnlyList<DivergenciaTributacao>>(usuarioId, empresaId, Permissao.FiscalRelatorios, ct) is { } negado) return negado;
        if (fim < inicio) return Resultado<IReadOnlyList<DivergenciaTributacao>>.Falha("Data final anterior à inicial.");
        var itens = await documentos.ListarItensAsync(empresaId, inicio, fim, operacao, ct);
        return Resultado<IReadOnlyList<DivergenciaTributacao>>.Ok(ConferenciaTributacao.Conferir(itens, await produtos.ListarAsync(empresaId, false, null, ct)));
    }
}
