using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Vorticis.Domain.Sped.Importacao;

namespace Vorticis.Folha.Esocial.Importacao;

/// <summary>Ajudas para navegar nos XMLs do eSocial sem depender do namespace de cada evento (evtXxx/v_S_01_0x_00).</summary>
internal static class Xe
{
    public static XElement? Filho(this XElement? e, string nome) => e?.Elements().FirstOrDefault(x => x.Name.LocalName == nome);

    public static XElement? Cam(this XElement? e, params string[] caminho)
    {
        foreach (var n in caminho) e = e.Filho(n);
        return e;
    }

    public static XElement? Primeiro(this XElement? e) => e?.Elements().FirstOrDefault();

    public static IEnumerable<XElement> Lista(this XElement? e, string nome) => e?.Elements().Where(x => x.Name.LocalName == nome) ?? [];

    public static XElement? Desc(this XElement? e, string nome) => e?.Descendants().FirstOrDefault(x => x.Name.LocalName == nome);

    public static string? Txt(this XElement? e, string nome) => Limpar(e.Filho(nome)?.Value);

    public static string? Txt(this XElement? e, params string[] caminho) => Limpar(e.Cam(caminho)?.Value);

    public static string? Limpar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static string Digitos(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());

    public static DateOnly? Dt(string? s)
        => s is not null && DateOnly.TryParseExact(s.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    /// <summary>Período "AAAA-MM" (primeiro dia do mês); "AAAA" vira dezembro do ano (competência do 13º salário).</summary>
    public static DateOnly? Periodo(string? s)
    {
        s = Limpar(s);
        if (s is null) return null;
        if (s.Length == 4 && int.TryParse(s, out var a) && a is > 1900 and < 2200) return new DateOnly(a, 12, 1);
        if (s.Length >= 7 && DateOnly.TryParseExact(s[..7] + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        return null;
    }

    public static decimal? Num(string? s)
        => s is not null && decimal.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    public static int? Int(string? s) => s is not null && int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    public static bool Sim(string? s) => string.Equals(s?.Trim(), "S", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Leitura dos arquivos de eventos do eSocial: o XML puro do evento (raiz eSocial), o XML dentro de lote/retorno com recibo (como baixado do portal ou
/// exportado por outro sistema) e ZIPs com vários arquivos. Só reconhece e separa os eventos; a interpretação é do <see cref="ConsolidadorEsocial"/>.
/// </summary>
public static class LeitorEsocial
{
    private const long LimiteTotal = 1L << 30;       // 1 GB descompactado (proteção contra ZIP malicioso)
    private const int ProfundidadeZip = 3;

    /// <summary>Tipo do evento pelo nome do elemento evtXxx.</summary>
    public static readonly IReadOnlyDictionary<string, string> Tipos = new Dictionary<string, string>
    {
        ["evtInfoEmpregador"] = "S-1000", ["evtTabEstab"] = "S-1005", ["evtTabRubrica"] = "S-1010", ["evtTabLotacao"] = "S-1020",
        ["evtAdmissao"] = "S-2200", ["evtAltCadastral"] = "S-2205", ["evtAltContratual"] = "S-2206", ["evtAfastTemp"] = "S-2230", ["evtDeslig"] = "S-2299",
        ["evtTSVInicio"] = "S-2300", ["evtTSVTermino"] = "S-2399", ["evtRemun"] = "S-1200", ["evtPgtos"] = "S-1210", ["evtExclusao"] = "S-3000",
    };

    public static string Descricao(string tipo) => tipo switch
    {
        "S-1000" => "Empregador", "S-1005" => "Estabelecimentos", "S-1010" => "Rubricas", "S-1020" => "Lotações",
        "S-2200" => "Admissões", "S-2205" => "Alterações cadastrais", "S-2206" => "Alterações contratuais", "S-2230" => "Afastamentos",
        "S-2299" => "Desligamentos", "S-2300" => "Trabalhadores sem vínculo (início)", "S-2399" => "Trabalhadores sem vínculo (término)",
        "S-1200" => "Remunerações", "S-1210" => "Pagamentos", "S-3000" => "Exclusões", _ => tipo,
    };

    /// <summary>Abre um ZIP (inclusive ZIP dentro de ZIP); qualquer outro arquivo volta como está.</summary>
    public static IReadOnlyList<ArquivoEsocial> Expandir(string nome, byte[] bytes, ICollection<OcorrenciaImportacao>? ocorrencias = null)
    {
        var res = new List<ArquivoEsocial>();
        long total = 0;
        Expandir(nome, bytes, 0, res, ref total, ocorrencias);
        return res;
    }

    private static bool EhZip(byte[] b) => b.Length > 4 && b[0] == 'P' && b[1] == 'K' && b[2] == 3 && b[3] == 4;

    private static void Expandir(string nome, byte[] bytes, int nivel, List<ArquivoEsocial> res, ref long total, ICollection<OcorrenciaImportacao>? oc)
    {
        if (!EhZip(bytes)) { res.Add(new ArquivoEsocial(nome, bytes)); return; }
        if (nivel >= ProfundidadeZip) { oc?.Add(OcorrenciaImportacao.Aviso(0, nome, "ZIP aninhado demais: ignorado.")); return; }
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            foreach (var e in zip.Entries)
            {
                if (e.Length == 0 || e.FullName.EndsWith('/')) continue;
                var ext = Path.GetExtension(e.Name).ToLowerInvariant();
                if (ext is not (".xml" or ".zip" or "")) continue;
                if (total + e.Length > LimiteTotal) { oc?.Add(OcorrenciaImportacao.Erro(0, nome, "O ZIP é grande demais (mais de 1 GB descompactado).")); return; }
                using var s = e.Open();
                using var ms = new MemoryStream();
                var buffer = new byte[81920];
                int n;
                while ((n = s.Read(buffer, 0, buffer.Length)) > 0)
                {
                    total += n;
                    if (total > LimiteTotal) { oc?.Add(OcorrenciaImportacao.Erro(0, nome, "O ZIP é grande demais (mais de 1 GB descompactado).")); return; }
                    ms.Write(buffer, 0, n);
                }
                Expandir($"{nome}/{e.FullName}", ms.ToArray(), nivel + 1, res, ref total, oc);
            }
        }
        catch (InvalidDataException)
        {
            oc?.Add(OcorrenciaImportacao.Aviso(0, nome, "Não foi possível abrir o ZIP: arquivo danificado."));
        }
    }

    /// <summary>Impressão digital do conjunto de arquivos (ordem dos nomes), para não importar duas vezes o mesmo material.</summary>
    public static string Impressao(IEnumerable<ArquivoEsocial> arquivos)
    {
        using var sha = SHA256.Create();
        foreach (var a in arquivos.OrderBy(a => a.Nome, StringComparer.Ordinal))
        {
            var h = SHA256.HashData(a.Conteudo);
            sha.TransformBlock(h, 0, h.Length, null, 0);
        }
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    /// <summary>Lê todos os arquivos e devolve os eventos encontrados (sem repetir o mesmo Id).</summary>
    public static (List<EventoLido> Eventos, Dictionary<string, int> NaoSuportados) Ler(IEnumerable<ArquivoEsocial> arquivos, ICollection<OcorrenciaImportacao> ocorrencias)
    {
        var porId = new Dictionary<string, EventoLido>(StringComparer.Ordinal);
        var naoSuportados = new Dictionary<string, int>(StringComparer.Ordinal);
        var semId = 0;
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        foreach (var arq in arquivos)
        {
            XDocument doc;
            try
            {
                using var ms = new MemoryStream(arq.Conteudo);
                using var xr = XmlReader.Create(ms, settings);
                doc = XDocument.Load(xr, LoadOptions.None);
            }
            catch (XmlException ex)
            {
                ocorrencias.Add(OcorrenciaImportacao.Aviso(ex.LineNumber, arq.Nome, $"Arquivo ignorado: não é um XML válido ({ex.Message.Split('.')[0]})."));
                continue;
            }
            var recibos = RecibosPorId(doc);
            var achou = false;
            foreach (var evt in doc.Descendants().Where(e => e.Parent?.Name.LocalName == "eSocial" && e.Name.LocalName.StartsWith("evt", StringComparison.Ordinal)))
            {
                var nome = evt.Name.LocalName;
                if (!Tipos.TryGetValue(nome, out var tipo)) { naoSuportados[nome] = naoSuportados.GetValueOrDefault(nome) + 1; achou = true; continue; }
                achou = true;
                var id = evt.Attribute("Id")?.Value.Trim() ?? $"SEM-ID-{++semId}";
                var ide = evt.Filho("ideEvento");
                var retif = Xe.Int(ide.Txt("indRetif")) ?? 1;
                var anterior = ide.Txt("nrRecibo");
                var recibo = recibos.GetValueOrDefault(id);
                var novo = new EventoLido(tipo, id, recibo, retif, anterior, arq.Nome, evt);
                if (porId.TryGetValue(id, out var ja))
                {
                    if (ja.Recibo is null && recibo is not null) porId[id] = novo;
                    continue;
                }
                porId[id] = novo;
            }
            if (!achou) ocorrencias.Add(OcorrenciaImportacao.Aviso(0, arq.Nome, "Arquivo sem eventos do eSocial reconhecíveis (ele só traz o retorno do envio?)."));
        }
        return ([.. porId.Values], naoSuportados);
    }

    /// <summary>Recibo (recibo/nrRecibo) de cada evento: fica no elemento de retorno que carrega o mesmo Id do evento.</summary>
    private static Dictionary<string, string> RecibosPorId(XDocument doc)
    {
        var res = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var nr in doc.Descendants().Where(e => e.Name.LocalName == "nrRecibo" && e.Parent?.Name.LocalName == "recibo"))
        {
            var dono = nr.Ancestors().FirstOrDefault(a => a.Attribute("Id")?.Value.StartsWith("ID", StringComparison.Ordinal) == true);
            if (dono is not null && Xe.Limpar(nr.Value) is { } v) res[dono.Attribute("Id")!.Value.Trim()] = v;
        }
        return res;
    }
}
