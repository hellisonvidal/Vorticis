using Perseus.Domain.Comum;

namespace Perseus.Application.Cadastros;

/// <summary>Dados cadastrais de um CNPJ obtidos de fonte externa.</summary>
public sealed record DadosCnpj(
    string Cnpj,
    string RazaoSocial,
    string? NomeFantasia,
    string? NaturezaJuridica,      // código CONCLA, 4 dígitos
    string? CnaePrincipal,         // 7 dígitos
    string? Uf,
    string? CodMunicipioIbge,      // 7 dígitos
    string? Municipio,
    DateOnly? DataInicioAtividades,
    string? SituacaoCadastral,     // ATIVA, BAIXADA, INAPTA, SUSPENSA, NULA
    bool? OptanteSimples,
    bool? OptanteMei,
    string? Cep,
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    string? Email,
    string? Telefone,
    string Fonte,
    DateTimeOffset ConsultadoEm)
{
    /// <summary>Regime sugerido a partir das opções públicas. Presumido/Real não constam da base pública.</summary>
    public RegimeTributario? RegimeSugerido =>
        OptanteMei == true ? RegimeTributario.MEI : OptanteSimples == true ? RegimeTributario.SimplesNacional : null;

    public bool Ativa => string.Equals(SituacaoCadastral, "ATIVA", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Fonte de dados cadastrais de CNPJ. Implementações: base pública (dados abertos da RFB via BrasilAPI/Minha Receita);
/// futuramente API oficial paga (Serpro "Consulta CNPJ").
/// </summary>
public interface IConsultaCnpj
{
    Task<Resultado<DadosCnpj>> ConsultarAsync(string cnpj, CancellationToken ct);
}
