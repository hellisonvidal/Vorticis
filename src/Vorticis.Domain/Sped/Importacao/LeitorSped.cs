using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Vorticis.Domain.Sped.Importacao;

/// <summary>Tipo de escrituração SPED reconhecido pelo registro 0000.</summary>
public enum TipoArquivoSped { Desconhecido, Ecd, Ecf, EfdIcmsIpi, EfdContribuicoes }

public static class TipoArquivoSpedExtensoes
{
    public static string Codigo(this TipoArquivoSped t) => t switch
    {
        TipoArquivoSped.Ecd => "ECD", TipoArquivoSped.Ecf => "ECF", TipoArquivoSped.EfdIcmsIpi => "EFD_ICMS", TipoArquivoSped.EfdContribuicoes => "EFD_CONTRIB",
        _ => "DESCONHECIDO",
    };

    public static string Nome(this TipoArquivoSped t) => t switch
    {
        TipoArquivoSped.Ecd => "ECD (SPED Contábil)", TipoArquivoSped.Ecf => "ECF (SPED)", TipoArquivoSped.EfdIcmsIpi => "EFD ICMS/IPI",
        TipoArquivoSped.EfdContribuicoes => "EFD-Contribuições", _ => "arquivo desconhecido",
    };
}

/// <summary>Uma linha do arquivo: "|REG|campo2|campo3|...|". O acesso por índice usa a numeração do manual do leiaute (REG = campo 1).</summary>
public sealed record RegistroSped(int Linha, string Nome, string[] Campos)
{
    /// <summary>Campo <paramref name="n"/> (numeração do manual: 1 = REG, 2 = primeiro campo de dados). Vazio quando não existe.</summary>
    public string this[int n] => n == 1 ? Nome : n - 2 >= 0 && n - 2 < Campos.Length ? Campos[n - 2].Trim() : "";

    public bool Tem(int n) => this[n].Length > 0;

    /// <summary>Data ddmmaaaa; nula quando vazia ou inválida.</summary>
    public DateOnly? Data(int n) => LeitorSped.Data(this[n]);

    /// <summary>Valor decimal com vírgula (aceita ponto); zero quando vazio ou inválido.</summary>
    public decimal Valor(int n) => LeitorSped.Decimal(this[n]) ?? 0m;

    public decimal? ValorOuNulo(int n) => LeitorSped.Decimal(this[n]);

    public override string ToString() => $"|{Nome}|{string.Join('|', Campos)}|";
}

/// <summary>Arquivo SPED lido: registros (sem o bloco de assinatura), validação do leiaute e identificação do tipo.</summary>
public sealed class ArquivoSped
{
    public required IReadOnlyList<RegistroSped> Registros { get; init; }
    public required TipoArquivoSped Tipo { get; init; }
    public required string Sha256 { get; init; }
    public bool Assinado { get; init; }
    /// <summary>Problemas do leiaute (arquivo truncado, linha fora do padrão). Se houver erro, a importação não pode seguir.</summary>
    public IReadOnlyList<OcorrenciaImportacao> Ocorrencias { get; init; } = [];
    public IReadOnlyDictionary<string, int> Contagem { get; init; } = new Dictionary<string, int>();

    public bool Valido => Tipo != TipoArquivoSped.Desconhecido && Ocorrencias.All(o => o.Gravidade != GravidadeOcorrencia.Erro);
    public RegistroSped? Zero => Registros.FirstOrDefault(r => r.Nome == "0000");
    public IEnumerable<RegistroSped> Todos(string nome) => Registros.Where(r => r.Nome == nome);
}

public enum GravidadeOcorrencia { Informacao, Aviso, Erro }

/// <summary>Algo a mostrar no relatório: aviso ou erro com a linha do arquivo e o motivo.</summary>
/// <param name="Pontual">Erro de um item só (um lançamento, uma nota): pode ser ignorado pela opção do assistente; os demais impedem a importação.</param>
public sealed record OcorrenciaImportacao(GravidadeOcorrencia Gravidade, int Linha, string Registro, string Motivo, bool Pontual = false)
{
    public static OcorrenciaImportacao ErroPontual(int linha, string registro, string motivo) => new(GravidadeOcorrencia.Erro, linha, registro, motivo, true);
    public static OcorrenciaImportacao Erro(int linha, string registro, string motivo) => new(GravidadeOcorrencia.Erro, linha, registro, motivo);
    public static OcorrenciaImportacao Aviso(int linha, string registro, string motivo) => new(GravidadeOcorrencia.Aviso, linha, registro, motivo);
    public static OcorrenciaImportacao Info(int linha, string registro, string motivo) => new(GravidadeOcorrencia.Informacao, linha, registro, motivo);
}

/// <summary>
/// Leitor genérico de arquivos SPED em texto ISO-8859-1: campos separados por "|", uma linha por registro. Para na linha do
/// registro 9999 — o que vem depois (bloco de assinatura "SBRCAAEPDR" em binário) é ignorado, o arquivo vale com ou sem ele.
/// </summary>
public static class LeitorSped
{
    public static ArquivoSped Ler(byte[] conteudo)
    {
        var texto = FormatoSped.Latin1.GetString(conteudo);
        var registros = new List<RegistroSped>();
        var oc = new List<OcorrenciaImportacao>();
        var contagem = new Dictionary<string, int>();
        var assinado = false;
        var viu9999 = false;
        var linhaNum = 0;
        var inicio = 0;
        var fimConteudo = conteudo.Length;       // posição depois do 9999: o hash ignora o bloco de assinatura
        while (inicio < texto.Length)
        {
            var fim = texto.IndexOf('\n', inicio);
            var bruta = fim < 0 ? texto[inicio..] : texto[inicio..fim];
            inicio = fim < 0 ? texto.Length : fim + 1;
            linhaNum++;
            var linha = bruta.TrimEnd('\r');
            if (viu9999)
            {
                if (linha.Contains("SBRCAAEPDR", StringComparison.Ordinal)) assinado = true;
                continue;
            }
            if (linhaNum == 1) linha = linha.TrimStart('﻿');
            if (linha.Trim().Length == 0) continue;
            if (linha[0] != '|' || linha[^1] != '|' || linha.Length < 3)
            {
                oc.Add(OcorrenciaImportacao.Erro(linhaNum, "", "Linha fora do padrão do SPED (deve começar e terminar com \"|\")."));
                continue;
            }
            var partes = linha.Split('|');                  // [0]="" [1]=REG ... [^1]=""
            var nome = partes[1].Trim();
            if (nome.Length != 4)
            {
                oc.Add(OcorrenciaImportacao.Erro(linhaNum, nome, $"Nome de registro inválido \"{nome}\"."));
                continue;
            }
            registros.Add(new RegistroSped(linhaNum, nome, partes[2..^1]));
            contagem[nome] = contagem.GetValueOrDefault(nome) + 1;
            if (nome == "9999") { viu9999 = true; fimConteudo = inicio; }
        }
        if (registros.Count == 0)
            oc.Add(OcorrenciaImportacao.Erro(1, "", "O arquivo está vazio ou não é um arquivo SPED em texto."));
        else
        {
            if (registros[0].Nome != "0000")
                oc.Add(OcorrenciaImportacao.Erro(registros[0].Linha, registros[0].Nome, "O primeiro registro deve ser o 0000 (abertura do arquivo)."));
            if (!viu9999)
                oc.Add(OcorrenciaImportacao.Erro(registros[^1].Linha, registros[^1].Nome, "O arquivo termina sem o registro 9999: está incompleto (cortado ou gerado pela metade)."));
            else
            {
                var r9999 = registros[^1];
                if (int.TryParse(r9999[2], out var qtd) && qtd != r9999.Linha)
                    oc.Add(OcorrenciaImportacao.Aviso(r9999.Linha, "9999", $"O registro 9999 informa {qtd} linhas, mas o arquivo tem {r9999.Linha}."));
            }
            foreach (var r in registros.Where(r => r.Nome == "9900"))
                if (contagem.TryGetValue(r[2], out var real) && int.TryParse(r[3], out var dito) && dito != real)
                    oc.Add(OcorrenciaImportacao.Aviso(r.Linha, "9900", $"O registro {r[2]} aparece {real} vez(es), mas o 9900 informa {dito}."));
        }
        return new ArquivoSped
        {
            Registros = registros, Tipo = Identificar(registros), Sha256 = Convert.ToHexString(SHA256.HashData(conteudo.AsSpan(0, Math.Min(fimConteudo, conteudo.Length)))).ToLowerInvariant(),
            Assinado = assinado, Ocorrencias = oc, Contagem = contagem,
        };
    }

    /// <summary>ECD: 0000 com "LECD"; ECF: "LECF"; EFD ICMS/IPI: a data inicial no 4º campo; EFD-Contribuições: a data inicial no 7º.</summary>
    public static TipoArquivoSped Identificar(IReadOnlyList<RegistroSped> registros)
    {
        if (registros.FirstOrDefault(r => r.Nome == "0000") is not { } z) return TipoArquivoSped.Desconhecido;
        if (z[2] == "LECD") return TipoArquivoSped.Ecd;
        if (z[2] == "LECF") return TipoArquivoSped.Ecf;
        if (z[2].Length == 3 && z[2].All(char.IsAsciiDigit))
        {
            if (Data(z[4]) is not null && z[4].Length == 8) return TipoArquivoSped.EfdIcmsIpi;
            if (Data(z[7]) is not null && z[7].Length == 8) return TipoArquivoSped.EfdContribuicoes;
        }
        return TipoArquivoSped.Desconhecido;
    }

    public static DateOnly? Data(string? s)
    {
        if (s is not { Length: 8 } || !s.All(char.IsAsciiDigit)) return null;
        return DateOnly.TryParseExact(s, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    public static decimal? Decimal(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim().Replace(',', '.');
        return decimal.TryParse(t, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    /// <summary>Só dígitos (e letras, para o CNPJ alfanumérico), em maiúsculas.</summary>
    public static string Documento(string? s) => s is null ? "" : new string(s.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
