using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Desktop.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Produtos;
using Vorticis.Infra;

namespace Vorticis.Fiscal.Desktop.ViewModels;

/// <summary>Busca de participante e de produto por texto digitado (código, nome/descrição, CPF/CNPJ ou código de barras).</summary>
internal static class Resolvedores
{
    public static async Task<(long Id, string Codigo, string Nome)?> ParticipanteAsync(VorticisApp app, long empresa, long usuario, string texto)
    {
        texto = texto.Trim();
        var lista = await app.Participantes.ListarAsync(empresa, usuario);
        var p = lista.FirstOrDefault(x => x.Codigo == texto) ?? lista.FirstOrDefault(x => string.Equals(x.Nome, texto, StringComparison.CurrentCultureIgnoreCase))
                ?? lista.FirstOrDefault(x => x.CpfCnpj is { Length: > 0 } && x.CpfCnpj == new string(texto.Where(char.IsAsciiDigit).ToArray()));
        return p is null ? null : (p.Id, p.Codigo, p.Nome);
    }

    public static async Task<Produto?> ProdutoAsync(VorticisApp app, long empresa, long usuario, string texto, bool apenasAtivos)
    {
        texto = texto.Trim();
        var r = await app.Fiscal().Produtos.ListarAsync(empresa, apenasAtivos, texto, usuario);
        if (!r.Sucesso) return null;
        return r.Valor.FirstOrDefault(x => x.Codigo.ToString() == texto || x.Gtin == texto || string.Equals(x.Descricao, texto, StringComparison.CurrentCultureIgnoreCase))
               ?? (r.Valor.Count == 1 ? r.Valor[0] : null);
    }
}

public sealed record CentroOpcao(long? Id, string Texto, string? Codigo = null)
{
    public override string ToString() => Texto;
}

public sealed record ItemNotaLinha(ItemFiscal Item)
{
    public int N => Item.Numero;
    public string Produto => Item.CodProduto ?? "";
    public string Descricao => Item.Descricao ?? "";
    public string Cfop => Item.Cfop;
    public string Quantidade => Item.Quantidade.ToString("0.####", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
    public string Unitario => Numeros.Moeda(Item.ValorUnitario);
    public string Total => Numeros.Moeda(Item.ValorContabil);
    public string Icms => Numeros.MoedaOuVazio(Item.Icms);
    public string Ipi => Numeros.MoedaOuVazio(Item.Ipi);
    public string Pis => Numeros.MoedaOuVazio(Item.Pis);
    public string Cofins => Numeros.MoedaOuVazio(Item.Cofins);
    public string Iss => Numeros.MoedaOuVazio(Item.Iss);
    public string CentroCusto => Item.CentroCustoCodigo ?? "";
}

/// <summary>Campos de digitação de um item (texto, como digitado); converte de e para <see cref="ItemFiscal"/>.</summary>
public partial class ItemForm : ObservableObject
{
    [ObservableProperty] private string _produto = "";
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private string _cfop = "";
    [ObservableProperty] private string _ncm = "";
    [ObservableProperty] private string _cest = "";
    [ObservableProperty] private string _unidade = "UN";
    [ObservableProperty] private string _origem = "0";
    [ObservableProperty] private string _quantidade = "1";
    [ObservableProperty] private string _unitario = "";
    [ObservableProperty] private string _desconto = "";
    [ObservableProperty] private string _frete = "";
    [ObservableProperty] private string _seguro = "";
    [ObservableProperty] private string _outras = "";
    [ObservableProperty] private string _cstIcms = "";
    [ObservableProperty] private string _bcIcms = "";
    [ObservableProperty] private string _aliqIcms = "";
    [ObservableProperty] private string _icms = "";
    [ObservableProperty] private string _bcIcmsSt = "";
    [ObservableProperty] private string _icmsSt = "";
    [ObservableProperty] private string _cstIpi = "";
    [ObservableProperty] private string _bcIpi = "";
    [ObservableProperty] private string _aliqIpi = "";
    [ObservableProperty] private string _ipi = "";
    [ObservableProperty] private string _cstPis = "";
    [ObservableProperty] private string _bcPis = "";
    [ObservableProperty] private string _aliqPis = "";
    [ObservableProperty] private string _pis = "";
    [ObservableProperty] private string _cstCofins = "";
    [ObservableProperty] private string _bcCofins = "";
    [ObservableProperty] private string _aliqCofins = "";
    [ObservableProperty] private string _cofins = "";
    [ObservableProperty] private string _bcIss = "";
    [ObservableProperty] private string _aliqIss = "";
    [ObservableProperty] private string _iss = "";

    public long? ProdutoId { get; set; }
    public string? Gtin { get; set; }

    private static decimal D(string? t) => Numeros.LerValor(t) ?? 0m;
    private static string T(decimal v) => v == 0 ? "" : v.ToString("0.####", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
    private static string? N(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();

    public void Limpar()
    {
        Produto = Descricao = Cfop = Ncm = Cest = CstIcms = BcIcms = AliqIcms = Icms = BcIcmsSt = IcmsSt = CstIpi = BcIpi = AliqIpi = Ipi = "";
        CstPis = BcPis = AliqPis = Pis = CstCofins = BcCofins = AliqCofins = Cofins = BcIss = AliqIss = Iss = Unitario = Desconto = Frete = Seguro = Outras = "";
        (Unidade, Origem, Quantidade, ProdutoId, Gtin) = ("UN", "0", "1", null, null);
    }

    /// <summary>Preenche os campos com os padrões do produto para a operação (CFOP, CST, alíquotas, NCM, unidade).</summary>
    public void AplicarProduto(Produto p, TipoOperacao operacao)
    {
        (ProdutoId, Gtin, Produto, Descricao, Unidade, Ncm, Cest, Origem) = (p.Id, p.Gtin, p.Codigo.ToString(), p.Descricao, p.Unidade, p.Ncm ?? "", p.Cest ?? "", p.Origem);
        Cfop = (operacao == TipoOperacao.Entrada ? p.CfopEntrada : p.CfopSaida) ?? Cfop;
        CstIcms = (operacao == TipoOperacao.Entrada ? p.CstIcmsEntrada : p.CstIcmsSaida) ?? CstIcms;
        (AliqIcms, AliqIpi, AliqPis, AliqCofins) = (T(p.AliqIcms), T(p.AliqIpi), T(p.AliqPis), T(p.AliqCofins));
    }

    public void Carregar(ItemFiscal i)
    {
        (ProdutoId, Gtin) = (i.ProdutoId, i.Gtin);
        (Produto, Descricao, Cfop, Ncm, Cest, Unidade, Origem) = (i.CodProduto ?? "", i.Descricao ?? "", i.Cfop, i.Ncm ?? "", i.Cest ?? "", i.Unidade ?? "", i.Origem ?? "0");
        (Quantidade, Unitario, Desconto, Frete, Seguro, Outras) = (T(i.Quantidade), T(i.ValorUnitario), T(i.Desconto), T(i.Frete), T(i.Seguro), T(i.Outras));
        (CstIcms, BcIcms, AliqIcms, Icms, BcIcmsSt, IcmsSt) = (i.CstIcms ?? "", T(i.BcIcms), T(i.AliqIcms), T(i.Icms), T(i.BcIcmsSt), T(i.IcmsSt));
        (CstIpi, BcIpi, AliqIpi, Ipi) = (i.CstIpi ?? "", T(i.BcIpi), T(i.AliqIpi), T(i.Ipi));
        (CstPis, BcPis, AliqPis, Pis) = (i.CstPis ?? "", T(i.BcPis), T(i.AliqPis), T(i.Pis));
        (CstCofins, BcCofins, AliqCofins, Cofins) = (i.CstCofins ?? "", T(i.BcCofins), T(i.AliqCofins), T(i.Cofins));
        (BcIss, AliqIss, Iss) = (T(i.BcIss), T(i.AliqIss), T(i.Iss));
    }

    /// <summary>Base padrão dos tributos: produtos - desconto + frete + seguro + outras despesas.</summary>
    public decimal BaseItem() => Dinheiro(D(Quantidade) * D(Unitario)) - D(Desconto) + D(Frete) + D(Seguro) + D(Outras);

    private static decimal Dinheiro(decimal v) => Vorticis.Domain.Comum.Dinheiro.Arredondar(v);

    /// <summary>Calcula base (se vazia) e valor de cada tributo com alíquota informada e valor vazio: valor = base x alíquota / 100.</summary>
    public void CalcularTributos()
    {
        var @base = BaseItem();
        void C(ref string bc, string aliq, ref string valor)
        {
            if (D(aliq) <= 0 || D(valor) != 0) return;
            if (D(bc) == 0) bc = T(@base);
            valor = T(Dinheiro(D(bc) * D(aliq) / 100m));
        }
        var (bcIcms, icms) = (BcIcms, Icms); C(ref bcIcms, AliqIcms, ref icms); (BcIcms, Icms) = (bcIcms, icms);
        var (bcIpi, ipi) = (BcIpi, Ipi); C(ref bcIpi, AliqIpi, ref ipi); (BcIpi, Ipi) = (bcIpi, ipi);
        var (bcPis, pis) = (BcPis, Pis); C(ref bcPis, AliqPis, ref pis); (BcPis, Pis) = (bcPis, pis);
        var (bcCof, cof) = (BcCofins, Cofins); C(ref bcCof, AliqCofins, ref cof); (BcCofins, Cofins) = (bcCof, cof);
        var (bcIss, iss) = (BcIss, Iss); C(ref bcIss, AliqIss, ref iss); (BcIss, Iss) = (bcIss, iss);
    }

    public ItemFiscal Montar(long? centroId, string? centroCodigo) => new(
        0, N(Produto), N(Descricao), N(Ncm), N(Cest), Cfop.Trim(), N(Unidade), D(Quantidade), D(Unitario), 0, D(Desconto), D(Frete), D(Seguro), D(Outras), 0,
        N(CstIcms), N(Origem), D(BcIcms), D(AliqIcms), D(Icms), D(BcIcmsSt), D(IcmsSt), N(CstIpi), D(BcIpi), D(AliqIpi), D(Ipi),
        N(CstPis), D(BcPis), D(AliqPis), D(Pis), N(CstCofins), D(BcCofins), D(AliqCofins), D(Cofins), D(BcIss), D(AliqIss), D(Iss),
        Gtin, ProdutoId, centroId, centroCodigo);
}

/// <summary>
/// Lançamentos › Documentos fiscais › Entrada / Saída: digitação manual de nota fiscal (cabeçalho e itens), gravada nas mesmas tabelas da
/// importação de XML. Notas contabilizadas ficam somente para consulta.
/// </summary>
public abstract partial class DigitacaoDocumentoViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public static IReadOnlyList<string> Modelos { get; } = ["NF-e (55)", "NFC-e (65)", "NFS-e"];
    public static IReadOnlyList<ModeloDocumento> ModelosValores { get; } = [ModeloDocumento.NFe55, ModeloDocumento.NFCe65, ModeloDocumento.NFSe];

    public abstract TipoOperacao Operacao { get; }
    public string Titulo => $"Nota fiscal de {Operacao.Rotulo().ToLowerInvariant()}";
    public string RotuloParticipante => Operacao == TipoOperacao.Entrada ? "Fornecedor" : "Cliente";
    public string RotuloData => Operacao == TipoOperacao.Entrada ? "Data de entrada" : "Data de saída";

    public ObservableCollection<DocumentoLinha> Lista { get; } = [];
    public ObservableCollection<ItemNotaLinha> Itens { get; } = [];
    public ObservableCollection<CentroOpcao> Centros { get; } = [new CentroOpcao(null, "(sem centro de custo)")];
    public ItemForm Item { get; } = new();

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ExcluirCommand))] private DocumentoLinha? _selecionada;
    [ObservableProperty] private DateTime? _filtroInicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _filtroFim = DateTime.Today;
    [ObservableProperty] private int _modelo;
    [ObservableProperty] private string _serie = "1";
    [ObservableProperty] private string _numero = "";
    [ObservableProperty] private string _chave = "";
    [ObservableProperty] private DateTime? _emissao = DateTime.Today;
    [ObservableProperty] private DateTime? _dataLivro = DateTime.Today;
    [ObservableProperty] private string _natureza = "";
    [ObservableProperty] private string _participante = "";
    [ObservableProperty] private string _totalInformado = "";
    [ObservableProperty] private CentroOpcao? _centroDocumento;
    [ObservableProperty] private CentroOpcao? _centroItem;
    [ObservableProperty] private ItemNotaLinha? _itemSelecionado;
    [ObservableProperty] private string _totais = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PodeEditar))] private bool _contabilizado;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PodeEditar))] private bool _usaCentroCusto;
    [ObservableProperty] private string _situacaoDocumento = "Nova nota";

    private long _documentoId;
    private long _participanteId;
    private TotaisDocumento _retencoes = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, 0, 0, 0, 0);

    public bool PodeManter => Contexto.Pode(Permissao.FiscalDocumentosImportar);
    public bool PodeEditar => PodeManter && !Contabilizado;
    public bool MostraCentro => UsaCentroCusto;

    partial void OnUsaCentroCustoChanged(bool value) => OnPropertyChanged(nameof(MostraCentro));

    public void AtualizarPermissoes()
    {
        OnPropertyChanged(nameof(PodeManter));
        OnPropertyChanged(nameof(PodeEditar));
    }

    partial void OnSelecionadaChanged(DocumentoLinha? value)
    {
        if (value is not null) _ = CarregarDocumentoAsync(value.Id);
    }

    partial void OnItemSelecionadoChanged(ItemNotaLinha? value)
    {
        if (value is null) return;
        Item.Carregar(value.Item);
        CentroItem = Centros.FirstOrDefault(c => c.Id == value.Item.CentroCustoId) ?? Centros[0];
    }

    /// <summary>Carga ao abrir: configuração (centros de custo), centros e notas digitadas do período.</summary>
    public Task CarregarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var cfg = await app.Fiscal().Digitacao.ObterConfiguracaoAsync(empresa, usuario);
        UsaCentroCusto = cfg.Sucesso && cfg.Valor.CentroCustoAtivo;
        Centros.Clear();
        Centros.Add(new CentroOpcao(null, "(sem centro de custo)"));
        if (UsaCentroCusto)
            foreach (var c in (await app.PlanoContas.ListarCentrosAsync(empresa, usuario)).Where(c => c.Ativo && c.Analitico))
                Centros.Add(new CentroOpcao(c.Id, $"{c.Codigo} - {c.Descricao}", c.Codigo));
        CentroDocumento = CentroItem = Centros[0];
        await ListarAsync(app, empresa, usuario);
    });

    private async Task ListarAsync(VorticisApp app, long empresa, long usuario)
    {
        var f = new FiltroDocumentos(Data(FiltroInicio, new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1)), Data(FiltroFim, DateOnly.FromDateTime(DateTime.Today)), Operacao);
        var r = await app.Fiscal().Documentos.ConsultarAsync(empresa, f, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Lista.Clear();
        foreach (var d in r.Valor.Where(d => d.Digitado)) Lista.Add(new DocumentoLinha(d));
    }

    [RelayCommand]
    private Task ConsultarAsync() => ExecutarAsync(ListarAsync, Permissao.FiscalDocumentosConsultar);

    private async Task CarregarDocumentoAsync(long id)
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var r = await app.Fiscal().Digitacao.ObterAsync(empresa, id, sessao.Id);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var d = r.Valor;
        _documentoId = d.Id;
        _participanteId = d.ParticipanteId;
        _retencoes = d.Totais;
        (Modelo, Serie, Numero, Chave, Emissao, DataLivro, Natureza) =
            (Math.Max(0, ModelosValores.ToList().IndexOf(d.Modelo)), d.Serie, d.Numero, d.Chave ?? "", d.Emissao.ToDateTime(TimeOnly.MinValue), d.DataLivro.ToDateTime(TimeOnly.MinValue), d.Natureza ?? "");
        var lista = await app.Participantes.ListarAsync(empresa, sessao.Id);
        Participante = lista.FirstOrDefault(p => p.Id == d.ParticipanteId)?.Nome ?? "";
        CentroDocumento = Centros.FirstOrDefault(c => c.Id == d.CentroCustoId) ?? Centros[0];
        Itens.Clear();
        foreach (var i in d.Itens) Itens.Add(new ItemNotaLinha(i));
        TotalInformado = "";
        Contabilizado = d.Contabilizado;
        SituacaoDocumento = d.Contabilizado ? "Nota contabilizada: somente consulta (desfaça a contabilização para alterar)" : $"Alterando a nota {d.Numero}";
        AtualizarTotais();
        Erros.Clear();
        Mensagem = null;
    }

    [RelayCommand]
    private void Novo()
    {
        Selecionada = null;
        (_documentoId, _participanteId) = (0, 0);
        _retencoes = _retencoes with { IssRetido = false, RetPis = 0, RetCofins = 0, RetCsll = 0, Irrf = 0, RetPrev = 0 };
        (Modelo, Serie, Numero, Chave, Emissao, DataLivro, Natureza, Participante, TotalInformado) = (0, "1", "", "", DateTime.Today, DateTime.Today, "", "", "");
        CentroDocumento = CentroItem = Centros[0];
        Itens.Clear();
        Item.Limpar();
        Contabilizado = false;
        SituacaoDocumento = "Nova nota";
        AtualizarTotais();
        Erros.Clear();
        Mensagem = null;
    }

    private TotaisDocumento CalcularTotais() => CalculadoraNota.Totalizar(Itens.Select(i => i.Item), _retencoes);

    private void AtualizarTotais()
    {
        var t = CalcularTotais();
        Totais = $"Produtos {Numeros.Moeda(t.Produtos)} · Desconto {Numeros.Moeda(t.Desconto)} · Frete {Numeros.Moeda(t.Frete)} · IPI {Numeros.Moeda(t.Ipi)} · ICMS {Numeros.Moeda(t.Icms)} · " +
                 $"ICMS-ST {Numeros.Moeda(t.IcmsSt)} · PIS {Numeros.Moeda(t.Pis)} · COFINS {Numeros.Moeda(t.Cofins)} · ISS {Numeros.Moeda(t.Iss)} · TOTAL DA NOTA {Numeros.Moeda(t.Total)}";
    }

    // ------------------------------------------------------------------ itens

    [RelayCommand]
    private Task BuscarProdutoAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (string.IsNullOrWhiteSpace(Item.Produto)) { Erros.Add("Informe o código, a descrição ou o código de barras do produto."); return; }
        var p = await Resolvedores.ProdutoAsync(app, empresa, usuario, Item.Produto, true);
        if (p is null) { Erros.Add($"Produto \"{Item.Produto.Trim()}\" não encontrado ou ambíguo (cadastre em Cadastros › Produtos)."); return; }
        Item.AplicarProduto(p, Operacao);
    });

    [RelayCommand]
    private void CalcularTributos() => Item.CalcularTributos();

    private ItemFiscal? MontarItem()
    {
        var item = Item.Montar(CentroItem?.Id, CentroItem?.Codigo);
        return item;
    }

    [RelayCommand]
    private async Task AdicionarItemAsync() => await ConfirmarItemAsync(substituir: false);

    [RelayCommand]
    private async Task AtualizarItemAsync() => await ConfirmarItemAsync(substituir: true);

    private Task ConfirmarItemAsync(bool substituir) => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (substituir && ItemSelecionado is null) { Erros.Add("Selecione o item a atualizar."); return; }
        if (Item.ProdutoId is null && !string.IsNullOrWhiteSpace(Item.Produto))
        {
            var p = await Resolvedores.ProdutoAsync(app, empresa, usuario, Item.Produto, true);
            if (p is null) { Erros.Add($"Produto \"{Item.Produto.Trim()}\" não encontrado ou ambíguo."); return; }
            var antes = (Item.Descricao, Item.Cfop, Item.Unidade);
            Item.AplicarProduto(p, Operacao);
            if (!string.IsNullOrWhiteSpace(antes.Descricao)) Item.Descricao = antes.Descricao;
            if (!string.IsNullOrWhiteSpace(antes.Cfop)) Item.Cfop = antes.Cfop;
        }
        var novo = CalculadoraNota.Completar(MontarItem()!);
        if (novo.Quantidade <= 0 || string.IsNullOrWhiteSpace(novo.Descricao) || string.IsNullOrWhiteSpace(novo.Cfop))
        {
            Erros.Add("Informe produto/descrição, CFOP e quantidade do item.");
            return;
        }
        if (substituir)
        {
            var pos = Itens.IndexOf(ItemSelecionado!);
            Itens[pos] = new ItemNotaLinha(novo with { Numero = pos + 1 });
        }
        else Itens.Add(new ItemNotaLinha(novo with { Numero = Itens.Count + 1 }));
        Item.Limpar();
        ItemSelecionado = null;
        AtualizarTotais();
    });

    [RelayCommand]
    private void RemoverItem()
    {
        if (ItemSelecionado is not { } s) { Erros.Add("Selecione o item a remover."); return; }
        Itens.Remove(s);
        for (var i = 0; i < Itens.Count; i++) Itens[i] = new ItemNotaLinha(Itens[i].Item with { Numero = i + 1 });
        Item.Limpar();
        AtualizarTotais();
    }

    // ------------------------------------------------------------------ gravação

    [RelayCommand]
    private Task SalvarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (string.IsNullOrWhiteSpace(Participante)) { Erros.Add($"Informe o {RotuloParticipante.ToLowerInvariant()}."); return; }
        var part = await Resolvedores.ParticipanteAsync(app, empresa, usuario, Participante);
        if (part is null) { Erros.Add($"Participante \"{Participante.Trim()}\" não encontrado (informe o código, o nome ou o CPF/CNPJ cadastrado)."); return; }
        _participanteId = part.Value.Id;
        var totais = CalcularTotais();
        if (Numeros.LerValor(TotalInformado) is { } informado) totais = totais with { Total = informado };   // total conferido com a soma dos itens
        var doc = new DocumentoDigitado(_documentoId, Operacao, ModelosValores[Math.Clamp(Modelo, 0, 2)], Serie, Numero, Chave, Data(Emissao, default), Data(DataLivro, default),
            Natureza, _participanteId, CentroDocumento?.Id, totais, Itens.Select(i => i.Item).ToList());
        var r = await app.Fiscal().Digitacao.GravarAsync(empresa, doc, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await ListarAsync(app, empresa, usuario);
        _documentoId = r.Valor;
        Selecionada = Lista.FirstOrDefault(d => d.Id == r.Valor);
        Mensagem = "Nota gravada.";
    }, Permissao.FiscalDocumentosImportar);

    [RelayCommand(CanExecute = nameof(TemSelecionada))]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada is not { } s) return;
        if (!await Contexto.ConfirmarAsync("Excluir nota", $"Excluir a nota {s.Modelo} {s.Numero}?", "Excluir", "Cancelar")) return;
        var r = await app.Fiscal().Documentos.ExcluirAsync(empresa, [s.Id], usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Lista.Remove(s);
        Novo();
        Mensagem = "Nota excluída.";
    }, Permissao.FiscalDocumentosImportar);

    private bool TemSelecionada() => Selecionada is not null;
}

public sealed class NotaEntradaViewModel(IContexto contexto) : DigitacaoDocumentoViewModel(contexto)
{
    public override TipoOperacao Operacao => TipoOperacao.Entrada;
}

public sealed class NotaSaidaViewModel(IContexto contexto) : DigitacaoDocumentoViewModel(contexto)
{
    public override TipoOperacao Operacao => TipoOperacao.Saida;
}

/// <summary>Cadastros › Configurações fiscais: opções do Fiscal por empresa.</summary>
public partial class ConfiguracoesFiscaisViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    [ObservableProperty] private bool _usaCentroCusto;
    [ObservableProperty] private bool _empresaUsaCentroCusto;
    public bool PodeManter => Contexto.Pode(Permissao.FiscalDocumentosImportar);

    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeManter));

    public Task CarregarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Fiscal().Digitacao.ObterConfiguracaoAsync(empresa, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        (UsaCentroCusto, EmpresaUsaCentroCusto) = (r.Valor.UsaCentroCusto, r.Valor.EmpresaUsaCentroCusto);
    });

    [RelayCommand]
    private Task SalvarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Fiscal().Digitacao.GravarConfiguracaoAsync(empresa, UsaCentroCusto, usuario);
        MostrarResultado(r, "Configurações gravadas.");
        if (!r.Sucesso) UsaCentroCusto = false;
    }, Permissao.FiscalDocumentosImportar);
}
