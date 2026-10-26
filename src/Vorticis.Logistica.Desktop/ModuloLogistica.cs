using Vorticis.Desktop.Comum.Modulos;
using C = Vorticis.Desktop;
using E = Vorticis.Erp.Desktop;
using F = Vorticis.Fiscal.Desktop;
using Fo = Vorticis.Folha.Desktop;
using Fr = Vorticis.Frota.Desktop;

namespace Vorticis.Logistica.Desktop;

/// <summary>
/// Vorticis Logística: o programa da transportadora. Reúne num menu só as telas do Frota (operação e manutenção), do Gerencial
/// (financeiro, faturamento, compras e estoque), do Fiscal, do Contábil e da Folha. As telas são as dos módulos (mesmas regras,
/// permissões e banco); a empresa ativa é a transportadora, cadastrada já com todos esses módulos.
/// </summary>
public sealed class ModuloLogistica() : ModuloCombinado(Frota, Contabil, Fiscal, Folha, Gerencial), Vorticis.Desktop.Comum.Modulos.IModuloDesktop
{
    private static readonly Fr.ModuloFrota Frota = new();
    private static readonly C.ModuloContabil Contabil = new();
    private static readonly F.ModuloFiscal Fiscal = new();
    private static readonly Fo.ModuloFolha Folha = new();
    private static readonly E.ModuloErp Gerencial = new();

    /// <summary>Telas (DataTemplates) dos módulos reunidos, do App.axaml de cada um.</summary>
    public static IEnumerable<Avalonia.Application> Apps() => [new Fr.App(), new C.App(), new F.App(), new Fo.App(), new E.App()];

    public override string Nome => "Vorticis Logística";
    public override Vorticis.Application.Cadastros.ModulosEmpresa ModuloEmpresa => Vorticis.Application.Cadastros.ModulosEmpresa.Frota;
    public override string NomeCurto => "Logística";
    public override string Subtitulo => "Frota, manutenção, financeiro, fiscal, contábil e folha da transportadora";
    public override string NomeExecutavel => "VorticisLogistica";
    public override string CorDestaque => "#0F766E";   // verde-petróleo (ícone e selo do programa)
    public override string CorBarra => "#134E4A";
    public string RotuloEmpresas => "Transportadoras";
    public string RotuloEmpresaAtiva => "Transportadora";

    private static ItemMenu? Topo(Vorticis.Desktop.Comum.Modulos.IModuloDesktop parte, string titulo) =>
        parte.Menus.FirstOrDefault(m => m.Titulo.Replace("_", "") == titulo);

    public override IReadOnlyList<ItemMenu> ItensCadastros =>
    [
        .. DaParte(Frota, Frota.ItensCadastros),
        ItemMenu.Separador,
        ItemMenu.Submenu("_Gerencial e financeiro", [.. DaParte(Gerencial, Gerencial.ItensCadastros)]),
        ItemMenu.Submenu("_Contábil", [.. DaParte(Contabil, Contabil.ItensCadastros)]),
        ItemMenu.Submenu("_Fiscal", [.. DaParte(Fiscal, Fiscal.ItensCadastros)]),
        ItemMenu.Submenu("F_olha de pagamento", [.. DaParte(Folha, Folha.ItensCadastros)]),
        ItemMenu.Separador,
        ItemMenu.Submenu("Parâmetros dos _módulos",
            ItemMenu.Tela("Gerencial e financeiro", Chave(Gerencial, TelasComuns.Parametros)),
            ItemMenu.Tela("Contábil", Chave(Contabil, TelasComuns.Parametros)),
            ItemMenu.Tela("Fiscal", Chave(Fiscal, TelasComuns.Parametros)),
            ItemMenu.Tela("Folha de pagamento", Chave(Folha, TelasComuns.Parametros))),
    ];

    public override IReadOnlyList<ItemMenu> Menus
    {
        get
        {
            var gerencial = DaParte(Gerencial, Gerencial.Menus);
            ItemMenu? G(string titulo) => gerencial.FirstOrDefault(m => m.Titulo.Replace("_", "") == titulo);
            var financeiro = G("Financeiro")!;
            var relatoriosFrota = DaParte(Frota, Topo(Frota, "Relatórios")!.Filhos!);
            return
            [
                .. DaParte(Frota, Frota.Menus.Where(m => m.Titulo.Replace("_", "") is "Operação" or "Manutenção")),
                ItemMenu.Submenu("_Emissão",
                    ItemMenu.EmBreve("CT-e – Conhecimento de transporte"),
                    ItemMenu.EmBreve("MDF-e – Manifesto de documentos fiscais"),
                    ItemMenu.EmBreve("NFS-e – Nota fiscal de serviço (Padrão Nacional)")),
                ItemMenu.Submenu("_Financeiro",
                    [.. financeiro.Filhos!, ItemMenu.Separador,
                     .. new[] { G("Faturamento"), G("Compras"), G("Estoque"), G("Monitores") }.Where(m => m is not null).Select(m => m! with { Titulo = m.Titulo.Replace("_", "") })]),
                Submenu("F_iscal", Fiscal),
                Submenu("_Contábil", Contabil),
                Submenu("Fol_ha", Folha),
                ItemMenu.Submenu("_Relatórios",
                    [.. relatoriosFrota, ItemMenu.Separador,
                     ItemMenu.Submenu("_Gerencial e financeiro", [.. G("Relatórios")!.Filhos!])]),
            ];
        }
    }
}

/// <summary>Janela principal do Vorticis Logística.</summary>
public sealed class MainViewModel() : MainViewModelBase(new ModuloLogistica());
