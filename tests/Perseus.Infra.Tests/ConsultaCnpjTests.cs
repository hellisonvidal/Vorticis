using System.Net;
using System.Text;
using Perseus.Application.Cadastros;
using Perseus.Infra.Cnpj;

namespace Perseus.Infra.Tests;

/// <summary>Simula as APIs externas (sem acesso à internet nos testes).</summary>
public sealed class HttpSimulado(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<Uri> Chamadas { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Chamadas.Add(request.RequestUri!);
        return Task.FromResult(responder(request));
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

public class ConsultaCnpjTests
{
    // Formato real das respostas da BrasilAPI / Minha Receita (campos relevantes).
    public const string RespostaExemplo = """
        {"uf":"SP","cep":"01310100","cnpj":"11222333000181","bairro":"BELA VISTA","numero":"1000","municipio":"SAO PAULO",
         "logradouro":"PAULISTA","cnae_fiscal":4751201,"complemento":"CONJ 101","razao_social":"EXEMPLO COMERCIO DE INFORMATICA LTDA",
         "nome_fantasia":"EXEMPLO INFO","ddd_telefone_1":"1133334444","email":"CONTATO@EXEMPLO.COM.BR","opcao_pelo_mei":false,
         "opcao_pelo_simples":true,"codigo_municipio_ibge":3550308,"data_inicio_atividade":"2015-03-02",
         "codigo_natureza_juridica":2062,"descricao_situacao_cadastral":"ATIVA","descricao_tipo_de_logradouro":"AVENIDA",
         "qsa":[{"nome_socio":"FULANO DE TAL"}]}
        """;

    private static ConsultaCnpjPublica Consulta(HttpSimulado http) => new(new HttpClient(http));

    [Fact]
    public async Task Interpreta_os_dados_cadastrais()
    {
        var http = new HttpSimulado(_ => HttpSimulado.Json(RespostaExemplo));
        var r = await Consulta(http).ConsultarAsync("11222333000181", TestContext.Current.CancellationToken);

        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        var d = r.Valor;
        Assert.Equal("EXEMPLO COMERCIO DE INFORMATICA LTDA", d.RazaoSocial);
        Assert.Equal(("2062", "4751201", "3550308", "SP"), (d.NaturezaJuridica, d.CnaePrincipal, d.CodMunicipioIbge, d.Uf));
        Assert.Equal(new DateOnly(2015, 3, 2), d.DataInicioAtividades);
        Assert.Equal("AVENIDA PAULISTA", d.Logradouro);
        Assert.Equal("contato@exemplo.com.br", d.Email);
        Assert.Equal(RegimeTributario.SimplesNacional, d.RegimeSugerido);
        Assert.True(d.Ativa);
        Assert.StartsWith("BrasilAPI", d.Fonte);
        Assert.Equal("https://brasilapi.com.br/api/cnpj/v1/11222333000181", Assert.Single(http.Chamadas).ToString());
    }

    [Fact]
    public async Task Usa_a_fonte_reserva_quando_a_primeira_falha()
    {
        var http = new HttpSimulado(req => req.RequestUri!.Host == "brasilapi.com.br"
            ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            : HttpSimulado.Json(RespostaExemplo.Replace("\"cnae_fiscal\":4751201", "\"cnae_fiscal\":\"0111301\"")));
        var r = await Consulta(http).ConsultarAsync("11222333000181", TestContext.Current.CancellationToken);

        Assert.True(r.Sucesso);
        Assert.StartsWith("Minha Receita", r.Valor.Fonte);
        Assert.Equal("0111301", r.Valor.CnaePrincipal);   // aceita número como texto e preserva zero à esquerda
        Assert.Equal(2, http.Chamadas.Count);
    }

    [Fact]
    public async Task Nao_encontrado_e_falha_total_geram_mensagens_claras()
    {
        var ct = TestContext.Current.CancellationToken;
        var naoEncontrado = await Consulta(new HttpSimulado(_ => new HttpResponseMessage(HttpStatusCode.NotFound))).ConsultarAsync("11222333000181", ct);
        Assert.Contains("não encontrado", naoEncontrado.Erros[0]);

        var semRede = await Consulta(new HttpSimulado(_ => throw new HttpRequestException("sem rota"))).ConsultarAsync("11222333000181", ct);
        Assert.Contains("preencha manualmente", semRede.Erros[0]);
        Assert.Contains("sem rota", semRede.Erros[0]);
    }

    [Fact]
    public async Task Cnpj_invalido_nao_chega_a_consultar()
    {
        var http = new HttpSimulado(_ => HttpSimulado.Json(RespostaExemplo));
        var servico = new ServicoEmpresa(null!, null!, Consulta(http));
        var r = await servico.ConsultarCnpjAsync("11.222.333/0001-00", TestContext.Current.CancellationToken);
        Assert.Contains("CNPJ inválido", r.Erros[0]);
        Assert.Empty(http.Chamadas);
    }
}
