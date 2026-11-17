using Vorticis.Domain.Comum;

namespace Vorticis.Fiscal.Simples;

/// <summary>Receitas do ano-calendário até a competência (inclusive) e início de atividade, para conferir os limites que o RBT12 sozinho não mostra.</summary>
public sealed record EntradaLimitesSimples(DateOnly Competencia, DateOnly InicioAtividade, decimal ReceitaAno, decimal ReceitaExportacaoAno);

/// <summary>
/// Alertas dos limites do Simples Nacional a partir de 2027 (LC 123/2006 com as LC 214/2025 e 227/2026): limite adicional de exportação,
/// limite proporcional de início de atividade (mês iniciado conta como inteiro). O limite global e o sublimite (agora também do IBS), com o
/// excesso de até 20%, são tratados no cálculo do DAS. Os valores vêm de <see cref="TabelaSimples"/> (parâmetros legais), nunca do código.
/// </summary>
public static class LimitesSimples
{
    /// <summary>Meses de atividade no ano da competência, com o mês de início contando como inteiro (0 se a atividade começou em outro ano).</summary>
    public static int MesesDeAtividadeNoAno(DateOnly competencia, DateOnly inicioAtividade) =>
        inicioAtividade.Year == competencia.Year ? Math.Max(1, competencia.Month - inicioAtividade.Month + 1) : 0;

    public static IReadOnlyList<string> Avaliar(TabelaSimples tabela, EntradaLimitesSimples e)
    {
        var avisos = new List<string>();
        if (tabela.LimiteExportacao is { } exp && e.ReceitaExportacaoAno > exp)
            avisos.Add($"Receita de exportação do ano ({Formato.Moeda(e.ReceitaExportacaoAno)}) acima do limite adicional de {Formato.Moeda(exp)}: " +
                       "a parte excedente não tem a exclusão de tributos da exportação. Confira o enquadramento.");
        if (tabela.LimiteInicioAtividadeMensal is { } mensal && MesesDeAtividadeNoAno(e.Competencia, e.InicioAtividade) is var meses and > 0)
        {
            var limite = mensal * meses;
            if (e.ReceitaAno > limite)
                avisos.Add($"Início de atividade em {e.InicioAtividade:MM/yyyy}: o limite do ano é proporcional ({Formato.Moeda(mensal)} × {meses} mês(es), contando o mês iniciado como inteiro = {Formato.Moeda(limite)}) " +
                           $"e a receita acumulada no ano é {Formato.Moeda(e.ReceitaAno)}. Confira a exclusão do regime.");
        }
        return avisos;
    }
}
