using System.Text;
using Npgsql;
using Vorticis.Application.Cadastros;
using Vorticis.Application.Contabil;
using Vorticis.Application.Sped;
using Vorticis.Application.Sped.Importacao;
using Vorticis.Domain.Contabil;
using Vorticis.Domain.Seguranca;
using Vorticis.Domain.Sped.Importacao;

namespace Vorticis.Infra.Tests;

/// <summary>Importação de arquivo SPED – ECF (ida e volta com o gerador do projeto): e-Lalur/e-Lacs, Parte B, apurações trimestrais, plano de contas, CNPJ e desfazer.</summary>
public class ImportacaoSpedEcfTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private const string CpfContador = "52998224725";
    private const string CpfSocia = "39053344705";
    private const string Referencial = """
        CODIGO|DESCRICAO|TIPO|COD_SUP|NIVEL|COD_NAT
        1|ATIVO|S||1|01
        1.01|Ativo circulante|S|1|2|01
        1.01.01|Disponibilidades e outros|A|1.01|3|01
        2|PASSIVO E PATRIMÔNIO LÍQUIDO|S||1|02
        2.01|Passivo circulante|S|2|2|02
        2.01.01|Obrigações|A|2.01|3|02
        2.03|PATRIMÔNIO LÍQUIDO|S|2|2|03
        2.03.01|Capital social|A|2.03|3|03
        2.03.02|Lucros ou prejuízos acumulados|A|2.03|3|03
        3|RESULTADO LÍQUIDO DO PERÍODO|S||1|04
        3.01|RESULTADO ANTES DO IRPJ E DA CSLL|S|3|2|04
        3.01.01|Receitas|A|3.01|3|04
        3.01.02|Custos e despesas|A|3.01|3|04
        3.02|PROVISÃO PARA CSLL E IRPJ|S|3|2|04
        3.02.01.01.01|Provisão para CSLL e IRPJ|S|3.02|3|04
        3.02.01.01.01.01|Provisão para CSLL|A|3.02.01.01.01|4|04
        3.02.01.01.01.02|Provisão para IRPJ|A|3.02.01.01.01|4|04
        """;

    private static string? Ref(string codigo, string nat) => nat switch
    {
        "01" => "1.01.01",
        "02" => "2.01.01",
        "03" => codigo.StartsWith("2.3.01") ? "2.03.01" : "2.03.02",
        "04" => codigo.StartsWith("4.5.01.002") ? "3.02.01.01.01.01" : codigo.StartsWith("4.5.01.001") ? "3.02.01.01.01.02" : codigo.StartsWith("3.1") ? "3.01.01" : "3.01.02",
        _ => null,
    };

    private async Task Lancar(long empresa, long adm, DateOnly data, string debito, string credito, decimal valor, string hist)
        => Assert.True((await db.App.Lancamentos.EfetivarAsync(new LancamentoProposto(empresa, data, OrigemLancamento.Manual,
            [new(debito, IndicadorDC.D, valor, hist, null), new(credito, IndicadorDC.C, valor, hist)]), adm, TestContext.Current.CancellationToken)).Sucesso);

    private async Task<(long Empresa, long Adm)> PrepararAsync(string cnpj, string codPlanRef, string regime)
    {
        var ct = TestContext.Current.CancellationToken;
        var app = db.App;
        var empresa = await db.CriarEmpresaAsync(cnpj, 2025);
        var adm = await db.AdminAsync();
        await db.EscalarAsync<int>("""
            UPDATE core.empresa SET nire = '35212345678', municipio = 'SÃO PAULO', cep = '01310100', logradouro = 'AVENIDA PAULISTA', numero = '1000',
                   bairro = 'BELA VISTA', email = 'contato@exemplo.com.br', telefone = '(11) 3333-4444' WHERE id = @e RETURNING 1
            """, ("e", empresa));
        await db.EscalarAsync<int>("UPDATE core.empresa_regime SET regime = @r::core.regime_tributario WHERE empresa_id = @e RETURNING 1", ("r", regime), ("e", empresa));
        var admin = new SessaoUsuario(adm, "admin_teste", "Admin", true, false);
        await app.PlanoReferencial.ImportarAsync(admin, codPlanRef, 2025, Encoding.Latin1.GetBytes(Referencial), "ref.txt", ct);
        Assert.True((await app.PlanoReferencial.DefinirPlanoAsync(empresa, 2025, codPlanRef, adm, ct)).Sucesso);
        var mapa = (await app.PlanoReferencial.CarregarAsync(empresa, 2025, adm, ct)).Valor.Contas;
        Assert.True((await app.PlanoReferencial.VincularAsync(empresa, 2025, mapa.ToDictionary(c => c.ContaId, c => Ref(c.Codigo, c.CodNat)), adm, ct)).Sucesso);
        Assert.True((await app.Responsaveis.VincularContadorAsync(empresa, new Contador(0, "José da Silva Contador", CpfContador, "1SP123456", "SP",
            "contador@exemplo.com.br", "(11) 3333-4444", null), new DateOnly(2020, 1, 1), true, adm, ct)).Sucesso);
        Assert.True((await app.Responsaveis.SalvarSocioAsync(empresa, new Socio(0, "Maria Sócia", 'F', CpfSocia, null, "Sócio-Administrador", 49, 100m,
            null, null, true, "maria@exemplo.com.br", "11999998888"), adm, ct)).Sucesso);
        await Lancar(empresa, adm, new(2025, 1, 2), "1.1.01.002", "2.3.01.001", 100_000m, "Integralização do capital");
        return (empresa, adm);
    }

    private async Task Apuracao(long empresa, string regime, int tri, decimal receita, decimal? lucro, decimal adIrpj, decimal exIrpj, decimal adCsll, decimal exCsll,
                                decimal compIrpj, decimal compCsll, decimal prejIrpj, decimal prejCsll, decimal baseIrpj, decimal baseCsll, decimal normal, decimal adicional,
                                decimal dedIrpj, decimal csll, decimal dedCsll, string memoria = "")
    {
        var ini = new DateOnly(2025, tri * 3 - 2, 1);
        var fim = ini.AddMonths(3).AddDays(-1);
        var json = "{\"memoria\":[" + (memoria.Length == 0 ? "" : string.Join(",", memoria.Split('\n').Select(l => "\"" + l + "\""))) + "]}";
        await db.EscalarAsync<int>("""
            INSERT INTO fiscal.apuracao_lucro (empresa_id, regime, ano, trimestre, periodo_inicio, periodo_fim, receita_bruta, lucro_contabil, adicoes_irpj, exclusoes_irpj,
                adicoes_csll, exclusoes_csll, compensacao_irpj, compensacao_csll, prejuizo_irpj, prejuizo_csll, base_irpj, base_csll, irpj_normal, irpj_adicional,
                irpj_deducoes, irpj_a_pagar, csll_devida, csll_deducoes, csll_a_pagar, vencimento, resultado)
            VALUES (@e, @reg, 2025, @t, @ini, @fim, @rec, @ll, @ai, @ei, @ac, @ec, @ci, @cc, @pi, @pc, @bi, @bc, @n, @ad, @di, @ip, @cs, @dc, @cp, @fim, @res::jsonb)
            RETURNING 1
            """, ("e", empresa), ("reg", regime), ("t", (short)tri), ("ini", ini), ("fim", fim), ("rec", receita), ("ll", (object?)lucro ?? DBNull.Value),
            ("ai", adIrpj), ("ei", exIrpj), ("ac", adCsll), ("ec", exCsll), ("ci", compIrpj), ("cc", compCsll), ("pi", prejIrpj), ("pc", prejCsll), ("bi", baseIrpj),
            ("bc", baseCsll), ("n", normal), ("ad", adicional), ("di", dedIrpj), ("ip", Math.Max(0, normal + adicional - dedIrpj)), ("cs", csll), ("dc", dedCsll),
            ("cp", Math.Max(0, csll - dedCsll)), ("res", json));
    }

    private async Task Lalur(long empresa, int tri, string tributo, string tipo, string desc, decimal valor, string codigo, string? contaCodigo)
        => await db.EscalarAsync<int>("""
            INSERT INTO fiscal.lalur_lancamento (empresa_id, ano, trimestre, tributo, tipo, descricao, valor, codigo_ecf, conta_id)
            VALUES (@e, 2025, @t, @tr, @tp, @d, @v, @c, (SELECT id FROM contabil.plano_contas WHERE empresa_id = @e AND codigo = @cc))
            RETURNING 1
            """, ("e", empresa), ("t", (short)tri), ("tr", tributo), ("tp", tipo), ("d", desc), ("v", valor), ("c", codigo), ("cc", (object?)contaCodigo ?? DBNull.Value));

    private async Task ParteB(long empresa, string tributo, string tipo, int? ano, int? tri, decimal valor)
        => await db.EscalarAsync<int>("""
            INSERT INTO fiscal.lalur_parte_b (empresa_id, tributo, tipo, ano, trimestre, valor, apuracao_id)
            VALUES (@e, @tr, @tp, @a, @t, @v, (SELECT id FROM fiscal.apuracao_lucro WHERE empresa_id = @e AND ano = @a AND trimestre = @t)) RETURNING 1
            """, ("e", empresa), ("tr", tributo), ("tp", tipo), ("a", (object?)(ano is null ? null : (short)ano) ?? DBNull.Value),
            ("t", (object?)(tri is null ? null : (short)tri) ?? DBNull.Value), ("v", valor));

    /// <summary>Lucro Real trimestral: lucro no 1º tri, prejuízo no 2º, lucro com compensação no 3º e lucro com exclusão no 4º.</summary>
    private async Task<(long Empresa, long Adm, byte[] Arquivo)> RealAsync(string cnpj)
    {
        var ct = TestContext.Current.CancellationToken;
        var (e, a) = await PrepararAsync(cnpj, "1", "LUCRO_REAL");
        Task Receita(int mes, int dia, decimal v) => Lancar(e, a, new(2025, mes, dia), "1.1.01.002", "3.1.01.003", v, "Serviços prestados");
        Task Despesa(int mes, int dia, decimal v) => Lancar(e, a, new(2025, mes, dia), "4.2.02.008", "1.1.01.002", v, "Serviços de terceiros");
        Task ProvIrpj(int mes, int dia, decimal v) => Lancar(e, a, new(2025, mes, dia), "4.5.01.001", "2.1.04.003", v, "Provisão do IRPJ");
        Task ProvCsll(int mes, int dia, decimal v) => Lancar(e, a, new(2025, mes, dia), "4.5.01.002", "2.1.04.003", v, "Provisão da CSLL");
        await Receita(2, 10, 50_000m); await Despesa(3, 15, 20_000m); await ProvCsll(3, 31, 2_790m); await ProvIrpj(3, 31, 4_650m);
        await Receita(4, 10, 10_000m); await Despesa(5, 20, 40_000m);
        await Receita(7, 10, 60_000m); await Despesa(8, 20, 15_000m); await ProvCsll(9, 30, 2_835m); await ProvIrpj(9, 30, 4_725m);
        await Receita(10, 10, 20_000m); await Despesa(11, 20, 10_000m); await ProvCsll(12, 30, 720m); await ProvIrpj(12, 30, 1_200m);
        Assert.True((await db.App.Encerramento.EncerrarAsync(e, 2025, new ContasEncerramento(), a, ct)).Sucesso);
        await Lalur(e, 1, "AMBOS", "ADICAO", "Multa de trânsito", 1_000m, "8.65", "4.2.02.008");
        await Lalur(e, 4, "AMBOS", "EXCLUSAO", "Juros sobre o capital próprio", 2_000m, "166.03", null);
        await Apuracao(e, "REAL", 1, 50_000m, 30_000m, 1_000m, 0, 1_000m, 0, 0, 0, 0, 0, 31_000m, 31_000m, 4_650m, 0, 0, 2_790m, 0);
        await Apuracao(e, "REAL", 2, 10_000m, -30_000m, 0, 0, 0, 0, 0, 0, 30_000m, 30_000m, 0, 0, 0, 0, 0, 0, 0);
        await Apuracao(e, "REAL", 3, 60_000m, 45_000m, 0, 0, 0, 0, 13_500m, 13_500m, 0, 0, 31_500m, 31_500m, 4_725m, 0, 500m, 2_835m, 100m);
        await Apuracao(e, "REAL", 4, 20_000m, 10_000m, 0, 2_000m, 0, 2_000m, 0, 0, 0, 0, 8_000m, 8_000m, 1_200m, 0, 0, 720m, 0);
        await ParteB(e, "IRPJ", "SALDO_INICIAL", null, null, 10_000m);
        await ParteB(e, "IRPJ", "PREJUIZO", 2025, 2, 30_000m); await ParteB(e, "IRPJ", "COMPENSACAO", 2025, 3, -13_500m);
        await ParteB(e, "CSLL", "PREJUIZO", 2025, 2, 30_000m); await ParteB(e, "CSLL", "COMPENSACAO", 2025, 3, -13_500m);
        var r = await db.App.Ecf.GerarAsync(e, 2025, new OpcoesEcf(), a, ct: ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        return (e, a, r.Valor.Conteudo);
    }

    private async Task<List<string>> ListaAsync(string sql, long empresa)
    {
        var res = new List<string>();
        await using var con = new NpgsqlConnection(db.ConnectionString);
        await con.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("e", empresa);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            res.Add(string.Join('|', Enumerable.Range(0, r.FieldCount).Select(i => r.IsDBNull(i) ? "" : r.GetValue(i) is decimal d ? d.ToString("0.00") : r.GetValue(i).ToString())));
        return res;
    }

    private const string SqlLalur = "SELECT ano, trimestre, tributo, tipo, descricao, valor, codigo_ecf FROM fiscal.lalur_lancamento WHERE empresa_id = @e ORDER BY trimestre, tributo, codigo_ecf";
    private const string SqlParteB = "SELECT tributo, tipo, COALESCE(ano, 0), COALESCE(trimestre, 0), valor FROM fiscal.lalur_parte_b WHERE empresa_id = @e ORDER BY tributo, tipo, trimestre";
    private const string SqlApuracao = """
        SELECT regime, trimestre, base_irpj, base_csll, irpj_normal, irpj_adicional, irpj_deducoes, csll_devida, csll_deducoes, compensacao_irpj, compensacao_csll,
               prejuizo_irpj, prejuizo_csll, adicoes_irpj, exclusoes_irpj, adicoes_csll, exclusoes_csll
          FROM fiscal.apuracao_lucro WHERE empresa_id = @e ORDER BY trimestre
        """;

    [Fact]
    public async Task Ida_e_volta_da_ecf_lucro_real_reproduz_lalur_parte_b_e_apuracoes()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var (origem, adm, arquivo) = await RealAsync("10108632000127");
        var lalurOrigem = await ListaAsync(SqlLalur, origem);
        var parteBOrigem = await ListaAsync(SqlParteB, origem);
        var apuracaoOrigem = await ListaAsync(SqlApuracao, origem);
        Assert.Equal(2, lalurOrigem.Count);
        await db.EscalarAsync<int>("SELECT core.fn_excluir_empresa(@e)", ("e", origem));
        var destino = await db.CriarEmpresaAsync("10108632000127", 2025);

        var op = new OpcoesImportacaoSped("TUDO", SubstituirPlano: true);
        var previa = await db.App.ImportacaoEcf.PreviaAsync(destino, arquivo, "ecf.txt", op, adm, ct);
        Assert.True(previa.Sucesso, string.Join("; ", previa.Erros));
        Assert.True(previa.Valor.CnpjConfere);
        Assert.True(previa.Valor.PodeImportar, string.Join("; ", previa.Valor.Ocorrencias.Where(o => o.Gravidade == GravidadeOcorrencia.Erro).Select(o => o.Motivo)));
        var linhas = previa.Valor.Linhas.ToDictionary(l => l.Registro);
        Assert.Equal(2, linhas["M300"].Novos);
        Assert.Equal(4, linhas["N630"].Novos);
        Assert.True(linhas["J050"].Novos > 5);
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.apuracao_lucro WHERE empresa_id = @e", ("e", destino)));

        var rel = await db.App.ImportacaoEcf.ImportarAsync(destino, arquivo, "ecf.txt", op, adm, null, ct);
        Assert.True(rel.Sucesso, string.Join("; ", rel.Erros));
        Assert.Equal(lalurOrigem, await ListaAsync(SqlLalur, destino));
        Assert.Equal(apuracaoOrigem, await ListaAsync(SqlApuracao, destino));
        // Parte B: o saldo inicial e os movimentos voltam (a conta da CSLL sem saldo não gera saldo inicial)
        Assert.Equal(parteBOrigem, await ListaAsync(SqlParteB, destino));
        Assert.Equal(4L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.lalur_parte_b WHERE empresa_id = @e AND apuracao_id IS NOT NULL", ("e", destino)));
        Assert.True(await db.EscalarAsync<long>("SELECT count(*) FROM contabil.plano_contas WHERE empresa_id = @e AND codigo = '4.5.01.002'", ("e", destino)) == 1);
        Assert.Equal(1L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.lalur_lancamento WHERE empresa_id = @e AND conta_id IS NOT NULL", ("e", destino)));   // 8.65 ligado à conta de despesa

        var hist = Assert.Single(await db.App.ImportacaoEcf.HistoricoAsync(destino, adm, ct));
        Assert.Equal("ECF", hist.Tipo);

        // o mesmo arquivo não entra duas vezes
        var dup = await db.App.ImportacaoEcf.ImportarAsync(destino, arquivo, "ecf.txt", op, adm, null, ct);
        Assert.False(dup.Sucesso);

        // desfazer: some tudo o que a importação criou
        var des = await db.App.ImportacaoEcf.DesfazerAsync(destino, rel.Valor.ImportacaoId, adm, ct);
        Assert.True(des.Sucesso, string.Join("; ", des.Erros));
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.apuracao_lucro WHERE empresa_id = @e", ("e", destino)));
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.lalur_lancamento WHERE empresa_id = @e", ("e", destino)));
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.lalur_parte_b WHERE empresa_id = @e", ("e", destino)));
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM contabil.plano_contas WHERE empresa_id = @e AND codigo = '4.5.01.002' AND descricao <> ''", ("e", destino)) - 1);   // plano anterior (modelo) devolvido
        var de_novo = await db.App.ImportacaoEcf.ImportarAsync(destino, arquivo, "ecf.txt", new OpcoesImportacaoSped("LALUR"), adm, null, ct);
        Assert.True(de_novo.Sucesso, string.Join("; ", de_novo.Erros));
        Assert.Equal(lalurOrigem, await ListaAsync(SqlLalur, destino));
    }

    [Fact]
    public async Task Ecf_de_outro_cnpj_ou_de_outro_tipo_e_recusada_e_apuracao_existente_e_mantida()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var (origem, adm, arquivo) = await RealAsync("10122211000150");
        var outra = await db.CriarEmpresaAsync("10135790000176", 2025);
        var p = await db.App.ImportacaoEcf.PreviaAsync(outra, arquivo, "ecf.txt", new OpcoesImportacaoSped("TUDO"), adm, ct);
        Assert.True(p.Sucesso);
        Assert.False(p.Valor.CnpjConfere);
        Assert.Equal(origem, p.Valor.EmpresaCorrespondenteId);
        Assert.False((await db.App.ImportacaoEcf.ImportarAsync(outra, arquivo, "ecf.txt", new OpcoesImportacaoSped("TUDO"), adm, null, ct)).Sucesso);
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.apuracao_lucro WHERE empresa_id = @e", ("e", outra)));

        // tipo errado
        var tipo = await db.App.ImportacaoEcd.PreviaAsync(origem, arquivo, "ecf.txt", new OpcoesImportacaoSped("TUDO"), adm, ct);
        Assert.False(tipo.Sucesso);
        Assert.Contains("ECF", string.Join(" ", tipo.Erros));

        // na própria empresa de origem tudo já existe: nada é duplicado
        var antesAp = await ListaAsync(SqlApuracao, origem); var antesLal = await ListaAsync(SqlLalur, origem); var antesB = await ListaAsync(SqlParteB, origem);
        var previa = (await db.App.ImportacaoEcf.PreviaAsync(origem, arquivo, "ecf.txt", new OpcoesImportacaoSped("LALUR"), adm, ct)).Valor;
        Assert.All(previa.Linhas.Where(l => l.Registro is "N630" or "N670" or "M300"), l => Assert.Equal(0, l.Novos));
        var r = await db.App.ImportacaoEcf.ImportarAsync(origem, arquivo, "ecf.txt", new OpcoesImportacaoSped("LALUR"), adm, null, ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        Assert.Equal(antesAp, await ListaAsync(SqlApuracao, origem));
        Assert.Equal(antesLal, await ListaAsync(SqlLalur, origem));
        Assert.Equal(antesB, await ListaAsync(SqlParteB, origem));

        // sem permissão (perfil Consulta)
        var consulta = await db.EscalarAsync<long>("INSERT INTO core.usuario (login, nome, senha_hash, perfil_id) VALUES ('consulta_imp_ecf', 'Consulta', 'x', (SELECT id FROM core.perfil WHERE nome = 'Consulta')) RETURNING id");
        Assert.False((await db.App.ImportacaoEcf.PreviaAsync(origem, arquivo, "ecf.txt", new OpcoesImportacaoSped("TUDO"), consulta, ct)).Sucesso);
    }

    [Fact]
    public async Task Ida_e_volta_da_ecf_lucro_presumido_reproduz_as_apuracoes_trimestrais()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var (e, a) = await PrepararAsync("10149369000114", "2", "LUCRO_PRESUMIDO");
        for (var t = 1; t <= 4; t++)
        {
            var mes = t * 3 - 2;
            await Lancar(e, a, new(2025, mes + 1, 10), "1.1.01.002", "3.1.01.003", t == 1 ? 101_000m : 100_000m, "Serviços prestados");
            await Lancar(e, a, new(2025, mes + 2, 15), "4.2.02.008", "1.1.01.002", 30_000m, "Serviços de terceiros");
        }
        Assert.True((await db.App.Encerramento.EncerrarAsync(e, 2025, new ContasEncerramento(), a, ct)).Sucesso);
        const string memoria = "Lucro Presumido · trimestre\nPrestação de serviços em geral, intermediação, administração, locação e cessão de bens: receita 100.000,00 × 32% = base IRPJ 32.000,00 · × 32% = base CSLL 32.000,00";
        await Apuracao(e, "PRESUMIDO", 1, 100_000m, null, 0, 0, 0, 0, 0, 0, 0, 0, 33_000m, 33_000m, 4_950m, 0, 500m, 2_970m, 0,
            memoria + "\nOutras receitas (ganhos de capital, rendimentos financeiros etc.) integram 100% das bases: 1.000,00");
        for (var t = 2; t <= 4; t++) await Apuracao(e, "PRESUMIDO", t, 100_000m, null, 0, 0, 0, 0, 0, 0, 0, 0, 32_000m, 32_000m, 4_800m, 0, 0, 2_880m, 0, memoria);
        var r = await db.App.Ecf.GerarAsync(e, 2025, new OpcoesEcf(), a, ct: ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        const string sql = "SELECT regime, trimestre, receita_bruta, base_irpj, base_csll, irpj_normal, irpj_adicional, irpj_deducoes, csll_devida, csll_deducoes FROM fiscal.apuracao_lucro WHERE empresa_id = @e ORDER BY trimestre";
        var origem = await ListaAsync(sql, e);
        await db.EscalarAsync<int>("SELECT core.fn_excluir_empresa(@e)", ("e", e));
        var destino = await db.CriarEmpresaAsync("10149369000114", 2025);
        await db.EscalarAsync<int>("UPDATE core.empresa_regime SET regime = 'LUCRO_PRESUMIDO' WHERE empresa_id = @e RETURNING 1", ("e", destino));
        var previa = (await db.App.ImportacaoEcf.PreviaAsync(destino, r.Valor.Conteudo, "ecf.txt", new OpcoesImportacaoSped("LALUR"), a, ct)).Valor;
        Assert.DoesNotContain(previa.Ocorrencias, o => o.Gravidade == GravidadeOcorrencia.Erro);
        Assert.Equal(4, previa.Linhas.Single(l => l.Registro == "P300").Novos);
        var rel = await db.App.ImportacaoEcf.ImportarAsync(destino, r.Valor.Conteudo, "ecf.txt", new OpcoesImportacaoSped("LALUR"), a, null, ct);
        Assert.True(rel.Sucesso, string.Join("; ", rel.Erros));
        Assert.Equal(origem, await ListaAsync(sql, destino));
        // a memória gravada pode ser lida de novo pelo gerador da ECF (receita × percentual)
        Assert.Contains("receita 100.000,00 × 32% = base IRPJ 32.000,00 · × 32% = base CSLL 32.000,00",
                        await db.EscalarAsync<string>("SELECT resultado->>'memoria' FROM fiscal.apuracao_lucro WHERE empresa_id = @e AND trimestre = 2", ("e", destino)));
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.lalur_parte_b WHERE empresa_id = @e", ("e", destino)));
    }
}
