using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Perseus.Application.Cadastros;
using Perseus.Infra;

namespace Perseus.Desktop.ViewModels;

/// <summary>Contexto compartilhado pelos módulos da tela: aplicação conectada e empresa ativa.</summary>
public interface IContexto
{
    PerseusApp? App { get; }
    long? EmpresaId { get; }
    Task RecarregarEmpresasAsync(long? selecionar = null);
}

public partial class MainViewModel : ObservableObject, IContexto, IAsyncDisposable
{
    public MainViewModel()
    {
        Cadastro = new CadastroEmpresaViewModel(this);
        Lancamento = new LancamentoViewModel(this);
        Balancete = new BalanceteViewModel(this);
        Razao = new RazaoViewModel(this);
        Diario = new DiarioViewModel(this);
        Periodos = new PeriodosViewModel(this);
    }

    public PerseusApp? App { get; private set; }
    public long? EmpresaId => EmpresaSelecionada?.Id;

    public ObservableCollection<EmpresaResumo> Empresas { get; } = [];

    [ObservableProperty] private EmpresaResumo? _empresaSelecionada;
    [ObservableProperty] private string _status = "Conectando ao banco de dados...";
    [ObservableProperty] private bool _conectado;
    [ObservableProperty] private bool _temEmpresa;

    public CadastroEmpresaViewModel Cadastro { get; }
    public LancamentoViewModel Lancamento { get; }
    public BalanceteViewModel Balancete { get; }
    public RazaoViewModel Razao { get; }
    public DiarioViewModel Diario { get; }
    public PeriodosViewModel Periodos { get; }

    public async Task InicializarAsync(string connectionString)
    {
        try
        {
            await BancoLocal.GarantirBancoAsync(connectionString);
            App = await PerseusApp.IniciarAsync(connectionString);
            Conectado = true;
            await RecarregarEmpresasAsync();
        }
        catch (Exception ex)
        {
            Status = $"Falha ao conectar: {ex.Message}. Configure a conexão em {Configuracao.ArquivoConfiguracao} ou na variável PERSEUS_DB.";
        }
    }

    public async Task RecarregarEmpresasAsync(long? selecionar = null)
    {
        if (App is null) return;
        var atual = selecionar ?? EmpresaSelecionada?.Id;
        Empresas.Clear();
        foreach (var e in await App.RepositorioEmpresas.ListarAsync(CancellationToken.None)) Empresas.Add(e);
        EmpresaSelecionada = Empresas.FirstOrDefault(e => e.Id == atual) ?? Empresas.FirstOrDefault();
        Status = Empresas.Count == 0 ? "Conectado. Cadastre a primeira empresa na aba \"Nova empresa\"." : $"Conectado. {Empresas.Count} empresa(s).";
    }

    partial void OnEmpresaSelecionadaChanged(EmpresaResumo? value)
    {
        TemEmpresa = value is not null;
        _ = Disparar(Lancamento.AoTrocarEmpresaAsync);
        _ = Disparar(Periodos.CarregarAsync);
        Balancete.Linhas.Clear();
        Razao.Linhas.Clear();
        Diario.Linhas.Clear();
    }

    /// <summary>Tarefa disparada pela troca de empresa: erros vão para a barra de status em vez de sumirem.</summary>
    private async Task Disparar(Func<Task> acao)
    {
        try
        {
            await acao();
        }
        catch (Exception ex)
        {
            Status = $"Erro ao carregar dados da empresa: {ex.Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (App is not null) await App.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
