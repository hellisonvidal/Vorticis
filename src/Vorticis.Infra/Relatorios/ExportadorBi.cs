using System.Globalization;
using System.Text;
using Vorticis.Application.Relatorios;

namespace Vorticis.Infra.Relatorios;

public enum FormatoExportacaoBi { Csv, Excel, Ambos }

/// <summary>
/// Grava os fatos e dimensões para BI externo numa pasta: um CSV por tabela (UTF-8 com BOM, separador vírgula, decimal com ponto e data AAAA-MM-DD, que o
/// Power BI e o Excel leem em qualquer idioma) e/ou uma pasta de trabalho Excel com uma aba por tabela.
/// </summary>
public static class ExportadorBi
{
    public const string ArquivoExcel = "dados_bi.xlsx";
    public const string ArquivoLeiaMe = "leia-me.txt";

    public static IReadOnlyList<string> Gravar(string pasta, IReadOnlyList<TabelaRelatorio> tabelas, FormatoExportacaoBi formato)
    {
        Directory.CreateDirectory(pasta);
        var arquivos = new List<string>();
        if (formato is FormatoExportacaoBi.Csv or FormatoExportacaoBi.Ambos)
            foreach (var t in tabelas)
            {
                var caminho = Path.Combine(pasta, t.Titulo + ".csv");
                File.WriteAllBytes(caminho, new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(Csv(t))).ToArray());
                arquivos.Add(caminho);
            }
        if (formato is FormatoExportacaoBi.Excel or FormatoExportacaoBi.Ambos)
        {
            var caminho = Path.Combine(pasta, ArquivoExcel);
            File.WriteAllBytes(caminho, ExportadorExcel.Gerar(tabelas));
            arquivos.Add(caminho);
        }
        var leia = Path.Combine(pasta, ArquivoLeiaMe);
        File.WriteAllText(leia, LeiaMe(tabelas), new UTF8Encoding(true));
        arquivos.Add(leia);
        return arquivos;
    }

    public static string Csv(TabelaRelatorio t)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", t.Colunas.Select(c => Campo(c.Titulo))));
        foreach (var l in t.Linhas) sb.AppendLine(string.Join(",", l.Valores.Select(Celula)));
        return sb.ToString();
    }

    private static string Celula(object? v) => v switch
    {
        null => "",
        decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        long or int => Convert.ToString(v, CultureInfo.InvariantCulture)!,
        var o => Campo(Convert.ToString(o, CultureInfo.InvariantCulture) ?? ""),
    };

    private static string Campo(string s) => s.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    private static string LeiaMe(IReadOnlyList<TabelaRelatorio> tabelas)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Dados para BI (Power BI, Excel) - gerados pelo programa");
        sb.AppendLine();
        sb.AppendLine("Arquivos: um por tabela (formato CSV, UTF-8, separador vírgula, decimal com ponto, data AAAA-MM-DD) e/ou dados_bi.xlsx com uma aba por tabela.");
        sb.AppendLine("Relações sugeridas (muitos para um): fato_partidas[id_conta] -> dim_plano_contas[id_conta]; fato_partidas[id_centro_custo] -> dim_centro_custo[id_centro_custo];");
        sb.AppendLine("fato_partidas[id_participante] -> dim_participante[id_participante]; fato_partidas[data] -> dim_calendario[data].");
        sb.AppendLine("Medida principal: soma de valor_assinado (débito positivo, crédito negativo). Para receitas, use o valor com sinal invertido.");
        sb.AppendLine();
        foreach (var t in tabelas)
        {
            sb.AppendLine($"{t.Titulo}: {t.Linhas.Count} linhas. Colunas: {string.Join(", ", t.Colunas.Select(c => c.Titulo))}");
            if (!string.IsNullOrWhiteSpace(t.Rodape)) sb.AppendLine("  " + t.Rodape);
        }
        return sb.ToString();
    }
}
