using Vorticis.Desktop.Comum.Modulos;
using Vorticis.Domain.Seguranca;
using Vorticis.Escritorio.Desktop.ViewModels;

namespace Vorticis.Escritorio.Desktop;

/// <summary>
/// Módulo Escritório: gerenciador do escritório contábil, com cadastros próprios (clientes e fornecedores do escritório); o cliente
/// pode ser cadastrado como empresa nos módulos do contrato. A empresa ativa é o próprio escritório. Permissões ESCRITORIO_* conferidas nos serviços.
/// </summary>
public sealed class ModuloEscritorio : IModuloDesktop
{
    public string Nome => "Vorticis Escritório";
    public Vorticis.Application.Cadastros.ModulosEmpresa ModuloEmpresa => Vorticis.Application.Cadastros.ModulosEmpresa.Escritorio;
    public string NomeCurto => "Escritório";
    public string Subtitulo => "Gestão do escritório contábil";
    public string NomeExecutavel => "VorticisEscritorio";
    public string CorDestaque => "#1D4ED8";   // azul (ícone e selo do módulo)
    public string CorBarra => "#1E3A8A";      // azul-marinho

    public bool UsaCadastrosComuns => false;
    public bool UsaClientesFornecedores => false;
    public bool UsaCalendarioObrigacoes => false;

    /// <summary>Cadastros: Escritório (o cadastro de empresas, aqui só os escritórios), Clientes e Fornecedores.</summary>
    public string RotuloEmpresas => "Escritório";
    public string RotuloEmpresaAtiva => "Escritório";

    /// <summary>Usuários, controle de acesso e banco de dados (conexão, backup, restauração, limpeza) da suíte ficam no Escritório.</summary>
    public bool GerenciaSistema => true;

    public IReadOnlyList<ItemMenu> ItensCadastros { get; } =
    [
        ItemMenu.Tela("Clientes", EscritorioTelas.Clientes),
        ItemMenu.Tela("Fornecedores", EscritorioTelas.Fornecedores),
    ];

    public IReadOnlyList<ItemMenu> Menus { get; } =
    [
        ItemMenu.Submenu("_Organização",
            ItemMenu.Tela("Tarefas e prazos", EscritorioTelas.Tarefas),
            ItemMenu.Tela("Protocolos de documentos", EscritorioTelas.Protocolos)),
        ItemMenu.Submenu("_Relatórios",
            ItemMenu.Tela("Gerador de contratos", EscritorioTelas.Contratos),
            ItemMenu.Tela("Carteira de clientes", EscritorioTelas.Carteira),
            ItemMenu.Tela("Relatório de honorários", EscritorioTelas.RelHonorarios)),
    ];

    public IReadOnlyList<AtalhoTela> Atalhos { get; } =
    [
        new("Ctrl+T", EscritorioTelas.Tarefas, "Tarefas e prazos"),
    ];

    private static bool Pode(MainViewModelBase m, params Permissao[] p) => m.TemAcessoNaEmpresa && p.Any(m.Pode);

    public void RegistrarTelas(CatalogoTelas c)
    {
        c.Instancia("Carteira", m => new CarteiraViewModel(m), aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(EscritorioTelas.Carteira, "Relatórios › Carteira de clientes",
                  m => Pode(m, Permissao.EscritorioRelatorios, Permissao.EscritorioTarefas, Permissao.EscritorioHonorarios), v => v.CarregarAsync());
        c.Instancia("Tarefas", m => new TarefasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(EscritorioTelas.Tarefas, "Organização › Tarefas e prazos", m => Pode(m, Permissao.EscritorioTarefas, Permissao.EscritorioRelatorios), v => v.CarregarAsync());
        c.Instancia("Protocolos", m => new ProtocolosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(EscritorioTelas.Protocolos, "Organização › Protocolos de documentos", m => Pode(m, Permissao.EscritorioTarefas, Permissao.EscritorioRelatorios), v => v.CarregarAsync());
        c.Instancia("ClientesEscritorio", m => new ClientesEscritorioViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(EscritorioTelas.Clientes, "Cadastros › Clientes",
                  m => Pode(m, Permissao.EscritorioTarefas, Permissao.EscritorioHonorarios, Permissao.EscritorioRelatorios), v => v.CarregarAsync());
        c.Instancia("FornecedoresEscritorio", m => new FornecedoresEscritorioViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(EscritorioTelas.Fornecedores, "Cadastros › Fornecedores",
                  m => Pode(m, Permissao.EscritorioTarefas, Permissao.EscritorioHonorarios, Permissao.EscritorioRelatorios), v => v.CarregarAsync());
        c.Instancia("Contratos", m => new ContratosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(EscritorioTelas.Contratos, "Relatórios › Gerador de contratos", m => Pode(m, Permissao.EscritorioHonorarios, Permissao.EscritorioRelatorios), v => v.CarregarAsync());
        c.Instancia("RelHonorarios", m => new RelHonorariosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoesExportacao())
            .Tela(EscritorioTelas.RelHonorarios, "Relatórios › Relatório de honorários", m => Pode(m, Permissao.EscritorioRelatorios));
        c.Instancia("ParametrosModulo", m => new ParametrosViewModelEscritorio(m), aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(TelasComuns.Parametros, "Cadastros › Parâmetros", m => m.TemAcessoNaEmpresa, v => v.CarregarAsync());
    }
}

public sealed class MainViewModel() : MainViewModelBase(new ModuloEscritorio());
