using Perseus.Application.Seguranca;
using Perseus.Domain.Cadastros;
using Perseus.Domain.Comum;
using Perseus.Domain.Seguranca;

namespace Perseus.Application.Cadastros;

/// <summary>Cliente e/ou fornecedor da empresa (participante da ECD, registro 0150).</summary>
public sealed record Participante(
    long Id, string Codigo, string Nome, string? NomeFantasia, string? CpfCnpj, string? InscricaoEstadual, string? Uf,
    string? CodMunicipioIbge, string? Municipio, string? Email, string? Telefone, bool Cliente, bool Fornecedor,
    string? CodigoConta, bool Ativo, bool TemMovimento = false);

public interface IRepositorioParticipantes
{
    Task<IReadOnlyList<Participante>> ListarAsync(long empresaId, CancellationToken ct);
    /// <summary>Inclui (Id = 0) ou altera. <paramref name="contaId"/> é a conta contábil já resolvida.</summary>
    Task<long> GravarAsync(long empresaId, Participante p, long? contaId, CancellationToken ct);
    Task ExcluirAsync(long id, CancellationToken ct);
}

/// <summary>
/// Cadastros › Clientes e fornecedores. O código é numérico e sequencial por empresa; a mesma pessoa pode ser cliente
/// e fornecedor. Quem já aparece em lançamentos não é excluído (inative-o).
/// </summary>
public sealed class ServicoParticipantes(IRepositorioParticipantes repositorio, Contabil.IRepositorioPlanoContas plano, Autorizador autorizador)
{
    public async Task<IReadOnlyList<Participante>> ListarAsync(long empresaId, long usuarioId, CancellationToken ct = default)
        => (await autorizador.ObterAsync(usuarioId, empresaId, ct)).TemAcesso
            ? (await repositorio.ListarAsync(empresaId, ct)).OrderBy(p => p.Codigo.Length).ThenBy(p => p.Codigo, StringComparer.Ordinal).ToList()
            : [];

    public static string ProximoCodigo(IEnumerable<Participante> existentes) =>
        ((existentes.Select(p => int.TryParse(p.Codigo, out var n) ? n : 0).DefaultIfEmpty(0).Max()) + 1).ToString();

    public async Task<Resultado<long>> GravarAsync(long empresaId, Participante p, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<long>(usuarioId, empresaId, Permissao.LancamentoIncluir, ct) is { } negado) return negado;
        var lista = await repositorio.ListarAsync(empresaId, ct);
        var codigo = string.IsNullOrWhiteSpace(p.Codigo) ? ProximoCodigo(lista) : p.Codigo.Trim();
        var doc = Cnpj.SomenteDigitos(p.CpfCnpj);
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(p.Nome)) erros.Add("Informe o nome ou a razão social.");
        if (!p.Cliente && !p.Fornecedor) erros.Add("Marque se é cliente, fornecedor ou ambos.");
        if (doc.Length > 0 && !(doc.Length == 11 ? Cpf.Valido(doc) : doc.Length == 14 && Cnpj.Valido(doc))) erros.Add("CPF ou CNPJ inválido.");
        if (!string.IsNullOrWhiteSpace(p.Uf) && !Uf.Valida(p.Uf)) erros.Add("UF inválida.");
        if (!string.IsNullOrWhiteSpace(p.CodMunicipioIbge) && (p.CodMunicipioIbge.Trim().Length != 7 || !p.CodMunicipioIbge.Trim().All(char.IsAsciiDigit)))
            erros.Add("Código IBGE do município deve ter 7 dígitos.");
        if (codigo.Length > 60) erros.Add("Código muito longo.");
        if (lista.Any(x => x.Id != p.Id && x.Codigo == codigo)) erros.Add($"Já existe o participante de código {codigo}.");
        if (doc.Length > 0 && lista.FirstOrDefault(x => x.Id != p.Id && x.CpfCnpj == doc) is { } mesmo)
            erros.Add($"Este CPF/CNPJ já está cadastrado (código {mesmo.Codigo} – {mesmo.Nome}).");
        long? contaId = null;
        if (!string.IsNullOrWhiteSpace(p.CodigoConta))
        {
            var conta = (await plano.ListarAsync(empresaId, ct)).FirstOrDefault(c => c.Codigo == p.CodigoConta.Trim()
                                                                                   || c.CodigoReduzido?.ToString() == p.CodigoConta.Trim());
            if (conta is null || !conta.Analitica) erros.Add($"Conta contábil {p.CodigoConta} não encontrada entre as analíticas.");
            else contaId = conta.Id;
        }
        if (p.Id != 0 && lista.All(x => x.Id != p.Id)) erros.Add("Participante não encontrado.");
        if (erros.Count > 0) return Resultado<long>.Falha(erros);
        static string? N(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        try
        {
            return Resultado<long>.Ok(await repositorio.GravarAsync(empresaId, p with
            {
                Codigo = codigo, Nome = p.Nome.Trim(), NomeFantasia = N(p.NomeFantasia), CpfCnpj = doc.Length == 0 ? null : doc,
                InscricaoEstadual = N(p.InscricaoEstadual)?.ToUpperInvariant(), Uf = N(p.Uf)?.ToUpperInvariant(),
                CodMunicipioIbge = N(p.CodMunicipioIbge), Municipio = N(p.Municipio), Email = N(p.Email), Telefone = N(p.Telefone),
            }, contaId, ct));
        }
        catch (ViolacaoRegraException ex) { return Resultado<long>.Falha(ex.Message); }
    }

    public async Task<Resultado> ExcluirAsync(long empresaId, long id, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.LancamentoIncluir, ct) is { } negado) return Resultado.Falha(negado);
        var p = (await repositorio.ListarAsync(empresaId, ct)).FirstOrDefault(x => x.Id == id);
        if (p is null) return Resultado.Falha("Participante não encontrado.");
        if (p.TemMovimento) return Resultado.Falha($"{p.Nome} aparece em lançamentos e não pode ser excluído; use Alterar e desmarque Ativo.");
        try
        {
            await repositorio.ExcluirAsync(id, ct);
            return Resultado.Ok();
        }
        catch (ViolacaoRegraException ex) { return Resultado.Falha(ex.Message); }
    }
}
