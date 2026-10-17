using Perseus.Domain.Comum;
using Perseus.Domain.Seguranca;

namespace Perseus.Application.Sistema;

/// <summary>Como a tela de lançamento é digitada.</summary>
public enum ModoLancamento
{
    /// <summary>Cada linha é um lançamento completo: conta débito, conta crédito e valor.</summary>
    PartidaDobrada,
    /// <summary>Cada linha é uma partida (uma conta, D ou C); um lançamento tem no mínimo 2 linhas.</summary>
    PartidaSimples,
}

public sealed record ConfiguracoesGerais(ModoLancamento ModoLancamento, string? PastaBackup = null);

/// <summary>Configurações › Configurações gerais (valem para todos os usuários e empresas).</summary>
public sealed class ServicoConfiguracoesGerais(IRepositorioConfiguracao repositorio)
{
    private const string ChaveModo = "modo_lancamento";
    private const string ChavePastaBackup = "pasta_backup";

    public async Task<ConfiguracoesGerais> ObterAsync(CancellationToken ct = default)
    {
        var v = await repositorio.LerAsync([ChaveModo, ChavePastaBackup], ct);
        var modo = v.TryGetValue(ChaveModo, out var m) && Enum.TryParse<ModoLancamento>(m.Valor, out var lido) ? lido : ModoLancamento.PartidaDobrada;
        return new ConfiguracoesGerais(modo, v.TryGetValue(ChavePastaBackup, out var p) && p.Valor.Length > 0 ? p.Valor : null);
    }

    /// <summary>Pasta dos backups: a escolhida pelo usuário ou a padrão (no diretório de instalação).</summary>
    public async Task<string> PastaBackupAsync(CancellationToken ct = default) => (await ObterAsync(ct)).PastaBackup ?? ServicoBackup.PastaPadrao;

    /// <summary>Grava a pasta escolhida para os backups (vazio = volta à padrão). Precisa ser possível gravar nela.</summary>
    public async Task<Resultado> SalvarPastaBackupAsync(SessaoUsuario admin, string? pasta, CancellationToken ct = default)
    {
        if (!admin.Administrador) return Resultado.Falha("Somente o administrador define a pasta dos backups.");
        var p = string.IsNullOrWhiteSpace(pasta) ? "" : pasta.Trim();
        if (p.Length > 0 && !ServicoBackup.PodeGravar(p)) return Resultado.Falha($"Não é possível gravar na pasta {p}.");
        await repositorio.GravarAsync(new Dictionary<string, string?> { [ChavePastaBackup] = p }, admin.Id, ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> SalvarAsync(SessaoUsuario admin, ConfiguracoesGerais c, CancellationToken ct = default)
    {
        if (!admin.Administrador) return Resultado.Falha("Somente o administrador altera as configurações gerais.");
        await repositorio.GravarAsync(new Dictionary<string, string?> { [ChaveModo] = c.ModoLancamento.ToString() }, admin.Id, ct);
        return Resultado.Ok();
    }

    public const string TextoConfirmacaoLimpeza = "LIMPAR";

    /// <summary>Senha de administrador inicial (pedida para restaurar backup e limpar o banco). Troque-a em Configurações gerais.</summary>
    public const string SenhaAdministradorPadrao = "PERSEUS";
    /// <summary>Login e senha do administrador recriado depois da limpeza do banco.</summary>
    public const string LoginAdministradorPadrao = "ADMIN";
    private const string ChaveSenhaAdministrador = "senha_administrador";

    /// <summary>Confere a senha de administrador (hash em configuracao_sistema; enquanto não for trocada, vale a padrão).</summary>
    public async Task<bool> VerificarSenhaAdministradorAsync(string? senha, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(senha)) return false;
        var v = await repositorio.LerAsync([ChaveSenhaAdministrador], ct);
        return v.TryGetValue(ChaveSenhaAdministrador, out var h) && h.Valor.Length > 0
            ? Senha.Verificar(senha, h.Valor)
            : senha == SenhaAdministradorPadrao;
    }

    /// <summary>Ainda usa a senha de administrador padrão (aviso para trocá-la).</summary>
    public async Task<bool> UsandoSenhaPadraoAsync(CancellationToken ct = default) =>
        await VerificarSenhaAdministradorAsync(SenhaAdministradorPadrao, ct);

    public async Task<Resultado> AlterarSenhaAdministradorAsync(SessaoUsuario admin, string atual, string nova, string confirmacao, CancellationToken ct = default)
    {
        if (!admin.Administrador) return Resultado.Falha("Somente o administrador altera a senha de administrador.");
        if (!await VerificarSenhaAdministradorAsync(atual, ct)) return Resultado.Falha("Senha de administrador atual incorreta.");
        if (nova != confirmacao) return Resultado.Falha("A confirmação não confere com a nova senha.");
        if (nova.Length < 6) return Resultado.Falha("A senha de administrador deve ter ao menos 6 caracteres.");
        await repositorio.GravarAsync(new Dictionary<string, string?> { [ChaveSenhaAdministrador] = Senha.GerarHash(nova) }, admin.Id, ct);
        return Resultado.Ok();
    }

    /// <summary>
    /// Limpa o banco de dados: exclui TODAS as empresas com a escrituração, contadores, escritórios, tabelas do plano
    /// referencial, layouts, usuários e perfis personalizados, e recria o administrador padrão (ADMIN / PERSEUS, com troca
    /// de senha no primeiro acesso). Não recomendado: só o administrador, digitando LIMPAR e informando a senha de administrador.
    /// </summary>
    public async Task<Resultado<int>> LimparBancoAsync(SessaoUsuario admin, string confirmacao, string? senhaAdministrador, CancellationToken ct = default)
    {
        if (!admin.Administrador) return Resultado<int>.Falha("Somente o administrador pode limpar o banco de dados.");
        if (confirmacao.Trim() != TextoConfirmacaoLimpeza)
            return Resultado<int>.Falha($"Para confirmar, digite {TextoConfirmacaoLimpeza} (em maiúsculas) no campo de confirmação.");
        if (!await VerificarSenhaAdministradorAsync(senhaAdministrador, ct)) return Resultado<int>.Falha("Senha de administrador incorreta.");
        try
        {
            return Resultado<int>.Ok(await repositorio.LimparBancoAsync(admin.Id, Senha.GerarHash(SenhaAdministradorPadrao), ct));
        }
        catch (ViolacaoRegraException ex) { return Resultado<int>.Falha(ex.Message); }
    }
}
