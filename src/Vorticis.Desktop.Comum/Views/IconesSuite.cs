using Avalonia.Controls;
using Avalonia.Media;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Qual ícone usar para cada módulo e para cada categoria de menu (Cadastros, Lançamentos, Relatórios...).</summary>
public static class IconesSuite
{
    /// <summary>Ícone do módulo (a cor é a CorDestaque dele): Escritório prédio, Contábil balança, Fiscal nota, Folha crachá, Gerencial gráfico, Condomínio residencial.</summary>
    public static Geometry DoModulo(string nomeExecutavel) => nomeExecutavel switch
    {
        "VorticisEscritorio" => Icones.Domain,
        "VorticisContabil" => Icones.ScaleBalance,
        "VorticisFiscal" => Icones.ReceiptText,
        "VorticisFolha" => Icones.CardAccountDetails,
        "VorticisGerencial" => Icones.ChartLine,
        "VorticisCondominio" => Icones.HomeCity,
        _ => Icones.FolderMultiple,
    };

    /// <summary>Ícone da categoria do menu pelo título ("_Cadastros", "Re_latórios"...); pasta quando não reconhece.</summary>
    public static Geometry DaCategoria(string titulo)
    {
        var t = Vorticis.Desktop.Comum.ViewModels.SuiteViewModel.Normalizar(titulo.Replace("_", ""));
        bool Tem(params string[] partes) => partes.Any(t.Contains);
        if (Tem("cadastro")) return Icones.FolderAccount;
        if (Tem("imobiliz")) return Icones.Sofa;
        if (Tem("estoque")) return Icones.Warehouse;
        if (Tem("faturamento")) return Icones.FileSendOutline;
        if (Tem("compras")) return Icones.Cart;
        if (Tem("lancamento")) return Icones.PencilPlusOutline;
        if (Tem("consulta")) return Icones.Magnify;
        if (Tem("relatorio", "resumo", "gerenciais")) return Icones.FileChart;
        if (Tem("integra", "exportar", "importar", "sped", "esocial")) return Icones.SwapHorizontal;
        if (Tem("obriga")) return Icones.CalendarCheck;
        if (Tem("configura", "parametro")) return Icones.Cog;
        if (Tem("ajuda", "manual")) return Icones.HelpCircle;
        if (Tem("livro")) return Icones.BookOpenPageVariant;
        if (Tem("document", "notas")) return Icones.ReceiptText;
        if (Tem("financ", "duplicata", "pro-labore", "prolabore")) return Icones.Cash;
        if (Tem("apura", "calculo", "deprecia")) return Icones.CalculatorVariantOutline;
        if (Tem("fechamento", "concilia")) return Icones.CheckCircle;
        if (Tem("monitor")) return Icones.MonitorDashboard;
        if (Tem("organiza")) return Icones.FileTree;
        if (Tem("administra", "banco de dados")) return Icones.ApplicationCogOutline;
        if (Tem("contabeis")) return Icones.BriefcaseOutline;
        if (Tem("funcionario", "folha")) return Icones.AccountGroup;
        return Icones.FolderOutline;
    }

    /// <summary>Ícone de uma função pelo título (Empresas, Plano de contas, Balancete...); o da categoria quando não reconhece.</summary>
    public static Geometry DaFuncao(string titulo, Geometry categoria)
    {
        var t = Vorticis.Desktop.Comum.ViewModels.SuiteViewModel.Normalizar(titulo);
        bool Tem(params string[] partes) => partes.Any(t.Contains);
        if (Tem("sair")) return Icones.Backburger;
        if (Tem("tela inicial", "inicio")) return Icones.Home;
        if (Tem("empresa", "condominio", "escritorio")) return Icones.Domain;
        if (Tem("usuario", "perfil", "acesso")) return Icones.ShieldAccount;
        if (Tem("funcionario", "colaborador", "socio", "contador", "cliente", "fornecedor", "participante", "responsave")) return Icones.Account;
        if (Tem("plano de contas")) return Icones.FileTree;
        if (Tem("balancete", "balanco", "saldos")) return Icones.ScaleBalance;
        if (Tem("razao", "diario", "livro")) return Icones.BookOpenPageVariant;
        if (Tem("lancamento")) return Icones.PencilPlusOutline;
        if (Tem("importa")) return Icones.Import;
        if (Tem("exporta", "sped", "ecd", "ecf", "efd")) return Icones.Export;
        if (Tem("nota", "document", "xml")) return Icones.ReceiptText;
        if (Tem("backup", "banco", "restaur")) return Icones.DatabaseOutline;
        if (Tem("certificado", "senha", "bloqueio", "fechamento", "encerramento")) return Icones.Lock;
        if (Tem("calcul", "apura", "deprecia")) return Icones.CalculatorVariantOutline;
        if (Tem("pdf")) return Icones.FilePdfBox;
        if (Tem("relatorio", "resumo")) return Icones.FileChart;
        if (Tem("obriga", "calendario", "prazo", "vencimento")) return Icones.CalendarCheck;
        if (Tem("parametro", "configura", "menus")) return Icones.Cog;
        if (Tem("manual", "ajuda", "sobre", "licen")) return Icones.HelpCircle;
        if (Tem("consulta", "pesquisa", "busca")) return Icones.Magnify;
        if (Tem("bens", "imobiliz")) return Icones.BriefcaseOutline;
        if (Tem("pedido", "venda", "compra", "estoque", "produto")) return Icones.Cart;
        if (Tem("financeiro", "pagar", "receber", "pagamento", "cobranca", "caixa")) return Icones.Cash;
        if (Tem("painel", "monitor", "indicador")) return Icones.ChartLine;
        return categoria;
    }

    /// <summary>PathIcon pronto para o ícone da categoria (menus montados em código).</summary>
    public static PathIcon IconeDe(Geometry g, double tamanho = 16) => new() { Data = g, Width = tamanho, Height = tamanho };
}
