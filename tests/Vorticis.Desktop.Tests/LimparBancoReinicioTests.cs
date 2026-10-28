using Avalonia.Threading;
using Vorticis.Desktop.Comum;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Infra.Tests;

namespace Vorticis.Desktop.Tests;

/// <summary>
/// Configurações gerais › Limpar banco: ao concluir, o programa volta ao primeiro acesso (usuário vorticis/vorticis → cadastro do administrador → perfis)
/// sem reiniciar: telas fechadas, sessão, empresa ativa, logo e login salvo esquecidos.
/// </summary>
public class LimparBancoReinicioTests(BancoDeTeste db, SessaoHeadless headless) : IClassFixture<BancoDeTeste>
{
    private static async Task<T> ForaDaSessao<T>(Task<T> tarefa)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = tarefa.ContinueWith(t => { if (t.IsFaulted) tcs.SetException(t.Exception!.InnerExceptions); else tcs.SetResult(t.Result); }, TaskScheduler.Default);
        return await tcs.Task;
    }

    private static async Task Esperar(CancellationToken ct, int ciclos = 25)
    {
        for (var i = 0; i < ciclos; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
    }

    [Fact]
    public async Task Limpar_o_banco_volta_ao_primeiro_acesso_sem_reiniciar_o_programa()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var empresa = await db.CriarEmpresaAsync("27183946000359");
        await db.App.Perfis.ConcluirConfiguracaoInicialAsync(new Vorticis.Domain.Seguranca.SessaoUsuario(adm, "admin_teste", "Admin", true, false), ct);
        Assert.True((await db.App.Empresas.SalvarLogoAsync(empresa, new Vorticis.Application.Cadastros.LogoEmpresa(Infra.Tests.LogoEmpresaTests.PngSolido(40, 20, 1, 2, 3), true), ct)).Sucesso);
        // só mexe no login salvo do computador se não houver um (não apaga o de quem roda os testes)
        var salvouLogin = !File.Exists(LoginSalvo.Arquivo);
        if (salvouLogin) LoginSalvo.Salvar("admin_teste", "Senha1234");

        try
        {
            var ok = await ForaDaSessao(headless.Sessao.Dispatch(async () =>
            {
                var vm = new Vorticis.Desktop.ViewModels.MainViewModel();
                await vm.InicializarAsync(db.ConnectionString);
                var login = Assert.IsType<LoginViewModel>(vm.Conteudo);
                (login.Usuario, login.Senha) = ("admin_teste", "Senha1234");
                await login.EntrarCommand.ExecuteAsync(null);
                for (var i = 0; i < 200 && !vm.Logado; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
                Assert.True(vm.Logado);
                vm.EmpresaSelecionada = vm.EmpresasPermitidas.Single(e => e.Id == empresa);
                await Esperar(ct, 40);
                Assert.NotNull(Vorticis.Application.Relatorios.LogoRelatorios.Atual);   // logo da empresa ativa

                // telas abertas (a de configurações gerais é a que executa a limpeza)
                vm.AbrirCommand.Execute(TelasComuns.Empresas);
                vm.AbrirCommand.Execute(TelasComuns.Parametros);
                vm.AbrirCommand.Execute(TelasComuns.ConfiguracoesGerais);
                await Esperar(ct);
                Assert.True(vm.JanelasAbertas.Count >= 3);

                var cfg = vm.ConfiguracoesGerais;
                cfg.ConfirmacaoLimpeza = "LIMPAR";
                var execucao = cfg.LimparBancoCommand.ExecuteAsync(null);
                for (var i = 0; i < 1500 && !execucao.IsCompleted; i++)
                {
                    if (vm.Confirmacao is { } pedido)
                    {
                        if (pedido.PedeSenha) pedido.Senha = "VORTICIS";
                        vm.ResponderConfirmacaoCommand.Execute(true);   // senha do administrador; "limpar sem backup", se o backup não puder ser gerado
                    }
                    await Task.Delay(20, ct);
                    Dispatcher.UIThread.RunJobs();
                }
                await execucao;
                await Esperar(ct, 40);

                // volta à tela de entrada da instalação nova
                Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM core.empresa"));
                Assert.False(vm.Logado);
                Assert.Null(vm.Sessao);
                Assert.Null(vm.EmpresaSelecionada);
                Assert.Empty(vm.EmpresasPermitidas);
                Assert.Empty(vm.JanelasAbertas);
                Assert.Null(vm.FuncaoAtiva);
                Assert.Null(Vorticis.Application.Relatorios.LogoRelatorios.Atual);
                var tela = Assert.IsType<LoginViewModel>(vm.Conteudo);
                Assert.True(tela.ImplantacaoAtiva);
                Assert.Equal("", tela.Usuario);
                Assert.Equal("", tela.Senha);
                Assert.False(tela.SalvarLogin);
                Assert.Contains("vorticis", tela.Mensagem);
                if (salvouLogin) Assert.False(File.Exists(LoginSalvo.Arquivo));   // login salvo esquecido

                // e o recomeço funciona: vorticis/vorticis entra na implantação para cadastrar o administrador
                (tela.Usuario, tela.Senha) = ("vorticis", "vorticis");
                await tela.EntrarCommand.ExecuteAsync(null);
                for (var i = 0; i < 200 && !vm.Logado; i++) { await Task.Delay(20, ct); Dispatcher.UIThread.RunJobs(); }
                Assert.True(vm.Logado);
                Assert.True(vm.Sessao!.Implantacao);
                Assert.Null(vm.EmpresaSelecionada);
                await Esperar(ct);
                Assert.Single(vm.JanelasAbertas);   // só o cadastro de usuários da implantação
                return true;
            }, ct));
            Assert.True(ok);
        }
        finally
        {
            if (salvouLogin) LoginSalvo.Esquecer();
        }
    }
}
