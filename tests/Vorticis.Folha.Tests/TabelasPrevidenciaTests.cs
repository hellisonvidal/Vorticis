using Vorticis.Folha.Regras;

namespace Vorticis.Folha.Tests;

/// <summary>Cadastros › Tabelas legais › Tabelas da previdência social: tabelas vigentes na competência, para consulta e impressão.</summary>
public class TabelasPrevidenciaTests
{
    [Fact]
    public void Mostra_as_faixas_do_INSS_o_teto_salario_familia_minimo_FGTS_e_IRRF_da_competencia()
    {
        var legais = TabelasLegais.Embutidas();
        var comp = new DateOnly(2026, 3, 1);
        var t = TabelasLegais.Em(comp, legais);
        var tabela = TabelasPrevidencia.Montar(comp, legais);
        Assert.Equal(TabelasPrevidencia.Titulo, tabela.Titulo);
        var itens = tabela.Linhas.Select(l => (string)l.Valores[1]!).ToList();
        Assert.Equal(t.Inss.Faixas.Count, itens.Count(i => i.Contains("ª faixa")));
        Assert.Contains(tabela.Linhas, l => (string)l.Valores[1]! == "Teto do salário de contribuição" && (string)l.Valores[2]! == Vorticis.Domain.Comum.Formato.Moeda(t.Inss.Teto));
        Assert.Contains(tabela.Linhas, l => (string)l.Valores[1]! == "Cota por filho ou equiparado" && (string)l.Valores[2]! == Vorticis.Domain.Comum.Formato.Moeda(t.SalarioFamilia.Cota));
        Assert.Contains(tabela.Linhas, l => (string)l.Valores[1]! == "Depósito mensal" && ((string)l.Valores[2]!).StartsWith("8"));
        Assert.Equal(t.Irrf.Faixas.Count, tabela.Linhas.Count(l => ((string)l.Valores[1]!).StartsWith("Até") || (string)l.Valores[1]! == "Acima da faixa anterior"));
        Assert.Contains("03/2026", tabela.Rodape);
        Assert.Throws<InvalidOperationException>(() => TabelasPrevidencia.Montar(new DateOnly(1980, 1, 1), legais));   // sem tabela vigente
    }
}
