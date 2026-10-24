using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Desktop.Comum.ViewModels;

public enum ModoLogin { Entrar, TrocaObrigatoria }

/// <summary>
/// Tela de acesso: login e troca obrigatória de senha. Em instalação nova (banco sem usuários) só entra o usuário de
/// implantação (vorticis / vorticis), que é levado ao cadastro de usuários para criar o primeiro administrador.
/// </summary>
public partial class LoginViewModel(MainViewModelBase main) : ModuloViewModel(main)
{
    private SessaoUsuario? _pendente;
    private string _senhaDigitada = "";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(EmEntrar), nameof(EmTrocaObrigatoria))]
    private ModoLogin _modo = ModoLogin.Entrar;

    [ObservableProperty] private string _usuario = "";
    [ObservableProperty] private string _senha = "";
    [ObservableProperty] private string _confirmacao = "";

    /// <summary>Salvar login neste computador (usuário e senha; a senha cifrada com DPAPI).</summary>
    [ObservableProperty] private bool _salvarLogin;

    private bool _senhaVeioSalva;

    /// <summary>Nome e descrição do módulo, mostrados no topo da tela de acesso.</summary>
    public string NomeModulo => main.Modulo.Nome;
    public string SubtituloModulo => main.Modulo.Subtitulo;

    /// <summary>Cor de destaque do módulo (nome no topo da tela de acesso).</summary>
    public Avalonia.Media.IBrush PincelDestaque => main.PincelDestaque;

    /// <summary>Instalação nova (banco sem usuários): a tela explica o acesso de implantação.</summary>
    [ObservableProperty] private bool _implantacaoAtiva;

    public string AvisoImplantacao =>
        $"Instalação nova, sem usuários cadastrados. Entre com o usuário \"{ServicoUsuarios.LoginImplantacao}\" e a senha " +
        $"\"{ServicoUsuarios.SenhaImplantacao}\" para cadastrar o administrador do sistema. Esse acesso só funciona até o " +
        "primeiro administrador ser criado.";

    public bool EmEntrar => Modo == ModoLogin.Entrar;
    public bool EmTrocaObrigatoria => Modo == ModoLogin.TrocaObrigatoria;

    public async Task PrepararAsync()
    {
        Limpar();
        await AtualizarImplantacaoAsync();
        if (!ImplantacaoAtiva) CarregarLoginSalvo();
    }

    /// <summary>Confere no banco se a implantação está ativa (nenhum usuário cadastrado) para o aviso da tela.</summary>
    public async Task AtualizarImplantacaoAsync()
        => ImplantacaoAtiva = main.App is { } app && await app.Usuarios.ImplantacaoAtivaAsync();

    public void CarregarLoginSalvo()
    {
        if (LoginSalvo.Obter() is not { } salvo) return;
        (Usuario, SalvarLogin) = (salvo.Usuario, true);
        if (salvo.Senha is { } senha) (Senha, _senhaVeioSalva) = (senha, true);
    }

    public void Limpar()
    {
        (Senha, Confirmacao) = ("", "");
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

        if (!ConferirLicenca()) return;

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
    private async Task TrocarSenhaAsync()
    {
        if (!Preparar(out var app) || _pendente is null) return;
        var r = await app.Usuarios.AlterarPropriaSenhaAsync(_pendente, _senhaDigitada, Senha, Confirmacao);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await Concluir(_pendente with { DeveTrocarSenha = false });
    }

    /// <summary>Licença recusada: mostra o botão "Instalar arquivo de licença".</summary>
    [ObservableProperty] private bool _precisaLicenca;

    /// <summary>Confere a licença do módulo antes de entrar; avisa quando faltam 30 dias ou menos para vencer.</summary>
    private bool ConferirLicenca()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var l = LicencaInstalada.ConferirModulo(main.Modulo.NomeExecutavel, hoje);
        PrecisaLicenca = !l.Sucesso;
        if (!l.Sucesso)
        {
            foreach (var e in l.Erros) Erros.Add(e);
            return false;
        }
        main.AvisoLicenca = l.Valor.DiasRestantes(hoje) <= 30 ? $"A licença vence em {l.Valor.ValidaAte:dd/MM/yyyy}: solicite a renovação ao fornecedor." : null;
        return true;
    }

    /// <summary>Instala o arquivo de licença escolhido (a view abre o diálogo de arquivo) e tenta entrar de novo.</summary>
    public async Task InstalarLicencaAsync(string arquivo)
    {
        Erros.Clear();
        var r = LicencaInstalada.Instalar(arquivo);
        if (!r.Sucesso) { foreach (var e in r.Erros) Erros.Add(e); return; }
        PrecisaLicenca = false;
        Mensagem = $"Licença instalada: {r.Valor.Cliente} — {r.Valor.ModulosTexto}, válida até {r.Valor.ValidaAte:dd/MM/yyyy}.";
        if (!string.IsNullOrEmpty(Usuario) && !string.IsNullOrEmpty(Senha)) await EntrarAsync();
    }

    private bool Preparar(out Infra.VorticisApp app)
    {
        Erros.Clear();
        Mensagem = null;
        app = main.App!;
        if (main.App is null) { Erros.Add("Sem conexão com o banco de dados."); return false; }
        return true;
    }

    private async Task Concluir(SessaoUsuario sessao)
    {
        var login = sessao.Implantacao ? "" : Usuario;
        // o acesso de implantação é de uso único: nunca fica salvo no computador
        if (SalvarLogin && !sessao.Implantacao) LoginSalvo.Salvar(login.Trim(), Senha);
        else if (!sessao.Implantacao) LoginSalvo.Esquecer();
        var salvar = SalvarLogin && !sessao.Implantacao;
        Limpar();
        (Usuario, SalvarLogin) = (login, salvar);
        await main.EntrarAsync(sessao);
    }
}
