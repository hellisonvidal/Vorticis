namespace Vorticis.Domain.Contabil;

/// <summary>Partida de lançamento (não rascunho, sem encerramento) com todas as dimensões de análise. Centro e participante 0 = sem.</summary>
public sealed record PartidaBi(long LancamentoId, long Numero, DateOnly Data, long ContaId, long CentroId, long ParticipanteId, IndicadorDC DC, decimal Valor, string Historico)
{
    /// <summary>Valor com sinal: débito positivo, crédito negativo.</summary>
    public decimal Assinado => DC == IndicadorDC.D ? Valor : -Valor;
}

public sealed record ParticipanteBi(long Id, string Codigo, string Nome);

/// <summary>Tudo o que o cubo e a exportação para BI precisam, carregado uma vez.</summary>
public sealed record DadosBi(IReadOnlyList<ContaPlano> Plano, IReadOnlyList<CentroPlano> Centros, IReadOnlyList<ParticipanteBi> Participantes, IReadOnlyList<PartidaBi> Partidas);

public enum DimensaoCubo { Conta, Grupo, CentroCusto, Participante, Mes, Trimestre, Ano, Nenhuma }

/// <summary>Medida somada em cada célula: débitos, créditos, saldo (débitos − créditos) ou resultado (créditos − débitos).</summary>
public enum MedidaCubo { Saldo, Resultado, Debitos, Creditos }

public sealed record ConsultaCubo(DimensaoCubo Linha, DimensaoCubo Coluna, MedidaCubo Medida = MedidaCubo.Saldo, string? ContaPrefixo = null, bool SoResultado = false);

public sealed record LinhaCubo(string Chave, string Rotulo, IReadOnlyList<decimal> Valores, decimal Total);

public sealed record ColunaCubo(string Chave, string Rotulo);

public sealed record CuboResultado(string TituloLinha, IReadOnlyList<ColunaCubo> Colunas, IReadOnlyList<LinhaCubo> Linhas, IReadOnlyList<decimal> TotaisColunas, decimal Total);

/// <summary>Uma partida listada ao clicar numa célula do cubo (drill-down).</summary>
public sealed record DetalheCubo(DateOnly Data, long Numero, string Conta, string Centro, string Participante, IndicadorDC DC, decimal Valor, string Historico);

/// <summary>Cubo (tabela dinâmica) simples sobre as partidas: uma dimensão nas linhas, outra nas colunas, soma de uma medida.</summary>
public static class Cubo
{
    public static string Nome(DimensaoCubo d) => d switch
    {
        DimensaoCubo.Conta => "Conta", DimensaoCubo.Grupo => "Grupo de contas", DimensaoCubo.CentroCusto => "Centro de custo",
        DimensaoCubo.Participante => "Participante", DimensaoCubo.Mes => "Mês", DimensaoCubo.Trimestre => "Trimestre", DimensaoCubo.Ano => "Ano", _ => "(nenhuma)",
    };

    public static string Nome(MedidaCubo m) => m switch
    {
        MedidaCubo.Saldo => "Saldo (débitos − créditos)", MedidaCubo.Resultado => "Resultado (créditos − débitos)",
        MedidaCubo.Debitos => "Débitos", _ => "Créditos",
    };

    private sealed class Contexto(DadosBi dados)
    {
        public readonly Dictionary<long, ContaPlano> Contas = dados.Plano.ToDictionary(c => c.Id);
        public readonly Dictionary<long, CentroPlano> Centros = dados.Centros.ToDictionary(c => c.Id);
        public readonly Dictionary<long, ParticipanteBi> Participantes = dados.Participantes.ToDictionary(p => p.Id);
    }

    private static (string Chave, string Rotulo) Valor(Contexto x, PartidaBi p, DimensaoCubo d)
    {
        switch (d)
        {
            case DimensaoCubo.Conta:
                return x.Contas.TryGetValue(p.ContaId, out var c) ? (c.Codigo, $"{c.Codigo} — {c.Descricao}") : ("?", "Conta não encontrada");
            case DimensaoCubo.Grupo:
                if (!x.Contas.TryGetValue(p.ContaId, out var cg)) return ("?", "Conta não encontrada");
                var g = CalculoBi.Grupo(cg, x.Contas);
                return (g.Codigo, $"{g.Codigo} — {g.Descricao}");
            case DimensaoCubo.CentroCusto:
                return p.CentroId != 0 && x.Centros.TryGetValue(p.CentroId, out var cc) ? (cc.Codigo, $"{cc.Codigo} — {cc.Descricao}") : ("~", Gerenciais.SemCentro);
            case DimensaoCubo.Participante:
                return p.ParticipanteId != 0 && x.Participantes.TryGetValue(p.ParticipanteId, out var pa) ? (pa.Codigo, $"{pa.Codigo} — {pa.Nome}") : ("~", "Sem participante");
            case DimensaoCubo.Mes: return ($"{p.Data:yyyy-MM}", $"{p.Data:MM/yyyy}");
            case DimensaoCubo.Trimestre:
                var t = (p.Data.Month - 1) / 3 + 1;
                return ($"{p.Data.Year}-T{t}", $"{t}º tri/{p.Data.Year}");
            case DimensaoCubo.Ano: return ($"{p.Data.Year}", $"{p.Data.Year}");
            default: return ("", "Total");
        }
    }

    private static IEnumerable<PartidaBi> Filtrar(Contexto x, DadosBi dados, ConsultaCubo q)
    {
        var prefixo = string.IsNullOrWhiteSpace(q.ContaPrefixo) ? null : q.ContaPrefixo.Trim();
        foreach (var p in dados.Partidas)
        {
            if (!x.Contas.TryGetValue(p.ContaId, out var c)) continue;
            if (q.SoResultado && c.CodNat != "04") continue;
            if (prefixo is not null && !c.Codigo.StartsWith(prefixo, StringComparison.Ordinal)) continue;
            yield return p;
        }
    }

    private static decimal Medir(PartidaBi p, MedidaCubo m) => m switch
    {
        MedidaCubo.Debitos => p.DC == IndicadorDC.D ? p.Valor : 0,
        MedidaCubo.Creditos => p.DC == IndicadorDC.C ? p.Valor : 0,
        MedidaCubo.Resultado => -p.Assinado,
        _ => p.Assinado,
    };

    public static CuboResultado Montar(DadosBi dados, ConsultaCubo q)
    {
        var x = new Contexto(dados);
        var itens = Filtrar(x, dados, q).Select(p => (L: Valor(x, p, q.Linha), C: Valor(x, p, q.Coluna), V: Medir(p, q.Medida))).ToList();
        var colunas = q.Coluna == DimensaoCubo.Nenhuma ? []
            : itens.Select(i => i.C).Distinct().OrderBy(c => c.Chave, StringComparer.Ordinal).Select(c => new ColunaCubo(c.Chave, c.Rotulo)).ToList();
        var indice = colunas.Select((c, i) => (c.Chave, i)).ToDictionary(a => a.Chave, a => a.i);
        var linhas = new List<LinhaCubo>();
        foreach (var g in itens.GroupBy(i => i.L.Chave).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var vals = new decimal[colunas.Count];
            foreach (var i in g) if (q.Coluna != DimensaoCubo.Nenhuma) vals[indice[i.C.Chave]] += i.V;
            linhas.Add(new LinhaCubo(g.Key, g.First().L.Rotulo, vals, g.Sum(i => i.V)));
        }
        var totais = Enumerable.Range(0, colunas.Count).Select(k => linhas.Sum(l => l.Valores[k])).ToList();
        return new CuboResultado(Nome(q.Linha), colunas, linhas, totais, linhas.Sum(l => l.Total));
    }

    /// <summary>Partidas que formam uma célula (<paramref name="linha"/> e <paramref name="coluna"/>; nulo = todas as linhas / todas as colunas).</summary>
    public static IReadOnlyList<DetalheCubo> Detalhe(DadosBi dados, ConsultaCubo q, string? linha, string? coluna)
    {
        var x = new Contexto(dados);
        return Filtrar(x, dados, q)
            .Where(p => (linha is null || Valor(x, p, q.Linha).Chave == linha) && (coluna is null || q.Coluna == DimensaoCubo.Nenhuma || Valor(x, p, q.Coluna).Chave == coluna))
            .OrderBy(p => p.Data).ThenBy(p => p.Numero)
            .Select(p => new DetalheCubo(p.Data, p.Numero, x.Contas.TryGetValue(p.ContaId, out var c) ? $"{c.Codigo} {c.Descricao}" : "?",
                                         p.CentroId != 0 && x.Centros.TryGetValue(p.CentroId, out var cc) ? $"{cc.Codigo} {cc.Descricao}" : "",
                                         p.ParticipanteId != 0 && x.Participantes.TryGetValue(p.ParticipanteId, out var pa) ? $"{pa.Codigo} {pa.Nome}" : "",
                                         p.DC, p.Valor, p.Historico))
            .ToList();
    }
}
