using Vorticis.Folha.Servicos;

namespace Vorticis.Folha.Tests;

/// <summary>Obrigações › FGTS Digital: demonstrativo do FGTS da competência, guia só com a folha fechada, pagamento e vencimento no dia 20.</summary>
public class FgtsDigitalTests(BancoFolha db) : IClassFixture<BancoFolha>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Vencimento_e_o_dia_20_do_mes_seguinte_antecipado_para_dia_util()
    {
        Assert.Equal(new DateOnly(2026, 7, 20), ServicoFgtsDigital.Vencimento(new DateOnly(2026, 6, 1)));    // segunda-feira
        Assert.Equal(new DateOnly(2026, 6, 19), ServicoFgtsDigital.Vencimento(new DateOnly(2026, 5, 15)));   // 20/06/2026 é sábado
        Assert.Equal(new DateOnly(2026, 6, 20), ServicoFgtsDigital.Vencimento(new DateOnly(2026, 5, 1), antecipa: false));
    }

    [Fact]
    public async Task Demonstrativo_da_folha_guia_so_com_a_competencia_fechada_e_pagamento()
    {
        BancoFolha.ExigirBanco();
        var adm = await db.AdminAsync();
        var empresa = await db.CriarEmpresaAsync("19131243000197");
        var comp = new DateOnly(2026, 6, 1);
        Assert.True((await db.Folha.Cadastros.GravarFuncionarioAsync(empresa, FolhaIntegracaoTests.Novo("7", "Ana Souza", "52998224725", 3000m), adm, ct: Ct)).Sucesso);
        Assert.True((await db.Folha.Folha.CalcularMensalAsync(empresa, comp, adm, ct: Ct)).Sucesso);
        var fgts = db.Folha.FgtsDigital;

        var d = (await fgts.DemonstrativoAsync(empresa, comp, adm, Ct)).Valor!;
        var ana = Assert.Single(d.Linhas);
        Assert.Equal(("Ana Souza", 3000m, 240m), (ana.Nome, ana.Base, ana.Fgts));     // 8% da base
        Assert.Equal(240m, d.Total);
        Assert.Equal(new DateOnly(2026, 7, 20), d.Vencimento);
        Assert.False(d.CompetenciaFechada);
        Assert.Contains("feche a competência", d.Situacao);
        Assert.Contains("não está fechada", (await fgts.RegistrarGuiaAsync(empresa, comp, "GFD-123", adm, Ct)).Erros[0]);

        Assert.True((await db.Folha.Folha.FecharAsync(empresa, comp, adm, Ct)).Sucesso);
        Assert.False((await fgts.RegistrarGuiaAsync(empresa, comp, " ", adm, Ct)).Sucesso);
        Assert.Contains("Registre a guia", (await fgts.RegistrarPagamentoAsync(empresa, comp, new DateOnly(2026, 7, 15), 240m, adm, Ct)).Erros[0]);
        Assert.True((await fgts.RegistrarGuiaAsync(empresa, comp, "GFD-123", adm, Ct)).Sucesso);
        var guia = (await fgts.DemonstrativoAsync(empresa, comp, adm, Ct)).Valor!.Guia!;
        Assert.Equal(("GFD-123", 240m, new DateOnly(2026, 7, 20), false), (guia.Identificador, guia.Valor, guia.Vencimento, guia.Paga));
        Assert.True((await fgts.RegistrarPagamentoAsync(empresa, comp, new DateOnly(2026, 7, 15), 240m, adm, Ct)).Sucesso);
        var paga = (await fgts.DemonstrativoAsync(empresa, comp, adm, Ct)).Valor!;
        Assert.Contains("paga em 15/07/2026", paga.Situacao);
        Assert.Contains("já está paga", (await fgts.RegistrarGuiaAsync(empresa, comp, "GFD-999", adm, Ct)).Erros[0]);
        Assert.False((await fgts.ExcluirGuiaAsync(empresa, comp, adm, Ct)).Sucesso);   // paga não sai
        Assert.Equal(2, paga.Tabela().Linhas.Count);                                     // trabalhador e total
    }
}
