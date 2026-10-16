using Perseus.Application.Seguranca;
using Perseus.Domain.Cadastros;
using Perseus.Domain.Comum;
using Perseus.Domain.Contabil;
using Perseus.Domain.Seguranca;

namespace Perseus.Application.Cadastros;

public sealed record Contador(long Id, string Nome, string Cpf, string Crc, string UfCrc, string? Email, string? Telefone, long? EscritorioId)
{
    /// <summary>CRC como impresso nos termos: "UF-número", sem repetir a UF quando ela já faz parte do número (ex.: 1SP123456/O-7).</summary>
    public string CrcCompleto => Crc.Contains(UfCrc, StringComparison.OrdinalIgnoreCase) ? Crc : $"{UfCrc}-{Crc}";
}

public sealed record EscritorioContabil(long Id, string Cnpj, string RazaoSocial, string? Crc, string? UfCrc, string? Email, string? Telefone);

public sealed record VinculoContabil(long Id, long? ContadorId, long? EscritorioId, string Nome, string Documento, string? Crc,
                                     DateOnly Inicio, DateOnly? Fim, bool ResponsavelTecnico)
{
    public bool Vigente => Fim is null;
    public string Tipo => ContadorId is not null ? "Contador" : "Escritório contábil";
}

public sealed record Socio(long Id, string Nome, char TipoPessoa, string? CpfCnpj, string? DocumentoMascarado, string Qualificacao,
                           int? QualificacaoCodigo, decimal? Participacao, DateOnly? DataEntrada, DateOnly? DataSaida,
                           bool RepresentanteLegal, string? Email, string? Telefone, string Origem = "MANUAL")
{
    public bool Ativo => DataSaida is null;
}

public interface IRepositorioResponsaveis
{
    Task<IReadOnlyList<Contador>> ListarContadoresAsync(CancellationToken ct);
    Task<IReadOnlyList<EscritorioContabil>> ListarEscritoriosAsync(CancellationToken ct);
    /// <summary>Insere ou atualiza pelo CPF; devolve o id.</summary>
    Task<long> SalvarContadorAsync(Contador c, CancellationToken ct);
    /// <summary>Insere ou atualiza pelo CNPJ; devolve o id.</summary>
    Task<long> SalvarEscritorioAsync(EscritorioContabil e, CancellationToken ct);

    Task<IReadOnlyList<VinculoContabil>> ListarVinculosAsync(long empresaId, CancellationToken ct);
    Task<long> IncluirVinculoAsync(long empresaId, long? contadorId, long? escritorioId, DateOnly inicio, bool responsavel, long usuarioId, CancellationToken ct);
    Task EncerrarVinculoAsync(long empresaId, long vinculoId, DateOnly fim, long usuarioId, CancellationToken ct);
    Task DefinirResponsavelAsync(long empresaId, long vinculoId, long usuarioId, CancellationToken ct);
    Task ExcluirVinculoAsync(long empresaId, long vinculoId, long usuarioId, CancellationToken ct);

    Task<IReadOnlyList<Socio>> ListarSociosAsync(long empresaId, CancellationToken ct);
    Task<long> SalvarSocioAsync(long empresaId, Socio s, long usuarioId, CancellationToken ct);
    Task ExcluirSocioAsync(long empresaId, long socioId, long usuarioId, CancellationToken ct);
}

/// <summary>
/// Contabilidade responsável (contadores e escritórios, com vigência e responsável técnico) e quadro societário.
/// São os signatários da ECD/ECF (registros J930/0930: contador com CRC e representante legal) e dos termos do Diário.
/// </summary>
public sealed class ServicoResponsaveis(IRepositorioResponsaveis repositorio, IRepositorioEmpresa empresas, Autorizador autorizador,
                                       IConsultaCnpj? consultaCnpj = null)
{
    // ------------------------------------------------------------------ leitura

    public async Task<Resultado<IReadOnlyList<VinculoContabil>>> ListarVinculosAsync(long empresaId, long usuarioId, CancellationToken ct = default)
        => (await autorizador.ObterAsync(usuarioId, empresaId, ct)).TemAcesso
            ? Resultado<IReadOnlyList<VinculoContabil>>.Ok(await repositorio.ListarVinculosAsync(empresaId, ct))
            : Resultado<IReadOnlyList<VinculoContabil>>.Falha("Você não tem acesso a esta empresa.");

    public async Task<Resultado<IReadOnlyList<Socio>>> ListarSociosAsync(long empresaId, long usuarioId, CancellationToken ct = default)
        => (await autorizador.ObterAsync(usuarioId, empresaId, ct)).TemAcesso
            ? Resultado<IReadOnlyList<Socio>>.Ok(await repositorio.ListarSociosAsync(empresaId, ct))
            : Resultado<IReadOnlyList<Socio>>.Falha("Você não tem acesso a esta empresa.");

    public Task<IReadOnlyList<Contador>> ListarContadoresAsync(CancellationToken ct = default) => repositorio.ListarContadoresAsync(ct);
    public Task<IReadOnlyList<EscritorioContabil>> ListarEscritoriosAsync(CancellationToken ct = default) => repositorio.ListarEscritoriosAsync(ct);

    /// <summary>Contador responsável técnico vigente (assina a ECD e os termos do Diário).</summary>
    public async Task<Contador?> ResponsavelTecnicoAsync(long empresaId, CancellationToken ct = default)
    {
        var v = (await repositorio.ListarVinculosAsync(empresaId, ct)).FirstOrDefault(x => x.Vigente && x.ResponsavelTecnico);
        return v?.ContadorId is long id ? (await repositorio.ListarContadoresAsync(ct)).FirstOrDefault(c => c.Id == id) : null;
    }

    public async Task<Socio?> RepresentanteLegalAsync(long empresaId, CancellationToken ct = default)
        => (await repositorio.ListarSociosAsync(empresaId, ct)).FirstOrDefault(s => s.Ativo && s.RepresentanteLegal);

    // ------------------------------------------------------------------ contabilidade

    public async Task<Resultado<long>> VincularContadorAsync(long empresaId, Contador dados, DateOnly inicio, bool responsavelTecnico, long usuarioId,
                                                            CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<long>(usuarioId, empresaId, Permissao.EmpresaResponsaveisManter, ct) is { } negado) return negado;
        var cpf = Cnpj.SomenteDigitos(dados.Cpf);
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(dados.Nome)) erros.Add("Informe o nome do contador.");
        if (!Cpf.Valido(cpf)) erros.Add("CPF do contador inválido.");
        if (string.IsNullOrWhiteSpace(dados.Crc)) erros.Add("Informe o número do CRC.");
        if (!Uf.Valida(dados.UfCrc)) erros.Add("UF do CRC inválida.");
        if (erros.Count > 0) return Resultado<long>.Falha(erros);

        var vinculos = await repositorio.ListarVinculosAsync(empresaId, ct);
        var contadorId = await repositorio.SalvarContadorAsync(dados with
        {
            Nome = dados.Nome.Trim(), Cpf = cpf, Crc = dados.Crc.Trim().ToUpperInvariant(), UfCrc = dados.UfCrc.Trim().ToUpperInvariant(),
            Email = Limpar(dados.Email)?.ToLowerInvariant(), Telefone = Limpar(dados.Telefone),
        }, ct);
        if (vinculos.Any(v => v.Vigente && v.ContadorId == contadorId))
            return Resultado<long>.Falha("Este contador já está vinculado à empresa (dados cadastrais atualizados).");
        if (responsavelTecnico && vinculos.FirstOrDefault(v => v.Vigente && v.ResponsavelTecnico) is { } atual)
            return Resultado<long>.Falha($"{atual.Nome} já é o responsável técnico vigente. Encerre o vínculo dele ou vincule sem marcar como responsável.");
        return await Gravar(() => repositorio.IncluirVinculoAsync(empresaId, contadorId, null, inicio, responsavelTecnico, usuarioId, ct));
    }

    public async Task<Resultado<long>> VincularEscritorioAsync(long empresaId, EscritorioContabil dados, DateOnly inicio, long usuarioId,
                                                              CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<long>(usuarioId, empresaId, Permissao.EmpresaResponsaveisManter, ct) is { } negado) return negado;
        var cnpj = new string(dados.Cnpj.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        var erros = new List<string>();
        if (!Cnpj.Valido(cnpj)) erros.Add("CNPJ do escritório inválido.");
        if (string.IsNullOrWhiteSpace(dados.RazaoSocial)) erros.Add("Informe a razão social do escritório.");
        if (!string.IsNullOrWhiteSpace(dados.UfCrc) && !Uf.Valida(dados.UfCrc)) erros.Add("UF do CRC inválida.");
        if (erros.Count > 0) return Resultado<long>.Falha(erros);

        var id = await repositorio.SalvarEscritorioAsync(dados with
        {
            Cnpj = cnpj, RazaoSocial = dados.RazaoSocial.Trim(), Crc = Limpar(dados.Crc)?.ToUpperInvariant(), UfCrc = Limpar(dados.UfCrc)?.ToUpperInvariant(),
            Email = Limpar(dados.Email)?.ToLowerInvariant(), Telefone = Limpar(dados.Telefone),
        }, ct);
        if ((await repositorio.ListarVinculosAsync(empresaId, ct)).Any(v => v.Vigente && v.EscritorioId == id))
            return Resultado<long>.Falha("Este escritório já está vinculado à empresa (dados cadastrais atualizados).");
        return await Gravar(() => repositorio.IncluirVinculoAsync(empresaId, null, id, inicio, false, usuarioId, ct));
    }

    public async Task<Resultado> EncerrarVinculoAsync(long empresaId, long vinculoId, DateOnly fim, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.EmpresaResponsaveisManter, ct) is { } negado) return Resultado.Falha(negado);
        var v = (await repositorio.ListarVinculosAsync(empresaId, ct)).FirstOrDefault(x => x.Id == vinculoId);
        if (v is null) return Resultado.Falha("Vínculo não encontrado.");
        if (!v.Vigente) return Resultado.Falha("O vínculo já está encerrado.");
        if (fim < v.Inicio) return Resultado.Falha($"A data de encerramento não pode ser anterior ao início ({v.Inicio:dd/MM/yyyy}).");
        return await GravarSemValor(() => repositorio.EncerrarVinculoAsync(empresaId, vinculoId, fim, usuarioId, ct));
    }

    public async Task<Resultado> DefinirResponsavelAsync(long empresaId, long vinculoId, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.EmpresaResponsaveisManter, ct) is { } negado) return Resultado.Falha(negado);
        var v = (await repositorio.ListarVinculosAsync(empresaId, ct)).FirstOrDefault(x => x.Id == vinculoId);
        if (v is null) return Resultado.Falha("Vínculo não encontrado.");
        if (v.ContadorId is null) return Resultado.Falha("O responsável técnico deve ser um contador (pessoa física com CRC).");
        if (!v.Vigente) return Resultado.Falha("O vínculo está encerrado.");
        return await GravarSemValor(() => repositorio.DefinirResponsavelAsync(empresaId, vinculoId, usuarioId, ct));
    }

    public async Task<Resultado> ExcluirVinculoAsync(long empresaId, long vinculoId, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.EmpresaResponsaveisManter, ct) is { } negado) return Resultado.Falha(negado);
        return await GravarSemValor(() => repositorio.ExcluirVinculoAsync(empresaId, vinculoId, usuarioId, ct));
    }

    public async Task<Resultado<DadosCnpj>> ConsultarCnpjAsync(string cnpj, CancellationToken ct = default)
    {
        var c = new string(cnpj.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        if (!Cnpj.Valido(c)) return Resultado<DadosCnpj>.Falha("CNPJ inválido.");
        if (consultaCnpj is null) return Resultado<DadosCnpj>.Falha("Consulta de CNPJ não configurada.");
        return await consultaCnpj.ConsultarAsync(c, ct);
    }

    // ------------------------------------------------------------------ sócios

    public async Task<Resultado<long>> SalvarSocioAsync(long empresaId, Socio s, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<long>(usuarioId, empresaId, Permissao.EmpresaResponsaveisManter, ct) is { } negado) return negado;
        var doc = s.TipoPessoa == 'J'
            ? new string((s.CpfCnpj ?? "").Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray())
            : Cnpj.SomenteDigitos(s.CpfCnpj);
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(s.Nome)) erros.Add("Informe o nome do sócio.");
        if (string.IsNullOrWhiteSpace(s.Qualificacao)) erros.Add("Informe a qualificação (ex.: Sócio-Administrador).");
        if (s.TipoPessoa is not ('F' or 'J' or 'E')) erros.Add("Tipo de pessoa inválido.");
        if (doc.Length > 0 && s.TipoPessoa == 'F' && !Cpf.Valido(doc)) erros.Add("CPF do sócio inválido.");
        if (doc.Length > 0 && s.TipoPessoa == 'J' && !Cnpj.Valido(doc)) erros.Add("CNPJ do sócio inválido.");
        if (s.Participacao is < 0 or > 100) erros.Add("Participação deve estar entre 0 e 100%.");
        if (s.DataEntrada is { } e && s.DataSaida is { } sa && sa < e) erros.Add("A data de saída não pode ser anterior à de entrada.");
        if (s.RepresentanteLegal && s.TipoPessoa == 'F' && doc.Length == 0)
            erros.Add("Informe o CPF do representante legal (exigido na assinatura da ECD e dos termos).");
        if (erros.Count > 0) return Resultado<long>.Falha(erros);

        var outros = (await repositorio.ListarSociosAsync(empresaId, ct)).Where(x => x.Id != s.Id && x.Ativo).ToArray();
        var soma = outros.Sum(x => x.Participacao ?? 0) + (s.DataSaida is null ? s.Participacao ?? 0 : 0);
        if (soma > 100) return Resultado<long>.Falha($"A soma das participações dos sócios ativos ficaria em {soma:0.####}% (máximo 100%).");

        return await Gravar(() => repositorio.SalvarSocioAsync(empresaId, s with
        {
            Nome = s.Nome.Trim(), CpfCnpj = doc.Length == 0 ? null : doc, Qualificacao = s.Qualificacao.Trim(),
            Email = Limpar(s.Email)?.ToLowerInvariant(), Telefone = Limpar(s.Telefone),
        }, usuarioId, ct));
    }

    public async Task<Resultado> ExcluirSocioAsync(long empresaId, long socioId, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.EmpresaResponsaveisManter, ct) is { } negado) return Resultado.Falha(negado);
        return await GravarSemValor(() => repositorio.ExcluirSocioAsync(empresaId, socioId, usuarioId, ct));
    }

    /// <summary>Busca o QSA da empresa na base pública da RFB e inclui os sócios que ainda não estão cadastrados.</summary>
    public async Task<Resultado<int>> ImportarDaReceitaAsync(long empresaId, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<int>(usuarioId, empresaId, Permissao.EmpresaResponsaveisManter, ct) is { } negado) return negado;
        var empresa = await empresas.ObterAsync(empresaId, ct);
        if (empresa is null) return Resultado<int>.Falha("Empresa não encontrada.");
        var dados = await ConsultarCnpjAsync(empresa.Cnpj, ct);
        if (!dados.Sucesso) return Resultado<int>.Falha(dados.Erros);
        return await IncluirDoQsaAsync(empresaId, dados.Valor.QuadroSocietario, dados.Valor.Fonte, usuarioId, ct);
    }

    /// <summary>Inclui sócios vindos de uma consulta já feita (ex.: no cadastro da empresa), sem duplicar pelo nome.</summary>
    public async Task<Resultado<int>> IncluirDoQsaAsync(long empresaId, IReadOnlyList<SocioCnpj> qsa, string fonte, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<int>(usuarioId, empresaId, Permissao.EmpresaResponsaveisManter, ct) is { } negado) return negado;
        var existentes = (await repositorio.ListarSociosAsync(empresaId, ct)).Select(s => LeitorPlanoReferencial.Normalizar(s.Nome)).ToHashSet();
        var temRepresentante = (await repositorio.ListarSociosAsync(empresaId, ct)).Any(s => s.Ativo && s.RepresentanteLegal);
        int incluidos = 0;
        foreach (var q in qsa.Where(q => !existentes.Contains(LeitorPlanoReferencial.Normalizar(q.Nome))))
        {
            await repositorio.SalvarSocioAsync(empresaId, new Socio(0, q.Nome.Trim(), q.TipoPessoa, null, q.DocumentoMascarado,
                q.Qualificacao ?? "Sócio", q.QualificacaoCodigo, null, q.DataEntrada, null, false, null, null,
                fonte.Length > 40 ? fonte[..40] : fonte), usuarioId, ct);
            incluidos++;
        }
        return Resultado<int>.Ok(incluidos);
    }

    private static string? Limpar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static async Task<Resultado<long>> Gravar(Func<Task<long>> acao)
    {
        try { return Resultado<long>.Ok(await acao()); }
        catch (ViolacaoRegraException ex) { return Resultado<long>.Falha(ex.Message); }
    }

    private static async Task<Resultado> GravarSemValor(Func<Task> acao)
    {
        try { await acao(); return Resultado.Ok(); }
        catch (ViolacaoRegraException ex) { return Resultado.Falha(ex.Message); }
    }
}
