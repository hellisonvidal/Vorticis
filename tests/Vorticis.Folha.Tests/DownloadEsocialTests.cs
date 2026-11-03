using System.Net;
using System.Text;
using System.Xml.Linq;
using Vorticis.Folha.Esocial;
using Vorticis.Folha.Esocial.Importacao;
using Vorticis.Folha.Servicos;

namespace Vorticis.Folha.Tests;

/// <summary>Baixar do eSocial: pedidos SOAP (consulta de identificadores e download por Id/recibo) e leitura das respostas, com servidor simulado (nunca acessa o eSocial).</summary>
public class DownloadEsocialTests(BancoFolha db) : IClassFixture<BancoFolha>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Simulado(Func<string, string, string> responder) : HttpMessageHandler
    {
        public List<(string Url, string Acao, string Corpo)> Chamadas { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var corpo = await request.Content!.ReadAsStringAsync(cancellationToken);
            var acao = request.Headers.GetValues("SOAPAction").First().Trim('"');
            Chamadas.Add((request.RequestUri!.ToString(), acao, corpo));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responder(acao, corpo), Encoding.UTF8, "text/xml") };
        }
    }

    private static string Soap(string conteudo) => $"<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\"><s:Body>{conteudo}</s:Body></s:Envelope>";

    private static ClienteDownloadEsocial Cliente(Simulado s, AmbienteEsocial a = AmbienteEsocial.ProducaoRestrita)
        => new(new HttpClient(s), EnderecosDownloadEsocial.Do(a), AmbientePadrao);

    private const string AmbientePadrao = "12.345.678/0001-95";

    private const string RespostaIds = """
        <ConsultarIdentificadoresEventosEmpregadorResponse xmlns="http://www.esocial.gov.br/servicos/empregador/consulta/identificadores-eventos/v1_0_0"><ConsultarIdentificadoresEventosEmpregadorResult>
        <eSocial xmlns="http://www.esocial.gov.br/schema/consulta/identificadores-eventos/retorno/v1_0_0"><retornoConsultaIdentificadoresEvts><status><cdResposta>201</cdResposta><descResposta>Sucesso</descResposta></status>
        <retornoIdentificadoresEvts><idEvento><id>ID112345678000195202601100900010000A</id><nrRecibo>1.1.0000000000000000011</nrRecibo></idEvento><idEvento><id>ID112345678000195202601100900020000B</id><nrRecibo>1.1.0000000000000000012</nrRecibo></idEvento></retornoIdentificadoresEvts></retornoConsultaIdentificadoresEvts></eSocial>
        </ConsultarIdentificadoresEventosEmpregadorResult></ConsultarIdentificadoresEventosEmpregadorResponse>
        """;

    [Fact]
    public async Task Monta_os_pedidos_de_consulta_de_identificadores_e_le_a_resposta()
    {
        var s = new Simulado((_, _) => Soap(RespostaIds));
        var c = Cliente(s);
        var emp = await c.ListarEmpregadorAsync("S-1200", new DateOnly(2025, 3, 15), Ct);
        var tab = await c.ListarTabelaAsync("S-1010", new DateOnly(2018, 1, 1), new DateOnly(2026, 1, 31), Ct);
        var trab = await c.ListarTrabalhadorAsync("529.982.247-25", new DateOnly(2018, 1, 1), new DateOnly(2026, 1, 31), Ct);

        Assert.True(emp.Sucesso);
        Assert.Equal(2, emp.Eventos.Count);
        Assert.Equal("1.1.0000000000000000011", emp.Eventos[0].Recibo);
        Assert.Equal(3, s.Chamadas.Count);
        Assert.All(s.Chamadas, x => Assert.Contains("producaorestrita", x.Url));
        Assert.EndsWith("ServicoConsultarIdentificadoresEventos/ConsultarIdentificadoresEventosEmpregador", s.Chamadas[0].Acao);
        var corpo = XElement.Parse(s.Chamadas[0].Corpo);
        Assert.Equal("S-1200", corpo.Descendants().Single(e => e.Name.LocalName == "tpEvt").Value);
        Assert.Equal("2025-03", corpo.Descendants().Single(e => e.Name.LocalName == "perApur").Value);
        Assert.Equal("12345678", corpo.Descendants().Single(e => e.Name.LocalName == "nrInsc").Value);
        Assert.Equal("1", corpo.Descendants().Single(e => e.Name.LocalName == "tpInsc").Value);
        Assert.EndsWith("ConsultarIdentificadoresEventosTabela", s.Chamadas[1].Acao);
        var t = XElement.Parse(s.Chamadas[1].Corpo);
        Assert.Equal("2018-01-01", t.Descendants().Single(e => e.Name.LocalName == "dtIni").Value);
        Assert.Equal("2026-01-31", t.Descendants().Single(e => e.Name.LocalName == "dtFim").Value);
        Assert.EndsWith("ConsultarIdentificadoresEventosTrabalhador", s.Chamadas[2].Acao);
        Assert.Equal("52998224725", XElement.Parse(s.Chamadas[2].Corpo).Descendants().Single(e => e.Name.LocalName == "cpfTrab").Value);
        Assert.Equal(2, trab.Eventos.Count);
        Assert.True(tab.Sucesso);
    }

    [Fact]
    public async Task Baixa_por_id_e_por_recibo_e_separa_os_eventos_da_resposta()
    {
        var evento = AmostrasEsocial.Evento("evtTabLotacao", AmostrasEsocial.Id(new DateTime(2026, 1, 1)),
            "<ideEvento><tpAmb>1</tpAmb></ideEvento><ideEmpregador><tpInsc>1</tpInsc><nrInsc>12345678</nrInsc></ideEmpregador><infoLotacao><inclusao><ideLotacao><codLotacao>L1</codLotacao><iniValid>2020-01</iniValid></ideLotacao><dadosLotacao><tpLotacao>01</tpLotacao></dadosLotacao></inclusao></infoLotacao>");
        var embutido = evento.Replace("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", "");
        var respostas = new Queue<string>(
        [
            // 1) eventos embutidos como XML
            Soap($"<SolicitarDownloadEventosPorIdResponse xmlns=\"http://www.esocial.gov.br/servicos/empregador/download/solicitacao/v1_0_0\"><SolicitarDownloadEventosPorIdResult><eSocial xmlns=\"http://www.esocial.gov.br/schema/download/retornoSolicitacao/v1_0_0\"><retornoSolicitacao><status><cdResposta>201</cdResposta><descResposta>Sucesso</descResposta></status><retornoEvts><arquivo>{embutido}</arquivo></retornoEvts></retornoSolicitacao></eSocial></SolicitarDownloadEventosPorIdResult></SolicitarDownloadEventosPorIdResponse>"),
            // 2) evento em base64
            Soap($"<R><status><cdResposta>201</cdResposta><descResposta>Sucesso</descResposta></status><arquivo>{Convert.ToBase64String(Encoding.UTF8.GetBytes(evento))}</arquivo></R>"),
            // 3) recusado
            Soap("<R><status><cdResposta>401</cdResposta><descResposta>Empregador sem procuração</descResposta></status></R>"),
        ]);
        var s = new Simulado((_, _) => respostas.Dequeue());
        var c = Cliente(s, AmbienteEsocial.Producao);

        var porId = await c.BaixarPorIdAsync(["ID112345678000195202601100900010000A", "ID112345678000195202601100900020000B"], Ct);
        Assert.True(porId.Sucesso);
        Assert.Single(porId.Arquivos);
        Assert.Contains("download.esocial.gov.br", s.Chamadas[0].Url);
        Assert.EndsWith("ServicoSolicitarDownloadEventos/SolicitarDownloadEventosPorId", s.Chamadas[0].Acao);
        var ids = XElement.Parse(s.Chamadas[0].Corpo).Descendants().Where(e => e.Name.LocalName == "id").Select(e => e.Value).ToList();
        Assert.Equal(2, ids.Count);
        var oc = new List<Vorticis.Domain.Sped.Importacao.OcorrenciaImportacao>();
        Assert.Single(LeitorEsocial.Ler(porId.Arquivos, oc).Eventos);

        var porRecibo = await c.BaixarPorReciboAsync("1.1.0000000000000000011", Ct);
        Assert.Single(porRecibo.Arquivos);
        Assert.EndsWith("SolicitarDownloadEventosPorNrRecibo", s.Chamadas[1].Acao);
        Assert.Equal("1.1.0000000000000000011", XElement.Parse(s.Chamadas[1].Corpo).Descendants().Single(e => e.Name.LocalName == "nrRecibo").Value);
        Assert.Single(LeitorEsocial.Ler(porRecibo.Arquivos, oc).Eventos);

        var recusado = await c.BaixarPorIdAsync(["ID1"], Ct);
        Assert.False(recusado.Sucesso);
        Assert.Equal(401, recusado.Codigo);
        Assert.Empty(recusado.Arquivos);
    }

    private sealed class ClienteFalso(IReadOnlyList<ArquivoEsocial> arquivos) : IClienteDownloadEsocial
    {
        private readonly List<(EventoLido Evento, ArquivoEsocial Arquivo)> _eventos = arquivos.SelectMany(a => LeitorEsocial.Ler([a], []).Eventos.Select(e => (e, a))).ToList();
        public List<int> TamanhosDeLote { get; } = [];
        public List<string> Consultas { get; } = [];

        private static RespostaIdentificadores Ids(IEnumerable<EventoLido> e) => new(201, "ok", e.Select(x => new IdentificadorEvento(x.Id, x.Recibo)).ToList(), "");

        public Task<RespostaIdentificadores> ListarEmpregadorAsync(string tipoEvento, DateOnly periodo, CancellationToken ct = default)
        {
            Consultas.Add($"{tipoEvento} {periodo:yyyy-MM}");
            return Task.FromResult(Ids(_eventos.Select(x => x.Evento).Where(e => e.Tipo == tipoEvento && e.Evento.Elements().First(x => x.Name.LocalName == "ideEvento").Elements().FirstOrDefault(x => x.Name.LocalName == "perApur")?.Value == periodo.ToString("yyyy-MM"))));
        }

        public Task<RespostaIdentificadores> ListarTabelaAsync(string tipoEvento, DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        {
            Consultas.Add($"{tipoEvento} tabela");
            return Task.FromResult(Ids(_eventos.Select(x => x.Evento).Where(e => e.Tipo == tipoEvento)));
        }

        public Task<RespostaIdentificadores> ListarTrabalhadorAsync(string cpf, DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        {
            Consultas.Add($"cpf {cpf}");
            return Task.FromResult(Ids(_eventos.Select(x => x.Evento).Where(e => e.Tipo.StartsWith("S-2") && e.Evento.ToString().Contains(cpf))));
        }

        public Task<RespostaDownload> BaixarPorIdAsync(IReadOnlyList<string> ids, CancellationToken ct = default)
        {
            TamanhosDeLote.Add(ids.Count);
            var arquivos = ids.Select(i => _eventos.First(e => e.Evento.Id == i).Arquivo).ToList();
            return Task.FromResult(new RespostaDownload(201, "ok", arquivos, ""));
        }

        public Task<RespostaDownload> BaixarPorReciboAsync(string recibo, CancellationToken ct = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Baixa_tudo_do_periodo_em_lotes_e_alimenta_a_importacao()
    {
        BancoFolha.ExigirBanco();
        var empresa = await db.CriarEmpresaAsync(AmostrasEsocial.Cnpj);
        var adm = await db.AdminAsync();
        var falso = new ClienteFalso(AmostrasEsocial.Conjunto());
        var svc = db.Folha.DownloadEsocial;
        svc.FabricaCliente = (_, _, _) => falso;
        using var cert = AssinaturaEsocialTests.CertificadoTeste("EMPRESA ORIGEM LTDA:" + AmostrasEsocial.Cnpj);
        var opcoes = new OpcoesDownloadEsocial(new DateOnly(2025, 1, 1), new DateOnly(2025, 3, 1));

        var r = await svc.BaixarAsync(empresa, opcoes, AmbienteEsocial.ProducaoRestrita, PapelTransmissor.Empregador, cert, adm, ct: Ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        Assert.All(falso.TamanhosDeLote, n => Assert.InRange(n, 1, ClienteDownloadEsocial.IdsPorSolicitacao));
        Assert.Contains("S-1010 tabela", falso.Consultas);
        Assert.Contains("S-1200 2025-02", falso.Consultas);
        Assert.Contains("S-1210 2025-03", falso.Consultas);
        Assert.Contains($"cpf {AmostrasEsocial.CpfMaria}", falso.Consultas);          // eventos dos trabalhadores achados nas remunerações
        Assert.True(r.Valor.Baixados >= 20);
        Assert.Equal(r.Valor.Identificadores, r.Valor.Baixados);

        // os arquivos baixados seguem o mesmo caminho da importação por arquivo
        var analise = await db.Folha.ImportacaoEsocial.AnalisarAsync(empresa, r.Valor.Arquivos, "Baixado do eSocial", adm, Ct);
        Assert.True(analise.Sucesso, string.Join("; ", analise.Erros));
        Assert.Equal(2, analise.Valor.Dados.Trabalhadores.Count);
        var previa = await db.Folha.ImportacaoEsocial.PreviaAsync(empresa, analise.Valor, new OpcoesImportacaoEsocial(), adm, Ct);
        Assert.True(previa.Sucesso);
        Assert.Equal(2, previa.Valor.Plano.Funcionarios.Count);

        // certificado de outro empregador e perfil sem permissão são recusados
        using var outro = AssinaturaEsocialTests.CertificadoTeste("OUTRA EMPRESA:99888777000166");
        var recusado = await svc.BaixarAsync(empresa, opcoes, AmbienteEsocial.ProducaoRestrita, PapelTransmissor.Empregador, outro, adm, ct: Ct);
        Assert.False(recusado.Sucesso);
        Assert.Contains("não é do empregador", string.Join(' ', recusado.Erros));
        Assert.False((await svc.BaixarAsync(empresa, opcoes, AmbienteEsocial.ProducaoRestrita, PapelTransmissor.Empregador, cert, 999999, ct: Ct)).Sucesso);
        Assert.False((await svc.BaixarAsync(empresa, opcoes with { Fim = new DateOnly(2024, 1, 1) }, AmbienteEsocial.ProducaoRestrita, PapelTransmissor.Empregador, cert, adm, ct: Ct)).Sucesso);
    }

    [Fact]
    public async Task Sem_eventos_ou_com_falha_de_comunicacao_avisa_o_motivo()
    {
        BancoFolha.ExigirBanco();
        var empresa = await db.CriarEmpresaAsync("98765432000198");
        var adm = await db.AdminAsync();
        var svc = db.Folha.DownloadEsocial;
        svc.FabricaCliente = (_, _, _) => new ClienteFalso([]);
        using var cert = AssinaturaEsocialTests.CertificadoTeste("EMPRESA:98765432000198");
        var vazio = await svc.BaixarAsync(empresa, new OpcoesDownloadEsocial(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 1)), AmbienteEsocial.ProducaoRestrita, PapelTransmissor.Empregador, cert, adm, ct: Ct);
        Assert.False(vazio.Sucesso);
        Assert.Contains("nenhum evento", string.Join(' ', vazio.Erros));

        var s = new Simulado((_, _) => throw new HttpRequestException("sem rede"));
        svc.FabricaCliente = (_, _, _) => Cliente(s);
        var falha = await svc.BaixarAsync(empresa, new OpcoesDownloadEsocial(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 1)), AmbienteEsocial.ProducaoRestrita, PapelTransmissor.Empregador, cert, adm, ct: Ct);
        Assert.False(falha.Sucesso);
        Assert.Contains("Falha de conexão", string.Join(' ', falha.Erros));
    }
}
