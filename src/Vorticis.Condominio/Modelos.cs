using Vorticis.Domain.Comum;

namespace Vorticis.Condominio;

public enum Rateio { Fracao, Igual, Fixo }

public sealed record ConfiguracaoCondominio(int DiaVencimento = 10, decimal MultaPercentual = 2, decimal JurosMensal = 1);

public sealed record Unidade(long Id, string Bloco, string Numero, string Tipo, decimal FracaoIdeal, decimal? AreaM2, long? ProprietarioId, long? InquilinoId,
                             bool CobrarInquilino, bool Ativa = true, string? ProprietarioNome = null, string? InquilinoNome = null)
{
    public string Identificacao => string.IsNullOrWhiteSpace(Bloco) ? Numero : $"{Bloco}-{Numero}";
    public string? Responsavel => CobrarInquilino && InquilinoNome is not null ? InquilinoNome : ProprietarioNome;
}

public sealed record Taxa(long Id, string Descricao, Rateio Rateio, decimal Valor, DateOnly Inicio, DateOnly? Fim, bool Ativa = true)
{
    public bool Vigente(DateOnly competencia) => Ativa && new DateOnly(Inicio.Year, Inicio.Month, 1) <= competencia && (Fim is null || Fim >= competencia);
}

public sealed record ItemCobranca(long? TaxaId, string Descricao, decimal Valor);

public sealed record Cobranca(long Id, long UnidadeId, string Unidade, DateOnly Competencia, DateOnly Vencimento, decimal Valor, string Situacao, DateOnly? PagoEm,
                              decimal Multa, decimal Juros, decimal Desconto, decimal? ValorPago, string? Responsavel, IReadOnlyList<ItemCobranca> Itens);

public sealed record Despesa(long Id, DateOnly Data, DateOnly Vencimento, long? FornecedorId, string Categoria, string Descricao, decimal Valor, DateOnly? PagoEm,
                             string? FornecedorNome = null);

public sealed record AreaComum(long Id, string Nome, int Capacidade, decimal TaxaReserva, bool Ativa = true);
public sealed record Reserva(long Id, long AreaId, string Area, long UnidadeId, string Unidade, DateOnly Data, string Situacao, string? Observacao);
public sealed record Ocorrencia(long Id, long? UnidadeId, string? Unidade, DateOnly Data, string Tipo, string Titulo, string? Descricao, string Situacao, DateOnly? ResolvidaEm);

public sealed record PrestacaoContas(DateOnly Mes, decimal Arrecadado, decimal EncargosRecebidos, IReadOnlyList<(string Categoria, decimal Valor)> Despesas,
                                     decimal Emitido, decimal Inadimplencia)
{
    public decimal TotalDespesas => Despesas.Sum(d => d.Valor);
    public decimal Saldo => Arrecadado + EncargosRecebidos - TotalDespesas;
}

/// <summary>Regras puras do condomínio: rateio das taxas e encargos de atraso (Código Civil, art. 1.336, § 1º).</summary>
public static class RegrasCondominio
{
    public static DateOnly Mes(DateOnly d) => new(d.Year, d.Month, 1);

    /// <summary>
    /// Valor de cada unidade numa taxa: pela fração ideal (o resto do arredondamento vai para a maior fração), em partes iguais (resto na primeira)
    /// ou fixo por unidade.
    /// </summary>
    public static IReadOnlyDictionary<long, decimal> Ratear(Taxa taxa, IReadOnlyList<Unidade> unidades)
    {
        var ativas = unidades.Where(u => u.Ativa).ToList();
        var res = new Dictionary<long, decimal>();
        if (ativas.Count == 0) return res;
        switch (taxa.Rateio)
        {
            case Rateio.Fixo:
                foreach (var u in ativas) res[u.Id] = taxa.Valor;
                return res;
            case Rateio.Igual:
                var parte = Math.Floor(taxa.Valor / ativas.Count * 100) / 100;
                foreach (var u in ativas) res[u.Id] = parte;
                res[ativas[0].Id] += taxa.Valor - parte * ativas.Count;
                return res;
            default:
                var soma = ativas.Sum(u => u.FracaoIdeal);
                foreach (var u in ativas) res[u.Id] = Dinheiro.Arredondar(taxa.Valor * u.FracaoIdeal / soma);
                var maior = ativas.OrderByDescending(u => u.FracaoIdeal).First();
                res[maior.Id] += taxa.Valor - res.Values.Sum();
                return res;
        }
    }

    /// <summary>Composição da cobrança de cada unidade na competência, pelas taxas vigentes.</summary>
    public static IReadOnlyDictionary<long, List<ItemCobranca>> Compor(IReadOnlyList<Taxa> taxas, IReadOnlyList<Unidade> unidades, DateOnly competencia)
    {
        var res = unidades.Where(u => u.Ativa).ToDictionary(u => u.Id, _ => new List<ItemCobranca>());
        foreach (var t in taxas.Where(t => t.Vigente(Mes(competencia))))
            foreach (var (id, valor) in Ratear(t, unidades))
                if (valor != 0) res[id].Add(new ItemCobranca(t.Id, t.Descricao, valor));
        return res;
    }

    /// <summary>Multa (limitada ao máximo legal) e juros simples pro rata dia sobre o valor, para pagamento após o vencimento.</summary>
    public static (decimal Multa, decimal Juros) Encargos(decimal valor, DateOnly vencimento, DateOnly pagamento, ConfiguracaoCondominio cfg, decimal multaMaxima)
    {
        if (pagamento <= vencimento) return (0, 0);
        var multa = Dinheiro.Arredondar(valor * Math.Min(cfg.MultaPercentual, multaMaxima) / 100m);
        var dias = pagamento.DayNumber - vencimento.DayNumber;
        var juros = Dinheiro.Arredondar(valor * cfg.JurosMensal / 100m * dias / 30m);
        return (multa, juros);
    }

    public static DateOnly Vencimento(DateOnly competencia, int dia) => new(competencia.Year, competencia.Month, Math.Min(dia, DateTime.DaysInMonth(competencia.Year, competencia.Month)));

    public static string Sql(this Rateio r) => r switch { Rateio.Igual => "IGUAL", Rateio.Fixo => "FIXO", _ => "FRACAO" };
    public static Rateio RateioDe(string s) => s switch { "IGUAL" => Rateio.Igual, "FIXO" => Rateio.Fixo, _ => Rateio.Fracao };
    public static string Rotulo(this Rateio r) => r switch { Rateio.Igual => "Partes iguais", Rateio.Fixo => "Valor fixo por unidade", _ => "Fração ideal" };
}
