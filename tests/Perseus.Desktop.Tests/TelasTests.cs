using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Perseus.Application.Cadastros;
using Perseus.Domain.Seguranca;
using Perseus.Desktop.ViewModels;
using Perseus.Desktop.Views;
using Perseus.Infra.Cnpj;
using Perseus.Infra.Tests;

[assembly: AssemblyFixture(typeof(Perseus.Desktop.Tests.SessaoHeadless))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Perseus.Desktop.Tests;

public static class AppDeTeste
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>
/// O Avalonia admite uma única aplicação por processo: todas as classes de teste usam a mesma sessão headless
/// (fixture do assembly) e rodam em sequência.
/// </summary>
public sealed class SessaoHeadless : IDisposable
{
    public HeadlessUnitTestSession Sessao { get; } = HeadlessUnitTestSession.StartNew(typeof(AppDeTeste));

    public void Dispose() => Sessao.Dispose();
}

/// <summary>
/// Fluxo completo pela camada de apresentação (ViewModels + janela real renderizada sem tela).
/// Se PERSEUS_SCREENSHOTS apontar para uma pasta, salva PNGs das telas.
/// </summary>
public class TelasTests(BancoDeTeste db, SessaoHeadless headless) : IClassFixture<BancoDeTeste>
{
    private static readonly string? Pasta = Environment.GetEnvironmentVariable("PERSEUS_SCREENSHOTS");

    private static void Foto(Window janela, string nome)
    {
        // Alguns controles (ex.: CalendarDatePicker) atualizam o texto em uma passada posterior do dispatcher.
        Dispatcher.UIThread.RunJobs();
        janela.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
        var quadro = janela.CaptureRenderedFrame();
        Assert.NotNull(quadro);
        if (Pasta is not null) quadro.Save(Path.Combine(Pasta, nome + ".png"));
    }

    /// <summary>
    /// Garante que o restante do teste (inclusive o Dispose da sessão) não rode na thread da sessão headless:
    /// se uma asserção falhar lá dentro, a continuação síncrona chamaria Dispose na própria thread e travaria,
    /// escondendo a falha real.
    /// </summary>
    private static async Task<T> ForaDaSessao<T>(Task<T> tarefa)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = tarefa.ContinueWith(t =>
        {
            if (t.IsFaulted) tcs.SetException(t.Exception!.InnerExceptions);
            else if (t.IsCanceled) tcs.SetCanceled();
            else tcs.SetResult(t.Result);
        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        return await tcs.Task;
    }

    [Fact]
    public async Task Fluxo_completo_com_perfil_personalizado_definido_pelo_administrador()
    {
        BancoDeTeste.ExigirBanco();
        var sessao = headless.Sessao;
        // Retornar um valor força a sobrecarga Dispatch<T>(Func<Task<T>>), que aguarda o corpo assíncrono;
        // com Func<Task> a sobrecarga escolhida devolveria Task<Task> e as asserções não seriam aguardadas.
        var concluido = await ForaDaSessao(sessao.Dispatch(async () =>
        {
            // Consulta de CNPJ com o parser real e resposta HTTP simulada (os testes não acessam a internet).
            var http = new HttpSimulado(_ => HttpSimulado.Json(ConsultaCnpjTests.RespostaExemplo));
            var vm = new MainViewModel();
            await vm.InicializarAsync(db.ConnectionString, new ConsultaCnpjPublica(new HttpClient(http)));
            var janela = new MainWindow { DataContext = vm, Width = 1280, Height = 780 };
            janela.Show();

            // 1. Primeiro acesso: cria o administrador
            var login = Assert.IsType<LoginViewModel>(vm.Conteudo);
            Assert.True(login.EmPrimeiroAcesso);
            (login.Nome, login.Usuario, login.Senha, login.Confirmacao) = ("Maria Contadora", "maria", "Senha1234", "Senha1234");
            Foto(janela, "01_primeiro_acesso");
            await login.CriarAdministradorCommand.ExecuteAsync(null);
            Assert.True(vm.Logado, string.Join("; ", login.Erros));
            Assert.Equal(WindowState.Maximized, janela.WindowState);   // após o login a janela maximiza

            // 1b. Configuração inicial obrigatória: perfis de acesso (o cadastro de empresas não é exigido)
            Assert.Same(vm.Perfis, vm.Conteudo);
            Assert.True(vm.Perfis.ConfiguracaoInicial);
            foreach (var bloqueada in new[] { Telas.Empresas, Telas.Usuarios, Telas.Lancamento, Telas.Inicio, Telas.Backup })
                Assert.False(vm.AbrirCommand.CanExecute(bloqueada), bloqueada);
            await vm.Perfis.CarregarAsync();
            Foto(janela, "01b_configuracao_inicial_perfis");
            await vm.Perfis.ConcluirConfiguracaoInicialCommand.ExecuteAsync(null);
            Assert.Empty(vm.Perfis.Erros);
            Assert.False(vm.Perfis.ConfiguracaoInicial);

            // 1c. Tela de descanso, sem obrigar o cadastro de empresa
            Assert.Same(vm.Inicio, vm.Conteudo);
            Assert.Contains("Cadastros › Empresas", vm.Inicio.Dica);
            Foto(janela, "01c_tela_inicial");
            Assert.False(vm.AbrirCommand.CanExecute(Telas.Lancamento));
            Assert.True(vm.AbrirCommand.CanExecute(Telas.Empresas));
            vm.AbrirCommand.Execute(Telas.Empresas);

            // 2. Cadastros › Empresas: busca na base pública do CNPJ e cadastra
            var emp = vm.Empresas;
            emp.Cnpj = "11.222.333/0001-81";
            await emp.ConsultarCnpjCommand.ExecuteAsync(null);
            Assert.Empty(emp.Erros);
            Assert.Equal("EXEMPLO COMERCIO DE INFORMATICA LTDA", emp.RazaoSocial);
            Assert.Equal(("4751201", "3550308", "AVENIDA PAULISTA"), (emp.Cnae, emp.MunicipioIbge, emp.Logradouro));
            Assert.Equal(RegimeTributario.SimplesNacional, emp.Regime);
            Assert.Equal(new DateTime(2015, 3, 2), emp.InicioAtividades);
            emp.AnoExercicio = 2026;
            Foto(janela, "02_empresa_dados_receita");
            var picker = janela.GetVisualDescendants().OfType<CalendarDatePicker>().Single();
            Assert.Equal((new DateTime(2015, 3, 2), (DateTime?)new DateTime(2015, 3, 2), "02/03/2015"), (emp.InicioAtividades!.Value, picker.SelectedDate, picker.Text));
            await emp.CadastrarCommand.ExecuteAsync(null);
            Assert.Empty(emp.Erros);
            var empresa = Assert.Single(vm.EmpresasPermitidas);
            Assert.True(await db.EscalarAsync<bool>(
                """
                SELECT fonte_dados LIKE 'BrasilAPI%' AND cep = '01310100' AND situacao_cadastral = 'ATIVA'
                       AND data_inicio_ativ = '2015-03-02' AND cnae_principal = '4751201'
                  FROM core.empresa WHERE id = @id
                """,
                ("id", empresa.Id)));

            // 3. Lançamentos › Lançamento contábil
            Assert.True(vm.AbrirCommand.CanExecute(Telas.Lancamento));
            vm.AbrirCommand.Execute(Telas.Lancamento);
            var l = vm.Lancamento;
            await l.AoTrocarEmpresaAsync();
            l.Data = new DateTime(2026, 3, 10);
            (l.Linhas[0].Conta, l.Linhas[0].Valor, l.Linhas[0].Historico) = ("1.1.01.002", "1.500,00", "Venda à vista NF 123");
            (l.Linhas[1].Conta, l.Linhas[1].Valor, l.Linhas[1].Historico) = ("3.1.01.001", "1500.00", "Venda à vista NF 123");
            await l.EfetivarCommand.ExecuteAsync(null);
            Assert.Equal("Lançamento nº 1 efetivado.", l.Mensagem);
            (l.Linhas[0].Conta, l.Linhas[0].Valor, l.Linhas[0].Historico) = ("4.2.02.001", "2.300,00", "Aluguel de março");
            (l.Linhas[1].Conta, l.Linhas[1].Valor) = ("1.1.01.002", "2.000,00");
            Foto(janela, "03_lancamento");

            // Menu aberto (submenu Lançamentos)
            var menuLancamentos = janela.GetVisualDescendants().OfType<MenuItem>().First(m => (m.Header as string) == "_Lançamentos");
            menuLancamentos.IsSubMenuOpen = true;
            Foto(janela, "04_menu_lancamentos");
            menuLancamentos.IsSubMenuOpen = false;

            // 4. Relatórios
            vm.AbrirCommand.Execute(Telas.Balancete);
            (vm.Balancete.Inicio, vm.Balancete.Fim) = (new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));
            await vm.Balancete.GerarCommand.ExecuteAsync(null);
            Assert.Contains("✓ fechado", vm.Balancete.Totais);
            Foto(janela, "05_balancete");

            // 5. Cadastros › Plano de contas: inclui subconta, herdando natureza
            vm.AbrirCommand.Execute(Telas.PlanoContas);
            var plano = vm.PlanoContas;
            await plano.CarregarAsync();
            plano.Selecionada = plano.Lista.Single(c => c.Conta.Codigo == "1.1.01.003");
            Assert.Equal("1.1.01.004", plano.NovoCodigo);            // sugestão da próxima irmã
            plano.NovaDescricao = "Caixa da filial";
            await plano.IncluirCommand.ExecuteAsync(null);
            Assert.Empty(plano.Erros);
            Assert.Contains(plano.Lista, c => c.Conta.Codigo == "1.1.01.004" && c.Conta.Analitica);
            Foto(janela, "06_plano_contas");

            // Centros de custo
            vm.AbrirCommand.Execute(Telas.CentrosCusto);
            (vm.CentrosCusto.NovoCodigo, vm.CentrosCusto.NovaDescricao) = ("ADM", "Administrativo");
            await vm.CentrosCusto.IncluirCommand.ExecuteAsync(null);
            Assert.Empty(vm.CentrosCusto.Erros);
            Assert.Single(vm.CentrosCusto.Lista);

            // Saldos iniciais
            vm.AbrirCommand.Execute(Telas.SaldosIniciais);
            var saldos = vm.SaldosIniciais;
            saldos.Ano = 2026;
            await saldos.CarregarAsync();
            saldos.Linhas.Single(l => l.Codigo == "1.1.01.001").Valor = "5.000,00";
            saldos.Linhas.Single(l => l.Codigo == "1.2.03.003").Valor = "40.000,00";
            saldos.Linhas.Single(l => l.Codigo == "2.3.01.001").Valor = "45.000,00";
            Assert.True(saldos.Balanceado);
            saldos.SomentePreenchidas = true;
            Foto(janela, "07_saldos_iniciais");
            await saldos.LancarCommand.ExecuteAsync(null);
            Assert.Empty(saldos.Erros);
            Assert.Contains("lançamento nº 2", saldos.Mensagem);

            // Relatórios › Balanço Patrimonial e DRE (antes do encerramento: resultado destacado no PL)
            vm.AbrirCommand.Execute(Telas.Demonstracoes);
            var dem = vm.Demonstracoes;
            (dem.Inicio, dem.Fim) = (new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
            await dem.GerarCommand.ExecuteAsync(null);
            Assert.Empty(dem.Erros);
            Assert.StartsWith("✓", dem.Resumo);
            Assert.Contains(dem.Dre, l => l.Descricao == "LUCRO LÍQUIDO DO PERÍODO" && l.Valor == "1.500,00");
            Foto(janela, "08a_demonstracoes");

            // Lançamentos › Encerramento do exercício
            vm.AbrirCommand.Execute(Telas.Encerramento);
            var enc = vm.Encerramento;
            enc.Ano = 2026;
            await enc.PreverCommand.ExecuteAsync(null);
            Assert.Contains("LUCRO DO EXERCÍCIO: 1.500,00", enc.Previa);
            await enc.EncerrarCommand.ExecuteAsync(null);
            Assert.Empty(enc.Erros);
            Assert.True(enc.Encerrado);
            Foto(janela, "08b_encerramento");

            // Cadastros › Empresas, aba Sócios: os do QSA vieram no cadastro da empresa; completa CPF e indica o representante legal
            vm.AbrirCommand.Execute(Telas.Empresas);
            Assert.True(vm.Empresas.TemEmpresaSelecionada);
            vm.Empresas.AbaSelecionada = CadastroEmpresaViewModel.AbaSocios;
            var socios = vm.Socios;
            await socios.CarregarAsync();
            Assert.Equal(["BELTRANA DA SILVA", "FULANO DE TAL"], socios.Lista.Select(s => s.Nome).Order());
            socios.Selecionado = socios.Lista.Single(s => s.Nome == "FULANO DE TAL");
            Assert.Equal("***123456**", socios.DocumentoMascarado);
            (socios.Documento, socios.Participacao, socios.Representante) = ("390.533.447-05", "70", true);
            await socios.SalvarCommand.ExecuteAsync(null);
            Assert.Empty(socios.Erros);
            Foto(janela, "08g_socios");

            // Cadastros › Empresas, aba Contadores (o mesmo cadastro de Cadastros › Contadores)
            vm.Empresas.AbaSelecionada = CadastroEmpresaViewModel.AbaContadores;
            var cont = vm.Contabilidade;
            await cont.CarregarAsync();
            (cont.ContadorNome, cont.ContadorCpf, cont.ContadorCrc, cont.ContadorUf) = ("Maria Contadora", "529.982.247-25", "1SP123456/O-7", "SP");
            await cont.VincularContadorCommand.ExecuteAsync(null);
            Assert.Empty(cont.Erros);
            (cont.EscritorioCnpj, cont.EscritorioRazao, cont.EscritorioCrc) = ("45.723.174/0001-10", "Contábil Exemplo Ltda", "2SP012345/O-1");
            await cont.VincularEscritorioCommand.ExecuteAsync(null);
            Assert.Empty(cont.Erros);
            Assert.Equal(2, cont.Vinculos.Count);
            Foto(janela, "08h_contabilidade");
            vm.AbrirCommand.Execute(Telas.Contabilidade);
            Assert.Same(cont, vm.Conteudo);
            Assert.Equal("Cadastros › Contadores", vm.TituloTela);
            Assert.Equal(2, cont.Vinculos.Count);

            // Relatórios › Livros e relatórios em PDF
            var pastaPdf = Path.Combine(Path.GetTempPath(), "perseus_pdf_" + Guid.NewGuid().ToString("N"));
            (ArquivosGerados.AbrirAposGerar, ArquivosGerados.Pasta) = (false, pastaPdf);
            vm.AbrirCommand.Execute(Telas.LivrosPdf);
            var livros = vm.LivrosPdf;
            await livros.PreencherResponsaveisAsync();
            // termos preenchidos a partir dos cadastros de sócios e contadores
            Assert.Equal(("FULANO DE TAL", "390.533.447-05", "Maria Contadora", "1SP123456/O-7"),
                         (livros.Representante, livros.RepresentanteCpf, livros.Contador, livros.Crc));
            (livros.Inicio, livros.Fim) = (new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
            await livros.DiarioCommand.ExecuteAsync(null);
            Assert.Empty(livros.Erros);
            Assert.True(File.Exists(livros.UltimoArquivo));
            await livros.DemonstracoesCommand.ExecuteAsync(null);
            Assert.Empty(livros.Erros);
            Foto(janela, "08c_livros_pdf");
            if (Pasta is not null) foreach (var pdf in Directory.GetFiles(pastaPdf)) File.Copy(pdf, Path.Combine(Pasta, Path.GetFileName(pdf)), true);
            Directory.Delete(pastaPdf, true);

            // Configurações › Plano referencial da RFB (tabela FICTÍCIA de teste) e Cadastros › Vínculo ao plano referencial
            var arquivoRef = Path.Combine(Path.GetTempPath(), "referencial_ficticio_" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(arquivoRef, PlanoReferencialTests.TabelaFicticia, System.Text.Encoding.Latin1);
            vm.AbrirCommand.Execute(Telas.ImportacaoReferencial);
            var imp = vm.ImportacaoReferencial;
            (imp.Ano, imp.Arquivo) = (2026, arquivoRef);
            await imp.ImportarCommand.ExecuteAsync(null);
            Assert.Empty(imp.Erros);
            Assert.Single(imp.Tabelas);
            Foto(janela, "08d_importacao_referencial");
            File.Delete(arquivoRef);

            vm.AbrirCommand.Execute(Telas.MapeamentoReferencial);
            var map = vm.MapeamentoReferencial;
            map.Ano = 2026;
            await map.CarregarAsync();
            await map.UsarPlanoCommand.ExecuteAsync(null);
            Assert.Empty(map.Erros);
            Assert.Contains("7 conta(s) com movimento sem vínculo", map.Resumo);
            map.SomentePendentes = true;
            await map.SugerirCommand.ExecuteAsync(null);
            Assert.Contains(map.Linhas, l => l.Codigo == "1.1.01.001" && l.SugestaoTexto.StartsWith("1.01.01"));
            Foto(janela, "08e_vinculo_sugestoes");
            await map.AceitarSugestoesCommand.ExecuteAsync(null);
            Assert.Empty(map.Erros);
            map.SomentePendentes = false;
            map.Selecionada = map.Linhas.First(l => l.Pendente);
            map.FiltroReferencial = "";
            map.ReferencialEscolhida = map.Opcoes.First();
            await map.VincularCommand.ExecuteAsync(null);
            Assert.Empty(map.Erros);
            Foto(janela, "08f_vinculo");

            // 6. Configurações › Perfis de acesso: perfil personalizado só com Razão e Diário
            vm.AbrirCommand.Execute(Telas.Perfis);
            var perfis = vm.Perfis;
            await perfis.CarregarAsync();
            Assert.Equal(["Assistente", "Consulta", "Contador"], perfis.Lista.Select(p => p.Nome).Order());
            perfis.NovoCommand.Execute(null);
            (perfis.Nome, perfis.Descricao) = ("Auditor externo", "Consulta Razão e Diário");
            foreach (var f in perfis.Grupos.SelectMany(g => g.Funcoes))
                f.Marcada = f.Definicao.Permissao is Permissao.RelatorioRazao or Permissao.RelatorioDiario;
            await perfis.SalvarCommand.ExecuteAsync(null);
            Assert.Empty(perfis.Erros);
            Foto(janela, "08_perfis");

            // Usuários: cria João e atribui o perfil "Auditor externo" na empresa
            vm.AbrirCommand.Execute(Telas.Usuarios);
            var u = vm.Usuarios;
            await u.CarregarAsync();
            (u.NovoLogin, u.NovoNome, u.NovaSenha) = ("joao", "João Auditor", "Inicial123");
            await u.CriarCommand.ExecuteAsync(null);
            Assert.Empty(u.Erros);
            for (int i = 0; i < 50 && u.Acessos.Count == 0; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); }
            var acesso = Assert.Single(u.Acessos);
            acesso.Perfil = acesso.Opcoes.Single(o => o.Nome == "Auditor externo");
            await u.SalvarCommand.ExecuteAsync(null);
            Assert.Equal("Alterações salvas.", u.Mensagem);
            Foto(janela, "09_usuarios");

            // Backup
            vm.AbrirCommand.Execute(Telas.Backup);
            var pastaBackup = Path.Combine(Path.GetTempPath(), "perseus_ui_" + Guid.NewGuid().ToString("N"));
            vm.Backup.Pasta = pastaBackup;
            await vm.Backup.GerarCommand.ExecuteAsync(null);
            Assert.Empty(vm.Backup.Erros);
            Assert.Single(vm.Backup.Arquivos);
            Foto(janela, "10_backup");
            Directory.Delete(pastaBackup, true);

            vm.AbrirCommand.Execute(Telas.Sobre);
            Foto(janela, "11_sobre");

            // 7. Sai e entra como João: troca obrigatória de senha e perfil personalizado
            vm.SairCommand.Execute(null);
            Assert.False(vm.Logado);
            login = Assert.IsType<LoginViewModel>(vm.Conteudo);
            (login.Usuario, login.Senha) = ("joao", "errada123");
            await login.EntrarCommand.ExecuteAsync(null);
            Assert.Equal(["Usuário ou senha inválidos."], login.Erros);
            Foto(janela, "12_login_erro");

            login.Senha = "Inicial123";
            await login.EntrarCommand.ExecuteAsync(null);
            Assert.True(login.EmTrocaObrigatoria);
            (login.Senha, login.Confirmacao) = ("NovaSenha9", "NovaSenha9");
            await login.TrocarSenhaCommand.ExecuteAsync(null);
            Assert.True(vm.Logado, string.Join("; ", login.Erros));

            // Menu reflete exatamente o perfil
            Assert.True(vm.AbrirCommand.CanExecute(Telas.Razao));
            Assert.True(vm.AbrirCommand.CanExecute(Telas.Diario));
            Assert.True(vm.AbrirCommand.CanExecute(Telas.PlanoContas));     // consulta do plano
            foreach (var proibida in new[] { Telas.Balancete, Telas.Lancamento, Telas.Estorno, Telas.SaldosIniciais, Telas.Periodos,
                                             Telas.Empresas, Telas.Usuarios, Telas.Perfis, Telas.Backup, Telas.Conexao })
                Assert.False(vm.AbrirCommand.CanExecute(proibida), proibida);
            Assert.Same(vm.Inicio, vm.Conteudo);                             // entra pela tela de descanso
            Assert.Null(vm.Inicio.Dica);
            vm.AbrirCommand.Execute(Telas.Razao);
            Assert.Equal("", vm.Balancete.Totais);                           // nada da sessão anterior fica visível

            vm.Razao.Conta = "1.1.01.002";
            (vm.Razao.Inicio, vm.Razao.Fim) = (new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
            await vm.Razao.GerarCommand.ExecuteAsync(null);
            Assert.Single(vm.Razao.Linhas);

            vm.AbrirCommand.Execute(Telas.PlanoContas);
            await vm.PlanoContas.CarregarAsync();
            Assert.False(vm.PlanoContas.PodeEditar);
            vm.PlanoContas.NovoCodigo = "1.1.01.005";
            vm.PlanoContas.NovaDescricao = "Tentativa";
            await vm.PlanoContas.IncluirCommand.ExecuteAsync(null);           // mesmo forçando, o serviço recusa
            Assert.Contains(vm.PlanoContas.Erros, e => e.Contains("não permite"));
            Foto(janela, "13_perfil_restrito");

            janela.Close();
            await vm.DisposeAsync();
            return true;
        }, TestContext.Current.CancellationToken));
        Assert.True(concluido);
    }
}
