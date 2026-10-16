using Perseus.Application.Cadastros;
using Perseus.Domain.Seguranca;

namespace Perseus.Infra.Tests;

public class EmpresasTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private static readonly SessaoUsuario Admin = new(0, "teste", "Teste", true, false);

    [Fact]
    public async Task Altera_cadastro_com_inscricao_estadual_e_exclui_mantendo_a_escrituracao()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var svc = db.App.Empresas;
        var id = await db.CriarEmpresaAsync("11444777000161");

        var atual = (await svc.ObterAsync(id, ct))!;
        Assert.Equal(RegimeTributario.LucroPresumido, atual.Regime);
        Assert.Null(atual.InscricaoEstadual);

        // município de outra UF é recusado
        var errada = new AlteracaoEmpresa(id, "Empresa Alterada LTDA", "Fantasia", "2062", "4751201", "SP", "3304557",
                                          new DateOnly(2020, 1, 1), "110.042.490.114");
        Assert.Contains("é de RJ", (await svc.AlterarAsync(errada, ct)).Erros[0]);
        Assert.Contains("2 a 14 dígitos", (await svc.AlterarAsync(errada with { CodMunicipioIbge = "3550308", InscricaoEstadual = "1" }, ct)).Erros[0]);

        var r = await svc.AlterarAsync(errada with { CodMunicipioIbge = "3550308", Complemento = new ComplementoEmpresa(Telefone: "1144445555") }, ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        var depois = (await svc.ObterAsync(id, ct))!;
        Assert.Equal(("Empresa Alterada LTDA", "110042490114", "1144445555"), (depois.RazaoSocial, depois.InscricaoEstadual, depois.Telefone));
        Assert.Equal("110042490114", await db.EscalarAsync<string>(
            "SELECT inscricao_estadual FROM core.estabelecimento WHERE empresa_id = @id AND matriz", ("id", id)));

        Assert.True((await svc.SalvarInscricaoEstadualAsync(id, "isento", ct)).Sucesso);
        Assert.Equal("ISENTO", (await svc.ObterAsync(id, ct))!.InscricaoEstadual);

        // Excluir: some da lista e do acesso, mas os dados ficam (guarda da escrituração)
        Assert.True((await svc.ExcluirAsync(id, ct)).Sucesso);
        Assert.Null(await svc.ObterAsync(id, ct));
        Assert.DoesNotContain(await db.App.Usuarios.EmpresasPermitidasAsync(Admin, ct), e => e.Id == id);
        Assert.Equal(1L, await db.EscalarAsync<long>("SELECT count(*) FROM core.empresa WHERE id = @id AND NOT ativa", ("id", id)));
        Assert.True(await db.EscalarAsync<long>("SELECT count(*) FROM contabil.plano_contas WHERE empresa_id = @id", ("id", id)) > 0);
    }

    [Fact]
    public async Task Usuario_sem_historico_e_excluido_e_com_historico_e_inativado()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var usuarios = db.App.Usuarios;
        var consulta = await db.PerfilIdAsync("Consulta");
        var novo = (await usuarios.CriarUsuarioAsync(Admin, "nunca_entrou", "Nunca Entrou", "Inicial123", consulta, ct)).Valor;
        var antigo = (await usuarios.CriarUsuarioAsync(Admin, "ja_entrou", "Já Entrou", "Inicial123", consulta, ct)).Valor;
        Assert.True((await usuarios.AutenticarAsync("ja_entrou", "Inicial123", ct)).Sucesso);   // gera registro de acesso

        Assert.True((await usuarios.ExcluirUsuarioAsync(Admin, novo, ct)).Valor);
        Assert.False((await usuarios.ExcluirUsuarioAsync(Admin, antigo, ct)).Valor);
        var lista = await usuarios.ListarAsync(Admin, ct);
        Assert.DoesNotContain(lista, u => u.Id == novo);
        Assert.False(lista.Single(u => u.Id == antigo).Ativo);

        var self = new SessaoUsuario(antigo, "ja_entrou", "Já Entrou", true, false);
        Assert.Contains("próprio usuário", (await usuarios.ExcluirUsuarioAsync(self, antigo, ct)).Erros[0]);
    }
}
