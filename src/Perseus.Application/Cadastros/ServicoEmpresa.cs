using Perseus.Domain.Cadastros;
using Perseus.Domain.Comum;
using Perseus.Domain.Contabil;

namespace Perseus.Application.Cadastros;

public enum RegimeTributario { MEI, SimplesNacional, LucroPresumido, LucroReal, LucroArbitrado, ImuneIsenta }

public sealed record NovaEmpresa(
    string Cnpj,
    string RazaoSocial,
    string? NomeFantasia,
    string NaturezaJuridica,
    string CnaePrincipal,
    string Uf,
    string CodMunicipioIbge,
    DateOnly DataInicioAtividades,
    RegimeTributario Regime,
    int AnoPrimeiroExercicio,
    bool ImportarPlanoModelo = true,
    ComplementoEmpresa? Complemento = null,
    string? InscricaoEstadual = null);

/// <summary>Alteração do cadastro (o CNPJ não muda; o regime tem histórico próprio).</summary>
public sealed record AlteracaoEmpresa(
    long Id,
    string RazaoSocial,
    string? NomeFantasia,
    string NaturezaJuridica,
    string CnaePrincipal,
    string Uf,
    string CodMunicipioIbge,
    DateOnly DataInicioAtividades,
    string? InscricaoEstadual,
    ComplementoEmpresa? Complemento = null);

/// <summary>Endereço, contato e rastreabilidade da origem dos dados cadastrais.</summary>
public sealed record ComplementoEmpresa(
    string? Cep = null,
    string? Logradouro = null,
    string? Numero = null,
    string? Complemento = null,
    string? Bairro = null,
    string? Municipio = null,
    string? Email = null,
    string? Telefone = null,
    string? SituacaoCadastral = null,
    string? FonteDados = null,
    DateTimeOffset? DadosConsultadosEm = null);

public sealed record EmpresaResumo(long Id, string Cnpj, string RazaoSocial);

public sealed record EmpresaDetalhe(long Id, string Cnpj, string RazaoSocial, string? Nire, string Uf, string CodMunicipioIbge, string? Municipio,
                                    string? Cep, string? Logradouro, string? Numero, string? Complemento, string? Bairro,
                                    string? NomeFantasia = null, string NaturezaJuridica = "", string CnaePrincipal = "",
                                    DateOnly? DataInicioAtividades = null, string? InscricaoEstadual = null, string? Email = null,
                                    string? Telefone = null, RegimeTributario? Regime = null, string? SituacaoCadastral = null,
                                    string? FonteDados = null);

/// <summary>Conta do plano modelo. Tipo, nível e pai derivam do código (máscara 9.9.99.999).</summary>
public sealed record ContaModelo(string Codigo, string Descricao, string CodNat, IndicadorDC NaturezaSaldo);

public interface IRepositorioEmpresa
{
    Task<bool> CnpjExisteAsync(string cnpj, CancellationToken ct);
    /// <summary>Cria empresa, regime, exercício com 12 períodos e (opcional) o plano de contas, numa transação.</summary>
    Task<long> CriarAsync(NovaEmpresa empresa, IReadOnlyList<ContaModelo> plano, CancellationToken ct);
    Task CriarExercicioAsync(long empresaId, int ano, CancellationToken ct);
    Task<IReadOnlyList<EmpresaResumo>> ListarAsync(CancellationToken ct);
    Task<EmpresaDetalhe?> ObterAsync(long id, CancellationToken ct);
    Task AtualizarAsync(AlteracaoEmpresa e, CancellationToken ct);
    Task AtualizarInscricaoEstadualAsync(long id, string? inscricao, CancellationToken ct);
    /// <summary>Exclusão lógica: a empresa deixa de aparecer e de ser acessível; a escrituração é mantida (guarda legal).</summary>
    Task DesativarAsync(long id, CancellationToken ct);
}

public interface IFontePlanoModelo
{
    IReadOnlyList<ContaModelo> Obter();
}

public sealed class ServicoEmpresa(IRepositorioEmpresa repositorio, IFontePlanoModelo planoModelo, IConsultaCnpj? consultaCnpj = null,
                                   IConsultaInscricaoEstadual? consultaIe = null)
{
    /// <summary>Consulta os dados cadastrais do CNPJ na fonte externa configurada (para pré-preencher o cadastro).</summary>
    public async Task<Resultado<DadosCnpj>> ConsultarCnpjAsync(string cnpj, CancellationToken ct = default)
    {
        var c = new string(cnpj.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        if (!Cnpj.Valido(c)) return Resultado<DadosCnpj>.Falha("CNPJ inválido. Confira os dígitos antes de consultar.");
        if (consultaCnpj is null) return Resultado<DadosCnpj>.Falha("Consulta de CNPJ não configurada.");
        return await consultaCnpj.ConsultarAsync(c, ct);
    }

    public async Task<Resultado<long>> CadastrarAsync(NovaEmpresa e, CancellationToken ct = default)
    {
        var erros = new List<string>();
        var cnpj = new string(e.Cnpj.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        if (!Cnpj.Valido(cnpj)) erros.Add("CNPJ inválido.");
        if (string.IsNullOrWhiteSpace(e.RazaoSocial)) erros.Add("Razão social obrigatória.");
        if (e.NaturezaJuridica.Length != 4 || !e.NaturezaJuridica.All(char.IsAsciiDigit)) erros.Add("Natureza jurídica deve ter 4 dígitos (tabela CONCLA).");
        var cnae = Cnpj.SomenteDigitos(e.CnaePrincipal);
        if (cnae.Length != 7) erros.Add("CNAE deve ter 7 dígitos.");
        if (e.Uf.Length != 2) erros.Add("UF inválida.");
        if (e.CodMunicipioIbge.Length != 7 || !e.CodMunicipioIbge.All(char.IsAsciiDigit)) erros.Add("Código de município IBGE deve ter 7 dígitos.");
        if (e.AnoPrimeiroExercicio < e.DataInicioAtividades.Year) erros.Add("O primeiro exercício não pode ser anterior ao início das atividades.");
        var cep = Cnpj.SomenteDigitos(e.Complemento?.Cep);
        if (cep.Length is not (0 or 8)) erros.Add("CEP deve ter 8 dígitos.");
        if (!Domain.Cadastros.InscricaoEstadual.Valida(e.InscricaoEstadual)) erros.Add("Inscrição estadual deve ter de 2 a 14 dígitos ou ISENTO.");
        if (erros.Count > 0) return Resultado<long>.Falha(erros);

        if (await repositorio.CnpjExisteAsync(cnpj, ct)) return Resultado<long>.Falha("Já existe empresa com este CNPJ.");

        var normalizada = e with
        {
            Cnpj = cnpj, CnaePrincipal = cnae, Uf = e.Uf.ToUpperInvariant(), RazaoSocial = e.RazaoSocial.Trim(),
            InscricaoEstadual = Domain.Cadastros.InscricaoEstadual.Normalizar(e.InscricaoEstadual),
            Complemento = e.Complemento is null ? null : e.Complemento with { Cep = cep.Length == 0 ? null : cep },
        };
        var plano = e.ImportarPlanoModelo ? planoModelo.Obter() : [];
        return Resultado<long>.Ok(await repositorio.CriarAsync(normalizada, plano, ct));
    }

    public Task<EmpresaDetalhe?> ObterAsync(long id, CancellationToken ct = default) => repositorio.ObterAsync(id, ct);

    public async Task<Resultado> AlterarAsync(AlteracaoEmpresa e, CancellationToken ct = default)
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(e.RazaoSocial)) erros.Add("Razão social obrigatória.");
        if (e.NaturezaJuridica.Length != 4 || !e.NaturezaJuridica.All(char.IsAsciiDigit)) erros.Add("Natureza jurídica deve ter 4 dígitos (tabela CONCLA).");
        var cnae = Cnpj.SomenteDigitos(e.CnaePrincipal);
        if (cnae.Length != 7) erros.Add("CNAE deve ter 7 dígitos.");
        if (!Uf.Valida(e.Uf)) erros.Add("UF inválida.");
        if (e.CodMunicipioIbge.Length != 7 || !e.CodMunicipioIbge.All(char.IsAsciiDigit)) erros.Add("Código de município IBGE deve ter 7 dígitos.");
        else if (Uf.DeCodigoIbge(e.CodMunicipioIbge) is { } ufIbge && !string.Equals(ufIbge, e.Uf.Trim(), StringComparison.OrdinalIgnoreCase))
            erros.Add($"O município IBGE {e.CodMunicipioIbge} é de {ufIbge}, não de {e.Uf.ToUpperInvariant()}.");
        var cep = Cnpj.SomenteDigitos(e.Complemento?.Cep);
        if (cep.Length is not (0 or 8)) erros.Add("CEP deve ter 8 dígitos.");
        if (!Domain.Cadastros.InscricaoEstadual.Valida(e.InscricaoEstadual)) erros.Add("Inscrição estadual deve ter de 2 a 14 dígitos ou ISENTO.");
        if (erros.Count > 0) return Resultado.Falha(erros);
        if (await repositorio.ObterAsync(e.Id, ct) is null) return Resultado.Falha("Empresa não encontrada.");

        await repositorio.AtualizarAsync(e with
        {
            RazaoSocial = e.RazaoSocial.Trim(), CnaePrincipal = cnae, Uf = e.Uf.Trim().ToUpperInvariant(),
            InscricaoEstadual = Domain.Cadastros.InscricaoEstadual.Normalizar(e.InscricaoEstadual),
            Complemento = e.Complemento is null ? null : e.Complemento with { Cep = cep.Length == 0 ? null : cep },
        }, ct);
        return Resultado.Ok();
    }

    /// <summary>Grava só a inscrição estadual (após o usuário confirmar o número encontrado na consulta).</summary>
    public async Task<Resultado> SalvarInscricaoEstadualAsync(long id, string? inscricao, CancellationToken ct = default)
    {
        if (!Domain.Cadastros.InscricaoEstadual.Valida(inscricao)) return Resultado.Falha("Inscrição estadual deve ter de 2 a 14 dígitos ou ISENTO.");
        await repositorio.AtualizarInscricaoEstadualAsync(id, Domain.Cadastros.InscricaoEstadual.Normalizar(inscricao), ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> ExcluirAsync(long id, CancellationToken ct = default)
    {
        if (await repositorio.ObterAsync(id, ct) is null) return Resultado.Falha("Empresa não encontrada.");
        await repositorio.DesativarAsync(id, ct);
        return Resultado.Ok();
    }

    /// <summary>
    /// Inscrições estaduais do CNPJ na UF da empresa (UF informada ou deduzida do código IBGE do município).
    /// A gravação é decisão do usuário (ver <see cref="SalvarInscricaoEstadualAsync"/>).
    /// </summary>
    public async Task<Resultado<IReadOnlyList<InscricaoEstadualEncontrada>>> ConsultarInscricaoEstadualAsync(
        string cnpj, string? uf, string? codMunicipioIbge, CancellationToken ct = default)
    {
        var c = new string(cnpj.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        if (!Cnpj.Valido(c)) return Resultado<IReadOnlyList<InscricaoEstadualEncontrada>>.Falha("CNPJ inválido. Confira os dígitos antes de consultar.");
        var estado = Uf.Valida(uf) ? uf!.Trim().ToUpperInvariant() : Uf.DeCodigoIbge(codMunicipioIbge);
        if (estado is null) return Resultado<IReadOnlyList<InscricaoEstadualEncontrada>>.Falha("Informe a UF ou o código IBGE do município para consultar a inscrição estadual.");
        if (consultaIe is null) return Resultado<IReadOnlyList<InscricaoEstadualEncontrada>>.Falha("Consulta de inscrição estadual não configurada.");
        var r = await consultaIe.ConsultarAsync(c, ct);
        return r.Sucesso
            ? Resultado<IReadOnlyList<InscricaoEstadualEncontrada>>.Ok(r.Valor.Where(i => i.Uf == estado).ToList())
            : r;
    }
}
