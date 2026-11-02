using Vorticis.Application.Sistema;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Infra.Tests;

/// <summary>Configurações › Histórico de alterações: consulta da trilha com filtros, o que mudou campo a campo e a conferência da cadeia.</summary>
public class AuditoriaConsultaTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Mostra_quem_alterou_o_que_e_confere_a_trilha()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var admin = new SessaoUsuario(await db.AdminAsync(), "admin_teste", "Admin", true, false);
        var empresa = await db.CriarEmpresaAsync("19131243000197");
        await db.EscalarAsync<int>("UPDATE core.empresa SET nome_fantasia = 'Nova Fantasia' WHERE id = @e RETURNING 1", ("e", empresa));

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var r = await db.App.Auditoria.ConsultarAsync(admin, new FiltroAuditoria(hoje.AddDays(-1), hoje.AddDays(1), Tabela: "core.empresa", EmpresaId: empresa), ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        var alteracao = r.Valor.First(x => x.Operacao == "UPDATE");
        Assert.Equal("Empresas", alteracao.Cadastro);
        Assert.Equal("Alteração", alteracao.OperacaoTexto);
        Assert.Contains(alteracao.Diferencas(), d => d.Campo == "nome_fantasia" && d.Depois == "Nova Fantasia");
        Assert.Contains("nome_fantasia", alteracao.Resumo);
        Assert.Contains(r.Valor, x => x.Operacao == "INSERT" && x.RegistroId == empresa);
        Assert.True(r.Valor[0].Id > r.Valor[^1].Id);                                                          // mais recentes primeiro

        Assert.Empty((await db.App.Auditoria.ConsultarAsync(admin, new FiltroAuditoria(hoje.AddDays(-1), hoje.AddDays(1), Usuario: "ninguem_assim"), ct)).Valor);
        Assert.False((await db.App.Auditoria.ConsultarAsync(admin with { Administrador = false }, new FiltroAuditoria(hoje, hoje), ct)).Sucesso);
        Assert.False((await db.App.Auditoria.ConsultarAsync(admin, new FiltroAuditoria(hoje, hoje.AddDays(-1)), ct)).Sucesso);
        Assert.Contains("core.empresa", await db.App.Auditoria.TabelasAsync(ct));

        // o login de quem usa o programa vai para a trilha mesmo nas gravações que não informam o usuário
        db.App.UsarLogin("maria.contadora");
        try
        {
            var outra = await db.CriarEmpresaAsync("60701190000104");
            var registro = (await db.App.Auditoria.ConsultarAsync(admin, new FiltroAuditoria(hoje.AddDays(-1), hoje.AddDays(1), Tabela: "core.empresa", EmpresaId: outra), ct)).Valor;
            Assert.All(registro, x => Assert.Equal("maria.contadora", x.Usuario));
            Assert.NotEmpty(registro);
        }
        finally { db.App.UsarLogin(null); }

        var integra = await db.App.Auditoria.VerificarIntegridadeAsync(admin, ct);
        Assert.True(integra.Sucesso);
        Assert.Null(integra.Valor);
    }
}
