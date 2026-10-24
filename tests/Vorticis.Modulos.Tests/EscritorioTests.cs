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
        Assert.Equal((10L, new DateOnly(2026, 2, 28), 800m), (c.PessoaId, c.Vencimento, c.Valor));
    }

    [Fact]
    public void Gerador_de_contratos_qualifica_as_partes_e_numera_as_clausulas()
    {
        var esc = new DadosEscritorio("Escritório Modelo Ltda", "11222333000181", "Rua A, 10", "Campinas", "SP");
        var cli = new PessoaEscritorio(1, 1, true, false, "Padaria Boa", "11444777000161");
        var c = new ContratoEscritorio(0, 0, 1, ModeloContrato.Cliente, GeradorContratos.ObjetoPadrao(ModeloContrato.Cliente), 1500m, Periodicidade.Mensal, 10,
                                       new DateOnly(2026, 1, 1), null, "IPCA", null, "-");
        var texto = GeradorContratos.Gerar(esc, cli, c);
        Assert.StartsWith("CONTRATANTE: Padaria Boa, inscrita no CNPJ sob o nº 11.444.777/0001-61", texto);
        Assert.Contains("CONTRATADA: Escritório Modelo Ltda", texto);
        Assert.Contains("R$ 1.500,00 por mês, com vencimento no dia 10", texto);
        Assert.Contains("variação do IPCA", texto);
        Assert.Contains("por prazo indeterminado", texto);
        Assert.Contains("comarca de Campinas", texto);
        var forn = GeradorContratos.Gerar(esc, cli with { Cliente = false, Fornecedor = true }, c with { Modelo = ModeloContrato.Fornecedor });
        Assert.StartsWith("CONTRATANTE: Escritório Modelo Ltda", forn);
        var doc = GeradorContratos.Documento(esc, cli, c with { Texto = texto, Numero = 7 });
        Assert.Equal("CONTRATO DE PRESTAÇÃO DE SERVIÇOS CONTÁBEIS Nº 7", doc.Titulo);
        Assert.Equal(10, doc.Blocos.Count);
        Assert.NotEmpty(RegrasEscritorio.Validar(new PessoaEscritorio(0, 1, false, false, "", "123")));
        Assert.Empty(RegrasEscritorio.Validar(cli));
    }
}

public class EscritorioIntegracaoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Cadastro_proprio_tarefas_honorarios_protocolo_e_contratos()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var escritorio = await db.CriarEmpresaAsync("27865757000102");
        var svc = ServicosEscritorio.De(db.App);

        // cadastro próprio do escritório (não vira participante nem empresa dos outros módulos)
        var cli = await svc.GravarPessoaAsync(escritorio, new PessoaEscritorio(0, 1, true, false, "Padaria Boa", "11.444.777/0001-61"), adm, ct);
        Assert.True(cli.Sucesso, string.Join("; ", cli.Erros));
        var forn = (await svc.GravarPessoaAsync(escritorio, new PessoaEscritorio(0, 2, false, true, "Papelaria", null), adm, ct)).Valor;
        Assert.False((await svc.GravarPessoaAsync(escritorio, new PessoaEscritorio(0, 3, true, false, "Repetida", "11444777000161"), adm, ct)).Sucesso);
        Assert.Equal(0L, await db.EscalarAsync<long>("SELECT COUNT(*) FROM core.participante WHERE empresa_id = @e", ("e", escritorio)));
        Assert.Equal(["Padaria Boa"], (await svc.CarteiraAsync(escritorio, adm, new DateOnly(2026, 3, 1), ct)).Select(c => c.Nome));

        var t = await svc.GravarTarefaAsync(new Tarefa(0, escritorio, cli.Valor, "DCTFWeb", null, new DateOnly(2026, 3, 15), adm, 1, SituacaoTarefa.Pendente, Recorrencia.Mensal), adm, ct);
        Assert.True(t.Sucesso, string.Join("; ", t.Erros));
        var tarefa = Assert.Single(await svc.ListarTarefasAsync(escritorio, cli.Valor, true, adm, ct));
        Assert.Equal("Padaria Boa", tarefa.PessoaNome);
        var prox = await svc.ConcluirTarefaAsync(tarefa, adm, ct);
        Assert.NotNull(prox.Valor);
        Assert.Equal(new DateOnly(2026, 4, 15), Assert.Single(await svc.ListarTarefasAsync(escritorio, null, true, adm, ct)).Vencimento);

        Assert.True((await svc.GravarHonorarioAsync(escritorio, new ContratoHonorario(0, cli.Valor, 900m, 10, new DateOnly(2026, 1, 1), null, "IPCA", null), adm, ct)).Sucesso);
        Assert.Equal(1, (await svc.GerarCobrancasAsync(escritorio, new DateOnly(2026, 3, 1), adm, ct)).Valor);
        Assert.Equal(0, (await svc.GerarCobrancasAsync(escritorio, new DateOnly(2026, 3, 1), adm, ct)).Valor);
        var cob = Assert.Single(await svc.ListarCobrancasAsync(escritorio, new DateOnly(2026, 3, 1), false, adm, ct));
        Assert.True((await svc.ReceberAsync(escritorio, cob, new DateOnly(2026, 3, 10), 900m, adm, ct)).Sucesso);
        Assert.Empty(await svc.ListarCobrancasAsync(escritorio, null, true, adm, ct));
        Assert.Single(await svc.ListarCobrancasAsync(escritorio, new DateOnly(2026, 1, 1), false, adm, ct, ate: new DateOnly(2026, 12, 1)));

        Assert.Equal(1, (await svc.IncluirProtocoloAsync(new Protocolo(0, escritorio, cli.Valor, 0, new DateOnly(2026, 3, 10), TipoProtocolo.Entrega, "Guia DAS", null, "Maria"), adm, ct)).Valor);
        Assert.Equal("Padaria Boa", Assert.Single(await svc.ListarProtocolosAsync(escritorio, adm, ct)).ClienteNome);

        // contratos: numerados, cliente precisa estar marcado como cliente
        var pessoas = await svc.ListarPessoasAsync(escritorio, adm, ct);
        var esc = await svc.DadosEscritorioAsync(escritorio, ct);
        var novo = new ContratoEscritorio(0, 0, cli.Valor, ModeloContrato.Cliente, "Serviços contábeis", 900m, Periodicidade.Mensal, 10, new DateOnly(2026, 1, 1), null, "IPCA", null, "-");
        novo = novo with { Texto = GeradorContratos.Gerar(esc, pessoas.Single(p => p.Id == cli.Valor), novo) };
        Assert.Equal(1, (await svc.GravarContratoAsync(escritorio, novo, adm, ct)).Valor);
        Assert.Contains("não está marcado como cliente", (await svc.GravarContratoAsync(escritorio, novo with { PessoaId = forn }, adm, ct)).Erros[0]);
        Assert.Equal(2, (await svc.GravarContratoAsync(escritorio, novo with { PessoaId = forn, Modelo = ModeloContrato.Fornecedor }, adm, ct)).Valor);
        var contratos = await svc.ListarContratosAsync(escritorio, adm, ct);
        Assert.Equal([2, 1], contratos.Select(c => c.Numero));
        Assert.Equal(3, TabelasEscritorio.Contratos(contratos, true).Linhas.Count);

        // cliente com movimento não é excluído
        Assert.Contains("desmarque Ativo", (await svc.ExcluirPessoaAsync(escritorio, cli.Valor, adm, ct)).Erros[0]);

        // usuário sem perfil no escritório não grava nem vê nada
        var outro = await db.EscalarAsync<long>("INSERT INTO core.usuario (login, nome, senha_hash) VALUES ('sem_acesso_esc', 'Sem acesso', 'x') RETURNING id");
        Assert.False((await svc.GravarHonorarioAsync(escritorio, new ContratoHonorario(0, cli.Valor, 1m, 1, new DateOnly(2026, 1, 1), null, "NENHUM", null), outro, ct)).Sucesso);
        Assert.Empty(await svc.ListarTarefasAsync(escritorio, null, false, outro, ct));
        Assert.Empty(await svc.ListarPessoasAsync(escritorio, outro, ct));
    }
}
