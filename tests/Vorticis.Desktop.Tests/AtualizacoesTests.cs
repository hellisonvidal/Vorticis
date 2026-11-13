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
        var (site, handler, repo) = (Atualizacoes.Site, Atualizacoes.Handler, Atualizacoes.RepositorioGitHub);
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
            Assert.StartsWith("Não foi possível consultar as atualizações", vm.Situacao);

            // sem site: a última versão publicada no GitHub, com o instalador e só as notas da versão nova
            Atualizacoes.Site = () => null;
            var github = new SiteFalso("""{"tag_name":"v99.1.0","published_at":"2026-11-13T10:00:00Z","body":"# Vorticis 99.1.0\nNovidades\n\n---\n\n# Vorticis 1.0.0","assets":[{"name":"VorticisSuiteEmpresarial-portatil-99.1.0.zip","browser_download_url":"https://x/p.zip"},{"name":"Setup-Vorticis-99.1.0.exe","browser_download_url":"https://x/Setup-Vorticis-99.1.0.exe"}]}""");
            Atualizacoes.Handler = github;
            await vm.VerificarAsync();
            Assert.Equal("https://api.github.com/repos/hellisonvidal/Vorticis/releases/latest", github.Pedido);
            Assert.True(vm.TemNova);
            Assert.True(vm.PodeAtualizar);
            Assert.Equal("https://x/Setup-Vorticis-99.1.0.exe", vm.Instalador);
            Assert.Equal("# Vorticis 99.1.0\nNovidades", vm.Notas);
            Assert.Contains("13/11/2026", vm.Situacao);

            // Atualizar agora: baixa o instalador e o executa (aqui, sem executar)
            string? executado = null;
            var executar = Atualizacoes.Executar;
            Atualizacoes.Executar = a => { executado = a; return false; };
            try
            {
                Atualizacoes.Handler = new SiteFalso("conteudo do instalador");
                var atualizacao = vm.AtualizarAgoraCommand.ExecuteAsync(null);
                for (var i = 0; i < 100 && contabil.Confirmacao is null; i++) await Task.Delay(20, TestContext.Current.CancellationToken);
                Assert.Contains("Os dados são preservados", contabil.Confirmacao!.Mensagem);
                contabil.ResponderConfirmacaoCommand.Execute(true);
                await atualizacao;
                Assert.Empty(vm.Erros);
                Assert.NotNull(executado);
                Assert.EndsWith("Setup-Vorticis-99.1.0.exe", executado);
                Assert.Equal("conteudo do instalador", await File.ReadAllTextAsync(executado!, TestContext.Current.CancellationToken));
                Assert.Contains("Instalador baixado em", vm.Situacao);
                Assert.Equal(100, vm.Progresso);
            }
            finally { Atualizacoes.Executar = executar; }

            Atualizacoes.RepositorioGitHub = () => null;
            await vm.VerificarAsync();
            Assert.Contains("Não há onde procurar", vm.Situacao);
            Assert.Null(await Atualizacoes.ConsultarAsync(TestContext.Current.CancellationToken));
        }
        finally { (Atualizacoes.Site, Atualizacoes.Handler, Atualizacoes.RepositorioGitHub) = (site, handler, repo); }
    }
}
