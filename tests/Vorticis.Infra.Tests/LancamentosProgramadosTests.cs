using Vorticis.Infra.Contabil;

namespace Vorticis.Infra.Tests;

/// <summary>Lançamentos › Contábeis › Lançamentos programados: gera uma vez por competência, respeita a vigência e o dia.</summary>
public class LancamentosProgramadosTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public void Data_vigencia_e_historico()
    {
        var p = new LancamentoProgramado(1, "Aluguel", "1", "2", 100m, "Aluguel {competencia}", 31, new(2026, 1, 1), new(2026, 3, 1));
        Assert.Equal(new DateOnly(2026, 2, 28), p.DataEm(new(2026, 2, 1)));
        Assert.True(p.VigenteEm(new(2026, 3, 1)));
        Assert.False(p.VigenteEm(new(2026, 4, 1)));
        Assert.False(p.VigenteEm(new(2025, 12, 1)));
        Assert.False((p with { Ativo = false }).VigenteEm(new(2026, 2, 1)));
        Assert.Equal("Aluguel 02/2026", p.HistoricoEm(new(2026, 2, 1)));
    }

    [Fact]
    public async Task Gera_uma_vez_por_competencia_e_valida()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var empresa = await db.CriarEmpresaAsync("34238864000168");
        var s = db.App.LancamentosProgramados;
        Assert.Contains("diferentes", (await s.GravarAsync(empresa, new(0, "X", "1.1.01.001", "1.1.01.001", 10m, "H", 5, new(2026, 1, 1)), adm, ct)).Erros[0]);
        var id = await s.GravarAsync(empresa, new(0, "Aluguel", "1.1.01.001", "3.1.01.001", 1500m, "Aluguel {competencia}", 10, new(2026, 1, 1)), adm, ct);
        Assert.True(id.Sucesso, string.Join("; ", id.Erros));
        var invalido = await s.GravarAsync(empresa, new(0, "Conta errada", "9.9.99", "3.1.01.001", 50m, "Teste", 10, new(2026, 1, 1)), adm, ct);
        Assert.True(invalido.Sucesso);

        var g = await s.GerarAsync(empresa, new(2026, 2, 1), adm, ct);
        Assert.True(g.Sucesso, string.Join("; ", g.Erros));
        Assert.Equal((1, 1500m), (g.Valor.Gerados, g.Valor.Total));
        Assert.Contains(g.Valor.Avisos, a => a.StartsWith("Conta errada"));
        Assert.Equal(0, (await s.GerarAsync(empresa, new(2026, 2, 1), adm, ct)).Valor.Gerados);   // não duplica
        Assert.True(await db.EscalarAsync<bool>("""
            SELECT EXISTS (SELECT 1 FROM contabil.lancamento l JOIN contabil.partida p ON p.lancamento_id = l.id
                            WHERE l.empresa_id = @e AND l.data_lancamento = DATE '2026-02-10' AND p.historico = 'Aluguel 02/2026')
            """, ("e", empresa)));
        var lista = await s.ListarAsync(empresa, adm, ct);
        Assert.Equal(new DateOnly(2026, 2, 1), lista.Single(x => x.Descricao == "Aluguel").UltimaGerada);
        Assert.True((await s.ExcluirAsync(empresa, id.Valor, adm, ct)).Sucesso);
        Assert.Equal(1, await db.EscalarAsync<long>("SELECT count(*) FROM contabil.lancamento WHERE empresa_id = @e", ("e", empresa)));   // o gerado fica
    }
}
