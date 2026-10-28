using Vorticis.Domain.Sped.Importacao;

namespace Vorticis.Fiscal.Sped.Importacao;

public sealed record ParticipanteLidoEfd(int Linha, string Codigo, string Nome, string CodPais, string? Cnpj, string? Cpf, string? Ie, string? CodMunicipio)
{
    public string? Documento => Cnpj ?? Cpf;
    public bool Estrangeiro => CodPais != "01058" && CodPais.Length > 0;
}
public sealed record ProdutoLidoEfd(int Linha, string Codigo, string Descricao, string? Gtin, string Unidade, string TipoItem, string? Ncm, decimal AliqIcms, string? Cest);

public sealed record ItemLidoEfd(int Linha, int Numero, string CodItem, string? Descricao, decimal Quantidade, string Unidade, decimal ValorItem, decimal Desconto,
                                 string? CstIcms, string Cfop, decimal BcIcms, decimal AliqIcms, decimal Icms, decimal BcSt, decimal IcmsSt,
                                 string? CstIpi, decimal BcIpi, decimal AliqIpi, decimal Ipi, string? CstPis, decimal BcPis, decimal AliqPis, decimal Pis,
                                 string? CstCofins, decimal BcCofins, decimal AliqCofins, decimal Cofins);

public sealed record AnaliticoLidoEfd(int Linha, string Cst, string Cfop, decimal Aliquota, decimal Operacao, decimal BcIcms, decimal Icms, decimal BcSt, decimal IcmsSt, decimal Ipi);

/// <summary>Nota lida do arquivo: C100 (NF-e/NFC-e), A100 (NFS-e) ou D100 (CT-e), com itens (C170/A170) e analítico (C190/D190).</summary>
public sealed class DocumentoLidoEfd
{
    public required int Linha { get; init; }
    public required string Registro { get; init; }           // C100, A100 ou D100
    public required bool Entrada { get; init; }
    public required bool Propria { get; init; }              // emissão própria (IND_EMIT = 0)
    public string? CodPart { get; init; }
    public required string Modelo { get; init; }             // 55, 65, 57 ou NFSE
    public required string CodSit { get; init; }
    public required string Serie { get; init; }
    public required string Numero { get; init; }
    public string? Chave { get; init; }
    public DateOnly? Emissao { get; init; }
    public DateOnly? EntradaSaida { get; init; }
    public decimal Total { get; init; }
    public decimal Desconto { get; init; }
    public decimal Mercadorias { get; init; }
    public decimal Frete { get; init; }
    public decimal Seguro { get; init; }
    public decimal Outras { get; init; }
    public decimal BcIcms { get; init; }
    public decimal Icms { get; init; }
    public decimal BcSt { get; init; }
    public decimal IcmsSt { get; init; }
    public decimal Ipi { get; init; }
    public decimal Pis { get; init; }
    public decimal Cofins { get; init; }
    public decimal Iss { get; init; }
    public decimal RetPis { get; init; }
    public decimal RetCofins { get; init; }
    public decimal ValorServico { get; init; }
    public List<ItemLidoEfd> Itens { get; } = [];
    public List<AnaliticoLidoEfd> Analitico { get; } = [];

    public bool Cancelada => CodSit is "02" or "03";
    public bool Denegada => CodSit == "04";
    public bool Inutilizada => CodSit == "05";
    public string Situacao => Cancelada ? "CANCELADA" : Denegada ? "DENEGADA" : "AUTORIZADA";
    /// <summary>Data da escrituração da nota. Cancelada, denegada e inutilizada vêm sem datas no arquivo: valem o primeiro dia do período.</summary>
    public DateOnly? DataEfetiva(DateOnly inicioPeriodo) => EntradaSaida ?? Emissao ?? (CodSit != "00" && CodSit != "01" ? inicioPeriodo : null);
    public string Identificacao => $"{(Modelo == "NFSE" ? "NFS-e" : Modelo == "57" ? "CT-e" : Modelo == "65" ? "NFC-e" : "NF-e")} {Numero}" + (Serie.Length > 0 ? $" série {Serie}" : "");
}

public sealed record AjusteLidoEfd(int Linha, string Registro, string Codigo, string Descricao, decimal Valor, DateOnly Competencia, string? Observacao);
public sealed record ApuracaoIcmsLida(int Linha, DateOnly Competencia, decimal TotDebitos, decimal AjDebitos, decimal TotAjDebitos, decimal EstornosCred, decimal TotCreditos,
                                      decimal AjCreditos, decimal TotAjCreditos, decimal EstornosDeb, decimal SldCredorAnt, decimal SldApurado, decimal TotDed,
                                      decimal IcmsRecolher, decimal SldCredorTransportar, decimal DebEsp);
public sealed record InventarioItemLidoEfd(int Linha, string CodItem, string Unidade, decimal Quantidade, decimal ValorUnitario, decimal Valor);
public sealed record InventarioLidoEfd(int Linha, DateOnly Data, string Motivo, string? Conta, List<InventarioItemLidoEfd> Itens);
public sealed record ConsolidadoLidoEfd(int Linha, string Registro, string Modelo, DateOnly Competencia, string? Ncm, string Cfop, string? CstPis, string? CstCofins, decimal Valor,
                                        decimal BcPis, decimal AliqPis, decimal Pis, decimal BcCofins, decimal AliqCofins, decimal Cofins);
public sealed record ApuracaoContribLida(int Linha, DateOnly Competencia, bool Cumulativo, decimal ReceitaBase, decimal PisDebito, decimal CofinsDebito, decimal BaseCredito,
                                         decimal PisCredito, decimal CofinsCredito, decimal PisRetido, decimal CofinsRetido, decimal PisAPagar, decimal CofinsAPagar);

public sealed class EfdLida
{
    public required CabecalhoSped Cabecalho { get; init; }
    public bool Contribuicoes { get; init; }
    public string? Perfil { get; init; }
    public IReadOnlyList<ParticipanteLidoEfd> Participantes { get; init; } = [];
    public IReadOnlyList<ProdutoLidoEfd> Produtos { get; init; } = [];
    public IReadOnlyList<DocumentoLidoEfd> Documentos { get; init; } = [];
    public IReadOnlyList<AjusteLidoEfd> Ajustes { get; init; } = [];
    public IReadOnlyList<ApuracaoIcmsLida> ApuracoesIcms { get; init; } = [];
    public IReadOnlyList<InventarioLidoEfd> Inventarios { get; init; } = [];
    public IReadOnlyList<ConsolidadoLidoEfd> Consolidados { get; init; } = [];
    public IReadOnlyList<ApuracaoContribLida> ApuracoesContrib { get; init; } = [];
    /// <summary>Registros de dados presentes no arquivo que esta versão não importa (nome → quantidade).</summary>
    public IReadOnlyDictionary<string, int> NaoImportados { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<OcorrenciaImportacao> Ocorrencias { get; init; } = [];
}

/// <summary>Interpreta os registros da EFD ICMS/IPI e da EFD-Contribuições.</summary>
public static class LeitorEfd
{
    private static readonly HashSet<string> Estruturais = ["0000", "0001", "0005", "0100", "0110", "0140", "0990", "9001", "9900", "9990", "9999", "C001", "C990", "D001", "D990",
        "E001", "E990", "G001", "G990", "H001", "H990", "K001", "K990", "1001", "1990", "A001", "A990", "A010", "C010", "F001", "F990", "I001", "I990", "M001", "M990", "P001", "P990",
        "B001", "B990", "E100", "E200", "E500", "0200", "0190", "0150", "0300", "0305", "0400", "0450", "0460", "0500", "0600", "1010", "0120", "0111", "0145", "0205", "0206", "0208", "0210"];

    public static EfdLida Ler(ArquivoSped arq)
    {
        var contrib = arq.Tipo == TipoArquivoSped.EfdContribuicoes;
        var z = arq.Zero!;
        var cab = contrib
            ? new CabecalhoSped(arq.Tipo, LeitorSped.Documento(z[9]), z[8], z[10], null, z.Data(6) ?? default, z.Data(7) ?? default, z[2])
            : new CabecalhoSped(arq.Tipo, LeitorSped.Documento(z[7]), z[6], z[9], z[10].Length > 0 ? z[10] : null, z.Data(4) ?? default, z.Data(5) ?? default, z[2]);
        var oc = new List<OcorrenciaImportacao>();
        if (cab.Inicio == default || cab.Fim == default || cab.Fim < cab.Inicio)
            oc.Add(OcorrenciaImportacao.Erro(z.Linha, "0000", "As datas do período (DT_INI e DT_FIN) do registro 0000 são inválidas."));
        if (cab.Cnpj.Length != 14) oc.Add(OcorrenciaImportacao.Erro(z.Linha, "0000", "CNPJ do registro 0000 inválido."));

        var parts = new List<ParticipanteLidoEfd>();
        var prods = new List<ProdutoLidoEfd>();
        var docs = new List<DocumentoLidoEfd>();
        var ajustes = new List<AjusteLidoEfd>();
        var apIcms = new List<ApuracaoIcmsLida>();
        var invs = new List<InventarioLidoEfd>();
        var cons = new List<ConsolidadoLidoEfd>();
        var apContrib = new List<ApuracaoContribLida>();
        var naoImp = new Dictionary<string, int>();
        var unidades = new Dictionary<string, string>();

        DocumentoLidoEfd? doc = null;
        DateOnly competencia = new(cab.Inicio.Year, cab.Inicio.Month, 1);
        string ufSt = "";
        RegistroSped? c180 = null;
        var c181 = new List<RegistroSped>(); var c185 = new List<RegistroSped>();
        RegistroSped? m200 = null, m210 = null, m600 = null, m610 = null, m100 = null, m500 = null;
        InventarioLidoEfd? inv = null;
        string? ultimoE111 = null;

        void FecharC180()
        {
            if (c180 is null) return;
            var vend = c180.Nome == "C180";
            var off = c180.Nome == "C190" ? 1 : 0;      // C191/C195 trazem o CNPJ/CPF do participante antes do CST
            foreach (var p in c181)
            {
                var cf = c185.FirstOrDefault(x => x[3 + off] == p[3 + off]);
                cons.Add(new ConsolidadoLidoEfd(p.Linha, c180.Nome, c180[2], new DateOnly(c180.Data(3)?.Year ?? cab.Inicio.Year, c180.Data(3)?.Month ?? cab.Inicio.Month, 1), c180[6].Length > 0 ? c180[6] : null,
                    p[3 + off], p[2 + off], cf?[2 + off], p.Valor(4 + off), p.Valor(6 + off), p.Valor(7 + off), p.Valor(10 + off), cf?.Valor(6 + off) ?? 0, cf?.Valor(7 + off) ?? 0, cf?.Valor(10 + off) ?? 0));
            }
            _ = vend;
            c180 = null; c181.Clear(); c185.Clear();
        }
        void FecharApuracao()
        {
            if (m200 is null && m600 is null) return;
            var cum = m210?[2] == "51" || (m210 is null && m200 is { } a && a.Valor(9) > 0 && a.Valor(2) == 0);
            var pis = m200; var cof = m600;
            decimal Ret(RegistroSped? r) => r is null ? 0 : cum ? r.Valor(10) : r.Valor(6);
            apContrib.Add(new ApuracaoContribLida((pis ?? cof)!.Linha, competencia, cum, m210?.Valor(3) ?? m610?.Valor(3) ?? 0, m210?.Valor(13) ?? 0, m610?.Valor(13) ?? 0,
                m100?.Valor(4) ?? m500?.Valor(4) ?? 0, m100?.Valor(8) ?? 0, m500?.Valor(8) ?? 0, Ret(pis), Ret(cof), pis?.Valor(13) ?? 0, cof?.Valor(13) ?? 0));
            m200 = m210 = m600 = m610 = m100 = m500 = null;
        }

        foreach (var r in arq.Registros)
        {
            if (r.Nome is "C180" or "C190" && contrib) FecharC180();
            switch (r.Nome)
            {
                case "0150":
                    parts.Add(new ParticipanteLidoEfd(r.Linha, r[2], r[3], r[4], r[5].Length > 0 ? r[5] : null, r[6].Length > 0 ? r[6] : null, r[7].Length > 0 ? r[7] : null, r[8].Length > 0 ? r[8] : null));
                    break;
                case "0190":
                    unidades[r[2]] = r[3];
                    break;
                case "0200":
                    prods.Add(new ProdutoLidoEfd(r.Linha, r[2], r[3], r[4].Length > 0 ? r[4] : null, r[6], r[7], r[8].Length > 0 ? r[8] : null, r.Valor(12), !contrib && r[13].Length > 0 ? r[13] : null));
                    break;
                case "C100":
                {
                    var d = Doc(r, "C100", r[5], r[9], 11, 10, 12);
                    doc = d; docs.Add(d);
                    break;
                }
                case "A100":
                {
                    doc = new DocumentoLidoEfd
                    {
                        Linha = r.Linha, Registro = "A100", Entrada = r[2] == "0", Propria = r[3] == "0", CodPart = r[4].Length > 0 ? r[4] : null, Modelo = "NFSE", CodSit = r[5],
                        Serie = r[6], Numero = Zeros(r[8]), Chave = r[9].Length > 0 ? r[9] : null, Emissao = r.Data(10), EntradaSaida = r.Data(11) ?? r.Data(10), Total = r.Valor(12),
                        Desconto = r.Valor(14), Mercadorias = r.Valor(12), RetPis = r.Valor(19), RetCofins = r.Valor(20), Iss = r.Valor(21), Pis = r.Valor(16), Cofins = r.Valor(18),
                        ValorServico = r.Valor(12),
                    };
                    docs.Add(doc);
                    break;
                }
                case "D100":
                {
                    doc = new DocumentoLidoEfd
                    {
                        Linha = r.Linha, Registro = "D100", Entrada = r[2] == "0", Propria = r[3] == "0", CodPart = r[4].Length > 0 ? r[4] : null, Modelo = r[5].Length > 0 ? r[5] : "57", CodSit = r[6],
                        Serie = r[7], Numero = Zeros(r[9]), Chave = r[10].Length > 0 ? r[10] : null, Emissao = r.Data(11), EntradaSaida = r.Data(12) ?? r.Data(11), Total = r.Valor(15),
                        Desconto = r.Valor(16), ValorServico = r.Valor(18), BcIcms = r.Valor(19), Icms = r.Valor(20),
                    };
                    docs.Add(doc);
                    break;
                }
                case "C170" when doc is { Registro: "C100" }:
                    doc.Itens.Add(new ItemLidoEfd(r.Linha, int.TryParse(r[2], out var n) ? n : doc.Itens.Count + 1, r[3], r[4].Length > 0 ? r[4] : null, r.Valor(5), r[6], r.Valor(7), r.Valor(8),
                        r[10].Length > 0 ? r[10] : null, r[11], r.Valor(13), r.Valor(14), r.Valor(15), r.Valor(16), r.Valor(18), r[20].Length > 0 ? r[20] : null, r.Valor(22), r.Valor(23), r.Valor(24),
                        r[25].Length > 0 ? r[25] : null, r.Valor(26), r.Valor(27), r.Valor(30), r[31].Length > 0 ? r[31] : null, r.Valor(32), r.Valor(33), r.Valor(36)));
                    break;
                case "A170" when doc is { Registro: "A100" }:
                    doc.Itens.Add(new ItemLidoEfd(r.Linha, int.TryParse(r[2], out var na) ? na : doc.Itens.Count + 1, r[3], r[4].Length > 0 ? r[4] : null, 1, "UN", r.Valor(5), r.Valor(6),
                        null, "NFSE", 0, 0, 0, 0, 0, null, 0, 0, 0, r[9], r.Valor(10), r.Valor(11), r.Valor(12), r[13], r.Valor(14), r.Valor(15), r.Valor(16)));
                    break;
                case "C190" when doc is { Registro: "C100" } && !contrib:
                    doc.Analitico.Add(new AnaliticoLidoEfd(r.Linha, r[2], r[3], r.Valor(4), r.Valor(5), r.Valor(6), r.Valor(7), r.Valor(8), r.Valor(9), r.Valor(11)));
                    break;
                case "D190" when doc is { Registro: "D100" }:
                    doc.Analitico.Add(new AnaliticoLidoEfd(r.Linha, r[2], r[3], r.Valor(4), r.Valor(5), r.Valor(6), r.Valor(7), 0, 0, 0));
                    break;
                case "C180" or "C190" when contrib:
                    c180 = r;
                    break;
                case "C181" or "C191" when contrib && c180 is not null:
                    c181.Add(r);
                    break;
                case "C185" or "C195" when contrib && c180 is not null:
                    c185.Add(r);
                    break;
                case "E100":
                    competencia = r.Data(2) is { } di ? new DateOnly(di.Year, di.Month, 1) : competencia;
                    break;
                case "E110":
                    apIcms.Add(new ApuracaoIcmsLida(r.Linha, competencia, r.Valor(2), r.Valor(3), r.Valor(4), r.Valor(5), r.Valor(6), r.Valor(7), r.Valor(8), r.Valor(9), r.Valor(10),
                                                     r.Valor(11), r.Valor(12), r.Valor(13), r.Valor(14), r.Valor(15)));
                    break;
                case "E111":
                    ajustes.Add(new AjusteLidoEfd(r.Linha, "E111", r[2], r[3], r.Valor(4), competencia, null));
                    ultimoE111 = r[2];
                    break;
                case "E112":
                    if (ajustes.Count > 0 && ajustes[^1].Registro == "E111" && r[6].Length > 0) ajustes[^1] = ajustes[^1] with { Observacao = r[6] };
                    break;
                case "E200":
                    ufSt = r[2];
                    competencia = r.Data(3) is { } ds ? new DateOnly(ds.Year, ds.Month, 1) : competencia;
                    break;
                case "E220":
                    ajustes.Add(new AjusteLidoEfd(r.Linha, "E220", r[2], r[3], r.Valor(4), competencia, null));
                    break;
                case "H005":
                    inv = new InventarioLidoEfd(r.Linha, r.Data(2) ?? cab.Fim, r[4], null, []);
                    invs.Add(inv);
                    break;
                case "H010" when inv is not null:
                    if (r[8].Length == 0 || r[8] == "0")
                        inv.Itens.Add(new InventarioItemLidoEfd(r.Linha, r[2], r[3], r.Valor(4), r.Valor(5), r.Valor(6)));
                    if (inv.Conta is null && r[11].Length > 0) invs[^1] = inv = inv with { Conta = r[11] };
                    break;
                case "M100": m100 = r; break;
                case "M500": m500 = r; break;
                case "M200": m200 = r; break;
                case "M210": m210 = r; break;
                case "M600": m600 = r; break;
                case "M610": m610 = r; break;
                default:
                    if (!Estruturais.Contains(r.Nome) && !(r.Nome is "C170" or "C190" or "A170" or "D190" or "E111" or "E112" or "E113" or "E116" or "E210" or "E250" or "E510" or "E520" or "E530"
                                                                or "M105" or "M505" or "H010" or "C181" or "C185" or "C191" or "C195"))
                        naoImp[r.Nome] = naoImp.GetValueOrDefault(r.Nome) + 1;
                    break;
            }
        }
        FecharC180();
        FecharApuracao();
        _ = ufSt; _ = ultimoE111;

        // unidades: o 0190 só descreve; o C170 já traz a unidade
        return new EfdLida
        {
            Cabecalho = cab, Contribuicoes = contrib, Perfil = contrib ? null : z[14],
            Participantes = parts, Produtos = prods, Documentos = docs, Ajustes = ajustes, ApuracoesIcms = apIcms, Inventarios = invs, Consolidados = cons,
            ApuracoesContrib = apContrib, NaoImportados = naoImp, Ocorrencias = oc,
        };

        static string Zeros(string numero) => numero.Length > 0 && numero.All(char.IsAsciiDigit) ? numero.TrimStart('0') is { Length: > 0 } s ? s : "0" : numero;

        DocumentoLidoEfd Doc(RegistroSped r, string reg, string modelo, string chave, int campoDtES, int campoDtDoc, int campoTotal) => new()
        {
            Linha = r.Linha, Registro = reg, Entrada = r[2] == "0", Propria = r[3] == "0", CodPart = r[4].Length > 0 ? r[4] : null, Modelo = modelo, CodSit = r[6],
            Serie = r[7], Numero = Zeros(r[8]), Chave = chave.Length > 0 ? chave : null, Emissao = r.Data(campoDtDoc), EntradaSaida = r.Data(campoDtES) ?? r.Data(campoDtDoc),
            Total = r.Valor(campoTotal), Desconto = r.Valor(14), Mercadorias = r.Valor(16), Frete = r.Valor(18), Seguro = r.Valor(19), Outras = r.Valor(20),
            BcIcms = r.Valor(21), Icms = r.Valor(22), BcSt = r.Valor(23), IcmsSt = r.Valor(24), Ipi = r.Valor(25), Pis = r.Valor(26), Cofins = r.Valor(27),
        };
    }
}
