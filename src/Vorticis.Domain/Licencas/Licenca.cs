using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Vorticis.Domain.Comum;

namespace Vorticis.Domain.Licencas;

/// <summary>
/// Licença de uso da suíte para um cliente: os módulos liberados, a validade e o número de usuários. Emitida e assinada pelo
/// Gerenciador de licenças (software separado, do fornecedor) com a chave privada; os módulos conferem a assinatura com a chave pública.
/// </summary>
public sealed record Licenca(string Id, string Cliente, string? Documento, IReadOnlyList<string> Modulos, DateOnly EmitidaEm, DateOnly ValidaAte, int Usuarios)
{
    public bool Libera(string modulo) => Modulos.Contains(modulo, StringComparer.OrdinalIgnoreCase);
    public bool Vencida(DateOnly hoje) => hoje > ValidaAte;
    public int DiasRestantes(DateOnly hoje) => ValidaAte.DayNumber - hoje.DayNumber;
    public string ModulosTexto => string.Join(", ", Modulos.Select(ModulosLicenca.Nome));
}

/// <summary>Códigos dos módulos na licença (os mesmos nomes de componente do instalador).</summary>
public static class ModulosLicenca
{
    public static IReadOnlyList<string> Todos { get; } = ["contabil", "fiscal", "folha", "escritorio", "erp", "condominio"];

    public static string Nome(string codigo) => codigo switch
    {
        "contabil" => "Contábil", "fiscal" => "Fiscal", "folha" => "Folha de Pagamento", "escritorio" => "Escritório", "erp" => "ERP",
        "condominio" => "Condomínio", _ => codigo,
    };

    /// <summary>Código do módulo pelo nome do executável ("VorticisContabil" → "contabil").</summary>
    public static string DoExecutavel(string nomeExecutavel) =>
        (nomeExecutavel.StartsWith("Vorticis", StringComparison.Ordinal) ? nomeExecutavel["Vorticis".Length..] : nomeExecutavel).ToLowerInvariant();
}

/// <summary>
/// Arquivo de licença (.vlic): texto simples "CHAVE=valor", uma linha por campo, terminado pela ASSINATURA (ECDSA P-256 com SHA-256,
/// em Base64) de todas as linhas anteriores. O instalador lê a linha MODULOS para oferecer só os módulos licenciados; o programa
/// confere a assinatura antes de liberar o módulo.
/// </summary>
public static class ArquivoLicenca
{
    public const string Cabecalho = "VORTICIS-LICENCA 1";
    public const string Extensao = ".vlic";

    /// <summary>Linhas assinadas (sem a assinatura), sempre separadas por \n.</summary>
    public static string Corpo(Licenca l)
    {
        static string Limpo(string? s) => (s ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return string.Join("\n",
        [
            Cabecalho,
            $"ID={Limpo(l.Id)}",
            $"CLIENTE={Limpo(l.Cliente)}",
            $"DOCUMENTO={Limpo(l.Documento)}",
            $"MODULOS={string.Join(",", l.Modulos.Select(m => m.ToLowerInvariant()))}",
            $"EMITIDA_EM={l.EmitidaEm:yyyy-MM-dd}",
            $"VALIDA_ATE={l.ValidaAte:yyyy-MM-dd}",
            $"USUARIOS={l.Usuarios}",
        ]) + "\n";
    }

    public static IReadOnlyList<string> Validar(Licenca l)
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(l.Cliente)) erros.Add("Informe o cliente.");
        if (l.Modulos.Count == 0) erros.Add("Marque pelo menos um módulo.");
        if (l.Modulos.Any(m => !ModulosLicenca.Todos.Contains(m))) erros.Add("Módulo desconhecido na licença.");
        if (l.ValidaAte < l.EmitidaEm) erros.Add("A validade não pode ser anterior à emissão.");
        if (l.Usuarios < 1) erros.Add("Informe o número de usuários (1 ou mais).");
        return erros;
    }

    /// <summary>Texto completo do arquivo, assinado com a chave privada do fornecedor (PEM).</summary>
    public static string Assinar(Licenca l, string chavePrivadaPem)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(chavePrivadaPem);
        var corpo = Corpo(l);
        var assinatura = ecdsa.SignData(Encoding.UTF8.GetBytes(corpo), HashAlgorithmName.SHA256);
        return corpo + "ASSINATURA=" + Convert.ToBase64String(assinatura) + "\n";
    }

    /// <summary>Lê o arquivo e confere a assinatura com a chave pública (PEM). Arquivo alterado ou de outro fornecedor é recusado.</summary>
    public static Resultado<Licenca> Ler(string texto, string chavePublicaPem)
    {
        var normal = texto.Replace("\r\n", "\n").TrimStart('﻿');
        var pos = normal.IndexOf("ASSINATURA=", StringComparison.Ordinal);
        if (!normal.StartsWith(Cabecalho + "\n", StringComparison.Ordinal) || pos < 0) return Resultado<Licenca>.Falha("O arquivo não é uma licença do Vorticis.");
        var corpo = normal[..pos];
        byte[] assinatura;
        try { assinatura = Convert.FromBase64String(normal[(pos + "ASSINATURA=".Length)..].Trim()); }
        catch (FormatException) { return Resultado<Licenca>.Falha("Assinatura da licença ilegível."); }
        using (var ecdsa = ECDsa.Create())
        {
            ecdsa.ImportFromPem(chavePublicaPem);
            if (!ecdsa.VerifyData(Encoding.UTF8.GetBytes(corpo), assinatura, HashAlgorithmName.SHA256))
                return Resultado<Licenca>.Falha("A assinatura da licença não confere: o arquivo foi alterado ou não foi emitido pelo fornecedor do Vorticis.");
        }
        var campos = corpo.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(l => l.Split('=', 2)).Where(p => p.Length == 2)
                          .ToDictionary(p => p[0], p => p[1]);
        string C(string k) => campos.TryGetValue(k, out var v) ? v : "";
        DateOnly D(string k) => DateOnly.TryParseExact(C(k), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateOnly.MinValue;
        var l = new Licenca(C("ID"), C("CLIENTE"), C("DOCUMENTO") is { Length: > 0 } doc ? doc : null,
                            C("MODULOS").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), D("EMITIDA_EM"), D("VALIDA_ATE"),
                            int.TryParse(C("USUARIOS"), out var u) ? u : 0);
        return Resultado<Licenca>.Ok(l);
    }

    /// <summary>Confere se a licença libera o módulo na data (vencida ou sem o módulo: recusa com o motivo).</summary>
    public static Resultado ConferirModulo(Licenca l, string modulo, DateOnly hoje)
    {
        if (l.Vencida(hoje)) return Resultado.Falha($"A licença de {l.Cliente} venceu em {l.ValidaAte:dd/MM/yyyy}. Solicite a renovação ao fornecedor.");
        if (!l.Libera(modulo)) return Resultado.Falha($"O módulo {ModulosLicenca.Nome(modulo)} não está na licença de {l.Cliente} (liberados: {l.ModulosTexto}).");
        return Resultado.Ok();
    }
}

/// <summary>Par de chaves do fornecedor (ECDSA P-256) e a chave pública embutida nos módulos.</summary>
public static class ChavesLicenca
{
    public static (string Privada, string Publica) Gerar()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (ecdsa.ExportPkcs8PrivateKeyPem(), ecdsa.ExportSubjectPublicKeyInfoPem());
    }

    /// <summary>Chave pública do fornecedor, embutida no programa (Licencas/chave-publica.pem).</summary>
    public static string PublicaDoFornecedor
    {
        get
        {
            using var s = typeof(ChavesLicenca).Assembly.GetManifestResourceStream("Vorticis.Domain.Licencas.chave-publica.pem")
                          ?? throw new InvalidOperationException("Chave pública das licenças não embutida.");
            using var r = new StreamReader(s);
            return r.ReadToEnd();
        }
    }

    public static string PublicaDe(string chavePrivadaPem)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(chavePrivadaPem);
        return ecdsa.ExportSubjectPublicKeyInfoPem();
    }
}
