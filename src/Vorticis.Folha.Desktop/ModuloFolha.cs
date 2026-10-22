using Vorticis.Desktop.Comum.Modulos;
using Vorticis.Domain.Seguranca;
using Vorticis.Folha.Desktop.ViewModels;

namespace Vorticis.Folha.Desktop;

/// <summary>
/// Módulo Folha de Pagamento: cadastros (funcionários, cargos, departamentos, rubricas, parâmetros), folha mensal com eventos
/// variáveis, férias, 13º salário, rescisões, provisões, contabilização, eSocial e relatórios. As permissões (FOLHA_*) são
/// verificadas nos serviços; o menu apenas esconde o que o perfil não permite.
/// </summary>
public sealed class ModuloFolha : IModuloDesktop
{
    public string Nome => "Vorticis Folha de Pagamento";
    public string NomeCurto => "Folha";
    public string Subtitulo => "Folha de pagamento e eSocial";
    public string NomeExecutavel => "VorticisFolha";
    public string CorDestaque => "#6D28D9";   // roxo
    public string CorBarra => "#4C1D95";

    public IReadOnlyList<ItemMenu> ItensCadastros { get; } = [];

    public IReadOnlyList<ItemMenu> Menus { get; } =
    [
        ItemMenu.Submenu("_Funcionários",
            ItemMenu.Tela("Cadastro de funcionários", FolhaTelas.Funcionarios),
            ItemMenu.Tela("Cargos", FolhaTelas.Cargos),
            ItemMenu.Tela("Departamentos", FolhaTelas.Departamentos),
            ItemMenu.Tela("Rubricas (eventos)", FolhaTelas.Rubricas),
            ItemMenu.Separador,
            ItemMenu.Tela("Parâmetros e tabelas legais", FolhaTelas.Parametros)),
        ItemMenu.Submenu("_Folha",
            ItemMenu.Tela("Eventos variáveis", FolhaTelas.Eventos),
            ItemMenu.Tela("Cálculo da folha mensal", FolhaTelas.FolhaMensal),
            ItemMenu.Tela("Férias", FolhaTelas.Ferias),
            ItemMenu.Tela("13º salário", FolhaTelas.Decimo),
            ItemMenu.Tela("Rescisões", FolhaTelas.Rescisoes),
            ItemMenu.Separador,
            ItemMenu.Tela("Provisões de férias e 13º", FolhaTelas.Provisoes),
            ItemMenu.Tela("Contabilização", FolhaTelas.Contabilizacao)),
        ItemMenu.Submenu("_Obrigações",
            ItemMenu.Tela("eSocial", FolhaTelas.ESocial),
            ItemMenu.EmBreve("FGTS (GFD)"),
            ItemMenu.EmBreve("DCTFWeb e INSS"),
            ItemMenu.EmBreve("RAIS e DIRF")),
        ItemMenu.Submenu("_Relatórios",
            ItemMenu.Tela("Holerites e recibos", FolhaTelas.Holerites),
            ItemMenu.Tela("Resumo da folha", FolhaTelas.Resumo),
            ItemMenu.Tela("Encargos patronais", FolhaTelas.Encargos),
            ItemMenu.Tela("Provisões", FolhaTelas.Provisoes)),
    ];

    public IReadOnlyList<AtalhoTela> Atalhos { get; } =
    [
        new("Ctrl+F", FolhaTelas.FolhaMensal, "Cálculo da folha mensal"),
        new("Ctrl+E", FolhaTelas.Eventos, "Eventos variáveis"),
    ];

    private static bool Pode(Vorticis.Desktop.Comum.ViewModels.MainViewModelBase m, params Permissao[] permissoes)
        => m.TemAcessoNaEmpresa && permissoes.Any(m.Pode);

    private static bool Ver(Vorticis.Desktop.Comum.ViewModels.MainViewModelBase m)
        => Pode(m, Permissao.FolhaConsultar, Permissao.FolhaCadastrosManter, Permissao.FolhaParametrosManter, Permissao.FolhaEventosLancar, Permissao.FolhaCalcular,
                Permissao.FolhaFechar, Permissao.FolhaReabrir, Permissao.FolhaContabilizar, Permissao.FolhaESocial, Permissao.FolhaRelatorios);

    public void RegistrarTelas(CatalogoTelas c)
    {
        c.Instancia(nameof(MainViewModel.Funcionarios), m => new FuncionariosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Funcionarios, "Folha › Funcionários › Cadastro de funcionários", Ver, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Cargos), m => new CargosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Cargos, "Folha › Funcionários › Cargos", Ver, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Departamentos), m => new DepartamentosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Departamentos, "Folha › Funcionários › Departamentos", Ver, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Rubricas), m => new RubricasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Rubricas, "Folha › Funcionários › Rubricas (eventos)", Ver, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Parametros), m => new ParametrosFolhaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Parametros, "Folha › Parâmetros e tabelas legais", Ver, v => v.CarregarAsync());

        c.Instancia(nameof(MainViewModel.Eventos), m => new EventosVariaveisViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Eventos, "Folha › Eventos variáveis", Ver, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.FolhaMensal), m => new FolhaMensalViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoLimpar: v => { v.Calculos.Clear(); v.Itens.Clear(); },
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.FolhaMensal, "Folha › Cálculo da folha mensal", Ver, v => v.CarregarAsync())
            .Tela(FolhaTelas.Holerites, "Relatórios › Holerites e recibos", m => Pode(m, Permissao.FolhaRelatorios, Permissao.FolhaConsultar, Permissao.FolhaCalcular), v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Ferias), m => new FeriasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Ferias, "Folha › Férias", Ver, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Decimo), m => new DecimoTerceiroViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoLimpar: v => { v.Calculos.Clear(); v.Itens.Clear(); },
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Decimo, "Folha › 13º salário", Ver, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Rescisoes), m => new RescisoesViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Rescisoes, "Folha › Rescisões", Ver, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Contabilizacao), m => new ContabilizacaoFolhaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Contabilizacao, "Folha › Contabilização", m => Pode(m, Permissao.FolhaContabilizar, Permissao.FolhaParametrosManter), v => v.CarregarAsync());

        c.Instancia(nameof(MainViewModel.ESocial), m => new ESocialViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoLimpar: v => { v.Eventos.Clear(); v.Xml = ""; },
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.ESocial, "Obrigações › eSocial", m => Pode(m, Permissao.FolhaESocial), v => v.CarregarAsync());

        c.Instancia(nameof(MainViewModel.Resumo), m => new ResumoFolhaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoesExportacao(), aoLimpar: v => v.Limpar())
            .Tela(FolhaTelas.Resumo, "Relatórios › Resumo da folha", m => Pode(m, Permissao.FolhaRelatorios));
        c.Instancia(nameof(MainViewModel.Encargos), m => new EncargosFolhaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoesExportacao(), aoLimpar: v => v.Limpar())
            .Tela(FolhaTelas.Encargos, "Relatórios › Encargos patronais", m => Pode(m, Permissao.FolhaRelatorios));
        c.Instancia(nameof(MainViewModel.Provisoes), m => new ProvisoesFolhaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(), aoLimpar: v => v.Limpar())
            .Tela(FolhaTelas.Provisoes, "Relatórios › Provisões de férias e 13º", m => Pode(m, Permissao.FolhaRelatorios, Permissao.FolhaCalcular));
    }
}
