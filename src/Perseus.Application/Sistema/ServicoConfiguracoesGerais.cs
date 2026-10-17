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

    /// <summary>
    /// Limpa o banco de dados: exclui TODAS as empresas com a escrituração, contadores, escritórios, tabelas do plano
    /// referencial e layouts. Usuários, perfis e configurações ficam. Não recomendado: só o administrador, digitando LIMPAR.
    /// </summary>
    public async Task<Resultado<int>> LimparBancoAsync(SessaoUsuario admin, string confirmacao, CancellationToken ct = default)
    {
        if (!admin.Administrador) return Resultado<int>.Falha("Somente o administrador pode limpar o banco de dados.");
        if (confirmacao.Trim() != TextoConfirmacaoLimpeza)
            return Resultado<int>.Falha($"Para confirmar, digite {TextoConfirmacaoLimpeza} (em maiúsculas) no campo de confirmação.");
        try
        {
            return Resultado<int>.Ok(await repositorio.LimparBancoAsync(admin.Id, ct));
        }
        catch (ViolacaoRegraException ex) { return Resultado<int>.Falha(ex.Message); }
    }
}
