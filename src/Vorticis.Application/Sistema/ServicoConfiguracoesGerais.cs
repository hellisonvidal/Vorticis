using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Application.Sistema;

public sealed record ConfiguracoesGerais(string? PastaBackup = null);

/// <summary>Backup automático: uma vez por dia, no primeiro acesso do dia; guarda os últimos <paramref name="Manter"/>.</summary>
public sealed record BackupAutomatico(bool Ligado = true, int Manter = BackupAutomatico.ManterPadrao, DateTimeOffset? Ultimo = null)
{
    public const int ManterPadrao = 7, ManterMaximo = 90;
}

/// <summary>Configurações › Configurações gerais (valem para todos os usuários e empresas).</summary>
public sealed class ServicoConfiguracoesGerais(IRepositorioConfiguracao repositorio)
{
    private const string ChavePastaBackup = "pasta_backup";

    public async Task<ConfiguracoesGerais> ObterAsync(CancellationToken ct = default)
    {
        var v = await repositorio.LerAsync([ChavePastaBackup], ct);
        return new ConfiguracoesGerais(v.TryGetValue(ChavePastaBackup, out var p) && p.Valor.Length > 0 ? p.Valor : null);
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

    private const string ChaveBackupLigado = "backup_automatico", ChaveBackupManter = "backup_automatico_manter", ChaveBackupUltimo = "backup_automatico_ultimo";

    /// <summary>Configuração do backup automático (ligado por padrão: guarda os 7 últimos).</summary>
    public async Task<BackupAutomatico> BackupAutomaticoAsync(CancellationToken ct = default)
    {
        var v = await repositorio.LerAsync([ChaveBackupLigado, ChaveBackupManter, ChaveBackupUltimo], ct);
        string? L(string k) => v.TryGetValue(k, out var x) && x.Valor.Length > 0 ? x.Valor : null;
        return new BackupAutomatico(L(ChaveBackupLigado) != "0",
                                    int.TryParse(L(ChaveBackupManter), out var m) ? Math.Clamp(m, 1, BackupAutomatico.ManterMaximo) : BackupAutomatico.ManterPadrao,
                                    DateTimeOffset.TryParse(L(ChaveBackupUltimo), System.Globalization.CultureInfo.InvariantCulture,
                                                            System.Globalization.DateTimeStyles.AssumeUniversal, out var u) ? u : null);
    }

    public async Task<Resultado> SalvarBackupAutomaticoAsync(SessaoUsuario admin, bool ligado, int manter, CancellationToken ct = default)
    {
        if (!admin.Administrador) return Resultado.Falha("Somente o administrador configura o backup automático.");
        if (manter is < 1 or > BackupAutomatico.ManterMaximo) return Resultado.Falha($"Guarde de 1 a {BackupAutomatico.ManterMaximo} backups automáticos.");
        await repositorio.GravarAsync(new Dictionary<string, string?> { [ChaveBackupLigado] = ligado ? "1" : "0", [ChaveBackupManter] = manter.ToString() }, admin.Id, ct);
        return Resultado.Ok();
    }

    /// <summary>Marca o dia do backup automático (antes de gerar: outro computador que entrar em seguida não repete).</summary>
    public Task MarcarBackupAutomaticoAsync(DateTimeOffset quando, CancellationToken ct = default)
        => repositorio.GravarAsync(new Dictionary<string, string?> { [ChaveBackupUltimo] = quando.ToUniversalTime().ToString("O") }, null, ct);

    public const string TextoConfirmacaoLimpeza = "LIMPAR";

    /// <summary>Senha de administrador inicial (pedida para restaurar backup e limpar o banco). Troque-a em Configurações gerais.</summary>
    public const string SenhaAdministradorPadrao = "VORTICIS";
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
    /// Limpa o banco de dados: exclui TODAS as empresas com a escrituração, contadores, escritórios, certificados, tabelas do
    /// plano referencial, layouts, TODOS os usuários e os perfis personalizados, e volta a senha de administrador à padrão.
    /// O banco fica como numa instalação nova: o próximo acesso é o do usuário de implantação (vorticis / vorticis, ver
    /// <see cref="Seguranca.ServicoUsuarios.LoginImplantacao"/>), que cadastra o administrador e depois segue o fluxo normal
    /// (definição dos perfis). Não recomendado: só o administrador, digitando LIMPAR e informando a senha de administrador.
    /// </summary>
    public async Task<Resultado<int>> LimparBancoAsync(SessaoUsuario admin, string confirmacao, string? senhaAdministrador, CancellationToken ct = default)
    {
        if (!admin.Administrador) return Resultado<int>.Falha("Somente o administrador pode limpar o banco de dados.");
        if (confirmacao.Trim() != TextoConfirmacaoLimpeza)
            return Resultado<int>.Falha($"Para confirmar, digite {TextoConfirmacaoLimpeza} (em maiúsculas) no campo de confirmação.");
        if (!await VerificarSenhaAdministradorAsync(senhaAdministrador, ct)) return Resultado<int>.Falha("Senha de administrador incorreta.");
        try
        {
            return Resultado<int>.Ok(await repositorio.LimparBancoAsync(admin.Id, ct));
        }
        catch (ViolacaoRegraException ex) { return Resultado<int>.Falha(ex.Message); }
    }
}
