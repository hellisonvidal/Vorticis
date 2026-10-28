using Vorticis.Domain.Contabil;

namespace Vorticis.Domain.Sped.Importacao;

public sealed record ContaLidaEcd(int Linha, string Codigo, string Descricao, short Nivel, bool Analitica, string? CodigoSuperior, string CodNat, DateOnly DataInclusao);
public sealed record ReferenciaLidaEcd(int Linha, string CodigoConta, string? CentroCusto, string CodigoEntidade, string CodigoReferencial);
public sealed record AglutinacaoLidaEcd(int Linha, string CodigoConta, string? CentroCusto, string Codigo);
public sealed record HistoricoLidoEcd(int Linha, string Codigo, string Descricao);
public sealed record CentroLidoEcd(int Linha, string Codigo, string Descricao, DateOnly DataInclusao);
public sealed record ParticipanteLidoEcd(int Linha, string Codigo, string Nome, string? Documento, string? Uf, string? CodMunicipio, string CodPais);
public sealed record SaldoLidoEcd(int Linha, DateOnly Inicio, DateOnly Fim, string Conta, string? Centro, decimal Inicial, decimal Debitos, decimal Creditos, decimal Final);
public sealed record PartidaLidaEcd(int Linha, string Conta, string? Centro, decimal Valor, IndicadorDC DC, string? NumeroArquivo, string? CodigoHistorico, string Historico, string? Participante);
public sealed record LancamentoLidoEcd(int Linha, string Numero, DateOnly Data, decimal Valor, char Indicador, DateOnly? DataExtemporanea, IReadOnlyList<PartidaLidaEcd> Partidas)
{
    public decimal TotalDebitos => Partidas.Where(p => p.DC == IndicadorDC.D).Sum(p => p.Valor);
    public decimal TotalCreditos => Partidas.Where(p => p.DC == IndicadorDC.C).Sum(p => p.Valor);
}
public sealed record SignatarioLidoEcd(int Linha, string Nome, string Documento, string Qualificacao, string CodigoQualificacao, string? Crc, string? Email, string? Telefone,
                                       string? UfCrc, bool ResponsavelLegal)
{
    public bool Contador => CodigoQualificacao == "900";
}

/// <summary>Conteúdo de uma ECD já separado por assunto (saldos com sinal: débito positivo).</summary>
public sealed class EcdLida
{
    public required CabecalhoSped Cabecalho { get; init; }
    public string? CodigoPlanoReferencial { get; init; }
    public IReadOnlyList<ContaLidaEcd> Contas { get; init; } = [];
    public IReadOnlyList<ReferenciaLidaEcd> Referencias { get; init; } = [];
    public IReadOnlyList<AglutinacaoLidaEcd> Aglutinacoes { get; init; } = [];
    public IReadOnlyList<HistoricoLidoEcd> Historicos { get; init; } = [];
    public IReadOnlyList<CentroLidoEcd> Centros { get; init; } = [];
    public IReadOnlyList<ParticipanteLidoEcd> Participantes { get; init; } = [];
    public IReadOnlyList<SaldoLidoEcd> Saldos { get; init; } = [];
    public IReadOnlyList<LancamentoLidoEcd> Lancamentos { get; init; } = [];
    public IReadOnlyList<SignatarioLidoEcd> Signatarios { get; init; } = [];
    public IReadOnlyList<OcorrenciaImportacao> Ocorrencias { get; init; } = [];
}

/// <summary>Interpreta os registros da ECD (leiaute 9): 0000, 0150, I050, I051, I052, I075, I100, I150/I155, I200/I250 e J930.</summary>
public static class LeitorEcd
{
    public static EcdLida Ler(ArquivoSped arq)
    {
        var oc = new List<OcorrenciaImportacao>();
        var z = arq.Zero!;
        var cab = new CabecalhoSped(TipoArquivoSped.Ecd, LeitorSped.Documento(z[6]), z[5], z[7], z[8].Length > 0 ? z[8] : null,
                                    z.Data(3) ?? default, z.Data(4) ?? default, "9");
        var contas = new List<ContaLidaEcd>();
        var refs = new List<ReferenciaLidaEcd>();
        var aglut = new List<AglutinacaoLidaEcd>();
        var hist = new List<HistoricoLidoEcd>();
        var centros = new List<CentroLidoEcd>();
        var parts = new List<ParticipanteLidoEcd>();
        var saldos = new List<SaldoLidoEcd>();
        var lctos = new List<LancamentoLidoEcd>();
        var sig = new List<SignatarioLidoEcd>();

        if (cab.Inicio == default || cab.Fim == default || cab.Fim < cab.Inicio)
            oc.Add(OcorrenciaImportacao.Erro(z.Linha, "0000", "As datas do período (DT_INI e DT_FIN) do registro 0000 são inválidas."));
        if (cab.Cnpj.Length != 14) oc.Add(OcorrenciaImportacao.Erro(z.Linha, "0000", "CNPJ do registro 0000 inválido."));

        (DateOnly Ini, DateOnly Fim)? periodo = null;
        RegistroSped? i200 = null;
        List<PartidaLidaEcd>? partidas = null;
        void FecharLancamento()
        {
            if (i200 is null) return;
            var d = i200.Data(3);
            if (d is null) oc.Add(OcorrenciaImportacao.ErroPontual(i200.Linha, "I200", $"Lançamento {i200[2]}: data inválida."));
            else
                lctos.Add(new LancamentoLidoEcd(i200.Linha, i200[2], d.Value, i200.Valor(4), i200[5].Length > 0 ? i200[5][0] : 'N', i200.Data(6), partidas!));
            i200 = null; partidas = null;
        }

        foreach (var r in arq.Registros)
        {
            switch (r.Nome)
            {
                case "0150":
                    parts.Add(new ParticipanteLidoEcd(r.Linha, r[2], r[3], r[5].Length > 0 ? r[5] : r[6].Length > 0 ? r[6] : null, r[8].Length > 0 ? r[8] : null,
                                                       r[11].Length > 0 ? r[11] : null, r[4].Length > 0 ? r[4] : "01058"));
                    break;
                case "I050":
                {
                    var nivel = short.TryParse(r[5], out var n) ? n : (short)0;
                    if (r[6].Length == 0 || nivel <= 0 || r[3].Length == 0 || r[4] is not ("S" or "A"))
                        oc.Add(OcorrenciaImportacao.Erro(r.Linha, "I050", $"Conta {r[6]}: campos obrigatórios ausentes ou inválidos (natureza, tipo, nível ou código)."));
                    else
                        contas.Add(new ContaLidaEcd(r.Linha, r[6], r[8], nivel, r[4] == "A", r[7].Length > 0 ? r[7] : null, r[3], r.Data(2) ?? cab.Inicio));
                    break;
                }
                case "I051":
                    if (contas.Count > 0) refs.Add(new ReferenciaLidaEcd(r.Linha, contas[^1].Codigo, r[2].Length > 0 ? r[2] : null, "", r[3]));
                    break;
                case "I052":
                    if (contas.Count > 0) aglut.Add(new AglutinacaoLidaEcd(r.Linha, contas[^1].Codigo, r[2].Length > 0 ? r[2] : null, r[3]));
                    break;
                case "I075":
                    hist.Add(new HistoricoLidoEcd(r.Linha, r[2], r[3]));
                    break;
                case "I100":
                    centros.Add(new CentroLidoEcd(r.Linha, r[3], r[4], r.Data(2) ?? cab.Inicio));
                    break;
                case "I150":
                    periodo = r.Data(2) is { } ini && r.Data(3) is { } fim ? (ini, fim) : null;
                    if (periodo is null) oc.Add(OcorrenciaImportacao.Erro(r.Linha, "I150", "Período do saldo (I150) com datas inválidas."));
                    break;
                case "I155":
                    if (periodo is { } p)
                        saldos.Add(new SaldoLidoEcd(r.Linha, p.Ini, p.Fim, r[2], r[3].Length > 0 ? r[3] : null,
                                                    r[5] == "C" ? -r.Valor(4) : r.Valor(4), r.Valor(6), r.Valor(7), r[9] == "C" ? -r.Valor(8) : r.Valor(8)));
                    break;
                case "I200":
                    FecharLancamento();
                    i200 = r; partidas = [];
                    break;
                case "I250":
                    if (i200 is null) { oc.Add(OcorrenciaImportacao.ErroPontual(r.Linha, "I250", "Partida (I250) sem o lançamento (I200) antes.")); break; }
                    if (r[5] is not ("D" or "C")) { oc.Add(OcorrenciaImportacao.ErroPontual(r.Linha, "I250", $"Lançamento {i200[2]}: indicador D/C inválido.")); }
                    partidas!.Add(new PartidaLidaEcd(r.Linha, r[2], r[3].Length > 0 ? r[3] : null, r.Valor(4), r[5] == "C" ? IndicadorDC.C : IndicadorDC.D,
                                                      r[6].Length > 0 ? r[6] : null, r[7].Length > 0 ? r[7] : null, r[8], r[9].Length > 0 ? r[9] : null));
                    break;
                case "I350" or "I355" or "J001":
                    FecharLancamento();
                    break;
                case "J930":
                    FecharLancamento();
                    sig.Add(new SignatarioLidoEcd(r.Linha, r[2], LeitorSped.Documento(r[3]), r[4], r[5], r[6].Length > 0 ? r[6] : null, r[7].Length > 0 ? r[7] : null,
                                                   r[8].Length > 0 ? r[8] : null, r[9].Length > 0 ? r[9] : null, r[12] == "S"));
                    break;
            }
            if (r.Nome is "I990" or "J990") FecharLancamento();
        }
        FecharLancamento();

        if (contas.Count == 0) oc.Add(OcorrenciaImportacao.Erro(z.Linha, "I050", "O arquivo não tem plano de contas (registros I050)."));
        return new EcdLida
        {
            Cabecalho = cab, CodigoPlanoReferencial = z[23].Length > 0 ? z[23] : null,
            Contas = contas, Referencias = refs, Aglutinacoes = aglut, Historicos = hist, Centros = centros, Participantes = parts, Saldos = saldos,
            Lancamentos = lctos, Signatarios = sig, Ocorrencias = oc,
        };
    }
}
