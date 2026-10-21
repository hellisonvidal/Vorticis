using Vorticis.Desktop.Comum.Modulos;

namespace Vorticis.Folha.Desktop;

/// <summary>
/// Módulo Folha: por enquanto só o menu com itens desabilitados ("em breve"). Para implementar uma função: registrar a
/// tela em <see cref="RegistrarTelas"/> (<c>catalogo.Instancia(...).Tela(chave, ...)</c>), trocar o item do menu por
/// <c>ItemMenu.Tela(titulo, chave)</c> e associar o ViewModel à View em App.axaml.
/// </summary>
public sealed class ModuloFolha : IModuloDesktop
{
    public string Nome => "Vorticis Folha de Pagamento";
    public string NomeCurto => "Folha";
    public string Subtitulo => "Folha de pagamento e eSocial";
    public string NomeExecutavel => "VorticisFolha";

    public IReadOnlyList<ItemMenu> ItensCadastros { get; } = [];

    public IReadOnlyList<ItemMenu> Menus { get; } =
    [
        ItemMenu.Submenu("_Funcionários",
            ItemMenu.EmBreve("Cadastro de funcionários"),
            ItemMenu.EmBreve("Cargos e departamentos"),
            ItemMenu.EmBreve("Rubricas (eventos)")),
        ItemMenu.Submenu("_Folha",
            ItemMenu.EmBreve("Cálculo da folha mensal"),
            ItemMenu.EmBreve("Férias"),
            ItemMenu.EmBreve("13º salário"),
            ItemMenu.EmBreve("Rescisões")),
        ItemMenu.Submenu("_Obrigações",
            ItemMenu.EmBreve("eSocial"),
            ItemMenu.EmBreve("FGTS (GFD)"),
            ItemMenu.EmBreve("DCTFWeb e INSS"),
            ItemMenu.EmBreve("RAIS e DIRF")),
        ItemMenu.Submenu("_Relatórios",
            ItemMenu.EmBreve("Holerites"),
            ItemMenu.EmBreve("Resumo da folha"),
            ItemMenu.EmBreve("Provisões")),
    ];

    public IReadOnlyList<AtalhoTela> Atalhos { get; } = [];

    public void RegistrarTelas(CatalogoTelas catalogo)
    {
        // Nenhuma tela própria ainda.
    }
}
