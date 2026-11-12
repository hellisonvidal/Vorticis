using Vorticis.Domain.Comum;
using Vorticis.Domain.Contabil;
using Vorticis.Fiscal.Documentos;

namespace Vorticis.Fiscal.Contabilizacao;

/// <summary>Regra: para o CFOP (e, opcionalmente, sentido e participante), debita e credita as contas indicadas (por código).</summary>
public sealed record RegraContabilizacao(
    long Id, int Prioridade, TipoOperacao? Operacao, string? Cfop, long? ParticipanteId, string? ParticipanteNome, string ContaDebito,
    string ContaCredito, string Historico, bool Ativo = true)
{
    public const string HistoricoPadrao = "NF {DOCUMENTO} - {PARTICIPANTE}";
}

/// <summary>Resultado da montagem do lançamento de uma nota: a proposta, ou os motivos de não ser possível.</summary>
public sealed record PropostaContabilizacao(DocumentoFiscal Documento, LancamentoProposto? Lancamento, IReadOnlyList<string> Erros)
{
    public bool Pronta => Lancamento is not null && Erros.Count == 0;
}

/// <summary>
/// Contas da reforma tributária (códigos), da aba Provisões de impostos: IBS e CBS a recuperar (crédito das entradas, ativo) e a recolher (débito das
/// saídas, passivo) e o Imposto Seletivo (despesa e a recolher). Só se aplicam nos anos em que IBS/CBS são contabilizados (2027 em diante).
/// </summary>
public sealed record ContasReforma(string? IbsRecuperar, string? IbsRecolher, string? CbsRecuperar, string? CbsRecolher, string? IsDespesa, string? IsRecolher);

/// <summary>Escolha da regra e montagem do lançamento de uma nota. Regras puras, testáveis sem banco.</summary>
public static class MotorContabilizacao
{
    /// <summary>
    /// Regra mais específica que casa com a operação: participante (vence qualquer regra sem participante), depois CFOP completo, depois prefixo
    /// (o mais longo primeiro) e, por fim, a regra com sentido sobre a sem sentido. Empate: a que vem antes na lista (menor prioridade, depois menor id).
    /// Regras inativas são ignoradas.
    /// </summary>
    public static RegraContabilizacao? Escolher(IEnumerable<RegraContabilizacao> regras, TipoOperacao operacao, string cfop, long? participanteId)
    {
        static int Pontos(RegraContabilizacao r, TipoOperacao op, string cfop, long? part)
        {
            if (!r.Ativo) return -1;
            var pontos = 0;
            if (r.Operacao is { } o) { if (o != op) return -1; pontos += 1; }
            if (!string.IsNullOrWhiteSpace(r.Cfop))
            {
                if (!Cfop.Casa(r.Cfop.Trim(), cfop)) return -1;
                pontos += r.Cfop.Trim().Length >= cfop.Length ? 40 : 10 * Math.Min(r.Cfop.Trim().Length, 3);
            }
            if (r.ParticipanteId is { } p) { if (p != part) return -1; pontos += 100; }
            return pontos;
        }

        return regras.Select(r => (Regra: r, Pontos: Pontos(r, operacao, cfop, participanteId)))
                     .Where(x => x.Pontos >= 0)
                     .OrderByDescending(x => x.Pontos).ThenBy(x => x.Regra.Prioridade).ThenBy(x => x.Regra.Id)
                     .Select(x => x.Regra).FirstOrDefault();
    }

    /// <summary>
    /// Um lançamento por nota, na data do livro: para cada CFOP, uma partida a débito e outra a crédito com o valor contábil dos itens
    /// (produto - desconto + frete + seguro + outras + IPI + ICMS-ST). Documento = número da nota; histórico pelo modelo da regra.
    /// Com <paramref name="usaCentroCusto"/>, as partidas são separadas por centro de custo (o do item, ou o do documento na falta dele) e o centro
    /// vai na partida de débito nas entradas (despesa/custo) e na de crédito nas saídas (receita).
    /// Com <paramref name="contaParticipante"/> (conta de fornecedores na entrada, de clientes na saída — nota com duplicatas), ela é a contrapartida da nota
    /// (crédito na entrada, débito na saída), exceto quando a nota ou uma regra específica do participante já define a conta.
    /// </summary>
    /// <para>
    /// Com <paramref name="reforma"/> (ano em que IBS/CBS são "por fora" e contabilizados), os tributos da reforma entram em partidas próprias:
    /// na saída, D na conta de débito da nota (cliente) e C IBS/CBS a recolher, mais o Imposto Seletivo (D despesa, C a recolher); na entrada, só dos
    /// itens que creditam, D IBS/CBS a recuperar e C na conta de crédito da nota (fornecedor).
    /// </para>
    public static PropostaContabilizacao Montar(long empresaId, DocumentoFiscal doc, IReadOnlyList<ItemFiscal> itens, IReadOnlyList<RegraContabilizacao> regras,
                                                bool usaCentroCusto = false, string? contaParticipante = null, ContasReforma? reforma = null)
    {
        var erros = new List<string>();
        if (doc.Situacao != SituacaoDocumento.Autorizada)
            return new(doc, null, [$"Documento {doc.Numero} {doc.Situacao.Rotulo().ToLowerInvariant()}: não é contabilizado."]);
        if (itens.Count == 0) return new(doc, null, [$"Documento {doc.Numero} sem itens."]);

        var partidas = new List<PartidaProposta>();
        string? Centro(ItemFiscal i) => usaCentroCusto ? i.CentroCustoCodigo ?? doc.CentroCustoCodigo : null;
        foreach (var grupo in itens.GroupBy(i => (i.Cfop, Centro: Centro(i))).OrderBy(g => g.Key.Cfop, StringComparer.Ordinal).ThenBy(g => g.Key.Centro ?? "", StringComparer.Ordinal))
        {
            var valor = Dinheiro.Arredondar(grupo.Sum(i => i.ValorContabil));
            if (valor <= 0) continue;
            var regra = Escolher(regras, doc.Operacao, grupo.Key.Cfop, doc.ParticipanteId);
            // Contas escolhidas na própria nota (escrituração) sobrepõem as da regra; com as duas informadas, a regra nem é exigida.
            if (!string.IsNullOrWhiteSpace(doc.ContaDebito) || !string.IsNullOrWhiteSpace(doc.ContaCredito))
            {
                var deb = string.IsNullOrWhiteSpace(doc.ContaDebito) ? regra?.ContaDebito : doc.ContaDebito.Trim();
                var cre = string.IsNullOrWhiteSpace(doc.ContaCredito) ? regra?.ContaCredito : doc.ContaCredito.Trim();
                if (deb is not null && cre is not null)
                    regra = (regra ?? new RegraContabilizacao(0, 0, null, null, null, null, deb, cre, "NF {DOCUMENTO} - {PARTICIPANTE}")) with { ContaDebito = deb, ContaCredito = cre };
            }
            if (regra is null)
            {
                erros.Add($"Documento {doc.Numero}: nenhuma regra de contabilização para o CFOP {grupo.Key.Cfop} ({doc.Operacao.Rotulo().ToLowerInvariant()}).");
                continue;
            }
            if (!string.IsNullOrWhiteSpace(contaParticipante) && regra.ParticipanteId is null)
            {
                if (doc.Operacao == TipoOperacao.Entrada && string.IsNullOrWhiteSpace(doc.ContaCredito)) regra = regra with { ContaCredito = contaParticipante.Trim() };
                else if (doc.Operacao == TipoOperacao.Saida && string.IsNullOrWhiteSpace(doc.ContaDebito)) regra = regra with { ContaDebito = contaParticipante.Trim() };
            }
            var historico = VariaveisHistorico.Aplicar(regra.Historico, new ContextoHistorico(
                Documento: doc.Numero, Participante: doc.NomeParticipante, Data: doc.DataLivro, Valor: valor)).Texto;
            var centro = grupo.Key.Centro;
            var entrada = doc.Operacao == TipoOperacao.Entrada;
            partidas.Add(new PartidaProposta(regra.ContaDebito, IndicadorDC.D, valor, historico, entrada ? centro : null, doc.ParticipanteCodigo, doc.Numero));
            partidas.Add(new PartidaProposta(regra.ContaCredito, IndicadorDC.C, valor, historico, entrada ? null : centro, doc.ParticipanteCodigo, doc.Numero));
        }
        if (erros.Count == 0 && partidas.Count == 0) erros.Add($"Documento {doc.Numero} sem valor a contabilizar.");
        if (erros.Count == 0 && reforma is not null) PartidasReforma(doc, itens, reforma, partidas, erros);
        if (erros.Count > 0) return new(doc, null, erros);

        var origem = doc.Modelo == ModeloDocumento.NFSe ? OrigemLancamento.NFSe : OrigemLancamento.NFe;
        return new(doc, new LancamentoProposto(empresaId, doc.DataLivro, origem, partidas, DocumentoOrigemId: doc.Id), []);
    }

    private static void PartidasReforma(DocumentoFiscal doc, IReadOnlyList<ItemFiscal> itens, ContasReforma c, List<PartidaProposta> partidas, List<string> erros)
    {
        var entrada = doc.Operacao == TipoOperacao.Entrada;
        var lista = itens.Where(i => i.Reforma is not null && (!entrada || i.CreditaIbsCbs)).ToList();
        var ibs = Dinheiro.Arredondar(lista.Sum(i => i.Reforma!.Ibs));
        var cbs = Dinheiro.Arredondar(lista.Sum(i => i.Reforma!.Cbs));
        var seletivo = entrada ? 0 : Dinheiro.Arredondar(itens.Sum(i => i.Reforma?.ImpostoSeletivo ?? 0));
        if (ibs <= 0 && cbs <= 0 && seletivo <= 0) return;
        string? Exigir(string? conta, string nome)
        {
            if (string.IsNullOrWhiteSpace(conta)) erros.Add($"Documento {doc.Numero}: configure a conta de {nome} em Cadastros › Regras de contabilização, aba Provisões de impostos.");
            return conta?.Trim();
        }
        var historico = $"NF {doc.Numero} - {doc.NomeParticipante}";
        // contrapartida: a mesma conta do participante usada pela nota (débito na saída, crédito na entrada)
        var principal = partidas.FirstOrDefault(p => p.DC == (entrada ? IndicadorDC.C : IndicadorDC.D))?.CodigoConta;
        if (principal is null) return;
        if (ibs + cbs > 0)
        {
            var (contaIbs, contaCbs) = entrada
                ? (Exigir(c.IbsRecuperar, "IBS a recuperar"), Exigir(c.CbsRecuperar, "CBS a recuperar"))
                : (Exigir(c.IbsRecolher, "IBS a recolher"), Exigir(c.CbsRecolher, "CBS a recolher"));
            if (erros.Count > 0) return;
            var dc = entrada ? IndicadorDC.D : IndicadorDC.C;
            if (ibs > 0) partidas.Add(new PartidaProposta(contaIbs!, dc, ibs, $"{historico} · IBS", null, doc.ParticipanteCodigo, doc.Numero));
            if (cbs > 0) partidas.Add(new PartidaProposta(contaCbs!, dc, cbs, $"{historico} · CBS", null, doc.ParticipanteCodigo, doc.Numero));
            partidas.Add(new PartidaProposta(principal, entrada ? IndicadorDC.C : IndicadorDC.D, ibs + cbs, $"{historico} · IBS/CBS", null, doc.ParticipanteCodigo, doc.Numero));
        }
        if (seletivo > 0)
        {
            var (desp, rec) = (Exigir(c.IsDespesa, "despesa do Imposto Seletivo"), Exigir(c.IsRecolher, "Imposto Seletivo a recolher"));
            if (erros.Count > 0) return;
            partidas.Add(new PartidaProposta(desp!, IndicadorDC.D, seletivo, $"{historico} · Imposto Seletivo", null, doc.ParticipanteCodigo, doc.Numero));
            partidas.Add(new PartidaProposta(rec!, IndicadorDC.C, seletivo, $"{historico} · Imposto Seletivo", null, doc.ParticipanteCodigo, doc.Numero));
        }
    }

}
