using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Application.Seguranca;
using Perseus.Domain.Seguranca;

namespace Perseus.Desktop.ViewModels;

public sealed record UsuarioLinha(UsuarioResumo Usuario)
{
    public string Login => Usuario.Login;
    public string Nome => Usuario.Nome;
    public string Tipo => Usuario.Administrador ? "Administrador" : "Usuário";
    public string Situacao => Usuario.Ativo ? "Ativo" : "Inativo";
    public string UltimoAcesso => Usuario.UltimoAcesso?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "nunca";
}

public sealed record OpcaoPerfil(long? Id, string Nome);

public partial class AcessoLinha : ObservableObject
{
    public static readonly OpcaoPerfil SemAcesso = new(null, "Sem acesso");

    public required long EmpresaId { get; init; }
    public required string Empresa { get; init; }
    public required IReadOnlyList<OpcaoPerfil> Opcoes { get; init; }
    public long? Original { get; set; }
    [ObservableProperty] private OpcaoPerfil _perfil = SemAcesso;
}

/// <summary>Configurações › Usuários e permissões (somente administrador; o serviço também verifica).</summary>
public partial class UsuariosViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<UsuarioLinha> Lista { get; } = [];
    public ObservableCollection<AcessoLinha> Acessos { get; } = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TemSelecionado))] private UsuarioLinha? _selecionado;
    [ObservableProperty] private string _nomeEdicao = "";
    [ObservableProperty] private bool _adminEdicao;
    [ObservableProperty] private bool _ativoEdicao;
    [ObservableProperty] private string _senhaTemporaria = "";

    [ObservableProperty] private string _novoLogin = "";
    [ObservableProperty] private string _novoNome = "";
    [ObservableProperty] private string _novaSenha = "";
    [ObservableProperty] private bool _novoAdmin;

    public bool TemSelecionado => Selecionado is not null;

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao) return;
        var id = Selecionado?.Usuario.Id;
        var geracao = NovaGeracao();
        var usuarios = await app.Usuarios.ListarAsync(sessao);
        if (!EhAtual(geracao)) return;
        Lista.Clear();
        foreach (var u in usuarios) Lista.Add(new UsuarioLinha(u));
        Selecionado = Lista.FirstOrDefault(l => l.Usuario.Id == id);
    }

    partial void OnSelecionadoChanged(UsuarioLinha? value)
    {
        Acessos.Clear();
        if (value is null) return;
        (NomeEdicao, AdminEdicao, AtivoEdicao, SenhaTemporaria) = (value.Nome, value.Usuario.Administrador, value.Usuario.Ativo, "");
        _ = CarregarAcessosAsync(value.Usuario.Id);
    }

    private async Task CarregarAcessosAsync(long usuarioId)
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao) return;
        try
        {
            var opcoes = new List<OpcaoPerfil> { AcessoLinha.SemAcesso };
            opcoes.AddRange((await app.Perfis.ListarAsync(sessao)).Select(p => new OpcaoPerfil(p.Id, p.Nome)));
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

    [RelayCommand]
    private Task CriarAsync() => Admin(async (app, sessao) =>
    {
        var r = await app.Usuarios.CriarUsuarioAsync(sessao, NovoLogin, NovoNome, NovaSenha, NovoAdmin);
        MostrarResultado(r, $"Usuário {NovoLogin.Trim()} criado. Ele deverá trocar a senha no primeiro acesso.");
        if (!r.Sucesso) return;
        (NovoLogin, NovoNome, NovaSenha, NovoAdmin) = ("", "", "", false);
        await CarregarAsync();
        Selecionado = Lista.FirstOrDefault(l => l.Usuario.Id == r.Valor);
    });

    [RelayCommand]
    private Task SalvarAsync() => Admin(async (app, sessao) =>
    {
        if (Selecionado is null) return;
        var id = Selecionado.Usuario.Id;
        var r = await app.Usuarios.AlterarUsuarioAsync(sessao, id, NomeEdicao, AdminEdicao, AtivoEdicao);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        foreach (var a in Acessos.Where(a => a.Perfil.Id != a.Original))
        {
            var acesso = await app.Usuarios.DefinirAcessoAsync(sessao, id, a.EmpresaId, a.Perfil.Id);
            if (!acesso.Sucesso) { MostrarResultado(acesso, ""); return; }
            a.Original = a.Perfil.Id;
        }
        await CarregarAsync();
        Mensagem = "Alterações salvas.";
    });

    [RelayCommand]
    private Task RedefinirSenhaAsync() => Admin(async (app, sessao) =>
    {
        if (Selecionado is null) return;
        var r = await app.Usuarios.RedefinirSenhaAsync(sessao, Selecionado.Usuario.Id, SenhaTemporaria);
        MostrarResultado(r, $"Senha de {Selecionado.Login} redefinida e desbloqueada. Ele deverá trocá-la no próximo acesso.");
        if (r.Sucesso) SenhaTemporaria = "";
    });

    private async Task Admin(Func<Infra.PerseusApp, SessaoUsuario, Task> acao)
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
