using System.Globalization;
using Vorticis.Domain.Contabil;
using Vorticis.Domain.Obrigacoes;
using Vorticis.Domain.Sped.Importacao;

namespace Vorticis.Application.Sped.Importacao;

public sealed record SituacaoEcf(SituacaoContabil Contabil, string? Regime, IReadOnlySet<(string Regime, int Ano, int Trimestre)> Apuracoes,
                                 IReadOnlySet<(int Ano, int Trimestre, string Tributo, bool Adicao, string Codigo, decimal Valor)> Lalur, IReadOnlySet<string> SaldosIniciais);

public sealed record ApuracaoEcfPlanejada(string Regime, int Ano, int Trimestre, DateOnly Inicio, DateOnly Fim, decimal ReceitaBruta, decimal? LucroContabil,
    decimal AdicoesIrpj, decimal ExclusoesIrpj, decimal AdicoesCsll, decimal ExclusoesCsll, decimal CompensacaoIrpj, decimal CompensacaoCsll, decimal PrejuizoIrpj, decimal PrejuizoCsll,
    decimal BaseIrpj, decimal BaseCsll, decimal IrpjNormal, decimal IrpjAdicional, decimal IrpjDeducoes, decimal IrpjAPagar, decimal CsllDevida, decimal CsllDeducoes,
    decimal CsllAPagar, DateOnly Vencimento, string Memoria, bool Existente);

public sealed record LalurEcfPlanejado(int Ano, int Trimestre, string Tributo, bool Adicao, string Descricao, decimal Valor, string Codigo, string? CodigoContaFinal, bool Existente);
public sealed record ParteBEcfPlanejada(string Tributo, string Tipo, int? Ano, int? Trimestre, decimal Valor, string Descricao, string? Regime);

public sealed class PlanoImportacaoEcf
{
    public required EcfLida Ecf { get; init; }
    /// <summary>Plano de contas a importar (J050/J051), quando a empresa não tem plano ou a opção de substituir está marcada.</summary>
    public PlanoImportacaoEcd? Plano { get; init; }
    public IReadOnlyList<ApuracaoEcfPlanejada> Apuracoes { get; init; } = [];
    public IReadOnlyList<LalurEcfPlanejado> Lalur { get; init; } = [];
    public IReadOnlyList<ParteBEcfPlanejada> ParteB { get; init; } = [];
    public IReadOnlyList<LinhaPrevia> Linhas { get; init; } = [];
    public IReadOnlyList<OcorrenciaImportacao> Ocorrencias { get; init; } = [];
}

/// <summary>Monta o plano de importação da ECF: Parte A (e-Lalur/e-Lacs), saldos e movimentos da Parte B, apurações do IRPJ/CSLL e, se faltar, o plano de contas (J050).</summary>
public static class PlanejadorEcf
{
    private static string Moeda(decimal v) => v.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
    private static string Pct(decimal v) => v.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));

    public static PlanoImportacaoEcf Planejar(EcfLida ecf, SituacaoEcf sit, OpcoesImportacaoSped op)
    {
        var oc = new List<OcorrenciaImportacao>(ecf.Ocorrencias);
        var cont = new ContadorImportacao();
        var comPlano = op.Modo == "TUDO";

        // ---- regime
        var regimeArquivo = ecf.Real ? "REAL" : ecf.Presumido ? "PRESUMIDO" : null;
        var regimeEmpresa = sit.Regime switch { "LUCRO_REAL" => "REAL", "LUCRO_PRESUMIDO" => "PRESUMIDO", _ => null };
        if (regimeArquivo is null)
            oc.Add(OcorrenciaImportacao.Aviso(0, "0010", $"A forma de tributação do arquivo ({ecf.FormaTributacao}) não é Lucro Real nem Lucro Presumido: só o plano de contas e a Parte B são considerados."));
        else if (regimeEmpresa is not null && regimeEmpresa != regimeArquivo)
            oc.Add(OcorrenciaImportacao.Aviso(0, "0010", $"O arquivo é de {(ecf.Real ? "Lucro Real" : "Lucro Presumido")} e a empresa está cadastrada em {(regimeEmpresa == "REAL" ? "Lucro Real" : "Lucro Presumido")}. " +
                                                           "Confira o regime em Cadastros › Empresas."));
        else if (regimeArquivo is not null && regimeEmpresa is null)
            oc.Add(OcorrenciaImportacao.Aviso(0, "0010", $"O regime tributário cadastrado da empresa é diferente do arquivo ({(ecf.Real ? "Lucro Real" : "Lucro Presumido")}). Confira em Cadastros › Empresas."));

        // ---- plano de contas (J050/J051)
        PlanoImportacaoEcd? plano = null;
        IReadOnlyDictionary<string, string> mapaFinal = new Dictionary<string, string>();
        if (comPlano && ecf.Contas.Count > 0)
        {
            var temPlano = sit.Contabil.Contas.Count > 0;
            var importa = !temPlano || op.SubstituirPlano;
            var ecdLida = new EcdLida
            {
                Cabecalho = ecf.Cabecalho with { Tipo = TipoArquivoSped.Ecd }, CodigoPlanoReferencial = ecf.CodigoPlanoReferencial, Contas = ecf.Contas,
                Referencias = ecf.Referencias,
            };
            if (importa)
            {
                plano = PlanejadorEcd.Planejar(ecdLida, sit.Contabil, op with { Modo = "PLANO" });
                mapaFinal = plano.CodigoFinalDe;
                oc.AddRange(plano.Ocorrencias.Where(o => !ecf.Ocorrencias.Contains(o)).Select(o => o with { Registro = o.Registro.Replace("I05", "J05") }));
            }
            else
            {
                var conv = ConversorPlanoSped.Converter(ecf.Contas, sit.Contabil.Mascara, op.ConverterMascara);
                mapaFinal = conv.Codigos;
                oc.Add(OcorrenciaImportacao.Info(0, "J050", "A empresa já tem plano de contas: o plano do arquivo não foi importado; as contas do e-Lalur foram ligadas às contas de mesmo código."));
            }
        }
        var linhasPlano = plano?.Linhas.Where(l => l.Registro is "I050" or "I051").Select(l => l with { Registro = l.Registro.Replace("I05", "J05") }).ToList() ?? [];
        var existentes = sit.Contabil.Contas.Select(c => c.Codigo).ToHashSet(StringComparer.Ordinal);
        string? ContaFinal(string? origem)
        {
            if (origem is null || !mapaFinal.TryGetValue(origem, out var fin)) return null;
            return plano is not null || existentes.Contains(fin) ? fin : null;
        }

        // ---- períodos trimestrais
        var apuracoes = new List<ApuracaoEcfPlanejada>();
        var lalur = new List<LalurEcfPlanejado>();
        var parteB = new List<ParteBEcfPlanejada>();
        if (regimeArquivo is not null)
            foreach (var per in ecf.Periodos)
            {
                var p = per.Periodo;
                if (p.Trimestre is not { } tri)
                {
                    oc.Add(OcorrenciaImportacao.Aviso(0, ecf.Real ? "N030" : "P030", $"O período {p.Inicio:dd/MM/yyyy} a {p.Fim:dd/MM/yyyy} ({p.Codigo}) não é trimestral e não foi importado (só os trimestres T01 a T04)."));
                    continue;
                }
                var ano = p.Fim.Year;
                var existe = sit.Apuracoes.Contains((regimeArquivo, ano, tri));
                decimal Soma(IEnumerable<LinhaParteALidaEcf> ls, string tipo) => ls.Where(l => l.Tipo == tipo && l.Codigo != "9").Sum(l => Math.Abs(l.Valor));
                decimal Comp(IEnumerable<LinhaParteALidaEcf> ls) => ls.Where(l => l.Tipo == "P" && l.Codigo == "173").Sum(l => Math.Abs(l.Valor));
                var mem = per.M300.Select(l => l.Descricao).Take(0).ToList();
                ApuracaoEcfPlanejada ap;
                if (ecf.Real)
                {
                    var prejI = per.M410.Where(m => m.Tributo == 'I').Sum(m => m.Valor);
                    var prejC = per.M410.Where(m => m.Tributo == 'C').Sum(m => m.Valor);
                    var l2 = per.M350.FirstOrDefault(l => l.Codigo == "2")?.Valor;
                    ap = new ApuracaoEcfPlanejada("REAL", ano, tri, p.Inicio, p.Fim, 0m, l2, Soma(per.M300, "A"), Soma(per.M300, "E"), Soma(per.M350, "A"), Soma(per.M350, "E"),
                        Comp(per.M300), Comp(per.M350), prejI, prejC, Math.Max(0m, per.Tab("N630", "1")), Math.Max(0m, per.Tab("N670", "1")), per.Tab("N630", "3"), per.Tab("N630", "4"), per.Tab("N630", "20"),
                        per.Tab("N630", "26"), per.Tab("N670", "4"), per.Tab("N670", "17"), per.Tab("N670", "21"), CalendarioDiasUteis.UltimoDiaUtil(p.Fim.AddMonths(1).Year, p.Fim.AddMonths(1).Month),
                        System.Text.Json.JsonSerializer.Serialize(new { origem = "SPED", arquivo = "ECF", periodo = p.Codigo, memoria = new[] { "Apuração importada de arquivo SPED (ECF)." } }), existe);
                    cont.Registrar("N630", "Apuração do IRPJ (referência)", novo: !existe, conflito: false, amostra: $"{tri}º trimestre de {ano}: IRPJ a pagar {Moeda(ap.IrpjAPagar)}");
                    cont.Registrar("N670", "Apuração da CSLL (referência)", novo: !existe, conflito: false, amostra: $"{tri}º trimestre de {ano}: CSLL a pagar {Moeda(ap.CsllAPagar)}");
                }
                else
                {
                    var receitas = new List<(string Cod, decimal PctIrpj, decimal PctCsll, decimal Valor)>();
                    foreach (var (cod, pi, pc) in new[] { ("2", 1.6m, 12m), ("4", 8m, 12m), ("6", 16m, 12m), ("8", 32m, 32m), ("9", 38.4m, 38.4m) })
                        if (per.Tab("P200", cod) is var v && v != 0) receitas.Add((cod, pi, pc, v));
                    var outras = per.Tab("P200", "20");
                    var memoria = receitas.Select(r => $"receita {Moeda(r.Valor)} × {Pct(r.PctIrpj)}% = base IRPJ {Moeda(Math.Round(r.Valor * r.PctIrpj / 100m, 2))} · × {Pct(r.PctCsll)}% = base CSLL {Moeda(Math.Round(r.Valor * r.PctCsll / 100m, 2))}").ToList();
                    if (outras != 0) memoria.Add($"Outras receitas (ganhos de capital e demais receitas): {Moeda(outras)}");
                    ap = new ApuracaoEcfPlanejada("PRESUMIDO", ano, tri, p.Inicio, p.Fim, receitas.Sum(r => r.Valor), null, 0, 0, 0, 0, 0, 0, 0, 0, per.Tab("P300", "1"), per.Tab("P500", "1"),
                        per.Tab("P300", "3"), per.Tab("P300", "4"), per.Tab("P300", "10"), per.Tab("P300", "15"), per.Tab("P500", "4"), per.Tab("P500", "11"), per.Tab("P500", "13"),
                        CalendarioDiasUteis.UltimoDiaUtil(p.Fim.AddMonths(1).Year, p.Fim.AddMonths(1).Month),
                        System.Text.Json.JsonSerializer.Serialize(new { origem = "SPED", arquivo = "ECF", periodo = p.Codigo, memoria }), existe);
                    cont.Registrar("P300", "Apuração do IRPJ (referência)", novo: !existe, conflito: false, amostra: $"{tri}º trimestre de {ano}: IRPJ a pagar {Moeda(ap.IrpjAPagar)}");
                    cont.Registrar("P500", "Apuração da CSLL (referência)", novo: !existe, conflito: false, amostra: $"{tri}º trimestre de {ano}: CSLL a pagar {Moeda(ap.CsllAPagar)}");
                }
                apuracoes.Add(ap);
                if (existe) oc.Add(OcorrenciaImportacao.Aviso(0, ecf.Real ? "N630" : "P300", $"A apuração do {tri}º trimestre de {ano} já existe no sistema e foi mantida (a Parte B desse trimestre também)."));

                // ---- Parte A (e-Lalur / e-Lacs): adições e exclusões; códigos iguais nos dois viram "ambos os tributos"
                if (ecf.Real)
                {
                    var itens350 = per.M350.Where(l => l.Tipo is "A" or "E").ToList();
                    foreach (var l in per.M300.Where(l => l.Tipo is "A" or "E" && l.Codigo != "9"))
                    {
                        var par = itens350.FirstOrDefault(x => x.Codigo == l.Codigo && x.Tipo == l.Tipo && Math.Abs(x.Valor) == Math.Abs(l.Valor));
                        if (par is not null) itens350.Remove(par);
                        var adicao = l.Tipo == "A";
                        var valor = Math.Abs(l.Valor);
                        if (valor == 0) continue;
                        var tributo = par is not null ? "AMBOS" : "IRPJ";
                        var jaTem = sit.Lalur.Contains((ano, tri, tributo, adicao, l.Codigo, valor));
                        var desc = Texto(l.Historico ?? l.Descricao, 200);
                        lalur.Add(new LalurEcfPlanejado(ano, tri, tributo, adicao, desc, valor, l.Codigo, ContaFinal(l.Contas.Count == 1 ? l.Contas[0] : null), jaTem || existe));
                        cont.Registrar("M300", "Lançamentos do e-Lalur / e-Lacs (Parte A)", novo: !(jaTem || existe), conflito: false, amostra: $"{l.Codigo} {Moeda(valor)}");
                    }
                    foreach (var l in itens350.Where(l => l.Codigo != "9"))
                    {
                        var adicao = l.Tipo == "A";
                        var valor = Math.Abs(l.Valor);
                        if (valor == 0) continue;
                        var jaTem = sit.Lalur.Contains((ano, tri, "CSLL", adicao, l.Codigo, valor));
                        lalur.Add(new LalurEcfPlanejado(ano, tri, "CSLL", adicao, Texto(l.Historico ?? l.Descricao, 200), valor, l.Codigo, ContaFinal(l.Contas.Count == 1 ? l.Contas[0] : null), jaTem || existe));
                        cont.Registrar("M350", "Lançamentos do e-Lalur / e-Lacs (Parte A)", novo: !(jaTem || existe), conflito: false, amostra: $"{l.Codigo} {Moeda(valor)}");
                    }
                    // ---- Parte B: prejuízo gerado e compensações do período
                    if (!existe)
                    {
                        if (ap.PrejuizoIrpj > 0) parteB.Add(new ParteBEcfPlanejada("IRPJ", "PREJUIZO", ano, tri, ap.PrejuizoIrpj, "Prejuízo fiscal apurado no período (importado do SPED)", "REAL"));
                        if (ap.PrejuizoCsll > 0) parteB.Add(new ParteBEcfPlanejada("CSLL", "PREJUIZO", ano, tri, ap.PrejuizoCsll, "Base de cálculo negativa da CSLL apurada no período (importada do SPED)", "REAL"));
                        if (ap.CompensacaoIrpj > 0) parteB.Add(new ParteBEcfPlanejada("IRPJ", "COMPENSACAO", ano, tri, -ap.CompensacaoIrpj, "Compensação de prejuízo fiscal (importada do SPED)", "REAL"));
                        if (ap.CompensacaoCsll > 0) parteB.Add(new ParteBEcfPlanejada("CSLL", "COMPENSACAO", ano, tri, -ap.CompensacaoCsll, "Compensação de base negativa da CSLL (importada do SPED)", "REAL"));
                        foreach (var m in parteB.Where(m => m.Ano == ano && m.Trimestre == tri))
                            cont.Registrar("M410", "Movimentos da Parte B (prejuízos e compensações)", novo: true, conflito: false, amostra: $"{m.Tributo} {m.Tipo.ToLowerInvariant()} {Moeda(m.Valor)}");
                    }
                }
            }
        // ---- saldos iniciais da Parte B (M010)
        if (regimeArquivo == "REAL")
            foreach (var s in ecf.SaldosParteB)
            {
                var tributo = s.Tributo == 'C' ? "CSLL" : "IRPJ";
                if (s.Saldo <= 0)
                {
                    if (s.Saldo < 0) oc.Add(OcorrenciaImportacao.Aviso(s.Linha, "M010", $"A conta {s.Codigo} tem saldo credor; só saldos devedores (prejuízo a compensar) são importados."));
                    continue;
                }
                var existe = sit.SaldosIniciais.Contains(tributo);
                cont.Registrar("M010", "Saldos da Parte B (prejuízo fiscal e base negativa)", novo: !existe, conflito: false, amostra: $"{tributo}: {Moeda(s.Saldo)}");
                if (existe) oc.Add(OcorrenciaImportacao.Aviso(s.Linha, "M010", $"O saldo inicial da Parte B de {tributo} já existe no sistema e foi mantido."));
                else parteB.Add(new ParteBEcfPlanejada(tributo, "SALDO_INICIAL", null, null, s.Saldo, "Saldo inicial (importado do SPED)", null));
            }
        foreach (var (reg, qtd) in ecf.NaoImportados.OrderBy(x => x.Key))
            oc.Add(OcorrenciaImportacao.Info(0, reg, $"O registro {reg} ({qtd} linha(s)) não é importado por esta versão do sistema."));
        oc.Add(OcorrenciaImportacao.Info(0, "K155", "Os saldos contábeis (K155/K355), o balanço e a DRE referenciais (L100/L300 ou P100/P150) e os sócios (Y600) não são importados: eles são recalculados a partir da contabilidade."));

        var linhas = linhasPlano.Concat(cont.Linhas()).ToList();
        return new PlanoImportacaoEcf { Ecf = ecf, Plano = plano, Apuracoes = apuracoes, Lalur = lalur, ParteB = parteB, Linhas = linhas, Ocorrencias = oc };
    }

    private static string Texto(string? s, int max) => string.IsNullOrWhiteSpace(s) ? "Lançamento importado do SPED" : s.Length > max ? s[..max] : s;
}
