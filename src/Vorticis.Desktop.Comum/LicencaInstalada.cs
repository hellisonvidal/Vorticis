using Vorticis.Domain.Comum;
using Vorticis.Domain.Licencas;

namespace Vorticis.Desktop.Comum;

/// <summary>
/// Arquivo de licença (licenca.vlic) deste computador. Procurado, nesta ordem: no caminho da variável VORTICIS_LICENCA, na pasta do
/// programa (onde o instalador o copia), em %LOCALAPPDATA%\Vorticis e em %PROGRAMDATA%\Vorticis. A assinatura é conferida com a chave
/// pública do fornecedor embutida no programa.
/// </summary>
public static class LicencaInstalada
{
    public const string NomeArquivo = "licenca" + ArquivoLicenca.Extensao;

    /// <summary>Chave pública que confere as licenças: a do fornecedor, embutida no programa (os testes automatizados usam uma própria).</summary>
    public static string ChavePublica { get; set; } = ChavesLicenca.PublicaDoFornecedor;

    public static IEnumerable<string> Locais()
    {
        if (Environment.GetEnvironmentVariable("VORTICIS_LICENCA") is { Length: > 0 } env) yield return env;
        yield return Path.Combine(AppContext.BaseDirectory, NomeArquivo);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vorticis", NomeArquivo);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Vorticis", NomeArquivo);
    }

    /// <summary>A primeira licença válida (assinatura conferida) encontrada; falha com o motivo se não houver nenhuma.</summary>
    public static Resultado<Licenca> Carregar(string? chavePublica = null, string? arquivo = null)
    {
        chavePublica ??= ChavePublica;
        string? erro = null;
        foreach (var caminho in (arquivo is null ? Locais() : [arquivo]).Where(File.Exists))
        {
            var r = ArquivoLicenca.Ler(File.ReadAllText(caminho), chavePublica);
            if (r.Sucesso) return r;
            erro ??= $"{caminho}: {r.Erros[0]}";
        }
        return Resultado<Licenca>.Falha(erro ?? "Licença não encontrada neste computador. Instale o arquivo de licença (licenca.vlic) recebido do fornecedor.");
    }

    /// <summary>Licença válida para o módulo hoje (assinatura, validade e módulo liberado).</summary>
    public static Resultado<Licenca> ConferirModulo(string nomeExecutavel, DateOnly hoje, string? chavePublica = null, string? arquivo = null)
    {
        var r = Carregar(chavePublica, arquivo);
        if (!r.Sucesso) return r;
        var c = ArquivoLicenca.ConferirModulo(r.Valor, ModulosLicenca.DoExecutavel(nomeExecutavel), hoje);
        return c.Sucesso ? r : Resultado<Licenca>.Falha(c.Erros);
    }

    /// <summary>Confere o arquivo escolhido e o copia para %LOCALAPPDATA%\Vorticis (vale para este usuário do Windows).</summary>
    public static Resultado<Licenca> Instalar(string arquivo, string? destino = null, string? chavePublica = null)
    {
        if (!File.Exists(arquivo)) return Resultado<Licenca>.Falha("Arquivo não encontrado.");
        var texto = File.ReadAllText(arquivo);
        var r = ArquivoLicenca.Ler(texto, chavePublica ?? ChavePublica);
        if (!r.Sucesso) return r;
        destino ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vorticis", NomeArquivo);
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        File.WriteAllText(destino, texto);
        return r;
    }
}
