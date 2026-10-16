using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Domain.Seguranca;

namespace Perseus.Desktop.ViewModels;

public enum ModoLogin { Entrar, PrimeiroAcesso, TrocaObrigatoria }

/// <summary>Tela de acesso: login, criação do primeiro administrador e troca obrigatória de senha.</summary>
public partial class LoginViewModel(MainViewModel main) : ModuloViewModel(main)
{
    private SessaoUsuario? _pendente;
    private string _senhaDigitada = "";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(EmEntrar), nameof(EmPrimeiroAcesso), nameof(EmTrocaObrigatoria))]
    private ModoLogin _modo = ModoLogin.Entrar;

    [ObservableProperty] private string _usuario = "";
    [ObservableProperty] private string _senha = "";
    [ObservableProperty] private string _nome = "";
    [ObservableProperty] private string _confirmacao = "";

    /// <summary>Salvar login neste computador (usuário e senha; a senha cifrada com DPAPI).</summary>
    [ObservableProperty] private bool _salvarLogin;

    private bool _senhaVeioSalva;

    public bool EmEntrar => Modo == ModoLogin.Entrar;
    public bool EmPrimeiroAcesso => Modo == ModoLogin.PrimeiroAcesso;
    public bool EmTrocaObrigatoria => Modo == ModoLogin.TrocaObrigatoria;

    public async Task PrepararAsync()
    {
        Limpar();
        if (main.App is { } app && await app.Usuarios.PrecisaCriarAdministradorAsync())
        {
            Modo = ModoLogin.PrimeiroAcesso;
            return;
        }
        CarregarLoginSalvo();
    }

    public void CarregarLoginSalvo()
    {
        if (LoginSalvo.Obter() is not { } salvo) return;
        (Usuario, SalvarLogin) = (salvo.Usuario, true);
        if (salvo.Senha is { } senha) (Senha, _senhaVeioSalva) = (senha, true);
    }

    public void Limpar()
    {
        (Senha, Confirmacao, Nome) = ("", "", "");
        _pendente = null;
        _senhaDigitada = "";
        _senhaVeioSalva = false;
        Erros.Clear();
        Mensagem = null;
        Modo = ModoLogin.Entrar;
    }

    [RelayCommand]
    private async Task EntrarAsync()
    {
        if (!Preparar(out var app)) return;
        var r = await app.Usuarios.AutenticarAsync(Usuario, Senha);
        if (!r.Sucesso)
        {
            MostrarResultado(r, "");
            // senha salva que deixou de valer (trocada ou redefinida): deixa de preenchê-la
            if (_senhaVeioSalva && SalvarLogin) LoginSalvo.Salvar(Usuario.Trim(), "");
            (Senha, _senhaVeioSalva) = ("", false);
            return;
        }

        if (r.Valor.DeveTrocarSenha)
        {
            (_pendente, _senhaDigitada) = (r.Valor, Senha);
            (Senha, Confirmacao) = ("", "");
            Modo = ModoLogin.TrocaObrigatoria;
            Mensagem = "Por segurança, defina uma nova senha para continuar.";
            return;
        }
        await Concluir(r.Valor);
    }

    [RelayCommand]
    private async Task CriarAdministradorAsync()
    {
        if (!Preparar(out var app)) return;
        var r = await app.Usuarios.CriarPrimeiroAdministradorAsync(Usuario, Nome, Senha, Confirmacao);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await Concluir(r.Valor);
    }

    [RelayCommand]
    private async Task TrocarSenhaAsync()
    {
        if (!Preparar(out var app) || _pendente is null) return;
        var r = await app.Usuarios.AlterarPropriaSenhaAsync(_pendente, _senhaDigitada, Senha, Confirmacao);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await Concluir(_pendente with { DeveTrocarSenha = false });
    }

    private bool Preparar(out Infra.PerseusApp app)
    {
        Erros.Clear();
        Mensagem = null;
        app = main.App!;
        if (main.App is null) { Erros.Add("Sem conexão com o banco de dados."); return false; }
        return true;
    }

    private async Task Concluir(SessaoUsuario sessao)
    {
        var login = Usuario;
        if (SalvarLogin) LoginSalvo.Salvar(login.Trim(), Senha);
        else LoginSalvo.Esquecer();
        var salvar = SalvarLogin;
        Limpar();
        (Usuario, SalvarLogin) = (login, salvar);
        await main.EntrarAsync(sessao);
    }
}
