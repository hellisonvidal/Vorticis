using Vorticis.Domain.Comum;
using System.Text.Json;

namespace Vorticis.Folha.Regras;

/// <summary>Faixa do INSS do empregado: alíquota (%) aplicada à parcela da base até o limite <paramref name="Ate"/>.</summary>
public sealed record FaixaInss(decimal Ate, decimal Aliquota);

public sealed record TabelaInss(IReadOnlyList<FaixaInss> Faixas)
{
    /// <summary>Teto do salário de contribuição (limite da última faixa).</summary>
    public decimal Teto => Faixas[^1].Ate;
}

/// <summary>Faixa da tabela progressiva do IRRF. <paramref name="Ate"/> nulo = faixa final (sem limite).</summary>
public sealed record FaixaIrrf(decimal? Ate, decimal Aliquota, decimal Deduzir);

/// <summary>
/// Redução do IRRF da Lei 15.270/2025: até <see cref="LimiteIsencao"/> de rendimento a redução vai até
/// <see cref="ReducaoMaxima"/> (limitada ao imposto, o que zera o IRRF); acima disso e até <see cref="LimiteSuperior"/>,
/// redução de <see cref="Constante"/> - <see cref="Coeficiente"/> x rendimento tributável.
/// </summary>
public sealed record ReducaoIrrf(decimal LimiteIsencao, decimal ReducaoMaxima, decimal LimiteSuperior, decimal Constante, decimal Coeficiente);

public sealed record TabelaIrrf(IReadOnlyList<FaixaIrrf> Faixas, decimal DeducaoDependente, decimal DescontoSimplificado, ReducaoIrrf? Reducao);

public sealed record TabelaSalarioFamilia(decimal LimiteRemuneracao, decimal Cota);

/// <summary>Alíquotas do FGTS (%): depósito mensal, aprendiz e multa rescisória.</summary>
public sealed record ParametrosFgts(decimal Aliquota, decimal AliquotaAprendiz, decimal MultaSemJustaCausa, decimal MultaAcordo);

/// <summary>Linha de <c>core.parametro_legal</c> (ou do arquivo embutido) de uma chave FOLHA_*.</summary>
public sealed record ParametroLegal(string Chave, DateOnly VigenciaInicio, DateOnly? VigenciaFim, string Valor, string Fundamento);

/// <summary>Conjunto de tabelas legais vigentes em uma data.</summary>
public sealed record TabelasLegais(TabelaInss Inss, TabelaIrrf Irrf, TabelaSalarioFamilia SalarioFamilia, decimal SalarioMinimo, ParametrosFgts Fgts)
{
    public const string ChaveInss = "FOLHA_INSS_EMPREGADO";
    public const string ChaveIrrf = "FOLHA_IRRF_MENSAL";
    public const string ChaveSalarioFamilia = "FOLHA_SALARIO_FAMILIA";
    public const string ChaveSalarioMinimo = "FOLHA_SALARIO_MINIMO";
    public const string ChaveFgts = "FOLHA_FGTS";

    public static readonly string[] Chaves = [ChaveInss, ChaveIrrf, ChaveSalarioFamilia, ChaveSalarioMinimo, ChaveFgts];

    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    /// <summary>Escolhe, para cada tabela, a vigência que cobre <paramref name="data"/>.</summary>
    public static TabelasLegais Em(DateOnly data, IEnumerable<ParametroLegal> parametros)
    {
        var lista = parametros.ToList();
        ParametroLegal Vigente(string chave) =>
            lista.Where(p => p.Chave == chave && p.VigenciaInicio <= data && (p.VigenciaFim is null || p.VigenciaFim >= data))
                 .OrderByDescending(p => p.VigenciaInicio).FirstOrDefault()
            ?? throw new InvalidOperationException($"Não há tabela legal {chave} vigente em {data:dd/MM/yyyy}. Cadastre a vigência em core.parametro_legal.");

        var inss = JsonSerializer.Deserialize<DtoInss>(Vigente(ChaveInss).Valor, Opcoes)!;
        var irrf = JsonSerializer.Deserialize<DtoIrrf>(Vigente(ChaveIrrf).Valor, Opcoes)!;
        var sf = JsonSerializer.Deserialize<DtoSalarioFamilia>(Vigente(ChaveSalarioFamilia).Valor, Opcoes)!;
        var min = JsonSerializer.Deserialize<DtoValor>(Vigente(ChaveSalarioMinimo).Valor, Opcoes)!;
        var fgts = JsonSerializer.Deserialize<DtoFgts>(Vigente(ChaveFgts).Valor, Opcoes)!;
        return new TabelasLegais(
            new TabelaInss(inss.Faixas.Select(f => new FaixaInss(f.Ate, f.Aliquota)).ToArray()),
            new TabelaIrrf(irrf.Faixas.Select(f => new FaixaIrrf(f.Ate, f.Aliquota, f.Deduzir)).ToArray(), irrf.DeducaoDependente,
                           irrf.DescontoSimplificado, irrf.Reducao),
            new TabelaSalarioFamilia(sf.LimiteRemuneracao, sf.Cota), min.Valor,
            new ParametrosFgts(fgts.Aliquota, fgts.AliquotaAprendiz, fgts.MultaSemJustaCausa, fgts.MultaAcordo));
    }

    /// <summary>
    /// Tabelas embutidas (Dados/tabelas_legais.json): as mesmas vigências gravadas pela migração 040, usadas em testes
    /// e como referência. O aplicativo lê sempre do banco.
    /// </summary>
    public static IReadOnlyList<ParametroLegal> Embutidas()
    {
        using var s = typeof(TabelasLegais).Assembly.GetManifestResourceStream("Folha.tabelas_legais.json")
                      ?? throw new InvalidOperationException("Recurso tabelas_legais.json não embutido.");
        var itens = JsonSerializer.Deserialize<DtoParametro[]>(s, Opcoes)!;
        return itens.Select(i => new ParametroLegal(i.Chave, DateOnly.Parse(i.VigenciaInicio),
                                                    i.VigenciaFim is null ? null : DateOnly.Parse(i.VigenciaFim), i.Valor.GetRawText(), i.Fundamento)).ToArray();
    }

    private sealed record DtoParametro(string Chave, string VigenciaInicio, string? VigenciaFim, JsonElement Valor, string Fundamento);
    private sealed record DtoFaixaInss(decimal Ate, decimal Aliquota);
    private sealed record DtoInss(DtoFaixaInss[] Faixas);
    private sealed record DtoFaixaIrrf(decimal? Ate, decimal Aliquota, decimal Deduzir);
    private sealed record DtoIrrf(DtoFaixaIrrf[] Faixas, decimal DeducaoDependente, decimal DescontoSimplificado, ReducaoIrrf? Reducao);
    private sealed record DtoSalarioFamilia(decimal LimiteRemuneracao, decimal Cota);
    private sealed record DtoValor(decimal Valor);
    private sealed record DtoFgts(decimal Aliquota, decimal AliquotaAprendiz, decimal MultaSemJustaCausa, decimal MultaAcordo);
}

/// <summary>Cadastros › Tabelas da previdência social: as tabelas legais da folha vigentes na competência, para consulta e impressão.</summary>
public static class TabelasPrevidencia
{
    public const string Titulo = "Tabelas da previdência social";

    public static Vorticis.Application.Relatorios.TabelaRelatorio Montar(DateOnly competencia, IReadOnlyList<ParametroLegal> parametros)
    {
        var t = TabelasLegais.Em(competencia, parametros);
        string Pct(decimal v) => $"{Formato.Num(v)}%";
        string Vig(string chave) => parametros.Where(p => p.Chave == chave && p.VigenciaInicio <= competencia && (p.VigenciaFim is null || p.VigenciaFim >= competencia))
                                              .OrderByDescending(p => p.VigenciaInicio).Select(p => $"desde {p.VigenciaInicio:dd/MM/yyyy}").FirstOrDefault() ?? "";
        var linhas = new List<Vorticis.Application.Relatorios.LinhaRelatorio>();
        void L(string tabela, string item, string valor, bool destaque = false) => linhas.Add(new([tabela, item, valor], destaque));
        decimal anterior = 0;
        for (var i = 0; i < t.Inss.Faixas.Count; i++)
        {
            var f = t.Inss.Faixas[i];
            L(i == 0 ? $"INSS do empregado ({Vig(TabelasLegais.ChaveInss)})" : "", $"{i + 1}ª faixa: de {Formato.Moeda(anterior == 0 ? 0 : anterior + 0.01m)} a {Formato.Moeda(f.Ate)}", Pct(f.Aliquota), i == 0);
            anterior = f.Ate;
        }
        L("", "Teto do salário de contribuição", Formato.Moeda(t.Inss.Teto));
        L($"Salário-família ({Vig(TabelasLegais.ChaveSalarioFamilia)})", "Remuneração até", Formato.Moeda(t.SalarioFamilia.LimiteRemuneracao), true);
        L("", "Cota por filho ou equiparado", Formato.Moeda(t.SalarioFamilia.Cota));
        L($"Salário mínimo ({Vig(TabelasLegais.ChaveSalarioMinimo)})", "Valor mensal", Formato.Moeda(t.SalarioMinimo), true);
        L($"FGTS ({Vig(TabelasLegais.ChaveFgts)})", "Depósito mensal", Pct(t.Fgts.Aliquota), true);
        L("", "Depósito do aprendiz", Pct(t.Fgts.AliquotaAprendiz));
        L("", "Multa na dispensa sem justa causa", Pct(t.Fgts.MultaSemJustaCausa));
        L("", "Multa na rescisão por acordo", Pct(t.Fgts.MultaAcordo));
        for (var i = 0; i < t.Irrf.Faixas.Count; i++)
        {
            var f = t.Irrf.Faixas[i];
            L(i == 0 ? $"IRRF mensal ({Vig(TabelasLegais.ChaveIrrf)})" : "", f.Ate is { } ate ? $"Até {Formato.Moeda(ate)}" : "Acima da faixa anterior",
              f.Aliquota == 0 ? "isento" : $"{Pct(f.Aliquota)} (deduzir {Formato.Moeda(f.Deduzir)})", i == 0);
        }
        L("", "Dedução por dependente", Formato.Moeda(t.Irrf.DeducaoDependente));
        L("", "Desconto simplificado", Formato.Moeda(t.Irrf.DescontoSimplificado));
        var fundamentos = parametros.Where(p => TabelasLegais.Chaves.Contains(p.Chave) && p.VigenciaInicio <= competencia && (p.VigenciaFim is null || p.VigenciaFim >= competencia))
                                    .Select(p => p.Fundamento).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct();
        return new(Titulo, [new("Tabela", Vorticis.Application.Relatorios.TipoColuna.Texto, 6), new("Item", Vorticis.Application.Relatorios.TipoColuna.Texto, 8),
                            new("Valor", Vorticis.Application.Relatorios.TipoColuna.Texto, 4)], linhas,
                   $"Vigentes em {competencia:MM/yyyy}. As tabelas são atualizadas com o programa (parâmetros legais) e usadas em todos os cálculos da folha. Fundamento: {string.Join("; ", fundamentos)}.");
    }
}
