using Vorticis.Folha.Desktop.ViewModels;

namespace Vorticis.Folha.Desktop;

/// <summary>Janela principal do módulo Folha: o casco com o <see cref="ModuloFolha"/> e o acesso tipado às telas do módulo.</summary>
public sealed class MainViewModel() : MainViewModelBase(new ModuloFolha())
{
    public FuncionariosViewModel Funcionarios => Instancia<FuncionariosViewModel>();
    public CargosViewModel Cargos => Instancia<CargosViewModel>();
    public DepartamentosViewModel Departamentos => Instancia<DepartamentosViewModel>();
    public RubricasViewModel Rubricas => Instancia<RubricasViewModel>();
    public ParametrosFolhaViewModel Parametros => Instancia<ParametrosFolhaViewModel>();
    public EventosVariaveisViewModel Eventos => Instancia<EventosVariaveisViewModel>();
    public FolhaMensalViewModel FolhaMensal => Instancia<FolhaMensalViewModel>();
    public FeriasViewModel Ferias => Instancia<FeriasViewModel>();
    public DecimoTerceiroViewModel Decimo => Instancia<DecimoTerceiroViewModel>();
    public RescisoesViewModel Rescisoes => Instancia<RescisoesViewModel>();
    public ContabilizacaoFolhaViewModel Contabilizacao => Instancia<ContabilizacaoFolhaViewModel>();
    public ESocialViewModel ESocial => Instancia<ESocialViewModel>();
    public ResumoFolhaViewModel Resumo => Instancia<ResumoFolhaViewModel>();
    public EncargosFolhaViewModel Encargos => Instancia<EncargosFolhaViewModel>();
    public ProvisoesFolhaViewModel Provisoes => Instancia<ProvisoesFolhaViewModel>();
}
