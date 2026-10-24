using Vorticis.Domain.Licencas;

namespace Vorticis.Domain.Tests;

public class LicencasTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 24);

    private static Licenca Nova(params string[] modulos) => new("ABC123", "Padaria Boa Ltda", "11444777000161", modulos, Hoje, Hoje.AddYears(1), 3);

    [Fact]
    public void Licenca_assinada_e_conferida_pela_chave_publica()
    {
        var (privada, publica) = ChavesLicenca.Gerar();
        var texto = ArquivoLicenca.Assinar(Nova("contabil", "fiscal"), privada);
        Assert.StartsWith(ArquivoLicenca.Cabecalho + "\n", texto);
        Assert.Contains("\nMODULOS=contabil,fiscal\n", texto);              // linha lida pelo instalador
        var lida = ArquivoLicenca.Ler(texto.Replace("\n", "\r\n"), publica);   // tolera fim de linha do Windows
        Assert.True(lida.Sucesso, string.Join("; ", lida.Erros));
        Assert.Equal(("Padaria Boa Ltda", 3, Hoje.AddYears(1)), (lida.Valor.Cliente, lida.Valor.Usuarios, lida.Valor.ValidaAte));
        Assert.Equal("Contábil, Fiscal", lida.Valor.ModulosTexto);
        Assert.Equal(publica, ChavesLicenca.PublicaDe(privada));

        // arquivo alterado, outra chave ou lixo: recusado
        Assert.Contains("não confere", ArquivoLicenca.Ler(texto.Replace("MODULOS=contabil,fiscal", "MODULOS=contabil,fiscal,folha"), publica).Erros[0]);
        Assert.Contains("não confere", ArquivoLicenca.Ler(texto, ChavesLicenca.Gerar().Publica).Erros[0]);
        Assert.Contains("não é uma licença", ArquivoLicenca.Ler("qualquer coisa", publica).Erros[0]);
    }

    [Fact]
    public void Modulo_so_entra_se_licenciado_e_dentro_da_validade()
    {
        var l = Nova("contabil", "folha");
        Assert.True(ArquivoLicenca.ConferirModulo(l, ModulosLicenca.DoExecutavel("VorticisContabil"), Hoje).Sucesso);
        Assert.Contains("não está na licença", ArquivoLicenca.ConferirModulo(l, "erp", Hoje).Erros[0]);
        Assert.Contains("venceu", ArquivoLicenca.ConferirModulo(l, "contabil", Hoje.AddYears(1).AddDays(1)).Erros[0]);
        Assert.Equal("condominio", ModulosLicenca.DoExecutavel("VorticisCondominio"));
        Assert.NotEmpty(ArquivoLicenca.Validar(Nova()));
        Assert.NotEmpty(ArquivoLicenca.Validar(Nova("xyz")));
        Assert.Empty(ArquivoLicenca.Validar(l));
    }

    [Fact]
    public void Chave_publica_do_fornecedor_esta_embutida()
        => Assert.StartsWith("-----BEGIN PUBLIC KEY-----", ChavesLicenca.PublicaDoFornecedor);
}
