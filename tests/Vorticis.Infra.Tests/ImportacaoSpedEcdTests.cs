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

/// <summary>Importação de arquivo SPED – ECD (ida e volta com o gerador do próprio projeto): plano, saldos, lançamentos, duplicidade, CNPJ e desfazer.</summary>
public class ImportacaoSpedEcdTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private const string CpfContador = "52998224725";
    private const string CpfSocia = "39053344705";
    private const string Referencial = """
        CODIGO|DESCRICAO|TIPO|COD_SUP|NIVEL|COD_NAT
        1|ATIVO|S||1|01
        1.01|Disponibilidades|A|1|2|01
        1.02|Outros ativos|A|1|2|01
        2|PASSIVO|S||1|02
        2.01|Obrigações|A|2|2|02
        2.03|PATRIMÔNIO LÍQUIDO|S|2|2|03
        2.03.01|Capital social|A|2.03|3|03
        2.03.02|Lucros ou prejuízos acumulados|A|2.03|3|03
        3|RESULTADO|S||1|04
        3.01|Receitas|A|3|2|04
        3.02|Custos e despesas|A|3|2|04
        """;

    private async Task Lancar(long empresa, long adm, DateOnly data, string debito, string credito, decimal valor, string hist, string? centro = null)
        => Assert.True((await db.App.Lancamentos.EfetivarAsync(new LancamentoProposto(empresa, data, OrigemLancamento.Manual,
            [new(debito, IndicadorDC.D, valor, hist, centro), new(credito, IndicadorDC.C, valor, hist)]), adm, TestContext.Current.CancellationToken)).Sucesso);

    /// <summary>Empresa de origem com movimento em 2025, encerrada, com referencial, contador e sócia; devolve a ECD gerada.</summary>
    internal async Task<(long Empresa, long Adm, byte[] Arquivo)> GerarEcdAsync(string cnpj, bool encerrar = true)
    {
        var ct = TestContext.Current.CancellationToken;
        var app = db.App;
        var empresa = await db.CriarEmpresaAsync(cnpj, 2025);
        var adm = await db.AdminAsync();
        await db.EscalarAsync<int>("UPDATE core.empresa SET nire = '35212345678', inscricao_estadual = '110042490114', municipio = 'SÃO PAULO' WHERE id = @e RETURNING 1", ("e", empresa));
        Assert.True((await app.PlanoContas.IncluirCentroAsync(empresa, "1", "ADMINISTRAÇÃO", null, adm, new DateOnly(2025, 1, 1), ct)).Sucesso);
        await Lancar(empresa, adm, new(2025, 1, 2), "1.1.01.002", "2.3.01.001", 50_000m, "Integralização do capital - sócia");
        await Lancar(empresa, adm, new(2025, 3, 10), "1.1.01.002", "3.1.01.003", 12_000m, "Serviços prestados - NF 12");
        await Lancar(empresa, adm, new(2025, 4, 5), "4.2.02.001", "1.1.01.002", 3_000m, "Aluguel de abril", "1");
        await Lancar(empresa, adm, new(2025, 4, 6), "3.2.01.005", "2.1.04.003", 600m, "ISS do mês");
        if (encerrar) Assert.True((await app.Encerramento.EncerrarAsync(empresa, 2025, new ContasEncerramento(), adm, ct)).Sucesso);
        var admin = new SessaoUsuario(adm, "admin_teste", "Admin", true, false);
        await app.PlanoReferencial.ImportarAsync(admin, "2", 2025, Encoding.Latin1.GetBytes(Referencial), "ref.txt", ct);
        Assert.True((await app.PlanoReferencial.DefinirPlanoAsync(empresa, 2025, "2", adm, ct)).Sucesso);
        var mapa = (await app.PlanoReferencial.CarregarAsync(empresa, 2025, adm, ct)).Valor.Contas;
        var vinculos = mapa.ToDictionary(c => c.ContaId, c => (string?)(c.CodNat switch
        {
            "01" => c.Codigo.StartsWith("1.1.01") ? "1.01" : "1.02",
            "02" => "2.01",
            "03" => c.Codigo.StartsWith("2.3.01") ? "2.03.01" : "2.03.02",
            _ => c.Codigo.StartsWith('3') ? "3.01" : "3.02",
        }));
        Assert.True((await app.PlanoReferencial.VincularAsync(empresa, 2025, vinculos, adm, ct)).Sucesso);
        Assert.True((await app.Responsaveis.VincularContadorAsync(empresa, new Contador(0, "José da Silva Contador", CpfContador, "1SP123456", "SP",
            "contador@exemplo.com.br", "(11) 3333-4444", null), new DateOnly(2020, 1, 1), true, adm, ct)).Sucesso);
        Assert.True((await app.Responsaveis.SalvarSocioAsync(empresa, new Socio(0, "Maria Sócia", 'F', CpfSocia, null, "Sócio-Administrador", 49, 100m,
            null, null, true, "maria@exemplo.com.br", "11999998888"), adm, ct)).Sucesso);
        var r = await app.Ecd.GerarAsync(empresa, 2025, new OpcoesEcd(NumeroCertidaoCrc: "SP/2025/123456", ValidadeCertidaoCrc: new DateOnly(2026, 3, 31)), adm, ct: ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        return (empresa, adm, r.Valor.Conteudo);
    }

    /// <summary>Lançamentos da empresa em texto (número, data, indicador, partidas com conta, lado, valor, histórico e centro), na ordem do número.</summary>
    internal async Task<List<string>> RetratoAsync(long empresa)
    {
        var res = new List<string>();
        await using var con = new NpgsqlConnection(db.ConnectionString);
        await con.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            SELECT l.numero, l.data_lancamento, l.ind_lcto::text, c.codigo, p.ind_dc::text, p.valor, p.historico, COALESCE(cc.codigo, ''), COALESCE(p.num_arquivo, '')
              FROM contabil.lancamento l JOIN contabil.partida p ON p.lancamento_id = l.id JOIN contabil.plano_contas c ON c.id = p.conta_id
              LEFT JOIN contabil.centro_custo cc ON cc.id = p.centro_custo_id
             WHERE l.empresa_id = @e ORDER BY l.numero, p.sequencia
            """, con);
        cmd.Parameters.AddWithValue("e", empresa);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            res.Add($"{r.GetInt64(0)}|{r.GetFieldValue<DateOnly>(1):yyyy-MM-dd}|{r.GetString(2)}|{r.GetString(3)}|{r.GetString(4)}|{r.GetDecimal(5):0.00}|{r.GetString(6)}|{r.GetString(7)}|{r.GetString(8)}");
        return res;
    }

    internal async Task<List<string>> SaldosAsync(long empresa)
    {
        var res = new List<string>();
        await using var con = new NpgsqlConnection(db.ConnectionString);
        await con.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            SELECT c.codigo, s.competencia, s.total_debito, s.total_credito FROM contabil.saldo_mensal s JOIN contabil.plano_contas c ON c.id = s.conta_id
             WHERE s.empresa_id = @e AND (s.total_debito <> 0 OR s.total_credito <> 0) ORDER BY c.codigo, s.competencia, s.centro_custo_id
            """, con);
        cmd.Parameters.AddWithValue("e", empresa);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) res.Add($"{r.GetString(0)}|{r.GetFieldValue<DateOnly>(1):yyyy-MM}|{r.GetDecimal(2):0.00}|{r.GetDecimal(3):0.00}");
        return res;
    }

    private async Task<List<string>> PlanoAsync(long empresa)
    {
        var res = new List<string>();
        await using var con = new NpgsqlConnection(db.ConnectionString);
        await con.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT codigo, descricao, tipo::text, nivel, cod_nat, natureza_saldo::text FROM contabil.plano_contas WHERE empresa_id = @e ORDER BY codigo", con);
        cmd.Parameters.AddWithValue("e", empresa);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) res.Add($"{r.GetString(0)}|{r.GetString(1)}|{r.GetString(2)}|{r.GetInt16(3)}|{r.GetString(4)}|{r.GetString(5)}");
        return res;
    }

    private Task ExcluirEmpresa(long empresa) => db.EscalarAsync<int>("SELECT core.fn_excluir_empresa(@e)", ("e", empresa));

    [Fact]
    public async Task Ida_e_volta_importa_plano_lancamentos_e_saldos_iguais_aos_da_empresa_de_origem()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var (origem, adm, arquivo) = await GerarEcdAsync("10013579000180");
        var lancOrigem = await RetratoAsync(origem);
        var saldosOrigem = await SaldosAsync(origem);
        var planoOrigem = await PlanoAsync(origem);
        Assert.NotEmpty(lancOrigem);
        await ExcluirEmpresa(origem);                                  // migração: a empresa de origem some e a nova entra com o mesmo CNPJ
        var destino = await db.CriarEmpresaAsync("10013579000180", 2025);

        // prévia: não grava nada
        var op = new OpcoesImportacaoSped("TUDO", SubstituirPlano: true, CadastrarSignatarios: true);
        var previa = await db.App.ImportacaoEcd.PreviaAsync(destino, arquivo, "ecd.txt", op, adm, ct);
        Assert.True(previa.Sucesso, string.Join("; ", previa.Erros));
        Assert.True(previa.Valor.CnpjConfere);
        Assert.True(previa.Valor.PodeImportar);
        Assert.Equal(0, previa.Valor.ErrosPontuais);
        var linhas = previa.Valor.Linhas.ToDictionary(l => l.Registro);
        Assert.True(linhas["I050"].Novos is > 10 and < 100);
        Assert.Equal(lancOrigem.Select(x => x.Split('|')[0]).Distinct().Count(), linhas["I200"].Novos);
        Assert.Equal(0, await db.EscalarAsync<long>("SELECT count(*) FROM contabil.lancamento WHERE empresa_id = @e", ("e", destino)));

        var progresso = new List<ProgressoImportacao>();
        var rel = await db.App.ImportacaoEcd.ImportarAsync(destino, arquivo, "ecd.txt", op, adm, new Progress<ProgressoImportacao>(progresso.Add), ct);
        Assert.True(rel.Sucesso, string.Join("; ", rel.Erros));
        Assert.Equal(0, rel.Valor.Erros);
        Assert.True(rel.Valor.Importados > 0);

        Assert.Equal(lancOrigem, await RetratoAsync(destino));
        Assert.Equal(saldosOrigem, await SaldosAsync(destino));
        var planoDestino = await PlanoAsync(destino);
        static string SemNatSintetica(string c) => c.Split('|') is { Length: 6 } p && p[4] == "04" ? string.Join('|', p[..5]) : c;     // a natureza do saldo das contas de resultado não vem no arquivo: é deduzida do movimento
        var difer = string.Join("\n", planoDestino.Where(c => !planoOrigem.Select(SemNatSintetica).Contains(SemNatSintetica(c))));
        Assert.True(difer.Length == 0, difer);                                  // todas as contas importadas batem com as da origem
        Assert.Equal(planoDestino.Count, previa.Valor.Linhas.Single(l => l.Registro == "I050").Novos);
        Assert.Contains(planoDestino, c => c.StartsWith("1.1.01.002|"));
        Assert.Equal(1L, await db.EscalarAsync<long>("SELECT count(*) FROM contabil.centro_custo WHERE empresa_id = @e AND codigo = '1'", ("e", destino)));
        Assert.Equal(1L, await db.EscalarAsync<long>("SELECT count(*) FROM core.contador WHERE cpf = @c", ("c", CpfContador)));
        Assert.True(await db.EscalarAsync<long>("SELECT count(*) FROM contabil.mapeamento_referencial m JOIN contabil.plano_contas c ON c.id = m.conta_id WHERE c.empresa_id = @e", ("e", destino)) > 0);
        Assert.Contains(progresso, p => p.Etapa.Contains("lançamentos"));

        // histórico
        var hist = await db.App.ImportacaoEcd.HistoricoAsync(destino, adm, ct);
        var h = Assert.Single(hist);
        Assert.Equal("Importada", h.Situacao);
        Assert.Equal("ecd.txt", h.Arquivo);
        Assert.Contains("I200", rel.Valor.ParaCsv());

        // o mesmo arquivo não entra duas vezes
        var dup = await db.App.ImportacaoEcd.ImportarAsync(destino, arquivo, "ecd.txt", op with { SubstituirPlano = false }, adm, null, ct);
        Assert.False(dup.Sucesso);
        Assert.Contains("já foi importado", string.Join(" ", dup.Erros));
    }

    [Fact]
    public async Task Arquivo_com_assinatura_e_so_plano_e_plano_com_saldos()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var (origem, adm, arquivo) = await GerarEcdAsync("10027158000109");
        await ExcluirEmpresa(origem);
        var comAssinatura = arquivo.Concat(Encoding.Latin1.GetBytes("SBRCAAEPDR01900")).Concat(new byte[] { 0x30, 0x82, 0x01, 0x0A, 0xFF, 0x0D, 0x0A, 0x00 }).ToArray();
        var destino = await db.CriarEmpresaAsync("10027158000109", 2025);
        var r = await db.App.ImportacaoEcd.ImportarAsync(destino, comAssinatura, "ecd_assinada.txt", new OpcoesImportacaoSped("PLANO", SubstituirPlano: true), adm, null, ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM contabil.lancamento WHERE empresa_id = @e", ("e", destino)));
        Assert.True(await db.EscalarAsync<long>("SELECT count(*) FROM contabil.plano_contas WHERE empresa_id = @e", ("e", destino)) > 5);

        var rel = await db.App.ImportacaoEcd.PreviaAsync(destino, comAssinatura, "x.txt", new OpcoesImportacaoSped("PLANO_SALDOS"), adm, ct);
        Assert.True(rel.Sucesso);
    }

    [Fact]
    public async Task Cnpj_diferente_e_arquivo_de_outro_tipo_sao_recusados()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var (origem, adm, arquivo) = await GerarEcdAsync("10040737000191");
        var outra = await db.CriarEmpresaAsync("10054316000110", 2025);
        var previa = await db.App.ImportacaoEcd.PreviaAsync(outra, arquivo, "ecd.txt", new OpcoesImportacaoSped("TUDO", SubstituirPlano: true), adm, ct);
        Assert.True(previa.Sucesso);
        Assert.False(previa.Valor.CnpjConfere);
        Assert.False(previa.Valor.PodeImportar);
        Assert.Equal(origem, previa.Valor.EmpresaCorrespondenteId);      // a empresa do CNPJ do arquivo existe: o assistente oferece trocar
        var r = await db.App.ImportacaoEcd.ImportarAsync(outra, arquivo, "ecd.txt", new OpcoesImportacaoSped("TUDO", SubstituirPlano: true), adm, null, ct);
        Assert.False(r.Sucesso);
        Assert.Contains("CNPJ", string.Join(" ", r.Erros));
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM contabil.lancamento WHERE empresa_id = @e", ("e", outra)));

        var texto = Encoding.Latin1.GetString(arquivo).Replace("|LECD|", "|LECF|");
        var tipo = await db.App.ImportacaoEcd.PreviaAsync(origem, Encoding.Latin1.GetBytes(texto), "x.txt", new OpcoesImportacaoSped(), adm, ct);
        Assert.False(tipo.Sucesso);
        Assert.Contains("ECF", string.Join(" ", tipo.Erros));

        var truncado = await db.App.ImportacaoEcd.PreviaAsync(origem, arquivo[..(arquivo.Length / 2)], "x.txt", new OpcoesImportacaoSped(), adm, ct);
        Assert.True(!truncado.Sucesso || !truncado.Valor.PodeImportar);
        var lixo = await db.App.ImportacaoEcd.PreviaAsync(origem, Encoding.Latin1.GetBytes("isto não é SPED"), "x.txt", new OpcoesImportacaoSped(), adm, ct);
        Assert.False(lixo.Sucesso);
    }

    [Fact]
    public async Task Soma_ao_plano_existente_sem_duplicar_contas_e_desfaz_a_importacao()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var (origem, adm, arquivo) = await GerarEcdAsync("10067895000135");
        await ExcluirEmpresa(origem);
        var destino = await db.CriarEmpresaAsync("10067895000135", 2025);       // plano modelo já tem as mesmas contas
        var planoAntes = await PlanoAsync(destino);

        var op = new OpcoesImportacaoSped("TUDO");
        var previa = (await db.App.ImportacaoEcd.PreviaAsync(destino, arquivo, "ecd.txt", op, adm, ct)).Valor;
        var i050 = previa.Linhas.Single(l => l.Registro == "I050");
        Assert.True(i050.Existentes > 0);                                    // contas do modelo reaproveitadas, não duplicadas
        var rel = await db.App.ImportacaoEcd.ImportarAsync(destino, arquivo, "ecd.txt", op, adm, null, ct);
        Assert.True(rel.Sucesso, string.Join("; ", rel.Erros));
        Assert.Equal(planoAntes.Count, (await PlanoAsync(destino)).Count);   // plano igual: nada duplicado
        Assert.True(await db.EscalarAsync<long>("SELECT count(*) FROM contabil.lancamento WHERE empresa_id = @e", ("e", destino)) >= 5);

        // desfazer: remove lançamentos e o que foi criado
        var id = rel.Valor.ImportacaoId;
        var des = await db.App.ImportacaoEcd.DesfazerAsync(destino, id, adm, ct);
        Assert.True(des.Sucesso, string.Join("; ", des.Erros));
        Assert.True(des.Valor.Desfeita);
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM contabil.lancamento WHERE empresa_id = @e", ("e", destino)));
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM contabil.saldo_mensal WHERE empresa_id = @e AND (total_debito <> 0 OR total_credito <> 0)", ("e", destino)));
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM contabil.centro_custo WHERE empresa_id = @e", ("e", destino)));
        Assert.Equal(planoAntes, await PlanoAsync(destino));
        Assert.Equal("Desfeita", Assert.Single(await db.App.ImportacaoEcd.HistoricoAsync(destino, adm, ct)).Situacao);
        Assert.False((await db.App.ImportacaoEcd.DesfazerAsync(destino, id, adm, ct)).Sucesso);   // não desfaz duas vezes

        // depois de desfeita, pode importar o mesmo arquivo de novo
        var de_novo = await db.App.ImportacaoEcd.ImportarAsync(destino, arquivo, "ecd.txt", op, adm, null, ct);
        Assert.True(de_novo.Sucesso, string.Join("; ", de_novo.Erros));

        // com o período fechado, não dá para desfazer
        await db.EscalarAsync<int>("UPDATE contabil.periodo SET status = 'FECHADO' WHERE competencia = '2025-04-01' AND exercicio_id IN (SELECT id FROM contabil.exercicio WHERE empresa_id = @e) RETURNING 1", ("e", destino));
        var bloqueada = await db.App.ImportacaoEcd.DesfazerAsync(destino, de_novo.Valor.ImportacaoId, adm, ct);
        Assert.False(bloqueada.Sucesso);
        Assert.Contains("fechado", string.Join(" ", bloqueada.Erros));
    }

    [Fact]
    public async Task Substituir_o_plano_devolve_o_plano_anterior_ao_desfazer_e_exige_permissao()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var (origem, adm, arquivo) = await GerarEcdAsync("10081474000169");
        await ExcluirEmpresa(origem);
        var destino = await db.CriarEmpresaAsync("10081474000169", 2025);
        var antes = await PlanoAsync(destino);
        var op = new OpcoesImportacaoSped("PLANO", SubstituirPlano: true);

        var consulta = await db.EscalarAsync<long>("""
            INSERT INTO core.usuario (login, nome, senha_hash, perfil_id) VALUES ('consulta_imp_ecd', 'Consulta', 'x', (SELECT id FROM core.perfil WHERE nome = 'Consulta')) RETURNING id
            """);
        Assert.False((await db.App.ImportacaoEcd.PreviaAsync(destino, arquivo, "ecd.txt", op, consulta, ct)).Sucesso);

        var r = await db.App.ImportacaoEcd.ImportarAsync(destino, arquivo, "ecd.txt", op, adm, null, ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        Assert.NotEqual(antes, await PlanoAsync(destino));
        var des = await db.App.ImportacaoEcd.DesfazerAsync(destino, r.Valor.ImportacaoId, adm, ct);
        Assert.True(des.Sucesso, string.Join("; ", des.Erros));
        Assert.Equal(antes, await PlanoAsync(destino));
    }

    [Fact]
    public async Task Plano_com_codigos_fora_do_padrao_e_renumerado_pela_hierarquia_e_os_lancamentos_acompanham()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var destino = await db.CriarEmpresaAsync("10095053000197", 2025);
        const string cnpj = "10095053000197";
        var linhas = new[]
        {
            $"|0000|LECD|01012025|31122025|Empresa de Teste LTDA|{cnpj}|SP||3550308||0|0|0|0||0|0||N|N|0|0||",
            "|0001|0|", "|0990|3|",
            "|I001|0|", "|I010|G|9.00|",
            "|I050|01012025|01|S|1|A|||ATIVO|", "|I050|01012025|01|S|2|AC|A|ATIVO CIRCULANTE|",
            "|I050|01012025|01|A|3|CX|AC|CAIXA|", "|I050|01012025|01|A|3|BCO|AC|BANCOS|",
            "|I050|01012025|03|S|1|P|||PATRIMONIO LIQUIDO|", "|I050|01012025|03|A|2|CAP|P|CAPITAL SOCIAL|",
            "|I150|01012025|31012025|", "|I155|CX||0,00|D|1000,00|0,00|1000,00|D|", "|I155|CAP||0,00|C|0,00|1000,00|1000,00|C|",
            "|I200|1|05012025|1000,00|N||", "|I250|CX||1000,00|D||| Integralização|", "|I250|CAP||1000,00|C||| Integralização|",
            "|I990|10|", "|J001|0|", "|J990|2|", "|9001|0|", "|9999|25|",
        };
        var arq = Encoding.Latin1.GetBytes(string.Join("\r\n", linhas) + "\r\n");
        var op = new OpcoesImportacaoSped("TUDO", SubstituirPlano: true);
        var previa = await db.App.ImportacaoEcd.PreviaAsync(destino, arq, "x.txt", op, adm, ct);
        Assert.True(previa.Sucesso, string.Join("; ", previa.Erros));
        Assert.Contains(previa.Valor.Ocorrencias, o => o.Motivo.Contains("renumerados"));
        var r = await db.App.ImportacaoEcd.ImportarAsync(destino, arq, "x.txt", op, adm, null, ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        var plano = await PlanoAsync(destino);
        Assert.Equal(6, plano.Count);
        Assert.Contains(plano, c => c.Contains("|CAIXA|A|3|01|D"));
        var lanc = await RetratoAsync(destino);
        Assert.Equal(2, lanc.Count);
        Assert.Contains("|D|1000.00|Integralização|", string.Join('\n', lanc));
    }

    [Fact]
    public async Task Saldo_inicial_do_primeiro_mes_vira_implantacao_e_o_exercicio_que_falta_e_criado()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        const string cnpj = "10108632000127";
        var destino = await db.CriarEmpresaAsync(cnpj, 2025);
        var linhas = new[]
        {
            $"|0000|LECD|01012026|31122026|Empresa de Teste LTDA|{cnpj}|SP||3550308||0|0|0|0||0|0||N|N|0|0||",
            "|0001|0|", "|0990|3|", "|I001|0|", "|I010|G|9.00|",
            "|I050|01012026|01|S|1|1|||ATIVO|", "|I050|01012026|01|A|2|1.1|1|CAIXA|", "|I050|01012026|03|S|1|3|||PATRIMONIO LIQUIDO|", "|I050|01012026|03|A|2|3.1|3|CAPITAL|",
            "|I050|01012026|04|S|1|4|||RECEITAS|", "|I050|01012026|04|A|2|4.1|4|VENDAS|",
            "|I150|01012026|31012026|", "|I155|1.1||1000,00|D|500,00|0,00|1500,00|D|", "|I155|3.1||1000,00|C|0,00|0,00|1000,00|C|", "|I155|4.1||0,00|C|0,00|500,00|500,00|C|",
            "|I200|1|10012026|500,00|N||", "|I250|1.1||500,00|D||| Venda à vista|", "|I250|4.1||500,00|C||| Venda à vista|",
            "|I990|8|", "|J001|0|", "|J990|2|", "|9001|0|", "|9999|25|",
        };
        var arq = Encoding.Latin1.GetBytes(string.Join("\r\n", linhas) + "\r\n");
        var op = new OpcoesImportacaoSped("TUDO", SubstituirPlano: true);
        var previa = (await db.App.ImportacaoEcd.PreviaAsync(destino, arq, "ecd2026.txt", op, adm, ct)).Valor;
        Assert.Contains(previa.Linhas, l => l.Registro == "I155" && l.Novos == 2);
        Assert.Contains(previa.Ocorrencias, o => o.Motivo.Contains("batem"));
        var r = await db.App.ImportacaoEcd.ImportarAsync(destino, arq, "ecd2026.txt", op, adm, null, ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        Assert.Equal(1L, await db.EscalarAsync<long>("SELECT count(*) FROM contabil.exercicio WHERE empresa_id = @e AND ano = 2026", ("e", destino)));
        Assert.Equal(1L, await db.EscalarAsync<long>("SELECT count(*) FROM contabil.lancamento WHERE empresa_id = @e AND origem = 'SALDO_INICIAL' AND data_lancamento = '2026-01-01'", ("e", destino)));
        Assert.Equal(1000m, await db.EscalarAsync<decimal>("""
            SELECT COALESCE(SUM(p.valor), 0) FROM contabil.partida p JOIN contabil.lancamento l ON l.id = p.lancamento_id
             WHERE l.empresa_id = @e AND l.origem = 'SALDO_INICIAL' AND p.ind_dc = 'D'
            """, ("e", destino)));

        var des = await db.App.ImportacaoEcd.DesfazerAsync(destino, r.Valor.ImportacaoId, adm, ct);
        Assert.True(des.Sucesso, string.Join("; ", des.Erros));
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM contabil.exercicio WHERE empresa_id = @e AND ano = 2026", ("e", destino)));
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM contabil.lancamento WHERE empresa_id = @e", ("e", destino)));
    }
}
