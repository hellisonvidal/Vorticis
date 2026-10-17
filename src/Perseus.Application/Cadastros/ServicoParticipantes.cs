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

/// <summary>Resultado da importação de planilha.</summary>
public sealed record ImportacaoParticipantes(int Incluidos, int Atualizados, IReadOnlyList<string> Erros);

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

    /// <summary>
    /// Importa clientes/fornecedores de planilha (Excel ou CSV). Colunas reconhecidas pelo nome: NOME (ou RAZAO_SOCIAL),
    /// CPF_CNPJ (CNPJ, CPF, DOCUMENTO), NOME_FANTASIA, IE, UF, MUNICIPIO, IBGE, EMAIL, TELEFONE, CODIGO. Quem já está cadastrado
    /// com o mesmo CPF/CNPJ passa a ser também cliente/fornecedor (os demais dados não são alterados).
    /// </summary>
    public async Task<Resultado<ImportacaoParticipantes>> ImportarAsync(long empresaId, TabelaImportada t, bool cliente, bool fornecedor, long usuarioId,
                                                                       CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<ImportacaoParticipantes>(usuarioId, empresaId, Permissao.LancamentoIncluir, ct) is { } negado) return negado;
        var iNome = t.Coluna("NOME", "RAZAO_SOCIAL", "NOME_RAZAO_SOCIAL", "RAZAO", "NOME_RAZAO", "CLIENTE", "FORNECEDOR");
        if (iNome < 0) return Resultado<ImportacaoParticipantes>.Falha($"A planilha precisa de uma coluna NOME ou RAZAO_SOCIAL (encontrado: {string.Join(", ", t.Colunas)}).");
        var iDoc = t.Coluna("CPF_CNPJ", "CNPJ_CPF", "CNPJ", "CPF", "DOCUMENTO", "CPF_CNPJ_");
        var iFant = t.Coluna("NOME_FANTASIA", "FANTASIA");
        var iIe = t.Coluna("IE", "INSCRICAO_ESTADUAL", "INSC_ESTADUAL");
        var iUf = t.Coluna("UF", "ESTADO");
        var iMun = t.Coluna("MUNICIPIO", "CIDADE");
        var iIbge = t.Coluna("IBGE", "COD_MUNICIPIO", "CODIGO_IBGE", "COD_IBGE");
        var iEmail = t.Coluna("EMAIL", "E_MAIL");
        var iTel = t.Coluna("TELEFONE", "FONE", "CELULAR");
        var iCod = t.Coluna("CODIGO", "COD");
        string? V(int linha, int col) => col >= 0 && t.Valor(linha, col) is { Length: > 0 } v ? v : null;

        int incluidos = 0, atualizados = 0;
        var erros = new List<string>();
        for (var i = 0; i < t.Linhas.Count; i++)
        {
            var nome = V(i, iNome);
            if (nome is null) continue;
            var lista = await repositorio.ListarAsync(empresaId, ct);
            var doc = Cnpj.SomenteDigitos(V(i, iDoc));
            if (doc.Length is > 0 and < 11) doc = doc.PadLeft(11, '0');           // CPF com zeros à esquerda perdidos no Excel
            if (doc.Length is > 11 and < 14) doc = doc.PadLeft(14, '0');
            if (doc.Length > 0 && lista.FirstOrDefault(x => x.CpfCnpj == doc) is { } existente)
            {
                if ((cliente && !existente.Cliente) || (fornecedor && !existente.Fornecedor))
                {
                    var r = await GravarAsync(empresaId, existente with { Cliente = existente.Cliente || cliente, Fornecedor = existente.Fornecedor || fornecedor },
                                              usuarioId, ct);
                    if (r.Sucesso) atualizados++;
                    else erros.Add($"Linha {t.NumerosLinha[i]} ({nome}): {string.Join(" ", r.Erros)}");
                }
                continue;
            }
            var novo = new Participante(0, V(i, iCod) ?? "", nome, V(i, iFant), doc.Length == 0 ? null : doc, V(i, iIe), V(i, iUf), V(i, iIbge),
                                        V(i, iMun), V(i, iEmail), V(i, iTel), cliente, fornecedor, null, true);
            if (novo.Codigo.Length > 0 && lista.Any(x => x.Codigo == novo.Codigo)) novo = novo with { Codigo = "" };
            var g = await GravarAsync(empresaId, novo, usuarioId, ct);
            if (g.Sucesso) incluidos++;
            else erros.Add($"Linha {t.NumerosLinha[i]} ({nome}): {string.Join(" ", g.Erros)}");
        }
        return Resultado<ImportacaoParticipantes>.Ok(new ImportacaoParticipantes(incluidos, atualizados, erros));
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
