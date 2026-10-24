using Vorticis.Desktop.Comum.Modulos;
using Vorticis.Domain.Seguranca;
using Vorticis.Erp.Desktop.ViewModels;
using F = Vorticis.Fiscal.Desktop.ViewModels;

namespace Vorticis.Erp.Desktop;

/// <summary>
/// Módulo ERP: rotinas da empresa. Produtos, notas fiscais, duplicatas e conciliação são as telas do Fiscal (mesmos dados); o ERP acrescenta
/// pedidos de venda e compra, estoque, transporte e o painel do empresário. Permissões ERP_* (e FISCAL_* nas telas do Fiscal) nos serviços.
/// </summary>
public sealed class ModuloErp : IModuloDesktop
{
    public string Nome => "Vorticis ERP";
    public Vorticis.Application.Cadastros.ModulosEmpresa ModuloEmpresa => Vorticis.Application.Cadastros.ModulosEmpresa.Erp;
    public string NomeCurto => "ERP";
    public string Subtitulo => "Gestão empresarial: vendas, compras, estoque, financeiro e transporte";
    public string NomeExecutavel => "VorticisErp";
    public string CorDestaque => "#0F766E";   // verde-petróleo
    public string CorBarra => "#134E4A";

    public IReadOnlyList<ItemMenu> ItensCadastros { get; } =
    [
        ItemMenu.Tela("Produtos", F.Telas.Produtos),
        ItemMenu.Tela("Veículos e motoristas", ErpTelas.Veiculos),
    ];

    public IReadOnlyList<ItemMenu> Menus { get; } =
    [
        ItemMenu.Submenu("_Vendas",
            ItemMenu.Tela("Pedidos de venda", ErpTelas.PedidosVenda),
            ItemMenu.Tela("Notas fiscais de saída", F.Telas.NotaSaida),
            ItemMenu.EmBreve("Emissão de NF-e na SEFAZ")),
        ItemMenu.Submenu("C_ompras",
            ItemMenu.Tela("Pedidos de compra", ErpTelas.PedidosCompra),
            ItemMenu.Tela("Notas fiscais de entrada", F.Telas.NotaEntrada),
            ItemMenu.Tela("Importação de XML", F.Telas.ImportacaoXml)),
        ItemMenu.Submenu("_Estoque",
            ItemMenu.Tela("Posição e movimentos", ErpTelas.Estoque)),
        ItemMenu.Submenu("F_inanceiro",
            ItemMenu.Tela("Contas a pagar e a receber", F.Telas.Duplicatas),
            ItemMenu.Tela("Conciliação bancária", F.Telas.ConciliacaoDuplicatas),
            ItemMenu.Separador,
            ItemMenu.Tela("Relatório de contas a pagar", F.Telas.RelDuplicatasPagar),
            ItemMenu.Tela("Relatório de contas a receber", F.Telas.RelDuplicatasReceber),
            ItemMenu.Tela("Posição por vencimento", F.Telas.RelPosicaoDuplicatas)),
        ItemMenu.Submenu("_Transporte",
            ItemMenu.Tela("Conhecimentos e manifestos (CT-e e MDF-e)", ErpTelas.Transporte),
            ItemMenu.EmBreve("Emissão de CT-e e MDF-e na SEFAZ")),
        ItemMenu.Submenu("_Relatórios",
            ItemMenu.Tela("Painel do empresário", ErpTelas.Painel)),
    ];

    public IReadOnlyList<AtalhoTela> Atalhos { get; } =
    [
        new("Ctrl+P", ErpTelas.Painel, "Painel do empresário"),
        new("Ctrl+V", ErpTelas.PedidosVenda, "Pedidos de venda"),
    ];

    private static bool Pode(MainViewModelBase m, params Permissao[] p) => m.TemAcessoNaEmpresa && p.Any(m.Pode);

    public void RegistrarTelas(CatalogoTelas c)
    {
        c.Instancia("Painel", m => new PainelViewModel(m), aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(ErpTelas.Painel, "Relatórios › Painel do empresário", m => Pode(m, Permissao.ErpRelatorios), v => v.CarregarAsync());
        c.Instancia("PedidosVenda", m => new PedidosVendaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(ErpTelas.PedidosVenda, "Vendas › Pedidos de venda", m => Pode(m, Permissao.ErpVendas, Permissao.ErpRelatorios), v => v.CarregarAsync());
        c.Instancia("PedidosCompra", m => new PedidosCompraViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(ErpTelas.PedidosCompra, "Compras › Pedidos de compra", m => Pode(m, Permissao.ErpCompras, Permissao.ErpRelatorios), v => v.CarregarAsync());
        c.Instancia("Estoque", m => new EstoqueViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(ErpTelas.Estoque, "Estoque › Posição e movimentos", m => Pode(m, Permissao.ErpEstoque, Permissao.ErpRelatorios), v => v.CarregarAsync());
        c.Instancia("Veiculos", m => new VeiculosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(ErpTelas.Veiculos, "Cadastros › Veículos e motoristas", m => Pode(m, Permissao.ErpTransporte), v => v.CarregarAsync());
        c.Instancia("Transporte", m => new TransporteViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(ErpTelas.Transporte, "Transporte › Conhecimentos e manifestos", m => Pode(m, Permissao.ErpTransporte), v => v.CarregarAsync());

        // Telas do Fiscal usadas pelo ERP (mesmas chaves, mesmo manual)
        c.Instancia("ImportacaoXml", m => new F.ImportacaoXmlViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(F.Telas.ImportacaoXml, "Compras › Importação de XML", m => m.Pode(Permissao.FiscalDocumentosImportar));
        c.Instancia("NotaEntrada", m => new F.NotaEntradaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(F.Telas.NotaEntrada, "Compras › Notas fiscais de entrada", m => m.Pode(Permissao.FiscalDocumentosConsultar), v => v.CarregarAsync());
        c.Instancia("NotaSaida", m => new F.NotaSaidaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(F.Telas.NotaSaida, "Vendas › Notas fiscais de saída", m => m.Pode(Permissao.FiscalDocumentosConsultar), v => v.CarregarAsync());
        c.Instancia("Duplicatas", m => new F.DuplicatasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(F.Telas.Duplicatas, "Financeiro › Contas a pagar e a receber", m => m.Pode(Permissao.FiscalDocumentosConsultar), v => v.CarregarAsync());
        c.Instancia("ConciliacaoDuplicatas", m => new F.ConciliacaoDuplicatasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(F.Telas.ConciliacaoDuplicatas, "Financeiro › Conciliação bancária", m => m.Pode(Permissao.FiscalDuplicatasBaixar));
        c.Instancia("Produtos", m => new F.ProdutosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(F.Telas.Produtos, "Cadastros › Produtos", m => m.Pode(Permissao.FiscalDocumentosConsultar), v => v.CarregarAsync());
        c.Instancia("RelDuplicatasPagar", m => new F.RelDuplicatasPagarViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(F.Telas.RelDuplicatasPagar, "Financeiro › Relatório de contas a pagar", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("RelDuplicatasReceber", m => new F.RelDuplicatasReceberViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(F.Telas.RelDuplicatasReceber, "Financeiro › Relatório de contas a receber", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("RelPosicaoDuplicatas", m => new F.RelPosicaoDuplicatasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(F.Telas.RelPosicaoDuplicatas, "Financeiro › Posição por vencimento", m => m.Pode(Permissao.FiscalRelatorios));
        c.Instancia("ParametrosModulo", m => new F.ParametrosFiscalViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(TelasComuns.Parametros, "Cadastros › Parâmetros", m => m.TemAcessoNaEmpresa, v => v.CarregarAsync());
    }
}

public sealed class MainViewModel() : MainViewModelBase(new ModuloErp());
