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
    bool ImportarPlanoModelo = true);

public sealed record EmpresaResumo(long Id, string Cnpj, string RazaoSocial);

/// <summary>Conta do plano modelo. Tipo, nível e pai derivam do código (máscara 9.9.99.999).</summary>
public sealed record ContaModelo(string Codigo, string Descricao, string CodNat, IndicadorDC NaturezaSaldo);

public interface IRepositorioEmpresa
{
    Task<bool> CnpjExisteAsync(string cnpj, CancellationToken ct);
    /// <summary>Cria empresa, regime, exercício com 12 períodos e (opcional) o plano de contas, numa transação.</summary>
    Task<long> CriarAsync(NovaEmpresa empresa, IReadOnlyList<ContaModelo> plano, CancellationToken ct);
    Task CriarExercicioAsync(long empresaId, int ano, CancellationToken ct);
    Task<IReadOnlyList<EmpresaResumo>> ListarAsync(CancellationToken ct);
}

public interface IFontePlanoModelo
{
    IReadOnlyList<ContaModelo> Obter();
}

public sealed class ServicoEmpresa(IRepositorioEmpresa repositorio, IFontePlanoModelo planoModelo)
{
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
        if (erros.Count > 0) return Resultado<long>.Falha(erros);

        if (await repositorio.CnpjExisteAsync(cnpj, ct)) return Resultado<long>.Falha("Já existe empresa com este CNPJ.");

        var normalizada = e with { Cnpj = cnpj, CnaePrincipal = cnae, Uf = e.Uf.ToUpperInvariant(), RazaoSocial = e.RazaoSocial.Trim() };
        var plano = e.ImportarPlanoModelo ? planoModelo.Obter() : [];
        return Resultado<long>.Ok(await repositorio.CriarAsync(normalizada, plano, ct));
    }
}
