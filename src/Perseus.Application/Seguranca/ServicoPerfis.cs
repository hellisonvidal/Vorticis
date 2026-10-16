using Perseus.Domain.Comum;
using Perseus.Domain.Seguranca;

namespace Perseus.Application.Seguranca;

public sealed record PerfilAcesso(long Id, string Nome, string? Descricao, bool Padrao, IReadOnlySet<Permissao> Permissoes, int Usuarios);

public interface IRepositorioPerfis
{
    Task<IReadOnlyList<PerfilAcesso>> ListarAsync(CancellationToken ct);
    Task<bool> NomeEmUsoAsync(string nome, long? excetoId, CancellationToken ct);
    Task<long> CriarAsync(string nome, string? descricao, IReadOnlySet<Permissao> permissoes, CancellationToken ct);
    Task AtualizarAsync(long id, string nome, string? descricao, IReadOnlySet<Permissao> permissoes, CancellationToken ct);
    /// <summary>Lança <c>ViolacaoRegraException</c> se o perfil for padrão ou estiver em uso.</summary>
    Task ExcluirAsync(long id, CancellationToken ct);
}

/// <summary>Configurações › Perfis de acesso. Somente administradores.</summary>
public sealed class ServicoPerfis(IRepositorioPerfis repositorio)
{
    public async Task<IReadOnlyList<PerfilAcesso>> ListarAsync(SessaoUsuario sessao, CancellationToken ct = default)
        => sessao.Administrador ? await repositorio.ListarAsync(ct) : [];

    public async Task<Resultado<long>> SalvarAsync(SessaoUsuario admin, long? id, string nome, string? descricao,
                                                  IEnumerable<Permissao> permissoes, CancellationToken ct = default)
    {
        if (!admin.Administrador) return Resultado<long>.Falha("Somente administradores definem perfis.");
        nome = nome.Trim();
        var erros = new List<string>();
        if (nome.Length is < 2 or > 60) erros.Add("O nome do perfil deve ter de 2 a 60 caracteres.");
        var conjunto = permissoes.Where(p => p != Permissao.AdministrarSistema).ToHashSet();
        if (conjunto.Count == 0) erros.Add("Marque ao menos uma função para o perfil.");
        if (erros.Count == 0 && await repositorio.NomeEmUsoAsync(nome, id, ct)) erros.Add($"Já existe um perfil chamado \"{nome}\".");
        if (erros.Count > 0) return Resultado<long>.Falha(erros);

        var desc = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        if (id is long existente)
        {
            await repositorio.AtualizarAsync(existente, nome, desc, conjunto, ct);
            return Resultado<long>.Ok(existente);
        }
        return Resultado<long>.Ok(await repositorio.CriarAsync(nome, desc, conjunto, ct));
    }

    public async Task<Resultado> ExcluirAsync(SessaoUsuario admin, long id, CancellationToken ct = default)
    {
        if (!admin.Administrador) return Resultado.Falha("Somente administradores excluem perfis.");
        try
        {
            await repositorio.ExcluirAsync(id, ct);
            return Resultado.Ok();
        }
        catch (ViolacaoRegraException ex)
        {
            return Resultado.Falha(ex.Message);
        }
    }
}
