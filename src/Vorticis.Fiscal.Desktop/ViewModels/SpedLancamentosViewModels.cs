using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Sped;

namespace Vorticis.Fiscal.Desktop.ViewModels;

public sealed record OpcaoSped(long Id, string Texto)
{
    public override string ToString() => Texto;
}

// ---------------------------------------------------------------------------------------------- ajustes da apuração (E111, E220, E530)

public sealed record AjusteLinha(AjusteIcmsEfd A)
{
    public string Imposto => ImpostosAjuste.Rotulo(A.Imposto);
    public string Codigo => A.Codigo;
    public string Tipo => A.Imposto == ImpostosAjuste.Ipi ? (A.SentidoEfetivo == 'E' ? "Crédito" : "Débito") : OutrosLancamentosEfd.RotuloTipo(A.Tipo);
    public string Descricao => A.Descricao;
    public string Documento => A.TemDocumento ? $"{A.DocModelo} {A.DocSerie}/{A.DocNumero} de {A.DocData:dd/MM/yyyy} ({A.DocParticipante!.Nome})" : A.Observacao ?? "";
    public string Valor => Numeros.Moeda(A.Valor);
}

public sealed record CodigoAjusteLinha(CodigoAjuste C)
{
    public string Imposto => ImpostosAjuste.Rotulo(C.Imposto);
    public string Codigo => C.Codigo;
    public string Descricao => C.Descricao;
    public override string ToString() => $"{C.Codigo} – {C.Descricao}";
}

/// <summary>
/// Ajustes da apuração do ICMS (E111/E112/E113), do ICMS-ST (E220) e do IPI (E530) de um sentido: entrada (outros créditos e estornos de débito) ou saída
/// (outros débitos, estornos de crédito, deduções e débitos especiais). Os ajustes entram no Livro de Apuração e na EFD ICMS/IPI da competência.
/// </summary>
public abstract partial class AjustesViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public static IReadOnlyList<string> Impostos { get; } = ["ICMS", "ICMS-ST", "IPI"];
    private static readonly string[] CodigosImposto = [ImpostosAjuste.Icms, ImpostosAjuste.IcmsSt, ImpostosAjuste.Ipi];

    public abstract char Sentido { get; }
    public abstract string Titulo { get; }
    public abstract string Orientacao { get; }

    public ObservableCollection<AjusteLinha> Lista { get; } = [];
    public ObservableCollection<CodigoAjusteLinha> Codigos { get; } = [];
    [ObservableProperty] private DateTime? _competencia = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private AjusteLinha? _selecionado;
    [ObservableProperty] private int _imposto;
    [ObservableProperty] private CodigoAjusteLinha? _codigoCadastrado;
    [ObservableProperty] private string _codigo = "";
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private string _valor = "";
    [ObservableProperty] private string _observacao = "";
    [ObservableProperty] private string _docParticipante = "";
    [ObservableProperty] private string _docModelo = "55";
    [ObservableProperty] private string _docSerie = "";
    [ObservableProperty] private string _docNumero = "";
    [ObservableProperty] private DateTime? _docData;
    [ObservableProperty] private string _tipoCodigo = "";
    [ObservableProperty] private string _totais = "";

    public bool PodeManter => Contexto.Pode(Permissao.FiscalApurar);
    public bool EhIcms => Imposto == 0;
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeManter));

    private string ImpostoCodigo => CodigosImposto[Math.Clamp(Imposto, 0, 2)];

    partial void OnCompetenciaChanged(DateTime? value) => _ = CarregarAsync();
    partial void OnImpostoChanged(int value)
    {
        OnPropertyChanged(nameof(EhIcms));
        OnCodigoChanged(Codigo);
        _ = CarregarCodigosAsync();
    }
    partial void OnCodigoCadastradoChanged(CodigoAjusteLinha? value)
    {
        if (value is null) return;
        Codigo = value.Codigo;
        if (string.IsNullOrWhiteSpace(Descricao)) Descricao = value.Descricao;
    }
    partial void OnCodigoChanged(string value)
    {
        var c = (value ?? "").Trim().ToUpperInvariant();
        TipoCodigo = Imposto == 2 ? (Sentido == 'E' ? "Crédito do IPI" : "Débito do IPI")
            : c.Length >= 4 && c[3] is >= '0' and <= '5' ? OutrosLancamentosEfd.RotuloTipo((TipoAjusteIcms)(c[3] - '0')) : "";
    }
    partial void OnSelecionadoChanged(AjusteLinha? value)
    {
        if (value?.A is not { } a) return;
        Imposto = Math.Max(0, Array.IndexOf(CodigosImposto, a.Imposto));
        (Codigo, Descricao, Valor, Observacao) = (a.Codigo, a.Descricao, Numeros.Moeda(a.Valor), a.Observacao ?? "");
        (DocParticipante, DocModelo, DocSerie, DocNumero, DocData) = (a.DocParticipante?.Nome ?? "", a.DocModelo ?? "55", a.DocSerie ?? "", a.DocNumero ?? "", a.DocData?.ToDateTime(TimeOnly.MinValue));
    }

    private DateOnly Mes => Competencia is { } d ? new DateOnly(d.Year, d.Month, 1) : new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);

    public Task CarregarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Fiscal().OutrosLancamentosEfd.AjustesAsync(empresa, Mes, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var meus = r.Valor.Where(a => a.SentidoEfetivo == Sentido).ToList();
        lock (Lista)
        {
            Lista.Clear();
            foreach (var a in meus) Lista.Add(new AjusteLinha(a));
        }
        Totais = string.Join(" · ", meus.GroupBy(a => (a.Imposto, a.Tipo)).OrderBy(g => g.Key.Imposto).ThenBy(g => g.Key.Tipo)
            .Select(g => $"{ImpostosAjuste.Rotulo(g.Key.Imposto)} – {OutrosLancamentosEfd.RotuloTipo(g.Key.Tipo)}: {Numeros.Moeda(g.Sum(a => a.Valor))}"));
        await CarregarCodigosAsync(app, empresa, usuario);
    });

    private Task CarregarCodigosAsync() => ExecutarAsync(async (app, empresa, usuario) => await CarregarCodigosAsync(app, empresa, usuario));

    private async Task CarregarCodigosAsync(Vorticis.Infra.VorticisApp app, long empresa, long usuario)
    {
        var c = await app.Fiscal().OutrosLancamentosEfd.CodigosAjusteAsync(empresa, ImpostoCodigo, usuario);
        lock (Codigos)   // cargas simultâneas (mudança de competência e de imposto) não duplicam a lista
        {
            Codigos.Clear();
            if (c.Sucesso) foreach (var x in c.Valor) Codigos.Add(new CodigoAjusteLinha(x));
        }
    }

    [RelayCommand]
    private void Novo()
    {
        Selecionado = null;
        (Codigo, Descricao, Valor, Observacao, DocParticipante, DocModelo, DocSerie, DocNumero, DocData) = ("", "", "", "", "", "55", "", "", null);
    }

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        long? partId = null;
        if (!string.IsNullOrWhiteSpace(DocParticipante))
        {
            var texto = DocParticipante.Trim();
            var lista = await app.Participantes.ListarAsync(empresa, usuario);
            var p = lista.FirstOrDefault(x => x.Codigo == texto) ?? lista.FirstOrDefault(x => string.Equals(x.Nome, texto, StringComparison.CurrentCultureIgnoreCase))
                    ?? lista.FirstOrDefault(x => x.CpfCnpj == new string(texto.Where(char.IsAsciiDigit).ToArray()));
            if (p is null) { Erros.Add($"Participante \"{texto}\" não encontrado (informe o código, o nome ou o CPF/CNPJ cadastrado)."); return; }
            partId = p.Id;
        }
        var r = await app.Fiscal().OutrosLancamentosEfd.GravarAjusteAsync(empresa,
            new AjusteIcmsEfd(Selecionado?.A.Id ?? 0, Mes, Codigo, Descricao, Numeros.LerValor(Valor) ?? 0, ImpostoCodigo, Sentido, Observacao, partId,
                              DocModelo, DocSerie, DocNumero, DocData is { } dd ? DateOnly.FromDateTime(dd) : null), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Novo();
        Mensagem = "Ajuste gravado. Ele entra no Livro de Apuração e na EFD ICMS/IPI desta competência.";
    }, Permissao.FiscalApurar);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionado?.A is not { } a) { Erros.Add("Selecione o ajuste na lista."); return; }
        var r = await app.Fiscal().OutrosLancamentosEfd.ExcluirAjusteAsync(empresa, a.Id, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Novo();
        Mensagem = "Ajuste excluído.";
    }, Permissao.FiscalApurar);
}

/// <summary>Lançamentos › Ajustes › Ajustes de entrada: outros créditos e estornos de débito (no IPI, créditos).</summary>
public sealed partial class AjustesEntradaViewModel(IContexto contexto) : AjustesViewModel(contexto)
{
    public override char Sentido => 'E';
    public override string Titulo => "Ajustes de entrada da apuração do ICMS e do IPI";
    public override string Orientacao => "Ajustes que aumentam o crédito: outros créditos (tipo 2 do código) e estornos de débito (tipo 3). No IPI, créditos. Código da tabela 5.1.1 da UF, ex.: SP020099.";
}

/// <summary>Lançamentos › Ajustes › Ajustes de saída: outros débitos, estornos de crédito, deduções e débitos especiais (no IPI, débitos).</summary>
public sealed partial class AjustesSaidaViewModel(IContexto contexto) : AjustesViewModel(contexto)
{
    public override char Sentido => 'S';
    public override string Titulo => "Ajustes de saída da apuração do ICMS e do IPI";
    public override string Orientacao => "Ajustes que aumentam o débito: outros débitos (tipo 0 do código), estornos de crédito (1), deduções (4) e débitos especiais (5). No IPI, débitos. Código da tabela 5.1.1 da UF, ex.: SP000099.";
}

/// <summary>Lançamentos › Ajustes › Códigos de ajuste: cadastro simples dos códigos que a empresa usa (tabela 5.1.1 da UF; IPI com 3 posições).</summary>
public partial class CodigosAjusteViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public static IReadOnlyList<string> Impostos { get; } = ["ICMS", "ICMS-ST", "IPI"];
    private static readonly string[] CodigosImposto = [ImpostosAjuste.Icms, ImpostosAjuste.IcmsSt, ImpostosAjuste.Ipi];

    public ObservableCollection<CodigoAjusteLinha> Lista { get; } = [];
    [ObservableProperty] private int _imposto;
    [ObservableProperty] private string _codigo = "";
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private CodigoAjusteLinha? _selecionado;

    public bool PodeManter => Contexto.Pode(Permissao.FiscalApurar);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeManter));

    partial void OnSelecionadoChanged(CodigoAjusteLinha? value)
    {
        if (value is null) return;
        (Imposto, Codigo, Descricao) = (Math.Max(0, Array.IndexOf(CodigosImposto, value.C.Imposto)), value.Codigo, value.Descricao);
    }

    public Task CarregarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Fiscal().OutrosLancamentosEfd.CodigosAjusteAsync(empresa, null, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Lista.Clear();
        foreach (var c in r.Valor) Lista.Add(new CodigoAjusteLinha(c));
    });

    [RelayCommand]
    private void Novo() { Selecionado = null; (Codigo, Descricao) = ("", ""); }

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Fiscal().OutrosLancamentosEfd.GravarCodigoAjusteAsync(empresa, new CodigoAjuste(CodigosImposto[Math.Clamp(Imposto, 0, 2)], Codigo, Descricao), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Novo();
        Mensagem = "Código gravado.";
    }, Permissao.FiscalApurar);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionado is not { } s) { Erros.Add("Selecione o código na lista."); return; }
        var r = await app.Fiscal().OutrosLancamentosEfd.ExcluirCodigoAjusteAsync(empresa, s.C.Imposto, s.Codigo, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Novo();
        Mensagem = "Código excluído.";
    }, Permissao.FiscalApurar);
}

// ---------------------------------------------------------------------------------------------- contas de consumo (C500/D500)

public sealed record ContaConsumoLinha(ContaConsumo C)
{
    public string Entrada => C.Entrada.ToString("dd/MM/yyyy");
    public string Modelo => C.Modelo;
    public string Fornecedor => C.ParticipanteNome;
    public string Numero => C.Numero;
    public string Valor => Numeros.Moeda(C.Valor);
    public string Icms => Numeros.Moeda(C.Icms);
    public string Cfop => C.Cfop;
}

/// <summary>Lançamentos › SPED Fiscal › Contas de consumo: energia elétrica, gás e água (C500) e comunicação/telecomunicação (D500) recebidas.</summary>
public partial class ContasConsumoViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<ContaConsumoLinha> Lista { get; } = [];
    public ObservableCollection<OpcaoSped> Fornecedores { get; } = [];
    public static IReadOnlyList<string> Modelos { get; } = OutrosLancamentosEfd.Modelos.Select(m => m.Nome).ToList();
    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _fim = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month));
    [ObservableProperty] private ContaConsumoLinha? _selecionada;
    [ObservableProperty] private int _modelo;
    [ObservableProperty] private OpcaoSped? _fornecedor;
    [ObservableProperty] private string _numero = "";
    [ObservableProperty] private string _serie = "";
    [ObservableProperty] private DateTime? _emissao = DateTime.Today;
    [ObservableProperty] private DateTime? _dataEntrada = DateTime.Today;
    [ObservableProperty] private string _classe = "01";
    [ObservableProperty] private string _valor = "";
    [ObservableProperty] private string _cfop = OutrosLancamentosEfd.CfopSugerido("06");
    [ObservableProperty] private string _cst = "000";
    [ObservableProperty] private string _baseIcms = "";
    [ObservableProperty] private string _aliquota = "";
    [ObservableProperty] private string _icms = "";
    [ObservableProperty] private string _pis = "";
    [ObservableProperty] private string _cofins = "";
    [ObservableProperty] private string _totais = "";

    public bool PodeManter => Contexto.Pode(Permissao.FiscalDocumentosImportar);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeManter));

    private string CodigoModelo => OutrosLancamentosEfd.Modelos[Math.Clamp(Modelo, 0, OutrosLancamentosEfd.Modelos.Count - 1)].Codigo;

    partial void OnModeloChanged(int value) { if (Selecionada is null) Cfop = OutrosLancamentosEfd.CfopSugerido(CodigoModelo); }
    partial void OnBaseIcmsChanged(string value) => CalcularIcms();
    partial void OnAliquotaChanged(string value) => CalcularIcms();
    partial void OnValorChanged(string value) { if (string.IsNullOrWhiteSpace(BaseIcms) && Numeros.LerValor(value) is { } v) BaseIcms = Numeros.Moeda(v); }
    private void CalcularIcms()
    {
        if (Numeros.LerValor(BaseIcms) is { } b && Numeros.LerValor(Aliquota) is { } a) Icms = Numeros.Moeda(Math.Round(b * a / 100m, 2));
    }

    partial void OnSelecionadaChanged(ContaConsumoLinha? value)
    {
        if (value?.C is not { } c) return;
        Modelo = Math.Max(0, OutrosLancamentosEfd.Modelos.ToList().FindIndex(m => m.Codigo == c.Modelo));
        Fornecedor = Fornecedores.FirstOrDefault(f => f.Id == c.ParticipanteId);
        (Numero, Serie, Emissao, DataEntrada, Classe) = (c.Numero, c.Serie, c.Emissao.ToDateTime(TimeOnly.MinValue), c.Entrada.ToDateTime(TimeOnly.MinValue), c.ClasseConsumo);
        (Valor, Cfop, Cst, BaseIcms, Aliquota, Icms, Pis, Cofins) = (Numeros.Moeda(c.Valor), c.Cfop, c.CstIcms, Numeros.Moeda(c.BaseIcms), Formato.Num(c.AliqIcms),
                                                                     Numeros.Moeda(c.Icms), Numeros.Moeda(c.Pis), Numeros.Moeda(c.Cofins));
    }

    public Task CarregarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var parts = await app.Participantes.ListarAsync(empresa, usuario);
        var sel = Fornecedor?.Id;
        Fornecedores.Clear();
        foreach (var p in parts.Where(p => p.Fornecedor && p.Ativo).OrderBy(p => p.Nome)) Fornecedores.Add(new OpcaoSped(p.Id, $"{p.Codigo} – {p.Nome}"));
        Fornecedor = Fornecedores.FirstOrDefault(f => f.Id == sel);
        var r = await app.Fiscal().OutrosLancamentosEfd.ContasAsync(empresa, Data(Inicio, DateOnly.MinValue), Data(Fim, DateOnly.MaxValue), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Lista.Clear();
        foreach (var c in r.Valor) Lista.Add(new ContaConsumoLinha(c));
        Totais = $"{r.Valor.Count} nota(s) · valor {Numeros.Moeda(r.Valor.Sum(c => c.Valor))} · crédito de ICMS {Numeros.Moeda(r.Valor.Sum(c => c.Icms))}";
    });

    [RelayCommand]
    private void Nova()
    {
        Selecionada = null;
        (Numero, Serie, Valor, BaseIcms, Aliquota, Icms, Pis, Cofins, Cst, Classe) = ("", "", "", "", "", "", "", "", "000", "01");
        Cfop = OutrosLancamentosEfd.CfopSugerido(CodigoModelo);
    }

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        static decimal V(string s) => Numeros.LerValor(s) ?? 0;
        var c = new ContaConsumo(Selecionada?.C.Id ?? 0, CodigoModelo, Fornecedor?.Id ?? 0, "", Numero, Serie, Data(Emissao, default), Data(DataEntrada, default), Classe,
                                 V(Valor), Cfop, Cst, V(BaseIcms), V(Aliquota), V(Icms), V(Pis), V(Cofins));
        var r = await app.Fiscal().OutrosLancamentosEfd.GravarContaAsync(empresa, c, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Nova();
        Mensagem = "Nota gravada. Ela entra na EFD ICMS/IPI do mês da entrada (C500 ou D500) e o ICMS vira crédito na apuração.";
    }, Permissao.FiscalDocumentosImportar);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada?.C is not { } c) { Erros.Add("Selecione a nota na lista."); return; }
        var r = await app.Fiscal().OutrosLancamentosEfd.ExcluirContaAsync(empresa, c.Id, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Nova();
        Mensagem = "Nota excluída.";
    }, Permissao.FiscalDocumentosImportar);

    [RelayCommand]
    private Task ConsultarAsync() => CarregarAsync();
}

// ---------------------------------------------------------------------------------------------- inventário (bloco H)

public partial class ItemInventarioLinha(long produtoId, int codigo, string descricao, string unidade) : ObservableObject
{
    public long ProdutoId { get; } = produtoId;
    public int Codigo { get; } = codigo;
    public string Descricao { get; } = descricao;
    [ObservableProperty] private string _unidade = unidade;
    [ObservableProperty] private string _quantidade = "";
    [ObservableProperty] private string _valorUnitario = "";
    public string Valor => Numeros.LerValor(Quantidade) is { } q && Numeros.LerValor(ValorUnitario) is { } u ? Numeros.Moeda(Math.Round(q * u, 2)) : "";
    partial void OnQuantidadeChanged(string value) => OnPropertyChanged(nameof(Valor));
    partial void OnValorUnitarioChanged(string value) => OnPropertyChanged(nameof(Valor));
}

public sealed record InventarioLinha(Inventario I)
{
    public string Data => I.Data.ToString("dd/MM/yyyy");
    public string Efd => I.CompetenciaEfd.ToString("MM/yyyy");
    public string Itens => I.Itens.Count.ToString();
    public string Total => Numeros.Moeda(I.Total);
}

/// <summary>Lançamentos › SPED Fiscal › Inventário de estoque (bloco H da EFD ICMS/IPI).</summary>
public partial class InventarioViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<InventarioLinha> Lista { get; } = [];
    public ObservableCollection<ItemInventarioLinha> Itens { get; } = [];
    public ObservableCollection<OpcaoSped> Produtos { get; } = [];
    private readonly Dictionary<long, Vorticis.Fiscal.Produtos.Produto> _produtos = [];
    [ObservableProperty] private InventarioLinha? _selecionado;
    [ObservableProperty] private DateTime? _data = new DateTime(DateTime.Today.Year - 1, 12, 31);
    [ObservableProperty] private DateTime? _competenciaEfd = new DateTime(DateTime.Today.Year, 2, 1);
    [ObservableProperty] private string _contaContabil = "";
    [ObservableProperty] private OpcaoSped? _produto;
    [ObservableProperty] private ItemInventarioLinha? _itemSelecionado;
    [ObservableProperty] private string _total = "";

    public bool PodeManter => Contexto.Pode(Permissao.FiscalApurar);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeManter));

    partial void OnDataChanged(DateTime? value)
    {
        if (value is { } d && Selecionado is null)
            CompetenciaEfd = OutrosLancamentosEfd.CompetenciaSugerida(DateOnly.FromDateTime(d)).ToDateTime(TimeOnly.MinValue);
    }

    partial void OnSelecionadoChanged(InventarioLinha? value)
    {
        if (value?.I is not { } i) return;
        (Data, CompetenciaEfd, ContaContabil) = (i.Data.ToDateTime(TimeOnly.MinValue), i.CompetenciaEfd.ToDateTime(TimeOnly.MinValue), i.ContaContabil);
        Itens.Clear();
        foreach (var x in i.Itens)
            Itens.Add(new ItemInventarioLinha(x.ProdutoId, x.ProdutoCodigo, x.Descricao, x.Unidade)
                { Quantidade = x.Quantidade.ToString("0.###", Formato.PtBr), ValorUnitario = x.ValorUnitario.ToString("0.00####", Formato.PtBr) });
        AtualizarTotal();
    }

    private void AtualizarTotal() =>
        Total = $"{Itens.Count} item(ns) · total {Numeros.Moeda(Itens.Sum(i => Numeros.LerValor(i.Valor) ?? 0))}";

    public Task CarregarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var prods = await app.Fiscal().Produtos.ListarAsync(empresa, true, null, usuario);
        if (prods.Sucesso)
        {
            Produtos.Clear();
            _produtos.Clear();
            foreach (var p in prods.Valor.OrderBy(p => p.Codigo)) { Produtos.Add(new OpcaoSped(p.Id, $"{p.Codigo} – {p.Descricao}")); _produtos[p.Id] = p; }
        }
        var r = await app.Fiscal().OutrosLancamentosEfd.InventariosAsync(empresa, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Lista.Clear();
        foreach (var i in r.Valor) Lista.Add(new InventarioLinha(i));
    });

    [RelayCommand]
    private void Novo()
    {
        Selecionado = null;
        Itens.Clear();
        Data = new DateTime(DateTime.Today.Year - 1, 12, 31);
        AtualizarTotal();
    }

    [RelayCommand]
    private void AdicionarItem()
    {
        Erros.Clear();
        if (Produto is null || !_produtos.TryGetValue(Produto.Id, out var p)) { Erros.Add("Escolha o produto."); return; }
        if (Itens.Any(i => i.ProdutoId == p.Id)) { Erros.Add("O produto já está no inventário."); return; }
        var item = new ItemInventarioLinha(p.Id, p.Codigo, p.Descricao, p.Unidade);
        item.PropertyChanged += (_, _) => AtualizarTotal();
        Itens.Add(item);
        ItemSelecionado = item;
        AtualizarTotal();
    }

    [RelayCommand]
    private void RemoverItem()
    {
        if (ItemSelecionado is { } i) Itens.Remove(i);
        AtualizarTotal();
    }

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var itens = Itens.Select(i => new ItemInventario(i.ProdutoId, i.Codigo, i.Descricao, i.Unidade, Numeros.LerValor(i.Quantidade) ?? 0,
                                                         Numeros.LerValor(i.ValorUnitario) ?? 0, 0)).ToList();
        var inv = new Inventario(Selecionado?.I.Id ?? 0, Data(Data, default), "01", Data(CompetenciaEfd, default), ContaContabil, itens);
        var r = await app.Fiscal().OutrosLancamentosEfd.GravarInventarioAsync(empresa, inv, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Selecionado = Lista.FirstOrDefault(l => l.I.Id == r.Valor);
        Mensagem = $"Inventário gravado. Ele entra no bloco H da EFD de {inv.CompetenciaEfd:MM/yyyy}.";
    }, Permissao.FiscalApurar);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionado?.I is not { } i) { Erros.Add("Selecione o inventário na lista."); return; }
        var r = await app.Fiscal().OutrosLancamentosEfd.ExcluirInventarioAsync(empresa, i.Id, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Novo();
        Mensagem = "Inventário excluído.";
    }, Permissao.FiscalApurar);
}
