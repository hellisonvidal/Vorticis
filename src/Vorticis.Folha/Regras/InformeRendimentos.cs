using Vorticis.Domain.Obrigacoes;
using Vorticis.Folha.Cadastros;

namespace Vorticis.Folha.Regras;

/// <summary>
/// Valores do Comprovante de Rendimentos Pagos e de Imposto sobre a Renda Retido na Fonte de um funcionário no ano-calendário
/// (IN RFB 2.060/2021): quadro 3 (tributáveis, previdência oficial, pensão e IRRF), quadro 4 (isentos: indenizações por rescisão e outros)
/// e quadro 5 (tributação exclusiva: 13º salário líquido e o IRRF sobre ele).
/// </summary>
public sealed record InformeRendimentos(long FuncionarioId, string Matricula, string Nome, string Cpf, decimal Rendimentos, decimal Previdencia, decimal Pensao,
                                        decimal Irrf, decimal Indenizacoes, decimal OutrosIsentos, decimal Decimo, decimal IrrfDecimo,
                                        IReadOnlyList<(string Descricao, decimal Valor)> DetalheOutros)
{
    public bool Vazio => Rendimentos == 0 && Previdencia == 0 && Irrf == 0 && Indenizacoes == 0 && OutrosIsentos == 0 && Decimo == 0 && IrrfDecimo == 0;
}

public static class ApuracaoInformeRendimentos
{
    /// <summary>
    /// Data de pagamento do cálculo (regime de caixa: o rendimento entra no ano em que foi pago). Sem data informada, a folha mensal
    /// conta como paga no mês seguinte (até o 5º dia útil, CLT art. 459) e os demais cálculos na própria competência.
    /// </summary>
    public static DateOnly Pagamento(CalculoGravado c)
        => c.DataPagamento ?? (c.Tipo == TipoFolha.Mensal ? CalendarioDiasUteis.NesimoDiaUtil(c.Competencia.AddMonths(1).Year, c.Competencia.AddMonths(1).Month, 5) : c.Competencia);

    /// <summary>
    /// Soma os cálculos pagos no <paramref name="ano"/> por funcionário. A incidência de IRRF vem da rubrica (eSocial): 01 tributável,
    /// 09 13º salário, 31 pensão; os proventos sem incidência são isentos (na rescisão, indenizações). A 1ª parcela do 13º não entra:
    /// o 13º inteiro é informado na 2ª parcela (ou na rescisão), líquido do INSS, dos dependentes e da pensão (base do IRRF).
    /// </summary>
    public static IReadOnlyList<InformeRendimentos> Apurar(IEnumerable<CalculoGravado> calculos, IReadOnlyDictionary<string, string> incidenciaIrrf, int ano)
    {
        string Inc(ItemGravado i) => incidenciaIrrf.TryGetValue(i.Codigo, out var v) ? v
                                     : RubricasPadrao.EhPadrao(i.Codigo) ? RubricasPadrao.Obter(i.Codigo).IncIrrf : "00";
        var lista = new List<InformeRendimentos>();
        foreach (var g in calculos.Where(c => c.Tipo != TipoFolha.Decimo1 && Pagamento(c).Year == ano).GroupBy(c => c.FuncionarioId))
        {
            decimal rend = 0, prev = 0, pensao = 0, irrf = 0, indeniz = 0, outros = 0, decimo = 0, irrfDecimo = 0;
            var detalhe = new Dictionary<string, decimal>();
            foreach (var c in g)
            {
                if (c.Tipo == TipoFolha.Decimo2)
                {
                    decimo += c.BaseIrrf;
                    irrfDecimo += c.Itens.Where(i => i.Codigo is RubricasPadrao.IrrfDecimo or RubricasPadrao.Irrf).Sum(i => i.Valor);
                    continue;
                }
                foreach (var i in c.Itens.Where(i => i.Tipo is 'P' or 'D'))
                {
                    var inc = Inc(i);
                    switch (i.Codigo)
                    {
                        case RubricasPadrao.Inss: prev += i.Valor; continue;
                        case RubricasPadrao.Irrf: irrf += i.Valor; continue;
                        case RubricasPadrao.InssDecimo: decimo -= i.Valor; continue;
                        case RubricasPadrao.IrrfDecimo: irrfDecimo += i.Valor; continue;
                    }
                    if (inc == "31") { pensao += i.Valor; continue; }
                    if (i.Tipo == 'D') { if (inc == "01") rend -= i.Valor; continue; }
                    if (inc == "01") rend += i.Valor;
                    else if (inc == "09") decimo += i.Valor;
                    else if (c.Tipo == TipoFolha.Rescisao) indeniz += i.Valor;
                    else { outros += i.Valor; detalhe[i.Descricao] = detalhe.GetValueOrDefault(i.Descricao) + i.Valor; }
                }
            }
            var f = g.First();
            var informe = new InformeRendimentos(f.FuncionarioId, f.Matricula, f.FuncionarioNome, f.Cpf, rend, prev, pensao, irrf, indeniz, outros,
                                                 Math.Max(0, decimo), irrfDecimo, detalhe.Select(d => (d.Key, d.Value)).ToList());
            if (!informe.Vazio) lista.Add(informe);
        }
        return lista.OrderBy(i => i.Nome, StringComparer.CurrentCulture).ToList();
    }
}
