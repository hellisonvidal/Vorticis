using Npgsql;
using Perseus.Application.Seguranca;
using Perseus.Domain.Comum;
using Perseus.Domain.Seguranca;
using Perseus.Infra.Postgres;

namespace Perseus.Infra.Tests;

public sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
{
    public DateTimeOffset Agora { get; set; } = agora;
    public override DateTimeOffset GetUtcNow() => Agora;
}

public class PrimeiroAcessoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Cria_o_primeiro_administrador_uma_unica_vez()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var svc = db.App.Usuarios;

        Assert.True(await svc.PrecisaCriarAdministradorAsync(ct));
        Assert.Contains((await svc.CriarPrimeiroAdministradorAsync("admin", "Admin", "curta", "curta", ct)).Erros, e => e.Contains("8 caracteres"));
        Assert.Contains((await svc.CriarPrimeiroAdministradorAsync("admin", "Admin", "Senha1234", "Senha9999", ct)).Erros, e => e.Contains("confirmação"));

        var r = await svc.CriarPrimeiroAdministradorAsync("admin", "Administradora", "Senha1234", "Senha1234", ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        Assert.True(r.Valor.Administrador);
        Assert.False(await svc.PrecisaCriarAdministradorAsync(ct));

        var outro = await svc.CriarPrimeiroAdministradorAsync("intruso", "Intruso", "Senha1234", "Senha1234", ct);
        Assert.Contains(outro.Erros, e => e.Contains("Já existe usuário"));

        var login = await svc.AutenticarAsync("ADMIN", "Senha1234", ct);   // login sem distinção de caixa
        Assert.True(login.Sucesso);
        Assert.Equal("Administradora", login.Valor.Nome);
    }
}

public class UsuariosTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    // Sessão de administrador para os testes (o serviço só verifica a flag).
    private static readonly SessaoUsuario Admin = new(0, "teste", "Teste", true, false);

    private ServicoUsuarios Servico(TimeProvider? relogio = null) =>
        new(new RepositorioUsuarios(NpgsqlDataSource.Create(db.ConnectionString)), relogio);

    private async Task<long> CriarUsuario(ServicoUsuarios svc, string login, bool admin = false)
    {
        var r = await svc.CriarUsuarioAsync(Admin, login, $"Usuário {login}", "Inicial123", admin, TestContext.Current.CancellationToken);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        return r.Valor;
    }

    [Fact]
    public async Task Bloqueia_apos_cinco_tentativas_e_libera_apos_quinze_minutos()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var relogio = new RelogioFixo(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));
        var svc = Servico(relogio);
        await CriarUsuario(svc, "joana");

        for (int i = 1; i <= 4; i++)
            Assert.Equal(["Usuário ou senha inválidos."], (await svc.AutenticarAsync("joana", "errada" + i, ct)).Erros);
        Assert.Contains("bloqueado por 15 minutos", (await svc.AutenticarAsync("joana", "errada5", ct)).Erros[0]);
        Assert.Contains("bloqueado por excesso", (await svc.AutenticarAsync("joana", "Inicial123", ct)).Erros[0]);   // senha certa, mas bloqueado

        relogio.Agora = relogio.Agora.AddMinutes(16);
        var ok = await svc.AutenticarAsync("joana", "Inicial123", ct);
        Assert.True(ok.Sucesso);
        Assert.True(ok.Valor.DeveTrocarSenha);   // usuário criado pelo admin troca a senha no 1º acesso

        Assert.True(await db.EscalarAsync<bool>(
            "SELECT COUNT(*) FILTER (WHERE NOT sucesso) = 6 AND COUNT(*) FILTER (WHERE sucesso) = 1 FROM core.registro_acesso WHERE login = 'joana'"));
    }

    [Fact]
    public async Task Login_inexistente_e_usuario_inativo_recebem_a_mesma_mensagem()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var svc = Servico();
        var id = await CriarUsuario(svc, "pedro");
        Assert.True((await svc.AlterarUsuarioAsync(Admin, id, "Pedro", false, ativo: false, ct)).Sucesso);

        Assert.Equal(["Usuário ou senha inválidos."], (await svc.AutenticarAsync("pedro", "Inicial123", ct)).Erros);
        Assert.Equal(["Usuário ou senha inválidos."], (await svc.AutenticarAsync("ninguem", "Inicial123", ct)).Erros);
    }

    [Fact]
    public async Task Troca_de_senha_exige_a_atual_e_respeita_a_politica()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var svc = Servico();
        await CriarUsuario(svc, "lia");
        var sessao = (await svc.AutenticarAsync("lia", "Inicial123", ct)).Valor;

        Assert.Contains((await svc.AlterarPropriaSenhaAsync(sessao, "errada", "NovaSenha1", "NovaSenha1", ct)).Erros, e => e.Contains("atual incorreta"));
        Assert.Contains((await svc.AlterarPropriaSenhaAsync(sessao, "Inicial123", "somenteletras", "somenteletras", ct)).Erros, e => e.Contains("letras e números"));
        Assert.True((await svc.AlterarPropriaSenhaAsync(sessao, "Inicial123", "NovaSenha1", "NovaSenha1", ct)).Sucesso);

        var depois = await svc.AutenticarAsync("lia", "NovaSenha1", ct);
        Assert.True(depois.Sucesso);
        Assert.False(depois.Valor.DeveTrocarSenha);
    }

    [Fact]
    public async Task Somente_administrador_gerencia_usuarios_e_ultimo_admin_e_protegido()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var svc = Servico();
        var comum = new SessaoUsuario(99, "comum", "Comum", false, false);

        Assert.Contains((await svc.CriarUsuarioAsync(comum, "novo", "Novo", "Inicial123", false, ct)).Erros, e => e.Contains("Somente administradores"));
        Assert.Empty(await svc.ListarAsync(comum, ct));

        var id = await CriarUsuario(svc, "chefe", admin: true);
        var self = new SessaoUsuario(id, "chefe", "Chefe", true, false);
        Assert.Contains((await svc.AlterarUsuarioAsync(self, id, "Chefe", false, true, ct)).Erros, e => e.Contains("próprio acesso"));

        // Regra do banco: não se remove o último administrador ativo.
        var repo = new RepositorioUsuarios(NpgsqlDataSource.Create(db.ConnectionString));
        await using var con = new NpgsqlConnection(db.ConnectionString);
        await con.OpenAsync(ct);
        await using (var cmd = new NpgsqlCommand("UPDATE core.usuario SET administrador = FALSE WHERE id <> @id", con))
        {
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        var ex = await Assert.ThrowsAsync<ViolacaoRegraException>(() => repo.AlterarAsync(id, "Chefe", false, true, ct));
        Assert.Contains("ao menos um administrador", ex.Message);
    }

    [Fact]
    public async Task Usuario_comum_ve_apenas_empresas_com_acesso_e_seu_perfil()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var svc = Servico();
        var empresaA = await db.CriarEmpresaAsync("11444777000161");
        var empresaB = await db.CriarEmpresaAsync("06990590000123");
        var id = await CriarUsuario(svc, "ana");
        var ana = new SessaoUsuario(id, "ana", "Ana", false, false);

        Assert.Empty(await svc.EmpresasPermitidasAsync(ana, ct));
        var assistente = (await db.App.Perfis.ListarAsync(Admin, ct)).Single(p => p.Nome == "Assistente");
        await svc.DefinirAcessoAsync(Admin, id, empresaA, assistente.Id, ct);
        var permitidas = await svc.EmpresasPermitidasAsync(ana, ct);
        Assert.Equal(empresaA, Assert.Single(permitidas).Id);
        Assert.Equal("Assistente", permitidas[0].Perfil);
        var efetivas = await db.App.Autorizador.ObterAsync(id, empresaA, ct);
        Assert.True(efetivas.Pode(Permissao.LancamentoIncluir));
        Assert.False(efetivas.Pode(Permissao.PeriodoFechar));
        Assert.False((await db.App.Autorizador.ObterAsync(id, empresaB, ct)).TemAcesso);

        await svc.DefinirAcessoAsync(Admin, id, empresaA, null, ct);   // revoga
        Assert.Empty(await svc.EmpresasPermitidasAsync(ana, ct));
        Assert.Contains((await svc.EmpresasPermitidasAsync(Admin, ct)).Select(e => e.Id), e => e == empresaB);
    }
}
