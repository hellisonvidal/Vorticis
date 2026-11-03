using Vorticis.Domain.Contabil;

namespace Vorticis.Domain.Tests;

/// <summary>Conversão do plano para outra máscara: preenchimento e retirada de zeros por nível, conflitos e hierarquia.</summary>
public class ConversaoPlanoTests
{
    private static MascaraPlano M(string t) => MascaraPlano.Ler(t).Valor;
    private static IEnumerable<ContaParaConverter> Contas(params string[] codigos) => codigos.Select((c, i) => new ContaParaConverter(i + 1, c));

    [Fact]
    public void Completa_cada_nivel_com_zeros_a_esquerda_ate_a_largura_da_mascara()
    {
        var p = ConversaoPlano.Simular(Contas("1", "1.1", "1.1.1", "1.1.1.1", "1.1.1.12"), M("9.9.99.999"));
        Assert.True(p.Possivel, string.Join("; ", p.Erros));
        Assert.Equal(["1", "1.1", "1.1.01", "1.1.01.001", "1.1.01.012"], p.Itens.Select(i => i.Novo));
        Assert.Equal(3, p.Mudancas);                 // raiz e 2º nível já estavam certos
        Assert.Equal(0, p.Avisos);
        Assert.All(p.Itens, i => Assert.Null(i.Aviso));
    }

    [Fact]
    public void Retira_zeros_a_esquerda_quando_a_largura_diminui_e_avisa()
    {
        var p = ConversaoPlano.Simular(Contas("01", "01.01", "01.01.001"), M("9.9.99"));
        Assert.True(p.Possivel, string.Join("; ", p.Erros));
        Assert.Equal(["1", "1.1", "1.1.01"], p.Itens.Select(i => i.Novo));
        Assert.All(p.Itens, i => Assert.NotNull(i.Aviso));
        Assert.Contains("perde zeros", p.Itens[0].Aviso);
        Assert.Equal(3, p.Avisos);
    }

    [Fact]
    public void Recusa_o_plano_inteiro_quando_um_nivel_nao_cabe()
    {
        var p = ConversaoPlano.Simular(Contas("1", "1.1", "1.1.1", "1.1.1.120"), M("9.9.9.99"));
        Assert.False(p.Possivel);
        var erro = Assert.Single(p.Erros);
        Assert.Contains("1.1.1.120", erro);
        Assert.Contains("não cabe", erro);
        Assert.DoesNotContain("Exception", erro);
    }

    [Fact]
    public void Recusa_mudanca_do_numero_de_niveis()
    {
        var p = ConversaoPlano.Simular(Contas("1", "1.1", "1.1.1.1.1"), M("9.9.99.999"));
        Assert.False(p.Possivel);
        Assert.Contains(p.Erros, e => e.Contains("1.1.1.1.1") && e.Contains("níveis"));
        // conta de nível 1 com mais dígitos que a máscara: não cabe (nunca é fatiada em níveis)
        var raiz = ConversaoPlano.Simular(Contas("11101"), M("9.9.99.999"));
        Assert.False(raiz.Possivel);
        Assert.Contains(raiz.Erros, e => e.Contains("11101") && e.Contains("não cabe"));
    }

    [Fact]
    public void Recusa_codigos_que_ficariam_iguais()
    {
        // 1.1.01 e 1.1.1 viram o mesmo código; a conversão seria ambígua
        var p = ConversaoPlano.Simular(Contas("1", "1.1", "1.1.01", "1.1.1"), M("9.9.99"));
        Assert.False(p.Possivel);
        Assert.Contains(p.Erros, e => e.Contains("1.1.01") && e.Contains("1.1.1") && e.Contains("mesmo código"));
    }

    [Fact]
    public void Mantem_cada_conta_abaixo_da_mesma_superior()
    {
        var p = ConversaoPlano.Simular(Contas("1", "1.1", "1.1.1", "1.1.1.1", "2", "2.1"), M("99.99.999.9999"));
        Assert.True(p.Possivel, string.Join("; ", p.Erros));
        var novo = p.Itens.ToDictionary(i => i.Antigo, i => i.Novo);
        foreach (var (filho, pai) in new[] { ("1.1", "1"), ("1.1.1", "1.1"), ("1.1.1.1", "1.1.1"), ("2.1", "2") })
            Assert.Equal(novo[pai], novo[filho][..novo[filho].LastIndexOf('.')]);
        Assert.Equal("01.01.001.0001", novo["1.1.1.1"]);
    }

    [Fact]
    public void Conta_com_letra_ou_vazia_impede_a_conversao_com_mensagem_clara()
    {
        var p = ConversaoPlano.Simular(Contas("1", "1.A"), M("9.9"));
        Assert.False(p.Possivel);
        Assert.Contains(p.Erros, e => e.Contains("1.A") && e.Contains("letras"));
    }

    [Fact]
    public void Converter_duas_vezes_para_a_mesma_mascara_nao_muda_nada()
    {
        var primeira = ConversaoPlano.Simular(Contas("1", "1.1", "1.1.1", "1.1.1.1"), M("9.9.99.999"));
        var segunda = ConversaoPlano.Simular(primeira.Itens.Select(i => new ContaParaConverter(i.Id, i.Novo)), M("9.9.99.999"));
        Assert.True(segunda.Possivel);
        Assert.Equal(0, segunda.Mudancas);
    }

    [Fact]
    public void Plano_novo_tem_so_os_grupos_do_primeiro_nivel_na_largura_da_mascara()
    {
        var g = ConversaoPlano.GruposIniciais(M("99.99.999"));
        Assert.Equal(["01", "02", "03", "04", "05"], g.Select(x => x.Codigo));
        Assert.Equal(["01", "02", "03", "04", "04"], g.Select(x => x.CodNat));
        Assert.Equal(["1", "2", "3", "4", "5"], ConversaoPlano.GruposIniciais(M("9.9.99")).Select(x => x.Codigo));
    }

    [Theory]
    [InlineData("1.1.02", new[] { "1.1.02.001", "1.1.02.004", "1.1.03.001" }, "9.9.99.999", "1.1.02.005")]
    [InlineData("1.1.02", new string[0], "9.9.99.999", "1.1.02.001")]
    [InlineData("1.1.02", new[] { "1.1.02.001", "1.1.02.001.1" }, null, "1.1.02.002")]
    [InlineData("2", new[] { "2.01" }, "9.99.999", "2.02")]
    public void Proximo_codigo_livre_abaixo_da_conta_segue_a_largura_da_mascara(string pai, string[] existentes, string? mascara, string esperado)
    {
        var r = ConversaoPlano.ProximoCodigoFilho(pai, existentes, mascara is null ? null : M(mascara));
        Assert.True(r.Sucesso, string.Join("; ", r.Erros));
        Assert.Equal(esperado, r.Valor);
    }

    [Fact]
    public void Nivel_cheio_avisa_para_ampliar_a_mascara()
    {
        var r = ConversaoPlano.ProximoCodigoFilho("1.1", ["1.1.9"], M("9.9.9"));
        Assert.False(r.Sucesso);
        Assert.Contains("já está cheio", r.Erros[0]);
        Assert.Contains("máscara", r.Erros[0]);
    }
}
