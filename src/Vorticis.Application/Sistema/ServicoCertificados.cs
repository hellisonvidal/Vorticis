using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Application.Sistema;

/// <summary>Certificado guardado no repositório (sem o conteúdo do arquivo).</summary>
public sealed record CertificadoDigital(long Id, string Tipo, string Descricao, string Titular, string? Documento, string Emissor,
                                        DateTimeOffset ValidoDe, DateTimeOffset ValidoAte, string Impressao, long? EmpresaId)
{
    public int DiasParaVencer(DateTimeOffset agora) => (int)Math.Floor((ValidoAte - agora).TotalDays);

    public string Situacao(DateTimeOffset agora) => DiasParaVencer(agora) switch
    {
        < 0 => "Vencido",
        <= 30 => $"Vence em {DiasParaVencer(agora)} dia(s)",
        _ => "Válido",
    };
}

/// <summary>Dados lidos de um certificado (arquivo .pfx ou repositório do Windows), antes de guardar.</summary>
public sealed record DadosCertificado(string Titular, string? Documento, string Emissor, DateTimeOffset ValidoDe, DateTimeOffset ValidoAte,
                                      string Impressao, bool TemChavePrivada);

public interface IRepositorioCertificados
{
    Task<IReadOnlyList<CertificadoDigital>> ListarAsync(CancellationToken ct);
    Task<long> IncluirAsync(CertificadoDigital c, byte[]? arquivo, CancellationToken ct);
    Task<byte[]?> ObterArquivoAsync(long id, CancellationToken ct);
    Task ExcluirAsync(long id, CancellationToken ct);
}

/// <summary>
/// Configurações › Certificados digitais: repositório dos certificados usados na comunicação com órgãos públicos
/// (Receita Federal, SEFAZ, Junta Comercial). A1 (.pfx) fica guardado no banco como foi importado — protegido pela
/// própria senha, que não é gravada; A3 (token/cartão) é só registrado (o certificado fica no dispositivo). Só o
/// administrador mantém o repositório.
/// </summary>
public sealed class ServicoCertificados(IRepositorioCertificados repositorio, TimeProvider? relogio = null)
{
    private readonly TimeProvider _relogio = relogio ?? TimeProvider.System;
    public DateTimeOffset Agora => _relogio.GetUtcNow();

    public async Task<Resultado<IReadOnlyList<CertificadoDigital>>> ListarAsync(SessaoUsuario usuario, CancellationToken ct = default)
        => usuario.Administrador
            ? Resultado<IReadOnlyList<CertificadoDigital>>.Ok((await repositorio.ListarAsync(ct)).OrderBy(c => c.ValidoAte).ToList())
            : Resultado<IReadOnlyList<CertificadoDigital>>.Falha("Somente o administrador acessa os certificados digitais.");

    /// <summary>Lê o .pfx com a senha (sem guardar nada) para conferir titular e validade antes de importar.</summary>
    public static Resultado<DadosCertificado> LerPfx(byte[] pfx, string senha)
    {
        try
        {
            using var cert = X509CertificateLoader.LoadPkcs12(pfx, senha, X509KeyStorageFlags.EphemeralKeySet);
            return Resultado<DadosCertificado>.Ok(Dados(cert));
        }
        catch (CryptographicException)
        {
            return Resultado<DadosCertificado>.Falha("Não foi possível abrir o certificado: senha incorreta ou arquivo que não é .pfx/.p12.");
        }
    }

    /// <summary>Importa um certificado A1 (.pfx/.p12). A senha só é usada para conferir o arquivo.</summary>
    public async Task<Resultado<long>> ImportarA1Async(SessaoUsuario usuario, byte[] pfx, string senha, string? descricao, long? empresaId,
                                                     CancellationToken ct = default)
    {
        if (!usuario.Administrador) return Resultado<long>.Falha("Somente o administrador importa certificados digitais.");
        var lido = LerPfx(pfx, senha);
        if (!lido.Sucesso) return Resultado<long>.Falha(lido.Erros);
        if (!lido.Valor.TemChavePrivada) return Resultado<long>.Falha("O arquivo não contém a chave privada; exporte o certificado A1 com a chave (.pfx).");
        return await GravarAsync("A1", lido.Valor, pfx, descricao, empresaId, ct);
    }

    /// <summary>Registra um certificado A3 (token/cartão) visto no repositório do Windows.</summary>
    public async Task<Resultado<long>> RegistrarA3Async(SessaoUsuario usuario, DadosCertificado dados, string? descricao, long? empresaId,
                                                      CancellationToken ct = default)
    {
        if (!usuario.Administrador) return Resultado<long>.Falha("Somente o administrador registra certificados digitais.");
        return await GravarAsync("A3", dados, null, descricao, empresaId, ct);
    }

    private async Task<Resultado<long>> GravarAsync(string tipo, DadosCertificado d, byte[]? arquivo, string? descricao, long? empresaId,
                                                   CancellationToken ct)
    {
        if (d.ValidoAte < Agora) return Resultado<long>.Falha($"Certificado vencido em {d.ValidoAte.ToLocalTime():dd/MM/yyyy}.");
        if ((await repositorio.ListarAsync(ct)).FirstOrDefault(c => c.Impressao == d.Impressao) is { } existente)
            return Resultado<long>.Falha($"Este certificado já está no repositório ({existente.Descricao}).");
        var desc = string.IsNullOrWhiteSpace(descricao) ? d.Titular : descricao.Trim();
        try
        {
            return Resultado<long>.Ok(await repositorio.IncluirAsync(new CertificadoDigital(0, tipo, Truncar(desc, 150), Truncar(d.Titular, 250), d.Documento,
                Truncar(d.Emissor, 250), d.ValidoDe, d.ValidoAte, d.Impressao, empresaId), arquivo, ct));
        }
        catch (ViolacaoRegraException ex) { return Resultado<long>.Falha(ex.Message); }
    }

    public async Task<Resultado> ExcluirAsync(SessaoUsuario usuario, long id, CancellationToken ct = default)
    {
        if (!usuario.Administrador) return Resultado.Falha("Somente o administrador exclui certificados digitais.");
        await repositorio.ExcluirAsync(id, ct);
        return Resultado.Ok();
    }

    /// <summary>Carrega o certificado A1 guardado, pronto para assinar ou autenticar (a senha é pedida no uso).</summary>
    public async Task<Resultado<X509Certificate2>> AbrirA1Async(SessaoUsuario usuario, long id, string senha, CancellationToken ct = default)
    {
        if (!usuario.Administrador) return Resultado<X509Certificate2>.Falha("Somente o administrador usa os certificados digitais.");
        if (await repositorio.ObterArquivoAsync(id, ct) is not { } pfx) return Resultado<X509Certificate2>.Falha("Certificado A1 não encontrado.");
        try
        {
            return Resultado<X509Certificate2>.Ok(X509CertificateLoader.LoadPkcs12(pfx, senha, X509KeyStorageFlags.EphemeralKeySet));
        }
        catch (CryptographicException) { return Resultado<X509Certificate2>.Falha("Senha do certificado incorreta."); }
    }

    /// <summary>Certificados pessoais com chave privada instalados no Windows (A3 em token/cartão e A1 instalados).</summary>
    public static IReadOnlyList<DadosCertificado> ListarInstaladosNoWindows()
    {
        if (!OperatingSystem.IsWindows()) return [];
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        return store.Certificates.Where(c => c.HasPrivateKey).Select(Dados).OrderBy(d => d.Titular).ToList();
    }

    public static DadosCertificado Dados(X509Certificate2 c)
    {
        var nome = c.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        // ICP-Brasil: "RAZAO SOCIAL:12345678000190" (e-CNPJ) ou "NOME:12345678901" (e-CPF).
        var m = Regex.Match(nome, @"^(?<titular>.*?):(?<doc>\d{11}|\d{14})$");
        var titular = m.Success ? m.Groups["titular"].Value.Trim() : nome;
        var doc = m.Success ? m.Groups["doc"].Value : null;
        return new DadosCertificado(titular, doc, c.GetNameInfo(X509NameType.SimpleName, forIssuer: true),
            new DateTimeOffset(c.NotBefore.ToUniversalTime()), new DateTimeOffset(c.NotAfter.ToUniversalTime()), c.Thumbprint.ToUpperInvariant(),
            c.HasPrivateKey);
    }

    private static string Truncar(string s, int max) => s.Length <= max ? s : s[..max];
}
