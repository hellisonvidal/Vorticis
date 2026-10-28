using System.Text.RegularExpressions;
using Vorticis.Domain.Contabil;
using Vorticis.Domain.Sped.Importacao;

namespace Vorticis.Application.Sped.Importacao;

public sealed record ContaExistente(long Id, string Codigo, string Descricao, bool Analitica, string CodNat, bool TemMovimento);
public sealed record ParticipanteExistente(long Id, string Codigo, string? Documento);
public sealed record ExercicioExistente(int Ano, DateOnly Inicio, DateOnly Fim, bool Encerrado);

/// <summary>O que a empresa já tem, para a importação não duplicar nada.</summary>
public sealed record SituacaoContabil(
    string Cnpj, string Nome, string? Mascara, DateOnly InicioAtividades,
    IReadOnlyList<ContaExistente> Contas, IReadOnlySet<string> Centros, IReadOnlySet<string> Historicos,
    IReadOnlyList<ParticipanteExistente> Participantes, bool TemLancamentos, DateOnly? PrimeiroLancamento, bool TemSaldoInicial,
    IReadOnlyList<ExercicioExistente> Exercicios, IReadOnlySet<DateOnly> PeriodosFechados, bool PossuiContador);

public sealed record ContaPlanejada(ContaLidaEcd Origem, string Codigo, string? CodigoPai, short Nivel, bool Analitica, IndicadorDC Natureza, DateOnly DataInclusao,
                                    bool Existente, bool Valida);
public sealed record CentroPlanejado(string Codigo, string Descricao, DateOnly DataInclusao);
public sealed record HistoricoPlanejado(string Codigo, string Descricao);
public sealed record ReferenciaPlanejada(string CodigoConta, string CodigoReferencial);
public sealed record ParticipantePlanejado(string CodigoSped, string Nome, string? Documento, string? Uf, string? CodMunicipio, long? ExistenteId);
public sealed record LancamentoPlanejado(int Linha, string Numero, LancamentoProposto Proposta, IReadOnlyList<string> Erros)
{
    public bool Valido => Erros.Count == 0;
}
public sealed record SaldoInicialPlanejado(DateOnly Data, IReadOnlyList<PartidaProposta> Partidas);

/// <summary>Tudo o que a importação da ECD vai fazer, já resolvido (códigos finais das contas, o que é novo e o que já existe).</summary>
public sealed class PlanoImportacaoEcd
{
    public required EcdLida Ecd { get; init; }
    public bool SubstituirPlano { get; init; }
    public bool ImportaPlano { get; init; }
    public bool ImportaSaldos { get; init; }
    public bool ImportaLancamentos { get; init; }
    public IReadOnlyList<ContaPlanejada> Contas { get; init; } = [];
    public IReadOnlyDictionary<string, string> CodigoFinalDe { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<CentroPlanejado> CentrosNovos { get; init; } = [];
    public IReadOnlyList<HistoricoPlanejado> HistoricosNovos { get; init; } = [];
    public IReadOnlyList<ReferenciaPlanejada> Referencias { get; init; } = [];
    public IReadOnlyList<ParticipantePlanejado> Participantes { get; init; } = [];
    public SaldoInicialPlanejado? SaldoInicial { get; init; }
    public IReadOnlyList<LancamentoPlanejado> Lancamentos { get; init; } = [];
    public IReadOnlyList<SignatarioLidoEcd> SignatariosNovos { get; init; } = [];
    public IReadOnlyList<int> ExerciciosACriar { get; init; } = [];
    public IReadOnlyList<LinhaPrevia> Linhas { get; init; } = [];
    public IReadOnlyList<OcorrenciaImportacao> Ocorrencias { get; init; } = [];
    public string? Estrategia { get; init; }
    public bool TemErroPontual => Ocorrencias.Any(o => o.Gravidade == GravidadeOcorrencia.Erro && o.Pontual);
    public bool TemErroGeral => Ocorrencias.Any(o => o.Gravidade == GravidadeOcorrencia.Erro && !o.Pontual);
}

/// <summary>Conversão do plano de contas do arquivo para os códigos do sistema (números separados por ponto, hierarquia pelo código).</summary>
public static partial class ConversorPlanoSped
{
    [GeneratedRegex(@"^\d+(\.\d+)*$")] private static partial Regex Pontilhado();
    [GeneratedRegex(@"^\d+$")] private static partial Regex SoDigitos();

    public sealed record Resultado(IReadOnlyDictionary<string, string> Codigos, string Estrategia, IReadOnlyList<OcorrenciaImportacao> Ocorrencias);

    /// <summary>
    /// Escolhe como fica o código de cada conta: mantém quando já é "1.01.01" com a hierarquia no código; divide "1010101" pela largura de cada nível;
    /// senão renumera pela hierarquia (COD_CTA_SUP). Depois converte para a máscara da empresa, se couber.
    /// </summary>
    public static Resultado Converter(IReadOnlyList<ContaLidaEcd> contas, string? mascaraEmpresa, bool converter)
    {
        var oc = new List<OcorrenciaImportacao>();
        var porCodigo = contas.ToDictionary(c => c.Codigo, StringComparer.Ordinal);
        string? Sup(ContaLidaEcd c) => c.CodigoSuperior;
        string Estrategia;
        Dictionary<string, string> cod;

        var pontilhados = contas.All(c => Pontilhado().IsMatch(c.Codigo) && c.Codigo.Length <= 30);
        bool HierarquiaNoCodigo() => contas.All(c => Sup(c) is null
            ? !c.Codigo.Contains('.') : c.Codigo.Contains('.') && c.Codigo[..c.Codigo.LastIndexOf('.')] == Sup(c));
        if (pontilhados && HierarquiaNoCodigo())
        {
            cod = contas.ToDictionary(c => c.Codigo, c => c.Codigo, StringComparer.Ordinal);
            Estrategia = "mantidos";
        }
        else if (DividirPorLargura(contas, porCodigo) is { } dividido)
        {
            cod = dividido;
            Estrategia = "divididos";
            oc.Add(OcorrenciaImportacao.Aviso(0, "I050", "Os códigos das contas do arquivo não têm pontos (ex.: " + contas[0].Codigo + "); foram separados em níveis pelo tamanho de cada nível."));
        }
        else
        {
            cod = Renumerar(contas, mascaraEmpresa);
            Estrategia = "renumerados";
            oc.Add(OcorrenciaImportacao.Aviso(0, "I050", "Os códigos das contas do arquivo não seguem o padrão do sistema (números separados por ponto) e foram " +
                                                          "renumerados pela hierarquia do plano. Veja a correspondência (código do arquivo → código novo) nas informações abaixo."));
            foreach (var c in contas.Take(40)) oc.Add(OcorrenciaImportacao.Info(c.Linha, "I050", $"{c.Codigo} → {cod[c.Codigo]}  {c.Descricao}"));
            if (contas.Count > 40) oc.Add(OcorrenciaImportacao.Info(0, "I050", $"... e mais {contas.Count - 40} contas renumeradas."));
        }

        if (converter && !string.IsNullOrWhiteSpace(mascaraEmpresa) && MascaraPlano.Ler(mascaraEmpresa) is { Sucesso: true } m)
        {
            var conv = new Dictionary<string, string>(StringComparer.Ordinal);
            string? falha = null;
            foreach (var (orig, atual) in cod)
            {
                var r = m.Valor.Converter(atual);
                if (!r.Sucesso) { falha = r.Erros[0]; break; }
                conv[orig] = r.Valor;
            }
            if (falha is null && conv.Values.Distinct(StringComparer.Ordinal).Count() != conv.Count) falha = "dois códigos ficariam iguais";
            if (falha is null)
            {
                var mudou = conv.Count(x => x.Value != cod[x.Key]);
                if (mudou > 0)
                    oc.Add(OcorrenciaImportacao.Aviso(0, "I050", $"{mudou} código(s) de conta convertido(s) para a máscara da empresa ({m.Valor.Texto}); o resto já estava nela."));
                cod = conv;
            }
            else
                oc.Add(OcorrenciaImportacao.Aviso(0, "I050", $"Os códigos não cabem na máscara da empresa ({m.Valor.Texto}): {falha}. Foram mantidos como estão no arquivo; " +
                                                              "ajuste a máscara em Configurações › Contabilidade depois da importação, se quiser."));
        }
        return new Resultado(cod, Estrategia, oc);
    }

    private static Dictionary<string, string>? DividirPorLargura(IReadOnlyList<ContaLidaEcd> contas, Dictionary<string, ContaLidaEcd> porCodigo)
    {
        if (!contas.All(c => SoDigitos().IsMatch(c.Codigo))) return null;
        var larguras = new Dictionary<int, int>();   // nível (profundidade) → tamanho do trecho
        var profundidade = new Dictionary<string, int>();
        int Prof(ContaLidaEcd c) => profundidade.TryGetValue(c.Codigo, out var p) ? p
            : profundidade[c.Codigo] = c.CodigoSuperior is { } s && porCodigo.TryGetValue(s, out var pai) ? Prof(pai) + 1 : 1;
        foreach (var c in contas)
        {
            var d = Prof(c);
            var pai = c.CodigoSuperior is { } s && porCodigo.TryGetValue(s, out var pp) ? pp : null;
            if (pai is not null && !c.Codigo.StartsWith(pai.Codigo, StringComparison.Ordinal)) return null;
            var w = c.Codigo.Length - (pai?.Codigo.Length ?? 0);
            if (w <= 0) return null;
            if (larguras.TryGetValue(d, out var antes) && antes != w) return null;
            larguras[d] = w;
        }
        var res = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var c in contas)
        {
            var d = profundidade[c.Codigo];
            var partes = new List<string>();
            var pos = 0;
            for (var n = 1; n <= d; n++) { partes.Add(c.Codigo.Substring(pos, larguras[n])); pos += larguras[n]; }
            res[c.Codigo] = string.Join('.', partes);
        }
        return res.Values.Distinct().Count() == res.Count && res.Values.All(v => v.Length <= 30) ? res : null;
    }

    private static Dictionary<string, string> Renumerar(IReadOnlyList<ContaLidaEcd> contas, string? mascaraEmpresa)
    {
        var larguraMascara = MascaraPlano.Ler(mascaraEmpresa) is { Sucesso: true } m ? m.Valor.Segmentos : [];
        var filhos = contas.Where(c => c.CodigoSuperior is not null).GroupBy(c => c.CodigoSuperior!).ToDictionary(g => g.Key, g => g.ToList());
        var raizes = contas.Where(c => c.CodigoSuperior is null || contas.All(o => o.Codigo != c.CodigoSuperior)).ToList();
        var res = new Dictionary<string, string>(StringComparer.Ordinal);
        void Descer(List<ContaLidaEcd> irmaos, string prefixo, int nivel)
        {
            var w = Math.Max(nivel - 1 < larguraMascara.Count ? larguraMascara[nivel - 1] : 2, irmaos.Count.ToString().Length);
            if (nivel == 1) w = Math.Max(1, irmaos.Count.ToString().Length) > 1 ? irmaos.Count.ToString().Length : (larguraMascara.Count > 0 ? larguraMascara[0] : 1);
            for (var i = 0; i < irmaos.Count; i++)
            {
                var c = irmaos[i];
                if (res.ContainsKey(c.Codigo)) continue;
                var seg = (i + 1).ToString().PadLeft(w, '0');
                var codigo = prefixo.Length == 0 ? seg : prefixo + "." + seg;
                res[c.Codigo] = codigo;
                if (filhos.TryGetValue(c.Codigo, out var f)) Descer(f, codigo, nivel + 1);
            }
        }
        Descer(raizes, "", 1);
        return res;
    }
}

/// <summary>Monta o plano de importação da ECD: o que é novo, o que já existe, os conflitos e os erros de cada lançamento (sem gravar nada).</summary>
public static class PlanejadorEcd
{
    private const string NotaHistorico = "Lançamento importado do SPED";

    public static PlanoImportacaoEcd Planejar(EcdLida ecd, SituacaoContabil sit, OpcoesImportacaoSped op)
    {
        var oc = new List<OcorrenciaImportacao>(ecd.Ocorrencias);
        var cont = new ContadorImportacao();
        var importaPlano = true;
        var importaSaldos = op.Modo is "PLANO_SALDOS" or "TUDO";
        var importaLanc = op.Modo == "TUDO";
        var substituir = op.SubstituirPlano;
        if (substituir && sit.TemLancamentos)
        {
            oc.Add(OcorrenciaImportacao.Erro(0, "I050", "Não é possível substituir o plano: a empresa já tem lançamentos. Desmarque \"Substituir o plano atual\" para somar as contas do arquivo ao plano existente."));
            substituir = false;
        }

        // ------------------------------------------------------------ plano de contas
        var contasValidas = ecd.Contas;
        var duplicadas = ecd.Contas.GroupBy(c => c.Codigo, StringComparer.Ordinal).Where(g => g.Count() > 1).ToList();
        foreach (var g in duplicadas) oc.Add(OcorrenciaImportacao.Erro(g.Skip(1).First().Linha, "I050", $"A conta {g.Key} aparece mais de uma vez no arquivo."));
        contasValidas = ecd.Contas.GroupBy(c => c.Codigo, StringComparer.Ordinal).Select(g => g.First()).ToList();
        var codigosArquivo = contasValidas.Select(c => c.Codigo).ToHashSet(StringComparer.Ordinal);
        foreach (var c in contasValidas.Where(c => c.CodigoSuperior is not null && !codigosArquivo.Contains(c.CodigoSuperior)))
            oc.Add(OcorrenciaImportacao.Erro(c.Linha, "I050", $"A conta {c.Codigo} aponta para a conta superior {c.CodigoSuperior}, que não existe no arquivo."));
        if (oc.Any(o => o.Gravidade == GravidadeOcorrencia.Erro && o.Registro == "I050"))
            return Vazio(ecd, oc, op, substituir);
        foreach (var c in contasValidas.Where(c => !new[] { "01", "02", "03", "04", "05", "09" }.Contains(c.CodNat)))
            oc.Add(OcorrenciaImportacao.Erro(c.Linha, "I050", $"A conta {c.Codigo} tem natureza (COD_NAT) \"{c.CodNat}\" inválida."));
        if (oc.Any(o => o.Gravidade == GravidadeOcorrencia.Erro && o.Registro == "I050"))
            return Vazio(ecd, oc, op, substituir);

        var conv = ConversorPlanoSped.Converter(contasValidas, sit.Mascara, op.ConverterMascara);
        oc.AddRange(conv.Ocorrencias);
        var cod = conv.Codigos;
        var existentes = substituir ? new Dictionary<string, ContaExistente>() : sit.Contas.ToDictionary(c => c.Codigo, StringComparer.Ordinal);

        // natureza do saldo: pelo COD_NAT; contas de resultado e outras, pelo movimento (mais créditos que débitos = credora)
        var movimento = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var l in ecd.Lancamentos.Where(l => l.Indicador != 'E'))
            foreach (var p in l.Partidas)
                movimento[p.Conta] = movimento.GetValueOrDefault(p.Conta) + (p.DC == IndicadorDC.D ? p.Valor : -p.Valor);
        IndicadorDC Natureza(ContaLidaEcd c) => c.CodNat switch
        {
            "01" => IndicadorDC.D,
            "02" or "03" => IndicadorDC.C,
            _ => movimento.TryGetValue(c.Codigo, out var m) && m != 0 ? (m < 0 ? IndicadorDC.C : IndicadorDC.D)
                 : Regex.IsMatch(c.Descricao, "receita|venda|faturamento|rendimento|servi", RegexOptions.IgnoreCase) ? IndicadorDC.C : IndicadorDC.D,
        };
        // conta sintética de resultado: a natureza da maioria das analíticas abaixo dela
        IndicadorDC NaturezaSintetica(ContaLidaEcd c)
        {
            var abaixo = contasValidas.Where(x => x.Analitica && x.Codigo != c.Codigo && DescendeDe(x, c, contasValidas)).Select(Natureza).ToList();
            return abaixo.Count == 0 ? Natureza(c) : abaixo.Count(n => n == IndicadorDC.C) > abaixo.Count / 2.0 ? IndicadorDC.C : IndicadorDC.D;
        }

        var contasPlan = new List<ContaPlanejada>();
        var depth = new Dictionary<string, short>(StringComparer.Ordinal);
        short Nivel(ContaLidaEcd c) => depth.TryGetValue(c.Codigo, out var d) ? d
            : depth[c.Codigo] = (short)(c.CodigoSuperior is { } s ? Nivel(contasValidas.First(x => x.Codigo == s)) + 1 : 1);
        var porOrigem = contasValidas.ToDictionary(c => c.Codigo, StringComparer.Ordinal);
        var invalidas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in contasValidas)
        {
            var final = cod[c.Codigo];
            var nivel = Nivel(c);
            var pai = c.CodigoSuperior is null ? null : cod[c.CodigoSuperior];
            var analitica = c.Analitica;
            if (nivel == 1 && analitica) { analitica = false; oc.Add(OcorrenciaImportacao.Aviso(c.Linha, "I050", $"A conta {c.Codigo} está no nível 1 e foi cadastrada como sintética.")); }
            var data = c.DataInclusao < ecd.Cabecalho.Inicio ? c.DataInclusao : ecd.Cabecalho.Inicio;
            var existe = existentes.TryGetValue(final, out var ex);
            var valida = true;
            if (existe)
            {
                if (ex!.Analitica != analitica || ex.CodNat != c.CodNat)
                {
                    oc.Add(OcorrenciaImportacao.Aviso(c.Linha, "I050", $"Conflito: a conta {final} já existe na empresa como {(ex.Analitica ? "analítica" : "sintética")} (natureza {ex.CodNat}) " +
                                                                    $"e no arquivo como {(analitica ? "analítica" : "sintética")} (natureza {c.CodNat}). Foi mantida a da empresa."));
                    cont.Registrar("I050", "Plano de contas", novo: false, conflito: true, amostra: final);
                    if (ex.Analitica != analitica) valida = false;
                }
                else cont.Registrar("I050", "Plano de contas", novo: false, conflito: false);
            }
            else
            {
                if (pai is not null && existentes.TryGetValue(pai, out var exPai) && exPai.Analitica)
                {
                    oc.Add(OcorrenciaImportacao.Erro(c.Linha, "I050", $"A conta {final} ficaria abaixo da conta analítica {pai}, que já existe na empresa. Use \"Substituir o plano atual\" ou ajuste o plano."));
                    valida = false;
                    cont.Registrar("I050", "Plano de contas", novo: false, conflito: true, amostra: final);
                }
                else cont.Registrar("I050", "Plano de contas", novo: true, conflito: false, amostra: $"{final} {c.Descricao}");
            }
            contasPlan.Add(new ContaPlanejada(c, final, pai, nivel, analitica, analitica || c.CodNat is "01" or "02" or "03" ? Natureza(c) : NaturezaSintetica(c), data, existe, valida));
            if (!valida) invalidas.Add(c.Codigo);
        }
        var mapaFinal = contasPlan.ToDictionary(c => c.Origem.Codigo, c => c.Codigo, StringComparer.Ordinal);
        var contaPorFinal = contasPlan.ToDictionary(c => c.Codigo, StringComparer.Ordinal);
        if (substituir && sit.Contas.Count > 0)
            oc.Add(OcorrenciaImportacao.Info(0, "I050", $"O plano atual da empresa ({sit.Contas.Count} contas) será substituído pelo do arquivo; \"Desfazer importação\" devolve o plano anterior."));

        // ------------------------------------------------------------ vínculo referencial, aglutinação, históricos, centros, participantes
        var refs = new List<ReferenciaPlanejada>();
        foreach (var r in ecd.Referencias)
        {
            if (!mapaFinal.TryGetValue(r.CodigoConta, out var fin)) continue;
            refs.Add(new ReferenciaPlanejada(fin, r.CodigoReferencial));
            cont.Registrar("I051", "Vínculo ao plano referencial", novo: true, conflito: false, amostra: $"{fin} → {r.CodigoReferencial}");
        }
        foreach (var a in ecd.Aglutinacoes) cont.Registrar("I052", "Aglutinação (calculada na geração da ECD; não é guardada)", novo: false, conflito: false);

        var histNovos = new List<HistoricoPlanejado>();
        var histVistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var h in ecd.Historicos)
        {
            if (h.Codigo.Length is 0 or > 20 || !histVistos.Add(h.Codigo)) { cont.Registrar("I075", "Históricos padronizados", novo: false, conflito: false); continue; }
            var novo = !sit.Historicos.Contains(h.Codigo);
            cont.Registrar("I075", "Históricos padronizados", novo, conflito: false, amostra: h.Descricao);
            if (novo) histNovos.Add(new HistoricoPlanejado(h.Codigo, h.Descricao.Length > 250 ? h.Descricao[..250] : h.Descricao));
        }
        var centrosNovos = new List<CentroPlanejado>();
        var centrosVistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in ecd.Centros)
        {
            if (!centrosVistos.Add(c.Codigo)) continue;
            var novo = !sit.Centros.Contains(c.Codigo);
            cont.Registrar("I100", "Centros de custo", novo, conflito: false, amostra: $"{c.Codigo} {c.Descricao}");
            if (novo) centrosNovos.Add(new CentroPlanejado(c.Codigo.Length > 30 ? c.Codigo[..30] : c.Codigo, c.Descricao.Length > 150 ? c.Descricao[..150] : c.Descricao, c.DataInclusao));
        }
        var partsPlan = new List<ParticipantePlanejado>();
        if (importaLanc)
            foreach (var p in ecd.Participantes)
            {
                var doc = p.Documento is null ? null : LeitorSped.Documento(p.Documento);
                var ex = doc is { Length: > 0 } ? sit.Participantes.FirstOrDefault(x => x.Documento == doc) : null;
                cont.Registrar("0150", "Participantes", novo: ex is null, conflito: false, amostra: p.Nome);
                partsPlan.Add(new ParticipantePlanejado(p.Codigo, p.Nome, doc, p.Uf, p.CodMunicipio, ex?.Id));
            }

        // ------------------------------------------------------------ saldo inicial (I155 do primeiro período)
        SaldoInicialPlanejado? saldoInicial = null;
        if (importaSaldos && ecd.Saldos.Count > 0)
        {
            var primeiro = ecd.Saldos.Min(s => s.Inicio);
            var patrimoniais = ecd.Saldos.Where(s => s.Inicio == primeiro && s.Inicial != 0 && porOrigem.TryGetValue(s.Conta, out var c) && c.CodNat is "01" or "02" or "03")
                                         .GroupBy(s => s.Conta).Select(g => (Conta: g.Key, Saldo: g.Sum(x => x.Inicial))).Where(x => x.Saldo != 0).ToList();
            if (patrimoniais.Count > 0)
            {
                var deb = patrimoniais.Where(x => x.Saldo > 0).Sum(x => x.Saldo);
                var cred = -patrimoniais.Where(x => x.Saldo < 0).Sum(x => x.Saldo);
                if (sit.TemSaldoInicial)
                    oc.Add(OcorrenciaImportacao.Aviso(0, "I155", "A empresa já tem saldos iniciais (implantação) lançados; os saldos do arquivo não foram importados."));
                else if (sit.PrimeiroLancamento is { } pl && pl < primeiro)
                    oc.Add(OcorrenciaImportacao.Aviso(0, "I155", $"A empresa já tem lançamentos anteriores ({pl:dd/MM/yyyy}); os saldos iniciais do arquivo não foram importados para não duplicar."));
                else if (deb != cred)
                    oc.Add(OcorrenciaImportacao.Aviso(0, "I155", $"Os saldos iniciais do arquivo não fecham (débitos {deb:N2} x créditos {cred:N2}); não foram importados."));
                else if (patrimoniais.Any(x => invalidas.Contains(x.Conta)))
                    oc.Add(OcorrenciaImportacao.Aviso(0, "I155", "Há saldos em contas com conflito; os saldos iniciais não foram importados."));
                else
                {
                    var partidas = patrimoniais.Select(x => new PartidaProposta(mapaFinal[x.Conta], x.Saldo > 0 ? IndicadorDC.D : IndicadorDC.C, Math.Abs(x.Saldo),
                                                                              $"Saldo inicial em {primeiro:dd/MM/yyyy}")).ToList();
                    saldoInicial = new SaldoInicialPlanejado(primeiro, partidas);
                    foreach (var x in patrimoniais) cont.Registrar("I155", "Saldos iniciais (implantação)", novo: true, conflito: false, amostra: $"{mapaFinal[x.Conta]}: {x.Saldo:N2}");
                }
            }
        }

        // ------------------------------------------------------------ lançamentos
        var lancs = new List<LancamentoPlanejado>();
        var exerciciosACriar = new SortedSet<int>();
        if (importaLanc)
        {
            var ctxContas = contasPlan.Where(c => c.Valida).ToDictionary(c => c.Origem.Codigo, c => new ContaInfo(0, c.Codigo, c.Origem.Descricao, c.Analitica,
                                                                                                        true, DateOnly.MinValue, false, false, c.Origem.CodNat), StringComparer.Ordinal);
            var ctxCentros = ecd.Centros.Select(c => c.Codigo).Concat(sit.Centros).Distinct().ToDictionary(c => c, _ => 0L, StringComparer.Ordinal);
            var ctxParts = ecd.Participantes.Select(p => p.Codigo).Distinct().ToDictionary(c => c, _ => 0L, StringComparer.Ordinal);
            var histDesc = ecd.Historicos.GroupBy(h => h.Codigo).ToDictionary(g => g.Key, g => g.First().Descricao);
            var ordenados = ecd.Lancamentos.OrderBy(l => long.TryParse(l.Numero, out var n) ? n : long.MaxValue).ThenBy(l => l.Linha).ToList();
            foreach (var l in ordenados)
            {
                var ex = ExercicioDe(sit, l.Data);
                if (ex is null && ExercicioNovo(sit, l.Data) is not null) exerciciosACriar.Add(l.Data.Year);
                var erros = new List<string>();
                var partidas = new List<PartidaProposta>();
                var partidasFinais = new List<PartidaProposta>();
                foreach (var p in l.Partidas)
                {
                    if (!mapaFinal.TryGetValue(p.Conta, out var fin)) { erros.Add($"a conta {p.Conta} não existe no plano do arquivo"); continue; }
                    if (invalidas.Contains(p.Conta)) { erros.Add($"a conta {p.Conta} tem conflito com o plano da empresa"); continue; }
                    var hist = !string.IsNullOrWhiteSpace(p.Historico) ? p.Historico
                        : p.CodigoHistorico is { } ch && histDesc.TryGetValue(ch, out var hd) ? hd : NotaHistorico;
                    if (hist.Length > 500) hist = hist[..500];
                    var pp = new PartidaProposta(p.Conta, p.DC, p.Valor, hist, p.Centro, p.Participante, p.NumeroArquivo is { Length: > 100 } na ? na[..100] : p.NumeroArquivo);
                    partidas.Add(pp);
                    partidasFinais.Add(pp with { CodigoConta = fin, CodigoParticipante = p.Participante is { } pa ? "SPED:" + pa : null });
                }
                var ind = l.Indicador switch { 'E' => IndicadorLancamento.Encerramento, 'X' => IndicadorLancamento.Extemporaneo, _ => IndicadorLancamento.Normal };
                if (ind == IndicadorLancamento.Extemporaneo && l.DataExtemporanea is null) erros.Add("lançamento extemporâneo sem a data do fato (DT_LCTO_EXT)");
                if (ind != IndicadorLancamento.Extemporaneo && l.DataExtemporanea is not null) ind = IndicadorLancamento.Extemporaneo;
                // valida pelas mesmas regras da gravação, com as contas do arquivo
                if (erros.Count == 0)
                {
                    var proposta = new LancamentoProposto(0, l.Data, OrigemLancamento.Manual, partidas, ind, l.DataExtemporanea);
                    var periodo = ex is null
                        ? ExercicioNovo(sit, l.Data)
                        : new PeriodoInfo(0, ex.Inicio, ex.Fim, ex.Encerrado, true, !sit.PeriodosFechados.Contains(new DateOnly(l.Data.Year, l.Data.Month, 1)));
                    var v = Domain.Contabil.ValidadorLancamento.Validar(proposta, new ContextoValidacao(ctxContas, ctxCentros, ctxParts, periodo));
                    if (!v.Sucesso) erros.AddRange(v.Erros);
                }
                if (erros.Count > 0) foreach (var e in erros.Take(3)) oc.Add(OcorrenciaImportacao.ErroPontual(l.Linha, "I200", $"Lançamento {l.Numero}: {e}."));
                cont.Registrar("I200", "Lançamentos", novo: erros.Count == 0, conflito: erros.Count > 0, amostra: $"{l.Numero} em {l.Data:dd/MM/yyyy} - {l.Valor:N2}");
                cont.Registrar("I250", "Partidas dos lançamentos", novo: erros.Count == 0, conflito: erros.Count > 0);
                lancs.Add(new LancamentoPlanejado(l.Linha, l.Numero, new LancamentoProposto(0, l.Data, OrigemLancamento.Manual, partidasFinais, ind, l.DataExtemporanea), erros));
            }
            // conferência dos saldos mensais (I155) com o movimento dos lançamentos
            ConferirSaldos(ecd, lancs, ordenados, mapaFinal, oc);
        }

        // ------------------------------------------------------------ signatários
        var sigs = new List<SignatarioLidoEcd>();
        if (op.CadastrarSignatarios)
            foreach (var s in ecd.Signatarios)
            {
                if (s.Contador && s.Documento.Length == 11 && !string.IsNullOrWhiteSpace(s.Crc) && s.UfCrc is { Length: 2 })
                {
                    sigs.Add(s);
                    cont.Registrar("J930", "Signatários (contador)", novo: true, conflito: false, amostra: s.Nome);
                }
                else cont.Registrar("J930", "Signatários (cadastre os demais em Cadastros › Sócios e contador)", novo: false, conflito: false);
            }
        else foreach (var s in ecd.Signatarios) cont.Registrar("J930", "Signatários (não importados)", novo: false, conflito: false);

        return new PlanoImportacaoEcd
        {
            Ecd = ecd, SubstituirPlano = substituir, ImportaPlano = importaPlano, ImportaSaldos = importaSaldos, ImportaLancamentos = importaLanc,
            Contas = contasPlan, CodigoFinalDe = mapaFinal, CentrosNovos = centrosNovos, HistoricosNovos = histNovos, Referencias = refs, Participantes = partsPlan,
            SaldoInicial = saldoInicial, Lancamentos = lancs, SignatariosNovos = sigs, ExerciciosACriar = exerciciosACriar.ToList(),
            Linhas = cont.Linhas(), Ocorrencias = oc, Estrategia = conv.Estrategia,
        };
    }

    private static PlanoImportacaoEcd Vazio(EcdLida ecd, List<OcorrenciaImportacao> oc, OpcoesImportacaoSped op, bool substituir) =>
        new() { Ecd = ecd, SubstituirPlano = substituir, Ocorrencias = oc };

    private static bool DescendeDe(ContaLidaEcd x, ContaLidaEcd ancestral, IReadOnlyList<ContaLidaEcd> todas)
    {
        var atual = x;
        for (var i = 0; i < 20 && atual.CodigoSuperior is { } s; i++)
        {
            if (s == ancestral.Codigo) return true;
            atual = todas.FirstOrDefault(t => t.Codigo == s)!;
            if (atual is null) return false;
        }
        return false;
    }

    private static ExercicioExistente? ExercicioDe(SituacaoContabil sit, DateOnly d) => sit.Exercicios.FirstOrDefault(e => d >= e.Inicio && d <= e.Fim);

    /// <summary>Exercício que será criado para o ano (ano civil, começando no início das atividades quando cai no próprio ano).</summary>
    private static PeriodoInfo? ExercicioNovo(SituacaoContabil sit, DateOnly d)
    {
        var inicio = new DateOnly(d.Year, 1, 1);
        if (sit.InicioAtividades > inicio) inicio = sit.InicioAtividades;
        var fim = new DateOnly(d.Year, 12, 31);
        if (d < inicio || d > fim) return null;
        return new PeriodoInfo(0, inicio, fim, false, true, true);
    }

    private static void ConferirSaldos(EcdLida ecd, List<LancamentoPlanejado> lancs, List<LancamentoLidoEcd> ordenados, IReadOnlyDictionary<string, string> mapaFinal,
                                       List<OcorrenciaImportacao> oc)
    {
        if (ecd.Saldos.Count == 0) return;
        var mov = new Dictionary<(string Conta, int Ano, int Mes), (decimal D, decimal C)>();
        for (var i = 0; i < ordenados.Count; i++)
        {
            if (!lancs[i].Valido) continue;
            foreach (var p in ordenados[i].Partidas)
            {
                var k = (p.Conta, ordenados[i].Data.Year, ordenados[i].Data.Month);
                var a = mov.GetValueOrDefault(k);
                mov[k] = p.DC == IndicadorDC.D ? (a.D + p.Valor, a.C) : (a.D, a.C + p.Valor);
            }
        }
        var diverg = 0;
        foreach (var g in ecd.Saldos.GroupBy(s => (s.Conta, s.Inicio.Year, s.Inicio.Month)))
        {
            var d = g.Sum(x => x.Debitos);
            var c = g.Sum(x => x.Creditos);
            var m = mov.GetValueOrDefault(g.Key);
            if (m.D == d && m.C == c) continue;
            if (++diverg <= 10)
                oc.Add(OcorrenciaImportacao.Aviso(g.First().Linha, "I155", $"Conta {g.Key.Conta} em {g.Key.Month:00}/{g.Key.Year}: o saldo do arquivo tem débitos {d:N2} e créditos {c:N2}, " +
                                                                           $"mas os lançamentos importados somam {m.D:N2} e {m.C:N2}."));
        }
        if (diverg > 10) oc.Add(OcorrenciaImportacao.Aviso(0, "I155", $"... e mais {diverg - 10} divergência(s) entre os saldos mensais do arquivo e os lançamentos."));
        if (diverg == 0) oc.Add(OcorrenciaImportacao.Info(0, "I155", "Conferência: os débitos e créditos mensais dos saldos (I155) batem com os lançamentos importados."));
    }
}
