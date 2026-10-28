using System.Text;

namespace Vorticis.Domain.Sped.Importacao;

/// <summary>Identificação do arquivo (registro 0000) para a conferência com a empresa.</summary>
public sealed record CabecalhoSped(TipoArquivoSped Tipo, string Cnpj, string Nome, string Uf, string? InscricaoEstadual, DateOnly Inicio, DateOnly Fim,
                                   string? Versao = null)
{
    public string Periodo => $"{Inicio:dd/MM/yyyy} a {Fim:dd/MM/yyyy}";
}

/// <summary>Forma de importar escolhida no assistente (ex.: só o plano de contas, plano e saldos, tudo).</summary>
public sealed record ModoImportacao(string Codigo, string Titulo, string Descricao);

/// <summary>
/// Opções do assistente. <paramref name="Modo"/> é um dos códigos do importador; <paramref name="SubstituirPlano"/> troca o plano atual pelo do arquivo
/// (só se a empresa ainda não tem lançamentos); <paramref name="IgnorarComErro"/> grava o que está certo e lista o que tem erro (senão, qualquer erro cancela tudo).
/// </summary>
public sealed record OpcoesImportacaoSped(string Modo = "TUDO", bool SubstituirPlano = false, bool IgnorarComErro = false, bool ConverterMascara = true,
                                          bool CadastrarSignatarios = false);

/// <summary>Uma linha da prévia: quantos registros do tipo são novos, já existem ou conflitam, com amostras.</summary>
public sealed record LinhaPrevia(string Registro, string Descricao, int Total, int Novos, int Existentes, int Conflitos, IReadOnlyList<string> Amostras)
{
    public string AmostraTexto => string.Join("; ", Amostras);
}

public sealed record PreviaImportacao(CabecalhoSped Cabecalho, bool CnpjConfere, long? EmpresaCorrespondenteId, string? EmpresaCorrespondente,
                                      IReadOnlyList<LinhaPrevia> Linhas, IReadOnlyList<OcorrenciaImportacao> Ocorrencias, long? ImportacaoAnteriorId = null)
{
    public bool TemErro => Ocorrencias.Any(o => o.Gravidade == GravidadeOcorrencia.Erro);
    /// <summary>Erros que impedem a importação de qualquer jeito (CNPJ, leiaute); os pontuais podem ser ignorados pela opção do assistente.</summary>
    public bool PodeImportar => CnpjConfere && !Ocorrencias.Any(o => o.Gravidade == GravidadeOcorrencia.Erro && !o.Pontual);
    public int ErrosPontuais => Ocorrencias.Count(o => o.Gravidade == GravidadeOcorrencia.Erro && o.Pontual);
}

public sealed record ProgressoImportacao(string Etapa, int Atual, int Total)
{
    public double Percentual => Total <= 0 ? 0 : Math.Min(100d, 100d * Atual / Total);
}

public sealed record LinhaRelatorio(string Registro, string Descricao, int Importados, int Existentes, int ComErro);

/// <summary>Resultado final: quantidades por registro e as ocorrências (erros com linha e motivo).</summary>
public sealed record RelatorioImportacao(long ImportacaoId, string Arquivo, string Tipo, string Modo, IReadOnlyList<LinhaRelatorio> Linhas,
                                         IReadOnlyList<OcorrenciaImportacao> Ocorrencias, bool Desfeita = false)
{
    public int Importados => Linhas.Sum(l => l.Importados);
    public int Ignorados => Linhas.Sum(l => l.Existentes);
    public int Erros => Ocorrencias.Count(o => o.Gravidade == GravidadeOcorrencia.Erro);
    public int Avisos => Ocorrencias.Count(o => o.Gravidade == GravidadeOcorrencia.Aviso);

    public string Resumo => Desfeita
        ? $"Importação desfeita: {Importados} registro(s) removido(s)."
        : $"{Importados} registro(s) importado(s), {Ignorados} já existente(s) ignorado(s), {Erros} erro(s), {Avisos} aviso(s).";

    /// <summary>Texto do relatório para salvar (CSV com ponto e vírgula, abre direto no Excel).</summary>
    public string ParaCsv()
    {
        static string C(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder();
        sb.AppendLine($"Relatório da importação;{C(Tipo)};{C(Arquivo)};{C(Modo)}");
        sb.AppendLine(Resumo);
        sb.AppendLine();
        sb.AppendLine("Registro;Descrição;Importados;Já existentes (ignorados);Com erro");
        foreach (var l in Linhas) sb.AppendLine($"{l.Registro};{C(l.Descricao)};{l.Importados};{l.Existentes};{l.ComErro}");
        sb.AppendLine();
        sb.AppendLine("Gravidade;Linha do arquivo;Registro;Motivo");
        foreach (var o in Ocorrencias)
            sb.AppendLine($"{(o.Gravidade switch { GravidadeOcorrencia.Erro => "Erro", GravidadeOcorrencia.Aviso => "Aviso", _ => "Informação" })};{(o.Linha > 0 ? o.Linha.ToString() : "")};{o.Registro};{C(o.Motivo)}");
        return sb.ToString();
    }
}

/// <summary>Uma importação registrada no histórico.</summary>
public sealed record HistoricoImportacao(long Id, string Tipo, string Arquivo, DateOnly? Inicio, DateOnly? Fim, string Modo, DateTime EmUtc, string Usuario,
                                         bool Desfeita, int Importados)
{
    public string Situacao => Desfeita ? "Desfeita" : "Importada";
    public string Periodo => Inicio is { } i && Fim is { } f ? $"{i:dd/MM/yyyy} a {f:dd/MM/yyyy}" : "";
    public string Quando => EmUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}

/// <summary>Acumula os totais por registro enquanto o importador trabalha (usado para a prévia e para o relatório).</summary>
public sealed class ContadorImportacao
{
    private readonly Dictionary<string, (string Descricao, int Total, int Novos, int Existentes, int Conflitos, List<string> Amostras)> _d = [];
    private readonly List<string> _ordem = [];

    public void Registrar(string registro, string descricao, bool novo, bool conflito, string? amostra = null)
    {
        if (!_d.TryGetValue(registro, out var x)) { x = (descricao, 0, 0, 0, 0, []); _ordem.Add(registro); }
        x.Total++;
        if (conflito) x.Conflitos++; else if (novo) x.Novos++; else x.Existentes++;
        if (amostra is not null && x.Amostras.Count < 3 && (novo || conflito)) x.Amostras.Add(amostra);
        _d[registro] = x;
    }

    public IReadOnlyList<LinhaPrevia> Linhas() => _ordem.Select(r => { var x = _d[r]; return new LinhaPrevia(r, x.Descricao, x.Total, x.Novos, x.Existentes, x.Conflitos, x.Amostras); }).ToList();
}
