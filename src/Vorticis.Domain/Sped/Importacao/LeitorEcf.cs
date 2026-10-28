namespace Vorticis.Domain.Sped.Importacao;

public sealed record PeriodoLidoEcf(DateOnly Inicio, DateOnly Fim, string Codigo)
{
    /// <summary>Trimestre (1 a 4) dos períodos T01 a T04; nulo para o anual (A00) e os mensais por estimativa (A01 a A12).</summary>
    public int? Trimestre => Codigo.Length == 3 && Codigo[0] == 'T' && int.TryParse(Codigo[1..], out var t) && t is >= 1 and <= 4 ? t : null;
}

public sealed record LinhaParteALidaEcf(int Linha, string Registro, string Codigo, string Descricao, string? Tipo, decimal Valor, string? Historico, IReadOnlyList<string> Contas);
public sealed record LinhaTabelaLidaEcf(int Linha, string Registro, string Codigo, decimal Valor);
public sealed record SaldoParteBLidoEcf(int Linha, string Codigo, string Descricao, char Tributo, decimal Saldo);
public sealed record MovimentoParteBLidoEcf(int Linha, string Conta, char Tributo, decimal Valor, string Indicador, string Historico);

/// <summary>Um período (trimestre) da ECF com o que o bloco M, N ou P trouxe para ele.</summary>
public sealed class PeriodoEcfLido(PeriodoLidoEcf periodo)
{
    public PeriodoLidoEcf Periodo { get; } = periodo;
    public List<LinhaParteALidaEcf> M300 { get; } = [];
    public List<LinhaParteALidaEcf> M350 { get; } = [];
    public List<MovimentoParteBLidoEcf> M410 { get; } = [];
    public List<LinhaTabelaLidaEcf> Tabelas { get; } = [];

    public decimal Tab(string registro, string codigo) => Tabelas.FirstOrDefault(t => t.Registro == registro && t.Codigo == codigo)?.Valor ?? 0m;
    public bool Tem(string registro) => Tabelas.Any(t => t.Registro == registro);
}

public sealed class EcfLida
{
    public required CabecalhoSped Cabecalho { get; init; }
    /// <summary>0010.FORMA_TRIB: 1 Lucro Real, 5 Lucro Presumido, 2 Lucro Arbitrado, 8 Imune de IRPJ, 9 Isenta de IRPJ.</summary>
    public string FormaTributacao { get; init; } = "";
    public bool Real => FormaTributacao is "1" or "2" or "3" or "4";
    public bool Presumido => FormaTributacao is "5" or "6" or "7";
    public string? CodigoPlanoReferencial { get; init; }
    public IReadOnlyList<ContaLidaEcd> Contas { get; init; } = [];
    public IReadOnlyList<ReferenciaLidaEcd> Referencias { get; init; } = [];
    public IReadOnlyList<PeriodoEcfLido> Periodos { get; init; } = [];
    public IReadOnlyList<SaldoParteBLidoEcf> SaldosParteB { get; init; } = [];
    public IReadOnlyDictionary<string, int> NaoImportados { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<OcorrenciaImportacao> Ocorrencias { get; init; } = [];
}

/// <summary>Interpreta os registros da ECF (leiaute 12): 0000, 0010, 0030, C040, J050/J051, M010/M030/M300/M305/M310/M350/M355/M360/M410/M500, N030..N670 e P030..P500.</summary>
public static class LeitorEcf
{
    private static readonly HashSet<string> Conhecidos =
    [
        "0000", "0001", "0010", "0020", "0030", "0930", "0990", "C001", "C040", "C990", "E001", "E990", "J001", "J050", "J051", "J990", "K001", "K030", "K155", "K156", "K355", "K356", "K990",
        "L001", "L030", "L100", "L300", "L990", "M001", "M010", "M030", "M300", "M305", "M310", "M350", "M355", "M360", "M410", "M500", "M990", "N001", "N030", "N500", "N630", "N650", "N670",
        "N990", "P001", "P030", "P100", "P150", "P200", "P300", "P400", "P500", "P990", "Y001", "Y600", "Y672", "Y990", "9001", "9900", "9990", "9999",
    ];

    public static EcfLida Ler(ArquivoSped arq)
    {
        var z = arq.Zero!;
        var oc = new List<OcorrenciaImportacao>();
        var cab = new CabecalhoSped(TipoArquivoSped.Ecf, LeitorSped.Documento(z[4]), z[5], "", null, z.Data(10) ?? default, z.Data(11) ?? default, z[3]);
        if (cab.Inicio == default || cab.Fim == default || cab.Fim < cab.Inicio)
            oc.Add(OcorrenciaImportacao.Erro(z.Linha, "0000", "As datas do período (DT_INI e DT_FIN) do registro 0000 são inválidas."));
        if (cab.Cnpj.Length != 14) oc.Add(OcorrenciaImportacao.Erro(z.Linha, "0000", "CNPJ do registro 0000 inválido."));
        var forma = "";
        string? planRef = null;
        var contas = new List<ContaLidaEcd>();
        var refs = new List<ReferenciaLidaEcd>();
        var periodos = new List<PeriodoEcfLido>();
        var saldosB = new List<SaldoParteBLidoEcf>();
        var naoImp = new Dictionary<string, int>();
        PeriodoEcfLido? atual = null;
        LinhaParteALidaEcf? ultimaParteA = null;
        List<string>? contasDaUltima = null;
        string ultimoRegA = "";

        void FecharParteA()
        {
            if (ultimaParteA is null) return;
            var lista = ultimoRegA == "M300" ? atual!.M300 : atual!.M350;
            lista.Add(ultimaParteA with { Contas = contasDaUltima!.ToList() });
            ultimaParteA = null; contasDaUltima = null;
        }

        foreach (var r in arq.Registros)
        {
            if (r.Nome is not ("M305" or "M310" or "M355" or "M360") && ultimaParteA is not null) FecharParteA();
            switch (r.Nome)
            {
                case "0010": forma = r[4]; break;
                case "0030": cab = cab with { Uf = r[8] }; break;
                case "C040": planRef = r[16].Length > 0 ? r[16] : planRef; break;
                case "J050":
                {
                    var nivel = short.TryParse(r[5], out var n) ? n : (short)0;
                    if (r[6].Length == 0 || nivel <= 0 || r[3].Length == 0 || r[4] is not ("S" or "A"))
                        oc.Add(OcorrenciaImportacao.Erro(r.Linha, "J050", $"Conta {r[6]}: campos obrigatórios ausentes ou inválidos (natureza, tipo, nível ou código)."));
                    else contas.Add(new ContaLidaEcd(r.Linha, r[6], r[8], nivel, r[4] == "A", r[7].Length > 0 ? r[7] : null, r[3], r.Data(2) ?? cab.Inicio));
                    break;
                }
                case "J051":
                    if (contas.Count > 0) refs.Add(new ReferenciaLidaEcd(r.Linha, contas[^1].Codigo, r[2].Length > 0 ? r[2] : null, "", r[3]));
                    break;
                case "M010":
                    saldosB.Add(new SaldoParteBLidoEcf(r.Linha, r[2], r[3], r[7].Length > 0 ? r[7][0] : 'I', r[9] == "C" ? -r.Valor(8) : r.Valor(8)));
                    break;
                case "M030" or "N030" or "P030" or "L030":
                {
                    if (r.Data(2) is not { } ini || r.Data(3) is not { } fim)
                    {
                        oc.Add(OcorrenciaImportacao.Erro(r.Linha, r.Nome, "Período com datas inválidas."));
                        break;
                    }
                    var codigo = r[4];
                    atual = periodos.FirstOrDefault(p => p.Periodo.Inicio == ini && p.Periodo.Fim == fim);
                    if (atual is null) { atual = new PeriodoEcfLido(new PeriodoLidoEcf(ini, fim, codigo)); periodos.Add(atual); }
                    break;
                }
                case "M300" or "M350":
                    if (atual is null) break;
                    ultimaParteA = new LinhaParteALidaEcf(r.Linha, r.Nome, r[2], r[3], r[4].Length > 0 ? r[4] : null, r.ValorOuNulo(6) ?? 0m, r[7].Length > 0 ? r[7] : null, []);
                    contasDaUltima = [];
                    ultimoRegA = r.Nome;
                    break;
                case "M310" or "M360":
                    contasDaUltima?.Add(r[2]);
                    break;
                case "M410":
                    atual?.M410.Add(new MovimentoParteBLidoEcf(r.Linha, r[2], r[3].Length > 0 ? r[3][0] : 'I', r.Valor(4), r[5], r[7]));
                    break;
                case "N500" or "N630" or "N650" or "N670" or "P200" or "P300" or "P400" or "P500":
                    atual?.Tabelas.Add(new LinhaTabelaLidaEcf(r.Linha, r.Nome, r[2], r.Valor(4)));
                    break;
                default:
                    if (!Conhecidos.Contains(r.Nome)) naoImp[r.Nome] = naoImp.GetValueOrDefault(r.Nome) + 1;
                    break;
            }
        }
        FecharParteA();
        return new EcfLida
        {
            Cabecalho = cab, FormaTributacao = forma, CodigoPlanoReferencial = planRef, Contas = contas, Referencias = refs, Periodos = periodos,
            SaldosParteB = saldosB, NaoImportados = naoImp, Ocorrencias = oc,
        };
    }
}
