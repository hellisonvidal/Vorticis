using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Vorticis.Application.Cadastros;
using Vorticis.Desktop.Comum.Modulos;
using Vorticis.Domain.Seguranca;
using Vorticis.Frota;
using Vorticis.Infra;
using Vorticis.Infra.Tests;
using F = Vorticis.Frota.Desktop;

namespace Vorticis.Desktop.Tests;

/// <summary>Módulo Frota (piloto): menus, telas que renderizam e o fluxo veículo → abastecimentos → relatório pelas telas.</summary>
public class FrotaTelasTests(SessaoHeadless headless, BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private static IEnumerable<ItemMenu> Folhas(IEnumerable<ItemMenu> itens) => itens.SelectMany(i => i.Filhos is null ? [i] : Folhas(i.Filhos));

    private static async Task<T> ForaDaSessao<T>(Task<T> tarefa)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = tarefa.ContinueWith(t => { if (t.IsFaulted) tcs.SetException(t.Exception!.InnerExceptions); else tcs.SetResult(t.Result); }, TaskScheduler.Default);
        return await tcs.Task;
    }

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
        public ModulosEmpresa ModuloAtual => ModulosEmpresa.Frota;
        public Task<string?> SolicitarSenhaAsync(string titulo, string mensagem, string textoSim = "Confirmar", string textoNao = "Cancelar") => Task.FromResult<string?>(null);
        public string DescricaoEmpresa => "Transportadora de teste";
        public void AbrirTela(string tela) { }
        public Task ConcluirImplantacaoAsync(string loginAdministrador) => Task.CompletedTask;
    }

    [Fact]
    public void Menus_da_frota_tem_cadastros_operacao_e_relatorios_registrados()
    {
        var vm = new F.MainViewModel();
        Assert.Equal("Vorticis Frota", vm.Modulo.Nome);
        Assert.Equal(ModulosEmpresa.Frota, vm.Modulo.ModuloEmpresa);
        Assert.Equal("Transportadora", vm.Modulo.RotuloEmpresaAtiva);
        var cadastros = Folhas(vm.Menus[0].Filhos!).Select(i => i.Titulo).ToList();
        Assert.Contains("Veículos", cadastros);
        Assert.Contains("Motoristas", cadastros);
        Assert.Contains("Planos de manutenção", cadastros);
        Assert.Contains("Documentos e vencimentos", cadastros);
        var operacao = Assert.Single(vm.Menus, m => m.Titulo == "_Operação");
        Assert.Equal(["Abastecimentos", "Ordens de serviço", "Viagens"], operacao.Filhos!.Select(i => i.Titulo));
        var rel = Assert.Single(vm.Menus, m => m.Titulo == "_Relatórios");
        Assert.Equal(["Painel da frota", TabelasFrota.TituloCustos, TabelasFrota.TituloConsumo, TabelasFrota.TituloViagens, TabelasFrota.TituloVencimentos],
                     rel.Filhos!.Select(i => i.Titulo));
        Assert.All(Folhas(vm.Menus).Where(i => i.Chave is not null && i.Chave != TelasComuns.Sair), i => Assert.True(vm.ConheceTela(i.Chave!), i.Chave));
        Assert.All(vm.Atalhos.Where(a => a.Chave is not null), a => Assert.True(vm.ConheceTela(a.Chave!), a.Chave));
    }

    [Fact]
    public async Task Telas_da_frota_renderizam_e_o_relatorio_monta_as_colunas_da_tabela()
    {
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(() =>
        {
            var m = new F.MainViewModel();
            var relatorio = new F.ViewModels.RelatorioFrotaViewModel(m, TabelasFrota.TituloCustos);
            (object Vm, UserControl View)[] telas =
            [
                (new F.ViewModels.PainelFrotaViewModel(m), new F.Views.PainelFrotaView()),
                (new F.ViewModels.VeiculosViewModel(m), new F.Views.VeiculosView()),
                (new F.ViewModels.MotoristasViewModel(m), new F.Views.MotoristasView()),
                (new F.ViewModels.PlanosViewModel(m), new F.Views.PlanosView()),
                (new F.ViewModels.DocumentosViewModel(m), new F.Views.DocumentosView()),
                (new F.ViewModels.AbastecimentosViewModel(m), new F.Views.AbastecimentosView()),
                (new F.ViewModels.OrdensViewModel(m), new F.Views.OrdensView()),
                (new F.ViewModels.ViagensViewModel(m), new F.Views.ViagensView()),
                (relatorio, new F.Views.RelatorioFrotaView()),
            ];
            foreach (var (vm, view) in telas)
            {
                view.DataContext = vm;
                var janela = new Window { Content = view, Width = 1280, Height = 760 };
                janela.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.NotNull(janela.CaptureRenderedFrame());
                janela.Close();
            }
            var rv = new F.Views.RelatorioFrotaView { DataContext = relatorio };
            relatorio.Colunas = TabelasFrota.Custos([], [], [], []).Colunas;
            var grade = rv.FindControl<DataGrid>("Grade")!;
            Assert.Equal(9, grade.Columns.Count);
            Assert.Equal("Custo por km", grade.Columns[^1].Header);
            Assert.False(new F.ViewModels.RelatorioFrotaViewModel(m, TabelasFrota.TituloVencimentos).UsaPeriodo);
            return Task.FromResult(true);
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }

    [Fact]
    public async Task Veiculo_abastecimentos_com_media_e_relatorio_pelas_telas()
    {
        BancoDeTeste.ExigirBanco();
        var ctx = new Contexto(db.App, await db.AdminAsync(), await db.CriarEmpresaAsync("48731625000230"));

        var veiculos = new F.ViewModels.VeiculosViewModel(ctx);
        await veiculos.CarregarAsync();
        Assert.Equal("1", veiculos.Codigo);                            // código sugerido
        (veiculos.Placa, veiculos.Marca, veiculos.Modelo, veiculos.KmAtual, veiculos.MediaEsperada) = ("QWE4R56", "Mercedes", "Actros", "50000", "2,6");
        await veiculos.GravarCommand.ExecuteAsync(null);
        Assert.Equal("Veículo gravado.", veiculos.Mensagem);
        Assert.Equal("QWE4R56", Assert.Single(veiculos.Lista).Placa);
        veiculos.Tipo = F.ViewModels.VeiculosViewModel.Tipos.First(t => t.Valor == "REBOQUE");
        Assert.Equal("NENHUM", veiculos.Combustivel!.Valor);           // reboque não abastece

        var abast = new F.ViewModels.AbastecimentosViewModel(ctx) { Inicio = DateTime.Today.AddDays(-10), Fim = DateTime.Today };
        await abast.CarregarAsync();
        abast.Veiculo = abast.VeiculosOpcoes.Single(o => o.Id is not null);
        Assert.Equal("DIESEL_S10", abast.Combustivel!.Valor);          // combustível do veículo
        foreach (var (dias, km, litros) in new[] { (5, "50500", "300"), (2, "51500", "400") })
        {
            (abast.Data, abast.Km, abast.Litros, abast.Valor) = (DateTime.Today.AddDays(-dias), km, litros, "2000");
            abast.Veiculo = abast.VeiculosOpcoes.Single(o => o.Id is not null);
            await abast.GravarCommand.ExecuteAsync(null);
            Assert.Empty(abast.Erros);
        }
        Assert.Contains("2,50 km/l", abast.Mensagem);                  // 1.000 km / 400 l, abaixo de 2,6 mas dentro da tolerância de 15%
        Assert.Equal(2, abast.Lista.Count);
        Assert.Equal("2,50 km/l", abast.Lista[0].Media);

        var rel = new F.ViewModels.RelatorioFrotaViewModel(ctx, TabelasFrota.TituloConsumo) { Inicio = DateTime.Today.AddDays(-10), Fim = DateTime.Today };
        await rel.GerarCommand.ExecuteAsync(null);
        Assert.True(rel.TemDados);
        Assert.Equal("2,50", Assert.Single(rel.Linhas).V4);

        var painel = new F.ViewModels.PainelFrotaViewModel(ctx);
        await painel.CarregarAsync();
        Assert.Contains("Nenhuma pendência", painel.Resumo);
        Assert.Contains("1 veículo(s) ativo(s)", painel.Numeros);
    }
}
