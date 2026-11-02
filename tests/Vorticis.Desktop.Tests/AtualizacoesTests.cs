using System.Net;
using System.Text;
using Vorticis.Desktop.Comum;
using Vorticis.Desktop.Comum.ViewModels;

namespace Vorticis.Desktop.Tests;

/// <summary>Ajuda › Verificar atualizações: lê o atualizacao.json do site e compara com a versão instalada.</summary>
public class AtualizacoesTests
{
    private sealed class SiteFalso(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? Pedido { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Pedido = r.RequestUri!.ToString();
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    [Theory]
    [InlineData("1.0.2", "1.0.1", true)]
    [InlineData("v1.1.0", "1.0.9", true)]
    [InlineData("1.0.1", "1.0.1", false)]
    [InlineData("1.0.0", "1.0.1", false)]
    [InlineData("lixo", "1.0.1", false)]
    public void Compara_versoes(string publicada, string instalada, bool nova) => Assert.Equal(nova, Atualizacoes.MaisNova(publicada, instalada));

    [Fact]
    public async Task Mostra_a_versao_nova_publicada_no_site_e_sem_site_nao_procura()
    {
        var (site, handler) = (Atualizacoes.Site, Atualizacoes.Handler);
        var contabil = new Vorticis.Desktop.ViewModels.MainViewModel();
        try
        {
            var falso = new SiteFalso("""{"versao":"99.0.0","instalador":"https://vorticis.exemplo/downloads/Setup-Vorticis-99.0.0.exe","notas":"Novidades","publicada":"02/11/2026"}""");
            (Atualizacoes.Site, Atualizacoes.Handler) = (() => "https://vorticis.exemplo", falso);
            var vm = new AtualizacoesViewModel(contabil);
            await vm.VerificarAsync();
            Assert.Equal("https://vorticis.exemplo/atualizacao.json", falso.Pedido);
            Assert.True(vm.TemNova);
            Assert.StartsWith("Há uma versão nova: 99.0.0", vm.Situacao);
            Assert.Equal("Novidades", vm.Notas);
            Assert.EndsWith("Setup-Vorticis-99.0.0.exe", vm.Instalador);

            Atualizacoes.Handler = new SiteFalso($$"""{"versao":"{{SobreViewModel.Versao}}"}""");
            await vm.VerificarAsync();
            Assert.False(vm.TemNova);
            Assert.StartsWith("Você já usa a versão mais nova", vm.Situacao);

            Atualizacoes.Handler = new SiteFalso("", HttpStatusCode.NotFound);
            await vm.VerificarAsync();
            Assert.StartsWith("Não foi possível consultar o site", vm.Situacao);

            Atualizacoes.Site = () => null;
            await vm.VerificarAsync();
            Assert.Contains("ainda não foi configurado", vm.Situacao);
            Assert.Null(await Atualizacoes.ConsultarAsync(TestContext.Current.CancellationToken));
        }
        finally { (Atualizacoes.Site, Atualizacoes.Handler) = (site, handler); }
    }
}
