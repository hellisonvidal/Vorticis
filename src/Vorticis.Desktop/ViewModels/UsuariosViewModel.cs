using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Desktop.ViewModels;

public sealed record UsuarioLinha(UsuarioResumo Usuario)
{
    public string Numero => Usuario.Id.ToString("0000");
    public string Login => Usuario.Login;
    public string Nome => Usuario.Nome;
    public string Perfil => Usuario.Perfil ?? (Usuario.Administrador ? "Administrador" : "(definir perfil)");
    public string Situacao => Usuario.Ativo ? "Ativo" : "Inativo";
    public string UltimoAcesso => Usuario.UltimoAcesso?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "nunca";
}

public sealed record OpcaoPerfil(long? Id, string Nome, bool Administrador = false);

public partial class AcessoLinha : ObservableObject
{
    public static readonly OpcaoPerfil SemAcesso = new(null, "Sem acesso");

    public required long EmpresaId { get; init; }
    public required string Empresa { get; init; }
    public required IReadOnlyList<OpcaoPerfil> Opcoes { get; init; }
    public long? Original { get; set; }
    [ObservableProperty] private OpcaoPerfil _perfil = SemAcesso;
}

/// <summary>Configurações › Usuários, aba Usuários: Incluir / Alterar / Excluir, com o perfil do usuário e exceções por empresa (somente administrador).</summary>
public partial class UsuariosViewModel(IContexto contexto) : CadastroViewModel(contexto)
{
    public ObservableCollection<UsuarioLinha> Lista { get; } = [];
    public ObservableCollection<AcessoLinha> Acessos { get; } = [];

    /// <summary>Perfis que podem ser atribuídos ao usuário (aba Perfis de acesso).</summary>
    public ObservableCollection<OpcaoPerfil> PerfisDisponiveis { get; } = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TemSelecionado))] private UsuarioLinha? _selecionado;
    [ObservableProperty] private string _login = "";
    [ObservableProperty] private string _nomeEdicao = "";
    [ObservableProperty] private string _novaSenha = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(MostrarAcessos), nameof(DescricaoPerfil))] private OpcaoPerfil? _perfilEdicao;
    [ObservableProperty] private bool _ativoEdicao = true;
    [ObservableProperty] private string _senhaTemporaria = "";

    private bool _carregandoSelecao;

    public bool TemSelecionado => Selecionado is not null;

    /// <summary>Administradores acessam todas as empresas; para os demais, o acesso por empresa admite exceções (definidas ao alterar).</summary>
    public bool MostrarAcessos => !EmInclusao && PerfilEdicao is { Administrador: false };

    public string DescricaoPerfil => PerfilEdicao switch
    {
        null => "Escolha o perfil: ele define o que o usuário poderá fazer no sistema.",
        { Administrador: true } => "Administrador: todas as funções, em todas as empresas, inclusive usuários, perfis, empresas e backup.",
        var p => $"Perfil {p.Nome}: concedido em todas as empresas (e nas cadastradas depois). Exceções por empresa abaixo.",
    };

    protected override string NomeRegistro => "usuário";
    protected override string? DescricaoSelecionado => Selecionado is { } u ? $"{u.Nome} ({u.Login})" : null;
    public override bool PodeManter => Contexto.Sessao?.Administrador == true;
    protected override string DetalheExclusao =>
        "\n\nO usuário é apagado do banco de dados e o login fica livre para um novo cadastro. Os lançamentos que ele fez " +
        "continuam na escrituração; a trilha de auditoria guarda o login. Para só bloquear o acesso, use Alterar e desmarque Ativo.";

    protected override void AoMudarModo() => OnPropertyChanged(nameof(MostrarAcessos));

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao) return;
        var id = Selecionado?.Usuario.Id;
        var geracao = NovaGeracao();
        var usuarios = await app.Usuarios.ListarAsync(sessao);
        var perfis = await app.Perfis.ListarAsync(sessao);
        if (!EhAtual(geracao)) return;
        PerfisDisponiveis.Clear();
        foreach (var p in perfis) PerfisDisponiveis.Add(new OpcaoPerfil(p.Id, p.Nome, p.Administrador));
        Lista.Clear();
        foreach (var u in usuarios) Lista.Add(new UsuarioLinha(u));
        Selecionado = Lista.FirstOrDefault(l => l.Usuario.Id == id);
        AtualizarBarra();
    }

    partial void OnSelecionadoChanged(UsuarioLinha? value)
    {
        if (EmConsulta) MostrarSelecionado();
        AtualizarBarra();
    }

    protected override void MostrarSelecionado()
    {
        Acessos.Clear();
        _carregandoSelecao = true;
        if (Selecionado is { } value)
        {
            (Login, NomeEdicao, AtivoEdicao, SenhaTemporaria, NovaSenha) = (value.Login, value.Nome, value.Usuario.Ativo, "", "");
            PerfilEdicao = PerfisDisponiveis.FirstOrDefault(p => p.Id == value.Usuario.PerfilId);
            _ = CarregarAcessosAsync(value.Usuario.Id);
        }
        else
        {
            (Login, NomeEdicao, AtivoEdicao, SenhaTemporaria, NovaSenha) = ("", "", true, "", "");
            PerfilEdicao = null;
        }
        _carregandoSelecao = false;
    }

    protected override void PrepararInclusao()
    {
        Acessos.Clear();
        _carregandoSelecao = true;
        (Login, NomeEdicao, NovaSenha, AtivoEdicao, PerfilEdicao) = ("", "", "", true, null);
        _carregandoSelecao = false;
    }

    /// <summary>Trocar o perfil do usuário aplica o novo perfil em todas as empresas (as exceções podem ser refeitas abaixo).</summary>
    partial void OnPerfilEdicaoChanged(OpcaoPerfil? value)
    {
        if (_carregandoSelecao || value is null || value.Administrador) return;
        foreach (var a in Acessos) a.Perfil = a.Opcoes.FirstOrDefault(o => o.Id == value.Id) ?? a.Perfil;
    }

    private async Task CarregarAcessosAsync(long usuarioId)
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao) return;
        try
        {
            var opcoes = new List<OpcaoPerfil> { AcessoLinha.SemAcesso };
            opcoes.AddRange((await app.Perfis.ListarAsync(sessao)).Select(p => new OpcaoPerfil(p.Id, p.Nome, p.Administrador)));
            var atuais = (await app.Usuarios.ListarAcessosAsync(usuarioId)).ToDictionary(a => a.EmpresaId, a => a.PerfilId);
            var todas = await app.Usuarios.EmpresasPermitidasAsync(sessao);   // administrador: todas
            Acessos.Clear();
            foreach (var e in todas)
            {
                long? atual = atuais.TryGetValue(e.Id, out var pid) ? pid : null;
                Acessos.Add(new AcessoLinha
                {
                    EmpresaId = e.Id, Empresa = e.RazaoSocial, Opcoes = opcoes, Original = atual,
                    Perfil = opcoes.FirstOrDefault(o => o.Id == atual) ?? AcessoLinha.SemAcesso,
                });
            }
        }
        catch (Exception ex)
        {
            Erros.Add(ex.Message);
        }
    }

    protected override async Task<bool> GravarAsync(bool inclusao)
    {
        var ok = false;
        await Admin(async (app, sessao) =>
        {
            if (inclusao)
            {
                var r = await app.Usuarios.CriarUsuarioAsync(sessao, Login, NomeEdicao, NovaSenha, PerfilEdicao?.Id);
                MostrarResultado(r, $"Usuário {Login.Trim()} criado com o perfil {PerfilEdicao?.Nome}. Ele deverá trocar a senha no primeiro acesso.");
                if (!r.Sucesso) return;
                ok = true;
                Modo = ModoCadastro.Consulta;
                NovaSenha = "";
                await CarregarAsync();
                Selecionado = Lista.FirstOrDefault(l => l.Usuario.Id == r.Valor);
                return;
            }
            if (Selecionado is null) return;
            var id = Selecionado.Usuario.Id;
            var alt = await app.Usuarios.AlterarUsuarioAsync(sessao, id, NomeEdicao, PerfilEdicao?.Id, AtivoEdicao);
            if (!alt.Sucesso) { MostrarResultado(alt, ""); return; }
            foreach (var a in Acessos.Where(a => MostrarAcessos && a.Perfil.Id != a.Original))
            {
                var acesso = await app.Usuarios.DefinirAcessoAsync(sessao, id, a.EmpresaId, a.Perfil.Id);
                if (!acesso.Sucesso) { MostrarResultado(acesso, ""); return; }
                a.Original = a.Perfil.Id;
            }
            ok = true;
            Modo = ModoCadastro.Consulta;
            await CarregarAsync();
            Mensagem = "Alterações salvas.";
        });
        return ok;
    }

    protected override async Task<bool> ExcluirSelecionadoAsync()
    {
        var ok = false;
        await Admin(async (app, sessao) =>
        {
            if (Selecionado is not { } u) return;
            var r = await app.Usuarios.ExcluirUsuarioAsync(sessao, u.Usuario.Id);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            ok = true;
            Selecionado = null;
            await CarregarAsync();
            Mensagem = $"Usuário {u.Login} excluído.";
        });
        return ok;
    }

    [RelayCommand]
    private Task RedefinirSenhaAsync() => Admin(async (app, sessao) =>
    {
        if (Selecionado is null) return;
        var r = await app.Usuarios.RedefinirSenhaAsync(sessao, Selecionado.Usuario.Id, SenhaTemporaria);
        MostrarResultado(r, $"Senha de {Selecionado.Login} redefinida e desbloqueada. Ele deverá trocá-la no próximo acesso.");
        if (r.Sucesso) SenhaTemporaria = "";
    });

    private async Task Admin(Func<Infra.VorticisApp, SessaoUsuario, Task> acao)
    {
        Erros.Clear();
        Mensagem = null;
        if (Contexto.App is not { } app || Contexto.Sessao is not { Administrador: true } sessao)
        {
            Erros.Add("Somente administradores.");
            return;
        }
        try { await acao(app, sessao); }
        catch (Exception ex) { Erros.Add(ex.Message); }
    }
}
