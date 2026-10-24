using Vorticis.Desktop.Comum;
using Vorticis.Domain.Licencas;
using Vorticis.Licencas.Desktop;

namespace Vorticis.Desktop.Tests;

/// <summary>Gerenciador de licenças (fornecedor) emite o arquivo; o casco o instala e confere o módulo.</summary>
public class LicencasDesktopTests
{
    /// <summary>
    /// Os testes entram nos módulos pelo login: o assembly de testes instala uma licença com todos os módulos, assinada por uma chave
    /// gerada só para os testes (a chave do fornecedor não fica no repositório).
    /// </summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void LicencaDosTestes()
    {
        var (privada, publica) = ChavesLicenca.Gerar();
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var arquivo = Path.Combine(Path.GetTempPath(), $"vorticis-testes-{Environment.ProcessId}{ArquivoLicenca.Extensao}");
        File.WriteAllText(arquivo, ArquivoLicenca.Assinar(new Licenca("TESTES", "Testes automatizados", null, ModulosLicenca.Todos, hoje, hoje.AddYears(1), 99), privada));
        LicencaInstalada.ChavePublica = publica;
        Environment.SetEnvironmentVariable("VORTICIS_LICENCA", arquivo);
    }

    [Fact]
    public void Gerenciador_emite_licenca_que_o_modulo_instala_e_confere()
    {
        var pasta = Directory.CreateTempSubdirectory("vorticis-licencas").FullName;
        try
        {
            var g = new GerenciadorViewModel(pasta);
            Assert.False(g.TemChave);
            Assert.Null(g.EmitirLicenca());                                   // sem chave não emite
            g.CriarChaves();
            Assert.True(g.TemChave);
            Assert.StartsWith("ATENÇÃO", g.SituacaoChave);                    // par novo ≠ chave embutida na suíte
            g.Cliente = "Padaria Boa";
            Assert.Null(g.EmitirLicenca());                                   // sem módulo marcado
            g.Modulos.First(m => m.Codigo == "fiscal").Marcado = true;
            var arquivo = g.EmitirLicenca();
            Assert.NotNull(arquivo);
            Assert.Single(g.Emitidas);
            Assert.Single(new GerenciadorViewModel(pasta).Emitidas);           // histórico gravado

            var publica = g.ExportarChavePublica(Path.Combine(pasta, "pub.pem"))!;
            var chave = File.ReadAllText(publica);
            var destino = Path.Combine(pasta, "instalada", LicencaInstalada.NomeArquivo);
            var inst = LicencaInstalada.Instalar(arquivo!, destino, chave);
            Assert.True(inst.Sucesso, string.Join("; ", inst.Erros));
            Assert.True(File.Exists(destino));
            Assert.False(LicencaInstalada.Instalar(publica, destino, chave).Sucesso);   // não é licença

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            Assert.True(LicencaInstalada.ConferirModulo("VorticisFiscal", hoje, chave, destino).Sucesso);
            Assert.Contains("não está na licença", LicencaInstalada.ConferirModulo("VorticisFolha", hoje, chave, destino).Erros[0]);
            Assert.False(LicencaInstalada.Carregar(ChavesLicenca.PublicaDoFornecedor, destino).Sucesso);   // outra chave que não a do fornecedor
        }
        finally { Directory.Delete(pasta, true); }
    }
}
