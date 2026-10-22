using Vorticis.Desktop.Comum.Modulos;
using Vorticis.Desktop.ViewModels;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Desktop;

/// <summary>Módulo Contábil: menus, atalhos e telas (lançamentos, relatórios, imobilizado, plano de contas, importações...).</summary>
public sealed class ModuloContabil : IModuloDesktop
{
    public string Nome => "Vorticis Contábil";
    public Vorticis.Application.Cadastros.ModulosEmpresa ModuloEmpresa => Vorticis.Application.Cadastros.ModulosEmpresa.Contabil;
    public string NomeCurto => "Contábil";
    public string Subtitulo => "Escrituração Fiscal Contábil";
    public string NomeExecutavel => "VorticisContabil";
    public string CorDestaque => "#1B7F5A";   // verde da logo
    public string CorBarra => "#14305A";      // azul-marinho da logo

    public IReadOnlyList<ItemMenu> ItensCadastros { get; } =
    [
        ItemMenu.Tela("Plano de contas", Telas.PlanoContas),
        ItemMenu.Tela("Centros de custo", Telas.CentrosCusto),
        ItemMenu.Tela("Históricos", Telas.Historicos),
    ];

    public IReadOnlyList<ItemMenu> Menus { get; } =
    [
        ItemMenu.Submenu("_Lançamentos",
            ItemMenu.Submenu("_Contábeis",
                ItemMenu.Tela("Lançamento contábil", Telas.Lancamento, "Ctrl+L"),
                ItemMenu.Tela("Saldos iniciais (implantação)", Telas.SaldosIniciais),
                ItemMenu.Tela("Importar lançamentos (layout padrão)", Telas.ImportarLancamentos)),
            ItemMenu.Submenu("C_onciliações", ItemMenu.Tela("Conciliação automática", Telas.Conciliacao)),
            ItemMenu.Submenu("_Fechamentos",
                ItemMenu.Tela("Fechamento de períodos", Telas.Periodos),
                ItemMenu.Tela("Encerramento do exercício", Telas.Encerramento))),
        ItemMenu.Submenu("I_mobilizado",
            ItemMenu.Tela("Cadastro de bens", Telas.Bens),
            ItemMenu.Tela("Regras de depreciação (grupos)", Telas.GruposDepreciacao),
            ItemMenu.Separador,
            ItemMenu.Tela("Cálculo e lançamento da depreciação", Telas.Depreciacao)),
        ItemMenu.Submenu("C_onsultas",
            ItemMenu.Tela("Saldos de contas", Telas.ConsultaSaldos),
            ItemMenu.Tela("Lançamentos", Telas.ConsultaLancamentos),
            ItemMenu.Tela("Diferenças de saldos", Telas.DiferencasSaldos)),
        ItemMenu.Submenu("_Integrações",
            ItemMenu.Submenu("_Importar",
                ItemMenu.Tela("Planilhas (layouts personalizados)", Telas.ImportarPlanilhas),
                ItemMenu.Tela("Extratos bancários (OFX)", Telas.ImportarExtratos),
                ItemMenu.Tela("Lançamentos contábeis (layout padrão)", Telas.ImportarLancamentos),
                ItemMenu.Separador,
                ItemMenu.Tela("Plano de contas (planilha Excel/CSV)", Telas.ImportarPlano),
                ItemMenu.Tela("Plano referencial da RFB (tabela oficial)", Telas.ImportacaoReferencial),
                ItemMenu.Tela("Vínculo ao plano referencial (RFB)", Telas.MapeamentoReferencial)),
            ItemMenu.Submenu("_Exportar",
                ItemMenu.Tela("Lançamentos contábeis (Excel, PDF, CSV)", Telas.ExportarLancamentos),
                ItemMenu.Separador,
                ItemMenu.EmBreve("eSocial"))),
        ItemMenu.Submenu("_Relatórios",
            ItemMenu.Submenu("_Contábeis",
                ItemMenu.Tela("Livro Contábil Completo", Telas.LivrosPdf),
                ItemMenu.Tela("Termos (abertura e encerramento)", Telas.Termos),
                ItemMenu.Separador,
                ItemMenu.Tela("Livro Diário", Telas.Diario),
                ItemMenu.Tela("Livro Razão", Telas.Razao),
                ItemMenu.Tela("Balancete de verificação", Telas.Balancete, "Ctrl+B"),
                ItemMenu.Separador,
                ItemMenu.Tela("Balanço Patrimonial e DRE", Telas.Demonstracoes),
                ItemMenu.Tela("DMPL (mutações do patrimônio líquido)", Telas.Dmpl),
                ItemMenu.Tela("DLPA (lucros ou prejuízos acumulados)", Telas.Dlpa))),
        ItemMenu.Submenu("_Auditoria",
            ItemMenu.Tela("Gerar ECD (SPED Contábil)", Telas.EcdAssistente),
            ItemMenu.Tela("Gerar ECF (SPED)", Telas.EcfAssistente)),
    ];

    public IReadOnlyList<AtalhoTela> Atalhos { get; } =
    [
        new("Ctrl+L", Telas.Lancamento, "Lançamento contábil"),
        new("Ctrl+B", Telas.Balancete, "Balancete de verificação"),
        new("Ctrl+N", null, "Nova linha no lançamento"),
        new("Ctrl+Enter", null, "Efetivar lançamento"),
    ];

    public void RegistrarTelas(CatalogoTelas c)
    {
        static bool ComAcesso(Comum.ViewModels.MainViewModelBase m) => m.TemAcessoNaEmpresa;

        c.Instancia(nameof(MainViewModel.Lancamento), m => new LancamentoViewModel(m), aoTrocarEmpresa: (v, _) => v.AoTrocarEmpresaAsync())
            .Tela(Telas.Lancamento, "Lançamentos › Lançamento contábil",
                  m => m.Pode(Permissao.LancamentoIncluir) || m.Pode(Permissao.LancamentoEstornar), v => v.CarregarAsync())
            // o estorno fica na própria tela de lançamento
            .Tela(Telas.Estorno, "Lançamentos › Lançamento contábil", m => m.Pode(Permissao.LancamentoEstornar), v => v.CarregarAsync());

        c.Instancia(nameof(MainViewModel.Balancete), m => new BalanceteViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoesExportacao(),
                    aoLimpar: v => v.Linhas.Clear())
            .Tela(Telas.Balancete, "Relatórios › Contábeis › Balancete de verificação", m => m.Pode(Permissao.RelatorioBalancete));
        c.Instancia(nameof(MainViewModel.Razao), m => new RazaoViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoesExportacao(),
                    aoLimpar: v => v.Linhas.Clear())
            .Tela(Telas.Razao, "Relatórios › Contábeis › Livro Razão", m => m.Pode(Permissao.RelatorioRazao), v => v.CarregarContasAsync());
        c.Instancia(nameof(MainViewModel.Diario), m => new DiarioViewModel(m),
                    atualizarPermissoes: v => { v.AtualizarPermissoes(); v.AtualizarPermissoesExportacao(); }, aoLimpar: v => v.Linhas.Clear())
            .Tela(Telas.Diario, "Relatórios › Contábeis › Livro Diário", m => m.Pode(Permissao.RelatorioDiario));

        c.Instancia(nameof(MainViewModel.Periodos), m => new PeriodosViewModel(m), aoTrocarEmpresa: (v, _) => v.CarregarAsync())
            .Tela(Telas.Periodos, "Lançamentos › Fechamento de períodos",
                  m => m.Pode(Permissao.PeriodoFechar) || m.Pode(Permissao.PeriodoReabrir));

        c.Instancia(nameof(MainViewModel.PlanoContas), m => new PlanoContasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.PlanoContas, "Cadastros › Plano de contas", ComAcesso, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.CentrosCusto), m => new CentrosCustoViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.CentrosCusto, "Cadastros › Centros de custo", ComAcesso, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Historicos), m => new HistoricosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.Historicos, "Cadastros › Históricos", ComAcesso, v => v.CarregarAsync());

        c.Instancia(nameof(MainViewModel.SaldosIniciais), m => new SaldosIniciaisViewModel(m),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.SaldosIniciais, "Lançamentos › Saldos iniciais (implantação)", m => m.Pode(Permissao.SaldosIniciais), v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Conciliacao), m => new ConciliacaoViewModel(m))
            .Tela(Telas.Conciliacao, "Lançamentos › Conciliação automática", m => m.Pode(Permissao.LancamentoIncluir), v => v.CarregarContasAsync());
        c.Instancia(nameof(MainViewModel.Encerramento), m => new EncerramentoViewModel(m),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.Encerramento, "Lançamentos › Encerramento do exercício", m => m.Pode(Permissao.ExercicioEncerrar), v => v.CarregarAsync());

        c.Instancia(nameof(MainViewModel.Bens), m => new BensViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.Bens, "Imobilizado › Cadastro de bens", ComAcesso, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.GruposDepreciacao), m => new GruposDepreciacaoViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.GruposDepreciacao, "Imobilizado › Regras de depreciação", ComAcesso, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.Depreciacao), m => new DepreciacaoViewModel(m))
            .Tela(Telas.Depreciacao, "Imobilizado › Cálculo e lançamento da depreciação", ComAcesso);

        c.Instancia(nameof(MainViewModel.Consultas), m => new ConsultasViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.ConsultaSaldos, "Consultas", m => m.Pode(Permissao.RelatorioBalancete),
                  v => { v.AtualizarPermissoes(); v.AbaSelecionada = ConsultasViewModel.AbaSaldos; return Task.CompletedTask; })
            .Tela(Telas.ConsultaLancamentos, "Consultas", m => m.Pode(Permissao.RelatorioDiario) || m.Pode(Permissao.RelatorioRazao),
                  v => { v.AtualizarPermissoes(); v.AbaSelecionada = ConsultasViewModel.AbaLancamentos; return Task.CompletedTask; });
        c.Instancia(nameof(MainViewModel.EcdAssistente), m => new EcdAssistenteViewModel(m), aoLimpar: v => v.Limpar(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.EcdAssistente, "Auditoria › Gerar ECD (SPED Contábil)", m => m.Pode(Permissao.SpedEcdGerar), v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.EcfAssistente), m => new EcfAssistenteViewModel(m), aoLimpar: v => v.Limpar(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.EcfAssistente, "Auditoria › Gerar ECF (SPED)", m => m.Pode(Permissao.SpedEcfGerar), v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.DiferencasSaldos), m => new DiferencasSaldosViewModel(m))
            .Tela(Telas.DiferencasSaldos, "Consultas › Diferenças de saldos", m => m.Pode(Permissao.RelatorioBalancete));

        c.Instancia(nameof(MainViewModel.ImportacaoPlano), m => new ImportacaoPlanoViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes())
            .Tela(Telas.ImportarPlano, "Integrações › Importar › Plano de contas", m => m.Pode(Permissao.PlanoContasManter), v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.ImportacaoPlanilhas), m => new ImportacaoPlanilhaViewModel(m))
            .Tela(Telas.ImportarPlanilhas, "Integrações › Importar › Planilhas", m => m.Pode(Permissao.LancamentoIncluir), v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.ImportacaoExtratos), m => new ImportacaoExtratoViewModel(m))
            .Tela(Telas.ImportarExtratos, "Integrações › Importar › Extratos bancários (OFX)", m => m.Pode(Permissao.LancamentoIncluir));
        c.Instancia(nameof(MainViewModel.ImportacaoLancamentos), m => new ImportacaoLancamentosPadraoViewModel(m))
            .Tela(Telas.ImportarLancamentos, "Integrações › Importar › Lançamentos contábeis", m => m.Pode(Permissao.LancamentoIncluir));
        c.Instancia(nameof(MainViewModel.ImportacaoReferencial), m => new ImportacaoReferencialViewModel(m))
            .Tela(Telas.ImportacaoReferencial, "Integrações › Importar › Plano referencial da RFB",
                  m => m.Pode(Permissao.AdministrarSistema), v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.MapeamentoReferencial), m => new MapeamentoReferencialViewModel(m),
                    atualizarPermissoes: v => v.AtualizarPermissoes(), aoTrocarEmpresa: (v, ativa) => ativa ? v.CarregarAsync() : Task.CompletedTask)
            .Tela(Telas.MapeamentoReferencial, "Integrações › Importar › Vínculo ao plano referencial", ComAcesso, v => v.CarregarAsync());
        c.Instancia(nameof(MainViewModel.ExportacaoLancamentos), m => new ExportacaoLancamentosViewModel(m))
            .Tela(Telas.ExportarLancamentos, "Integrações › Exportar › Lançamentos contábeis",
                  m => m.Pode(Permissao.RelatorioDiario) || m.Pode(Permissao.LancamentoIncluir));

        c.Instancia(nameof(MainViewModel.Demonstracoes), m => new DemonstracoesViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoesExportacao())
            .Tela(Telas.Demonstracoes, "Relatórios › Contábeis › Balanço Patrimonial e DRE", m => m.Pode(Permissao.RelatorioDemonstracoes));
        c.Instancia(nameof(MainViewModel.Dmpl), m => new DmplViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoesExportacao())
            .Tela(Telas.Dmpl, "Relatórios › Contábeis › DMPL", m => m.Pode(Permissao.RelatorioDemonstracoes));
        c.Instancia(nameof(MainViewModel.Dlpa), m => new DlpaViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoesExportacao())
            .Tela(Telas.Dlpa, "Relatórios › Contábeis › DLPA", m => m.Pode(Permissao.RelatorioDemonstracoes));
        c.Instancia(nameof(MainViewModel.Termos), m => new TermosViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.PreencherResponsaveisAsync() : Task.CompletedTask)
            .Tela(Telas.Termos, "Relatórios › Contábeis › Termos", m => m.Pode(Permissao.RelatorioDiario), v => v.PreencherResponsaveisAsync());
        c.Instancia(nameof(MainViewModel.LivrosPdf), m => new LivrosPdfViewModel(m), atualizarPermissoes: v => v.AtualizarPermissoes(),
                    aoTrocarEmpresa: (v, ativa) => ativa ? v.PreencherResponsaveisAsync() : Task.CompletedTask)
            .Tela(Telas.LivrosPdf, "Relatórios › Contábeis › Livro Contábil Completo",
                  m => m.Pode(Permissao.RelatorioDiario) || m.Pode(Permissao.RelatorioRazao) || m.Pode(Permissao.RelatorioBalancete)
                       || m.Pode(Permissao.RelatorioDemonstracoes),
                  v => v.PreencherResponsaveisAsync());
    }
}
