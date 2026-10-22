using Vorticis.Application.Relatorios;
using Vorticis.Domain.Comum;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Guias;
using Vorticis.Fiscal.Lucro;
using Vorticis.Fiscal.Retencoes;
using Vorticis.Fiscal.Simples;

namespace Vorticis.Fiscal.Relatorios;

/// <summary>
/// Tabelas dos relatórios fiscais no formato comum de exportação (<see cref="TabelaRelatorio"/>): a mesma tabela aparece na tela e sai em PDF,
/// Excel e CSV. Puro: só monta linhas a partir dos resultados dos serviços.
/// </summary>
public static class TabelasFiscais
{
    private static LinhaRelatorio L(bool destaque, params object?[] valores) => new(valores, destaque);

    public static TabelaRelatorio Livro(TipoOperacao operacao, IReadOnlyList<LinhaLivro> linhas)
    {
        var titulo = operacao == TipoOperacao.Entrada ? "Livro de Registro de Entradas" : "Livro de Registro de Saídas";
        var colunas = new List<ColunaRelatorio>
        {
            new(operacao == TipoOperacao.Entrada ? "Entrada" : "Saída", TipoColuna.Data), new("Mod.", LarguraCm: 1.1), new("Sér.", LarguraCm: 0.9), new("Número", LarguraCm: 1.5),
            new("Emissão", TipoColuna.Data), new(operacao == TipoOperacao.Entrada ? "Fornecedor" : "Cliente"), new("CNPJ/CPF", LarguraCm: 3.2), new("UF", LarguraCm: 0.8),
            new("CFOP", LarguraCm: 1.2), new("Valor contábil", TipoColuna.Valor), new("BC ICMS", TipoColuna.Valor), new("Alíq.", TipoColuna.Valor, 1.2), new("ICMS", TipoColuna.Valor),
            new("ICMS-ST", TipoColuna.Valor), new("IPI", TipoColuna.Valor), new("PIS", TipoColuna.Valor), new("COFINS", TipoColuna.Valor), new("ISS", TipoColuna.Valor), new("Obs.", LarguraCm: 2),
        };
        var rows = linhas.Select(l => L(false, l.DataLivro, l.Modelo.Codigo() == "NFSE" ? "NFS" : l.Modelo.Codigo(), l.Serie, l.Numero, l.Emissao, l.Participante ?? "", l.Documento ?? "",
            l.Uf ?? "", l.Cfop, l.ValorContabil, l.BcIcms, l.AliqIcms, l.Icms, l.IcmsSt, l.Ipi, l.Pis, l.Cofins, l.Iss, l.Observacao)).ToList();
        rows.Add(L(true, null, "", "", "TOTAL", null, "", "", "", "", linhas.Sum(l => l.ValorContabil), linhas.Sum(l => l.BcIcms), null, linhas.Sum(l => l.Icms), linhas.Sum(l => l.IcmsSt),
            linhas.Sum(l => l.Ipi), linhas.Sum(l => l.Pis), linhas.Sum(l => l.Cofins), linhas.Sum(l => l.Iss), ""));
        return new TabelaRelatorio(titulo, colunas, rows, $"{linhas.Select(l => l.DocumentoId).Distinct().Count()} documento(s), {linhas.Count} linha(s) por CFOP. Cancelados e denegados aparecem sem valores.");
    }

    public static TabelaRelatorio ResumoCfop(IReadOnlyList<ResumoCfop> resumo)
    {
        var rows = resumo.Select(r => L(false, r.Cfop, Cfop.Ambito(r.Cfop), r.Documentos, r.ValorContabil, r.BcIcms, r.Icms, r.IcmsSt, r.Ipi, r.Pis, r.Cofins, r.Iss)).ToList();
        var t = LivrosFiscais.Total(resumo);
        rows.Add(L(true, "TOTAL", "", t.Documentos, t.ValorContabil, t.BcIcms, t.Icms, t.IcmsSt, t.Ipi, t.Pis, t.Cofins, t.Iss));
        return new TabelaRelatorio("Resumo por CFOP",
        [
            new("CFOP", LarguraCm: 1.8), new("Âmbito", LarguraCm: 3.5), new("Docs.", TipoColuna.Inteiro), new("Valor contábil", TipoColuna.Valor), new("BC ICMS", TipoColuna.Valor),
            new("ICMS", TipoColuna.Valor), new("ICMS-ST", TipoColuna.Valor), new("IPI", TipoColuna.Valor), new("PIS", TipoColuna.Valor), new("COFINS", TipoColuna.Valor), new("ISS", TipoColuna.Valor),
        ], rows);
    }

    public static IReadOnlyList<TabelaRelatorio> Simples(ResultadoSimples r)
    {
        var seg = new TabelaRelatorio($"Simples Nacional · {r.Competencia:MM/yyyy}",
        [
            new("Anexo", LarguraCm: 1.4), new("Atividade"), new("Tratamento"), new("Receita", TipoColuna.Valor), new("Faixa", TipoColuna.Inteiro), new("Alíq. nominal", TipoColuna.Valor, 2.2),
            new("Parcela a deduzir", TipoColuna.Valor, 2.8), new("Alíq. efetiva", TipoColuna.Valor, 2.2), new("Alíq. aplicada", TipoColuna.Valor, 2.2), new("DAS", TipoColuna.Valor),
        ],
        r.Segmentos.Select(s => L(false, s.Anexo.ToString(), s.Atividade.ToString(), s.Tratamento == TratamentoSimples.Normal ? "Normal" : s.Tratamento.ToString(), s.Receita, s.Faixa,
            s.AliquotaNominal, s.ParcelaDeduzir, s.AliquotaEfetiva, s.AliquotaAplicada, s.ValorDas))
         .Append(L(true, "", "TOTAL", "", r.ReceitaMes, null, null, null, null, null, r.ValorDas)).ToList(),
        $"RBT12 {Formato.Moeda(r.Rbt12)} · folha 12 meses {Formato.Moeda(r.Folha12)} · Fator R {r.FatorR * 100:0.00}% · vencimento {r.Vencimento:dd/MM/yyyy}");
        var tributos = new TabelaRelatorio("Repartição do DAS por tributo", [new("Tributo"), new("Valor", TipoColuna.Valor)],
            r.TotalPorTributo.OrderBy(t => t.Key).Select(t => L(false, t.Key, t.Value)).Append(L(true, "TOTAL", r.ValorDas)).ToList());
        var memoria = new TabelaRelatorio("Memória de cálculo", [new("Descrição")], r.Memoria.Concat(r.Avisos.Select(a => "ATENÇÃO: " + a)).Select(m => L(false, m)).ToList());
        return [seg, tributos, memoria];
    }

    public static IReadOnlyList<TabelaRelatorio> Lucro(ResultadoLucro r)
    {
        var real = r.Regime == RegimeLucro.Real;
        var linhas = new List<LinhaRelatorio>();
        void Add(string d, decimal v, bool destaque = false) => linhas.Add(L(destaque, d, v));
        Add("Receita bruta do trimestre", r.ReceitaBruta);
        if (real)
        {
            Add("Lucro contábil antes do IRPJ e da CSLL", r.LucroContabil ?? 0);
            Add("(+) Adições IRPJ (LALUR)", r.AdicoesIrpj);
            Add("(−) Exclusões IRPJ (LALUR)", r.ExclusoesIrpj);
            Add("(−) Compensação de prejuízos fiscais (limite 30%)", r.CompensacaoIrpj);
        }
        Add(real ? "Lucro real (base do IRPJ)" : "Base de cálculo do IRPJ (presunção + outras receitas)", r.BaseIrpj, true);
        Add("IRPJ 15%", r.IrpjNormal);
        Add("Adicional de IRPJ 10%", r.IrpjAdicional);
        Add("(−) IRRF e deduções", r.IrpjDeducoes);
        Add("IRPJ a pagar", r.IrpjAPagar, true);
        if (real)
        {
            Add("(+) Adições CSLL (LACS)", r.AdicoesCsll);
            Add("(−) Exclusões CSLL (LACS)", r.ExclusoesCsll);
            Add("(−) Compensação de base negativa (limite 30%)", r.CompensacaoCsll);
        }
        Add("Base de cálculo da CSLL", r.BaseCsll, true);
        Add("CSLL 9%", r.CsllDevida);
        Add("(−) CSLL retida", r.CsllDeducoes);
        Add("CSLL a pagar", r.CsllAPagar, true);
        if (r.PrejuizoIrpj > 0) Add("Prejuízo fiscal do trimestre (Parte B)", r.PrejuizoIrpj);
        if (r.PrejuizoCsll > 0) Add("Base negativa da CSLL do trimestre (Parte B)", r.PrejuizoCsll);
        var titulo = $"{(real ? "Lucro Real" : "Lucro Presumido")} · {r.Trimestre}º trimestre de {r.Ano}";
        return [new TabelaRelatorio(titulo, [new("Descrição"), new("Valor", TipoColuna.Valor)], linhas, $"Vencimento (cota única) {r.Vencimento:dd/MM/yyyy}"),
                new TabelaRelatorio("Memória de cálculo", [new("Descrição")], r.Memoria.Concat(r.Avisos.Select(a => "ATENÇÃO: " + a)).Select(m => L(false, m)).ToList())];
    }

    public static IReadOnlyList<TabelaRelatorio> PisCofins(ResultadoPisCofins r)
    {
        var linhas = new List<LinhaRelatorio>
        {
            L(false, "Receita da base de cálculo", r.ReceitaBase, null), L(false, "Alíquota (%)", r.AliquotaPis, r.AliquotaCofins),
            L(false, "Débito", r.PisDebito, r.CofinsDebito), L(false, "(−) Créditos", r.PisCredito, r.CofinsCredito), L(false, "(−) Retenções na fonte", r.PisRetido, r.CofinsRetido),
            L(true, "A pagar", r.PisAPagar, r.CofinsAPagar), L(false, "Saldo credor a transportar", r.SaldoCredorPis, r.SaldoCredorCofins),
        };
        return [new TabelaRelatorio($"PIS e COFINS {(r.Regime == RegimePisCofins.Cumulativo ? "cumulativos" : "não cumulativos")} · {r.Competencia:MM/yyyy}",
                    [new("Descrição"), new("PIS", TipoColuna.Valor), new("COFINS", TipoColuna.Valor)], linhas, $"Base dos créditos {Formato.Moeda(r.BaseCreditos)} · vencimento {r.Vencimento:dd/MM/yyyy}"),
                new TabelaRelatorio("Memória de cálculo", [new("Descrição")], r.Memoria.Concat(r.Avisos.Select(a => "ATENÇÃO: " + a)).Select(m => L(false, m)).ToList())];
    }

    public static TabelaRelatorio Guias(IReadOnlyList<Guia> guias)
    {
        var rows = guias.Select(g => L(false, g.Vencimento, g.Competencia.ToString("MM/yyyy"), g.RotuloDocumento, g.Descricao, g.CodigoReceita ?? "", g.Valor, g.Paga ? "Paga" : "A pagar",
            g.DataPagamento, g.Observacao ?? "")).ToList();
        rows.Add(L(true, null, "", "", "TOTAL A PAGAR", "", guias.Where(g => !g.Paga).Sum(g => g.Valor), "", null, ""));
        return new TabelaRelatorio("Guias de recolhimento a pagar",
            [new("Vencimento", TipoColuna.Data), new("Compet.", LarguraCm: 1.8), new("Doc.", LarguraCm: 1.6), new("Descrição"), new("Cód. receita", LarguraCm: 2), new("Valor", TipoColuna.Valor),
             new("Situação", LarguraCm: 1.8), new("Pago em", TipoColuna.Data), new("Observação")], rows,
            "DAS vence no dia 20 (prorroga); DARF federal antecipa para o dia útil anterior; GPS/DCTFWeb é informativo; o ISS segue a legislação do município.");
    }

    public static TabelaRelatorio Retencoes(IReadOnlyList<Retencao> retencoes)
    {
        var rows = retencoes.Select(r => L(false, r.Competencia.ToString("MM/yyyy"), r.Sentido == SentidoRetencao.Sofrida ? "Sofrida" : "Efetuada", r.Tributo.ToString(), r.DocumentoNumero ?? "",
            r.ParticipanteNome ?? "", r.BaseCalculo, r.Aliquota, r.Valor, r.CodigoReceita ?? "", r.Origem == "NOTA" ? "Nota" : "Manual", r.Descricao ?? "")).ToList();
        rows.Add(L(true, "", "", "", "", "TOTAL", null, null, retencoes.Sum(r => r.Valor), "", "", ""));
        return new TabelaRelatorio("Retenções (IRRF, PIS, COFINS, CSLL, INSS, ISS)",
            [new("Compet.", LarguraCm: 1.8), new("Sentido", LarguraCm: 1.9), new("Tributo", LarguraCm: 1.8), new("Nota", LarguraCm: 1.5), new("Participante"), new("Base", TipoColuna.Valor),
             new("Alíq. %", TipoColuna.Valor, 1.6), new("Valor", TipoColuna.Valor), new("Cód. receita", LarguraCm: 2), new("Origem", LarguraCm: 1.6), new("Descrição")], rows,
            "Sofrida: retida por clientes (crédito a compensar). Efetuada: retida de fornecedores (a recolher).");
    }
}
