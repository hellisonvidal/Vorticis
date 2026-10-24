using Vorticis.Desktop.Comum.Modulos;
using Vorticis.Domain.Seguranca;
using Vorticis.Escritorio.Desktop.ViewModels;

namespace Vorticis.Escritorio.Desktop;

/// <summary>
/// Módulo Escritório: gestão do escritório contábil. Os cadastros (empresas, contadores, sócios, clientes e fornecedores) são os do casco,
/// gravados no banco único e usados por todos os módulos da suíte. Permissões ESCRITORIO_* conferidas nos serviços.
/// </summary>
public sealed class ModuloEscritorio : IModuloDesktop
{
    public string Nome => "Vorticis Escritório";
    public Vorticis.Application.Cadastros.ModulosEmpresa ModuloEmpresa => Vorticis.Application.Cadastros.ModulosEmpresa.Escritorio;
    public string NomeCurto => "Escritório";
    public string Subtitulo => "Gestão do escritório e cadastros empresariais";
    public string NomeExecutavel => "VorticisEscritorio";
    public string CorDestaque => "#1D4ED8";   // azul
    public string CorBarra => "#1E3A8A";

    public IReadOnlyList<ItemMenu> ItensCadastros { get; } = [ItemMenu.Tela("Sócios", TelasComuns.Socios)];

    public IReadOnlyList<ItemMenu> Menus { get; } =
    [
        ItemMenu.Submenu("_Escritório",
            ItemMenu.Tela("Carteira de clientes", EscritorioTelas.Carteira),
            ItemMenu.Tela("Tarefas e prazos", EscritorioTelas.Tarefas),
            ItemMenu.Tela("Protocolo de documentos", EscritorioTelas.Protocolos)),
        ItemMenu.Submenu("_Honorários",
            ItemMenu.Tela("Contratos e cobranças", EscritorioTelas.Honorarios)),
    ];

    public IReadOnlyList<AtalhoTela> Atalhos { get; } =
    [
        new("Ctrl+T", EscritorioTelas.Tarefas, "Tarefas e prazos"),
    ];

    private static bool Pode(MainViewModelBase m, params Permissao[] p) => m.TemAcessoNaEmpresa && p.Any(m.Pode);

    public void RegistrarTelas(CatalogoTelas c)
    {
        c.Instancia("Carteira", m => new CarteiraViewModel(m), aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(EscritorioTelas.Carteira, "Escritório › Carteira de clientes",
                  m => Pode(m, Permissao.EscritorioRelatorios, Permissao.EscritorioTarefas, Permissao.EscritorioHonorarios), v => v.CarregarAsync());
        c.Instancia("Tarefas", m => new TarefasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(EscritorioTelas.Tarefas, "Escritório › Tarefas e prazos", m => Pode(m, Permissao.EscritorioTarefas, Permissao.EscritorioRelatorios), v => v.CarregarAsync());
        c.Instancia("Protocolos", m => new ProtocolosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(EscritorioTelas.Protocolos, "Escritório › Protocolo de documentos", m => Pode(m, Permissao.EscritorioTarefas, Permissao.EscritorioRelatorios), v => v.CarregarAsync());
        c.Instancia("Honorarios", m => new HonorariosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(EscritorioTelas.Honorarios, "Honorários › Contratos e cobranças", m => Pode(m, Permissao.EscritorioHonorarios, Permissao.EscritorioRelatorios), v => v.CarregarAsync());
        c.Instancia("ParametrosModulo", m => new ParametrosViewModelEscritorio(m), aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(TelasComuns.Parametros, "Cadastros › Parâmetros", m => m.TemAcessoNaEmpresa, v => v.CarregarAsync());
    }
}

public sealed class MainViewModel() : MainViewModelBase(new ModuloEscritorio());
