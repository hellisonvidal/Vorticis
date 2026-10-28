using Vorticis.Domain.Contabil;

namespace Vorticis.Application.Contabil;

/// <summary>Dados mínimos de uma conta do plano para a conferência (analítica? natureza COD_NAT).</summary>
public sealed record ContaConferida(bool Analitica, string CodNat);

/// <summary>
/// Conferência das contas de encerramento (apuração, lucros e prejuízos acumulados) contra o plano da empresa, em linguagem para o
/// contador: existe no plano, está na máscara, é analítica e é da natureza certa (resultado para a apuração; patrimônio líquido para
/// lucros e prejuízos). Usada antes de gravar os parâmetros e antes de calcular ou executar o encerramento.
/// </summary>
public static class ConferenciaContasEncerramento
{
    public const string NaturezaResultado = "04";
    public const string NaturezaPatrimonio = "03";

    public static string NomeNatureza(string codNat) => codNat switch
    {
        "01" => "Ativo", "02" => "Passivo", "03" => "Patrimônio líquido", "04" => "Resultado", "05" => "Compensação", _ => "Outras",
    };

    /// <param name="contas">Códigos informados.</param>
    /// <param name="mascara">Máscara do plano da empresa (null = não conferir).</param>
    /// <param name="buscar">Conta do plano pelo código (null se não existe).</param>
    /// <param name="aceitarVazias">Nos parâmetros, deixar o código em branco vale (usa o padrão do modelo); no encerramento, não.</param>
    public static IReadOnlyList<string> Conferir(ContasEncerramento contas, string? mascara, Func<string, ContaConferida?> buscar, bool aceitarVazias = false)
    {
        var erros = new List<string>();
        var m = string.IsNullOrWhiteSpace(mascara) ? null : MascaraPlano.Ler(mascara) is { Sucesso: true } r ? r.Valor : null;
        void Conferir1(string? codigo, string papel, string naturezaEsperada)
        {
            codigo = codigo?.Trim() ?? "";
            if (codigo.Length == 0)
            {
                if (!aceitarVazias) erros.Add($"Informe a conta de {papel}. Use a caixa de pesquisa para escolher uma conta do plano.");
                return;
            }
            var foraDaMascara = m is not null && !CabeNaMascara(codigo, m);
            var aviso = foraDaMascara ? $" Ela também não está na máscara do plano ({m!.Texto})." : "";
            if (buscar(codigo) is not { } c)
                erros.Add($"A conta de {papel} ({codigo}) não existe no plano de contas desta empresa.{aviso} Escolha uma conta existente pela caixa de pesquisa.");
            else if (!c.Analitica)
                erros.Add($"A conta de {papel} ({codigo}) é sintética (agrupadora). Escolha uma conta analítica, que recebe lançamentos.");
            else if (c.CodNat != naturezaEsperada)
                erros.Add($"A conta de {papel} ({codigo}) é do grupo {NomeNatureza(c.CodNat)}; deveria ser uma conta de {NomeNatureza(naturezaEsperada)}.");
            else if (foraDaMascara)
                erros.Add($"A conta de {papel} ({codigo}) não está na máscara do plano ({m!.Texto}). Escolha uma conta do plano pela caixa de pesquisa.");
        }
        Conferir1(contas.Apuracao, "apuração do resultado", NaturezaResultado);
        Conferir1(contas.LucrosAcumulados, "lucros acumulados", NaturezaPatrimonio);
        Conferir1(contas.PrejuizosAcumulados, "prejuízos acumulados", NaturezaPatrimonio);
        return erros;
    }

    /// <summary>A máscara só vale como regra quando o próprio plano já a segue (planos antigos podem ter códigos fora dela).</summary>
    public static string? MascaraSeguidaPeloPlano(string? mascara, IEnumerable<string> codigosDoPlano)
        => !string.IsNullOrWhiteSpace(mascara) && MascaraPlano.Ler(mascara) is { Sucesso: true } r && codigosDoPlano.All(c => CabeNaMascara(c, r.Valor)) ? mascara : null;

    private static bool CabeNaMascara(string codigo, MascaraPlano m)
    {
        var partes = codigo.Split('.');
        return partes.Length == m.Segmentos.Count && partes.Zip(m.Segmentos).All(p => p.First.Length == p.Second && p.First.All(char.IsAsciiLetterOrDigit));
    }
}
