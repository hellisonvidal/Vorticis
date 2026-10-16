using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Application.Cadastros;
using Perseus.Application.Seguranca;
using Perseus.Domain.Seguranca;
using Perseus.Infra;

namespace Perseus.Desktop.ViewModels;

/// <summary>Contexto compartilhado pelos módulos: aplicação conectada, usuário logado e empresa ativa.</summary>
public interface IContexto
{
    PerseusApp? App { get; }
    SessaoUsuario? Sessao { get; }
    long? EmpresaId { get; }
    bool Pode(Permissao permissao);
    Task RecarregarEmpresasAsync(long? selecionar = null);
}

/// <summary>Telas acessíveis pelo menu. A chave é o CommandParameter do item de menu.</summary>
public static class Telas
{
    public const string Empresas = "empresas";
    public const string Lancamento = "lancamento";
    public const string Estorno = "estorno";
    public const string Periodos = "periodos";
    public const string Balancete = "balancete";
    public const string Razao = "razao";
    public const string Diario = "diario";
    public const string Usuarios = "usuarios";
    public const string AlterarSenha = "senha";
    public const string Conexao = "conexao";
    public const string Sobre = "sobre";
}

public partial class MainViewModel : ObservableObject, IContexto, IAsyncDisposable
{
    private string _connectionString = "";
    private IConsultaCnpj? _consultaCnpj;

    public MainViewModel()
    {
        Login = new LoginViewModel(this);
        Conexao = new ConexaoViewModel(this);
        RecriarModulos();
    }

    /// <summary>
    /// Instâncias novas de todas as telas do sistema. Chamado a cada logout, para que nada digitado ou
    /// consultado por um usuário (relatórios, formulários, listas) fique visível para o próximo.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(Empresas), nameof(Lancamento), nameof(Balancete), nameof(Razao),
        nameof(Diario), nameof(Periodos), nameof(Usuarios), nameof(AlterarSenha), nameof(Sobre))]
    private void RecriarModulos()
    {
        Empresas = new CadastroEmpresaViewModel(this);
        Lancamento = new LancamentoViewModel(this);
        Balancete = new BalanceteViewModel(this);
        Razao = new RazaoViewModel(this);
        Diario = new DiarioViewModel(this);
        Periodos = new PeriodosViewModel(this);
        Usuarios = new UsuariosViewModel(this);
        AlterarSenha = new AlterarSenhaViewModel(this);
        Sobre = new SobreViewModel(this);
    }

    public PerseusApp? App { get; private set; }
    public long? EmpresaId => EmpresaSelecionada?.Id;
    public string ConnectionString => _connectionString;

    [ObservableProperty] private SessaoUsuario? _sessao;
    [ObservableProperty] private EmpresaPermitida? _empresaSelecionada;
    [ObservableProperty] private string _status = "Conectando ao banco de dados...";
    [ObservableProperty] private bool _logado;
    [ObservableProperty] private object? _conteudo;
    [ObservableProperty] private string _tituloTela = "";

    public ObservableCollection<EmpresaPermitida> EmpresasPermitidas { get; } = [];

    public LoginViewModel Login { get; }
    public ConexaoViewModel Conexao { get; }
    public CadastroEmpresaViewModel Empresas { get; private set; }
    public LancamentoViewModel Lancamento { get; private set; }
    public BalanceteViewModel Balancete { get; private set; }
    public RazaoViewModel Razao { get; private set; }
    public DiarioViewModel Diario { get; private set; }
    public PeriodosViewModel Periodos { get; private set; }
    public UsuariosViewModel Usuarios { get; private set; }
    public AlterarSenhaViewModel AlterarSenha { get; private set; }
    public SobreViewModel Sobre { get; private set; }

    public string UsuarioRotulo => Sessao is null ? "" : $"{Sessao.Nome}{(Sessao.Administrador ? " (administrador)" : "")}";

    // ------------------------------------------------------------------ ciclo de vida

    public async Task InicializarAsync(string connectionString, IConsultaCnpj? consultaCnpj = null)
    {
        _connectionString = connectionString;
        _consultaCnpj = consultaCnpj ?? _consultaCnpj;
        if (App is not null) { await App.DisposeAsync(); App = null; }
        try
        {
            await BancoLocal.GarantirBancoAsync(connectionString);
            App = await PerseusApp.IniciarAsync(connectionString, _consultaCnpj);
            Status = $"Conectado a {Configuracao.Descrever(connectionString)}.";
            await Login.PrepararAsync();
            Conteudo = Login;
        }
        catch (Exception ex)
        {
            Status = $"Sem conexão com o banco de dados: {ex.Message}";
            Conexao.CarregarDe(connectionString, ex.Message);
            Conteudo = Conexao;
        }
    }

    /// <summary>Chamado pelo login (ou pela troca obrigatória de senha) quando a sessão está pronta para uso.</summary>
    public async Task EntrarAsync(SessaoUsuario sessao)
    {
        Sessao = sessao;
        Logado = true;
        await RecarregarEmpresasAsync();
        if (EmpresasPermitidas.Count == 0 && sessao.Administrador) Abrir(Telas.Empresas);
        else if (Pode(Permissao.Lancar)) Abrir(Telas.Lancamento);
        else if (Pode(Permissao.ConsultarRelatorios)) Abrir(Telas.Balancete);
        else Abrir(Telas.Sobre);
    }

    [RelayCommand]
    private void Sair()
    {
        Sessao = null;
        Logado = false;
        EmpresasPermitidas.Clear();
        EmpresaSelecionada = null;
        TituloTela = "";
        RecriarModulos();
        Login.Limpar();
        Conteudo = Login;
        Status = $"Sessão encerrada. Conectado a {Configuracao.Descrever(_connectionString)}.";
    }

    public async Task RecarregarEmpresasAsync(long? selecionar = null)
    {
        if (App is null || Sessao is null) return;
        var atual = selecionar ?? EmpresaSelecionada?.Id;
        var lista = await App.Usuarios.EmpresasPermitidasAsync(Sessao);
        EmpresasPermitidas.Clear();
        foreach (var e in lista) EmpresasPermitidas.Add(e);
        EmpresaSelecionada = EmpresasPermitidas.FirstOrDefault(e => e.Id == atual) ?? EmpresasPermitidas.FirstOrDefault();
        AtualizarStatus();
    }

    // ------------------------------------------------------------------ permissões e navegação

    public bool Pode(Permissao permissao) =>
        Sessao is not null && (permissao == Permissao.AdministrarSistema
            ? Sessao.Administrador
            : EmpresaSelecionada is not null && Permissoes.Pode(Sessao, EmpresaSelecionada.Perfil, permissao));

    public bool PodeAbrir(string? tela) => Logado && tela switch
    {
        Telas.Empresas or Telas.Usuarios or Telas.Conexao => Pode(Permissao.AdministrarSistema),
        Telas.Lancamento or Telas.Estorno => Pode(Permissao.Lancar),
        Telas.Periodos or Telas.Balancete or Telas.Razao or Telas.Diario => Pode(Permissao.ConsultarRelatorios),
        Telas.AlterarSenha or Telas.Sobre => true,
        _ => false,
    };

    [RelayCommand(CanExecute = nameof(PodeAbrir))]
    private void Abrir(string? tela)
    {
        if (!PodeAbrir(tela)) return;
        (Conteudo, TituloTela) = tela switch
        {
            Telas.Empresas => ((object)Empresas, "Cadastros › Empresas"),
            Telas.Lancamento => (Lancamento, "Lançamentos › Lançamento contábil"),
            Telas.Estorno => (Diario, "Lançamentos › Estornar lançamento"),
            Telas.Periodos => (Periodos, "Lançamentos › Fechamento de períodos"),
            Telas.Balancete => (Balancete, "Relatórios › Balancete de verificação"),
            Telas.Razao => (Razao, "Relatórios › Livro Razão"),
            Telas.Diario => (Diario, "Relatórios › Livro Diário"),
            Telas.Usuarios => (Usuarios, "Configurações › Usuários e permissões"),
            Telas.AlterarSenha => (AlterarSenha, "Configurações › Alterar minha senha"),
            Telas.Conexao => (Conexao, "Configurações › Conexão com o banco de dados"),
            Telas.Sobre => (Sobre, "Ajuda › Sobre o Perseus"),
            _ => (Conteudo, TituloTela),
        };
        if (tela == Telas.Usuarios) _ = Disparar(Usuarios.CarregarAsync);
        if (tela == Telas.Conexao) Conexao.CarregarDe(_connectionString, null);
        if (tela == Telas.Sobre) Sobre.Atualizar();
    }

    partial void OnLogadoChanged(bool value) => AbrirCommand.NotifyCanExecuteChanged();

    partial void OnSessaoChanged(SessaoUsuario? value) => OnPropertyChanged(nameof(UsuarioRotulo));

    partial void OnEmpresaSelecionadaChanged(EmpresaPermitida? value)
    {
        AbrirCommand.NotifyCanExecuteChanged();
        AtualizarStatus();
        _ = Disparar(Lancamento.AoTrocarEmpresaAsync);
        _ = Disparar(Periodos.CarregarAsync);
        Balancete.Linhas.Clear();
        Razao.Linhas.Clear();
        Diario.Linhas.Clear();
        // Se a tela aberta deixou de ser permitida para a nova empresa, volta à tela neutra.
        if (Logado && ReferenceEquals(Conteudo, Lancamento) && !Pode(Permissao.Lancar)) Abrir(Telas.Sobre);
    }

    private void AtualizarStatus()
    {
        if (Sessao is null) return;
        var empresa = EmpresaSelecionada is null
            ? (EmpresasPermitidas.Count == 0 ? "nenhuma empresa liberada" : "nenhuma empresa selecionada")
            : $"perfil {(Sessao.Administrador ? "administrador" : EmpresaSelecionada.Perfil?.ToString().ToLowerInvariant())}";
        Status = $"Usuário: {Sessao.Login} | {empresa} | {Configuracao.Descrever(_connectionString)}";
    }

    /// <summary>Tarefa disparada em segundo plano: erros vão para a barra de status em vez de sumirem.</summary>
    private async Task Disparar(Func<Task> acao)
    {
        try
        {
            await acao();
        }
        catch (Exception ex)
        {
            Status = $"Erro ao carregar dados: {ex.Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (App is not null) await App.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
