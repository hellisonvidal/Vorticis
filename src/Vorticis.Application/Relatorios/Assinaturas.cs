using Vorticis.Application.Cadastros;
using Vorticis.Domain.Comum;

namespace Vorticis.Application.Relatorios;

/// <summary>
/// Opção comum a todos os relatórios em PDF: quais assinaturas entram no bloco final.
/// <paramref name="Responsavel"/>: sócio/responsável (representante legal); <paramref name="Contador"/>: contador com CRC.
/// </summary>
public sealed record OpcoesAssinatura(bool Responsavel = false, bool Contador = false)
{
    public static OpcoesAssinatura Nenhuma { get; } = new();
    public bool Alguma => Responsavel || Contador;
}

/// <summary>
/// Monta o bloco de assinaturas dos relatórios a partir dos cadastros da empresa (sócios/responsáveis e contadores).
/// Cada assinatura é um texto de duas linhas (nome; cargo/qualificação e documentos) que o gerador de PDF imprime sob uma linha.
/// </summary>
public sealed class ServicoAssinaturas(ServicoResponsaveis responsaveis)
{
    public async Task<Resultado<IReadOnlyList<string>>> MontarAsync(long empresaId, OpcoesAssinatura opcoes, CancellationToken ct = default)
    {
        var assinaturas = new List<string>();
        var erros = new List<string>();
        if (opcoes.Responsavel)
        {
            var socio = await responsaveis.SocioResponsavelAsync(empresaId, ct);
            if (socio is null) erros.Add("Assinatura do sócio/responsável: não há sócio ativo cadastrado para a empresa (Cadastros › Empresas › Sócios).");
            else assinaturas.Add(Formatar(socio));
        }
        if (opcoes.Contador)
        {
            var contador = await responsaveis.SignatarioEcdEcfAsync(empresaId, ct);
            if (contador is null) erros.Add("Assinatura do contador: não há contador responsável vinculado à empresa (Cadastros › Contadores).");
            else assinaturas.Add(Formatar(contador));
        }
        return erros.Count > 0 ? Resultado<IReadOnlyList<string>>.Falha(erros) : Resultado<IReadOnlyList<string>>.Ok(assinaturas);
    }

    /// <summary>Nome; qualificação (ou "Sócio/Responsável") e CPF/CNPJ.</summary>
    public static string Formatar(Socio s)
    {
        var cargo = string.IsNullOrWhiteSpace(s.Qualificacao) ? "Sócio/Responsável" : s.Qualificacao.Trim();
        if (s.RepresentanteLegal && !cargo.Contains("represent", StringComparison.OrdinalIgnoreCase)) cargo += " (representante legal)";
        return $"{s.Nome.Trim()}\n{cargo}{Documento(s.CpfCnpj)}";
    }

    /// <summary>Nome; "Contador — CRC UF-número" e CPF.</summary>
    public static string Formatar(Contador c) =>
        $"{c.Nome.Trim()}\nContador — CRC {c.CrcCompleto}{Documento(c.Cpf)}";

    private static string Documento(string? doc)
    {
        var d = new string((doc ?? "").Where(char.IsDigit).ToArray());
        return d.Length switch
        {
            11 => $" — CPF {Domain.Cadastros.Cpf.Formatar(d)}",
            14 => $" — CNPJ {ServicoLivros.FormatarCnpj(d)}",
            _ => "",
        };
    }
}
