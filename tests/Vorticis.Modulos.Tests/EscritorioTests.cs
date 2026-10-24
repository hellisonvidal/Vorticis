using Vorticis.Escritorio;
using Vorticis.Infra.Tests;

namespace Vorticis.Modulos.Tests;

public class RegrasEscritorioTests
{
    [Fact]
    public void Recorrencia_mantem_o_dia_e_usa_o_ultimo_dia_do_mes_curto()
    {
        Assert.Equal(new DateOnly(2026, 2, 28), RegrasEscritorio.Proxima(new DateOnly(2026, 1, 31), Recorrencia.Mensal));
        Assert.Equal(new DateOnly(2027, 1, 20), RegrasEscritorio.Proxima(new DateOnly(2026, 1, 20), Recorrencia.Anual));
        Assert.Null(RegrasEscritorio.Proxima(new DateOnly(2026, 1, 20), Recorrencia.Nenhuma));
    }

    [Fact]
    public void Gera_cobranca_so_para_contratos_vigentes_e_sem_cobranca_no_mes()
    {
        var comp = new DateOnly(2026, 2, 1);
        ContratoHonorario[] contratos =
        [
            new(1, 10, 800m, 31, new DateOnly(2025, 1, 1), null, "NENHUM", null),
            new(2, 20, 500m, 5, new DateOnly(2026, 3, 1), null, "NENHUM", null),     // ainda não começou
            new(3, 30, 600m, 5, new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 31), "NENHUM", null), // já terminou
            new(4, 40, 700m, 10, new DateOnly(2025, 1, 1), null, "NENHUM", null),    // já cobrada
        ];
        var r = RegrasEscritorio.GerarCobrancas(contratos, comp, new HashSet<long> { 40 });
        var c = Assert.Single(r);
        Assert.Equal((10L, new DateOnly(2026, 2, 28), 800m), (c.EmpresaId, c.Vencimento, c.Valor));
    }
}

public class EscritorioIntegracaoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Tarefa_recorrente_honorarios_e_protocolo()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var empresa = await db.CriarEmpresaAsync("11444777000161");
        var svc = ServicosEscritorio.De(db.App);

        var t = await svc.GravarTarefaAsync(new Tarefa(0, empresa, "DCTFWeb", null, new DateOnly(2026, 3, 15), adm, 1, SituacaoTarefa.Pendente, Recorrencia.Mensal), adm, ct);
        Assert.True(t.Sucesso, string.Join("; ", t.Erros));
        var tarefa = Assert.Single(await svc.ListarTarefasAsync(empresa, true, adm, ct));
        var prox = await svc.ConcluirTarefaAsync(tarefa, adm, ct);
        Assert.NotNull(prox.Valor);
        Assert.Equal(new DateOnly(2026, 4, 15), Assert.Single(await svc.ListarTarefasAsync(empresa, true, adm, ct)).Vencimento);

        Assert.True((await svc.GravarContratoAsync(new ContratoHonorario(0, empresa, 900m, 10, new DateOnly(2026, 1, 1), null, "IPCA", null), adm, ct)).Sucesso);
        Assert.Equal(1, (await svc.GerarCobrancasAsync(new DateOnly(2026, 3, 1), adm, ct)).Valor);
        Assert.Equal(0, (await svc.GerarCobrancasAsync(new DateOnly(2026, 3, 1), adm, ct)).Valor);
        var cob = Assert.Single(await svc.ListarCobrancasAsync(new DateOnly(2026, 3, 1), false, adm, ct), c => c.EmpresaId == empresa);
        Assert.True((await svc.ReceberAsync(cob, new DateOnly(2026, 3, 10), 900m, adm, ct)).Sucesso);
        Assert.DoesNotContain(await svc.ListarCobrancasAsync(null, true, adm, ct), c => c.EmpresaId == empresa);

        Assert.Equal(1, (await svc.IncluirProtocoloAsync(new Protocolo(0, empresa, 0, new DateOnly(2026, 3, 10), TipoProtocolo.Entrega, "Guia DAS", null, "Maria"), adm, ct)).Valor);
        Assert.Equal(2, (await svc.IncluirProtocoloAsync(new Protocolo(0, empresa, 0, new DateOnly(2026, 3, 11), TipoProtocolo.Recebimento, "Extrato", null, "João"), adm, ct)).Valor);

        // usuário sem perfil na empresa não grava nada
        var outro = await db.EscalarAsync<long>("INSERT INTO core.usuario (login, nome, senha_hash) VALUES ('sem_acesso_esc', 'Sem acesso', 'x') RETURNING id");
        Assert.False((await svc.GravarContratoAsync(new ContratoHonorario(0, empresa, 1m, 1, new DateOnly(2026, 1, 1), null, "NENHUM", null), outro, ct)).Sucesso);
        Assert.Empty(await svc.ListarTarefasAsync(null, false, outro, ct));
    }
}
