using Perseus.Domain.Comum;
using Perseus.Domain.Seguranca;

namespace Perseus.Application.Seguranca;

public sealed record CredenciaisUsuario(long Id, string Login, string Nome, string SenhaHash, bool Ativo, bool Administrador,
                                        bool DeveTrocarSenha, int TentativasFalhas, DateTimeOffset? BloqueadoAte);

/// <param name="PerfilId">Perfil do usuário (vale para todas as empresas; o acesso por empresa admite exceções).</param>
public sealed record UsuarioResumo(long Id, string Login, string Nome, bool Administrador, bool Ativo, DateTimeOffset? UltimoAcesso,
                                   long? PerfilId = null, string? Perfil = null);

public sealed record PerfilDoUsuario(long Id, string Nome, bool Administrador);

/// <summary>Tentativa de login registrada em core.registro_acesso.</summary>
public sealed record RegistroAcesso(DateTimeOffset OcorridoEm, string Login, bool Sucesso, string? Motivo, string? Estacao);

/// <param name="Perfil">Nome do perfil do usuário na empresa; nulo para administrador sem perfil atribuído.</param>
public sealed record EmpresaPermitida(long Id, string Cnpj, string RazaoSocial, string? Perfil)
{
    /// <summary>Código da empresa no sistema (nº do cadastro, 4 dígitos).</summary>
    public string Codigo => Id.ToString("0000");
    public string CnpjFormatado => Domain.Cadastros.Cnpj.Formatar(Cnpj);
}

public sealed record AcessoEmpresa(long EmpresaId, string RazaoSocial, long PerfilId, string Perfil);

public interface IRepositorioUsuarios
{
    Task<bool> ExisteAlgumAsync(CancellationToken ct);
    Task<CredenciaisUsuario?> ObterPorLoginAsync(string login, CancellationToken ct);
    Task<CredenciaisUsuario?> ObterPorIdAsync(long id, CancellationToken ct);
    Task<bool> LoginExisteAsync(string login, CancellationToken ct);

    /// <summary>Insere o usuário. Com <paramref name="somenteSeNaoHouverUsuarios"/>, só insere se a tabela estiver vazia (retorna null caso contrário).</summary>
    Task<long?> CriarAsync(string login, string nome, string senhaHash, bool administrador, long? perfilId, bool deveTrocarSenha,
                           bool somenteSeNaoHouverUsuarios, CancellationToken ct);
    Task<PerfilDoUsuario?> ObterPerfilAsync(long perfilId, CancellationToken ct);
    Task<long?> IdPerfilAdministradorAsync(CancellationToken ct);
    /// <summary>Concede o perfil em todas as empresas cadastradas (substitui acessos existentes).</summary>
    Task AtribuirPerfilEmTodasEmpresasAsync(long usuarioId, long perfilId, CancellationToken ct);
    Task<IReadOnlyList<RegistroAcesso>> ListarRegistroAcessoAsync(int limite, CancellationToken ct);
    /// <summary>Exclui o usuário do banco (o histórico fica sem o vínculo; a auditoria guarda o login).</summary>
    Task ExcluirAsync(long usuarioId, CancellationToken ct);

    Task RegistrarFalhaAsync(long? usuarioId, string login, int tentativas, DateTimeOffset? bloquearAte, string motivo, string? estacao, CancellationToken ct);
    Task RegistrarSucessoAsync(long usuarioId, string login, string? novoHash, string? estacao, CancellationToken ct);
    Task AlterarSenhaAsync(long usuarioId, string senhaHash, bool deveTrocarSenha, CancellationToken ct);
    Task AlterarAsync(long usuarioId, string nome, bool administrador, long perfilId, bool ativo, CancellationToken ct);
    Task<IReadOnlyList<UsuarioResumo>> ListarAsync(CancellationToken ct);
    /// <param name="perfilId">Perfil a atribuir; nulo revoga o acesso.</param>
    Task DefinirAcessoAsync(long usuarioId, long empresaId, long? perfilId, CancellationToken ct);
    Task<IReadOnlyList<AcessoEmpresa>> ListarAcessosAsync(long usuarioId, CancellationToken ct);
    Task<IReadOnlyList<EmpresaPermitida>> ListarEmpresasPermitidasAsync(long usuarioId, bool administrador, CancellationToken ct);
}

/// <summary>
/// Autenticação e gestão de usuários. Operações administrativas exigem uma sessão de administrador
/// e são verificadas aqui (não apenas na interface).
/// </summary>
public sealed class ServicoUsuarios(IRepositorioUsuarios repositorio, TimeProvider? relogio = null)
{
    public const int TentativasAteBloqueio = 5;
    public static readonly TimeSpan TempoBloqueio = TimeSpan.FromMinutes(15);
    private const string MensagemGenerica = "Usuário ou senha inválidos.";

    // Hash de referência para gastar o mesmo tempo quando o login não existe (evita descobrir logins pelo tempo de resposta).
    private static readonly Lazy<string> HashFicticio = new(() => Senha.GerarHash(Guid.NewGuid().ToString()));

    private readonly TimeProvider _relogio = relogio ?? TimeProvider.System;

    public Task<bool> PrecisaCriarAdministradorAsync(CancellationToken ct = default) => NaoExiste(ct);

    private async Task<bool> NaoExiste(CancellationToken ct) => !await repositorio.ExisteAlgumAsync(ct);

    /// <summary>Primeiro acesso: cria o administrador. Só funciona enquanto não houver nenhum usuário.</summary>
    public async Task<Resultado<SessaoUsuario>> CriarPrimeiroAdministradorAsync(string login, string nome, string senha, string confirmacao,
                                                                              CancellationToken ct = default)
    {
        var erros = ValidarNovo(login, nome, senha, confirmacao);
        if (erros.Count > 0) return Resultado<SessaoUsuario>.Falha(erros);

        var id = await repositorio.CriarAsync(login.Trim(), nome.Trim(), Senha.GerarHash(senha), administrador: true,
                                              await repositorio.IdPerfilAdministradorAsync(ct),
                                              deveTrocarSenha: false, somenteSeNaoHouverUsuarios: true, ct);
        if (id is null) return Resultado<SessaoUsuario>.Falha("Já existe usuário cadastrado. Entre com seu login.");
        await repositorio.RegistrarSucessoAsync(id.Value, login.Trim(), null, Environment.MachineName, ct);
        return Resultado<SessaoUsuario>.Ok(new SessaoUsuario(id.Value, login.Trim(), nome.Trim(), true, false));
    }

    public async Task<Resultado<SessaoUsuario>> AutenticarAsync(string login, string senha, CancellationToken ct = default)
    {
        login = login.Trim();
        var estacao = Environment.MachineName;
        if (login.Length == 0 || senha.Length == 0) return Resultado<SessaoUsuario>.Falha("Informe usuário e senha.");

        var u = await repositorio.ObterPorLoginAsync(login, ct);
        if (u is null)
        {
            Senha.Verificar(senha, HashFicticio.Value);
            await repositorio.RegistrarFalhaAsync(null, login, 0, null, "USUARIO_INEXISTENTE", estacao, ct);
            return Resultado<SessaoUsuario>.Falha(MensagemGenerica);
        }

        var agora = _relogio.GetUtcNow();
        if (u.BloqueadoAte is { } ate && ate > agora)
        {
            await repositorio.RegistrarFalhaAsync(u.Id, login, u.TentativasFalhas, ate, "BLOQUEADO", estacao, ct);
            return Resultado<SessaoUsuario>.Falha(
                $"Usuário bloqueado por excesso de tentativas. Tente novamente após {ate.ToLocalTime():HH:mm} ou peça ao administrador para redefinir a senha.");
        }

        if (!Senha.Verificar(senha, u.SenhaHash) || !u.Ativo)
        {
            var tentativas = u.Ativo ? u.TentativasFalhas + 1 : u.TentativasFalhas;
            DateTimeOffset? bloquear = tentativas >= TentativasAteBloqueio ? agora + TempoBloqueio : null;
            await repositorio.RegistrarFalhaAsync(u.Id, login, bloquear is null ? tentativas : 0, bloquear,
                                                  u.Ativo ? "SENHA_INVALIDA" : "INATIVO", estacao, ct);
            return Resultado<SessaoUsuario>.Falha(bloquear is null
                ? MensagemGenerica
                : $"Usuário bloqueado por {TempoBloqueio.TotalMinutes:0} minutos após {TentativasAteBloqueio} tentativas inválidas.");
        }

        await repositorio.RegistrarSucessoAsync(u.Id, login, Senha.PrecisaRehash(u.SenhaHash) ? Senha.GerarHash(senha) : null, estacao, ct);
        return Resultado<SessaoUsuario>.Ok(new SessaoUsuario(u.Id, u.Login, u.Nome, u.Administrador, u.DeveTrocarSenha));
    }

    public async Task<Resultado> AlterarPropriaSenhaAsync(SessaoUsuario sessao, string senhaAtual, string novaSenha, string confirmacao,
                                                         CancellationToken ct = default)
    {
        var u = await repositorio.ObterPorIdAsync(sessao.Id, ct);
        if (u is null || !Senha.Verificar(senhaAtual, u.SenhaHash)) return Resultado.Falha("Senha atual incorreta.");
        var erros = Senha.ValidarPolitica(novaSenha, u.Login).ToList();
        if (novaSenha != confirmacao) erros.Add("A confirmação não confere com a nova senha.");
        if (novaSenha == senhaAtual) erros.Add("A nova senha deve ser diferente da atual.");
        if (erros.Count > 0) return Resultado.Falha(erros);
        await repositorio.AlterarSenhaAsync(u.Id, Senha.GerarHash(novaSenha), deveTrocarSenha: false, ct);
        return Resultado.Ok();
    }

    // ---------------------------------------------------------------- administração

    /// <summary>
    /// Cadastra um usuário com o perfil informado. O perfil Administrador torna o usuário administrador do sistema;
    /// os demais perfis são concedidos em todas as empresas (e nas que forem cadastradas depois).
    /// </summary>
    public async Task<Resultado<long>> CriarUsuarioAsync(SessaoUsuario admin, string login, string nome, string senhaInicial,
                                                        long? perfilId, CancellationToken ct = default)
    {
        if (!admin.Administrador) return Resultado<long>.Falha("Somente administradores podem cadastrar usuários.");
        var erros = ValidarNovo(login, nome, senhaInicial, senhaInicial);
        var perfil = perfilId is long pid ? await repositorio.ObterPerfilAsync(pid, ct) : null;
        if (perfil is null) erros.Add("Informe o perfil do usuário.");
        if (erros.Count > 0) return Resultado<long>.Falha(erros);
        if (await repositorio.LoginExisteAsync(login.Trim(), ct)) return Resultado<long>.Falha($"O login \"{login.Trim()}\" já está em uso.");
        var id = (await repositorio.CriarAsync(login.Trim(), nome.Trim(), Senha.GerarHash(senhaInicial), perfil!.Administrador, perfil.Id,
                                               deveTrocarSenha: true, somenteSeNaoHouverUsuarios: false, ct))!.Value;
        if (!perfil.Administrador) await repositorio.AtribuirPerfilEmTodasEmpresasAsync(id, perfil.Id, ct);
        return Resultado<long>.Ok(id);
    }

    public async Task<Resultado> RedefinirSenhaAsync(SessaoUsuario admin, long usuarioId, string senhaTemporaria, CancellationToken ct = default)
    {
        if (!admin.Administrador) return Resultado.Falha("Somente administradores podem redefinir senhas.");
        var u = await repositorio.ObterPorIdAsync(usuarioId, ct);
        if (u is null) return Resultado.Falha("Usuário não encontrado.");
        var erros = Senha.ValidarPolitica(senhaTemporaria, u.Login);
        if (erros.Count > 0) return Resultado.Falha(erros);
        await repositorio.AlterarSenhaAsync(usuarioId, Senha.GerarHash(senhaTemporaria), deveTrocarSenha: true, ct);   // também desbloqueia
        return Resultado.Ok();
    }

    /// <summary>Altera nome, perfil e situação. O perfil Administrador concede (e os demais retiram) a administração do sistema.</summary>
    public async Task<Resultado> AlterarUsuarioAsync(SessaoUsuario admin, long usuarioId, string nome, long? perfilId, bool ativo,
                                                    CancellationToken ct = default)
    {
        if (!admin.Administrador) return Resultado.Falha("Somente administradores podem alterar usuários.");
        if (string.IsNullOrWhiteSpace(nome)) return Resultado.Falha("Informe o nome.");
        var perfil = perfilId is long pid ? await repositorio.ObterPerfilAsync(pid, ct) : null;
        if (perfil is null) return Resultado.Falha("Informe o perfil do usuário.");
        if (usuarioId == admin.Id && (!perfil.Administrador || !ativo))
            return Resultado.Falha("Você não pode remover seu próprio acesso de administrador.");
        try
        {
            await repositorio.AlterarAsync(usuarioId, nome.Trim(), perfil.Administrador, perfil.Id, ativo, ct);
            return Resultado.Ok();
        }
        catch (ViolacaoRegraException ex)
        {
            return Resultado.Falha(ex.Message);
        }
    }

    public async Task<Resultado> DefinirAcessoAsync(SessaoUsuario admin, long usuarioId, long empresaId, long? perfilId,
                                                   CancellationToken ct = default)
    {
        if (!admin.Administrador) return Resultado.Falha("Somente administradores podem conceder acesso.");
        try
        {
            await repositorio.DefinirAcessoAsync(usuarioId, empresaId, perfilId, ct);
            return Resultado.Ok();
        }
        catch (ViolacaoRegraException ex)
        {
            return Resultado.Falha(ex.Message);
        }
    }

    public async Task<IReadOnlyList<UsuarioResumo>> ListarAsync(SessaoUsuario admin, CancellationToken ct = default)
        => admin.Administrador ? await repositorio.ListarAsync(ct) : [];

    /// <summary>
    /// Exclui o usuário do banco de dados, liberando o login para um novo cadastro. O histórico (lançamentos, acessos)
    /// é mantido sem o vínculo; a trilha de auditoria guarda o login de quem fez cada operação.
    /// </summary>
    public async Task<Resultado> ExcluirUsuarioAsync(SessaoUsuario admin, long usuarioId, CancellationToken ct = default)
    {
        if (!admin.Administrador) return Resultado.Falha("Somente administradores podem excluir usuários.");
        if (usuarioId == admin.Id) return Resultado.Falha("Você não pode excluir o seu próprio usuário.");
        try
        {
            await repositorio.ExcluirAsync(usuarioId, ct);
            return Resultado.Ok();
        }
        catch (ViolacaoRegraException ex)
        {
            return Resultado.Falha(ex.Message);
        }
    }

    /// <summary>Últimas tentativas de login (sucesso e falha), mais recentes primeiro.</summary>
    public async Task<IReadOnlyList<RegistroAcesso>> RegistroDeAcessosAsync(SessaoUsuario admin, int limite = 300, CancellationToken ct = default)
        => admin.Administrador ? await repositorio.ListarRegistroAcessoAsync(limite, ct) : [];

    public Task<IReadOnlyList<AcessoEmpresa>> ListarAcessosAsync(long usuarioId, CancellationToken ct = default)
        => repositorio.ListarAcessosAsync(usuarioId, ct);

    public Task<IReadOnlyList<EmpresaPermitida>> EmpresasPermitidasAsync(SessaoUsuario sessao, CancellationToken ct = default)
        => repositorio.ListarEmpresasPermitidasAsync(sessao.Id, sessao.Administrador, ct);

    private static List<string> ValidarNovo(string login, string nome, string senha, string confirmacao)
    {
        var erros = new List<string>();
        var l = login.Trim();
        if (l.Length is < 3 or > 60 || !l.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-'))
            erros.Add("O login deve ter de 3 a 60 caracteres: letras, números, ponto, hífen ou sublinhado.");
        if (string.IsNullOrWhiteSpace(nome)) erros.Add("Informe o nome.");
        erros.AddRange(Senha.ValidarPolitica(senha, l));
        if (senha != confirmacao) erros.Add("A confirmação não confere com a senha.");
        return erros;
    }
}
