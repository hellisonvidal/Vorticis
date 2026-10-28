using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Npgsql;
using Vorticis.Application.Sped;
using Vorticis.Application.Sped.Importacao;
using Vorticis.Domain.Sped;
using Vorticis.Domain.Sped.Importacao;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Lucro;
using Vorticis.Fiscal.Sped;

namespace Vorticis.Fiscal.Tests;

/// <summary>Importação de EFD ICMS/IPI e EFD-Contribuições (ida e volta com os geradores do projeto): notas, cadastros, apuração, duplicidade e desfazer.</summary>
public class ImportacaoSpedEfdTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    private static readonly DateOnly Mar = new(2026, 3, 10);
    private static ArquivoXml Arq(string nome, string xml) => new(nome, XmlsDeTeste.Bytes(xml));

    private static string Chave(string cnpjEmit, int numero)
    {
        var b = $"352603{cnpjEmit}55001{numero:D9}1{numero:D8}";
        var soma = 0; var peso = 2;
        for (var i = b.Length - 1; i >= 0; i--) { soma += (b[i] - '0') * peso; peso = peso == 9 ? 2 : peso + 1; }
        var r = soma % 11;
        return b + (r < 2 ? 0 : 11 - r);
    }

    private static X509Certificate2 Certificado(string cn)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest($"CN={cn}, O=ICP-Brasil, C=BR", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx, "x"), "x", X509KeyStorageFlags.Exportable);
    }

    private async Task<long> EmpresaAsync(int semente)
    {
        var cnpj = CnpjsDeTeste.Valido(semente);
        var empresa = await db.CriarEmpresaAsync(cnpj);
        await db.EscalarAsync<long>("""
            UPDATE core.empresa SET inscricao_estadual = '123456789012', cep = '01310100', logradouro = 'Av. Paulista', numero = '1000', bairro = 'Bela Vista', email = 'a@b.com', telefone = '1130000000'
             WHERE id = @e RETURNING id
            """, ("e", empresa));
        var contador = await db.EscalarAsync<long>("INSERT INTO core.contador (nome, cpf, crc, uf_crc, email) VALUES ('Contador de Teste', @c, '1SP123456/O-7', 'SP', 'c@c.com') ON CONFLICT (cpf) DO UPDATE SET nome = core.contador.nome RETURNING id",
            ("c", $"{(123_456_000L + semente):D9}00"[..11]));
        await db.EscalarAsync<long>("INSERT INTO core.empresa_contabilidade (empresa_id, contador_id, inicio, responsavel_tecnico) VALUES (@e, @c, '2025-01-01', TRUE) RETURNING id",
            ("e", empresa), ("c", contador));
        return empresa;
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
            res.Add(string.Join('|', Enumerable.Range(0, r.FieldCount).Select(i => r.IsDBNull(i) ? "" : r.GetValue(i) is decimal d ? d.ToString("0.00") : r.GetValue(i) is DateOnly dt ? dt.ToString("yyyy-MM-dd") : r.GetValue(i).ToString())));
        return res;
    }

    private const string SqlNotas = """
        SELECT d.modelo, d.chave_acesso, d.tipo_operacao, d.numero, d.serie, d.situacao, d.data_emissao, d.valor_total, d.vl_icms,
               (SELECT count(*) FROM fiscal.documento_item i WHERE i.documento_id = d.id), (SELECT COALESCE(sum(i.valor_produto), 0) FROM fiscal.documento_item i WHERE i.documento_id = d.id)
          FROM fiscal.documento d WHERE d.empresa_id = @e AND d.modelo = ANY(ARRAY['55','57']) ORDER BY d.modelo, d.numero
        """;

    /// <summary>Empresa de origem com notas (vendas, compra, uma venda cancelada, NFS-e e CT-e), ajustes, inventário e PIS/COFINS apurado; devolve os dois arquivos SPED.</summary>
    private async Task<(long Empresa, long Adm, string Cnpj, byte[] Icms, byte[] Contrib, byte[] IcmsAssinado)> OrigemAsync(int semente)
    {
        var ct = TestContext.Current.CancellationToken;
        var empresa = await EmpresaAsync(semente);
        var cnpj = await db.EscalarAsync<string>("SELECT cnpj FROM core.empresa WHERE id = @e", ("e", empresa));
        var adm = await db.AdminAsync();
        var fiscal = db.App.Fiscal();
        var forn = XmlsDeTeste.CnpjFornecedor;
        var imp = await fiscal.Documentos.ImportarAsync(empresa,
        [
            Arq("v1.xml", XmlsDeTeste.Nfe(cnpj, XmlsDeTeste.CnpjCliente, 1, 1, Mar, [new ItemNfe("5102", 10_000m, 1_800m, Pis: 165m, Cofins: 760m)], chave: Chave(cnpj, 1))),
            Arq("v2.xml", XmlsDeTeste.Nfe(cnpj, XmlsDeTeste.CnpjCliente, 1, 2, Mar, [new ItemNfe("5102", 5_000m, 900m), new ItemNfe("5405", 2_000m, IcmsSt: 120m, AliqIcms: 0m)], chave: Chave(cnpj, 2))),
            Arq("c1.xml", XmlsDeTeste.Nfe(forn, cnpj, 0, 77, Mar, [new ItemNfe("1102", 6_000m, 1_080m)], chave: Chave(forn, 77))),
            Arq("v3.xml", XmlsDeTeste.Nfe(cnpj, XmlsDeTeste.CnpjCliente, 1, 3, Mar, [new ItemNfe("5102", 1_000m, 180m)], chave: Chave(cnpj, 3))),
            Arq("s.xml", XmlsDeTeste.Nfse(cnpj, XmlsDeTeste.CnpjCliente, 5, Mar, 10_000m, iss: 500m)),
            Arq("t.xml", CteDeTeste.Cte(forn, XmlsDeTeste.CnpjCliente, cnpj, 9, Mar, 1_000m)),
        ], adm, ct: ct);
        Assert.True(imp.Sucesso, string.Join(";", imp.Erros));
        await fiscal.Documentos.ImportarAsync(empresa, [Arq("x.xml", XmlsDeTeste.CancelamentoNfe(Chave(cnpj, 3)))], adm, ct: ct);

        var outros = fiscal.OutrosLancamentosEfd;
        foreach (var (cod, valor) in new[] { ("SP000001", 50m), ("SP020001", 100m) })
            Assert.True((await outros.GravarAjusteAsync(empresa, new AjusteIcmsEfd(0, Mar, cod, "Ajuste de teste " + cod, valor), adm, ct)).Sucesso);
        var prod = await db.EscalarAsync<long>("INSERT INTO fiscal.produto (empresa_id, codigo, descricao, unidade, ncm) VALUES (@e, 900, 'Mercadoria A', 'UN', '84714900') RETURNING id", ("e", empresa));
        var inv = new Inventario(0, new DateOnly(2026, 2, 28), "01", new DateOnly(2026, 3, 1), "1.1.04.001", [new ItemInventario(prod, 900, "Mercadoria A", "UN", 10m, 12.5m, 0)]);
        Assert.True((await outros.GravarInventarioAsync(empresa, inv, adm, ct)).Sucesso);
        var pc = (await fiscal.Lucro.PrepararPisCofinsAsync(empresa, Mar, RegimePisCofins.NaoCumulativo, false, adm, ct)).Valor;
        Assert.True((await fiscal.Lucro.SalvarPisCofinsAsync(empresa, pc.Entrada, adm, ct)).Sucesso);

        var opcoes = new OpcoesEfdIcms('A', '1', CodigoReceitaIcms: "046-2", CodigoReceitaIcmsSt: "063-2", DiaVencimento: 12);
        var cert = Certificado("Contador Teste:12345678901");
        var icms = await fiscal.Efd.GerarIcmsAsync(empresa, Mar, opcoes, adm, null, ct);
        Assert.True(icms.Sucesso, string.Join(";", icms.Erros));
        var assinado = await fiscal.Efd.GerarIcmsAsync(empresa, Mar, opcoes, adm, [new CertificadoAssinante(cert, "900")], ct);
        Assert.True(assinado.Sucesso, string.Join(";", assinado.Erros));
        var contrib = await fiscal.Efd.GerarContribuicoesAsync(empresa, Mar, new OpcoesEfdContribuicoes(), adm, null, ct);
        Assert.True(contrib.Sucesso, string.Join(";", contrib.Erros));
        return (empresa, adm, cnpj, icms.Valor.Conteudo, contrib.Valor.Conteudo, assinado.Valor.Conteudo);
    }

    /// <summary>Nota cancelada vai ao SPED só com a identificação (sem data nem valores): compara apenas o que o leiaute leva.</summary>
    private static List<string> SemValoresDeCanceladas(IEnumerable<string> notas) =>
        notas.Select(n => n.Split('|') is var c && c[5] == "CANCELADA" ? string.Join('|', c[..6]) : n).ToList();

    private Task ExcluirEmpresa(long empresa) => db.EscalarAsync<int>("SELECT core.fn_excluir_empresa(@e)", ("e", empresa));

    [Fact]
    public async Task Ida_e_volta_da_efd_icms_ipi_e_da_efd_contribuicoes_reproduz_notas_apuracao_e_cadastros()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var (origem, adm, cnpj, icms, contrib, icmsAssinado) = await OrigemAsync(81);
        var notasOrigem = await ListaAsync(SqlNotas, origem);
        var nfseOrigem = await ListaAsync("SELECT numero, valor_total, vl_iss, situacao FROM fiscal.documento WHERE empresa_id = @e AND modelo = 'NFSE'", origem);
        var pcOrigem = await ListaAsync("SELECT competencia, regime, receita_base, pis_debito, cofins_debito, base_credito, pis_credito, cofins_credito, pis_a_pagar, cofins_a_pagar FROM fiscal.apuracao_pis_cofins WHERE empresa_id = @e", origem);
        var ajustesOrigem = await ListaAsync("SELECT competencia, codigo, valor, sentido FROM fiscal.efd_ajuste_icms WHERE empresa_id = @e ORDER BY codigo", origem);
        Assert.Equal(6, notasOrigem.Count + nfseOrigem.Count);
        await ExcluirEmpresa(origem);
        var destino = await EmpresaAsync(81);                            // mesma empresa (mesmo CNPJ), agora vazia
        var fiscal = db.App.Fiscal();
        var op = new OpcoesImportacaoSped("TUDO");

        // ---------------- EFD ICMS/IPI (com bloco de assinatura no fim)
        var previa = await fiscal.ImportacaoEfdIcms.PreviaAsync(destino, icmsAssinado, "efd.txt", op, adm, ct);
        Assert.True(previa.Sucesso, string.Join("; ", previa.Erros));
        Assert.True(previa.Valor.CnpjConfere);
        Assert.True(previa.Valor.PodeImportar, string.Join("; ", previa.Valor.Ocorrencias.Where(o => o.Gravidade == GravidadeOcorrencia.Erro).Select(o => o.Motivo)));
        var linhas = previa.Valor.Linhas.ToDictionary(l => l.Registro);
        Assert.Equal(4, linhas["C100"].Novos);
        Assert.Equal(1, linhas["D100"].Novos);
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.documento WHERE empresa_id = @e", ("e", destino)));

        var r = await fiscal.ImportacaoEfdIcms.ImportarAsync(destino, icmsAssinado, "efd.txt", op, adm, null, ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        Assert.Equal(0, r.Valor.Erros);
        Assert.Equal(SemValoresDeCanceladas(notasOrigem), SemValoresDeCanceladas(await ListaAsync(SqlNotas, destino)));
        Assert.Equal("SPED", await db.EscalarAsync<string>("SELECT DISTINCT origem FROM fiscal.documento WHERE empresa_id = @e", ("e", destino)));
        Assert.Equal(ajustesOrigem, await ListaAsync("SELECT competencia, codigo, valor, sentido FROM fiscal.efd_ajuste_icms WHERE empresa_id = @e ORDER BY codigo", destino));
        Assert.Equal(1L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.apuracao_icms_historico WHERE empresa_id = @e", ("e", destino)));
        Assert.Equal(1L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.inventario i JOIN fiscal.inventario_item x ON x.inventario_id = i.id WHERE i.empresa_id = @e", ("e", destino)));
        Assert.True(await db.EscalarAsync<long>("SELECT count(*) FROM core.participante WHERE empresa_id = @e", ("e", destino)) >= 2);
        Assert.Equal(4L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.documento WHERE empresa_id = @e AND participante_id IS NOT NULL", ("e", destino)));

        // o mesmo arquivo não entra duas vezes
        var dup = await fiscal.ImportacaoEfdIcms.ImportarAsync(destino, icms, "efd2.txt", op, adm, null, ct);
        Assert.False(dup.Sucesso);
        Assert.Contains("já foi importado", string.Join(" ", dup.Erros));

        // ---------------- EFD-Contribuições: as notas de mercadoria já existem (mesma chave); entram a NFS-e e a apuração
        var previaC = (await fiscal.ImportacaoEfdContribuicoes.PreviaAsync(destino, contrib, "contrib.txt", op, adm, ct)).Valor;
        var lc = previaC.Linhas.ToDictionary(l => l.Registro);
        Assert.Equal(0, lc["C100"].Novos);
        Assert.Equal(4, lc["C100"].Existentes);
        Assert.Equal(1, lc["A100"].Novos);
        var rc = await fiscal.ImportacaoEfdContribuicoes.ImportarAsync(destino, contrib, "contrib.txt", op, adm, null, ct);
        Assert.True(rc.Sucesso, string.Join("; ", rc.Erros));
        Assert.Equal(nfseOrigem, await ListaAsync("SELECT numero, valor_total, vl_iss, situacao FROM fiscal.documento WHERE empresa_id = @e AND modelo = 'NFSE'", destino));
        Assert.Equal(pcOrigem, await ListaAsync("SELECT competencia, regime, receita_base, pis_debito, cofins_debito, base_credito, pis_credito, cofins_credito, pis_a_pagar, cofins_a_pagar FROM fiscal.apuracao_pis_cofins WHERE empresa_id = @e", destino));
        Assert.Equal(6L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.documento WHERE empresa_id = @e", ("e", destino)));   // nada duplicado

        // histórico e relatório
        var hist = await fiscal.ImportacaoEfdIcms.HistoricoAsync(destino, adm, ct);
        Assert.Single(hist);
        Assert.Contains("C100", r.Valor.ParaCsv());
        Assert.Single(await fiscal.ImportacaoEfdContribuicoes.HistoricoAsync(destino, adm, ct));

        // ---------------- desfazer a EFD ICMS/IPI
        var des = await fiscal.ImportacaoEfdIcms.DesfazerAsync(destino, r.Valor.ImportacaoId, adm, ct);
        Assert.True(des.Sucesso, string.Join("; ", des.Erros));
        Assert.Equal(1L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.documento WHERE empresa_id = @e AND modelo = 'NFSE'", ("e", destino)));   // a NFS-e veio da outra importação
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.apuracao_icms_historico WHERE empresa_id = @e", ("e", destino)));
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.efd_ajuste_icms WHERE empresa_id = @e", ("e", destino)));
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.inventario WHERE empresa_id = @e", ("e", destino)));
        Assert.True((await fiscal.ImportacaoEfdIcms.HistoricoAsync(destino, adm, ct)).Single().Situacao == "Desfeita");
        Assert.False((await fiscal.ImportacaoEfdIcms.DesfazerAsync(destino, r.Valor.ImportacaoId, adm, ct)).Sucesso);
        // e importa de novo
        var de_novo = await fiscal.ImportacaoEfdIcms.ImportarAsync(destino, icms, "efd.txt", op, adm, null, ct);
        Assert.True(de_novo.Sucesso, string.Join("; ", de_novo.Erros));
        Assert.Equal(SemValoresDeCanceladas(notasOrigem), SemValoresDeCanceladas(await ListaAsync(SqlNotas, destino)));
        _ = cnpj;
    }

    [Fact]
    public async Task Nota_ja_importada_do_xml_nao_duplica_e_cnpj_diferente_ou_tipo_errado_sao_recusados()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var (origem, adm, cnpj, icms, contrib, _) = await OrigemAsync(82);
        var fiscal = db.App.Fiscal();

        // a origem já tem as notas (do XML): importar o SPED só traz o que falta e nada de nota duplicada
        var previa = (await fiscal.ImportacaoEfdIcms.PreviaAsync(origem, icms, "efd.txt", new OpcoesImportacaoSped("TUDO"), adm, ct)).Valor;
        Assert.Equal(0, previa.Linhas.Single(l => l.Registro == "C100").Novos);
        Assert.True(previa.Linhas.Single(l => l.Registro == "C100").Existentes == 4, string.Join(" / ", previa.Linhas.Select(l => $"{l.Registro}:{l.Total}/{l.Novos}/{l.Existentes}/{l.Conflitos}")) + " " + string.Join(" | ", previa.Ocorrencias.Select(o => o.Motivo)));
        var antes = await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.documento WHERE empresa_id = @e", ("e", origem));
        var r = await fiscal.ImportacaoEfdIcms.ImportarAsync(origem, icms, "efd.txt", new OpcoesImportacaoSped("NOTAS"), adm, null, ct);
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        Assert.Equal(antes, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.documento WHERE empresa_id = @e", ("e", origem)));
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.documento WHERE empresa_id = @e AND origem = 'SPED'", ("e", origem)));

        // CNPJ diferente
        var outra = await EmpresaAsync(83);
        var p2 = await fiscal.ImportacaoEfdIcms.PreviaAsync(outra, icms, "efd.txt", new OpcoesImportacaoSped("TUDO"), adm, ct);
        Assert.True(p2.Sucesso);
        Assert.False(p2.Valor.CnpjConfere);
        Assert.Equal(origem, p2.Valor.EmpresaCorrespondenteId);
        var r2 = await fiscal.ImportacaoEfdIcms.ImportarAsync(outra, icms, "efd.txt", new OpcoesImportacaoSped("TUDO"), adm, null, ct);
        Assert.False(r2.Sucesso);
        Assert.Contains("CNPJ", string.Join(" ", r2.Erros));
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.documento WHERE empresa_id = @e", ("e", outra)));

        // tipo errado: Contribuições na tela de ICMS e vice-versa; arquivo truncado e lixo
        var t1 = await fiscal.ImportacaoEfdIcms.PreviaAsync(origem, contrib, "x.txt", new OpcoesImportacaoSped("TUDO"), adm, ct);
        Assert.False(t1.Sucesso);
        Assert.Contains("EFD-Contribuições", string.Join(" ", t1.Erros));
        var t2 = await fiscal.ImportacaoEfdContribuicoes.PreviaAsync(origem, icms, "x.txt", new OpcoesImportacaoSped("TUDO"), adm, ct);
        Assert.False(t2.Sucesso);
        var trunc = await fiscal.ImportacaoEfdIcms.PreviaAsync(origem, icms[..(icms.Length / 2)], "x.txt", new OpcoesImportacaoSped("TUDO"), adm, ct);
        Assert.True(!trunc.Sucesso || !trunc.Valor.PodeImportar);
        Assert.False((await fiscal.ImportacaoEfdIcms.PreviaAsync(origem, Encoding.Latin1.GetBytes("lixo"), "x.txt", new OpcoesImportacaoSped("TUDO"), adm, ct)).Sucesso);

        // sem permissão (perfil Consulta)
        var consulta = await db.EscalarAsync<long>("INSERT INTO core.usuario (login, nome, senha_hash, perfil_id) VALUES ('consulta_imp_efd', 'Consulta', 'x', (SELECT id FROM core.perfil WHERE nome = 'Consulta')) RETURNING id");
        Assert.False((await fiscal.ImportacaoEfdIcms.PreviaAsync(origem, icms, "efd.txt", new OpcoesImportacaoSped("TUDO"), consulta, ct)).Sucesso);
        _ = cnpj;
    }

    [Fact]
    public async Task Nota_com_periodo_fechado_e_modelo_nao_suportado_viram_erros_pontuais_e_podem_ser_ignorados()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var (origem, adm, _, icms, _, _) = await OrigemAsync(84);
        await ExcluirEmpresa(origem);
        var destino = await EmpresaAsync(84);
        // troca o modelo de uma nota por 01 (NF modelo 1, não suportada)
        var texto = Encoding.Latin1.GetString(icms);
        var linhas = texto.Split("\r\n").ToList();
        var i = linhas.FindIndex(l => l.StartsWith("|C100|0|1|"));
        var campos = linhas[i].Split('|'); campos[5] = "01"; linhas[i] = string.Join('|', campos);
        var alterado = Encoding.Latin1.GetBytes(string.Join("\r\n", linhas));
        var fiscal = db.App.Fiscal();
        var op = new OpcoesImportacaoSped("TUDO");
        var previa = (await fiscal.ImportacaoEfdIcms.PreviaAsync(destino, alterado, "efd.txt", op, adm, ct)).Valor;
        Assert.Equal(1, previa.ErrosPontuais);
        Assert.True(previa.PodeImportar);
        var recusa = await fiscal.ImportacaoEfdIcms.ImportarAsync(destino, alterado, "efd.txt", op, adm, null, ct);
        Assert.False(recusa.Sucesso);
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.documento WHERE empresa_id = @e", ("e", destino)));
        var ok = await fiscal.ImportacaoEfdIcms.ImportarAsync(destino, alterado, "efd.txt", op with { IgnorarComErro = true }, adm, null, ct);
        Assert.True(ok.Sucesso, string.Join("; ", ok.Erros));
        Assert.Equal(4L, await db.EscalarAsync<long>("SELECT count(*) FROM fiscal.documento WHERE empresa_id = @e", ("e", destino)));   // 5 menos a de modelo 01
        Assert.Contains(ok.Valor.Ocorrencias, o => o.Gravidade == GravidadeOcorrencia.Erro && o.Motivo.Contains("modelo 01"));
        // período fiscal fechado impede desfazer
        await db.EscalarAsync<int>("INSERT INTO fiscal.periodo_fechado (empresa_id, competencia) VALUES (@e, '2026-03-01') RETURNING 1", ("e", destino));
        var des = await fiscal.ImportacaoEfdIcms.DesfazerAsync(destino, ok.Valor.ImportacaoId, adm, ct);
        Assert.False(des.Sucesso);
        Assert.Contains("fechado", string.Join(" ", des.Erros));
        // e um período fechado também impede importar notas
        var outra = await EmpresaAsync(85);
        await db.EscalarAsync<int>("INSERT INTO fiscal.periodo_fechado (empresa_id, competencia) VALUES (@e, '2026-03-01') RETURNING 1", ("e", outra));
    }
}
