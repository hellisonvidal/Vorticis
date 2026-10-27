namespace Vorticis.Desktop.Tests;

/// <summary>Este produto usa pasta e banco próprios, sem colidir com o Vorticis Suite Logística (pasta "Vorticis").</summary>
public class PastasDeDadosTests
{
    [Fact]
    public void Caminhos_usam_VorticisEmpresarial_e_banco_proprio()
    {
        Assert.Equal("VorticisEmpresarial", Configuracao.NomePasta);
        Assert.Equal("vorticis_empresarial", Configuracao.NomeBanco);
        Assert.Contains("Database=vorticis_empresarial;", Configuracao.Padrao);
        Assert.Equal("VorticisEmpresarial", Path.GetFileName(Configuracao.PastaDados));
        Assert.Equal("VorticisEmpresarial", Path.GetFileName(Configuracao.PastaLocal));
        Assert.Equal(Path.Combine(Configuracao.PastaDados, "vorticis.json"), Configuracao.ArquivoConfiguracao);
        Assert.Equal(Path.Combine(Configuracao.PastaDados, "login.json"), LoginSalvo.Arquivo);
        foreach (var caminho in new[] { Configuracao.PastaBancoLocal, Configuracao.PastaExecucao, Configuracao.ArquivoTrava,
                     Configuracao.LogBancoLocal, Configuracao.LogConfiguracaoBanco })
            Assert.StartsWith(Configuracao.PastaLocal + Path.DirectorySeparatorChar, caminho);
    }

    [Fact]
    public void Migracao_move_Perseus_para_VorticisEmpresarial_sem_tocar_na_pasta_Vorticis()
    {
        var raiz = Directory.CreateTempSubdirectory("vorticis_pastas_").FullName;
        try
        {
            var cfg = Path.Combine(raiz, "cfg");
            var loc = Path.Combine(raiz, "loc");
            Directory.CreateDirectory(Path.Combine(cfg, "Perseus"));
            File.WriteAllText(Path.Combine(cfg, "Perseus", "perseus.json"), "{}");
            Directory.CreateDirectory(Path.Combine(loc, "Perseus", "banco"));
            Directory.CreateDirectory(Path.Combine(loc, "Vorticis", "banco")); // da Logística
            File.WriteAllText(Path.Combine(loc, "Vorticis", "banco", "marca.txt"), "logistica");

            Configuracao.MigrarPastasDoNomeAnterior(cfg, loc);

            Assert.True(File.Exists(Path.Combine(cfg, "VorticisEmpresarial", "vorticis.json")));
            Assert.True(Directory.Exists(Path.Combine(loc, "VorticisEmpresarial", "banco")));
            Assert.False(Directory.Exists(Path.Combine(cfg, "Perseus")));
            Assert.False(Directory.Exists(Path.Combine(loc, "Perseus")));
            Assert.Equal("logistica", File.ReadAllText(Path.Combine(loc, "Vorticis", "banco", "marca.txt")));
            Assert.False(Directory.Exists(Path.Combine(cfg, "Vorticis")));
        }
        finally { Directory.Delete(raiz, true); }
    }

    [Fact]
    public void Migracao_nao_move_se_a_pasta_nova_ja_existe()
    {
        var raiz = Directory.CreateTempSubdirectory("vorticis_pastas_").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(raiz, "Perseus"));
            Directory.CreateDirectory(Path.Combine(raiz, "VorticisEmpresarial"));
            Configuracao.MigrarPastasDoNomeAnterior(raiz, raiz);
            Assert.True(Directory.Exists(Path.Combine(raiz, "Perseus")));
        }
        finally { Directory.Delete(raiz, true); }
    }
}
