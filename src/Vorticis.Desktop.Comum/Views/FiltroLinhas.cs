using System.Globalization;
using System.Text;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Como o texto digitado no filtro de uma coluna é comparado com o conteúdo da célula.</summary>
public enum ModoTexto { Contem, ComecaCom, Igual }

/// <summary>Tipo do conteúdo de uma coluna, descoberto pelos valores das linhas (define quais campos o painel de filtros mostra).</summary>
public enum TipoDadoColuna { Texto, Numero, Data, SimNao }

/// <summary>Filtro de uma coluna. Os campos preenchidos se combinam (E).</summary>
public sealed class FiltroColuna
{
    public string Texto { get; set; } = "";
    public ModoTexto Modo { get; set; } = ModoTexto.Contem;
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public DateTime? De { get; set; }
    public DateTime? Ate { get; set; }
    public bool? SimNao { get; set; }
    /// <summary>Valores (como aparecem na célula) marcados na lista; nulo = todos.</summary>
    public HashSet<string>? Valores { get; set; }

    public bool Ativo => Texto.Trim().Length > 0 || Min is not null || Max is not null || De is not null || Ate is not null || SimNao is not null || Valores is not null;
}

/// <summary>Leitura de uma coluna da grade: valor bruto da linha e o texto como a célula mostra.</summary>
public sealed class LeitorColuna(string titulo, Func<object, object?> valor, string? formato = null)
{
    public string Titulo { get; } = titulo;
    /// <summary>Colunas ocultas ficam de fora da busca rápida.</summary>
    public Func<bool> Visivel { get; init; } = () => true;
    public object? Valor(object linha) => valor(linha);
    public string Texto(object linha) => FiltroLinhas.TextoDe(valor(linha), formato);
}

/// <summary>Estado dos filtros de uma lista: busca rápida + filtros por coluna (indexados pela posição da coluna).</summary>
public sealed class EstadoFiltro
{
    public string Busca { get; set; } = "";
    public Dictionary<int, FiltroColuna> Colunas { get; } = [];

    public int ColunasAtivas => Colunas.Values.Count(c => c.Ativo);
    public bool Ativo => Busca.Trim().Length > 0 || ColunasAtivas > 0;

    public void Limpar() { Busca = ""; Colunas.Clear(); }

    public FiltroColuna Coluna(int indice)
    {
        if (!Colunas.TryGetValue(indice, out var f)) Colunas[indice] = f = new FiltroColuna();
        return f;
    }

    /// <summary>Verdadeiro se a linha passa pela busca rápida e por todos os filtros de coluna.</summary>
    public bool Aceita(object linha, IReadOnlyList<LeitorColuna> leitores)
    {
        var termos = FiltroLinhas.Termos(Busca);
        if (termos.Length > 0)
        {
            var sb = new StringBuilder();
            foreach (var l in leitores)
            {
                if (!l.Visivel()) continue;
                var v = l.Valor(linha);
                sb.Append(FiltroLinhas.Normalizar(FiltroLinhas.TextoDe(v, null))).Append('\u001f');
                var exibido = l.Texto(linha);
                if (v is not string) sb.Append(FiltroLinhas.Normalizar(exibido)).Append('\u001f');
                if (v is decimal or double or float) sb.Append(FiltroLinhas.Normalizar(FiltroLinhas.TextoDe(v, null).Replace(".", ""))).Append('\u001f');
            }
            var texto = sb.ToString();
            foreach (var t in termos) if (!texto.Contains(t, StringComparison.Ordinal)) return false;
        }
        foreach (var (i, f) in Colunas)
        {
            if (!f.Ativo || i < 0 || i >= leitores.Count) continue;
            if (!AceitaColuna(f, leitores[i], linha)) return false;
        }
        return true;
    }

    private static bool AceitaColuna(FiltroColuna f, LeitorColuna leitor, object linha)
    {
        var bruto = leitor.Valor(linha);
        var exibido = leitor.Texto(linha);
        if (f.Texto.Trim().Length > 0)
        {
            var alvo = FiltroLinhas.Normalizar(exibido);
            var digitado = FiltroLinhas.Normalizar(f.Texto.Trim());
            var ok = f.Modo switch
            {
                ModoTexto.ComecaCom => alvo.StartsWith(digitado, StringComparison.Ordinal),
                ModoTexto.Igual => alvo == digitado,
                _ => alvo.Contains(digitado, StringComparison.Ordinal),
            };
            if (!ok) return false;
        }
        if (f.Min is not null || f.Max is not null)
        {
            if (!FiltroLinhas.TentarNumero(bruto, out var n)) return false;
            if (f.Min is { } mn && n < mn) return false;
            if (f.Max is { } mx && n > mx) return false;
        }
        if (f.De is not null || f.Ate is not null)
        {
            if (!FiltroLinhas.TentarData(bruto, out var d)) return false;
            if (f.De is { } de && d.Date < de.Date) return false;
            if (f.Ate is { } ate && d.Date > ate.Date) return false;
        }
        if (f.SimNao is { } sn)
        {
            if (!FiltroLinhas.TentarSimNao(bruto, out var b) || b != sn) return false;
        }
        if (f.Valores is { } vals && !vals.Contains(exibido)) return false;
        return true;
    }
}

/// <summary>Lógica pura dos filtros das listas (sem tela): normalização de texto, números, datas e tipo de coluna.</summary>
public static class FiltroLinhas
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] FormatosData = ["dd/MM/yyyy", "dd/MM/yyyy HH:mm", "dd/MM/yyyy HH:mm:ss", "dd/MM/yy"];

    /// <summary>Texto sem acentos e em minúsculas, para comparar sem diferenciar maiúsculas/acentos.</summary>
    public static string Normalizar(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    /// <summary>Termos da busca rápida (separados por espaço; todos precisam aparecer na linha).</summary>
    public static string[] Termos(string? busca)
        => Normalizar(busca).Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Texto de uma célula como o usuário vê (pt-BR).</summary>
    public static string TextoDe(object? v, string? formato)
    {
        if (v is null) return "";
        if (formato is not null)
        {
            try { return string.Format(PtBr, formato.Contains('{') ? formato : "{0:" + formato + "}", v); } catch (FormatException) { }
        }
        return v switch
        {
            string s => s,
            bool b => b ? "Sim" : "Não",
            DateTime dt => dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("dd/MM/yyyy", PtBr) : dt.ToString("dd/MM/yyyy HH:mm", PtBr),
            DateOnly d => d.ToString("dd/MM/yyyy", PtBr),
            DateTimeOffset o => o.ToString("dd/MM/yyyy HH:mm", PtBr),
            decimal m => m.ToString("N2", PtBr),
            double x => x.ToString("N2", PtBr),
            float f => f.ToString("N2", PtBr),
            IFormattable fm => fm.ToString(null, PtBr),
            _ => v.ToString() ?? "",
        };
    }

    public static bool TentarNumero(object? v, out decimal n)
    {
        n = 0;
        switch (v)
        {
            case null: return false;
            case decimal m: n = m; return true;
            case int or long or short or byte or sbyte or uint or ulong or ushort: n = Convert.ToDecimal(v, CultureInfo.InvariantCulture); return true;
            case double or float: try { n = Convert.ToDecimal(v, CultureInfo.InvariantCulture); return true; } catch (OverflowException) { return false; }
            case string s:
                s = s.Replace("R$", "").Replace("%", "").Trim();
                return s.Length > 0 && decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, PtBr, out n);
            default: return false;
        }
    }

    public static bool TentarData(object? v, out DateTime d)
    {
        d = default;
        switch (v)
        {
            case DateTime dt: d = dt; return true;
            case DateOnly @do: d = @do.ToDateTime(TimeOnly.MinValue); return true;
            case DateTimeOffset o: d = o.DateTime; return true;
            case string s: return DateTime.TryParseExact(s.Trim(), FormatosData, PtBr, DateTimeStyles.None, out d);
            default: return false;
        }
    }

    public static bool TentarSimNao(object? v, out bool b)
    {
        b = false;
        switch (v)
        {
            case bool x: b = x; return true;
            case string s:
                var n = Normalizar(s.Trim());
                if (n is "sim" or "s") { b = true; return true; }
                if (n is "nao" or "n") { b = false; return true; }
                return false;
            default: return false;
        }
    }

    /// <summary>Descobre o tipo da coluna pelos valores (vazios são ignorados; sem valores = texto).</summary>
    public static TipoDadoColuna DetectarTipo(IEnumerable<object?> valores)
    {
        bool num = true, data = true, sn = true, algum = false;
        foreach (var v in valores)
        {
            if (v is null || (v is string s0 && string.IsNullOrWhiteSpace(s0))) continue;
            algum = true;
            if (num && !TentarNumero(v, out _)) num = false;
            if (data && !TentarData(v, out _)) data = false;
            if (sn && !TentarSimNao(v, out _)) sn = false;
            if (!num && !data && !sn) break;
        }
        if (!algum) return TipoDadoColuna.Texto;
        if (sn) return TipoDadoColuna.SimNao;
        if (data) return TipoDadoColuna.Data;
        if (num) return TipoDadoColuna.Numero;
        return TipoDadoColuna.Texto;
    }

    /// <summary>Valores distintos (como aparecem na célula), ordenados; vazio vira "(vazio)" apenas na tela.</summary>
    public static List<string> ValoresDistintos(IEnumerable<object> linhas, LeitorColuna leitor, int limite)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var l in linhas)
        {
            set.Add(leitor.Texto(l));
            if (set.Count > limite) break;
        }
        var lista = set.ToList();
        lista.Sort(StringComparer.CurrentCultureIgnoreCase);
        return lista;
    }
}
