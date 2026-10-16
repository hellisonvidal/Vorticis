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
    void AoConcluirConfiguracaoInicial();
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
    public const string PlanoContas = "planocontas";
    public const string CentrosCusto = "centroscusto";
    public const string SaldosIniciais = "saldosiniciais";
    public const string Usuarios = "usuarios";
    public const string Perfis = "perfis";
    public const string Backup = "backup";
    public const string Encerramento = "encerramento";
    public const string Demonstracoes = "demonstracoes";
    public const string LivrosPdf = "livrospdf";
    public const string ImportacaoReferencial = "importacaoreferencial";
    public const string MapeamentoReferencial = "mapeamentoreferencial";
    public const string Contabilidade = "contabilidade";
    public const string Socios = "socios";
    public const string AlterarSenha = "senha";
    public const string Conexao = "conexao";
    public const string Sobre = "sobre";
    public const string Inicio = "inicio";
}

public partial class MainViewModel : ObservableObject, IContexto, IAsyncDisposable
{
    private string _connectionString = "";
    private string? _telaAtual;
    private PermissoesEfetivas _permissoes = PermissoesEfetivas.Nenhuma;
    private IConsultaCnpj? _consultaCnpj;
    private bool _configuracaoInicialPendente;

    public MainViewModel()
    {
        Login = new LoginViewModel(this);
        Conexao = new ConexaoViewModel(this);
        Inicio = new InicioViewModel(this);
        RecriarModulos();
    }

    /// <summary>
    /// Instâncias novas de todas as telas do sistema. Chamado a cada logout, para que nada digitado ou
    /// consultado por um usuário (relatórios, formulários, listas) fique visível para o próximo.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(Empresas), nameof(Lancamento), nameof(Balancete), nameof(Razao),
        nameof(Diario), nameof(Periodos), nameof(Usuarios), nameof(AlterarSenha), nameof(Sobre), nameof(Perfis), nameof(PlanoContas),
        nameof(CentrosCusto), nameof(SaldosIniciais), nameof(Backup), nameof(Encerramento), nameof(Demonstracoes), nameof(LivrosPdf),
        nameof(ImportacaoReferencial), nameof(MapeamentoReferencial), nameof(Contabilidade), nameof(Socios))]
    private void RecriarModulos()
    {
        Empresas = new CadastroEmpresaViewModel(this);
        Lancamento = new LancamentoViewModel(this);
        Balancete = new BalanceteViewModel(this);
        Razao = new RazaoViewModel(this);
        Diario = new DiarioViewModel(this);
        Periodos = new PeriodosViewModel(this);
        Usuarios = new UsuariosViewModel(this);
        Perfis = new PerfisViewModel(this);
        PlanoContas = new PlanoContasViewModel(this);
        CentrosCusto = new CentrosCustoViewModel(this);
        SaldosIniciais = new SaldosIniciaisViewModel(this);
        Backup = new BackupViewModel(this);
        Encerramento = new EncerramentoViewModel(this);
        Demonstracoes = new DemonstracoesViewModel(this);
        LivrosPdf = new LivrosPdfViewModel(this);
        ImportacaoReferencial = new ImportacaoReferencialViewModel(this);
        MapeamentoReferencial = new MapeamentoReferencialViewModel(this);
        Contabilidade = new ContabilidadeEmpresaViewModel(this);
        Socios = new SociosViewModel(this);
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
    public InicioViewModel Inicio { get; }
    public CadastroEmpresaViewModel Empresas { get; private set; }
    public LancamentoViewModel Lancamento { get; private set; }
    public BalanceteViewModel Balancete { get; private set; }
    public RazaoViewModel Razao { get; private set; }
    public DiarioViewModel Diario { get; private set; }
    public PeriodosViewModel Periodos { get; private set; }
    public UsuariosViewModel Usuarios { get; private set; }
    public PerfisViewModel Perfis { get; private set; }
    public PlanoContasViewModel PlanoContas { get; private set; }
    public CentrosCustoViewModel CentrosCusto { get; private set; }
    public SaldosIniciaisViewModel SaldosIniciais { get; private set; }
    public BackupViewModel Backup { get; private set; }
    public EncerramentoViewModel Encerramento { get; private set; }
    public DemonstracoesViewModel Demonstracoes { get; private set; }
    public LivrosPdfViewModel LivrosPdf { get; private set; }
    public ImportacaoReferencialViewModel ImportacaoReferencial { get; private set; }
    public MapeamentoReferencialViewModel MapeamentoReferencial { get; private set; }
    public ContabilidadeEmpresaViewModel Contabilidade { get; private set; }
    public SociosViewModel Socios { get; private set; }
    public AlterarSenhaViewModel AlterarSenha { get; private set; }
    public SobreViewModel Sobre { get; private set; }

    public string UsuarioRotulo => Sessao is null ? "" : $"{Sessao.Nome}{(Sessao.Administrador ? " (administrador)" : "")}";

    // ------------------------------------------------------------------ ciclo de vida

    /// <summary>Abertura do programa: prepara o banco (no modo local, cria e inicia o PostgreSQL embutido) e conecta.</summary>
    public async Task IniciarAsync()
    {
        string cs;
        try
        {
            Status = "Preparando o banco de dados...";
            cs = await BancoDoAplicativo.PrepararAsync(s => Status = s);
        }
        catch (Exception ex)
        {
            Status = $"Sem conexão com o banco de dados: {ex.Message}";
            Conexao.CarregarDe(Configuracao.Padrao, ex.Message);
            Conteudo = Conexao;
            return;
        }
        await InicializarAsync(cs);
    }

    public async Task InicializarAsync(string connectionString, IConsultaCnpj? consultaCnpj = null)
    {
        _connectionString = connectionString;
        _consultaCnpj = consultaCnpj ?? _consultaCnpj;
        if (Logado) Sair();
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
            Conexao.CarregarDe(BancoDoAplicativo.EhBancoLocal(connectionString) ? Configuracao.Padrao : connectionString, ex.Message);
            Conteudo = Conexao;
        }
    }

    /// <summary>Chamado pelo login (ou pela troca obrigatória de senha) quando a sessão está pronta para uso.</summary>
    public async Task EntrarAsync(SessaoUsuario sessao)
    {
        Sessao = sessao;
        Logado = true;
        _configuracaoInicialPendente = sessao.Administrador && App is not null && await App.Perfis.ConfiguracaoInicialPendenteAsync();
        await RecarregarEmpresasAsync();
        if (_configuracaoInicialPendente)
        {
            // Primeiro uso: definir os perfis de acesso é obrigatório; o cadastro de empresas pode ficar para depois.
            Perfis.ConfiguracaoInicial = true;
            AbrirCommand.NotifyCanExecuteChanged();
            Abrir(Telas.Perfis);
            Status = "Configuração inicial: defina os perfis de acesso dos usuários para liberar o sistema.";
        }
        else Abrir(Telas.Inicio);
    }

    public void AoConcluirConfiguracaoInicial()
    {
        _configuracaoInicialPendente = false;
        AbrirCommand.NotifyCanExecuteChanged();
        Abrir(Telas.Inicio);
        AtualizarStatus();
    }

    [RelayCommand]
    private void Sair()
    {
        Sessao = null;
        Logado = false;
        _configuracaoInicialPendente = false;
        EmpresasPermitidas.Clear();
        EmpresaSelecionada = null;
        _permissoes = PermissoesEfetivas.Nenhuma;
        _telaAtual = null;
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
        await AtualizarPermissoesAsync();
    }

    /// <summary>Lê do banco o que o usuário pode fazer na empresa ativa (perfil atribuído pelo administrador).</summary>
    public async Task AtualizarPermissoesAsync()
    {
        _permissoes = App is not null && Sessao is not null && EmpresaSelecionada is not null
            ? await App.Autorizador.ObterAsync(Sessao.Id, EmpresaSelecionada.Id)
            : PermissoesEfetivas.Nenhuma;
        AbrirCommand.NotifyCanExecuteChanged();
        Diario.AtualizarPermissoes();
        PlanoContas.AtualizarPermissoes();
        CentrosCusto.AtualizarPermissoes();
        LivrosPdf.AtualizarPermissoes();
        MapeamentoReferencial.AtualizarPermissoes();
        Contabilidade.AtualizarPermissoes();
        Socios.AtualizarPermissoes();
        AtualizarStatus();
        // Se a tela aberta deixou de ser permitida (troca de empresa/perfil), vai para a primeira permitida.
        if (Logado && _telaAtual is not null && !PodeAbrir(_telaAtual)) AbrirPrimeiraPermitida();
    }

    private void AbrirPrimeiraPermitida() => Abrir(PodeAbrir(Telas.Inicio) ? Telas.Inicio : Telas.Perfis);

    // ------------------------------------------------------------------ permissões e navegação

    public bool Pode(Permissao permissao) =>
        Sessao is not null && (permissao == Permissao.AdministrarSistema
            ? Sessao.Administrador
            : EmpresaSelecionada is not null && _permissoes.Pode(permissao));

    public bool PodeAbrir(string? tela) => Logado && (!_configuracaoInicialPendente
        ? PodeAbrirTela(tela)
        : tela is Telas.Perfis or Telas.Sobre or Telas.AlterarSenha);

    private bool PodeAbrirTela(string? tela) => tela switch
    {
        Telas.Empresas or Telas.Usuarios or Telas.Perfis or Telas.Conexao or Telas.Backup or Telas.ImportacaoReferencial
            => Pode(Permissao.AdministrarSistema),
        Telas.PlanoContas or Telas.CentrosCusto or Telas.MapeamentoReferencial or Telas.Contabilidade or Telas.Socios
            => EmpresaSelecionada is not null && _permissoes.TemAcesso,
        Telas.Lancamento => Pode(Permissao.LancamentoIncluir),
        Telas.Estorno => Pode(Permissao.LancamentoEstornar),
        Telas.SaldosIniciais => Pode(Permissao.SaldosIniciais),
        Telas.Periodos => Pode(Permissao.PeriodoFechar) || Pode(Permissao.PeriodoReabrir),
        Telas.Balancete => Pode(Permissao.RelatorioBalancete),
        Telas.Razao => Pode(Permissao.RelatorioRazao),
        Telas.Diario => Pode(Permissao.RelatorioDiario),
        Telas.Encerramento => Pode(Permissao.ExercicioEncerrar),
        Telas.Demonstracoes => Pode(Permissao.RelatorioDemonstracoes),
        Telas.LivrosPdf => Pode(Permissao.RelatorioDiario) || Pode(Permissao.RelatorioRazao) || Pode(Permissao.RelatorioBalancete)
                           || Pode(Permissao.RelatorioDemonstracoes),
        Telas.AlterarSenha or Telas.Sobre or Telas.Inicio => true,
        _ => false,
    };

    [RelayCommand(CanExecute = nameof(PodeAbrir))]
    private void Abrir(string? tela)
    {
        if (!PodeAbrir(tela)) return;
        (Conteudo, TituloTela) = tela switch
        {
            Telas.Empresas => ((object)Empresas, "Cadastros › Empresas"),
            Telas.PlanoContas => (PlanoContas, "Cadastros › Plano de contas"),
            Telas.CentrosCusto => (CentrosCusto, "Cadastros › Centros de custo"),
            Telas.SaldosIniciais => (SaldosIniciais, "Lançamentos › Saldos iniciais (implantação)"),
            Telas.Perfis => (Perfis, "Configurações › Perfis de acesso"),
            Telas.Backup => (Backup, "Configurações › Backup e restauração"),
            Telas.Encerramento => (Encerramento, "Lançamentos › Encerramento do exercício"),
            Telas.Demonstracoes => (Demonstracoes, "Relatórios › Balanço Patrimonial e DRE"),
            Telas.LivrosPdf => (LivrosPdf, "Relatórios › Livros e relatórios em PDF"),
            Telas.ImportacaoReferencial => (ImportacaoReferencial, "Configurações › Plano referencial da RFB"),
            Telas.MapeamentoReferencial => (MapeamentoReferencial, "Cadastros › Vínculo ao plano referencial"),
            Telas.Contabilidade => (Contabilidade, "Cadastros › Contadores e escritório contábil"),
            Telas.Socios => (Socios, "Cadastros › Sócios"),
            Telas.Lancamento => (Lancamento, "Lançamentos › Lançamento contábil"),
            Telas.Estorno => (Diario, "Lançamentos › Estornar lançamento"),
            Telas.Periodos => (Periodos, "Lançamentos › Fechamento de períodos"),
            Telas.Balancete => (Balancete, "Relatórios › Balancete de verificação"),
            Telas.Razao => (Razao, "Relatórios › Livro Razão"),
            Telas.Diario => (Diario, "Relatórios › Livro Diário"),
            Telas.Usuarios => (Usuarios, "Configurações › Usuários"),
            Telas.AlterarSenha => (AlterarSenha, "Configurações › Alterar minha senha"),
            Telas.Conexao => (Conexao, "Configurações › Conexão com o banco de dados"),
            Telas.Sobre => (Sobre, "Ajuda › Sobre o Perseus Contábil"),
            Telas.Inicio => (Inicio, ""),
            _ => (Conteudo, TituloTela),
        };
        _telaAtual = tela;
        if (tela == Telas.Usuarios) _ = Disparar(Usuarios.CarregarAsync);
        if (tela == Telas.Perfis) _ = Disparar(Perfis.CarregarAsync);
        if (tela == Telas.PlanoContas) _ = Disparar(PlanoContas.CarregarAsync);
        if (tela == Telas.CentrosCusto) _ = Disparar(CentrosCusto.CarregarAsync);
        if (tela == Telas.SaldosIniciais) _ = Disparar(SaldosIniciais.CarregarAsync);
        if (tela == Telas.Backup) Backup.Atualizar();
        if (tela == Telas.ImportacaoReferencial) _ = Disparar(ImportacaoReferencial.CarregarAsync);
        if (tela == Telas.MapeamentoReferencial) _ = Disparar(MapeamentoReferencial.CarregarAsync);
        if (tela == Telas.Contabilidade) _ = Disparar(Contabilidade.CarregarAsync);
        if (tela == Telas.Socios) _ = Disparar(Socios.CarregarAsync);
        if (tela == Telas.LivrosPdf) _ = Disparar(LivrosPdf.PreencherResponsaveisAsync);
        if (tela == Telas.Conexao)
            Conexao.CarregarDe(BancoDoAplicativo.EhBancoLocal(_connectionString) ? Configuracao.Padrao : _connectionString, null);
        if (tela == Telas.Sobre) Sobre.Atualizar();
        if (tela == Telas.Inicio) Inicio.Atualizar();
    }

    partial void OnLogadoChanged(bool value) => AbrirCommand.NotifyCanExecuteChanged();

    partial void OnSessaoChanged(SessaoUsuario? value) => OnPropertyChanged(nameof(UsuarioRotulo));

    partial void OnEmpresaSelecionadaChanged(EmpresaPermitida? value)
    {
        Balancete.Linhas.Clear();
        Razao.Linhas.Clear();
        Diario.Linhas.Clear();
        _ = Disparar(async () =>
        {
            await AtualizarPermissoesAsync();
            await Lancamento.AoTrocarEmpresaAsync();
            await Periodos.CarregarAsync();
            if (_telaAtual == Telas.PlanoContas) await PlanoContas.CarregarAsync();
            if (_telaAtual == Telas.CentrosCusto) await CentrosCusto.CarregarAsync();
            if (_telaAtual == Telas.SaldosIniciais) await SaldosIniciais.CarregarAsync();
            if (_telaAtual == Telas.MapeamentoReferencial) await MapeamentoReferencial.CarregarAsync();
            if (_telaAtual == Telas.Contabilidade) await Contabilidade.CarregarAsync();
            if (_telaAtual == Telas.Socios) await Socios.CarregarAsync();
            if (_telaAtual == Telas.LivrosPdf) await LivrosPdf.PreencherResponsaveisAsync();
        });
    }

    private void AtualizarStatus()
    {
        if (Sessao is null) return;
        var empresa = EmpresaSelecionada is null
            ? (EmpresasPermitidas.Count == 0 ? "nenhuma empresa liberada" : "nenhuma empresa selecionada")
            : Sessao.Administrador ? "administrador" : $"perfil {_permissoes.NomePerfil ?? "sem acesso"}";
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
