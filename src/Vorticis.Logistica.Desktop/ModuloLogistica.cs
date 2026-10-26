using Vorticis.Desktop.Comum.Modulos;
using C = Vorticis.Desktop;
using E = Vorticis.Erp.Desktop;
using F = Vorticis.Fiscal.Desktop;
using Fo = Vorticis.Folha.Desktop;
using Fr = Vorticis.Frota.Desktop;

namespace Vorticis.Logistica.Desktop;

/// <summary>
/// Vorticis Suite Logística: o programa da transportadora. Reúne num menu só as telas do Frota (operação e manutenção), do Gerencial
/// (financeiro, faturamento, compras e estoque) e, no menu Gerencial, as do Contábil e da Folha. Não mostra o Fiscal (o usuário usa o
/// Financeiro), o calendário de obrigações nem o painel do empresário; todos os relatórios ficam no menu Relatórios. As telas são as dos módulos (mesmas regras,
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

    public override string Nome => "Vorticis Suite Logística";
    public override Vorticis.Application.Cadastros.ModulosEmpresa ModuloEmpresa => Vorticis.Application.Cadastros.ModulosEmpresa.Frota;
    public override string NomeCurto => "Logística";
    public override string Subtitulo => "Frota, manutenção, financeiro, contábil e folha da transportadora";
    public override string NomeExecutavel => "VorticisLogistica";
    public override string CorDestaque => "#0F766E";   // verde-petróleo (ícone e selo do programa)
    public override string CorBarra => "#134E4A";
    public bool UsaCalendarioObrigacoes => false;
    public string RotuloEmpresas => "Transportadoras";
    public string RotuloEmpresaAtiva => "Transportadora";

    /// <summary>Atalhos das partes, sem os de telas que este programa não mostra (ex.: Consulta de documentos fiscais).</summary>
    public override IReadOnlyList<AtalhoTela> Atalhos =>
        [.. base.Atalhos.Where(a => a.Chave != Chave(Fiscal, F.ViewModels.Telas.Documentos))];

    private static ItemMenu? Topo(Vorticis.Desktop.Comum.Modulos.IModuloDesktop parte, string titulo) =>
        parte.Menus.FirstOrDefault(m => m.Titulo.Replace("_", "") == titulo);

    public override IReadOnlyList<ItemMenu> ItensCadastros =>
    [
        .. DaParte(Frota, Frota.ItensCadastros),
        ItemMenu.Separador,
        ItemMenu.Submenu("_Gerencial e financeiro", [.. DaParte(Gerencial, Gerencial.ItensCadastros)]),
        ItemMenu.Submenu("_Contábil", [.. DaParte(Contabil, Contabil.ItensCadastros)]),
        ItemMenu.Submenu("F_olha de pagamento", [.. DaParte(Folha, Folha.ItensCadastros)]),
        ItemMenu.Separador,
        ItemMenu.Submenu("Parâmetros dos _módulos",
            ItemMenu.Tela("Gerencial e financeiro", Chave(Gerencial, TelasComuns.Parametros)),
            ItemMenu.Tela("Contábil", Chave(Contabil, TelasComuns.Parametros)),
            ItemMenu.Tela("Fiscal", Chave(Fiscal, TelasComuns.Parametros)),
            ItemMenu.Tela("Folha de pagamento", Chave(Folha, TelasComuns.Parametros))),
    ];

    private static string Sem(ItemMenu m) => m.Titulo.Replace("_", "");

    public override IReadOnlyList<ItemMenu> Menus
    {
        get
        {
            var gerencial = DaParte(Gerencial, Gerencial.Menus);
            ItemMenu? G(string titulo) => gerencial.FirstOrDefault(m => Sem(m) == titulo);
            var financeiro = G("Financeiro")!;
            // Relatórios de cada área, reunidos no menu Relatórios (saem dos menus de origem)
            var relFrota = DaParte(Frota, Topo(Frota, "Relatórios")!.Filhos!);
            var corte = relFrota.ToList().FindIndex(i => i.EhSeparador);
            var relOperacao = relFrota.Take(corte).ToList();
            var relManutencao = relFrota.Skip(corte + 1).ToList();
            // sem o Painel do empresário (e o separador que o seguia)
            var relGerencial = G("Relatórios")!.Filhos!.Where(i => i.Chave != E.ViewModels.ErpTelas.Painel).SkipWhile(i => i.EhSeparador).ToList();
            var relContabil = DaParte(Contabil, Topo(Contabil, "Relatórios")!.Filhos!);
            var relFolha = DaParte(Folha, Topo(Folha, "Relatórios")!.Filhos!);
            var operacao = DaParte(Frota, Frota.Menus.Where(m => Sem(m) == "Operação")).Single();
            return
            [
                operacao with { Filhos = [.. operacao.Filhos!, ItemMenu.Separador, ItemMenu.Submenu("_Coletas", ItemMenu.EmBreve("Ordens de coleta"))] },
                .. DaParte(Frota, Frota.Menus.Where(m => Sem(m) == "Manutenção")),
                ItemMenu.Submenu("_Emissão",
                    ItemMenu.EmBreve("CT-e – Conhecimento de transporte"),
                    ItemMenu.EmBreve("MDF-e – Manifesto de documentos fiscais"),
                    ItemMenu.EmBreve("NFS-e – Nota fiscal de serviço (Padrão Nacional)")),
                ItemMenu.Submenu("_Financeiro",
                    [.. financeiro.Filhos!, ItemMenu.Separador,
                     .. new[] { G("Faturamento"), G("Compras"), G("Estoque"), G("Monitores") }.Where(m => m is not null).Select(m => m! with { Titulo = m.Titulo.Replace("_", "") })]),
                ItemMenu.Submenu("_Gerencial",
                    Submenu("_Contábil", Contabil, m => Sem(m) != "Relatórios"),
                    Submenu("_Folha", Folha, m => Sem(m) != "Relatórios")),
                ItemMenu.Submenu("_Relatórios",
                    ItemMenu.Submenu("_Frota e operação", [.. relOperacao]),
                    ItemMenu.Submenu("_Manutenção", [.. relManutencao]),
                    ItemMenu.Submenu("_Financeiro e gerencial", [.. relGerencial]),
                    ItemMenu.Submenu("_Contábil", [.. relContabil]),
                    ItemMenu.Submenu("_Folha", [.. relFolha])),
            ];
        }
    }
}

/// <summary>Janela principal do Vorticis Suite Logística.</summary>
public sealed class MainViewModel() : MainViewModelBase(new ModuloLogistica());
