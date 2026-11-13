using Vorticis.Application.Relatorios;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Regras;

namespace Vorticis.Fiscal.Relatorios;

/// <summary>DIFAL de um item de entrada interestadual para uso e consumo ou ativo imobilizado.</summary>
public sealed record LinhaDifal(DateOnly Data, string Numero, string? Fornecedor, string? UfOrigem, string Cfop, string? Produto, decimal Valor,
                                decimal AliqInterestadual, decimal IcmsOrigem, decimal BaseDifal, decimal AliqInterna, decimal Difal);

/// <summary>
/// Diferencial de alíquota do ICMS (EC 87/2015; LC 87/1996, art. 13, § 1º, e LC 190/2022) nas entradas de outra UF para uso e consumo
/// (CFOP 2556/2557) e ativo imobilizado (2551/2552). A alíquota interestadual é a destacada na nota ou, sem destaque, a da Resolução do Senado
/// (4% para importados; 7% de Sul/Sudeste, exceto ES, para Norte, Nordeste, Centro-Oeste e ES; 12% nos demais casos).
/// </summary>
public static class CalculoDifal
{
    public static readonly IReadOnlySet<string> Cfops = new HashSet<string> { "2551", "2552", "2556", "2557" };
    private static readonly HashSet<string> SulSudesteSemEs = ["SP", "RJ", "MG", "PR", "SC", "RS"];

    public static decimal AliquotaInterestadual(string? ufOrigem, string ufDestino, string? origemMercadoria)
    {
        if (origemMercadoria is "1" or "2" or "3" or "8") return 4m;
        return SulSudesteSemEs.Contains(ufOrigem ?? "") && !SulSudesteSemEs.Contains(ufDestino) ? 7m : 12m;
    }

    /// <param name="baseDupla">Base "por dentro" (o ICMS do destino integra a base), adotada pela maioria das UFs; falso = base única.</param>
    public static IReadOnlyList<LinhaDifal> Calcular(IEnumerable<LinhaItemFiscal> itens, string ufDestino, decimal aliqInterna, bool baseDupla)
    {
        var lista = new List<LinhaDifal>();
        foreach (var i in itens.Where(i => i.Situacao == SituacaoDocumento.Autorizada && i.Operacao == TipoOperacao.Entrada && Cfops.Contains(i.Item.Cfop)
                                           && !string.Equals(i.Uf, ufDestino, StringComparison.OrdinalIgnoreCase)))
        {
            var v = i.Item.ValorContabil;
            var inter = i.Item.AliqIcms > 0 ? i.Item.AliqIcms : AliquotaInterestadual(i.Uf, ufDestino, i.Item.Origem);
            if (inter >= aliqInterna || v <= 0) continue;
            var icmsOrigem = Dinheiro.Arredondar(v * inter / 100m);
            decimal baseDifal, difal;
            if (baseDupla)
            {
                baseDifal = Dinheiro.Arredondar((v - icmsOrigem) / (1 - aliqInterna / 100m));
                difal = Dinheiro.Arredondar(baseDifal * aliqInterna / 100m - icmsOrigem);
            }
            else
            {
                baseDifal = v;
                difal = Dinheiro.Arredondar(v * (aliqInterna - inter) / 100m);
            }
            lista.Add(new LinhaDifal(i.DataLivro, i.Numero, i.ParticipanteNome, i.Uf, i.Item.Cfop, i.Item.Descricao, v, inter, icmsOrigem, baseDifal, aliqInterna, difal));
        }
        return lista.OrderBy(l => l.Data).ThenBy(l => l.Numero).ToList();
    }

    public static TabelaRelatorio Tabela(IReadOnlyList<LinhaDifal> linhas, bool baseDupla)
    {
        var V = TipoColuna.Valor;
        var rows = linhas.Select(l => new LinhaRelatorio([l.Data, l.Numero, l.Fornecedor ?? "", l.UfOrigem ?? "", l.Cfop, l.Produto ?? "", l.Valor, l.AliqInterestadual,
                                                          l.IcmsOrigem, l.BaseDifal, l.AliqInterna, l.Difal])).ToList();
        rows.Add(new LinhaRelatorio([null, "", "TOTAL", "", "", "", linhas.Sum(l => l.Valor), null, linhas.Sum(l => l.IcmsOrigem), linhas.Sum(l => l.BaseDifal), null,
                                     linhas.Sum(l => l.Difal)], Destaque: true));
        var uso = linhas.Where(l => l.Cfop is "2556" or "2557").Sum(l => l.Difal);
        return new TabelaRelatorio("DIFAL – uso e consumo e ativo imobilizado",
            [new("Entrada", TipoColuna.Data, 2.1), new("Nota", TipoColuna.Texto, 1.8), new("Fornecedor", TipoColuna.Texto, 4.5), new("UF", TipoColuna.Texto, 0.9),
             new("CFOP", TipoColuna.Texto, 1.2), new("Produto"), new("Valor", V), new("Alíq. inter. %", V), new("ICMS origem", V), new("Base DIFAL", V),
             new("Alíq. interna %", V), new("DIFAL", V)],
            rows, $"Uso e consumo: {Formato.Moeda(uso)} · Ativo imobilizado: {Formato.Moeda(linhas.Sum(l => l.Difal) - uso)} · "
                  + (baseDupla ? "Base dupla (por dentro)." : "Base única.") + " Confira a alíquota interna do produto (há alíquotas diferentes por NCM e o adicional do fundo de pobreza).");
    }
}

/// <summary>Fiscal › Apuração › DIFAL: calcula o diferencial de alíquota do período pela UF da empresa.</summary>
public sealed class ServicoDifal(IRepositorioDocumentos documentos, IRepositorioRegrasFiscais regras, Autorizador autorizador)
{
    public async Task<Resultado<(string Uf, IReadOnlyList<LinhaDifal> Linhas)>> CalcularAsync(long empresaId, DateOnly inicio, DateOnly fim, decimal aliqInterna,
                                                                                            bool baseDupla, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<(string, IReadOnlyList<LinhaDifal>)>(usuarioId, empresaId, Permissao.FiscalRelatorios, ct) is { } negado) return negado;
        if (fim < inicio) return Resultado<(string, IReadOnlyList<LinhaDifal>)>.Falha("Data final anterior à inicial.");
        if (aliqInterna is <= 0 or >= 40) return Resultado<(string, IReadOnlyList<LinhaDifal>)>.Falha("Informe a alíquota interna do ICMS da sua UF (por exemplo, 18).");
        if (await regras.UfEmpresaAsync(empresaId, ct) is not { Length: 2 } uf)
            return Resultado<(string, IReadOnlyList<LinhaDifal>)>.Falha("Informe a UF no cadastro da empresa.");
        var itens = await documentos.ListarItensAsync(empresaId, inicio, fim, TipoOperacao.Entrada, ct);
        return Resultado<(string, IReadOnlyList<LinhaDifal>)>.Ok((uf, CalculoDifal.Calcular(itens, uf, aliqInterna, baseDupla)));
    }
}
