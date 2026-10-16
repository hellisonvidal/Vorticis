using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Perseus.Application.Cadastros;
using Perseus.Desktop.ViewModels;
using Perseus.Desktop.Views;
using Perseus.Infra.Cnpj;
using Perseus.Infra.Tests;

namespace Perseus.Desktop.Tests;

public static class AppDeTeste
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>
/// Fluxo completo pela camada de apresentação (ViewModels + janela real renderizada sem tela).
/// Se PERSEUS_SCREENSHOTS apontar para uma pasta, salva PNGs das telas.
/// </summary>
public class TelasTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
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

    [Fact]
    public async Task Primeiro_acesso_cadastro_por_cnpj_lancamento_usuarios_e_permissoes_pelo_menu()
    {
        BancoDeTeste.ExigirBanco();
        using var sessao = HeadlessUnitTestSession.StartNew(typeof(AppDeTeste));
        // Retornar um valor força a sobrecarga Dispatch<T>(Func<Task<T>>), que aguarda o corpo assíncrono;
        // com Func<Task> a sobrecarga escolhida devolveria Task<Task> e as asserções não seriam aguardadas.
        var concluido = await sessao.Dispatch(async () =>
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
            Assert.Same(vm.Empresas, vm.Conteudo);              // sem empresas: abre o cadastro
            Assert.False(vm.AbrirCommand.CanExecute(Telas.Lancamento));
            Assert.True(vm.AbrirCommand.CanExecute(Telas.Empresas));

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

            // 5. Configurações › Usuários: cria usuário com perfil Consulta na empresa
            vm.AbrirCommand.Execute(Telas.Usuarios);
            var u = vm.Usuarios;
            await u.CarregarAsync();
            (u.NovoLogin, u.NovoNome, u.NovaSenha) = ("joao", "João Auditor", "Inicial123");
            await u.CriarCommand.ExecuteAsync(null);
            Assert.Empty(u.Erros);
            Assert.Equal("joao", u.Selecionado?.Login);
            for (int i = 0; i < 50 && u.Acessos.Count == 0; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); }
            Assert.Single(u.Acessos).Perfil = "Consulta";
            await u.SalvarCommand.ExecuteAsync(null);
            Assert.Equal("Alterações salvas.", u.Mensagem);
            Foto(janela, "06_usuarios");

            vm.AbrirCommand.Execute(Telas.Sobre);
            Foto(janela, "07_sobre");

            // 6. Sai e entra como o novo usuário: troca obrigatória de senha e perfil Consulta
            vm.SairCommand.Execute(null);
            Assert.False(vm.Logado);
            login = Assert.IsType<LoginViewModel>(vm.Conteudo);
            Assert.True(login.EmEntrar);
            (login.Usuario, login.Senha) = ("joao", "errada123");
            await login.EntrarCommand.ExecuteAsync(null);
            Assert.Equal(["Usuário ou senha inválidos."], login.Erros);
            Foto(janela, "08_login_erro");

            login.Senha = "Inicial123";
            await login.EntrarCommand.ExecuteAsync(null);
            Assert.True(login.EmTrocaObrigatoria);
            (login.Senha, login.Confirmacao) = ("NovaSenha9", "NovaSenha9");
            await login.TrocarSenhaCommand.ExecuteAsync(null);
            Assert.True(vm.Logado, string.Join("; ", login.Erros));

            Assert.False(vm.AbrirCommand.CanExecute(Telas.Lancamento));
            Assert.False(vm.AbrirCommand.CanExecute(Telas.Empresas));
            Assert.False(vm.AbrirCommand.CanExecute(Telas.Usuarios));
            Assert.True(vm.AbrirCommand.CanExecute(Telas.Balancete));
            Assert.Same(vm.Balancete, vm.Conteudo);
            Assert.Equal("", vm.Balancete.Totais);                 // nada da sessão anterior fica visível
            Assert.Empty(vm.Balancete.Linhas);
            await vm.Periodos.CarregarAsync();
            vm.Periodos.Selecionado = vm.Periodos.Periodos[0];
            await vm.Periodos.FecharCommand.ExecuteAsync(null);   // perfil Consulta não fecha período
            Assert.Contains(vm.Periodos.Erros, e => e.Contains("perfil"));
            Foto(janela, "09_perfil_consulta");

            janela.Close();
            await vm.DisposeAsync();
            return true;
        }, TestContext.Current.CancellationToken);
        Assert.True(concluido);
    }
}
