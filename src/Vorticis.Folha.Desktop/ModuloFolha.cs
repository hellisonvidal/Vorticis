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
    public void PrepararServicos(Vorticis.Infra.VorticisApp app) => _ = app.Folha();

    public string Nome => "Vorticis Folha de Pagamento";
    public Vorticis.Application.Cadastros.ModulosEmpresa ModuloEmpresa => Vorticis.Application.Cadastros.ModulosEmpresa.Folha;
    public string NomeCurto => "Folha";
    public string Subtitulo => "Folha de pagamento e eSocial";
    public string NomeExecutavel => "VorticisFolha";
    public string CorDestaque => "#7E22CE";   // roxo (ícone e selo do módulo)
    public string CorBarra => "#3B0764";      // roxo-escuro

    // Clientes e fornecedores não têm uso na folha; a competência de trabalho fica ao lado do seletor de empresas.
    public bool UsaClientesFornecedores => false;
    public string? RotuloPeriodo => "Competência";

    public IReadOnlyList<ItemMenu> ItensCadastros { get; } =
    [
        ItemMenu.Submenu("Funcionários",
            ItemMenu.Tela("Cadastro de funcionários", FolhaTelas.Funcionarios),
            ItemMenu.Tela("Cargos", FolhaTelas.Cargos),
            ItemMenu.Tela("Departamentos", FolhaTelas.Departamentos),
            ItemMenu.Tela("Rubricas (eventos)", FolhaTelas.Rubricas)),
        ItemMenu.Tela("Sócios (pró-labore)", Vorticis.Desktop.Comum.ViewModels.TelasComuns.Socios),
        ItemMenu.Submenu("Contábeis",
            ItemMenu.Tela("Eventos contábeis", FolhaTelas.EventosContabeis),
            ItemMenu.Tela("Centros de custo", Vorticis.Desktop.Comum.ViewModels.TelasComuns.CentrosCusto)),
        ItemMenu.Submenu("Tabelas legais",
            ItemMenu.Tela("Tabelas da previdência social", FolhaTelas.TabelasPrevidencia),
            ItemMenu.Tela("Parâmetros e tabelas legais", FolhaTelas.Parametros)),
    ];

    public IReadOnlyList<ItemMenu> Menus { get; } =
    [
        ItemMenu.Submenu("C_álculos",
            ItemMenu.Submenu("_Folha",
                ItemMenu.Tela("Eventos variáveis", FolhaTelas.Eventos),
                ItemMenu.Tela("Cálculo da folha mensal", FolhaTelas.FolhaMensal),
                ItemMenu.Tela("Férias", FolhaTelas.Ferias),
                ItemMenu.Tela("13º salário", FolhaTelas.Decimo),
                ItemMenu.Tela("Rescisões", FolhaTelas.Rescisoes),
                ItemMenu.Separador,
                ItemMenu.Tela("Provisões de férias e 13º", FolhaTelas.Provisoes),
                ItemMenu.Tela("Contabilização", FolhaTelas.Contabilizacao),
                ItemMenu.Tela("Contabilização por eventos", FolhaTelas.ContabilizacaoEventos)),
            ItemMenu.Submenu("_Pró-labore",
                ItemMenu.Tela("Sócios e cálculo do pró-labore", FolhaTelas.ProLabore))),
        ItemMenu.Submenu("_Obrigações",
            ItemMenu.Submenu("eSocial",
                ItemMenu.Tela("Assistente de configuração", FolhaTelas.AssistenteEsocial),
                ItemMenu.Separador,
                ItemMenu.Tela("Geração dos eventos", FolhaTelas.ESocial),
                ItemMenu.Tela("Transmissão", FolhaTelas.Transmissao)),
            ItemMenu.Tela("FGTS Digital", FolhaTelas.FgtsDigital),
            ItemMenu.EmBreve("DCTFWeb e INSS"),
            ItemMenu.EmBreve("RAIS e DIRF")),
        ItemMenu.Submenu("_Relatórios",
            ItemMenu.Tela("Holerites e recibos", FolhaTelas.Holerites),
            ItemMenu.Tela("Resumo da folha", FolhaTelas.Resumo),
            ItemMenu.Tela("Encargos patronais", FolhaTelas.Encargos),
            ItemMenu.Tela("Provisões", FolhaTelas.Provisoes)),
    ];

    /// <summary>Ajuda › Dicas: as dicas deste módulo (as gerais vêm do casco).</summary>
    public IReadOnlyList<Dica> Dicas { get; } =
    [
        new("Folha", "Feche a competência depois de conferir", "Feche a folha do mês depois de conferir os holerites: a competência fechada não recalcula por engano e libera a guia do FGTS Digital.", FolhaTelas.FolhaMensal),
        new("Folha", "Assistente do eSocial", "Antes da primeira transmissão, rode Obrigações › eSocial › Assistente de configuração: ele aponta cada pendência e a tela para corrigir.", FolhaTelas.AssistenteEsocial),
        new("Folha", "FGTS Digital vence no dia 20", "A guia do FGTS Digital vence no dia 20 do mês seguinte (antecipa se não for dia útil). Confira o demonstrativo antes de emitir a guia no portal.", FolhaTelas.FgtsDigital),
        new("Folha", "Tabelas da previdência à mão", "Cadastros › Tabelas legais › Tabelas da previdência social mostra as faixas do INSS, o teto, o salário-família e o FGTS da competência, prontas para imprimir.", FolhaTelas.TabelasPrevidencia),
        new("Folha", "Pró-labore não tem FGTS", "O pró-labore do sócio paga INSS de contribuinte individual (11%, até o teto) e o patronal fora do Simples; não há FGTS nem 13º.", FolhaTelas.ProLabore),
        new("Folha", "Provisões todo mês", "Calcule as provisões de férias e 13º todo mês: a contabilidade fica com o passivo certo e o custo da folha não salta em dezembro.", FolhaTelas.Provisoes),
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
            .Tela(FolhaTelas.Funcionarios, "Cadastros › Funcionários › Cadastro de funcionários", Ver, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Cargos), m => new CargosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Cargos, "Cadastros › Funcionários › Cargos", Ver, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Departamentos), m => new DepartamentosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Departamentos, "Cadastros › Funcionários › Departamentos", Ver, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Rubricas), m => new RubricasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Rubricas, "Cadastros › Funcionários › Rubricas (eventos)", Ver, v => v.CarregarAsync());
        c.Instancia("ParametrosModulo", m => new ParametrosModuloFolhaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Vorticis.Desktop.Comum.ViewModels.TelasComuns.Parametros, "Cadastros › Parâmetros", m => m.TemAcessoNaEmpresa, v => v.CarregarAsync());
        c.Instancia("FgtsDigital", m => new FgtsDigitalViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(), aoLimpar: v => v.Limpar())
            .Tela(FolhaTelas.FgtsDigital, "Obrigações › FGTS Digital", Ver, v => v.GerarCommand.ExecuteAsync(null));
        c.Instancia("TabelasPrevidencia", m => new TabelasPrevidenciaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoesExportacao(), aoLimpar: v => v.Limpar())
            .Tela(FolhaTelas.TabelasPrevidencia, "Cadastros › Tabelas legais › Tabelas da previdência social", Ver, v => v.GerarCommand.ExecuteAsync(null));
        c.Instancia(nameof(MainViewModel.Parametros), m => new ParametrosFolhaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Parametros, "Cadastros › Tabelas legais › Parâmetros e tabelas legais", Ver, v => v.CarregarAsync());

        c.Instancia(nameof(MainViewModel.Eventos), m => new EventosVariaveisViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Eventos, "Cálculos › Folha › Eventos variáveis", Ver, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.FolhaMensal), m => new FolhaMensalViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoLimpar: v => { v.Calculos.Clear(); v.Itens.Clear(); },
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.FolhaMensal, "Cálculos › Folha › Cálculo da folha mensal", Ver, v => v.CarregarAsync())
            .Tela(FolhaTelas.Holerites, "Relatórios › Holerites e recibos", m => Pode(m, Permissao.FolhaRelatorios, Permissao.FolhaConsultar, Permissao.FolhaCalcular), v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Ferias), m => new FeriasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Ferias, "Cálculos › Folha › Férias", Ver, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Decimo), m => new DecimoTerceiroViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoLimpar: v => { v.Calculos.Clear(); v.Itens.Clear(); },
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Decimo, "Cálculos › Folha › 13º salário", Ver, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Rescisoes), m => new RescisoesViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Rescisoes, "Cálculos › Folha › Rescisões", Ver, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.ProLabore), m => new ProLaboreViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.ProLabore, "Cálculos › Pró-labore › Sócios e cálculo do pró-labore", Ver, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Contabilizacao), m => new ContabilizacaoFolhaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Contabilizacao, "Cálculos › Folha › Contabilização", m => Pode(m, Permissao.FolhaContabilizar, Permissao.FolhaParametrosManter), v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.ContabilizacaoEventos), m => new ContabilizacaoEventosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoLimpar: v => { v.Eventos.Clear(); v.Lancamento.Clear(); v.Total = ""; },
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.ContabilizacaoEventos, "Cálculos › Folha › Contabilização por eventos", m => Pode(m, Permissao.FolhaContabilizar), v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.EventosContabeis), m => new EventosContabeisViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.EventosContabeis, "Cadastros › Contábeis › Eventos contábeis", m => Pode(m, Permissao.FolhaContabilizar, Permissao.FolhaParametrosManter), v => v.CarregarAsync());

        c.Instancia(nameof(MainViewModel.ESocial), m => new ESocialViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoLimpar: v => { v.Eventos.Clear(); v.Xml = ""; },
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.ESocial, "Obrigações › eSocial", m => Pode(m, Permissao.FolhaESocial), v => v.CarregarAsync());
        c.Instancia("AssistenteEsocial", m => new AssistenteEsocialViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.AssistenteEsocial, "Obrigações › eSocial › Assistente de configuração", m => Pode(m, Permissao.FolhaESocial), v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Transmissao), m => new TransmissaoEsocialViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoLimpar: v => v.Limpar(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(FolhaTelas.Transmissao, "Obrigações › eSocial › Transmissão", m => Pode(m, Permissao.FolhaESocial), v => v.CarregarAsync());

        c.Instancia(nameof(MainViewModel.Resumo), m => new ResumoFolhaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoesExportacao(), aoLimpar: v => v.Limpar())
            .Tela(FolhaTelas.Resumo, "Relatórios › Resumo da folha", m => Pode(m, Permissao.FolhaRelatorios));
        c.Instancia(nameof(MainViewModel.Encargos), m => new EncargosFolhaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoesExportacao(), aoLimpar: v => v.Limpar())
            .Tela(FolhaTelas.Encargos, "Relatórios › Encargos patronais", m => Pode(m, Permissao.FolhaRelatorios));
        c.Instancia(nameof(MainViewModel.Provisoes), m => new ProvisoesFolhaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(), aoLimpar: v => v.Limpar())
            .Tela(FolhaTelas.Provisoes, "Relatórios › Provisões de férias e 13º", m => Pode(m, Permissao.FolhaRelatorios, Permissao.FolhaCalcular));
    }
}
