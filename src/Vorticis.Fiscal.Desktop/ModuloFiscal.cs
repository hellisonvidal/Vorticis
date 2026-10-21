using Vorticis.Desktop.Comum.Modulos;

namespace Vorticis.Fiscal.Desktop;

/// <summary>
/// Módulo Fiscal: por enquanto só o menu com itens desabilitados ("em breve"). Para implementar uma função: registrar a
/// tela em <see cref="RegistrarTelas"/> (<c>catalogo.Instancia(...).Tela(chave, ...)</c>), trocar o item do menu por
/// <c>ItemMenu.Tela(titulo, chave)</c> e associar o ViewModel à View em App.axaml.
/// </summary>
public sealed class ModuloFiscal : IModuloDesktop
{
    public string Nome => "Vorticis Fiscal";
    public string NomeCurto => "Fiscal";
    public string Subtitulo => "Escrituração e obrigações fiscais";
    public string NomeExecutavel => "VorticisFiscal";

    public IReadOnlyList<ItemMenu> ItensCadastros { get; } = [];

    public IReadOnlyList<ItemMenu> Menus { get; } =
    [
        ItemMenu.Submenu("_Documentos fiscais",
            ItemMenu.EmBreve("Notas fiscais de entrada"),
            ItemMenu.EmBreve("Notas fiscais de saída"),
            ItemMenu.EmBreve("Conhecimentos de transporte"),
            ItemMenu.EmBreve("Importação de XML")),
        ItemMenu.Submenu("_Apuração",
            ItemMenu.EmBreve("ICMS e IPI"),
            ItemMenu.EmBreve("PIS e COFINS"),
            ItemMenu.EmBreve("ISS"),
            ItemMenu.EmBreve("Simples Nacional")),
        ItemMenu.Submenu("_Obrigações",
            ItemMenu.EmBreve("SPED Fiscal — EFD ICMS/IPI"),
            ItemMenu.EmBreve("SPED EFD-Contribuições"),
            ItemMenu.EmBreve("Guias de recolhimento")),
        ItemMenu.Submenu("_Relatórios",
            ItemMenu.EmBreve("Livros fiscais"),
            ItemMenu.EmBreve("Resumo de apuração")),
    ];

    public IReadOnlyList<AtalhoTela> Atalhos { get; } = [];

    public void RegistrarTelas(CatalogoTelas catalogo)
    {
        // Nenhuma tela própria ainda.
    }
}
