using Vorticis.Frota;
using Vorticis.Infra.Tests;

namespace Vorticis.Modulos.Tests;

public class RegrasFrotaTests
{
    private static Veiculo V(long id = 1, string comb = "DIESEL_S10", string tipo = "CAVALO", decimal? media = 2.5m) =>
        new(id, (int)id, "ABC1D23", null, tipo, "Volvo", "FH 540", 2022, 2023, comb, 30000, 100000, media);

    private static Abastecimento A(decimal km, decimal litros, bool cheio = true, string comb = "DIESEL_S10", int dia = 1) =>
        new(0, 1, null, new DateOnly(2026, 3, dia), km, litros, litros * 6m, comb, null, cheio);

    [Fact]
    public void Placa_aceita_padrao_antigo_e_Mercosul_e_RENAVAM_confere_o_digito()
    {
        Assert.True(RegrasFrota.PlacaValida("abc-1234"));
        Assert.True(RegrasFrota.PlacaValida("BRA2E19"));
        Assert.False(RegrasFrota.PlacaValida("AB12345"));
        Assert.Equal("ABC-1234", RegrasFrota.FormatarPlaca(RegrasFrota.NormalizarPlaca("abc 1234")));
        Assert.Equal("BRA2E19", RegrasFrota.FormatarPlaca("BRA2E19"));
        Assert.True(RegrasFrota.RenavamValido("00639254187"));
        Assert.False(RegrasFrota.RenavamValido("00639254188"));
        Assert.False(RegrasFrota.RenavamValido("0063925418"));
    }

    [Fact]
    public void Validacoes_de_veiculo_motorista_abastecimento_e_viagem()
    {
        Assert.Empty(RegrasFrota.Validar(V()));
        Assert.Equal(3, RegrasFrota.Validar(V() with { Codigo = 0, Placa = "X", AnoModelo = 2025 }).Count);
        Assert.Empty(RegrasFrota.Validar(new Motorista(0, 1, "João", "52998224725", "12345678900", "E", null, null)));
        Assert.Contains("CPF inválido.", RegrasFrota.Validar(new Motorista(0, 1, "João", "11111111111", null, null, null, null)));
        // km não volta; combustível tem de combinar (Arla vale para diesel); reboque não abastece
        Assert.Empty(RegrasFrota.Validar(A(1000, 300), V(), 900));
        Assert.Contains(RegrasFrota.Validar(A(800, 300), V(), 900), e => e.Contains("menor que a do abastecimento anterior"));
        Assert.Empty(RegrasFrota.Validar(A(1000, 40, comb: "ARLA32"), V(), null));
        Assert.Contains(RegrasFrota.Validar(A(1000, 40, comb: "GASOLINA"), V(), null), e => e.Contains("não combina"));
        Assert.Contains(RegrasFrota.Validar(A(1000, 40), V(tipo: "SEMIRREBOQUE", comb: "NENHUM"), null), e => e.Contains("não abastece"));
        Assert.True(RegrasFrota.CombinaCombustivel("FLEX", "ETANOL"));
        var viagem = new Viagem(0, 0, 1, null, null, "Curitiba", "São Paulo", new DateOnly(2026, 3, 1), null, 1000, null, 5000, 300, 200, 0, "CONCLUIDA");
        Assert.Contains("Para concluir a viagem, informe a data e o km de chegada.", RegrasFrota.Validar(viagem));
        Assert.Empty(RegrasFrota.Validar(viagem with { Chegada = new DateOnly(2026, 3, 2), KmChegada = 1410 }));
        Assert.Equal(410m, (viagem with { KmChegada = 1410 }).KmRodado);
        Assert.Equal(500m, viagem.Despesas);
    }

    [Fact]
    public void Media_de_tanque_cheio_a_tanque_cheio_soma_os_parciais_e_ignora_Arla()
    {
        Abastecimento[] abs = [A(1000, 400, dia: 1), A(1500, 100, cheio: false, dia: 3), A(1600, 30, comb: "ARLA32", dia: 3), A(2000, 300, dia: 5), A(2600, 250, dia: 8)];
        var medias = RegrasFrota.Medias(abs);
        Assert.Equal(2, medias.Count);
        Assert.Equal((1000m, 400m, 2.5m), (medias[0].KmRodado, medias[0].Litros, medias[0].Media));   // 1000 km / (100 + 300) l
        Assert.Equal(2.4m, medias[1].Media);
        Assert.False(RegrasFrota.ConsumoForaDoEsperado(2.4m, 2.5m, 15));
        Assert.True(RegrasFrota.ConsumoForaDoEsperado(2.0m, 2.5m, 15));
        Assert.False(RegrasFrota.ConsumoForaDoEsperado(1m, null, 15));
    }

    [Fact]
    public void Preventiva_vence_pelo_que_vier_primeiro_e_avisa_antes()
    {
        var hoje = new DateOnly(2026, 6, 1);
        var p = new PlanoManutencao(1, 1, "Troca de óleo", 20000, 180, 100000, new DateOnly(2026, 3, 1));
        Assert.Null(RegrasFrota.Situacao(p, 110000, hoje, 30).Gravidade);
        Assert.Equal(GravidadeAviso.AVencer, RegrasFrota.Situacao(p, 118500, hoje, 30).Gravidade);        // a 10% do intervalo de km
        Assert.Equal(GravidadeAviso.Vencido, RegrasFrota.Situacao(p, 120000, hoje, 30).Gravidade);
        var s = RegrasFrota.Situacao(p, 105000, new DateOnly(2026, 8, 10), 30);                           // data: 28/08 está dentro de 30 dias
        Assert.Equal((GravidadeAviso.AVencer, 120000m, new DateOnly(2026, 8, 28)), (s.Gravidade!.Value, s.ProximaKm!.Value, s.ProximaData!.Value));
        Assert.Equal(GravidadeAviso.Vencido, RegrasFrota.SituacaoVencimento(hoje.AddDays(-1), hoje, 30));
        Assert.Equal(GravidadeAviso.AVencer, RegrasFrota.SituacaoVencimento(hoje.AddDays(30), hoje, 30));
        Assert.Null(RegrasFrota.SituacaoVencimento(hoje.AddDays(31), hoje, 30));
        Assert.Equal("vencido há 2 dia(s)", RegrasFrota.TextoVencimento(hoje.AddDays(-2), hoje));
        Assert.Equal("vence hoje", RegrasFrota.TextoVencimento(hoje, hoje));
    }

    [Fact]
    public void Relatorios_de_custos_consumo_e_viagens_fecham_os_totais()
    {
        Veiculo[] vs = [V()];
        Abastecimento[] abs = [A(1000, 400, dia: 1), A(2000, 400, dia: 5)];
        OrdemServico[] os = [new(1, 1, 1, null, "CORRETIVA", new DateOnly(2026, 3, 4), null, "Pneu", null, 800, 200)];
        Viagem[] vi = [new(1, 1, 1, null, "Cliente", "Curitiba", "São Paulo", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5), 1000, 2000, 9000, 400, 100, 0, "CONCLUIDA", "ABC1D23")];
        var custos = TabelasFrota.Custos(vs, abs, os, vi);
        Assert.Equal(2, custos.Linhas.Count);
        var total = custos.Linhas[^1].Valores;
        Assert.Equal((1000m, 4800m, 1000m, 500m, 6300m, 6.3m), ((decimal)total[2]!, (decimal)total[4]!, (decimal)total[5]!, (decimal)total[6]!, (decimal)total[7]!, (decimal)total[8]!));
        var consumo = TabelasFrota.Consumo(vs, abs, 15);
        Assert.Equal(2.5m, consumo.Linhas.Single().Valores[4]);
        var viagens = TabelasFrota.Viagens(vi, abs);
        Assert.Equal(9000m - 500m - 4800m, viagens.Linhas[0].Valores[8]);   // frete - despesas - combustível entre a saída e a chegada
        Assert.All(new[] { custos, consumo, viagens }, t => Assert.All(t.Linhas, l => Assert.Equal(t.Colunas.Count, l.Valores.Count)));
    }
}

public class FrotaIntegracaoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Cadastra_abastece_mantem_viaja_e_avisa_vencimentos()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var empresa = await db.CriarEmpresaAsync("48731625000150");
        var svc = ServicosFrota.De(db.App);
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        Assert.True((await svc.GravarConfiguracaoAsync(empresa, new ConfiguracaoFrota(30, 10), adm, ct)).Sucesso);
        Assert.False((await svc.GravarConfiguracaoAsync(empresa, new ConfiguracaoFrota(0, 10), adm, ct)).Sucesso);

        var cavalo = new Veiculo(0, 1, "bra-2e19", "00639254187", "CAVALO", "Scania", "R 450", 2022, 2022, "DIESEL_S10", null, 100000, 2.5m);
        var rv = await svc.GravarVeiculoAsync(empresa, cavalo, adm, ct);
        Assert.True(rv.Sucesso, string.Join(" ", rv.Erros));
        var veiculo = rv.Valor;
        Assert.Contains("Já existe", (await svc.GravarVeiculoAsync(empresa, cavalo with { Codigo = 2 }, adm, ct)).Erros[0]);   // placa repetida
        var reboque = (await svc.GravarVeiculoAsync(empresa, new Veiculo(0, 2, "ABC1234", null, "SEMIRREBOQUE", null, null, null, null, "NENHUM", 30000, 0, null), adm, ct)).Valor;
        var motorista = (await svc.GravarMotoristaAsync(empresa, new Motorista(0, 1, "Pedro Alves", "529.982.247-25", "12345678900", "E", hoje.AddDays(10), null), adm, ct)).Valor;
        Assert.Equal("BRA2E19", (await svc.VeiculosAsync(empresa, adm, ct)).Single(v => v.Id == veiculo).Placa);

        // abastecimentos: km atualiza o veículo; km menor que o anterior é recusado; reboque não abastece
        var d0 = hoje.AddDays(-20);
        Assert.True((await svc.GravarAbastecimentoAsync(empresa, new Abastecimento(0, veiculo, motorista, d0, 100500, 400, 2400, "DIESEL_S10", "Posto A"), adm, ct)).Sucesso);
        Assert.True((await svc.GravarAbastecimentoAsync(empresa, new Abastecimento(0, veiculo, motorista, d0.AddDays(3), 101300, 400, 2400, "DIESEL_S10", null), adm, ct)).Sucesso);
        Assert.Contains("menor", (await svc.GravarAbastecimentoAsync(empresa, new Abastecimento(0, veiculo, null, d0.AddDays(4), 101000, 100, 600, "DIESEL_S10", null), adm, ct)).Erros[0]);
        Assert.False((await svc.GravarAbastecimentoAsync(empresa, new Abastecimento(0, reboque, null, d0, 1, 10, 60, "DIESEL_S10", null), adm, ct)).Sucesso);
        Assert.Equal(101300m, (await svc.VeiculosAsync(empresa, adm, ct)).Single(v => v.Id == veiculo).KmAtual);
        Assert.Equal(2, (await svc.AbastecimentosAsync(empresa, d0, hoje, veiculo, adm, ct)).Count);

        // preventiva: plano vencido pelo km; a ordem concluída renova o plano
        var plano = (await svc.GravarPlanoAsync(empresa, new PlanoManutencao(0, veiculo, "Troca de óleo", 1000, null, 100000, null), adm, ct)).Valor;
        var avisos = await svc.AvisosAsync(empresa, hoje, adm, ct);
        Assert.Contains(avisos, a => a.Gravidade == GravidadeAviso.Vencido && a.Assunto.StartsWith("Manutenção"));
        Assert.Contains(avisos, a => a.Gravidade == GravidadeAviso.AVencer && a.Assunto == "CNH – Pedro Alves");
        Assert.Contains(avisos, a => a.Gravidade == GravidadeAviso.Atencao && a.Assunto.StartsWith("Consumo"));   // 800 km / 400 l = 2,0 km/l < 2,5 - 10%
        var os = await svc.GravarOrdemAsync(empresa, new OrdemServico(0, 0, veiculo, plano, "PREVENTIVA", hoje, 101300, "Troca de óleo", "Oficina B", 900, 300, "CONCLUIDA"), adm, ct);
        Assert.True(os.Sucesso, string.Join(" ", os.Erros));
        Assert.Equal(1, os.Valor);
        Assert.Equal(101300m, (await svc.PlanosAsync(empresa, adm, ct)).Single().UltimaKm);
        Assert.DoesNotContain(await svc.AvisosAsync(empresa, hoje, adm, ct), a => a.Assunto.StartsWith("Manutenção"));

        // viagem e documentos
        var vg = await svc.GravarViagemAsync(empresa, new Viagem(0, 0, veiculo, motorista, "Cliente X", "Curitiba", "Santos", hoje, hoje, 101300, 101700, 6000, 250, 150, 0,
                                                                  "CONCLUIDA"), adm, ct);
        Assert.True(vg.Sucesso, string.Join(" ", vg.Erros));
        Assert.Equal(101700m, (await svc.VeiculosAsync(empresa, adm, ct)).Single(v => v.Id == veiculo).KmAtual);
        Assert.True((await svc.GravarDocumentoAsync(empresa, new DocumentoFrota(0, veiculo, null, "LICENCIAMENTO", null, hoje.AddDays(-1), 300, null), adm, ct)).Sucesso);
        Assert.False((await svc.GravarDocumentoAsync(empresa, new DocumentoFrota(0, veiculo, motorista, "SEGURO", null, hoje, null, null), adm, ct)).Sucesso);
        Assert.Equal(GravidadeAviso.Vencido, (await svc.AvisosAsync(empresa, hoje, adm, ct))[0].Gravidade);

        // relatórios
        var custos = await svc.RelatorioAsync(empresa, TabelasFrota.TituloCustos, d0, hoje, adm, ct);
        Assert.True(custos.Sucesso);
        Assert.Equal(4800m + 1200m + 400m, custos.Valor!.Linhas[^1].Valores[7]);
        var res = await svc.RelatorioAsync(empresa, TabelasFrota.TituloViagens, d0, hoje, adm, ct);
        Assert.Equal(6000m - 400m, res.Valor!.Linhas[0].Valores[8]);   // sem abastecimento durante a viagem

        // veículo com movimento não pode ser excluído; o reboque sem movimento pode
        Assert.Contains("desmarque Ativo", (await svc.ExcluirVeiculoAsync(empresa, veiculo, adm, ct)).Erros[0]);
        Assert.True((await svc.ExcluirVeiculoAsync(empresa, reboque, adm, ct)).Sucesso);
    }
}
