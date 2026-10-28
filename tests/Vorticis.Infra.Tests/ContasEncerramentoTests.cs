using Vorticis.Application.Cadastros;
using Vorticis.Application.Contabil;

namespace Vorticis.Infra.Tests;

/// <summary>Conferência das contas de encerramento (existência, máscara, analítica e natureza) com mensagens para o contador.</summary>
public class ContasEncerramentoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private static readonly Dictionary<string, ContaConferida> Plano = new()
    {
        ["5.1.01.001"] = new(true, "04"), ["2.3.03.001"] = new(true, "03"), ["2.3.03.002"] = new(true, "03"),
        ["2.3.03"] = new(false, "03"), ["1.1.01.001"] = new(true, "01"),
    };
    private static ContaConferida? Achar(string c) => Plano.GetValueOrDefault(c);

    [Fact]
    public void Conferencia_aceita_contas_certas_e_explica_cada_problema_em_linguagem_simples()
    {
        Assert.Empty(ConferenciaContasEncerramento.Conferir(new(), "9.9.99.999", Achar));

        var erros = ConferenciaContasEncerramento.Conferir(new("9.9.99.999", "2.3.03", "1.1.01.001"), "9.9.99.999", Achar);
        Assert.Contains(erros, e => e.Contains("apuração do resultado (9.9.99.999) não existe no plano"));
        Assert.Contains(erros, e => e.Contains("lucros acumulados (2.3.03)") && e.Contains("sintética"));
        Assert.Contains(ConferenciaContasEncerramento.Conferir(new("5.1.01.001", "2.3.3.1", "2.3.03.002"), "9.9.99.999", Achar), e => e.Contains("não existe no plano") && e.Contains("não está na máscara"));
        Assert.Contains(erros, e => e.Contains("prejuízos acumulados (1.1.01.001)") && e.Contains("grupo Ativo") && e.Contains("Patrimônio líquido"));
        Assert.All(erros, e => Assert.DoesNotContain("Exception", e));

        // apuração com conta de patrimônio: natureza errada
        Assert.Contains(ConferenciaContasEncerramento.Conferir(new("2.3.03.001", "2.3.03.001", "2.3.03.002"), null, Achar), e => e.Contains("deveria ser uma conta de Resultado"));

        // em branco: no encerramento é obrigatório; nos parâmetros vale o padrão do modelo
        Assert.Equal(3, ConferenciaContasEncerramento.Conferir(new("", " ", ""), null, Achar).Count);
        Assert.Empty(ConferenciaContasEncerramento.Conferir(new("", "", ""), null, Achar, aceitarVazias: true));

        // a máscara só vale quando o próprio plano a segue
        Assert.Null(ConferenciaContasEncerramento.MascaraSeguidaPeloPlano("9.9.99.999", ["1.1.1.1"]));
        Assert.Equal("9.9.99.999", ConferenciaContasEncerramento.MascaraSeguidaPeloPlano("9.9.99.999", Plano.Keys.Where(k => k.Length == 10)));
    }

    [Fact]
    public async Task Servicos_avisam_antes_de_encerrar_e_nao_falham_com_erro_tecnico()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await db.CriarEmpresaAsync("11222333000181");
        var adm = await db.AdminAsync();

        Assert.Empty(await db.App.PlanoContas.ConferirContasEncerramentoAsync(empresa, new ContasEncerramento(), adm, ct));
        var ruins = new ContasEncerramento("9.9.99.999", "2.3.03", "1.1.01.002");
        var avisos = await db.App.PlanoContas.ConferirContasEncerramentoAsync(empresa, ruins, adm, ct);
        Assert.True(avisos.Count >= 3);
        Assert.Contains(avisos, a => a.Contains("não existe no plano"));
        Assert.Contains(avisos, a => a.Contains("sintética"));
        Assert.Contains(avisos, a => a.Contains("grupo Ativo"));

        // o próprio encerramento recusa com a mesma linguagem (inclusive código em branco), sem exceção
        foreach (var c in new[] { ruins, new ContasEncerramento("", "", ""), new ContasEncerramento(" 5.1.01.001 ", "2.3.03.001", "2.3.03.002") with { Apuracao = "5.1.01.001" } })
        {
            var r = await db.App.Encerramento.PreverAsync(empresa, 2026, c, adm, ct);
            if (c == ruins || c.Apuracao == "") { Assert.False(r.Sucesso); Assert.All(r.Erros, e => Assert.DoesNotContain("Exception", e)); }
        }

        // parâmetros: conta sintética, inexistente ou de outra natureza não grava
        var cfg = await db.App.PlanoContas.ObterConfiguracaoAsync(empresa, adm, ct);
        var salvo = await db.App.PlanoContas.SalvarConfiguracaoAsync(empresa, cfg with { ContaApuracao = "2.3.03.001" }, null, adm, ct);
        Assert.False(salvo.Sucesso);
        Assert.Contains(salvo.Erros, e => e.Contains("deveria ser uma conta de Resultado"));
        Assert.False((await db.App.PlanoContas.SalvarConfiguracaoAsync(empresa, cfg with { ContaLucros = "2.3.03" }, null, adm, ct)).Sucesso);
        Assert.True((await db.App.PlanoContas.SalvarConfiguracaoAsync(empresa, cfg with { ContaApuracao = "5.1.01.001" }, null, adm, ct)).Sucesso);
    }
}
