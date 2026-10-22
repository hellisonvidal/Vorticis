using Vorticis.Desktop.Comum.Modulos;
using Vorticis.Desktop.ViewModels;

namespace Vorticis.Desktop.Tests;

/// <summary>Consistência do menu do módulo com o casco: toda chave de menu e de atalho tem uma tela registrada.</summary>
public class MenusTests
{
    private static IEnumerable<ItemMenu> Folhas(IEnumerable<ItemMenu> itens) =>
        itens.SelectMany(i => i.Filhos is null ? [i] : Folhas(i.Filhos));

    [Fact]
    public void Toda_chave_de_menu_do_contabil_tem_tela_registrada()
    {
        var vm = new MainViewModel();
        var chaves = Folhas(vm.Menus).Where(i => !i.EhSeparador && i.Habilitado && i.Chave is not null && i.Chave != TelasComuns.Sair)
            .Select(i => i.Chave!).ToList();
        Assert.Contains(Telas.Lancamento, chaves);
        Assert.Contains(Telas.Empresas, chaves);
        Assert.All(chaves, c => Assert.True(vm.ConheceTela(c), c));
        Assert.All(vm.Atalhos.Where(a => a.Chave is not null), a => Assert.True(vm.ConheceTela(a.Chave!), a.Chave));
    }

    [Fact]
    public void Fiscal_tem_menu_comum_e_itens_do_modulo_desabilitados()
    {
        MainViewModelBase vm = new Vorticis.Fiscal.Desktop.MainViewModel();
        Assert.StartsWith("Vorticis ", vm.Modulo.Nome);
        Assert.Equal("_Cadastros", vm.Menus[0].Titulo);
        Assert.Equal("_Ajuda", vm.Menus[^1].Titulo);
        var proprios = vm.Menus.Skip(1).SkipLast(2);
        Assert.NotEmpty(proprios);
        Assert.All(Folhas(proprios), i => Assert.False(i.Habilitado || i.Chave is not null, i.Titulo));
        Assert.All(Folhas(vm.Menus).Where(i => i.Chave is not null && i.Chave != TelasComuns.Sair), i => Assert.True(vm.ConheceTela(i.Chave!), i.Chave));
    }

    [Fact]
    public void Menu_tem_os_menus_comuns_em_volta_dos_do_modulo()
    {
        var titulos = new MainViewModel().Menus.Select(m => m.Titulo).ToList();
        Assert.Equal("_Cadastros", titulos[0]);
        Assert.Equal("Con_figurações", titulos[^2]);
        Assert.Equal("_Ajuda", titulos[^1]);
        Assert.Contains("_Lançamentos", titulos);
    }

    [Fact]
    public void Menu_da_folha_tem_telas_registradas_e_so_o_que_falta_fica_em_breve()
    {
        var vm = new Vorticis.Folha.Desktop.MainViewModel();
        Assert.Equal("_Cadastros", vm.Menus[0].Titulo);
        Assert.Equal("_Ajuda", vm.Menus[^1].Titulo);
        var proprios = Folhas(vm.Menus.Skip(1).SkipLast(2)).Where(i => !i.EhSeparador).ToList();
        var habilitados = proprios.Where(i => i.Habilitado).ToList();
        Assert.All(habilitados, i => Assert.True(vm.ConheceTela(i.Chave!), i.Titulo));
        Assert.Contains(habilitados, i => i.Chave == "folha.funcionarios");
        Assert.Contains(habilitados, i => i.Chave == "folha.esocial");
        Assert.Equal(["FGTS (GFD) (em breve)", "DCTFWeb e INSS (em breve)", "RAIS e DIRF (em breve)"], proprios.Where(i => !i.Habilitado).Select(i => i.Titulo));
        Assert.All(vm.Atalhos.Where(a => a.Chave is not null), a => Assert.True(vm.ConheceTela(a.Chave!), a.Chave));
    }
}
