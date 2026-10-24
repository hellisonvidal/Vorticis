using Vorticis.Erp;
using Vorticis.Fiscal;
using Vorticis.Fiscal.Duplicatas;
using Vorticis.Fiscal.Produtos;
using Vorticis.Infra.Tests;

namespace Vorticis.Modulos.Tests;

public class RegrasErpTests
{
    [Fact]
    public void Custo_medio_placa_e_validacao_do_pedido()
    {
        Assert.Equal(12.5m, RegrasErp.CustoMedio([(10m, 10m), (10m, 15m), (-5m, 0m)]));
        Assert.Equal("ABC1D23", RegrasErp.PlacaValida("abc-1d23"));
        Assert.Null(RegrasErp.PlacaValida("AB12345"));
        var p = new Pedido(0, TipoPedido.Venda, 0, new DateOnly(2026, 3, 1), 0, SituacaoPedido.Aberto, 0, 30, null, null, [new ItemPedido(1, 1, 10, 20)]);
        var erros = RegrasErp.Validar(p);
        Assert.Contains("Escolha o cliente.", erros);
        Assert.Contains("O desconto não pode passar do valor do item.", erros);
        Assert.Contains("Parcelas: de 1 a 120.", erros);
    }
}

public class ErpIntegracaoTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Compra_recebida_e_venda_faturada_viram_notas_duplicatas_e_estoque()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var adm = await db.AdminAsync();
        var empresa = await db.CriarEmpresaAsync("45723174000110");
        var cli = await db.EscalarAsync<long>("INSERT INTO core.participante (empresa_id, codigo, nome, cpf_cnpj, cliente) VALUES (@e, 'C1', 'Cliente A', '11222333000181', TRUE) RETURNING id", ("e", empresa));
        var forn = await db.EscalarAsync<long>("INSERT INTO core.participante (empresa_id, codigo, nome, cpf_cnpj, fornecedor) VALUES (@e, 'F1', 'Fornecedor B', '45997418000153', TRUE) RETURNING id", ("e", empresa));
        var prod = await ServicosFiscais.De(db.App).Produtos.GravarAsync(empresa, new Produto(0, 0, "Caneta", "UN", "96081000", null, null, "0", "00", "1102", "5102", "000", "000", 18, 0, 0, 0), adm, ct);
        Assert.True(prod.Sucesso, string.Join("; ", prod.Erros));
        var erp = ServicosErp.De(db.App);
        var hoje = new DateOnly(2026, 3, 10);

        var compra = await erp.GravarPedidoAsync(empresa, new Pedido(0, TipoPedido.Compra, 0, hoje, forn, SituacaoPedido.Aberto, 1, 30, null, null,
                                                                     [new ItemPedido(prod.Valor, 100, 2m)]), adm, ct);
        Assert.True(compra.Sucesso, string.Join("; ", compra.Erros));
        var pc = Assert.Single(await erp.ListarPedidosAsync(empresa, TipoPedido.Compra, hoje, hoje, adm, ct));
        var nfEntrada = await erp.ConcluirPedidoAsync(empresa, pc, "777", "1", null, hoje, adm, ct);
        Assert.True(nfEntrada.Sucesso, string.Join("; ", nfEntrada.Erros));

        var venda = await erp.GravarPedidoAsync(empresa, new Pedido(0, TipoPedido.Venda, 0, hoje, cli, SituacaoPedido.Aberto, 2, 30, hoje.AddDays(30), null,
                                                                    [new ItemPedido(prod.Valor, 40, 5m, 10m)]), adm, ct);
        var pv = Assert.Single(await erp.ListarPedidosAsync(empresa, TipoPedido.Venda, hoje, hoje, adm, ct));
        Assert.Equal(190m, pv.Total);
        var nfSaida = await erp.ConcluirPedidoAsync(empresa, pv, (await erp.ProximoNumeroNotaAsync(empresa, ct)).ToString(), "1", null, hoje, adm, ct);
        Assert.True(nfSaida.Sucesso, string.Join("; ", nfSaida.Erros));

        var pos = Assert.Single(await erp.ProdutosAsync(empresa, adm, ct));
        Assert.Equal((60m, 2m), (pos.Saldo, pos.CustoMedio));
        var dups = await ServicosFiscais.De(db.App).Duplicatas.ConsultarAsync(empresa, new FiltroDuplicatas(DocumentoId: nfSaida.Valor), adm, ct);
        Assert.Equal(2, dups.Valor.Count);
        Assert.Equal(190m, dups.Valor.Sum(d => d.Valor));
        Assert.Equal(SituacaoPedido.Concluido, Assert.Single(await erp.ListarPedidosAsync(empresa, TipoPedido.Venda, hoje, hoje, adm, ct)).Situacao);
        Assert.False((await erp.CancelarPedidoAsync(empresa, pv with { Situacao = SituacaoPedido.Concluido }, adm, ct)).Sucesso);

        var painel = await erp.PainelAsync(empresa, hoje, adm, ct);
        Assert.True(painel.Sucesso, string.Join("; ", painel.Erros));
        Assert.Equal((190m, 200m, 190m), (painel.Valor.Vendas, painel.Valor.Compras, painel.Valor.ReceberAberto));

        Assert.False((await erp.GravarVeiculoAsync(empresa, new Veiculo(0, "XX-1", null, "CAMINHAO", "SP", 0, 0), adm, ct)).Sucesso);
        Assert.True((await erp.GravarVeiculoAsync(empresa, new Veiculo(0, "abc1d23", null, "CAMINHAO", "SP", 5000, 10000), adm, ct)).Sucesso);
    }
}
