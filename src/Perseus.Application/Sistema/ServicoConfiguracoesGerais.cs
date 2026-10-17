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

public sealed record ConfiguracoesGerais(ModoLancamento ModoLancamento);

/// <summary>Configurações › Configurações gerais (valem para todos os usuários e empresas).</summary>
public sealed class ServicoConfiguracoesGerais(IRepositorioConfiguracao repositorio)
{
    private const string ChaveModo = "modo_lancamento";

    public async Task<ConfiguracoesGerais> ObterAsync(CancellationToken ct = default)
    {
        var v = await repositorio.LerAsync([ChaveModo], ct);
        var modo = v.TryGetValue(ChaveModo, out var m) && Enum.TryParse<ModoLancamento>(m.Valor, out var lido) ? lido : ModoLancamento.PartidaDobrada;
        return new ConfiguracoesGerais(modo);
    }

    public async Task<Resultado> SalvarAsync(SessaoUsuario admin, ConfiguracoesGerais c, CancellationToken ct = default)
    {
        if (!admin.Administrador) return Resultado.Falha("Somente o administrador altera as configurações gerais.");
        await repositorio.GravarAsync(new Dictionary<string, string?> { [ChaveModo] = c.ModoLancamento.ToString() }, admin.Id, ct);
        return Resultado.Ok();
    }
}
