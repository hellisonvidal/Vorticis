using Vorticis.Condominio;
using Vorticis.Infra.Tests;

namespace Vorticis.Modulos.Tests;

public class RegrasCondominioTests
{
    private static Unidade U(long id, decimal f, bool ativa = true) => new(id, "A", id.ToString(), "APARTAMENTO", f, null, null, null, false, ativa);

    [Fact]
    public void Rateio_pela_fracao_fecha_o_total_e_partes_iguais_tambem()
    {
        Unidade[] us = [U(1, 0.3m), U(2, 0.3m), U(3, 0.4m), U(4, 0.2m, ativa: false)];
        var r = RegrasCondominio.Ratear(new Taxa(1, "Ordinária", Rateio.Fracao, 1000.01m, new DateOnly(2026, 1, 1), null), us);
        Assert.Equal(1000.01m, r.Values.Sum());
        Assert.Equal(400.01m, r[3]);                      // o centavo vai para a maior fração
        Assert.False(r.ContainsKey(4));                   // unidade inativa não paga
        var iguais = RegrasCondominio.Ratear(new Taxa(2, "Extra", Rateio.Igual, 100m, new DateOnly(2026, 1, 1), null), us);
        Assert.Equal((33.34m, 33.33m, 100m), (iguais[1], iguais[2], iguais.Values.Sum()));
        Assert.All(RegrasCondominio.Ratear(new Taxa(3, "Gás", Rateio.Fixo, 50m, new DateOnly(2026, 1, 1), null), us).Values, v => Assert.Equal(50m, v));
    }

    [Fact]
    public void Rateio_avulso_fecha_o_total_e_trata_valor_fixo_como_partes_iguais()
    {
        Unidade[] us = [U(1, 0.25m), U(2, 0.75m), U(3, 0.5m, ativa: false)];
        var r = RegrasCondominio.RatearValor(1000m, Rateio.Fracao, us);
        Assert.Equal([(1L, 250m), (2L, 750m)], r.Select(p => (p.UnidadeId, p.Valor)));
        var iguais = RegrasCondominio.RatearValor(100m, Rateio.Fixo, us);
        Assert.Equal(100m, iguais.Sum(p => p.Valor));
        Assert.Equal(2, iguais.Count);
    }

    [Fact]
    public void Ata_valida_os_campos_e_monta_abertura_pauta_deliberacoes_presenca_e_assinaturas()
    {
        var a = new Assembleia(1, 3, new DateOnly(2026, 5, 20), "19:30", TipoAssembleia.Extraordinaria, 2, "Salão de festas", "Ana Souza", "Bruno Lima", 2,
                               "1. Pintura da fachada", "Aprovada a pintura por unanimidade.");
        Assert.Empty(AtaAssembleia.Validar(a));
        var erros = AtaAssembleia.Validar(a with { Horario = "7h", Local = " ", Presidente = "", Pauta = "", Convocacao = 3 });
        Assert.Equal(5, erros.Count);
        Unidade[] us = [U(1, 0.5m) with { ProprietarioNome = "Carla" }, U(2, 0.5m)];
        var doc = AtaAssembleia.Documento(a, "Condomínio Edifício Sol", "12.345.678/0001-95", us, "Davi Rocha", listaPresenca: true);
        Assert.Equal("ATA DA ASSEMBLEIA GERAL EXTRAORDINÁRIA Nº 3", doc.Titulo);
        Assert.Contains("segunda convocação", doc.Blocos[0].Texto);
        Assert.Contains("2 de 2 unidade(s)", doc.Blocos[0].Texto);
        Assert.Equal("1. Pintura da fachada", doc.Blocos.Single(b => b.Titulo == "Ordem do dia").Texto);
        var presenca = doc.Blocos.Single(b => b.Titulo == "Lista de presença");
        Assert.True(presenca.NovaPagina);
        Assert.Equal(2, presenca.Linhas!.Count);
        Assert.Equal(["Ana Souza\nPresidente da mesa", "Bruno Lima\nSecretário(a)", "Davi Rocha\nSíndico(a)"], doc.Assinaturas!);
        var semLista = AtaAssembleia.Documento(a, "Condomínio Edifício Sol", null, us, null, listaPresenca: false);
        Assert.DoesNotContain(semLista.Blocos, b => b.Titulo == "Lista de presença");
        Assert.Equal(2, semLista.Assinaturas!.Count);
    }

    [Fact]
    public void Encargos_limitam_a_multa_e_cobram_juros_pro_rata()
    {
        var cfg = new ConfiguracaoCondominio(10, 10m, 1m);   // multa acima do limite legal é reduzida a 2%
        var venc = new DateOnly(2026, 3, 10);
        Assert.Equal((0m, 0m), RegrasCondominio.Encargos(500m, venc, venc, cfg, 2m));
        Assert.Equal((10m, 2.50m), RegrasCondominio.Encargos(500m, venc, venc.AddDays(15), cfg, 2m));
    }
}

public class CondominioIntegracaoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Gera_cobrancas_recebe_com_encargos_e_presta_contas()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var empresa = await db.CriarEmpresaAsync("26637142000158");
        var svc = ServicosCondominio.De(db.App);
        var prop = await db.EscalarAsync<long>("INSERT INTO core.participante (empresa_id, codigo, nome, cliente) VALUES (@e, 'P1', 'Proprietária', TRUE) RETURNING id", ("e", empresa));

        Assert.Contains("2%", (await svc.GravarConfiguracaoAsync(empresa, new ConfiguracaoCondominio(10, 5m, 1m), adm, ct)).Erros[0]);
        Assert.True((await svc.GravarConfiguracaoAsync(empresa, new ConfiguracaoCondominio(10, 2m, 1m), adm, ct)).Sucesso);
        Assert.True((await svc.GravarUnidadeAsync(empresa, new Unidade(0, "A", "101", "APARTAMENTO", 0.6m, 80m, prop, null, false), adm, ct)).Sucesso);
        Assert.True((await svc.GravarUnidadeAsync(empresa, new Unidade(0, "A", "102", "APARTAMENTO", 0.4m, 60m, prop, null, false), adm, ct)).Sucesso);
        Assert.Equal("Informe o número da unidade.", (await svc.GravarUnidadeAsync(empresa, new Unidade(0, "A", "", "APARTAMENTO", 1m, null, null, null, false), adm, ct)).Erros[0]);
        Assert.True((await svc.GravarTaxaAsync(empresa, new Taxa(0, "Taxa ordinária", Rateio.Fracao, 1000m, new DateOnly(2026, 1, 1), null), adm, ct)).Sucesso);
        Assert.True((await svc.GravarTaxaAsync(empresa, new Taxa(0, "Fundo de reserva", Rateio.Igual, 100m, new DateOnly(2026, 1, 1), null), adm, ct)).Sucesso);

        var comp = new DateOnly(2026, 3, 1);
        Assert.Equal(2, (await svc.GerarCobrancasAsync(empresa, comp, adm, ct)).Valor);
        Assert.Equal(0, (await svc.GerarCobrancasAsync(empresa, comp, adm, ct)).Valor);
        var cobs = await svc.CobrancasAsync(empresa, comp, false, adm, ct);
        var c101 = cobs.Single(c => c.Unidade == "A-101");
        Assert.Equal((650m, 2, new DateOnly(2026, 3, 10), "Proprietária"), (c101.Valor, c101.Itens.Count, c101.Vencimento, c101.Responsavel));

        var pag = new DateOnly(2026, 3, 25);
        var (multa, juros) = await svc.EncargosAsync(empresa, c101, pag, ct);
        Assert.Equal((13m, 3.25m), (multa, juros));
        Assert.False((await svc.ReceberAsync(empresa, c101, pag, 50m, 0, 0, adm, ct)).Sucesso);   // multa acima do limite
        Assert.True((await svc.ReceberAsync(empresa, c101, pag, multa, juros, 0, adm, ct)).Sucesso);
        Assert.True((await svc.GravarDespesaAsync(empresa, new Despesa(0, pag, pag, null, "LIMPEZA", "Material de limpeza", 120m, pag), adm, ct)).Sucesso);

        var pc = await svc.PrestacaoContasAsync(empresa, comp, adm, ct);
        Assert.Equal((650m, 16.25m, 120m, 1100m, 450m), (pc.Valor.Arrecadado, pc.Valor.EncargosRecebidos, pc.Valor.TotalDespesas, pc.Valor.Emitido, pc.Valor.Inadimplencia));
        Assert.Equal(546.25m, pc.Valor.Saldo);
        var inad = await svc.InadimplenciaAsync(empresa, new DateOnly(2026, 4, 9), adm, ct);
        Assert.Equal("A-102", Assert.Single(inad.Valor).C.Unidade);

        Assert.True((await svc.GravarAreaAsync(empresa, new AreaComum(0, "Salão de festas", 50, 100m), adm, ct)).Sucesso);
        var area = Assert.Single(await svc.AreasAsync(empresa, adm, ct));
        Assert.True((await svc.ReservarAsync(empresa, area.Id, c101.UnidadeId, new DateOnly(2026, 4, 4), null, adm, ct)).Sucesso);
        Assert.Contains("já está reservada", (await svc.ReservarAsync(empresa, area.Id, c101.UnidadeId, new DateOnly(2026, 4, 4), null, adm, ct)).Erros[0]);
    }

    [Fact]
    public async Task Rateio_entra_nas_cobrancas_abertas_cria_as_que_faltam_e_pula_as_pagas()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var empresa = await db.CriarEmpresaAsync("11444777000161");
        var svc = ServicosCondominio.De(db.App);
        Assert.False((await svc.PreviaRateioAsync(empresa, 100m, Rateio.Fracao, adm, ct)).Sucesso);   // sem unidades
        foreach (var (n, f) in new[] { ("101", 0.5m), ("102", 0.3m) })
            Assert.True((await svc.GravarUnidadeAsync(empresa, new Unidade(0, "A", n, "APARTAMENTO", f, null, null, null, false), adm, ct)).Sucesso);
        Assert.True((await svc.GravarTaxaAsync(empresa, new Taxa(0, "Taxa ordinária", Rateio.Igual, 300m, new DateOnly(2026, 1, 1), null), adm, ct)).Sucesso);
        var comp = new DateOnly(2026, 6, 1);
        Assert.Equal(2, (await svc.GerarCobrancasAsync(empresa, comp, adm, ct)).Valor);
        var cobs = await svc.CobrancasAsync(empresa, comp, false, adm, ct);
        var c101 = cobs.Single(c => c.Unidade == "A-101");
        Assert.True((await svc.ReceberAsync(empresa, c101, c101.Vencimento, 0, 0, 0, adm, ct)).Sucesso);
        // unidade cadastrada depois de gerar as cobranças: o rateio cria a cobrança dela
        Assert.True((await svc.GravarUnidadeAsync(empresa, new Unidade(0, "A", "103", "APARTAMENTO", 0.2m, null, null, null, false), adm, ct)).Sucesso);

        var previa = await svc.PreviaRateioAsync(empresa, 1000m, Rateio.Fracao, adm, ct);
        Assert.Equal([500m, 300m, 200m], previa.Valor.Select(p => p.Valor));
        Assert.False((await svc.LancarRateioAsync(empresa, comp, " ", 1000m, Rateio.Fracao, adm, ct)).Sucesso);
        var r = await svc.LancarRateioAsync(empresa, comp, "Rateio da pintura", 1000m, Rateio.Fracao, adm, ct);
        Assert.True(r.Sucesso);
        Assert.Equal(2, r.Valor.Lancadas);
        Assert.Contains("A-101", Assert.Single(r.Valor.Avisos));
        var depois = await svc.CobrancasAsync(empresa, comp, false, adm, ct);
        var c102 = depois.Single(c => c.Unidade == "A-102");
        Assert.Equal((450m, 2), (c102.Valor, c102.Itens.Count));
        var c103 = depois.Single(c => c.Unidade == "A-103");
        Assert.Equal((200m, "Rateio da pintura"), (c103.Valor, Assert.Single(c103.Itens).Descricao));
        Assert.Equal(150m, depois.Single(c => c.Unidade == "A-101").Valor);
    }

    [Fact]
    public async Task Assembleias_numeram_por_condominio_e_a_ata_leva_o_sindico_que_tambem_assina_os_livros()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var empresa = await db.CriarEmpresaAsync("45723174000110");
        var svc = ServicosCondominio.De(db.App);
        var a = new Assembleia(0, 0, new DateOnly(2026, 4, 10), "20:00", TipoAssembleia.Ordinaria, 1, "Salão", "Ana", "Bruno", 10, "Contas de 2025", "Aprovadas.");
        Assert.Equal(1, (await svc.GravarAssembleiaAsync(empresa, a, adm, ct)).Valor);
        Assert.Equal(2, (await svc.GravarAssembleiaAsync(empresa, a with { Tipo = TipoAssembleia.Extraordinaria }, adm, ct)).Valor);
        Assert.False((await svc.GravarAssembleiaAsync(empresa, a with { Pauta = "" }, adm, ct)).Sucesso);
        var lista = await svc.AssembleiasAsync(empresa, adm, ct);
        Assert.Equal([2, 1], lista.Select(x => x.Numero));
        Assert.Equal(1, (await svc.GravarAssembleiaAsync(empresa, lista[1] with { Local = "Garagem" }, adm, ct)).Valor);
        Assert.True((await svc.ExcluirAssembleiaAsync(empresa, lista[0].Id, adm, ct)).Sucesso);
        Assert.Equal("Garagem", Assert.Single(await svc.AssembleiasAsync(empresa, adm, ct)).Local);

        var resp = db.App.Responsaveis;
        var sindico = new Vorticis.Application.Cadastros.Sindico("Davi Rocha", "529.982.247-25", null, null, new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31), true, true);
        Assert.Contains("CPF", (await resp.SalvarSindicoAsync(empresa, sindico with { Cpf = "111.111.111-12" }, adm, ct)).Erros[0]);
        Assert.False((await resp.SalvarSindicoAsync(empresa, sindico with { AssinaLivros = false }, adm, ct)).Sucesso);   // digital sem assinar
        Assert.False((await resp.SalvarSindicoAsync(empresa, sindico with { MandatoFim = new DateOnly(2025, 1, 1) }, adm, ct)).Sucesso);
        Assert.True((await resp.SalvarSindicoAsync(empresa, sindico, adm, ct)).Sucesso);
        Assert.Equal("52998224725", (await resp.ObterSindicoAsync(empresa, adm, ct)).Valor!.Cpf);
        Assert.Equal("Davi Rocha", (await svc.DadosAtaAsync(empresa, ct)).Sindico);

        var ass = await db.App.Assinaturas.MontarAsync(empresa, new Vorticis.Application.Relatorios.OpcoesAssinatura(Responsavel: true), ct);
        Assert.Equal("Davi Rocha\nSíndico(a) — CPF 529.982.247-25 — assinado digitalmente (certificado ICP-Brasil)", Assert.Single(ass.Valor));
        Assert.True((await resp.SalvarSindicoAsync(empresa, sindico with { AssinaLivros = false, AssinaturaDigital = false }, adm, ct)).Sucesso);
        Assert.False((await db.App.Assinaturas.MontarAsync(empresa, new Vorticis.Application.Relatorios.OpcoesAssinatura(Responsavel: true), ct)).Sucesso);   // sem sócio
        Assert.True((await resp.ExcluirSindicoAsync(empresa, adm, ct)).Sucesso);
        Assert.Null((await resp.ObterSindicoAsync(empresa, adm, ct)).Valor);
    }
}
