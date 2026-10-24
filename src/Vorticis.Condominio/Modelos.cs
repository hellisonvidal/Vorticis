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

    /// <summary>Prévia de um rateio avulso (despesa extra, obra, fundo) entre as unidades ativas, pela fração ideal ou em partes iguais.</summary>
    public static IReadOnlyList<ParcelaRateio> RatearValor(decimal valor, Rateio criterio, IReadOnlyList<Unidade> unidades)
    {
        var valores = Ratear(new Taxa(0, "Rateio", criterio == Rateio.Fixo ? Rateio.Igual : criterio, valor, DateOnly.MinValue, null), unidades);
        return unidades.Where(u => u.Ativa && valores.ContainsKey(u.Id)).OrderBy(u => u.Bloco).ThenBy(u => u.Numero.PadLeft(10, '0'))
                       .Select(u => new ParcelaRateio(u.Id, u.Identificacao, u.Responsavel, u.FracaoIdeal, valores[u.Id])).ToList();
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

public enum TipoAssembleia { Ordinaria, Extraordinaria }

/// <summary>Assembleia do condomínio: mesa (presidente e secretário), pauta (ordem do dia) e deliberações; numerada por condomínio.</summary>
public sealed record Assembleia(long Id, int Numero, DateOnly Data, string Horario, TipoAssembleia Tipo, int Convocacao, string Local, string Presidente,
                                string Secretario, int UnidadesPresentes, string Pauta, string Deliberacoes)
{
    public string TipoTexto => Tipo == TipoAssembleia.Extraordinaria ? "Extraordinária" : "Ordinária";
}

/// <summary>Valor de uma unidade num rateio (prévia antes de lançar nas cobranças).</summary>
public sealed record ParcelaRateio(long UnidadeId, string Unidade, string? Responsavel, decimal Fracao, decimal Valor);

/// <summary>
/// Ata de assembleia (Código Civil, arts. 1.350 a 1.355): abertura com data, local, convocação e mesa, ordem do dia, deliberações,
/// encerramento e o bloco de assinaturas (presidente, secretário e síndico), com a lista de presença opcional (uma linha por unidade).
/// </summary>
public static class AtaAssembleia
{
    public static IReadOnlyList<string> Validar(Assembleia a)
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(a.Local)) erros.Add("Informe o local da assembleia.");
        if (string.IsNullOrWhiteSpace(a.Presidente) || string.IsNullOrWhiteSpace(a.Secretario)) erros.Add("Informe o presidente e o secretário da mesa.");
        if (string.IsNullOrWhiteSpace(a.Pauta)) erros.Add("Informe a pauta (ordem do dia).");
        if (!System.Text.RegularExpressions.Regex.IsMatch(a.Horario ?? "", @"^[0-2]\d:[0-5]\d$")) erros.Add("Horário no formato HH:MM (ex.: 19:30).");
        if (a.Convocacao is not (1 or 2)) erros.Add("Convocação: primeira ou segunda.");
        if (a.UnidadesPresentes < 0) erros.Add("Unidades presentes não pode ser negativo.");
        return erros;
    }

    public static string Titulo(Assembleia a) => $"ATA DA ASSEMBLEIA GERAL {(a.Tipo == TipoAssembleia.Extraordinaria ? "EXTRAORDINÁRIA" : "ORDINÁRIA")} Nº {a.Numero}";

    public static Vorticis.Application.Relatorios.DocumentoRelatorio Documento(Assembleia a, string condominio, string? cnpj, IReadOnlyList<Unidade> unidades,
                                                                              string? sindico, bool listaPresenca)
    {
        var ativas = unidades.Where(u => u.Ativa).ToList();
        var abertura = $"Aos {a.Data:dd/MM/yyyy}, às {a.Horario}, em {a.Local.Trim()}, reuniram-se em {(a.Convocacao == 2 ? "segunda" : "primeira")} convocação " +
                       $"os condôminos do {condominio}" + (string.IsNullOrWhiteSpace(cnpj) ? "" : $", CNPJ {cnpj}") +
                       $", em Assembleia Geral {a.TipoTexto}, com a presença de {a.UnidadesPresentes} de {ativas.Count} unidade(s), conforme a lista de presença. " +
                       $"A mesa foi composta por {a.Presidente.Trim()}, como presidente, e {a.Secretario.Trim()}, como secretário(a).";
        var encerramento = "Nada mais havendo a tratar, o presidente encerrou a assembleia, e eu, secretário(a), lavrei a presente ata, que, lida e achada conforme, " +
                           "vai assinada por mim, pelo presidente e pelo síndico.";
        var blocos = new List<Vorticis.Application.Relatorios.Bloco>
        {
            new(Texto: abertura),
            new(Titulo: "Ordem do dia", Texto: a.Pauta.Trim()),
            new(Titulo: "Deliberações", Texto: string.IsNullOrWhiteSpace(a.Deliberacoes) ? "(sem deliberações registradas)" : a.Deliberacoes.Trim()),
            new(Texto: encerramento),
        };
        if (listaPresenca && ativas.Count > 0)
            blocos.Add(new(Titulo: "Lista de presença", NovaPagina: true,
                Colunas: [new("Unidade", 2.5), new("Condômino", 8), new("Assinatura", 7.5)],
                Linhas: ativas.OrderBy(u => u.Bloco).ThenBy(u => u.Numero.PadLeft(10, '0'))
                              .Select(u => new Vorticis.Application.Relatorios.LinhaTabela([u.Identificacao, u.ProprietarioNome ?? "", "\n\n______________________________"])).ToList()));
        var assinaturas = new List<string> { $"{a.Presidente.Trim()}\nPresidente da mesa", $"{a.Secretario.Trim()}\nSecretário(a)" };
        if (!string.IsNullOrWhiteSpace(sindico)) assinaturas.Add($"{sindico.Trim()}\nSíndico(a)");
        return new Vorticis.Application.Relatorios.DocumentoRelatorio(Titulo(a), [condominio + (string.IsNullOrWhiteSpace(cnpj) ? "" : $" — CNPJ {cnpj}")],
                                                                      blocos, Assinaturas: assinaturas);
    }
}
