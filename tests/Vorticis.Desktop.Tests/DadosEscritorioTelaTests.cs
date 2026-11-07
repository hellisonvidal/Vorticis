using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Desktop.Comum.Views;
using Vorticis.Infra.Tests;
using E = Vorticis.Escritorio.Desktop;

namespace Vorticis.Desktop.Tests;

/// <summary>Escritório no primeiro uso: Dados do escritório (só o escritório, código 0001) e o escritório já como cliente 0001, primeiro da lista.</summary>
public class DadosEscritorioTelaTests(BancoDeTeste db, SessaoHeadless headless) : IClassFixture<BancoDeTeste>
{
    private static async Task<T> ForaDaSessao<T>(Task<T> tarefa)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = tarefa.ContinueWith(t => { if (t.IsFaulted) tcs.SetException(t.Exception!.InnerExceptions); else tcs.SetResult(t.Result); }, TaskScheduler.Default);
        return await tcs.Task;
    }

    [Fact]
    public async Task Primeiro_uso_cadastra_o_escritorio_como_0001_e_ele_ja_e_o_primeiro_cliente()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        await db.App.Perfis.ConcluirConfiguracaoInicialAsync(new Vorticis.Domain.Seguranca.SessaoUsuario(adm, "admin_teste", "Admin", true, false), ct);
        var pasta = Environment.GetEnvironmentVariable("VORTICIS_SCREENSHOTS");
        var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
        {
            async Task Calma(int n = 25) { for (var i = 0; i < n; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); } }
            Vorticis.Suite.Desktop.ModulosDaSuite.IncluirTelas(Avalonia.Application.Current!);   // telas do Escritório
            var vm = new E.MainViewModel();
            var janela = new MainWindow { DataContext = vm, Width = 1366, Height = 820 };
            try
            {
                janela.Show();
                await vm.InicializarAsync(db.ConnectionString);
                var login = Assert.IsType<LoginViewModel>(vm.Conteudo);
                (login.Usuario, login.Senha) = ("admin_teste", "Senha1234");
                await login.EntrarCommand.ExecuteAsync(null);
                for (var i = 0; i < 200 && !vm.Logado; i++) await Calma(1);
                await Calma(20);

                // sem escritório: abre Dados do escritório (só o escritório, sem plano, sócios, contadores e módulos)
                var cad = Assert.IsType<CadastroEmpresaViewModel>(vm.Conteudo);
                Assert.True(cad.ModoEscritorio);
                Assert.False(cad.MostraAbasEmpresa);
                cad.IncluirCommand.Execute(null);
                Assert.Equal("Cadastrar o escritório", cad.TituloFormulario);
                Assert.False(cad.MostraPrimeiroExercicio);
                (cad.Cnpj, cad.RazaoSocial, cad.NomeFantasia, cad.Cnae, cad.MunicipioIbge, cad.Uf) =
                    ("34028316000103", "Contabilidade Exemplo Ltda", "Exemplo Contábil", "6920601", "3550308", "SP");
                await Calma(5);
                var w = janela.JanelaDa(cad)!;
                if (pasta is not null) { w.CaptureRenderedFrame(); Dispatcher.UIThread.RunJobs(); w.CaptureRenderedFrame()?.Save(Path.Combine(pasta, "escritorio_dados_do_escritorio.png")); }
                Assert.DoesNotContain(w.GetLogicalDescendants().OfType<TabItem>(), t => t.IsVisible && t.Header as string is "Sócios" or "Contadores" or "Contabilidade");
                await cad.SalvarCommand.ExecuteAsync(null);
                for (var i = 0; i < 100 && cad.EmInclusao; i++) await Calma(1);
                Assert.True(cad.EmConsulta, string.Join("; ", cad.Erros));
                Assert.Contains("0001", cad.Mensagem);
                await Calma(10);
                Assert.Equal(1L, vm.EmpresaSelecionada?.Id);

                // Cadastros › Clientes: o escritório é o cliente 0001, o primeiro da lista
                vm.AbrirCommand.Execute(E.ViewModels.EscritorioTelas.Clientes);
                await Calma(40);
                var cli = Assert.IsType<E.ViewModels.ClientesEscritorioViewModel>(vm.Conteudo);
                var primeiro = cli.Lista.First();
                Assert.Equal((1, "Contabilidade Exemplo Ltda"), (primeiro.Codigo, primeiro.Nome));
                cli.IncluirCommand.Execute(null);
                Assert.Equal("2", cli.Codigo);                                                          // o próximo cliente é o 0002
                cli.CancelarCommand.Execute(null);
                var wc = janela.JanelaDa(cli)!;
                await Calma(5);
                if (pasta is not null) { wc.CaptureRenderedFrame(); Dispatcher.UIThread.RunJobs(); wc.CaptureRenderedFrame()?.Save(Path.Combine(pasta, "escritorio_clientes_0001.png")); }

                // seletor de clientes da barra: os clientes do cadastro (não só o escritório), "Todos os clientes" no alto
                var esc = Vorticis.Escritorio.ServicosEscritorio.De(vm.App!);
                var novo = await esc.GravarPessoaAsync(1, new Vorticis.Escritorio.PessoaEscritorio(0, 2, true, false, "Padaria Pão Quente Ltda", null), adm, ct);
                Assert.True(novo.Sucesso, string.Join("; ", novo.Erros));
                await vm.RecarregarClientesSeletorAsync();
                Assert.True(vm.UsaSeletorDeClientes);
                Assert.Equal(["Todos os clientes", "0001 – Contabilidade Exemplo Ltda", "0002 – Padaria Pão Quente Ltda"], vm.ClientesNoSeletor.Select(c => c.ToString()));
                Assert.Equal(1L, vm.EmpresaId);                                                         // a empresa de trabalho continua o escritório
                await Calma(5);
                Assert.True(janela.FindControl<ComboBox>("ClienteCabecalho")!.IsVisible);
                Assert.False(janela.FindControl<ComboBox>("EmpresaCabecalho")!.IsVisible);

                // o cliente em foco filtra as telas: Tarefas mostra só as dele
                foreach (var (pessoa, titulo) in new (long?, string)[] { (novo.Valor, "DAS da padaria"), (primeiro.P.Id, "Balancete do escritório") })
                    Assert.True((await esc.GravarTarefaAsync(new Vorticis.Escritorio.Tarefa(0, 1, pessoa, titulo, null, DateOnly.FromDateTime(DateTime.Today).AddDays(5), null, 2,
                                                                                           Vorticis.Escritorio.SituacaoTarefa.Pendente, Vorticis.Escritorio.Recorrencia.Nenhuma), adm, ct)).Sucesso);
                vm.AbrirCommand.Execute(E.ViewModels.EscritorioTelas.Tarefas);
                await Calma(30);
                var tar = Assert.IsType<E.ViewModels.TarefasViewModel>(vm.Conteudo);
                Assert.Equal(2, tar.Lista.Count);
                vm.ClienteEmFoco = vm.ClientesNoSeletor.Single(c => c.Id == novo.Valor);
                await Calma(30);
                Assert.Equal(["DAS da padaria"], tar.Lista.Select(t => t.T.Titulo));
                if (pasta is not null) { janela.CaptureRenderedFrame(); Dispatcher.UIThread.RunJobs(); janela.CaptureRenderedFrame()?.Save(Path.Combine(pasta, "escritorio_seletor_clientes.png")); }
                vm.ClienteEmFoco = vm.ClientesNoSeletor[0];
                await Calma(30);
                Assert.Equal(2, tar.Lista.Count);

                // Clientes › aba Contrato: valor, dia do vencimento e periodicidade do honorário gravados com o cliente
                vm.AbrirCommand.Execute(E.ViewModels.EscritorioTelas.Clientes);
                await Calma(30);
                cli.Selecionado = cli.Lista.Single(l => l.P.Id == novo.Valor);
                await Calma(10);
                cli.AlterarCommand.Execute(null);
                (cli.ValorHonorario, cli.DiaHonorario) = ("1.250,00", 31);
                cli.PeriodicidadeHonorario = E.ViewModels.ClientesEscritorioViewModel.PeriodicidadesHonorario[0];
                var abas = new E.Views.ClienteFichaView().FindControl<TabControl>("AbasCliente")!.Items.OfType<TabItem>().Select(t => t.Header as string).ToList();
                Assert.Equal(["Dados cadastrais", "Contrato"], abas);
                Assert.Contains("Contrato", abas);
                Assert.DoesNotContain("Módulos do contrato", abas);
                Assert.DoesNotContain("Honorários e cobranças", abas);
                await cli.SalvarCommand.ExecuteAsync(null);
                await Calma(20);
                Assert.True(cli.EmConsulta, string.Join("; ", cli.Erros));
                var contrato = Assert.Single(await esc.ListarHonorariosAsync(1, adm, ct));
                Assert.Equal((1250m, 31, Vorticis.Escritorio.PeriodicidadeHonorario.Mensal), (contrato.Valor, contrato.DiaVencimento, contrato.Periodicidade));
                Assert.Contains("1.250,00 por mês", cli.SituacaoHonorario);

                // Financeiro › Honorários: Novo honorário com 3 parcelas (traz o valor do contrato), cobrança avulsa e baixa
                vm.AbrirCommand.Execute(E.ViewModels.EscritorioTelas.Honorarios);
                await Calma(30);
                var hon = Assert.IsType<E.ViewModels.HonorariosViewModel>(vm.Conteudo);
                hon.CompetenciaDe = new DateTime(2027, 1, 1);
                hon.CompetenciaAte = new DateTime(2027, 12, 1);
                vm.ClienteEmFoco = vm.ClientesNoSeletor.Single(c => c.Id == novo.Valor);
                await hon.NovoHonorarioCommand.ExecuteAsync(null);
                await Calma(10);
                Assert.True(hon.Lancamento.FichaAberta);
                Assert.Equal(novo.Valor, hon.Lancamento.Cliente?.Valor);                              // o cliente em foco já vem escolhido
                Assert.Equal("1.250,00", hon.Lancamento.Valor);
                hon.Lancamento.Competencia = new DateTime(2027, 1, 1);
                hon.Lancamento.Quantidade = 3;
                Assert.Equal(["31/01/2027", "28/02/2027", "31/03/2027"], hon.Lancamento.Previa.Select(p => p.Vencimento));
                var wl = FichaPopup.Aberta(janela.JanelaDa(hon)!.GetLogicalDescendants().OfType<E.Views.LancamentoCobrancaHost>().Single())!;
                if (pasta is not null) { wl.CaptureRenderedFrame(); Dispatcher.UIThread.RunJobs(); wl.CaptureRenderedFrame()?.Save(Path.Combine(pasta, "escritorio_novo_honorario.png")); }
                await hon.Lancamento.LancarCommand.ExecuteAsync(null);
                await Calma(20);
                Assert.False(hon.Lancamento.FichaAberta, string.Join("; ", hon.Lancamento.Erros));
                await hon.CobrancaAvulsaCommand.ExecuteAsync(null);
                (hon.Lancamento.Descricao, hon.Lancamento.Valor, hon.Lancamento.Competencia, hon.Lancamento.Dia) = ("Declaração de IRPF", "350,00", new DateTime(2027, 4, 1), 30);
                await hon.Lancamento.LancarCommand.ExecuteAsync(null);
                await Calma(20);
                Assert.Equal(["Honorário", "Honorário", "Honorário", "Avulsa"], hon.Cobrancas.Select(c => c.Tipo));
                hon.Selecionada = hon.Cobrancas[0];
                hon.BaixarCommand.Execute(null);
                Assert.True(hon.Baixa.FichaAberta);
                hon.Baixa.Observacao = "Pix";
                await hon.Baixa.ConfirmarCommand.ExecuteAsync(null);
                await Calma(20);
                Assert.Equal("Recebida", hon.Cobrancas[0].Situacao);
                Assert.Contains("(Pix)", hon.Cobrancas[0].Recebido);
                if (pasta is not null) { var wh = janela.JanelaDa(hon)!; wh.CaptureRenderedFrame(); Dispatcher.UIThread.RunJobs(); wh.CaptureRenderedFrame()?.Save(Path.Combine(pasta, "escritorio_financeiro_honorarios.png")); }
                return true;
            }
            finally
            {
                typeof(MainWindow).GetField("_fechamentoConfirmado", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(janela, true);
                janela.Close();
            }
        }, ct));
        Assert.True(ok);
    }
}
