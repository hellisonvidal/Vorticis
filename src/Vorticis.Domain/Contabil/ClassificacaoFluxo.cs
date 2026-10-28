using System.Globalization;
using System.Text;

namespace Vorticis.Domain.Contabil;

/// <summary>
/// Como cada conta analítica entra na DFC (contas patrimoniais) e no EBITDA (contas de resultado).
/// Contas patrimoniais: Caixa, Operacional, Investimento, Financiamento ou ContrapartidaAjuste (conta que só recebe a
/// contrapartida de um ajuste sem efeito no caixa, como a depreciação acumulada). Contas de resultado: Receita, Deducao,
/// CustoDespesa, Depreciacao, Financeiro ou ImpostoLucro.
/// </summary>
public enum ClasseConta
{
    Caixa, Operacional, Investimento, Financiamento, ContrapartidaAjuste,
    Receita, Deducao, CustoDespesa, Depreciacao, Financeiro, ImpostoLucro,
}

/// <summary>Classificação gravada de uma conta; <paramref name="NaoRecorrente"/> vale para contas de resultado (EBITDA ajustado).</summary>
public sealed record ClassificacaoConta(long ContaId, ClasseConta Classe, bool NaoRecorrente = false);

public static class ClassesConta
{
    public static bool EhPatrimonial(this ClasseConta c) => c <= ClasseConta.ContrapartidaAjuste;

    public static string Nome(this ClasseConta c) => c switch
    {
        ClasseConta.Caixa => "Caixa e equivalentes",
        ClasseConta.Operacional => "Atividade operacional",
        ClasseConta.Investimento => "Atividade de investimento",
        ClasseConta.Financiamento => "Atividade de financiamento",
        ClasseConta.ContrapartidaAjuste => "Contrapartida de ajuste (sem caixa)",
        ClasseConta.Receita => "Receita",
        ClasseConta.Deducao => "Dedução da receita",
        ClasseConta.CustoDespesa => "Custo ou despesa operacional",
        ClasseConta.Depreciacao => "Depreciação, amortização e exaustão",
        ClasseConta.Financeiro => "Resultado financeiro",
        ClasseConta.ImpostoLucro => "IR e CSLL",
        _ => c.ToString(),
    };

    /// <summary>Classes que a conta pode receber, conforme ela seja patrimonial ou de resultado.</summary>
    public static IReadOnlyList<ClasseConta> Opcoes(bool patrimonial) => Enum.GetValues<ClasseConta>().Where(c => c.EhPatrimonial() == patrimonial).ToArray();

    public static string Codigo(this ClasseConta c) => c.ToString().ToUpperInvariant();
    public static ClasseConta? DeCodigo(string? codigo) => Enum.TryParse<ClasseConta>(codigo, ignoreCase: true, out var c) ? c : null;
}

/// <summary>
/// Sugestão automática da classificação, pelo nome da conta e dos grupos acima dela, pela natureza (COD_NAT) e pelo código do
/// plano referencial. O usuário sempre pode trocar; a sugestão só vale para a conta que ainda não foi classificada.
/// </summary>
public static class SugestaoClasse
{
    public static string Normalizar(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d) if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }

    private static bool Tem(string texto, params string[] termos) => termos.Any(texto.Contains);

    /// <param name="referencial">Conta → código do plano referencial (opcional).</param>
    public static IReadOnlyDictionary<long, ClasseConta> Sugerir(IReadOnlyList<ContaPlano> plano, IReadOnlyDictionary<long, string>? referencial = null)
    {
        var porId = plano.ToDictionary(c => c.Id);
        string Caminho(ContaPlano c)
        {
            var partes = new List<string>();
            for (var atual = c; atual is not null; atual = atual.ContaPaiId is { } p && porId.TryGetValue(p, out var pai) ? pai : null) partes.Add(Normalizar(atual.Descricao));
            return string.Join(" > ", partes);
        }
        var res = new Dictionary<long, ClasseConta>();
        foreach (var c in plano.Where(c => c.Analitica && c.CodNat is "01" or "02" or "03" or "04"))
        {
            var nome = Normalizar(c.Descricao);
            var caminho = Caminho(c);
            var refe = referencial is not null && referencial.TryGetValue(c.Id, out var r) ? r : "";
            res[c.Id] = c.CodNat switch
            {
                "01" => Ativo(nome, caminho, refe),
                "02" => Passivo(nome, caminho, refe),
                "03" => Tem(nome, "ajuste de avaliacao", "ajustes de avaliacao", "outros resultados abrangentes") ? ClasseConta.ContrapartidaAjuste : ClasseConta.Financiamento,
                _ => Resultado(nome, caminho, c.NaturezaSaldo),
            };
        }
        return res;
    }

    private static ClasseConta Ativo(string nome, string caminho, string refe)
    {
        if (Tem(nome, "deprecia", "amortiza", "exaust")) return ClasseConta.ContrapartidaAjuste;
        if (Tem(caminho, "liquidez imediata", "equivalente") || refe.StartsWith("1.01.01", StringComparison.Ordinal)) return ClasseConta.Caixa;
        if (Tem(nome, "caixa", "banco", "numerario")) return ClasseConta.Caixa;
        if (Tem(caminho, "imobilizado", "intangivel", "investimento", "participacoes societarias", "propriedade para investimento")
            || refe.StartsWith("1.02.02", StringComparison.Ordinal) || refe.StartsWith("1.02.03", StringComparison.Ordinal)
            || refe.StartsWith("1.02.04", StringComparison.Ordinal)) return ClasseConta.Investimento;
        if (Tem(nome, "aplicac")) return ClasseConta.Investimento;
        return ClasseConta.Operacional;
    }

    private static ClasseConta Passivo(string nome, string caminho, string refe)
    {
        if (Tem(caminho, "emprestimo", "financiamento", "debenture", "mutuo", "arrendamento", "leasing", "dividendo", "juros sobre o capital",
                "juros sobre capital", "lucros a distribuir", "partes relacionadas")) return ClasseConta.Financiamento;
        if (Tem(nome, "parcelamento", "fornecedor", "provisao", "trabalh", "tribut", "imposto", "salario", "ferias")) return ClasseConta.Operacional;
        if (Tem(caminho, "nao circulante", "longo prazo") || refe.StartsWith("2.02", StringComparison.Ordinal)) return ClasseConta.Financiamento;
        return ClasseConta.Operacional;
    }

    private static ClasseConta Resultado(string nome, string caminho, IndicadorDC natureza)
    {
        if (Tem(nome, "deprecia", "amortiza", "exaust")) return ClasseConta.Depreciacao;
        if (Tem(caminho, "csll", "contribuicao social sobre o lucro", "irpj", "imposto de renda")) return ClasseConta.ImpostoLucro;
        if (Tem(caminho, "financeir", "juros", "variacao cambial", "variacoes cambiais", "variacao monetaria", "descontos obtidos", "descontos concedidos"))
            return ClasseConta.Financeiro;
        if (natureza == IndicadorDC.D && Tem(caminho, "deducoes", "deducao", "devolucao", "devolucoes", "abatimento", "impostos sobre vendas",
                "impostos sobre faturamento", "impostos incidentes", "simples nacional", "icms sobre", "iss sobre", "pis sobre", "cofins sobre"))
            return ClasseConta.Deducao;
        if (natureza == IndicadorDC.C && Tem(caminho, "receita", "venda", "faturamento", "servicos prestados") && !Tem(caminho, "outras receitas", "nao operacion"))
            return ClasseConta.Receita;
        return ClasseConta.CustoDespesa;
    }

    /// <summary>Classe efetiva de cada conta analítica: a gravada pelo usuário ou, na falta dela, a sugerida.</summary>
    public static IReadOnlyDictionary<long, ClasseConta> Efetivas(IReadOnlyList<ContaPlano> plano, IReadOnlyList<ClassificacaoConta> gravadas,
                                                                  IReadOnlyDictionary<long, string>? referencial = null)
    {
        var res = new Dictionary<long, ClasseConta>(Sugerir(plano, referencial));
        var porId = plano.ToDictionary(c => c.Id);
        foreach (var g in gravadas)
            if (porId.TryGetValue(g.ContaId, out var c) && c.Analitica && g.Classe.EhPatrimonial() == (c.CodNat != "04")) res[g.ContaId] = g.Classe;
        return res;
    }
}
