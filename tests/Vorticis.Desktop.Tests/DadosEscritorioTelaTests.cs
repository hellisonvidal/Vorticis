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

                // seletor de clientes da barra: os clientes do cadastro (não só o escritório), sempre um escolhido (sem "todos os clientes")
                var esc = Vorticis.Escritorio.ServicosEscritorio.De(vm.App!);
                var novo = await esc.GravarPessoaAsync(1, new Vorticis.Escritorio.PessoaEscritorio(0, 2, true, false, "Padaria Pão Quente Ltda", null), adm, ct);
                Assert.True(novo.Sucesso, string.Join("; ", novo.Erros));
                await vm.RecarregarClientesSeletorAsync();
                Assert.True(vm.UsaSeletorDeClientes);
                Assert.Equal(["0001 – Contabilidade Exemplo Ltda", "0002 – Padaria Pão Quente Ltda"], vm.ClientesNoSeletor.Select(c => c.ToString()));
                Assert.Equal(primeiro.P.Id, vm.ClienteEmFocoId);                                         // o escritório (0001) vem escolhido
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
                Assert.Equal(["Balancete do escritório"], tar.Lista.Select(t => t.T.Titulo));           // nunca mistura clientes
                vm.ClienteEmFoco = vm.ClientesNoSeletor.Single(c => c.Id == novo.Valor);
                await Calma(30);
                Assert.Equal(["DAS da padaria"], tar.Lista.Select(t => t.T.Titulo));
                if (pasta is not null) { janela.CaptureRenderedFrame(); Dispatcher.UIThread.RunJobs(); janela.CaptureRenderedFrame()?.Save(Path.Combine(pasta, "escritorio_seletor_clientes.png")); }
                vm.ClienteEmFoco = vm.ClientesNoSeletor[0];
                await Calma(30);
                Assert.Equal(["Balancete do escritório"], tar.Lista.Select(t => t.T.Titulo));

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
                // a pesquisa é pelo vencimento (datas)
                hon.VencimentoDe = new DateTime(2027, 1, 1);
                hon.VencimentoAte = new DateTime(2028, 12, 31);
                vm.ClienteEmFoco = vm.ClientesNoSeletor.Single(c => c.Id == novo.Valor);
                await hon.NovoHonorarioCommand.ExecuteAsync(null);
                await Calma(10);
                Assert.True(hon.Lancamento.FichaAberta);
                Assert.Equal(novo.Valor, hon.Lancamento.Cliente?.Valor);                              // o cliente em foco já vem escolhido
                Assert.Equal("1.250,00", hon.Lancamento.Valor);
                hon.Lancamento.Competencia = new DateTime(2027, 1, 1);
                hon.Lancamento.Quantidade = 3;
                // regime de competência (padrão): vence no mês seguinte ao da competência
                Assert.True(hon.Lancamento.RegimeCompetencia);
                Assert.Equal(["28/02/2027", "31/03/2027", "30/04/2027"], hon.Lancamento.Previa.Select(p => p.Vencimento));
                // regime de caixa: vence no próprio mês (as duas caixas se excluem)
                hon.Lancamento.RegimeCaixa = true;
                Assert.False(hon.Lancamento.RegimeCompetencia);
                Assert.Equal(["31/01/2027", "28/02/2027", "31/03/2027"], hon.Lancamento.Previa.Select(p => p.Vencimento));
                var wl = FichaPopup.Aberta(janela.JanelaDa(hon)!.GetLogicalDescendants().OfType<E.Views.LancamentoCobrancaHost>().Single())!;
                if (pasta is not null) { wl.CaptureRenderedFrame(); Dispatcher.UIThread.RunJobs(); wl.CaptureRenderedFrame()?.Save(Path.Combine(pasta, "escritorio_novo_honorario.png")); }
                await hon.Lancamento.LancarCommand.ExecuteAsync(null);
                await Calma(20);
                Assert.False(hon.Lancamento.FichaAberta, string.Join("; ", hon.Lancamento.Erros));
                await hon.CobrancaAvulsaCommand.ExecuteAsync(null);
                Assert.True(hon.Lancamento.RegimeCaixa);                                              // o regime escolhido fica para o próximo lançamento
                (hon.Lancamento.Descricao, hon.Lancamento.Valor, hon.Lancamento.Competencia, hon.Lancamento.Dia) = ("Declaração de IRPF", "350,00", new DateTime(2027, 4, 1), 30);
                await hon.Lancamento.LancarCommand.ExecuteAsync(null);
                await Calma(20);
                Assert.False(hon.Lancamento.FichaAberta, string.Join("; ", hon.Lancamento.Erros));

                // uma segunda cobrança avulsa sem fechar a tela, digitando a competência na janela; o cliente é o do topo e não pode ser trocado
                await hon.CobrancaAvulsaCommand.ExecuteAsync(null);
                await Calma(10);
                Assert.True(hon.Lancamento.FichaAberta);
                var w2 = FichaPopup.Aberta(janela.JanelaDa(hon)!.GetLogicalDescendants().OfType<E.Views.LancamentoCobrancaHost>().Single())!;
                var comboCliente = w2.GetLogicalDescendants().OfType<ComboBox>().Single(c => c.Name == "ClienteCobranca");
                Assert.False(comboCliente.IsEnabled);
                Assert.Equal(novo.Valor, hon.Lancamento.Cliente?.Valor);
                w2.GetLogicalDescendants().OfType<TextBox>().Single(t => t.Name == "DescricaoCobranca").Text = "Alteração contratual";
                w2.GetLogicalDescendants().OfType<TextBox>().Single(t => t.Name == "CompetenciaCobranca").Text = "05/2027";
                w2.GetLogicalDescendants().OfType<TextBox>().Single(t => t.Name == "ValorCobranca").Text = "200,00";
                await Calma(5);
                Assert.Equal(new DateTime(2027, 5, 1), hon.Lancamento.Competencia);
                w2.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "Lancar").Command!.Execute(null);
                await Calma(30);
                Assert.False(hon.Lancamento.FichaAberta, string.Join("; ", hon.Lancamento.Erros));
                Assert.Equal(["Honorário", "Honorário", "Honorário", "Avulsa", "Avulsa"], hon.Cobrancas.Select(c => c.Tipo));
                Assert.Equal("10/05/2027", hon.Cobrancas[4].Vencimento);

                // pagamento à vista: entra já recebida, vencendo na data do pagamento digitada
                await hon.CobrancaAvulsaCommand.ExecuteAsync(null);
                (hon.Lancamento.Cliente, hon.Lancamento.Descricao, hon.Lancamento.Valor) = (hon.Lancamento.Clientes.Single(c => c.Valor == novo.Valor), "Certidão", "80,00");
                hon.Lancamento.CompetenciaTexto = "03/2027";
                hon.Lancamento.AVista = true;
                hon.Lancamento.DataPagamentoTexto = "15/03/2027";
                Assert.False(hon.Lancamento.MostraParcelamento);
                Assert.Equal(["15/03/2027"], hon.Lancamento.Previa.Select(p => p.Vencimento));
                await hon.Lancamento.LancarCommand.ExecuteAsync(null);
                await Calma(20);
                var avista = hon.Cobrancas.Single(c => c.Descricao == "Certidão");
                Assert.Equal(("15/03/2027", "Recebida"), (avista.Vencimento, avista.Situacao));
                Assert.StartsWith("15/03/2027 – 80,00", avista.Recebido);

                // Alterar: vencimento digitado e valor novo
                hon.Selecionada = hon.Cobrancas.Single(c => c.Descricao == "Alteração contratual");
                await hon.AlterarCommand.ExecuteAsync(null);
                Assert.True(hon.Lancamento.FichaAberta);
                Assert.Equal(("Alterar cobrança", "10/05/2027"), (hon.Lancamento.TituloFicha, hon.Lancamento.VencimentoTexto));
                (hon.Lancamento.VencimentoTexto, hon.Lancamento.Valor) = ("20/06/2027", "250,00");
                await hon.Lancamento.LancarCommand.ExecuteAsync(null);
                await Calma(20);
                Assert.False(hon.Lancamento.FichaAberta, string.Join("; ", hon.Lancamento.Erros));
                var alterada = hon.Cobrancas.Single(c => c.Descricao == "Alteração contratual");
                Assert.Equal(("20/06/2027", "250,00"), (alterada.Vencimento, alterada.Valor));

                // Excluir em lote (com confirmação) e lançar de novo nas mesmas condições: sem aviso de honorário já lançado
                hon.Selecionada = hon.Cobrancas.First(c => c.Tipo == "Honorário");
                hon.Marcadas = [.. hon.Cobrancas.Where(c => c.Tipo == "Honorário")];
                var exclusao = hon.ExcluirCommand.ExecuteAsync(null);
                for (var i = 0; i < 50 && vm.Confirmacao is null; i++) await Calma(2);
                Assert.Contains("3 cobranças", vm.Confirmacao!.Mensagem);
                vm.ResponderConfirmacaoCommand.Execute(true);
                await exclusao;
                await Calma(20);
                Assert.DoesNotContain(hon.Cobrancas, c => c.Tipo == "Honorário");
                await hon.NovoHonorarioCommand.ExecuteAsync(null);
                (hon.Lancamento.Cliente, hon.Lancamento.Competencia, hon.Lancamento.Quantidade, hon.Lancamento.RegimeCaixa) =
                    (hon.Lancamento.Clientes.Single(c => c.Valor == novo.Valor), new DateTime(2027, 1, 1), 3, true);
                hon.Lancamento.Valor = "1.250,00";
                await hon.Lancamento.LancarCommand.ExecuteAsync(null);
                await Calma(20);
                Assert.False(hon.Lancamento.FichaAberta, string.Join("; ", hon.Lancamento.Erros));
                Assert.Equal(3, hon.Cobrancas.Count(c => c.Tipo == "Honorário"));

                // Gerador de contratos: o contratante vem do cliente do topo, prazo padrão de 12 meses, e Gerar parcelas lança no Financeiro
                vm.ClienteEmFoco = vm.ClientesNoSeletor.Single(c => c.Id == novo.Valor);
                await Calma(20);
                vm.AbrirCommand.Execute(E.ViewModels.EscritorioTelas.Contratos);
                await Calma(30);
                var ctr = Assert.IsType<E.ViewModels.ContratosViewModel>(vm.Conteudo);
                ctr.NovoCommand.Execute(null);
                await Calma(10);
                Assert.Equal(novo.Valor, ctr.Parte?.Valor);
                Assert.Equal(12m, ctr.PrazoMeses);
                ctr.Inicio = new DateTime(2027, 7, 1);
                Assert.Equal(new DateTime(2028, 6, 30), ctr.Fim);
                (ctr.Valor, ctr.Dia, ctr.RegimeCaixa, ctr.RenovacaoAutomatica) = ("300,00", 31, true, true);
                Assert.False(ctr.RegimeCompetencia);
                var wf = FichaPopup.Aberta(janela.JanelaDa(ctr)!.GetLogicalDescendants().OfType<E.Views.ContratosView>().Single())!;
                Assert.False(wf.GetLogicalDescendants().OfType<ComboBox>().First(c => c.Name == "Contratante").IsEnabled);
                Assert.Equal(["Condições", "Texto do contrato"], wf.GetLogicalDescendants().OfType<TabControl>().First(t => t.Name == "AbasContrato").Items.OfType<TabItem>().Select(t => t.Header as string));
                if (pasta is not null) { wf.CaptureRenderedFrame(); Dispatcher.UIThread.RunJobs(); wf.CaptureRenderedFrame()?.Save(Path.Combine(pasta, "escritorio_contrato_ficha.png")); }
                async Task Confirmar(Task comando, string trecho)
                {
                    for (var i = 0; i < 50 && vm.Confirmacao is null; i++) await Calma(2);
                    Assert.Contains(trecho, vm.Confirmacao!.Mensagem);
                    vm.ResponderConfirmacaoCommand.Execute(true);
                    await comando;
                }
                await Confirmar(ctr.GerarParcelasCommand.ExecuteAsync(null), "12 parcelas");               // grava o contrato e lança as parcelas
                Assert.Empty(ctr.Erros);
                Assert.Contains("12 parcela(s) lançada(s)", ctr.Mensagem);
                Assert.True(ctr.FichaAberta);
                var numero = ctr.Selecionado!.C.Numero;
                Assert.Contains("12 parcela(s) lançada(s), de 07/2027 a 06/2028", ctr.ResumoParcelas);
                await Confirmar(ctr.GerarParcelasCommand.ExecuteAsync(null), "12 parcelas");               // de novo: recusa (use Estender)
                Assert.Contains(ctr.Erros, e => e.Contains("Estender parcelas"));
                await Confirmar(ctr.EstenderParcelasCommand.ExecuteAsync(null), "de 07/2028 a 06/2029");   // estende mais 12 meses
                Assert.Empty(ctr.Erros);
                Assert.Equal(new DateTime(2029, 6, 30), ctr.Fim);
                Assert.Contains("24 parcela(s)", ctr.ResumoParcelas);
                ctr.FichaAberta = false;
                await hon.CarregarDadosCommand.ExecuteAsync(null);
                var doContrato = hon.Cobrancas.Where(c => c.Descricao == $"Contrato nº {numero}").ToList();
                Assert.Equal(18, doContrato.Count);                                                        // vencimentos até 31/12/2028
                Assert.Equal(("07/2027", "31/07/2027", "1/12"), (doContrato[0].Competencia, doContrato[0].Vencimento, doContrato[0].Parcela));
                Assert.Equal(("06/2028", "30/06/2028"), (doContrato[11].Competencia, doContrato[11].Vencimento));
                // o filtro por vencimento: só até 31/12/2027
                hon.VencimentoAte = new DateTime(2027, 12, 31);
                await Calma(20);
                Assert.Equal(6, hon.Cobrancas.Count(c => c.Descricao == $"Contrato nº {numero}"));
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
