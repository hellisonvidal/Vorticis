using Vorticis.Condominio.Desktop.ViewModels;
using Vorticis.Desktop.Comum.Modulos;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Condominio.Desktop;

/// <summary>
/// Módulo Condomínio: a empresa ativa é o condomínio. Proprietários, inquilinos e fornecedores são os participantes do cadastro comum
/// (Cadastros › Clientes e Fornecedores). Permissões CONDOMINIO_* conferidas nos serviços.
/// </summary>
public sealed class ModuloCondominio : IModuloDesktop
{
    public string Nome => "Vorticis Condomínio";
    public Vorticis.Application.Cadastros.ModulosEmpresa ModuloEmpresa => Vorticis.Application.Cadastros.ModulosEmpresa.Condominio;
    public string NomeCurto => "Condomínio";
    public string Subtitulo => "Administração de condomínios";
    public string NomeExecutavel => "VorticisCondominio";
    public string CorDestaque => "#BE123C";   // vermelho-rubi (ícone e selo do módulo)
    public string CorBarra => "#881337";      // vinho
    public bool UsaCalendarioObrigacoes => false;
    public bool UsaSindico => true;
    public string RotuloEmpresas => "Condomínios";

    public IReadOnlyList<ItemMenu> ItensCadastros { get; } =
    [
        ItemMenu.Tela("Unidades e condôminos", CondominioTelas.Unidades),
        ItemMenu.Tela("Áreas comuns e reservas", CondominioTelas.Reservas),
    ];

    public IReadOnlyList<ItemMenu> Menus { get; } =
    [
        ItemMenu.Submenu("_Financeiro",
            ItemMenu.Tela("Taxas condominiais", CondominioTelas.Taxas),
            ItemMenu.Tela("Cobranças e recebimentos", CondominioTelas.Cobrancas),
            ItemMenu.Tela("Despesas do condomínio", CondominioTelas.Despesas),
            ItemMenu.Tela("Rateio entre unidades", CondominioTelas.Rateio)),
        ItemMenu.Submenu("_Administração",
            ItemMenu.Tela("Ocorrências e assembleias", CondominioTelas.Ocorrencias)),
        ItemMenu.Submenu("_Relatórios",
            ItemMenu.Tela("Prestação de contas e inadimplência", CondominioTelas.Prestacao),
            ItemMenu.Tela("Atas de Assembleia", CondominioTelas.Atas)),
    ];

    public IReadOnlyList<AtalhoTela> Atalhos { get; } = [new("Ctrl+B", CondominioTelas.Cobrancas, "Cobranças e recebimentos")];

    private static bool Pode(MainViewModelBase m, params Permissao[] p) => m.TemAcessoNaEmpresa && p.Any(m.Pode);
    private static readonly Permissao[] Todas = [Permissao.CondominioCadastros, Permissao.CondominioFinanceiro, Permissao.CondominioRelatorios];

    public void RegistrarTelas(CatalogoTelas c)
    {
        c.Instancia("Unidades", m => new UnidadesViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(CondominioTelas.Unidades, "Cadastros › Unidades e condôminos", m => Pode(m, Todas), v => v.CarregarAsync());
        c.Instancia("Reservas", m => new ReservasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(CondominioTelas.Reservas, "Cadastros › Áreas comuns e reservas", m => Pode(m, Todas), v => v.CarregarAsync());
        c.Instancia("Taxas", m => new TaxasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(CondominioTelas.Taxas, "Financeiro › Taxas condominiais", m => Pode(m, Permissao.CondominioFinanceiro, Permissao.CondominioRelatorios), v => v.CarregarAsync());
        c.Instancia("Cobrancas", m => new CobrancasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(CondominioTelas.Cobrancas, "Financeiro › Cobranças e recebimentos", m => Pode(m, Permissao.CondominioFinanceiro, Permissao.CondominioRelatorios), v => v.CarregarAsync());
        c.Instancia("Despesas", m => new DespesasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(CondominioTelas.Despesas, "Financeiro › Despesas do condomínio", m => Pode(m, Permissao.CondominioFinanceiro, Permissao.CondominioRelatorios), v => v.CarregarAsync());
        c.Instancia("Ocorrencias", m => new OcorrenciasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(CondominioTelas.Ocorrencias, "Administração › Ocorrências e assembleias", m => Pode(m, Todas), v => v.CarregarAsync());
        c.Instancia("Prestacao", m => new PrestacaoViewModel(m), aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(CondominioTelas.Prestacao, "Relatórios › Prestação de contas e inadimplência", m => Pode(m, Permissao.CondominioRelatorios), v => v.CarregarAsync());
        c.Instancia("Rateio", m => new RateioViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(CondominioTelas.Rateio, "Financeiro › Rateio entre unidades", m => Pode(m, Permissao.CondominioFinanceiro), v => v.CarregarAsync());
        c.Instancia("Atas", m => new AtasAssembleiaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(CondominioTelas.Atas, "Relatórios › Atas de Assembleia", m => Pode(m, Todas), v => v.CarregarAsync());
        c.Instancia("ParametrosModulo", m => new ParametrosCondominioViewModel(m), aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(TelasComuns.Parametros, "Cadastros › Parâmetros", m => m.TemAcessoNaEmpresa, v => v.CarregarAsync());
    }
}

public sealed class MainViewModel() : MainViewModelBase(new ModuloCondominio());
