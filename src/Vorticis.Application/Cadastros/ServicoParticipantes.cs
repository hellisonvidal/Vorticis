using Vorticis.Application.Seguranca;
using Vorticis.Domain.Cadastros;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Application.Cadastros;

/// <summary>Cliente e/ou fornecedor da empresa (participante da ECD, registro 0150).</summary>
public sealed record Participante(
    long Id, string Codigo, string Nome, string? NomeFantasia, string? CpfCnpj, string? InscricaoEstadual, string? Uf,
    string? CodMunicipioIbge, string? Municipio, string? Email, string? Telefone, bool Cliente, bool Fornecedor,
    string? CodigoConta, bool Ativo, bool TemMovimento = false, string? Cep = null, string? Logradouro = null, string? Numero = null,
    string? Complemento = null, string? Bairro = null, bool ControleConta = false);

public interface IRepositorioParticipantes
{
    Task<IReadOnlyList<Participante>> ListarAsync(long empresaId, CancellationToken ct);
    /// <summary>Inclui (Id = 0) ou altera. <paramref name="contaId"/> é a conta contábil já resolvida.</summary>
    Task<long> GravarAsync(long empresaId, Participante p, long? contaId, CancellationToken ct);
    Task ExcluirAsync(long id, CancellationToken ct);
    Task<bool> UsaParticipantesAsync(long empresaId, CancellationToken ct);
    /// <summary>Liga/desliga o plano com participantes; as contas dos participantes passam a exigir (ou não) o participante.</summary>
    Task DefinirUsaParticipantesAsync(long empresaId, bool usa, CancellationToken ct);
}

/// <summary>
/// Cadastros › Clientes e fornecedores. O código é numérico e sequencial por empresa; a mesma pessoa pode ser cliente
/// e fornecedor. Quem já aparece em lançamentos não é excluído.
/// </summary>
/// <summary>Participante gravado; <see cref="ContaCriada"/> é o código da conta aberta no plano de contas (null = nenhuma foi criada).</summary>
public sealed record ParticipanteGravado(long Id, string? ContaCriada);

public sealed class ServicoParticipantes(IRepositorioParticipantes repositorio, Contabil.IRepositorioPlanoContas plano, Autorizador autorizador)
{
    public async Task<IReadOnlyList<Participante>> ListarAsync(long empresaId, long usuarioId, CancellationToken ct = default)
        => (await autorizador.ObterAsync(usuarioId, empresaId, ct)).TemAcesso
            ? (await repositorio.ListarAsync(empresaId, ct)).OrderBy(p => p.Codigo.Length).ThenBy(p => p.Codigo, StringComparer.Ordinal).ToList()
            : [];

    /// <summary>A empresa optou pelo plano de contas com participantes (Cadastros › Empresas › Escrituração).</summary>
    public Task<bool> UsaParticipantesAsync(long empresaId, CancellationToken ct = default) => repositorio.UsaParticipantesAsync(empresaId, ct);

    public async Task<Resultado> DefinirUsaParticipantesAsync(long empresaId, bool usa, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.PlanoContasManter, ct) is { } negado) return Resultado.Falha(negado);
        try
        {
            await repositorio.DefinirUsaParticipantesAsync(empresaId, usa, ct);
            return Resultado.Ok();
        }
        catch (ViolacaoRegraException ex) { return Resultado.Falha(ex.Message); }
    }

    public static string ProximoCodigo(IEnumerable<Participante> existentes) =>
        Vorticis.Domain.Comum.Sequencia.MenorLivre(existentes.Select(p => p.Codigo)).ToString();

    public async Task<Resultado<long>> GravarAsync(long empresaId, Participante p, long usuarioId, CancellationToken ct = default)
    {
        var r = await GravarCriandoContaAsync(empresaId, p, false, usuarioId, ct);
        return r.Sucesso ? Resultado<long>.Ok(r.Valor.Id) : Resultado<long>.Falha(r.Erros);
    }

    /// <summary>
    /// Grava o participante e, com <paramref name="criarConta"/>, abre a conta analítica dele no plano de contas: sob a conta título de
    /// Clientes (ou de Fornecedores, se não for cliente) escolhida na configuração contábil, com o próximo código livre da máscara e o
    /// nome do participante, e liga a conta ao cadastro. Quem já tem conta ligada não ganha outra. A criação da conta exige a
    /// permissão de manter o plano de contas (ou de contabilização fiscal, no módulo Fiscal) e, se falhar, o participante não é gravado.
    /// </summary>
    public async Task<Resultado<ParticipanteGravado>> GravarCriandoContaAsync(long empresaId, Participante p, bool criarConta, long usuarioId,
                                                                             CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.LancamentoIncluir, ct) is { } negado) return Resultado<ParticipanteGravado>.Falha(negado);
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
        var cep = Cnpj.SomenteDigitos(p.Cep);
        if (cep.Length is not (0 or 8)) erros.Add("CEP deve ter 8 dígitos.");
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
        if (p.ControleConta && contaId is null && !erros.Any(e => e.StartsWith("Conta contábil")))
            erros.Add("Para alocar como participante, informe a conta contábil (ex.: Clientes ou Fornecedores).");
        if (p.ControleConta && !await repositorio.UsaParticipantesAsync(empresaId, ct))
            erros.Add("A empresa não usa o plano de contas com participantes (Cadastros › Empresas › Escrituração).");
        if (p.Id != 0 && lista.All(x => x.Id != p.Id)) erros.Add("Participante não encontrado.");
        if (erros.Count > 0) return Resultado<ParticipanteGravado>.Falha(erros);
        long? contaCriadaId = null;
        string? contaCriada = null;
        var jaLigado = contaId is not null || (p.Id != 0 && lista.FirstOrDefault(x => x.Id == p.Id)?.CodigoConta is { Length: > 0 });
        if (criarConta && !jaLigado)
        {
            var pa = await autorizador.ObterAsync(usuarioId, empresaId, ct);
            if (!pa.Pode(Permissao.PlanoContasManter) && !pa.Pode(Permissao.FiscalContabilizar))
                return Resultado<ParticipanteGravado>.Falha($"Seu perfil ({pa.NomePerfil}) não permite criar contas no plano de contas.");
            var nova = await CriarContaDoParticipanteAsync(empresaId, p, usuarioId, ct);
            if (!nova.Sucesso) return Resultado<ParticipanteGravado>.Falha(nova.Erros);
            (contaCriadaId, contaCriada) = (nova.Valor.Id, nova.Valor.Codigo);
            contaId = contaCriadaId;
            p = p with { CodigoConta = contaCriada };
        }
        static string? N(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        try
        {
            var id = await repositorio.GravarAsync(empresaId, p with
            {
                Codigo = codigo, Nome = p.Nome.Trim(), NomeFantasia = N(p.NomeFantasia), CpfCnpj = doc.Length == 0 ? null : doc,
                InscricaoEstadual = N(p.InscricaoEstadual)?.ToUpperInvariant(), Uf = N(p.Uf)?.ToUpperInvariant(),
                CodMunicipioIbge = N(p.CodMunicipioIbge), Municipio = N(p.Municipio), Email = N(p.Email), Telefone = N(p.Telefone),
                Cep = cep.Length == 0 ? null : cep, Logradouro = N(p.Logradouro), Numero = N(p.Numero), Complemento = N(p.Complemento),
                Bairro = N(p.Bairro),
            }, contaId, ct);
            return Resultado<ParticipanteGravado>.Ok(new ParticipanteGravado(id, contaCriada));
        }
        catch (ViolacaoRegraException ex)
        {
            if (contaCriadaId is { } criada) try { await plano.ExcluirAsync(criada, usuarioId, ct); } catch (ViolacaoRegraException) { /* a conta fica no plano */ }
            return Resultado<ParticipanteGravado>.Falha(ex.Message);
        }
    }

    /// <summary>Abre a conta analítica do participante sob a conta título (a configuração contábil da empresa define qual).</summary>
    private async Task<Resultado<(long Id, string Codigo)>> CriarContaDoParticipanteAsync(long empresaId, Participante p, long usuarioId, CancellationToken ct)
    {
        var cfg = await plano.ObterConfiguracaoAsync(empresaId, ct);
        var tituloId = p.Cliente ? cfg.ContaTituloClientesId : cfg.ContaTituloFornecedoresId;
        var rotulo = p.Cliente ? "Clientes" : "Fornecedores";
        if (tituloId is null)
            return Resultado<(long, string)>.Falha($"Para criar a conta no plano de contas, escolha antes a conta título de {rotulo} " +
                                                  "(Cadastros › Empresas › aba Contabilidade).");
        var contas = await plano.ListarAsync(empresaId, ct);
        var titulo = contas.FirstOrDefault(c => c.Id == tituloId);
        if (titulo is null || titulo.Analitica)
            return Resultado<(long, string)>.Falha($"A conta título de {rotulo} não existe mais ou deixou de ser sintética; escolha-a de novo na aba Contabilidade da empresa.");
        if (!titulo.Ativa) return Resultado<(long, string)>.Falha($"A conta título de {rotulo} ({titulo.Codigo}) está inativa.");
        var mascara = Domain.Contabil.MascaraPlano.Ler(cfg.MascaraPlano) is { Sucesso: true } m ? m.Valor : null;
        var codigo = Domain.Contabil.ConversaoPlano.ProximoCodigoFilho(titulo.Codigo, contas.Select(c => c.Codigo), mascara);
        if (!codigo.Sucesso) return Resultado<(long, string)>.Falha(codigo.Erros);
        var nome = p.Nome.Trim();
        var reduzido = Sequencia.MenorLivre(contas.Where(c => c.CodigoReduzido.HasValue).Select(c => c.CodigoReduzido!.Value));
        try
        {
            var id = await plano.IncluirAsync(empresaId, new Contabil.NovaConta(codigo.Valor, nome.Length > 200 ? nome[..200] : nome, titulo.CodNat,
                titulo.NaturezaSaldo, false, false, titulo.DataInclusao, reduzido), titulo.Id, (short)(titulo.Nivel + 1), true, null, usuarioId, ct);
            return Resultado<(long, string)>.Ok((id, codigo.Valor));
        }
        catch (ViolacaoRegraException ex) { return Resultado<(long, string)>.Falha($"Não foi possível criar a conta no plano: {ex.Message}"); }
    }

    public async Task<Resultado> ExcluirAsync(long empresaId, long id, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.LancamentoIncluir, ct) is { } negado) return Resultado.Falha(negado);
        var p = (await repositorio.ListarAsync(empresaId, ct)).FirstOrDefault(x => x.Id == id);
        if (p is null) return Resultado.Falha("Participante não encontrado.");
        if (p.TemMovimento) return Resultado.Falha($"{p.Nome} aparece em lançamentos e não pode ser excluído.");
        try
        {
            await repositorio.ExcluirAsync(id, ct);
            return Resultado.Ok();
        }
        catch (ViolacaoRegraException ex) { return Resultado.Falha(ex.Message); }
    }
}
