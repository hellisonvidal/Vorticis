using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Relatorios;

namespace Vorticis.Fiscal.Desktop.ViewModels;

public sealed record ResultadoImportacaoLinha(string Arquivo, string Situacao, string Numero, string Operacao, string Valor, string Mensagem);

/// <summary>Documentos fiscais › Importação de XML: arquivos ou pastas (inclusive .zip) com NF-e, NFC-e e NFS-e nacional.</summary>
public partial class ImportacaoXmlViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<ResultadoImportacaoLinha> Resultados { get; } = [];

    [ObservableProperty] private string _pasta = "";
    [ObservableProperty] private bool _incluirSubpastas = true;
    [ObservableProperty] private string _resumo = "";
    public bool PodeImportar => Contexto.Pode(Permissao.FiscalDocumentosImportar);

    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeImportar));

    [RelayCommand]
    private Task ImportarPastaAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (string.IsNullOrWhiteSpace(Pasta) || !Directory.Exists(Pasta)) { Erros.Add("Selecione uma pasta existente."); return; }
        MostrarResultadoImportacao(await app.Fiscal().Documentos.ImportarPastaAsync(empresa, Pasta.Trim(), IncluirSubpastas, usuario));
    }, Permissao.FiscalDocumentosImportar);

    /// <summary>Importa os arquivos escolhidos na tela (.xml e .zip).</summary>
    public Task ImportarArquivosAsync(IReadOnlyList<string> caminhos) => ExecutarAsync(async (app, empresa, usuario) =>
        MostrarResultadoImportacao(await app.Fiscal().Documentos.ImportarArquivosAsync(empresa, caminhos, usuario)), Permissao.FiscalDocumentosImportar);

    private void MostrarResultadoImportacao(Vorticis.Domain.Comum.Resultado<ResultadoImportacao> r)
    {
        Resultados.Clear();
        if (!r.Sucesso) { MostrarResultado(r, ""); Resumo = ""; return; }
        foreach (var a in r.Valor.Arquivos)
            Resultados.Add(new ResultadoImportacaoLinha(a.Arquivo, a.Situacao switch
            {
                SituacaoImportacao.Importado => "Importado", SituacaoImportacao.Duplicado => "Já importado", SituacaoImportacao.Cancelado => "Cancelamento",
                SituacaoImportacao.Ignorado => "Ignorado", _ => "Erro",
            }, a.Numero ?? "", a.Operacao?.Rotulo() ?? "", a.Valor == 0 ? "" : Numeros.Moeda(a.Valor), a.Mensagem));
        Resumo = r.Valor.Resumo;
        Mensagem = r.Valor.Erros == 0 ? "Importação concluída." : "Importação concluída com erros: veja a lista abaixo.";
    }
}

public sealed record DocumentoLinha(DocumentoFiscal Documento)
{
    public long Id => Documento.Id;
    public string Data => Documento.DataLivro.ToString("dd/MM/yyyy");
    public string Modelo => Documento.Modelo.Rotulo();
    public string Operacao => Documento.Operacao.Rotulo();
    public string Numero => Documento.Numero;
    public string Serie => Documento.Serie;
    public string Participante => Documento.NomeParticipante ?? "";
    public string Valor => Numeros.Moeda(Documento.Totais.Total);
    public string Icms => Numeros.MoedaOuVazio(Documento.Totais.Icms);
    public string Iss => Numeros.MoedaOuVazio(Documento.Totais.Iss);
    public string Situacao => Documento.Situacao.Rotulo();
    public string Contabilizado => Documento.Contabilizado ? "Sim" : "Não";
    public string Chave => Documento.Chave;
}

public sealed record ItemDocumentoLinha(ItemFiscal Item)
{
    public int N => Item.Numero;
    public string Cfop => Item.Cfop;
    public string Ncm => Item.Ncm ?? "";
    public string Descricao => Item.Descricao ?? "";
    public string Valor => Numeros.Moeda(Item.ValorContabil);
    public string CstIcms => Item.CstIcms ?? "";
    public string Icms => Numeros.MoedaOuVazio(Item.Icms);
    public string IcmsSt => Numeros.MoedaOuVazio(Item.IcmsSt);
    public string Ipi => Numeros.MoedaOuVazio(Item.Ipi);
    public string Pis => Numeros.MoedaOuVazio(Item.Pis);
    public string Cofins => Numeros.MoedaOuVazio(Item.Cofins);
    public string Iss => Numeros.MoedaOuVazio(Item.Iss);
}

/// <summary>Documentos fiscais › Consulta de documentos, com filtros, itens (CFOP, NCM, CST e tributos) e exclusão de importados.</summary>
public partial class DocumentosViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public static IReadOnlyList<string> Operacoes { get; } = ["Todas", "Entradas", "Saídas"];
    public static IReadOnlyList<string> Modelos { get; } = ["Todos", "NF-e (55)", "NFC-e (65)", "NFS-e"];
    public static IReadOnlyList<string> Situacoes { get; } = ["Todas", "Autorizadas", "Canceladas", "Denegadas"];
    public static IReadOnlyList<string> Contabilizacoes { get; } = ["Todas", "Contabilizadas", "Não contabilizadas"];

    public ObservableCollection<DocumentoLinha> Documentos { get; } = [];
    public ObservableCollection<ItemDocumentoLinha> Itens { get; } = [];

    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _fim = DateTime.Today;
    [ObservableProperty] private int _operacao;
    [ObservableProperty] private int _modelo;
    [ObservableProperty] private int _situacao;
    [ObservableProperty] private int _contabilizacao;
    [ObservableProperty] private string _texto = "";
    [ObservableProperty] private string _cfop = "";
    [ObservableProperty] private string _resumo = "";
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ExcluirCommand))] private DocumentoLinha? _selecionado;

    public bool PodeExcluir => Contexto.Pode(Permissao.FiscalDocumentosImportar);

    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeExcluir));

    partial void OnSelecionadoChanged(DocumentoLinha? value) => _ = CarregarItensAsync(value);

    private async Task CarregarItensAsync(DocumentoLinha? doc)
    {
        Itens.Clear();
        if (doc is null || Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var r = await app.Fiscal().Documentos.ItensAsync(empresa, doc.Id, sessao.Id);
        if (r.Sucesso && ReferenceEquals(Selecionado, doc)) foreach (var i in r.Valor) Itens.Add(new ItemDocumentoLinha(i));
    }

    [RelayCommand]
    private Task ConsultarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var f = new FiltroDocumentos(
            Inicio is { } i ? DateOnly.FromDateTime(i) : null, Fim is { } fi ? DateOnly.FromDateTime(fi) : null,
            Operacao switch { 1 => TipoOperacao.Entrada, 2 => TipoOperacao.Saida, _ => null },
            Modelo switch { 1 => ModeloDocumento.NFe55, 2 => ModeloDocumento.NFCe65, 3 => ModeloDocumento.NFSe, _ => null },
            Situacao switch { 1 => SituacaoDocumento.Autorizada, 2 => SituacaoDocumento.Cancelada, 3 => SituacaoDocumento.Denegada, _ => null },
            Texto, Cfop, Contabilizacao switch { 1 => true, 2 => false, _ => null });
        var r = await app.Fiscal().Documentos.ConsultarAsync(empresa, f, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Documentos.Clear();
        Itens.Clear();
        foreach (var d in r.Valor) Documentos.Add(new DocumentoLinha(d));
        var ativos = r.Valor.Where(d => d.Situacao == SituacaoDocumento.Autorizada).ToList();
        Resumo = $"{r.Valor.Count} documento(s) · total (autorizados) {Numeros.Moeda(ativos.Sum(d => d.Totais.Total))} · ICMS {Numeros.Moeda(ativos.Sum(d => d.Totais.Icms))} · " +
                 $"ISS {Numeros.Moeda(ativos.Sum(d => d.Totais.Iss))}" + (r.Valor.Count >= f.Limite ? $" · limitado a {f.Limite} documentos: refine o filtro" : "");
        Mensagem = r.Valor.Count == 0 ? "Nenhum documento encontrado." : null;
    }, Permissao.FiscalDocumentosConsultar);

    [RelayCommand(CanExecute = nameof(TemSelecionado))]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionado is not { } s) return;
        if (!await Contexto.ConfirmarAsync("Excluir documento", $"Excluir o documento {s.Modelo} {s.Numero} importado? O XML original também será removido.", "Excluir", "Cancelar")) return;
        var r = await app.Fiscal().Documentos.ExcluirAsync(empresa, [s.Id], usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Documentos.Remove(s);
        Mensagem = "Documento excluído.";
    }, Permissao.FiscalDocumentosImportar);

    private bool TemSelecionado() => Selecionado is not null;
}

/// <summary>Base dos livros de Entradas e de Saídas.</summary>
public abstract partial class LivroFiscalViewModel(IContexto contexto) : RelatorioFiscalViewModel(contexto)
{
    private (DateOnly Ini, DateOnly Fim) _periodo;

    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _fim = DateTime.Today;

    protected abstract TipoOperacao Operacao { get; }
    public abstract string Titulo { get; }
    protected override string TituloRelatorio => Titulo;
    protected override string Parametros => $"Período: {_periodo.Ini:dd/MM/yyyy} a {_periodo.Fim:dd/MM/yyyy}";
    protected override string NomeArquivo => $"{(Operacao == TipoOperacao.Entrada ? "livro_entradas" : "livro_saidas")}_{_periodo.Ini:yyyyMMdd}_{_periodo.Fim:yyyyMMdd}";

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var ini = Data(Inicio, new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1));
        var fim = Data(Fim, ini);
        var r = await app.Fiscal().Documentos.LivroAsync(empresa, Operacao, ini, fim, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); Limpar(); return; }
        _periodo = (ini, fim);
        var itens = await app.Fiscal().Documentos.ResumoPorCfopAsync(empresa, Operacao, ini, fim, usuario);
        Mostrar(itens.Sucesso ? [TabelasFiscais.Livro(Operacao, r.Valor), TabelasFiscais.ResumoCfop(itens.Valor)] : [TabelasFiscais.Livro(Operacao, r.Valor)]);
        Mensagem = r.Valor.Count == 0 ? "Nenhum documento no período." : "Escolha o formato em Exportar: PDF, Excel (.xlsx) ou CSV.";
    }, Permissao.FiscalRelatorios);
}

public sealed class LivroEntradasViewModel(IContexto contexto) : LivroFiscalViewModel(contexto)
{
    protected override TipoOperacao Operacao => TipoOperacao.Entrada;
    public override string Titulo => "Livro de Registro de Entradas";
}

public sealed class LivroSaidasViewModel(IContexto contexto) : LivroFiscalViewModel(contexto)
{
    protected override TipoOperacao Operacao => TipoOperacao.Saida;
    public override string Titulo => "Livro de Registro de Saídas";
}

/// <summary>Relatórios › Resumo por CFOP (valor contábil e tributos por CFOP, entradas e saídas).</summary>
public partial class ResumoCfopViewModel(IContexto contexto) : RelatorioFiscalViewModel(contexto)
{
    private (DateOnly Ini, DateOnly Fim) _periodo;

    public static IReadOnlyList<string> Operacoes { get; } = ["Entradas e saídas", "Somente entradas", "Somente saídas"];

    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _fim = DateTime.Today;
    [ObservableProperty] private int _operacao;

    protected override string TituloRelatorio => "Resumo por CFOP";
    protected override string Parametros => $"Período: {_periodo.Ini:dd/MM/yyyy} a {_periodo.Fim:dd/MM/yyyy} · {Operacoes[Operacao]}";
    protected override string NomeArquivo => $"resumo_cfop_{_periodo.Ini:yyyyMMdd}_{_periodo.Fim:yyyyMMdd}";

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var ini = Data(Inicio, new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1));
        var fim = Data(Fim, ini);
        var r = await app.Fiscal().Documentos.ResumoPorCfopAsync(empresa, Operacao switch { 1 => TipoOperacao.Entrada, 2 => TipoOperacao.Saida, _ => null }, ini, fim, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); Limpar(); return; }
        _periodo = (ini, fim);
        Mostrar(TabelasFiscais.ResumoCfop(r.Valor));
        Mensagem = r.Valor.Count == 0 ? "Nenhum documento no período." : "Escolha o formato em Exportar: PDF, Excel (.xlsx) ou CSV.";
    }, Permissao.FiscalRelatorios);
}
