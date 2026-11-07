using Vorticis.Desktop.Comum.Modulos;
using Vorticis.Domain.Seguranca;
using Vorticis.Escritorio.Desktop.ViewModels;

namespace Vorticis.Escritorio.Desktop;

/// <summary>
/// Módulo Escritório: só a gestão do próprio escritório contábil. Os clientes cadastrados aqui são as empresas usadas nos demais
/// módulos (cadastradas neles pelos módulos do contrato); o próprio escritório pode ser um deles. A empresa ativa é o próprio escritório, mostrado no topo como "Clientes". Permissões ESCRITORIO_* conferidas nos serviços.
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

    /// <summary>
    /// Cadastros: Dados do escritório (só o próprio escritório; não é o cadastro geral de empresas) e Clientes, que é onde as empresas
    /// atendidas são cadastradas e levadas aos módulos. O escritório escolhido no topo aparece como "Clientes:".
    /// </summary>
    public string RotuloEmpresas => "Dados do escritório";
    public string RotuloEmpresaAtiva => "Cliente";

    /// <summary>A barra mostra os clientes de Cadastros › Clientes (o escritório, 0001, é o primeiro); o escolhido filtra as telas do módulo.</summary>
    public Func<IContexto, Task<IReadOnlyList<ClienteNoSeletor>>>? ClientesDoSeletor => async ctx =>
        ctx.App is { } app && ctx.Sessao is { } sessao && ctx.EmpresaId is { } escritorio
            ? (await app.Escritorio().ListarPessoasAsync(escritorio, sessao.Id)).Where(p => p.Cliente && p.Ativo).OrderBy(p => p.Codigo)
                  .Select(p => new ClienteNoSeletor(p.Id, p.Codigo, p.Nome)).ToList()
            : [];

    /// <summary>Usuários, controle de acesso e banco de dados (conexão, backup, restauração, limpeza) da suíte ficam no Escritório.</summary>
    public bool GerenciaSistema => true;

    public IReadOnlyList<ItemMenu> ItensCadastros { get; } =
    [
        ItemMenu.Tela("Clientes", EscritorioTelas.Clientes),
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

    /// <summary>Ajuda › Dicas: as dicas deste módulo (as gerais vêm do casco).</summary>
    public IReadOnlyList<Dica> Dicas { get; } =
    [
        new("Escritório", "Tarefas que se repetem", "Marque Repetir (todo mês ou todo ano) nas tarefas de obrigações: ao concluir, a próxima é criada sozinha. Ctrl+T abre as tarefas.", EscritorioTelas.Tarefas),
        new("Escritório", "Protocole o que entrega e recebe", "Registre em Protocolos cada documento entregue ao cliente ou recebido dele: fica a prova de quem recebeu e quando.", EscritorioTelas.Protocolos),
        new("Escritório", "Cliente já vira empresa nos módulos", "Na aba Módulos do contrato do cliente, marque Contabilidade, Fiscal, Folha...: ele é cadastrado como empresa nesses módulos com o mesmo código.", EscritorioTelas.Clientes),
        new("Escritório", "Contrato pronto em PDF ou Word", "O Gerador de contratos monta o contrato de prestação de serviços com os dados do cliente e do escritório; o PDF pode sair assinado digitalmente.", EscritorioTelas.Contratos),
        new("Escritório", "Carteira de clientes", "Relatórios › Carteira de clientes mostra de uma vez as tarefas atrasadas e os honorários em aberto de cada cliente.", EscritorioTelas.Carteira),
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
