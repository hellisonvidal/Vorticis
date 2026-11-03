using Vorticis.Domain.Comum;

namespace Vorticis.Domain.Contabil;

/// <summary>Conta a converter: só o que importa para recodificar (o identificador pode ser 0 para modelos).</summary>
public sealed record ContaParaConverter(long Id, string Codigo);

/// <summary>Uma linha da prévia: código atual → código na nova máscara. <see cref="Aviso"/> não impede a conversão.</summary>
public sealed record ItemConversao(long Id, string Antigo, string Novo, string? Aviso = null)
{
    public bool Muda => Antigo != Novo;
}

/// <summary>Resultado da simulação: itens convertidos e/ou os problemas que impedem a conversão (<see cref="Erros"/>).</summary>
public sealed record PreviaConversao(string Mascara, IReadOnlyList<ItemConversao> Itens, IReadOnlyList<string> Erros)
{
    public bool Possivel => Erros.Count == 0;
    public int Mudancas => Itens.Count(i => i.Muda);
    public int Avisos => Itens.Count(i => i.Aviso is not null);
}

/// <summary>
/// Conversão do plano de contas para outra máscara (ex.: 9.9.99.999 → 9.99.999.9999). Regras, nível a nível: o código é
/// completado com zeros à esquerda até a largura do nível, ou tem os zeros à esquerda retirados (001 → 1) quando a largura
/// diminui — nesse caso a conta recebe um aviso. A conversão só é possível quando (1) todo nível cabe na largura nova, (2) o
/// número de níveis da conta não muda, (3) nenhum código novo se repete e (4) cada conta continua abaixo da mesma superior.
/// Se qualquer regra falhar, nada é convertido (conversão ambígua é recusada, nunca adivinhada).
/// </summary>
public static class ConversaoPlano
{
    public static PreviaConversao Simular(IEnumerable<ContaParaConverter> contas, MascaraPlano mascara)
    {
        var lista = contas.ToList();
        var erros = new List<string>();
        var itens = new List<ItemConversao>(lista.Count);
        var novoPorAntigo = new Dictionary<string, string>();

        foreach (var c in lista)
        {
            var antigo = c.Codigo.Trim();
            var r = ConverterCodigo(mascara, antigo);
            if (!r.Sucesso) { erros.Add($"Conta {antigo}: {r.Erros[0]}."); continue; }
            if (Niveis(r.Valor) != Niveis(antigo))
            {
                erros.Add($"Conta {antigo}: a conversão mudaria o número de níveis da conta ({Niveis(antigo)} para {Niveis(r.Valor)}), o que desmontaria a hierarquia.");
                continue;
            }
            novoPorAntigo[antigo] = r.Valor;
            itens.Add(new ItemConversao(c.Id, antigo, r.Valor, AvisoDeZeros(antigo, r.Valor)));
        }

        foreach (var g in itens.GroupBy(i => i.Novo).Where(g => g.Count() > 1).Take(10))
            erros.Add($"As contas {string.Join(", ", g.Select(x => x.Antigo))} ficariam com o mesmo código {g.Key}.");

        foreach (var i in itens)
        {
            var ponto = i.Antigo.LastIndexOf('.');
            if (ponto <= 0) continue;
            var paiAntigo = i.Antigo[..ponto];
            if (!novoPorAntigo.TryGetValue(paiAntigo, out var paiNovo)) continue;   // pai ausente: problema do plano, não da conversão
            var pontoNovo = i.Novo.LastIndexOf('.');
            if (pontoNovo <= 0 || i.Novo[..pontoNovo] != paiNovo)
                erros.Add($"Conta {i.Antigo}: o código novo {i.Novo} não ficaria abaixo da conta superior ({paiAntigo} → {paiNovo}).");
        }

        if (erros.Count > 30) { var extra = erros.Count - 30; erros = erros.Take(30).ToList(); erros.Add($"... e mais {extra} problema(s)."); }
        return new PreviaConversao(mascara.Texto, itens, erros);
    }

    /// <summary>
    /// Os códigos do plano são sempre hierárquicos (separados por ponto): um código sem ponto é uma conta de nível 1 e só tem
    /// os zeros à esquerda ajustados — nunca é "fatiado" em níveis como acontece com planilhas de códigos corridos.
    /// </summary>
    private static Resultado<string> ConverterCodigo(MascaraPlano mascara, string codigo)
    {
        if (codigo.Length == 0 || codigo.Contains('.') || !codigo.All(char.IsAsciiDigit)) return mascara.Converter(codigo);
        var v = codigo.TrimStart('0');
        if (v.Length == 0) v = "0";
        return v.Length > mascara.Segmentos[0]
            ? Resultado<string>.Falha($"no código {codigo}, o nível 1 ({codigo}) não cabe em {mascara.Segmentos[0]} dígito(s)")
            : Resultado<string>.Ok(v.PadLeft(mascara.Segmentos[0], '0'));
    }

    private static int Niveis(string codigo) => codigo.Split(codigo.Where(ch => !char.IsAsciiDigit(ch)).Distinct().ToArray(), StringSplitOptions.RemoveEmptyEntries).Length;


    /// <summary>Aviso quando algum nível perde zeros à esquerda (a largura do nível diminuiu).</summary>
    private static string? AvisoDeZeros(string antigo, string novo)
    {
        var a = antigo.Split('.');
        var n = novo.Split('.');
        if (a.Length != n.Length) return null;
        for (var i = 0; i < a.Length; i++)
            if (a[i].Length > n[i].Length) return $"o nível {i + 1} perde zeros à esquerda ({a[i]} → {n[i]})";
        return null;
    }

    /// <summary>
    /// Plano novo e vazio para uma máscara: só os grupos de nível 1 (sintéticos), prontos para receber subcontas.
    /// O código de cada grupo é o número (1, 2, 3...) completado até a largura do primeiro nível da máscara.
    /// </summary>
    public static IReadOnlyList<(string Codigo, string Descricao, string CodNat, IndicadorDC Saldo)> GruposIniciais(MascaraPlano mascara)
    {
        var largura = mascara.Segmentos[0];
        var grupos = new (string Descricao, string CodNat, IndicadorDC Saldo)[]
        {
            ("ATIVO", "01", IndicadorDC.D), ("PASSIVO", "02", IndicadorDC.C), ("PATRIMÔNIO LÍQUIDO", "03", IndicadorDC.C),
            ("RECEITAS", "04", IndicadorDC.C), ("CUSTOS E DESPESAS", "04", IndicadorDC.D),
        };
        var quantos = Math.Min(grupos.Length, (int)Math.Pow(10, Math.Min(largura, 2)) - 1);   // largura 1 comporta 1..9
        return grupos.Take(quantos).Select((g, i) => ((i + 1).ToString().PadLeft(largura, '0'), g.Descricao, g.CodNat, g.Saldo)).ToList();
    }

    /// <summary>
    /// Próximo código livre abaixo de uma conta: o maior número já usado entre as filhas diretas + 1, com a largura do nível na
    /// máscara (ou a largura das irmãs, ou 3 dígitos, sem máscara). Falha quando o nível está cheio (999 numa largura de 3).
    /// </summary>
    public static Resultado<string> ProximoCodigoFilho(string codigoPai, IEnumerable<string> codigosDoPlano, MascaraPlano? mascara)
    {
        var nivelFilho = codigoPai.Count(ch => ch == '.') + 2;
        var prefixo = codigoPai + ".";
        var irmas = codigosDoPlano.Where(c => c.StartsWith(prefixo, StringComparison.Ordinal) && c.Count(ch => ch == '.') == nivelFilho - 1)
                                  .Select(c => c[prefixo.Length..]).ToList();
        var largura = mascara is not null && nivelFilho <= mascara.Segmentos.Count ? mascara.Segmentos[nivelFilho - 1]
                    : irmas.Count > 0 ? irmas.Max(i => i.Length) : 3;
        var maior = irmas.Where(i => i.All(char.IsAsciiDigit) && long.TryParse(i, out _)).Select(long.Parse).DefaultIfEmpty(0).Max();
        var proximo = maior + 1;
        if (proximo.ToString().Length > largura)
            return Resultado<string>.Falha($"O nível {nivelFilho} da conta {codigoPai} já está cheio: a máscara permite até {new string('9', largura)} contas nele. Amplie a máscara do plano (Converter máscara) e tente de novo.");
        var codigo = prefixo + proximo.ToString().PadLeft(largura, '0');
        return codigo.Length > 30 ? Resultado<string>.Falha("O código da nova conta passaria de 30 caracteres.") : Resultado<string>.Ok(codigo);
    }
}
