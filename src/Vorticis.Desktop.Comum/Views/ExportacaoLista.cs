using System.Collections;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Vorticis.Application.Relatorios;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra.Relatorios;

namespace Vorticis.Desktop.Comum.Views;

public enum FormatoLista { Excel, Csv, Pdf }

/// <summary>
/// Exportar a lista: qualquer grade de registros vira planilha (.xlsx), CSV ou PDF com exatamente o que está na tela —
/// as colunas visíveis, as linhas que passaram pela busca e pelos filtros, na ordem escolhida pelo usuário.
/// </summary>
public static class ExportacaoLista
{
    /// <summary>Monta a tabela das colunas visíveis (com título) e das linhas informadas.</summary>
    public static TabelaRelatorio Montar(string titulo, IReadOnlyList<LeitorColuna> leitores, IEnumerable<object> linhas)
    {
        var colunas = leitores.Where(l => l.Visivel() && l.Titulo.Trim().Length > 0).ToList();
        var lista = linhas.ToList();
        var tipos = colunas.Select(c => TipoDe(lista.Select(c.Valor).FirstOrDefault(v => v is not null))).ToList();
        var resultado = lista.Select(l => new LinhaRelatorio([.. colunas.Select((c, i) => Valor(c, l, tipos[i]))])).ToList();
        return new TabelaRelatorio(titulo, [.. colunas.Select((c, i) => new ColunaRelatorio(c.Titulo, tipos[i]))], resultado,
                                   $"{resultado.Count} {(resultado.Count == 1 ? "registro" : "registros")}");
    }

    private static TipoColuna TipoDe(object? v) => v switch
    {
        decimal or double or float => TipoColuna.Valor,
        DateOnly or DateTime or DateTimeOffset => TipoColuna.Data,
        int or long or short => TipoColuna.Inteiro,
        _ => TipoColuna.Texto,
    };

    private static object? Valor(LeitorColuna c, object linha, TipoColuna tipo) => (tipo, c.Valor(linha)) switch
    {
        (_, null) => null,
        (TipoColuna.Valor, var v) => Convert.ToDecimal(v, System.Globalization.CultureInfo.InvariantCulture),
        (TipoColuna.Data, DateTime d) => DateOnly.FromDateTime(d),
        (TipoColuna.Data, DateTimeOffset d) => DateOnly.FromDateTime(d.LocalDateTime),
        (TipoColuna.Data, DateOnly d) => d,
        (TipoColuna.Inteiro, var v) => Convert.ToInt64(v, System.Globalization.CultureInfo.InvariantCulture),
        _ => c.Texto(linha),
    };

    /// <summary>Linhas como a grade mostra: filtradas e na ordem da visão (ou a fonte, se a grade ainda não tem visão).</summary>
    public static IEnumerable<object> LinhasVisiveis(DataGrid g)
        => g.CollectionView is IEnumerable v ? v.Cast<object>().Where(o => o is not null) : FiltroGrade.Linhas(g);

    /// <summary>Título do arquivo: o da janela da função (ou o do programa).</summary>
    public static string TituloDe(DataGrid g)
    {
        var titulo = (TopLevel.GetTopLevel(g) as Window)?.Title;
        return string.IsNullOrWhiteSpace(titulo) ? "Lista" : titulo.Split('—')[0].Trim();
    }

    /// <summary>Contexto do módulo da tela (permissões, empresa e usuário), se houver.</summary>
    public static ViewModels.IContexto? ContextoDe(DataGrid g)
        => g.GetSelfAndVisualAncestors().OfType<Control>().Select(c => c.DataContext).OfType<IComContexto>().FirstOrDefault()?.Contexto;

    /// <summary>Gera o arquivo; planilha e CSV seguem a permissão "Exportar relatórios para planilha", como nos relatórios.</summary>
    public static (byte[]? Conteudo, string? Erro) Gerar(DataGrid g, FormatoLista formato)
    {
        var contexto = ContextoDe(g);
        if (formato != FormatoLista.Pdf && contexto is not null && contexto.Sessao is not null && !contexto.Pode(Permissao.RelatorioExportar))
            return (null, $"Seu perfil não permite: {Permissao.RelatorioExportar.Descricao()}.");
        var tabela = Montar(TituloDe(g), FiltroGrade.Leitores(g), LinhasVisiveis(g));
        if (tabela.Colunas.Count == 0) return (null, "A lista não tem colunas para exportar.");
        return formato switch
        {
            FormatoLista.Excel => (ExportadorExcel.Gerar([tabela]), null),
            FormatoLista.Csv => (ExportacaoRelatorio.Csv(tabela), null),
            _ => (new GeradorPdf().Gerar(ExportacaoRelatorio.ParaPdf(tabela.Titulo,
                    [contexto?.DescricaoEmpresa ?? "", $"Emitido em {DateTime.Now:dd/MM/yyyy HH:mm}{(contexto?.Sessao is { } s ? $" por {s.Nome}" : "")}"],
                    [tabela], tabela.Colunas.Count > 6)).Conteudo, null),
        };
    }

    public static string Extensao(FormatoLista f) => f switch { FormatoLista.Excel => ".xlsx", FormatoLista.Csv => ".csv", _ => ".pdf" };
}
