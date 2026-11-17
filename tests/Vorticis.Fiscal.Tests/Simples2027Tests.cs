using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Comum;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Reforma;
using Vorticis.Fiscal.Simples;

namespace Vorticis.Fiscal.Tests;

/// <summary>Simples Nacional a partir de 2027 (LC 214/2025, LC 227/2026, Res. CGSN 186, 190 e 194/2026): repartição com CBS/IBS, opção por semestre, limites e janelas.</summary>
public class Simples2027Tests
{
    private static readonly DateOnly Jan27 = new(2027, 1, 1);
    private static TabelaSimples Tabela(DateOnly d) => TabelaSimples.Carregar(ParametrosEmbutidos.Json(ChavesParametro.SimplesNacional, d));
    private static readonly TabelaSimples T2026 = Tabela(new DateOnly(2026, 12, 1));
    private static readonly TabelaSimples T2027 = Tabela(Jan27);

    private static ResultadoSimples Calc(TabelaSimples t, decimal rbt12, bool regular, params ReceitaSimples[] r) =>
        CalculadoraSimples.Calcular(t, new EntradaSimples(Jan27, r, rbt12, 0, regular));

    [Fact]
    public void Tabela_de_2027_mantem_aliquotas_e_troca_PIS_COFINS_por_CBS_com_IBS_zerado_a_conferir()
    {
        Assert.False(T2026.TemIbsCbs);
        Assert.True(T2027.TemIbsCbs);
        Assert.True(T2027.ReparticaoAConferir);
        Assert.Contains("Resolução CGSN 190/2026", T2027.Observacao);
        Assert.All(T2027.Anexos.Values.SelectMany(f => f), f => Assert.Equal(100m, f.Reparticao.Values.Sum()));
        Assert.All(T2027.Anexos.Values.SelectMany(f => f), f => Assert.DoesNotContain("PIS", f.Reparticao.Keys));
        foreach (var anexo in T2026.Anexos.Keys)
            for (var i = 0; i < 6; i++)
            {
                var a = T2026.Anexos[anexo][i];
                var b = T2027.Anexos[anexo][i];
                Assert.Equal((a.Ate, a.Aliquota, a.Deduzir), (b.Ate, b.Aliquota, b.Deduzir));                    // alíquotas e faixas inalteradas
                Assert.Equal(a.Reparticao["PIS"] + a.Reparticao["COFINS"], b.Reparticao["CBS"]);                   // CBS no lugar de PIS/COFINS
                Assert.Equal(0m, b.Reparticao["IBS"]);
            }
        Assert.Equal((3_600_000m, 20m, 300_000m, true), (T2027.LimiteExportacao, T2027.ExcessoPct, T2027.LimiteInicioAtividadeMensal, T2027.SublimiteIncluiIbs));
        Assert.Equal(Tabela(new DateOnly(2026, 6, 1)).Anexos[AnexoSimples.I][1].Reparticao["PIS"], T2026.Anexos[AnexoSimples.I][1].Reparticao["PIS"]);   // até 2026 vale a tabela antiga
    }

    [Fact]
    public void DAS_dentro_mostra_CBS_e_no_regime_regular_as_parcelas_de_IBS_e_CBS_saem_do_DAS()
    {
        var receita = new ReceitaSimples(AtividadeSimples.ComercioRevenda, 50_000m);
        // (300.000 × 7,3% − 5.940) ÷ 300.000 = 5,32% · DAS = 2.660,00 · CBS = 15,5% (PIS 2,76 + COFINS 12,74) = 412,30
        var dentro = Calc(T2027, 300_000m, false, receita);
        Assert.Equal(2_660m, dentro.ValorDas);
        Assert.Equal(412.30m, dentro.TotalPorTributo["CBS"]);
        Assert.Equal(0m, dentro.TotalPorTributo["IBS"]);
        Assert.False(dentro.IbsCbsRegimeRegular);
        Assert.Contains(dentro.Memoria, m => m.Contains("Repartição:") && m.Contains("CBS"));
        Assert.Contains(dentro.Memoria, m => m.Contains("IBS/CBS dentro do DAS"));
        Assert.Contains(dentro.Avisos, a => a.Contains("CGSN 190/2026"));

        var regular = Calc(T2027, 300_000m, true, receita);
        Assert.True(regular.IbsCbsRegimeRegular);
        Assert.Equal(2_247.70m, regular.ValorDas);                                  // 2.660,00 − 412,30
        Assert.Equal(412.30m, regular.IbsCbsForaDoDas);
        Assert.DoesNotContain("CBS", regular.TotalPorTributo.Keys);
        Assert.DoesNotContain("IBS", regular.TotalPorTributo.Keys);
        Assert.Equal(regular.ValorDas, regular.TotalPorTributo.Values.Sum());
        Assert.Contains(regular.Memoria, m => m.Contains("regime regular") && m.Contains("Apuração › IBS e CBS"));
        // a tabela de 2026 ignora a opção (não há IBS/CBS no DAS)
        Assert.Equal(2_660m, Calc(T2026, 300_000m, true, receita).ValorDas);
    }

    [Fact]
    public void Exportacao_tira_CBS_e_IBS_do_DAS_e_o_servico_do_Anexo_III_no_regime_regular_perde_so_a_CBS()
    {
        var exp = Calc(T2027, 300_000m, false, new ReceitaSimples(AtividadeSimples.ComercioRevenda, 50_000m, TratamentoSimples.Exportacao));
        Assert.DoesNotContain("CBS", exp.TotalPorTributo.Keys);
        Assert.DoesNotContain("ICMS", exp.TotalPorTributo.Keys);
        Assert.Equal(new[] { "CPP", "CSLL", "IRPJ" }, exp.TotalPorTributo.Keys.Order().ToArray());
        var servico = Calc(T2027, 300_000m, true, new ReceitaSimples(AtividadeSimples.ServicosAnexoIII, 50_000m));
        Assert.Equal(new[] { "CPP", "CSLL", "IRPJ", "ISS" }, servico.TotalPorTributo.Keys.Order().ToArray());
    }

    [Fact]
    public void Excecoes_do_sublimite_incluem_o_IBS_e_a_CBS_continua_no_DAS()
    {
        var acima = CalculadoraSimples.TributosExcluidos(TratamentoSimples.Normal, true, false, true);
        Assert.Superset(new HashSet<string> { "ICMS", "ISS", "IBS" }, acima.ToHashSet());
        Assert.DoesNotContain("CBS", acima);
        Assert.DoesNotContain("IBS", CalculadoraSimples.TributosExcluidos(TratamentoSimples.Normal, true));           // regra antiga: só ICMS e ISS
    }

    [Theory]
    [InlineData(3_500_000, 0, "")]
    [InlineData(3_900_000, 1, "a partir de 1º de janeiro do ano seguinte; neste ano continuam no DAS")]
    [InlineData(4_320_000, 1, "só a partir de 1º de janeiro")]
    [InlineData(4_500_000, 1, "a partir do mês seguinte ao excesso")]
    [InlineData(5_000_000, 1, "a exclusão vale a partir de 1º de janeiro do ano seguinte")]
    [InlineData(5_760_000, 1, "a exclusão vale a partir de 1º de janeiro do ano seguinte")]
    [InlineData(5_800_000, 1, "a exclusão do regime vale a partir do mês seguinte ao excesso")]
    public void Excesso_de_ate_20_por_cento_vale_no_ano_seguinte_e_acima_disso_no_mes_seguinte(int rbt12, int avisosDeLimite, string trecho)
    {
        var r = Calc(T2027, rbt12, false, new ReceitaSimples(AtividadeSimples.ComercioRevenda, 100_000m));
        var avisos = r.Avisos.Where(a => a.Contains("sublimite") || a.Contains("limite do Simples")).ToList();
        Assert.True(avisos.Count >= avisosDeLimite, string.Join(" | ", r.Avisos));
        if (trecho.Length > 0) Assert.Contains(avisos, a => a.Contains(trecho));
        if (rbt12 <= 3_600_000) Assert.Empty(avisos);
        // sem a regra de 20% (tabela antiga) o excesso do sublimite vale na hora
        if (rbt12 is > 3_600_000 and <= 4_800_000)
            Assert.Contains(Calc(T2026, rbt12, false, new ReceitaSimples(AtividadeSimples.ComercioRevenda, 100_000m)).Avisos, a => a.Contains("acima do sublimite"));
    }

    [Fact]
    public void Limites_de_exportacao_e_de_inicio_de_atividade_contam_o_mes_iniciado_como_inteiro()
    {
        var mar = new DateOnly(2027, 5, 1);
        // início em 20/03: março, abril e maio = 3 meses = R$ 900.000
        Assert.Empty(LimitesSimples.Avaliar(T2027, new EntradaLimitesSimples(mar, new DateOnly(2027, 3, 20), 900_000m, 0)));
        var excedeu = LimitesSimples.Avaliar(T2027, new EntradaLimitesSimples(mar, new DateOnly(2027, 3, 20), 900_000.01m, 0)).Single();
        Assert.Contains("3 mês(es)", excedeu);
        Assert.Contains("900.000,00", excedeu);
        // começou no último dia do mês: o mês conta inteiro
        Assert.Equal(1, LimitesSimples.MesesDeAtividadeNoAno(new DateOnly(2027, 5, 31), new DateOnly(2027, 5, 31)));
        Assert.NotEmpty(LimitesSimples.Avaliar(T2027, new EntradaLimitesSimples(new DateOnly(2027, 5, 1), new DateOnly(2027, 5, 31), 300_000.01m, 0)));
        // empresa de anos anteriores: sem limite proporcional
        Assert.Equal(0, LimitesSimples.MesesDeAtividadeNoAno(mar, new DateOnly(2020, 1, 1)));
        Assert.Empty(LimitesSimples.Avaliar(T2027, new EntradaLimitesSimples(mar, new DateOnly(2020, 1, 1), 5_000_000m, 0)));
        // exportação
        Assert.Empty(LimitesSimples.Avaliar(T2027, new EntradaLimitesSimples(mar, new DateOnly(2020, 1, 1), 0, 3_600_000m)));
        Assert.Contains("limite adicional", LimitesSimples.Avaliar(T2027, new EntradaLimitesSimples(mar, new DateOnly(2020, 1, 1), 0, 3_600_000.01m)).Single());
        Assert.Empty(LimitesSimples.Avaliar(T2026, new EntradaLimitesSimples(mar, new DateOnly(2027, 3, 1), 9_000_000m, 9_000_000m)));    // tabela antiga não tem esses limites
    }

    // ------------------------------------------------------------------ janelas de opção

    private static readonly JanelasOpcaoIbsCbs Janelas = JanelasOpcaoIbsCbs.Carregar(ParametrosEmbutidos.Json(ChavesParametro.SimplesOpcaoIbsCbs));
    private static DateOnly D(int a, int m, int d) => new(a, m, d);
    private static readonly DateOnly Antiga = D(2020, 1, 1);

    [Fact]
    public void Janelas_vem_do_parametro_legal()
    {
        Assert.Equal(new JanelaData(D(2027, 3, 1), D(2027, 3, 31)), Janelas.JanelaOpcao(2027, 2));
        Assert.Equal(new JanelaData(D(2028, 3, 1), D(2028, 3, 31)), Janelas.JanelaOpcao(2028, 2));
        Assert.Equal(new JanelaData(D(2027, 3, 1), D(2027, 5, 31)), Janelas.JanelaCancelamento(2027, 2));
        Assert.Equal(new JanelaData(D(2027, 9, 1), D(2027, 9, 30)), Janelas.JanelaOpcao(2028, 1));            // setembro do ano anterior
        Assert.Equal(new JanelaData(D(2026, 9, 1), D(2026, 10, 30)), Janelas.JanelaOpcao(2027, 1));           // exceção da Res. CGSN 194/2026
        Assert.Equal(new JanelaData(D(2026, 11, 3), D(2026, 12, 20)), Janelas.JanelaCancelamento(2027, 1));
        Assert.Null(Janelas.JanelaCancelamento(2028, 1));                                                     // ainda sem janela cadastrada
        Assert.Equal(new JanelaData(D(2026, 10, 1), D(2026, 12, 31)), Janelas.Abertura);
    }

    [Fact]
    public void Pedido_dentro_da_janela_nao_avisa_e_fora_avisa_sem_bloquear()
    {
        var regular = OpcaoIbsCbs.RegimeRegular;
        // 2º semestre de 2027: março
        Assert.Empty(Janelas.Verificar(regular, OpcaoIbsCbs.DentroDoDas, D(2027, 7, 1), D(2027, 3, 31), Antiga, false));
        var fora = Assert.Single(Janelas.Verificar(regular, OpcaoIbsCbs.DentroDoDas, D(2027, 7, 1), D(2027, 4, 2), Antiga, false));
        Assert.Contains("fora da janela", fora);
        Assert.Contains("01/03/2027 a 31/03/2027", fora);
        // 1º semestre de 2027 (prorrogado): até 30/10/2026
        Assert.Empty(Janelas.Verificar(regular, null, D(2027, 1, 1), D(2026, 10, 30), Antiga, true));
        Assert.Contains("fora da janela", Assert.Single(Janelas.Verificar(regular, null, D(2027, 1, 1), D(2026, 11, 5), Antiga, true)));
        // 1º semestre de 2028: setembro de 2027
        Assert.Empty(Janelas.Verificar(regular, OpcaoIbsCbs.DentroDoDas, D(2028, 1, 1), D(2027, 9, 15), Antiga, false));
        Assert.NotEmpty(Janelas.Verificar(regular, OpcaoIbsCbs.DentroDoDas, D(2028, 1, 1), D(2027, 10, 1), Antiga, false));
        // sem data do pedido
        Assert.Contains("Informe a data do pedido", Assert.Single(Janelas.Verificar(regular, null, D(2027, 7, 1), null, Antiga, false)));
    }

    [Fact]
    public void Cancelamento_usa_a_janela_propria_e_o_padrao_dentro_do_DAS_nao_precisa_de_pedido()
    {
        var dentro = OpcaoIbsCbs.DentroDoDas;
        Assert.Empty(Janelas.Verificar(dentro, OpcaoIbsCbs.RegimeRegular, D(2027, 7, 1), D(2027, 5, 31), Antiga, false));
        Assert.Contains("cancelamento", Assert.Single(Janelas.Verificar(dentro, OpcaoIbsCbs.RegimeRegular, D(2027, 7, 1), D(2027, 6, 1), Antiga, false)));
        Assert.Empty(Janelas.Verificar(dentro, OpcaoIbsCbs.RegimeRegular, D(2027, 1, 1), D(2026, 12, 20), Antiga, false));
        Assert.NotEmpty(Janelas.Verificar(dentro, OpcaoIbsCbs.RegimeRegular, D(2027, 1, 1), D(2026, 12, 21), Antiga, false));
        Assert.Contains("Não há janela cadastrada", Assert.Single(Janelas.Verificar(dentro, OpcaoIbsCbs.RegimeRegular, D(2028, 1, 1), D(2027, 10, 1), Antiga, false)));
        Assert.Empty(Janelas.Verificar(dentro, null, D(2027, 7, 1), null, Antiga, true));                     // padrão
        Assert.Empty(Janelas.Verificar(dentro, OpcaoIbsCbs.DentroDoDas, D(2027, 7, 1), null, Antiga, false));
    }

    [Fact]
    public void Empresa_aberta_de_outubro_a_dezembro_de_2026_escolhe_na_inscricao_sem_prazo()
    {
        Assert.Empty(Janelas.Verificar(OpcaoIbsCbs.RegimeRegular, null, D(2027, 1, 1), D(2026, 12, 28), D(2026, 11, 10), true));
        Assert.NotEmpty(Janelas.Verificar(OpcaoIbsCbs.RegimeRegular, null, D(2027, 1, 1), D(2026, 12, 28), D(2026, 8, 10), true));   // aberta antes: vale a janela
    }
}

public class Simples2027IntegracaoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private static string ComIbsCbs(string xml, decimal baseItem, bool simples) => xml.Replace("</COFINS></imposto>",
        $"</COFINS><IBSCBS><CST>000</CST><cClassTrib>000001</cClassTrib><gIBSCBS><vBC>{baseItem:0.00}</vBC>" +
        $"<gIBSUF><pIBSUF>0.0500</pIBSUF><vIBSUF>{baseItem * 0.0005m:0.00}</vIBSUF></gIBSUF><gIBSMun><pIBSMun>0.0500</pIBSMun><vIBSMun>{baseItem * 0.0005m:0.00}</vIBSMun></gIBSMun>" +
        $"<vIBS>{baseItem * 0.001m:0.00}</vIBS><gCBS><pCBS>11.2000</pCBS><vCBS>{baseItem * 0.112m:0.00}</vCBS></gCBS></gIBSCBS></IBSCBS></imposto>")
        .Replace("<IE>111222</IE></emit>", $"<IE>111222</IE><CRT>{(simples ? 1 : 3)}</CRT></emit>");

    [Fact]
    public async Task Historico_da_opcao_por_semestre_vigencia_janela_e_permissao()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await db.CriarEmpresaAsync(CnpjsDeTeste.Valido(121));
        var adm = await db.AdminAsync();
        var svc = db.App.Fiscal().Opcao;
        var jan = new DateOnly(2027, 1, 1);
        var jul = new DateOnly(2027, 7, 1);

        Assert.Equal(OpcaoIbsCbs.DentroDoDas, await svc.NaDataAsync(empresa, new DateOnly(2027, 5, 1), ct));            // padrão
        Assert.False((await svc.GravarAsync(empresa, new OpcaoIbsCbsEmpresa(0, new DateOnly(2026, 7, 1), null, OpcaoIbsCbs.RegimeRegular, null, null, null), adm, ct)).Sucesso);

        var primeira = (await svc.GravarAsync(empresa, new OpcaoIbsCbsEmpresa(0, new DateOnly(2027, 3, 15), null, OpcaoIbsCbs.DentroDoDas, null, null, null), adm, ct)).Valor;
        Assert.Equal(jan, primeira.Opcao.VigenciaInicio);                                                             // ajustada ao início do semestre
        Assert.Empty(primeira.Avisos);                                                                                // dentro do DAS é o padrão

        var regular = await svc.GravarAsync(empresa, new OpcaoIbsCbsEmpresa(0, jul, null, OpcaoIbsCbs.RegimeRegular, new DateOnly(2027, 3, 15), "PROT-123", "Pedido no portal"), adm, ct);
        Assert.True(regular.Sucesso, string.Join(";", regular.Erros));
        Assert.Empty(regular.Valor.Avisos);                                                                           // março: dentro da janela
        var lista = (await svc.ListarAsync(empresa, adm, ct)).Valor;
        Assert.Equal([jul, jan], lista.Select(o => o.VigenciaInicio));
        Assert.Equal(new DateOnly(2027, 6, 30), lista.Single(o => o.VigenciaInicio == jan).VigenciaFim);              // a anterior fecha no dia anterior
        Assert.Equal(("PROT-123", "2º semestre de 2027"), (lista[0].Protocolo, lista[0].SemestreTexto));
        Assert.Equal(OpcaoIbsCbs.DentroDoDas, await svc.NaDataAsync(empresa, new DateOnly(2027, 6, 30), ct));
        Assert.Equal(OpcaoIbsCbs.RegimeRegular, await svc.NaDataAsync(empresa, new DateOnly(2027, 7, 1), ct));
        Assert.Equal(OpcaoIbsCbs.RegimeRegular, await svc.NaDataAsync(empresa, new DateOnly(2030, 1, 1), ct));          // vale até ser renunciada

        // pedido fora da janela: grava, mas avisa; o cancelamento do semestre seguinte usa a janela de cancelamento
        var tarde = await svc.GravarAsync(empresa, new OpcaoIbsCbsEmpresa(0, new DateOnly(2028, 7, 1), null, OpcaoIbsCbs.DentroDoDas, new DateOnly(2028, 6, 10), null, null), adm, ct);
        Assert.True(tarde.Sucesso);
        Assert.Contains("fora da janela", Assert.Single(tarde.Valor.Avisos));
        Assert.Equal(OpcaoIbsCbs.DentroDoDas, await svc.NaDataAsync(empresa, new DateOnly(2028, 8, 1), ct));
        Assert.False((await svc.GravarAsync(empresa, new OpcaoIbsCbsEmpresa(0, jul, null, OpcaoIbsCbs.RegimeRegular, null, new string('x', 61), null), adm, ct)).Sucesso);

        // excluir devolve o fim em aberto da anterior
        Assert.True((await svc.ExcluirAsync(empresa, tarde.Valor.Opcao.Id, adm, ct)).Sucesso);
        Assert.Null((await svc.ListarAsync(empresa, adm, ct)).Valor.Single(o => o.VigenciaInicio == jul).VigenciaFim);
        Assert.Equal(OpcaoIbsCbs.RegimeRegular, await svc.NaDataAsync(empresa, new DateOnly(2028, 8, 1), ct));

        // permissões: consulta lê mas não grava
        var sessaoAdm = new SessaoUsuario(adm, "a", "A", true, false);
        var consulta = await db.PerfilIdAsync("Consulta");
        var uid = (await db.App.Usuarios.CriarUsuarioAsync(sessaoAdm, "opcao_leitor", "L", "Inicial123", consulta, ct)).Valor;
        await db.App.Usuarios.DefinirAcessoAsync(sessaoAdm, uid, empresa, consulta, ct);
        Assert.False((await svc.GravarAsync(empresa, new OpcaoIbsCbsEmpresa(0, jan, null, OpcaoIbsCbs.RegimeRegular, null, null, null), uid, ct)).Sucesso);
        Assert.False((await svc.ExcluirAsync(empresa, lista[0].Id, uid, ct)).Sucesso);
    }

    [Fact]
    public async Task DAS_de_janeiro_de_2027_usa_a_repartição_nova_e_exclui_IBS_CBS_no_regime_regular()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var empresa = await db.CriarEmpresaAsync(CnpjsDeTeste.Valido(122));
        var adm = await db.AdminAsync();
        var fiscal = db.App.Fiscal();
        var jan = new DateOnly(2027, 1, 1);
        var receitas = new[] { new ReceitaSimples(AtividadeSimples.ComercioRevenda, 50_000m) };

        var dentro = (await fiscal.Simples.CalcularAsync(empresa, jan, receitas, 300_000m, 0, adm, ct)).Valor;
        Assert.Equal((2_660m, 412.30m, false), (dentro.ValorDas, dentro.TotalPorTributo["CBS"], dentro.IbsCbsRegimeRegular));
        Assert.Equal(0, dentro.TotalPorTributo.GetValueOrDefault("PIS") + dentro.TotalPorTributo.GetValueOrDefault("COFINS"));
        // dezembro de 2026 ainda usa PIS/COFINS
        var dez = (await fiscal.Simples.CalcularAsync(empresa, new DateOnly(2026, 12, 1), receitas, 300_000m, 0, adm, ct)).Valor;
        Assert.True(dez.TotalPorTributo.ContainsKey("COFINS"));
        Assert.False(dez.TotalPorTributo.ContainsKey("CBS"));

        Assert.True((await fiscal.Opcao.GravarAsync(empresa, new OpcaoIbsCbsEmpresa(0, jan, null, OpcaoIbsCbs.RegimeRegular, new DateOnly(2026, 10, 20), null, null), adm, ct)).Sucesso);
        var regular = (await fiscal.Simples.CalcularAsync(empresa, jan, receitas, 300_000m, 0, adm, ct)).Valor;
        Assert.Equal((2_247.70m, 412.30m, true), (regular.ValorDas, regular.IbsCbsForaDoDas, regular.IbsCbsRegimeRegular));
        var salva = (await fiscal.Simples.SalvarAsync(empresa, jan, receitas, 300_000m, 0, adm, ct)).Valor;
        var lida = (await fiscal.Simples.ListarAsync(empresa, 2027, adm, ct)).Valor.Single();
        Assert.Equal((2_247.70m, true, 412.30m), (lida.ValorDas, lida.Resultado.IbsCbsRegimeRegular, lida.Resultado.IbsCbsForaDoDas));
        Assert.Equal(salva.Resultado.Memoria, lida.Resultado.Memoria);
        Assert.DoesNotContain(dez.Avisos, a => a.Contains("CGSN 190"));
        Assert.Contains(lida.Resultado.Avisos, a => a.Contains("CGSN 190"));                                          // repartição ainda a conferir
    }

    [Fact]
    public async Task Credito_das_entradas_de_fornecedor_do_Simples_aparece_separado_e_a_conferencia_do_PGDAS_traz_o_IBS_CBS()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var cnpj = CnpjsDeTeste.Valido(123);
        var empresa = await db.CriarEmpresaAsync(cnpj);
        var adm = await db.AdminAsync();
        var fiscal = db.App.Fiscal();
        Assert.Equal(1L, await db.EscalarAsync<long>("WITH u AS (UPDATE core.empresa_regime SET regime = 'SIMPLES_NACIONAL' WHERE empresa_id = @e RETURNING 1) SELECT count(*) FROM u", ("e", empresa)));
        var dia = new DateOnly(2027, 3, 10);
        var venda = ComIbsCbs(XmlsDeTeste.Nfe(cnpj, XmlsDeTeste.CnpjCliente, 1, 71, dia, [new ItemNfe("5102", 10_000m)]), 10_000m, false);
        var compraSimples = ComIbsCbs(XmlsDeTeste.Nfe(XmlsDeTeste.CnpjFornecedor, cnpj, 1, 72, dia, [new ItemNfe("5102", 4_000m)]), 4_000m, true);
        var compraRegular = ComIbsCbs(XmlsDeTeste.Nfe(XmlsDeTeste.CnpjFornecedor, cnpj, 1, 73, dia, [new ItemNfe("5102", 1_000m)]), 1_000m, false);
        var imp = await fiscal.Documentos.ImportarAsync(empresa, [new ArquivoXml("v.xml", XmlsDeTeste.Bytes(venda)), new ArquivoXml("c1.xml", XmlsDeTeste.Bytes(compraSimples)),
            new ArquivoXml("c2.xml", XmlsDeTeste.Bytes(compraRegular))], adm, ct: ct);
        Assert.Equal(3, imp.Valor.Importados);
        var marcados = await db.EscalarAsync<bool[]>("SELECT array_agg(DISTINCT emitente_simples ORDER BY emitente_simples DESC) FROM fiscal.documento WHERE empresa_id = @e AND tipo_operacao = 'E'", ("e", empresa));
        Assert.Equal(new[] { true, false }, marcados);

        var a = (await fiscal.Reforma.ApurarAsync(empresa, dia, adm, ct)).Valor!;
        Assert.Equal((2, 5_000m * 0.112m), (a.Creditos.Notas, a.Creditos.Cbs));                                       // crédito de todas as entradas, pelo grupo da nota
        Assert.Equal((1, 4_000m * 0.112m, 4m), (a.CreditosDeSimples.Notas, a.CreditosDeSimples.Cbs, a.CreditosDeSimples.Ibs));
        Assert.Contains(a.Avisos(), x => x.Contains("fornecedores do Simples Nacional"));
        Assert.Contains(a.Avisos(), x => x.Contains("dentro do DAS neste semestre"));
        Assert.Contains(a.Tabela().Linhas, l => l.Valores[0] is string s && s.Contains("dos quais de fornecedores do Simples"));
        await fiscal.Opcao.GravarAsync(empresa, new OpcaoIbsCbsEmpresa(0, new DateOnly(2027, 1, 1), null, OpcaoIbsCbs.RegimeRegular, new DateOnly(2026, 10, 20), null, null), adm, ct);
        Assert.Contains((await fiscal.Reforma.ApurarAsync(empresa, dia, adm, ct)).Valor!.Avisos(), x => x.Contains("regime regular do IBS e da CBS"));

        // conferência do PGDAS-D: a receita é a da nota sem o IBS/CBS (regime regular) e traz o IBS/CBS dos documentos
        var conf = (await fiscal.Simples.ConferirPgdasAsync(empresa, dia, AtividadeSimples.ServicosFatorR, adm, ct)).Valor;
        var linha = Assert.Single(conf.Linhas);
        Assert.Equal((AnexoSimples.I, AtividadeSimples.ComercioRevenda, TratamentoSimples.Normal), (linha.Anexo, linha.Atividade, linha.Tratamento));
        Assert.Equal((10_000m - 1_120m - 10m, 1_120m, 10m), (linha.Receita, linha.Cbs, linha.Ibs));                    // regime regular: desconta IBS e CBS
        Assert.True(conf.RegimeRegular);
        Assert.Equal(2, conf.Tabelas().Count);
        Assert.Contains(conf.Avisos, x => x.Contains("pré-preencher"));
        Assert.Contains(conf.Avisos, x => x.Contains("emissão"));
    }
}
