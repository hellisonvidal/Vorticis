using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using Vorticis.Folha.Esocial.Importacao;

namespace Vorticis.Folha.Esocial;

/// <summary>
/// Endereços dos serviços de consulta de identificadores e de download de eventos do eSocial (Manual de Orientação do Desenvolvedor).
/// Conferir com a versão vigente do manual: o Vorticis não pôde testar estes serviços no ambiente real.
/// </summary>
public sealed record EnderecosDownloadEsocial(Uri Identificadores, Uri Download)
{
    public static readonly EnderecosDownloadEsocial ProducaoRestrita = new(
        new("https://webservices.producaorestrita.esocial.gov.br/servicos/empregador/consultaidentificadoreseventos/WsConsultarIdentificadoresEventos.svc"),
        new("https://webservices.producaorestrita.esocial.gov.br/servicos/empregador/dwlcirurgico/WsSolicitarDownloadEventos.svc"));

    public static readonly EnderecosDownloadEsocial Producao = new(
        new("https://webservices.consulta.esocial.gov.br/servicos/empregador/consultaidentificadoreseventos/WsConsultarIdentificadoresEventos.svc"),
        new("https://webservices.download.esocial.gov.br/servicos/empregador/dwlcirurgico/WsSolicitarDownloadEventos.svc"));

    public static EnderecosDownloadEsocial Do(AmbienteEsocial a) => a == AmbienteEsocial.Producao ? Producao : ProducaoRestrita;
}

public sealed record IdentificadorEvento(string Id, string? Recibo);

/// <summary>Resposta da consulta de identificadores: 201 = consulta feita; 202 = nada encontrado; 3xx/4xx = erro.</summary>
public sealed record RespostaIdentificadores(int Codigo, string Descricao, IReadOnlyList<IdentificadorEvento> Eventos, string XmlBruto)
{
    public bool Sucesso => Codigo is 201 or 202 or 203;
}

/// <summary>Resposta do download: os arquivos já separados (XML dos eventos com o recibo) e o texto bruto.</summary>
public sealed record RespostaDownload(int Codigo, string Descricao, IReadOnlyList<ArquivoEsocial> Arquivos, string XmlBruto)
{
    public bool Sucesso => Codigo is 201 or 202 or 203;
}

public interface IClienteDownloadEsocial
{
    Task<RespostaIdentificadores> ListarEmpregadorAsync(string tipoEvento, DateOnly periodo, CancellationToken ct = default);
    Task<RespostaIdentificadores> ListarTabelaAsync(string tipoEvento, DateOnly inicio, DateOnly fim, CancellationToken ct = default);
    Task<RespostaIdentificadores> ListarTrabalhadorAsync(string cpf, DateOnly inicio, DateOnly fim, CancellationToken ct = default);
    Task<RespostaDownload> BaixarPorIdAsync(IReadOnlyList<string> ids, CancellationToken ct = default);
    Task<RespostaDownload> BaixarPorReciboAsync(string recibo, CancellationToken ct = default);
}

/// <summary>
/// Cliente SOAP 1.1 dos serviços ConsultarIdentificadoresEventos (por empregador, tabela ou trabalhador) e SolicitarDownloadEventos (por Id ou por número
/// de recibo). Autenticação por TLS mútuo com o certificado do transmissor, como o envio de lotes. Os testes usam o construtor com um servidor simulado.
/// </summary>
public sealed class ClienteDownloadEsocial(HttpClient http, EnderecosDownloadEsocial enderecos, string raizCnpj) : IClienteDownloadEsocial
{
    public const string NsConsulta = "http://www.esocial.gov.br/servicos/empregador/consulta/identificadores-eventos/v1_0_0";
    public const string NsConsultaEmpregador = "http://www.esocial.gov.br/schema/consulta/identificadores-eventos/empregador/v1_0_0";
    public const string NsConsultaTabela = "http://www.esocial.gov.br/schema/consulta/identificadores-eventos/tabela/v1_0_0";
    public const string NsConsultaTrabalhador = "http://www.esocial.gov.br/schema/consulta/identificadores-eventos/trabalhador/v1_0_0";
    public const string NsDownload = "http://www.esocial.gov.br/servicos/empregador/download/solicitacao/v1_0_0";
    public const string NsDownloadId = "http://www.esocial.gov.br/schema/download/solicitacao/id/v1_0_0";
    public const string NsDownloadRecibo = "http://www.esocial.gov.br/schema/download/solicitacao/nrRecibo/v1_0_0";
    /// <summary>Quantos identificadores vão em cada solicitação de download.</summary>
    public const int IdsPorSolicitacao = 10;

    public static ClienteDownloadEsocial Criar(X509Certificate2 certificado, AmbienteEsocial ambiente, string raizCnpj, TimeSpan? timeout = null)
    {
        var handler = new SocketsHttpHandler();
        handler.SslOptions.ClientCertificates = [certificado];
        return new ClienteDownloadEsocial(new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromSeconds(120) }, EnderecosDownloadEsocial.Do(ambiente), raizCnpj);
    }

    private string Raiz => new string(raizCnpj.Where(char.IsDigit).ToArray()) is { Length: >= 8 } d ? d[..8] : raizCnpj;

    private XElement Empregador(XNamespace ns) => new(ns + "ideEmpregador", new XElement(ns + "tpInsc", 1), new XElement(ns + "nrInsc", Raiz));

    private static string Dia(DateOnly d) => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static string Mes(DateOnly d) => d.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

    // ---- pedidos (públicos para os testes conferirem o XML)

    public XElement PedidoEmpregador(string tipoEvento, DateOnly periodo)
    {
        XNamespace n = NsConsultaEmpregador;
        return new XElement(n + "eSocial", new XElement(n + "consultaIdentificadoresEvts", Empregador(n),
            new XElement(n + "consultaEvtsEmpregador", new XElement(n + "tpEvt", tipoEvento), new XElement(n + "perApur", Mes(periodo)))));
    }

    public XElement PedidoTabela(string tipoEvento, DateOnly inicio, DateOnly fim)
    {
        XNamespace n = NsConsultaTabela;
        return new XElement(n + "eSocial", new XElement(n + "consultaIdentificadoresEvts", Empregador(n),
            new XElement(n + "consultaEvtsTabela", new XElement(n + "tpEvt", tipoEvento), new XElement(n + "dtIni", Dia(inicio)), new XElement(n + "dtFim", Dia(fim)))));
    }

    public XElement PedidoTrabalhador(string cpf, DateOnly inicio, DateOnly fim)
    {
        XNamespace n = NsConsultaTrabalhador;
        return new XElement(n + "eSocial", new XElement(n + "consultaIdentificadoresEvts", Empregador(n),
            new XElement(n + "consultaEvtsTrabalhador", new XElement(n + "cpfTrab", new string(cpf.Where(char.IsDigit).ToArray())), new XElement(n + "dtIni", Dia(inicio)), new XElement(n + "dtFim", Dia(fim)))));
    }

    public XElement PedidoPorId(IReadOnlyList<string> ids)
    {
        XNamespace n = NsDownloadId;
        return new XElement(n + "eSocial", new XElement(n + "download", Empregador(n),
            new XElement(n + "solicDownloadEvtsPorId", ids.Select(i => new XElement(n + "id", i)))));
    }

    public XElement PedidoPorRecibo(string recibo)
    {
        XNamespace n = NsDownloadRecibo;
        return new XElement(n + "eSocial", new XElement(n + "download", Empregador(n),
            new XElement(n + "solicDownloadEventosPorNrRecibo", new XElement(n + "nrRecibo", recibo))));
    }

    // ---- chamadas

    private async Task<RespostaIdentificadores> ConsultarAsync(string operacao, XElement pedido, CancellationToken ct)
    {
        XNamespace n = NsConsulta;
        var corpo = new XElement(n + operacao, new XElement(n + "consulta", pedido));
        return LerIdentificadores(await SoapEsocial.PostarAsync(http, enderecos.Identificadores, $"{NsConsulta}/ServicoConsultarIdentificadoresEventos/{operacao}", corpo, ct));
    }

    public Task<RespostaIdentificadores> ListarEmpregadorAsync(string tipoEvento, DateOnly periodo, CancellationToken ct = default)
        => ConsultarAsync("ConsultarIdentificadoresEventosEmpregador", PedidoEmpregador(tipoEvento, periodo), ct);

    public Task<RespostaIdentificadores> ListarTabelaAsync(string tipoEvento, DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => ConsultarAsync("ConsultarIdentificadoresEventosTabela", PedidoTabela(tipoEvento, inicio, fim), ct);

    public Task<RespostaIdentificadores> ListarTrabalhadorAsync(string cpf, DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => ConsultarAsync("ConsultarIdentificadoresEventosTrabalhador", PedidoTrabalhador(cpf, inicio, fim), ct);

    public async Task<RespostaDownload> BaixarPorIdAsync(IReadOnlyList<string> ids, CancellationToken ct = default)
    {
        const string operacao = "SolicitarDownloadEventosPorId";
        XNamespace n = NsDownload;
        var corpo = new XElement(n + operacao, new XElement(n + "solicitacao", PedidoPorId(ids)));
        return LerDownload(await SoapEsocial.PostarAsync(http, enderecos.Download, $"{NsDownload}/ServicoSolicitarDownloadEventos/{operacao}", corpo, ct));
    }

    public async Task<RespostaDownload> BaixarPorReciboAsync(string recibo, CancellationToken ct = default)
    {
        const string operacao = "SolicitarDownloadEventosPorNrRecibo";
        XNamespace n = NsDownload;
        var corpo = new XElement(n + operacao, new XElement(n + "solicitacao", PedidoPorRecibo(recibo)));
        return LerDownload(await SoapEsocial.PostarAsync(http, enderecos.Download, $"{NsDownload}/ServicoSolicitarDownloadEventos/{operacao}", corpo, ct));
    }

    // ---- respostas (leitura tolerante: procura os elementos pelo nome local, sem depender da versão do namespace)

    private static string? Texto(XElement pai, string nome) => pai.Descendants().FirstOrDefault(e => e.Name.LocalName == nome)?.Value.Trim();

    private static int Inteiro(XElement pai, string nome) => int.TryParse(Texto(pai, nome), out var n) ? n : 0;

    public static RespostaIdentificadores LerIdentificadores(XElement resposta)
    {
        var status = resposta.Descendants().FirstOrDefault(e => e.Name.LocalName == "status") ?? throw new ErroComunicacaoEsocial("Resposta do eSocial sem o elemento status.");
        var eventos = new List<IdentificadorEvento>();
        foreach (var e in resposta.Descendants().Where(x => x.Name.LocalName is "idEvento" or "identificador" or "evento" or "ideEvento"))
        {
            var id = e.Attribute("Id")?.Value ?? e.Attribute("id")?.Value ?? e.Elements().FirstOrDefault(x => x.Name.LocalName is "id" or "Id")?.Value;
            if (string.IsNullOrWhiteSpace(id)) continue;
            var recibo = e.Elements().FirstOrDefault(x => x.Name.LocalName is "nrRecibo" or "nrRec")?.Value;
            if (eventos.All(x => x.Id != id.Trim())) eventos.Add(new IdentificadorEvento(id.Trim(), string.IsNullOrWhiteSpace(recibo) ? null : recibo.Trim()));
        }
        return new RespostaIdentificadores(Inteiro(status, "cdResposta"), Texto(status, "descResposta") ?? "", eventos, resposta.ToString(SaveOptions.DisableFormatting));
    }

    public static RespostaDownload LerDownload(XElement resposta)
    {
        var status = resposta.Descendants().FirstOrDefault(e => e.Name.LocalName == "status") ?? throw new ErroComunicacaoEsocial("Resposta do eSocial sem o elemento status.");
        var arquivos = new List<ArquivoEsocial>();
        var temEventos = resposta.Descendants().Any(e => e.Parent?.Name.LocalName == "eSocial" && e.Name.LocalName.StartsWith("evt", StringComparison.Ordinal) && LeitorEsocial.Tipos.ContainsKey(e.Name.LocalName));
        if (temEventos)
            arquivos.Add(new ArquivoEsocial($"download_{DateTime.Now:yyyyMMddHHmmssfff}.xml", Encoding.UTF8.GetBytes(resposta.ToString(SaveOptions.DisableFormatting))));
        else
        {
            // os eventos podem vir como texto (XML escapado) ou em base64 dentro de <arquivo>
            var n = 0;
            foreach (var folha in resposta.Descendants().Where(e => e.Name.LocalName is "arquivo" or "evento" && !e.HasElements && !string.IsNullOrWhiteSpace(e.Value)))
            {
                var txt = folha.Value.Trim();
                string? xml = null;
                if (txt.StartsWith('<')) xml = txt;
                else
                {
                    try { var dec = Encoding.UTF8.GetString(Convert.FromBase64String(txt)); if (dec.TrimStart().StartsWith('<')) xml = dec; } catch (FormatException) { }
                }
                if (xml is not null) arquivos.Add(new ArquivoEsocial($"download_{++n}.xml", Encoding.UTF8.GetBytes(xml)));
            }
        }
        return new RespostaDownload(Inteiro(status, "cdResposta"), Texto(status, "descResposta") ?? "", arquivos, resposta.ToString(SaveOptions.DisableFormatting));
    }
}
