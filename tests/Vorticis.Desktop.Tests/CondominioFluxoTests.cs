using Vorticis.Application.Cadastros;
using Vorticis.Condominio.Desktop.ViewModels;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra;
using Vorticis.Infra.Tests;

namespace Vorticis.Desktop.Tests;

/// <summary>Fluxo do Condomínio pelas telas: unidades → taxas → cobranças do mês → rateio entre unidades lançado nas cobranças.</summary>
public class CondominioFluxoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private sealed class Contexto(VorticisApp app, long usuario, long empresa) : IContexto
    {
        public VorticisApp? App => app;
        public SessaoUsuario? Sessao { get; } = new(usuario, "admin", "Admin", true, false);
        public long? EmpresaId => empresa;
        public bool Pode(Permissao permissao) => true;
        public Task RecarregarEmpresasAsync(long? selecionar = null) => Task.CompletedTask;
        public void AoConcluirConfiguracaoInicial() { }
        public Task<bool> ConfirmarAsync(string titulo, string mensagem, string textoSim = "Sim", string textoNao = "Não") => Task.FromResult(true);
        public Task ReiniciarParaLoginAsync(string mensagem) => Task.CompletedTask;
        public Task<ModulosEmpresa?> EscolherModulosAsync(ModulosEmpresa inicial) => Task.FromResult<ModulosEmpresa?>(inicial);
        public ModulosEmpresa ModuloAtual => ModulosEmpresa.Condominio;
        public Task<string?> SolicitarSenhaAsync(string titulo, string mensagem, string textoSim = "Confirmar", string textoNao = "Cancelar") => Task.FromResult<string?>(null);
        public string DescricaoEmpresa => "Condomínio de teste";
        public void AbrirTela(string tela) { }
        public Task ConcluirImplantacaoAsync(string loginAdministrador) => Task.CompletedTask;
    }

    [Fact]
    public async Task Unidades_taxas_cobrancas_e_rateio_pelas_telas()
    {
        BancoDeTeste.ExigirBanco();
        var ctx = new Contexto(db.App, await db.AdminAsync(), await db.CriarEmpresaAsync("73012045000150"));
        var comp = new DateTime(2026, 7, 1);

        var unidades = new UnidadesViewModel(ctx);
        await unidades.CarregarAsync();
        foreach (var (num, fracao) in new[] { ("101", "0,5"), ("102", "0,3"), ("103", "0,2") })
        {
            unidades.NovaCommand.Execute(null);
            (unidades.Bloco, unidades.Numero, unidades.Fracao) = ("A", num, fracao);
            await unidades.GravarCommand.ExecuteAsync(null);
            Assert.Empty(unidades.Erros);
        }
        Assert.Equal(3, unidades.Lista.Count);

        var taxas = new TaxasViewModel(ctx);
        await taxas.CarregarAsync();
        (taxas.Descricao, taxas.Valor, taxas.Inicio) = ("Taxa ordinária", "3.000,00", new DateTime(2026, 7, 15));   // início no meio do mês vale para o mês
        await taxas.GravarCommand.ExecuteAsync(null);
        Assert.Empty(taxas.Erros);

        var cobr = new CobrancasViewModel(ctx) { Competencia = comp };
        await cobr.GerarCommand.ExecuteAsync(null);
        Assert.Empty(cobr.Erros);
        Assert.Equal(3, cobr.Lista.Count);
        Assert.Equal(["1.500,00", "900,00", "600,00"], cobr.Lista.OrderBy(c => c.Unidade).Select(c => c.Valor.Replace(' ', ' ')));

        var rateio = new RateioViewModel(ctx) { Competencia = comp, Descricao = "Rateio da pintura", Valor = "1.000,00" };
        await rateio.VerPreviaCommand.ExecuteAsync(null);
        Assert.Empty(rateio.Erros);
        Assert.Equal(3, rateio.Previa.Count);
        Assert.Contains("1.000,00", rateio.Totais);
        await rateio.LancarCommand.ExecuteAsync(null);
        Assert.Empty(rateio.Erros);
        Assert.Contains("3 unidade(s)", rateio.Mensagem);

        await cobr.CarregarAsync();
        var a101 = cobr.Lista.Single(c => c.Unidade == "A-101");
        Assert.Equal("2.000,00", a101.Valor.Replace(' ', ' '));
        Assert.Contains("Rateio da pintura", a101.Composicao);
    }
}
