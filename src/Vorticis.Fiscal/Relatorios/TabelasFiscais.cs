using Vorticis.Application.Relatorios;
using Vorticis.Domain.Comum;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Duplicatas;
using Vorticis.Fiscal.Guias;
using Vorticis.Fiscal.Lucro;
using Vorticis.Fiscal.Produtos;
using Vorticis.Fiscal.Retencoes;
using Vorticis.Fiscal.Simples;
using Vorticis.Fiscal.Sped;

namespace Vorticis.Fiscal.Relatorios;

/// <summary>
/// Tabelas dos relatórios fiscais no formato comum de exportação (<see cref="TabelaRelatorio"/>): a mesma tabela aparece na tela e sai em PDF,
/// Excel e CSV. Puro: só monta linhas a partir dos resultados dos serviços.
/// </summary>
public static class TabelasFiscais
{
    private static LinhaRelatorio L(bool destaque, params object?[] valores) => new(valores, destaque);

    /// <summary>Relatório "Duplicatas a pagar" ou "Duplicatas a receber": uma linha por parcela, com o atraso na data-base.</summary>
    public static TabelaRelatorio Duplicatas(TipoDuplicata tipo, IReadOnlyList<Duplicata> lista, DateOnly dataBase)
    {
        var colunas = new List<ColunaRelatorio>
        {
            new("Vencimento", TipoColuna.Data), new("Documento", LarguraCm: 2.2), new("Parcela", LarguraCm: 1.5), new(tipo == TipoDuplicata.Pagar ? "Fornecedor" : "Cliente"),
            new("Emissão", TipoColuna.Data), new("Valor", TipoColuna.Valor), new("Baixado", TipoColuna.Valor), new("Saldo", TipoColuna.Valor), new("Situação", LarguraCm: 2),
            new("Atraso (dias)", TipoColuna.Inteiro, 1.8),
        };
        var rows = lista.Select(d => L(false, d.Vencimento, d.NumeroDocumento, d.Parcela, d.ParticipanteNome ?? "", d.Emissao, d.Valor, d.Valor - d.Saldo, d.Saldo,
            d.Situacao.Rotulo(), d.Vencida(dataBase) ? d.DiasAtraso(dataBase) : null)).ToList();
        rows.Add(L(true, null, "TOTAL", "", "", null, lista.Sum(d => d.Valor), lista.Sum(d => d.Valor - d.Saldo), lista.Sum(d => d.Saldo), "", null));
        return new TabelaRelatorio(tipo == TipoDuplicata.Pagar ? "Duplicatas a pagar" : "Duplicatas a receber", colunas, rows,
            $"{lista.Count} duplicata(s); {lista.Count(d => d.Vencida(dataBase))} vencida(s) em {dataBase:dd/MM/yyyy}.");
    }

    /// <summary>Posição por vencimento (aging): saldo em aberto por participante em cada faixa de atraso.</summary>
    public static TabelaRelatorio PosicaoPorVencimento(TipoDuplicata tipo, IReadOnlyList<LinhaAging> linhas, DateOnly dataBase)
    {
        var colunas = new List<ColunaRelatorio> { new(tipo == TipoDuplicata.Pagar ? "Fornecedor" : "Cliente") };
        colunas.AddRange(Aging.Rotulos.Select(r => new ColunaRelatorio(r, TipoColuna.Valor)));
        colunas.Add(new ColunaRelatorio("Total", TipoColuna.Valor));
        var rows = linhas.Select(l => L(false, [l.Participante, .. l.Faixas.Cast<object?>(), l.Total])).ToList();
        rows.Add(L(true, ["TOTAL", .. Enumerable.Range(0, Aging.Rotulos.Count).Select(i => (object?)linhas.Sum(l => l.Faixas[i])), linhas.Sum(l => l.Total)]));
        return new TabelaRelatorio($"Posição por vencimento — duplicatas {tipo.Rotulo().ToLowerInvariant()}", colunas, rows,
            $"Saldo em aberto em {dataBase:dd/MM/yyyy}. Só duplicatas abertas ou com baixa parcial.");
    }

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

    private static readonly ColunaRelatorio[] ColunasApuracao =
        [new("Descrição"), new("CFOP", LarguraCm: 1.8), new("Valor contábil", TipoColuna.Valor), new("Base de cálculo", TipoColuna.Valor), new("Imposto", TipoColuna.Valor)];

    private static IEnumerable<LinhaRelatorio> BlocoCfop(string titulo, IReadOnlyList<LinhaApuracaoCfop> linhas, string total)
    {
        yield return L(true, titulo, "", null, null, null);
        foreach (var l in linhas) yield return L(false, "   Operações com o CFOP", l.Cfop, l.Operacao, l.Base, l.Imposto);
        yield return L(true, total, "", linhas.Sum(l => l.Operacao), linhas.Sum(l => l.Base), linhas.Sum(l => l.Imposto));
    }

    private static LinhaRelatorio Valor(string descricao, decimal v) => L(false, descricao, "", null, null, v);

    /// <summary>Livro de Registro de Apuração do ICMS (modelo 9); os valores são os do registro E110 da EFD ICMS/IPI.</summary>
    public static TabelaRelatorio LivroApuracaoIcms(LivroApuracao livro)
    {
        var a = livro.Icms;
        var rows = new List<LinhaRelatorio>();
        rows.AddRange(BlocoCfop("1. DÉBITO DO IMPOSTO — saídas e prestações", a.DebitosCfop ?? [], "Total dos débitos por saídas e prestações"));
        rows.Add(Valor("   Outros débitos (ajustes de saída)", a.AjustesDebitos));
        rows.Add(Valor("   Estornos de créditos (ajustes de saída)", a.EstornosCreditos));
        rows.Add(L(true, "Total dos débitos", "", null, null, a.Debitos + a.AjustesDebitos + a.EstornosCreditos));
        rows.AddRange(BlocoCfop("2. CRÉDITO DO IMPOSTO — entradas e aquisições", a.CreditosCfop ?? [], "Total dos créditos por entradas"));
        rows.Add(Valor("   Outros créditos (ajustes de entrada)", a.AjustesCreditos));
        rows.Add(Valor("   Estornos de débitos (ajustes de entrada)", a.EstornosDebitos));
        rows.Add(L(true, "Total dos créditos", "", null, null, a.Creditos + a.AjustesCreditos + a.EstornosDebitos));
        rows.Add(L(true, "3. APURAÇÃO DO SALDO", "", null, null, null));
        rows.Add(Valor("   Saldo credor do período anterior", a.SaldoCredorAnterior));
        rows.Add(Valor("   Saldo devedor apurado", Math.Max(0, a.SaldoApurado)));
        rows.Add(Valor("   Deduções", a.Deducoes));
        if (a.DebitosEspeciais > 0) rows.Add(Valor("   Débitos especiais", a.DebitosEspeciais));
        rows.Add(L(true, "Imposto a recolher", "", null, null, a.Recolher));
        rows.Add(L(true, "Saldo credor a transportar para o período seguinte", "", null, null, a.SaldoCredorTransportar));
        return new TabelaRelatorio($"Livro de Registro de Apuração do ICMS — {livro.Inicio:MM/yyyy}", ColunasApuracao, rows,
            "Mesmos valores do registro E110 da EFD ICMS/IPI. Notas canceladas e denegadas e NFS-e não entram.");
    }

    /// <summary>Livro de Registro de Apuração do IPI (modelo 8); os valores são os do registro E520 da EFD ICMS/IPI.</summary>
    public static TabelaRelatorio LivroApuracaoIpi(LivroApuracao livro)
    {
        var ipi = livro.Ipi;
        var linhas = ipi?.Linhas ?? [];
        var rows = new List<LinhaRelatorio>();
        LinhaApuracaoCfop[] Por(Func<string, bool> f) => [.. linhas.Where(l => f(l.Cfop)).GroupBy(l => l.Cfop)
            .Select(g => new LinhaApuracaoCfop(g.Key, g.Sum(l => l.Contabil), g.Sum(l => l.Base), g.Sum(l => l.Ipi)))];
        rows.AddRange(BlocoCfop("1. SAÍDAS — débito do imposto", Por(CamposEfd.CfopSaida), "Total dos débitos por saídas"));
        rows.Add(Valor("   Outros débitos (ajustes de saída)", ipi?.OutrosDebitos ?? 0));
        rows.Add(L(true, "Total dos débitos", "", null, null, (ipi?.Debitos ?? 0) + (ipi?.OutrosDebitos ?? 0)));
        rows.AddRange(BlocoCfop("2. ENTRADAS — crédito do imposto", Por(CamposEfd.CfopEntrada), "Total dos créditos por entradas"));
        rows.Add(Valor("   Outros créditos (ajustes de entrada)", ipi?.OutrosCreditos ?? 0));
        rows.Add(L(true, "Total dos créditos", "", null, null, (ipi?.Creditos ?? 0) + (ipi?.OutrosCreditos ?? 0)));
        rows.Add(L(true, "3. APURAÇÃO DO SALDO", "", null, null, null));
        rows.Add(Valor("   Saldo credor do período anterior", 0));
        rows.Add(L(true, "Saldo devedor (imposto a recolher)", "", null, null, ipi?.SaldoDevedor ?? 0));
        rows.Add(L(true, "Saldo credor a transportar para o período seguinte", "", null, null, ipi?.SaldoCredor ?? 0));
        return new TabelaRelatorio($"Livro de Registro de Apuração do IPI — {livro.Inicio:MM/yyyy}", ColunasApuracao, rows,
            "Mesmos valores dos registros E510 e E520 da EFD ICMS/IPI. Só entram os itens com situação tributária de IPI informada.");
    }

    // ------------------------------------------------------------------ apuração do SPED Fiscal (Relatórios › Livros e apuração)

    private static readonly ColunaRelatorio[] ColunasCampoValor =
        [new("Campo", LarguraCm: 1.6), new("Descrição"), new("Valor", TipoColuna.Valor)];

    /// <summary>Resumo da apuração do ICMS: um campo do registro E110 por linha, já com os ajustes.</summary>
    public static TabelaRelatorio ResumoApuracaoIcms(ApuracaoSped a)
    {
        var ap = a.Icms;
        var rows = new List<LinhaRelatorio>
        {
            L(false, "02", "Valor total dos débitos por saídas e prestações (VL_TOT_DEBITOS)", ap.Debitos),
            L(false, "04", "Ajustes a débito — outros débitos (VL_TOT_AJ_DEBITOS)", ap.AjustesDebitos),
            L(false, "05", "Estornos de créditos (VL_ESTORNOS_CRED)", ap.EstornosCreditos),
            L(false, "06", "Valor total dos créditos por entradas e aquisições (VL_TOT_CREDITOS)", ap.Creditos),
            L(false, "08", "Ajustes a crédito — outros créditos (VL_TOT_AJ_CREDITOS)", ap.AjustesCreditos),
            L(false, "09", "Estornos de débitos (VL_ESTORNOS_DEB)", ap.EstornosDebitos),
            L(false, "10", "Saldo credor do período anterior (VL_SLD_CREDOR_ANT)", ap.SaldoCredorAnterior),
            L(true, "11", "Saldo devedor apurado (VL_SLD_APURADO)", Math.Max(0, ap.SaldoApurado)),
            L(false, "12", "Total de deduções (VL_TOT_DED)", ap.Deducoes),
            L(true, "13", "ICMS a recolher (VL_ICMS_RECOLHER)", ap.Recolher),
            L(true, "14", "Saldo credor a transportar (VL_SLD_CREDOR_TRANSPORTAR)", ap.SaldoCredorTransportar),
            L(false, "15", "Valores recolhidos ou a recolher, extra-apuração (DEB_ESP)", ap.DebitosEspeciais),
        };
        return new TabelaRelatorio($"Resumo da apuração do ICMS (E110) — {a.Inicio:MM/yyyy}", ColunasCampoValor, rows,
            $"{(a.Dados.Ajustes ?? []).Count(x => x.Imposto == ImpostosAjuste.Icms)} ajuste(s) do ICMS próprio considerado(s). Mesmos valores do registro E110 da EFD ICMS/IPI; notas canceladas, denegadas e NFS-e não entram.");
    }

    private static string TipoDoAjuste(AjusteIcmsEfd x) =>
        x.Imposto == ImpostosAjuste.Ipi ? (x.SentidoEfetivo == 'E' ? "Crédito" : "Débito") : OutrosLancamentosEfd.RotuloTipo(x.Tipo);

    private static string DocumentoDoAjuste(AjusteIcmsEfd x) =>
        x.TemDocumento ? $"Doc. {x.DocModelo} {x.DocSerie}/{x.DocNumero} de {x.DocData:dd/MM/yyyy}{(string.IsNullOrWhiteSpace(x.Observacao) ? "" : " — " + x.Observacao)}" : x.Observacao ?? "";

    /// <summary>Relação de ajustes de um intervalo de competências, com total por código.</summary>
    public static TabelaRelatorio RelacaoAjustes(IReadOnlyList<AjusteIcmsEfd> ajustes, DateOnly inicio, DateOnly fim)
    {
        var colunas = new List<ColunaRelatorio>
        {
            new("Competência", TipoColuna.Data), new("Imposto", LarguraCm: 1.8), new("Sentido", LarguraCm: 1.8), new("Código", LarguraCm: 2.2), new("Tipo", LarguraCm: 3.4),
            new("Descrição"), new("Documento / observação"), new("Valor", TipoColuna.Valor),
        };
        var rows = ajustes.OrderBy(a => a.Competencia).ThenBy(a => a.Imposto).ThenBy(a => a.Codigo, StringComparer.Ordinal).ThenBy(a => a.Id)
            .Select(a => L(false, a.Competencia, ImpostosAjuste.Rotulo(a.Imposto), a.SentidoEfetivo == 'E' ? "Entrada" : "Saída", a.Codigo, TipoDoAjuste(a), a.Descricao, DocumentoDoAjuste(a), a.Valor))
            .ToList();
        foreach (var g in ajustes.GroupBy(a => (a.Imposto, a.Codigo)).OrderBy(g => g.Key.Imposto).ThenBy(g => g.Key.Codigo, StringComparer.Ordinal))
            rows.Add(L(true, null, ImpostosAjuste.Rotulo(g.Key.Imposto), "", g.Key.Codigo, "Total do código", $"{g.Count()} lançamento(s)", "", g.Sum(a => a.Valor)));
        rows.Add(L(true, null, "", "", "", "TOTAL GERAL", $"{ajustes.Count} lançamento(s)", "", ajustes.Sum(a => a.Valor)));
        return new TabelaRelatorio($"Relação de ajustes da apuração — {inicio:MM/yyyy} a {fim:MM/yyyy}", colunas, rows,
            "Ajustes do ICMS próprio (E111), do ICMS-ST (E220) e do IPI (E530) lançados em Lançamentos › Ajustes.");
    }

    private static readonly ColunaRelatorio[] ColunasConferencia =
    [
        new("Item"), new("Código", LarguraCm: 2), new("CFOP", LarguraCm: 1.5), new("Alíq. %", TipoColuna.Valor, 1.4), new("Qtde.", TipoColuna.Inteiro, 1.4),
        new("Valor da operação", TipoColuna.Valor), new("Base ICMS", TipoColuna.Valor), new("ICMS (notas)", TipoColuna.Valor), new("Apuração (E110)", TipoColuna.Valor), new("Situação", LarguraCm: 3.2),
    ];

    /// <summary>
    /// Conferência da EFD ICMS/IPI do mês: registros por bloco, C190/D190 por CST, CFOP e alíquota, confronto das notas com o E110 e as pendências da validação.
    /// O arquivo é gerado só em memória.
    /// </summary>
    public static TabelaRelatorio ConferenciaEfd(ApuracaoSped a)
    {
        var rows = new List<LinhaRelatorio>();
        LinhaRelatorio Cab(string t) => L(true, t, "", "", null, null, null, null, null, null, "");

        rows.Add(Cab("1. REGISTROS DO ARQUIVO, POR BLOCO"));
        foreach (var bloco in a.Arquivo.Registros.GroupBy(r => r.Key[0]).OrderBy(g => g.Key))
        {
            rows.Add(L(true, $"Bloco {bloco.Key}", "", "", null, bloco.Sum(r => r.Value), null, null, null, null, ""));
            foreach (var r in bloco.OrderBy(r => r.Key, StringComparer.Ordinal)) rows.Add(L(false, "   Registro", r.Key, "", null, r.Value, null, null, null, null, ""));
        }
        rows.Add(L(true, "Total de linhas do arquivo", "", "", null, a.Arquivo.QuantidadeLinhas, null, null, null, null, ""));

        var docs = CalculoEfdIcms.Escrituraveis(a.Dados.Documentos).Where(d => d.Autorizada).ToList();
        var analiticos = docs.SelectMany(d => CalculoEfdIcms.Analitico(d).Select(x => (Doc: d, Item: x))).ToList();
        rows.Add(Cab("2. C190 / D190 POR CST, CFOP E ALÍQUOTA (notas autorizadas)"));
        foreach (var g in analiticos.GroupBy(x => (x.Item.Cst, x.Item.Cfop, x.Item.Aliquota)).OrderBy(g => g.Key.Cfop, StringComparer.Ordinal).ThenBy(g => g.Key.Cst, StringComparer.Ordinal).ThenBy(g => g.Key.Aliquota))
            rows.Add(L(false, g.First().Doc.Transporte && g.All(x => x.Doc.Transporte) ? "   D190 (CT-e)" : g.Any(x => x.Doc.Transporte) ? "   C190 e D190" : "   C190", g.Key.Cst, g.Key.Cfop, g.Key.Aliquota, g.Select(x => x.Doc.Id).Distinct().Count(),
                g.Sum(x => x.Item.Operacao), g.Sum(x => x.Item.BaseIcms), g.Sum(x => x.Item.Icms), null, ""));
        rows.Add(L(true, "Total dos analíticos", "", "", null, docs.Count, analiticos.Sum(x => x.Item.Operacao), analiticos.Sum(x => x.Item.BaseIcms), analiticos.Sum(x => x.Item.Icms), null, ""));

        string Sit(decimal dif) => Math.Abs(dif) <= 0.01m ? "Confere" : $"DIVERGE ({Numeros(dif)})";
        var debNotas = analiticos.Where(x => CamposEfd.CfopSaida(x.Item.Cfop)).Sum(x => x.Item.Icms);
        var credNotas = analiticos.Where(x => CamposEfd.CfopEntrada(x.Item.Cfop)).Sum(x => x.Item.Icms) + (a.Dados.Consumo ?? []).Sum(c => c.Icms);
        var cabecalho = docs.Sum(d => d.Icms);
        var itens = docs.Sum(d => d.Itens.Sum(i => i.Icms));
        rows.Add(Cab("3. NOTAS × APURAÇÃO (E110)"));
        rows.Add(L(false, "   ICMS das saídas nos analíticos × débitos do E110", "", "", null, null, null, null, debNotas, a.Icms.Debitos, Sit(debNotas - a.Icms.Debitos)));
        rows.Add(L(false, "   ICMS das entradas (e contas de consumo) nos analíticos × créditos do E110", "", "", null, null, null, null, credNotas, a.Icms.Creditos, Sit(credNotas - a.Icms.Creditos)));
        rows.Add(L(false, "   ICMS dos cabeçalhos das notas × ICMS dos itens", "", "", null, null, null, null, cabecalho, itens, Sit(cabecalho - itens)));
        var difSaldo = a.Icms.Debitos + a.Icms.AjustesDebitos + a.Icms.EstornosCreditos - a.Icms.Creditos - a.Icms.AjustesCreditos - a.Icms.EstornosDebitos - a.Icms.SaldoCredorAnterior - a.Icms.SaldoApurado;
        rows.Add(L(false, "   Saldo apurado recalculado com os ajustes × E110", "", "", null, null, null, null, a.Icms.Debitos + a.Icms.AjustesDebitos + a.Icms.EstornosCreditos - a.Icms.Creditos - a.Icms.AjustesCreditos - a.Icms.EstornosDebitos - a.Icms.SaldoCredorAnterior, a.Icms.SaldoApurado, Sit(difSaldo)));

        rows.Add(Cab("4. DIVERGÊNCIAS E PENDÊNCIAS DA VALIDAÇÃO"));
        if (a.Pendencias.Count == 0) rows.Add(L(false, "   Nenhuma pendência: o arquivo pode ser gerado.", "", "", null, null, null, null, null, null, ""));
        foreach (var p in a.Pendencias) rows.Add(L(false, "   " + p.Mensagem, p.Codigo, "", null, null, null, null, null, null, p.Bloqueante ? "Bloqueia a geração" : "Aviso"));
        return new TabelaRelatorio($"Conferência da EFD ICMS/IPI — {a.Inicio:MM/yyyy}", ColunasConferencia, rows,
            $"{docs.Count} nota(s) autorizada(s) conferida(s). Arquivo montado só para conferência; para gerar, use Obrigações › SPED › EFD ICMS/IPI.");
    }

    private static string Numeros(decimal v) => v.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

    private static readonly ColunaRelatorio[] ColunasIpi =
        [new("Descrição"), new("CFOP / CST", LarguraCm: 2.2), new("Valor contábil", TipoColuna.Valor), new("Base de cálculo", TipoColuna.Valor), new("IPI", TipoColuna.Valor)];

    /// <summary>Apuração do IPI do mês: E510 por CFOP e CST, E520 e os ajustes (E530).</summary>
    public static TabelaRelatorio ApuracaoIpi(ApuracaoSped a)
    {
        var ipi = a.Ipi;
        var rows = new List<LinhaRelatorio> { L(true, "1. OPERAÇÕES COM IPI (E510)", "", null, null, null) };
        foreach (var l in ipi?.Linhas ?? []) rows.Add(L(false, CamposEfd.CfopSaida(l.Cfop) ? "   Saída" : "   Entrada", $"{l.Cfop} / {l.Cst}", l.Contabil, l.Base, l.Ipi));
        rows.Add(L(true, "2. APURAÇÃO (E520)", "", null, null, null));
        rows.Add(L(false, "   Débitos por saídas", "", null, null, ipi?.Debitos ?? 0));
        rows.Add(L(false, "   Outros débitos (ajustes de saída)", "", null, null, ipi?.OutrosDebitos ?? 0));
        rows.Add(L(false, "   Créditos por entradas", "", null, null, ipi?.Creditos ?? 0));
        rows.Add(L(false, "   Outros créditos (ajustes de entrada)", "", null, null, ipi?.OutrosCreditos ?? 0));
        rows.Add(L(true, "   Saldo devedor (IPI a recolher)", "", null, null, ipi?.SaldoDevedor ?? 0));
        rows.Add(L(true, "   Saldo credor a transportar", "", null, null, ipi?.SaldoCredor ?? 0));
        rows.Add(L(true, "3. AJUSTES DO IPI (E530)", "", null, null, null));
        foreach (var x in (a.Dados.Ajustes ?? []).Where(x => x.Imposto == ImpostosAjuste.Ipi))
            rows.Add(L(false, $"   {(x.SentidoEfetivo == 'E' ? "Crédito" : "Débito")}: {x.Descricao}", x.Codigo, null, null, x.Valor));
        return new TabelaRelatorio($"Apuração do IPI — {a.Inicio:MM/yyyy}", ColunasIpi, rows,
            "Só entram os itens com situação tributária do IPI preenchida. Os ajustes saem no E530 da EFD ICMS/IPI.");
    }

    private static readonly ColunaRelatorio[] ColunasSt = [new("Descrição"), new("UF", LarguraCm: 1.2), new("Código", LarguraCm: 2.2), new("Valor", TipoColuna.Valor)];

    /// <summary>Apuração do ICMS-ST do mês por UF: retenção nas saídas, ajustes (E220), deduções e valor a recolher.</summary>
    public static TabelaRelatorio ApuracaoIcmsSt(ApuracaoSped a)
    {
        var rows = new List<LinhaRelatorio>();
        foreach (var st in a.Icms.St)
        {
            rows.Add(L(true, $"ICMS-ST — UF {st.Uf} (E210)", st.Uf, "", null));
            rows.Add(L(false, "   Valor retido nas saídas (VL_RETENCAO_ST)", st.Uf, "", st.Retencao));
            rows.Add(L(false, "   Outros débitos e estornos de crédito ST (VL_OUT_DEB_ST)", st.Uf, "", st.AjustesDebitos));
            rows.Add(L(false, "   Outros créditos e estornos de débito ST (VL_OUT_CRED_ST)", st.Uf, "", st.AjustesCreditos));
            rows.Add(L(false, "   Saldo devedor antes das deduções", st.Uf, "", st.SaldoAntesDeducoes));
            rows.Add(L(false, "   Deduções (VL_DEDUCOES_ST)", st.Uf, "", st.Deducoes));
            rows.Add(L(true, "   ICMS-ST a recolher (VL_ICMS_RECOL_ST)", st.Uf, "", st.Recolher));
            rows.Add(L(false, "   Débitos especiais (DEB_ESP_ST)", st.Uf, "", st.DebitosEspeciais));
            rows.Add(L(true, "   Saldo credor a transportar", st.Uf, "", st.SaldoCredorTransportar));
            foreach (var x in (a.Dados.Ajustes ?? []).Where(x => x.Imposto == ImpostosAjuste.IcmsSt && x.Uf == st.Uf))
                rows.Add(L(false, $"   Ajuste (E220): {x.Descricao}", st.Uf, x.Codigo, x.Valor));
        }
        if (rows.Count == 0) rows.Add(L(false, "Sem ICMS-ST retido nem ajustes de ICMS-ST no mês.", "", "", null));
        return new TabelaRelatorio($"Apuração do ICMS-ST — {a.Inicio:MM/yyyy}", ColunasSt, rows,
            "A retenção é a soma do ICMS-ST das saídas, por UF do destinatário. Os valores são os do registro E210 da EFD ICMS/IPI.");
    }

    public static TabelaRelatorio Produtos(IReadOnlyList<Produto> produtos)
    {
        var rows = produtos.Select(p => L(false, p.Codigo, p.Descricao, p.Unidade, p.Ncm ?? "", p.Cest ?? "", p.Gtin ?? "", p.Origem, TiposItem.Rotulo(p.TipoItem),
            p.CfopEntrada ?? "", p.CfopSaida ?? "", p.AliqIcms, p.AliqIpi, p.AliqPis, p.AliqCofins, p.Ativo ? "Sim" : "Não")).ToList();
        return new TabelaRelatorio("Produtos cadastrados",
        [
            new("Código", TipoColuna.Inteiro), new("Descrição"), new("Un.", LarguraCm: 1), new("NCM", LarguraCm: 2), new("CEST", LarguraCm: 1.8), new("GTIN", LarguraCm: 3.2),
            new("Orig.", LarguraCm: 1), new("Tipo do item", LarguraCm: 4), new("CFOP ent.", LarguraCm: 1.6), new("CFOP saí.", LarguraCm: 1.6), new("ICMS %", TipoColuna.Valor, 1.5),
            new("IPI %", TipoColuna.Valor, 1.5), new("PIS %", TipoColuna.Valor, 1.5), new("COFINS %", TipoColuna.Valor, 1.6), new("Ativo", LarguraCm: 1.1),
        ], rows, $"{produtos.Count} produto(s).");
    }

    public static TabelaRelatorio ItensNotas(IReadOnlyList<LinhaItemNota> itens)
    {
        var rows = itens.Select(i => L(false, i.DataLivro, i.Operacao.Rotulo(), i.Modelo.Codigo() == "NFSE" ? "NFS" : i.Modelo.Codigo(), i.Numero, i.Participante ?? "", i.CodigoProduto,
            i.Descricao ?? "", i.Ncm ?? "", i.Cfop, i.Unidade ?? "", i.Quantidade, i.ValorUnitario, i.ValorProduto, i.ValorContabil, i.Icms, i.IcmsSt, i.Ipi, i.Pis, i.Cofins, i.Iss)).ToList();
        rows.Add(L(true, null, "", "", "TOTAL", "", null, "", "", "", "", null, null, itens.Sum(i => i.ValorProduto), itens.Sum(i => i.ValorContabil), itens.Sum(i => i.Icms),
            itens.Sum(i => i.IcmsSt), itens.Sum(i => i.Ipi), itens.Sum(i => i.Pis), itens.Sum(i => i.Cofins), itens.Sum(i => i.Iss)));
        return new TabelaRelatorio("Itens das notas fiscais",
        [
            new("Data", TipoColuna.Data), new("Oper.", LarguraCm: 1.6), new("Mod.", LarguraCm: 1.1), new("Número", LarguraCm: 1.6), new("Participante"), new("Cód.", TipoColuna.Inteiro, 1.2),
            new("Produto/descrição"), new("NCM", LarguraCm: 2), new("CFOP", LarguraCm: 1.3), new("Un.", LarguraCm: 1), new("Qtde", TipoColuna.Valor), new("Unitário", TipoColuna.Valor),
            new("Valor produto", TipoColuna.Valor), new("Valor contábil", TipoColuna.Valor), new("ICMS", TipoColuna.Valor), new("ICMS-ST", TipoColuna.Valor), new("IPI", TipoColuna.Valor),
            new("PIS", TipoColuna.Valor), new("COFINS", TipoColuna.Valor), new("ISS", TipoColuna.Valor),
        ], rows, $"{itens.Count} item(ns) de notas autorizadas. Cancelados e denegados não entram.");
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

    /// <summary>Agrupamento dos resumos de vendas e de compras.</summary>
    public static IReadOnlyList<string> AgrupamentosResumo { get; } = ["Por nota", "Por cliente/fornecedor", "Por mês"];

    /// <summary>
    /// Resumo de vendas (notas de saída) ou de compras (notas de entrada) do período, por nota, por cliente/fornecedor ou
    /// por mês. Notas canceladas e denegadas ficam de fora.
    /// </summary>
    public static TabelaRelatorio ResumoNotas(IReadOnlyList<DocumentoFiscal> documentos, TipoOperacao operacao, int agrupamento)
    {
        var validas = documentos.Where(d => d.Operacao == operacao && d.Situacao == SituacaoDocumento.Autorizada).OrderBy(d => d.DataLivro).ThenBy(d => d.Numero).ToList();
        var quem = operacao == TipoOperacao.Saida ? "Cliente" : "Fornecedor";
        var titulo = operacao == TipoOperacao.Saida ? "Resumo de vendas" : "Resumo de compras";
        LinhaRelatorio Total(IEnumerable<DocumentoFiscal> ds) => L(true, "TOTAL", "", "", ds.Count(), ds.Sum(d => d.Totais.Total), ds.Sum(d => d.Totais.Icms),
            ds.Sum(d => d.Totais.Ipi), ds.Sum(d => d.Totais.Pis + d.Totais.Cofins), ds.Sum(d => d.Totais.Iss));
        ColunaRelatorio[] valores = [new("Notas", TipoColuna.Inteiro), new("Valor total", TipoColuna.Valor), new("ICMS", TipoColuna.Valor), new("IPI", TipoColuna.Valor),
                                     new("PIS+COFINS", TipoColuna.Valor), new("ISS", TipoColuna.Valor)];
        LinhaRelatorio Grupo(string a, string b, string c, IReadOnlyCollection<DocumentoFiscal> ds) => L(false, a, b, c, ds.Count, ds.Sum(d => d.Totais.Total),
            ds.Sum(d => d.Totais.Icms), ds.Sum(d => d.Totais.Ipi), ds.Sum(d => d.Totais.Pis + d.Totais.Cofins), ds.Sum(d => d.Totais.Iss));
        List<LinhaRelatorio> linhas;
        ColunaRelatorio[] chaves;
        switch (agrupamento)
        {
            case 1:
                chaves = [new(quem), new("CNPJ/CPF", LarguraCm: 3.4), new("", LarguraCm: 0.5)];
                linhas = validas.GroupBy(d => (Nome: d.NomeParticipante ?? "(sem identificação)", Doc: d.DocumentoParticipante ?? ""))
                                .OrderByDescending(g => g.Sum(d => d.Totais.Total))
                                .Select(g => Grupo(g.Key.Nome, Formatar(g.Key.Doc), "", g.ToList())).ToList();
                break;
            case 2:
                chaves = [new("Mês", LarguraCm: 2), new("", LarguraCm: 0.5), new("", LarguraCm: 0.5)];
                linhas = validas.GroupBy(d => new DateOnly(d.DataLivro.Year, d.DataLivro.Month, 1)).OrderBy(g => g.Key)
                                .Select(g => Grupo(g.Key.ToString("MM/yyyy"), "", "", g.ToList())).ToList();
                break;
            default:
                chaves = [new("Data", TipoColuna.Data), new("Nota", LarguraCm: 2.4), new(quem)];
                linhas = validas.Select(d => Grupo(d.DataLivro.ToString("dd/MM/yyyy"), $"{d.Modelo.Codigo()} {d.Numero}", d.NomeParticipante ?? "", [d])).ToList();
                break;
        }
        linhas.Add(Total(validas));
        return new TabelaRelatorio(titulo, [.. chaves, .. valores], linhas);
    }

    /// <summary>Recebimentos (ou pagamentos) do período: as baixas das duplicatas, com acréscimos, descontos e a conta movimentada.</summary>
    public static TabelaRelatorio Baixas(IReadOnlyList<(Duplicata Duplicata, BaixaDuplicata Baixa)> baixas, TipoDuplicata tipo)
    {
        var rows = baixas.OrderBy(b => b.Baixa.Data).ThenBy(b => b.Duplicata.ParticipanteNome)
            .Select(b => L(false, b.Baixa.Data, b.Duplicata.ParticipanteNome ?? "", $"{b.Duplicata.NumeroDocumento}/{b.Duplicata.Parcela}", b.Duplicata.Vencimento,
                           b.Baixa.Principal, b.Baixa.Juros + b.Baixa.Multa, b.Baixa.Desconto, b.Baixa.Liquido, b.Baixa.ContaDisponivel ?? "")).ToList();
        rows.Add(L(true, null, "TOTAL", $"{baixas.Count} baixa(s)", null, baixas.Sum(b => b.Baixa.Principal), baixas.Sum(b => b.Baixa.Juros + b.Baixa.Multa),
                   baixas.Sum(b => b.Baixa.Desconto), baixas.Sum(b => b.Baixa.Liquido), ""));
        var recebe = tipo == TipoDuplicata.Receber;
        return new TabelaRelatorio(recebe ? "Relatório de recebimentos" : "Relatório de pagamentos",
        [
            new("Data", TipoColuna.Data), new(recebe ? "Cliente" : "Fornecedor"), new("Documento/parcela", LarguraCm: 3), new("Vencimento", TipoColuna.Data),
            new("Principal", TipoColuna.Valor), new("Juros e multa", TipoColuna.Valor), new("Desconto", TipoColuna.Valor),
            new(recebe ? "Recebido" : "Pago", TipoColuna.Valor), new("Conta", LarguraCm: 2.6),
        ], rows);
    }

    private static string Formatar(string doc) => doc.Length switch
    {
        14 => Domain.Cadastros.Cnpj.Formatar(doc),
        11 => Domain.Cadastros.Cpf.Formatar(doc),
        _ => doc,
    };
}
