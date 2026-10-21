namespace Vorticis.Fiscal.Tests;

public class EstruturaTests
{
    [Fact]
    public void Biblioteca_do_modulo_identifica_o_modulo() => Assert.Equal("Vorticis Fiscal", ModuloFiscalInfo.Nome);
}
