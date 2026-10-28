using System.Globalization;
using System.Text.RegularExpressions;
using Vorticis.Domain.Comum;

namespace Vorticis.Application.Relatorios;

public enum TipoPeriodoComparado { Mes, Trimestre, Ano, Intervalo }

/// <summary>
/// Período escolhido pelo usuário: mês, trimestre ou ano que contém <paramref name="Inicio"/>, ou o intervalo
/// <paramref name="Inicio"/> a <paramref name="Fim"/>.
/// </summary>
public sealed record PeriodoEscolhido(TipoPeriodoComparado Tipo, DateOnly Inicio, DateOnly? Fim = null);

public sealed record PeriodoRelatorio(string Rotulo, DateOnly Inicio, DateOnly Fim);

/// <summary>Opções do comparativo, iguais para todos os relatórios contábeis.</summary>
public sealed record OpcoesComparativo(bool PeriodoAnterior = false, bool MesmoPeriodoAnoAnterior = false, IReadOnlyList<PeriodoEscolhido>? Escolhidos = null,
                                       bool VariacaoAbsoluta = true, bool VariacaoPercentual = true, bool AnaliseVertical = false)
{
    public const int MaximoEscolhidos = 3;
    public IReadOnlyList<PeriodoEscolhido> Periodos => Escolhidos ?? [];
    public bool TemPeriodos => PeriodoAnterior || MesmoPeriodoAnoAnterior || Periodos.Count > 0;
}

/// <summary>Valor de uma linha do relatório em um período; <c>Valor</c> nulo = linha de título (sem valor).</summary>
/// <param name="Chave">Identifica a mesma linha nos vários períodos.</param>
/// <param name="Textos">Colunas de texto da linha (código, descrição...), na ordem das colunas.</param>
/// <param name="Percentual">O valor já é um percentual (margem): a variação é em pontos percentuais e não há variação % nem análise vertical.</param>
public sealed record LinhaComparavel(string Chave, IReadOnlyList<object?> Textos, decimal? Valor, bool Destaque = false, int Recuo = 0, bool Percentual = false);

/// <param name="BaseVertical">Total que vale 100% na análise vertical (receita, total do ativo...); nulo = sem análise vertical.</param>
public sealed record DadosPeriodo(PeriodoRelatorio Periodo, IReadOnlyList<LinhaComparavel> Linhas, decimal? BaseVertical = null);

/// <summary>
/// Comparativo central dos relatórios contábeis: define os períodos comparados e monta a tabela com uma coluna por período,
/// variação absoluta e percentual em relação ao período atual (atual − comparado) e, se pedido, a análise vertical (%).
/// Cada relatório só informa as suas linhas por período; o formato é sempre o mesmo.
/// </summary>
public static class Comparativo
{
    private static readonly CultureInfo PtBr = Formato.PtBr;

    public static string? Validar(OpcoesComparativo op)
    {
        if (!op.TemPeriodos) return "Comparativo: marque \"Período anterior\", \"Mesmo período do ano anterior\" ou escolha ao menos um período.";
        if (op.Periodos.Count > OpcoesComparativo.MaximoEscolhidos) return $"Comparativo: escolha no máximo {OpcoesComparativo.MaximoEscolhidos} períodos.";
        foreach (var p in op.Periodos)
            if (p.Tipo == TipoPeriodoComparado.Intervalo && (p.Fim is null || p.Fim < p.Inicio)) return "Comparativo: o intervalo escolhido tem data final anterior à inicial.";
        return null;
    }

    private static bool EhMesCompleto(DateOnly i, DateOnly f) => i.Day == 1 && f == UltimoDia(i.Year, i.Month) && i.Year == f.Year && i.Month == f.Month;
    private static DateOnly UltimoDia(int ano, int mes) => new(ano, mes, DateTime.DaysInMonth(ano, mes));
    private static DateOnly FimDoMes(DateOnly d) => UltimoDia(d.Year, d.Month);
    private static bool EhFimDeMes(DateOnly d) => d == FimDoMes(d);

    public static PeriodoRelatorio Resolver(PeriodoEscolhido e)
    {
        switch (e.Tipo)
        {
            case TipoPeriodoComparado.Mes:
            {
                var i = new DateOnly(e.Inicio.Year, e.Inicio.Month, 1);
                return Rotular(i, FimDoMes(i));
            }
            case TipoPeriodoComparado.Trimestre:
            {
                var m = (e.Inicio.Month - 1) / 3 * 3 + 1;
                var i = new DateOnly(e.Inicio.Year, m, 1);
                return Rotular(i, UltimoDia(i.Year, m + 2));
            }
            case TipoPeriodoComparado.Ano:
                return Rotular(new DateOnly(e.Inicio.Year, 1, 1), new DateOnly(e.Inicio.Year, 12, 31));
            default:
                return Rotular(e.Inicio, e.Fim ?? e.Inicio);
        }
    }

    /// <summary>Nome do período: "mar/2026", "1º tri/2026", "2026", "31/12/2026" (data única) ou o intervalo.</summary>
    public static PeriodoRelatorio Rotular(DateOnly i, DateOnly f)
    {
        string rotulo;
        if (i == new DateOnly(i.Year, 1, 1) && f == new DateOnly(i.Year, 12, 31)) rotulo = i.Year.ToString(PtBr);
        else if (i.Day == 1 && i.Month % 3 == 1 && f == UltimoDia(i.Year, i.Month + 2) && i.Year == f.Year) rotulo = $"{(i.Month - 1) / 3 + 1}º tri/{i.Year}";
        else if (EhMesCompleto(i, f)) rotulo = i.ToString("MMM/yyyy", PtBr).Replace(".", "");
        else if (i == f) rotulo = i.ToString("dd/MM/yyyy", PtBr);
        else rotulo = $"{i:dd/MM/yyyy} a {f:dd/MM/yyyy}";
        return new PeriodoRelatorio(rotulo, i, f);
    }

    /// <summary>Período imediatamente anterior, de mesma duração: mês, trimestre ou ano completos voltam um mês, trimestre ou ano.</summary>
    public static PeriodoRelatorio Anterior(DateOnly i, DateOnly f, bool pontual = false)
    {
        if (pontual)
        {
            var d = f.AddMonths(-1);
            if (EhFimDeMes(f)) d = FimDoMes(d);
            return Rotular(d, d);
        }
        if (i.Day == 1 && EhFimDeMes(f))
        {
            var meses = (f.Year - i.Year) * 12 + f.Month - i.Month + 1;
            var ni = i.AddMonths(-meses);
            return Rotular(ni, FimDoMes(i.AddMonths(-1)));
        }
        var dias = f.DayNumber - i.DayNumber + 1;
        return Rotular(i.AddDays(-dias), i.AddDays(-1));
    }

    /// <summary>Mesmo período do ano anterior (fim de mês continua fim de mês, como 28/02 e 29/02).</summary>
    public static PeriodoRelatorio AnoAnterior(DateOnly i, DateOnly f)
    {
        var fim = f.AddYears(-1);
        if (EhFimDeMes(f)) fim = FimDoMes(fim);
        return Rotular(i.AddYears(-1), fim);
    }

    /// <summary>Períodos do relatório: o atual primeiro, depois o anterior, o do ano anterior e os escolhidos (sem repetir intervalos).</summary>
    /// <param name="pontual">Relatório de uma data só (Balanço): só a data final importa.</param>
    public static IReadOnlyList<PeriodoRelatorio> Periodos(DateOnly inicio, DateOnly fim, OpcoesComparativo op, bool pontual = false)
    {
        if (pontual) inicio = fim;
        var lista = new List<PeriodoRelatorio> { Rotular(inicio, fim) };
        void Adicionar(PeriodoRelatorio p)
        {
            if (pontual) p = Rotular(p.Fim, p.Fim);
            if (lista.All(x => x.Inicio != p.Inicio || x.Fim != p.Fim)) lista.Add(p);
        }
        if (op.PeriodoAnterior) Adicionar(Anterior(inicio, fim, pontual));
        if (op.MesmoPeriodoAnoAnterior) Adicionar(AnoAnterior(inicio, fim));
        foreach (var e in op.Periodos.Take(OpcoesComparativo.MaximoEscolhidos)) Adicionar(Resolver(e));
        return lista;
    }

    /// <summary>Texto para o cabeçalho do relatório: "Comparativo com ...".</summary>
    public static string Descricao(IReadOnlyList<PeriodoRelatorio> periodos) =>
        periodos.Count < 2 ? "" : "Comparativo: " + string.Join(" × ", periodos.Select(p => p.Rotulo));

    private static readonly Regex Datas = new(@"\d{2}/\d{2}/\d{4}", RegexOptions.Compiled);

    /// <summary>Chave que não muda com as datas do texto ("Saldo em 31/12/2025" = "Saldo em 31/12/2026").</summary>
    public static string ChaveSemDatas(string texto) => Datas.Replace(texto, "{data}");

    public static decimal? VariacaoPercentual(decimal atual, decimal comparado) =>
        comparado == 0 ? null : Math.Round((atual - comparado) / Math.Abs(comparado) * 100, 2, MidpointRounding.AwayFromZero);

    /// <summary>Monta a tabela comparativa; o primeiro item de <paramref name="periodos"/> é o período atual.</summary>
    public static TabelaRelatorio Montar(string titulo, IReadOnlyList<ColunaRelatorio> colunasTexto, IReadOnlyList<DadosPeriodo> periodos,
                                         OpcoesComparativo op, string? rodape = null)
    {
        // Linhas unidas pela chave, na ordem do período atual; as que só existem em outros períodos entram depois da anterior.
        var unidas = new List<(string Chave, LinhaComparavel Modelo)>();
        foreach (var p in periodos)
        {
            var vistas = new Dictionary<string, int>();
            var ultima = -1;
            foreach (var l in p.Linhas)
            {
                vistas[l.Chave] = vistas.GetValueOrDefault(l.Chave) + 1;
                var chave = vistas[l.Chave] > 1 ? $"{l.Chave}#{vistas[l.Chave]}" : l.Chave;
                var pos = unidas.FindIndex(u => u.Chave == chave);
                if (pos < 0) { pos = ultima + 1; unidas.Insert(pos, (chave, l)); }
                ultima = pos;
            }
        }
        var valores = periodos.Select(p =>
        {
            var vistas = new Dictionary<string, int>();
            var d = new Dictionary<string, decimal?>();
            foreach (var l in p.Linhas)
            {
                vistas[l.Chave] = vistas.GetValueOrDefault(l.Chave) + 1;
                d[vistas[l.Chave] > 1 ? $"{l.Chave}#{vistas[l.Chave]}" : l.Chave] = l.Valor;
            }
            return d;
        }).ToList();

        var colunas = new List<ColunaRelatorio>(colunasTexto);
        for (var k = 0; k < periodos.Count; k++)
        {
            colunas.Add(new(periodos[k].Periodo.Rotulo, TipoColuna.Valor, 2.9));
            if (op.AnaliseVertical) colunas.Add(new("AV %", TipoColuna.Valor, 1.7));
            if (k == 0) continue;
            if (op.VariacaoAbsoluta) colunas.Add(new($"Var. R$ × {periodos[k].Periodo.Rotulo}", TipoColuna.Valor, 2.9));
            if (op.VariacaoPercentual) colunas.Add(new($"Var. % × {periodos[k].Periodo.Rotulo}", TipoColuna.Valor, 2.2));
        }

        var linhas = new List<LinhaRelatorio>();
        foreach (var (chave, modelo) in unidas)
        {
            var existe = valores.Select(v => v.TryGetValue(chave, out var x) ? x : null).ToList();
            var titulo_ = existe.All(v => v is null);
            var celulas = new List<object?>(modelo.Textos);
            decimal atual = existe[0] ?? 0;
            for (var k = 0; k < periodos.Count; k++)
            {
                var v = titulo_ ? (decimal?)null : existe[k] ?? 0;
                celulas.Add(v);
                if (op.AnaliseVertical)
                    celulas.Add(v is { } vv && !modelo.Percentual && periodos[k].BaseVertical is { } b && b != 0
                        ? Math.Round(vv / b * 100, 2, MidpointRounding.AwayFromZero) : null);
                if (k == 0) continue;
                var comp = v ?? 0;
                if (op.VariacaoAbsoluta) celulas.Add(titulo_ ? null : atual - comp);
                if (op.VariacaoPercentual) celulas.Add(titulo_ || modelo.Percentual ? null : VariacaoPercentual(atual, comp));
            }
            linhas.Add(new LinhaRelatorio(celulas, modelo.Destaque, modelo.Recuo));
        }
        var nota = "Variação = período atual − período comparado; Var. % = variação ÷ valor comparado." + (op.AnaliseVertical ? " AV % = participação no total de referência do relatório." : "");
        return new TabelaRelatorio(titulo, colunas, linhas, string.IsNullOrWhiteSpace(rodape) ? nota : rodape + " " + nota);
    }
}
