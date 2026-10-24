using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Desktop.Comum;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra;
using Vorticis.Infra.Relatorios;

namespace Vorticis.Condominio.Desktop.ViewModels;

public static class CondominioTelas
{
    public const string Unidades = "condominio.unidades";
    public const string Reservas = "condominio.reservas";
    public const string Taxas = "condominio.taxas";
    public const string Cobrancas = "condominio.cobrancas";
    public const string Despesas = "condominio.despesas";
    public const string Ocorrencias = "condominio.ocorrencias";
    public const string Prestacao = "condominio.prestacao";
    public const string Rateio = "condominio.rateio";
    public const string Atas = "condominio.atas";
}

internal static class Ext
{
    public static ServicoCondominio Cond(this VorticisApp app) => ServicosCondominio.De(app);
    public static DateOnly Dia(DateTime? d) => d is { } v ? DateOnly.FromDateTime(v) : DateOnly.FromDateTime(DateTime.Today);
    public static DateTime Dt(DateOnly d) => d.ToDateTime(TimeOnly.MinValue);
    public static DateOnly? DiaOpc(DateTime? d) => d is { } v ? DateOnly.FromDateTime(v) : null;
}

public sealed record Opcao(long? Id, string Texto)
{
    public override string ToString() => Texto;
}

/// <summary>Base das telas do condomínio: carrega a lista de condôminos (participantes) e de unidades para os combos.</summary>
public abstract partial class CondominioBase(IContexto contexto, Permissao edicao) : ModuloViewModel(contexto)
{
    public ObservableCollection<Opcao> Pessoas { get; } = [];
    public ObservableCollection<Opcao> UnidadesOpcoes { get; } = [];
    public bool PodeEditar => Contexto.Pode(edicao);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeEditar));

    protected async Task CarregarCombosAsync(VorticisApp app, long empresa, long usuario)
    {
        var p = await app.Cond().CondominosAsync(empresa, usuario);
        var u = await app.Cond().UnidadesAsync(empresa, usuario);
        Pessoas.Clear();
        Pessoas.Add(new Opcao(null, "(nenhum)"));
        foreach (var (id, nome) in p) Pessoas.Add(new Opcao(id, nome));
        UnidadesOpcoes.Clear();
        UnidadesOpcoes.Add(new Opcao(null, "(nenhuma)"));
        foreach (var x in u.Where(x => x.Ativa)) UnidadesOpcoes.Add(new Opcao(x.Id, x.Identificacao));
    }
}

// ---------------------------------------------------------------------------------------------- unidades

public sealed record UnidadeLinha(Unidade U)
{
    public string Unidade => U.Identificacao;
    public string Tipo => U.Tipo;
    public string Fracao => U.FracaoIdeal.ToString("0.########", Formato.PtBr);
    public string Proprietario => U.ProprietarioNome ?? "";
    public string Inquilino => U.InquilinoNome ?? "";
    public string Cobranca => U.CobrarInquilino ? "Inquilino" : "Proprietário";
    public string Situacao => U.Ativa ? "Ativa" : "Inativa";
}

/// <summary>Cadastros › Unidades e condôminos.</summary>
public partial class UnidadesViewModel(IContexto contexto) : CondominioBase(contexto, Permissao.CondominioCadastros)
{
    public ObservableCollection<UnidadeLinha> Lista { get; } = [];
    public static IReadOnlyList<string> Tipos { get; } = ["APARTAMENTO", "CASA", "SALA", "LOJA", "VAGA", "OUTRO"];
    [ObservableProperty] private UnidadeLinha? _selecionada;
    [ObservableProperty] private string _bloco = "";
    [ObservableProperty] private string _numero = "";
    [ObservableProperty] private string _tipo = "APARTAMENTO";
    [ObservableProperty] private string _fracao = "";
    [ObservableProperty] private string _area = "";
    [ObservableProperty] private Opcao? _proprietario;
    [ObservableProperty] private Opcao? _inquilino;
    [ObservableProperty] private bool _cobrarInquilino;
    [ObservableProperty] private bool _ativa = true;
    [ObservableProperty] private string _totais = "";

    partial void OnSelecionadaChanged(UnidadeLinha? value)
    {
        if (value?.U is not { } u) return;
        (Bloco, Numero, Tipo, Fracao, Area, CobrarInquilino, Ativa) = (u.Bloco, u.Numero, u.Tipo, u.FracaoIdeal.ToString("0.########", Formato.PtBr),
                                                                       u.AreaM2 is { } a ? Formato.Num(a) : "", u.CobrarInquilino, u.Ativa);
        (Proprietario, Inquilino) = (Pessoas.FirstOrDefault(p => p.Id == u.ProprietarioId) ?? Pessoas.FirstOrDefault(), Pessoas.FirstOrDefault(p => p.Id == u.InquilinoId) ?? Pessoas.FirstOrDefault());
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        var g = NovaGeracao();
        await CarregarCombosAsync(app, empresa, sessao.Id);
        var itens = await app.Cond().UnidadesAsync(empresa, sessao.Id);
        if (!EhAtual(g)) return;
        Lista.Clear();
        foreach (var u in itens) Lista.Add(new UnidadeLinha(u));
        Totais = $"{itens.Count(u => u.Ativa)} unidade(s) ativa(s) · soma das frações {itens.Where(u => u.Ativa).Sum(u => u.FracaoIdeal).ToString("0.########", Formato.PtBr)}";
    }

    [RelayCommand]
    private void Nova()
    {
        Selecionada = null;
        (Numero, Fracao, Area, CobrarInquilino, Ativa) = ("", "", "", false, true);
        (Proprietario, Inquilino) = (Pessoas.FirstOrDefault(), Pessoas.FirstOrDefault());
    }

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var u = new Unidade(Selecionada?.U.Id ?? 0, Bloco, Numero, Tipo, Formato.LerValor(Fracao) ?? 0, Formato.LerValor(Area), Proprietario?.Id, Inquilino?.Id, CobrarInquilino, Ativa);
        var r = await app.Cond().GravarUnidadeAsync(empresa, u, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = "Unidade gravada.";
    }, Permissao.CondominioCadastros);
}

// ---------------------------------------------------------------------------------------------- taxas

public sealed record TaxaLinha(Taxa T)
{
    public string Descricao => T.Descricao;
    public string Rateio => T.Rateio.Rotulo();
    public string Valor => Formato.Moeda(T.Valor);
    public string Vigencia => $"{T.Inicio:MM/yyyy}" + (T.Fim is { } f ? $" a {f:MM/yyyy}" : " em diante");
    public string Situacao => T.Ativa ? "Ativa" : "Inativa";
}

/// <summary>Financeiro › Taxas condominiais.</summary>
public partial class TaxasViewModel(IContexto contexto) : CondominioBase(contexto, Permissao.CondominioFinanceiro)
{
    public ObservableCollection<TaxaLinha> Lista { get; } = [];
    public sealed record OpcaoRateio(Vorticis.Condominio.Rateio R) { public override string ToString() => R.Rotulo(); }
    public static IReadOnlyList<OpcaoRateio> Rateios { get; } = [new(Vorticis.Condominio.Rateio.Fracao), new(Vorticis.Condominio.Rateio.Igual), new(Vorticis.Condominio.Rateio.Fixo)];
    [ObservableProperty] private TaxaLinha? _selecionada;
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private OpcaoRateio? _rateio = Rateios[0];
    [ObservableProperty] private string _valor = "";
    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _fim;
    [ObservableProperty] private bool _ativa = true;

    partial void OnSelecionadaChanged(TaxaLinha? value)
    {
        if (value?.T is not { } t) return;
        (Descricao, Rateio, Valor, Inicio, Fim, Ativa) = (t.Descricao, Rateios.First(r => r.R == t.Rateio), Formato.Num(t.Valor), Ext.Dt(t.Inicio), t.Fim is { } f ? Ext.Dt(f) : null, t.Ativa);
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        var itens = await app.Cond().TaxasAsync(empresa, sessao.Id);
        Lista.Clear();
        foreach (var t in itens) Lista.Add(new TaxaLinha(t));
    }

    [RelayCommand]
    private void Nova() { Selecionada = null; (Descricao, Rateio, Valor, Fim, Ativa) = ("", Rateios[0], "", null, true); }

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Cond().GravarTaxaAsync(empresa, new Taxa(Selecionada?.T.Id ?? 0, Descricao, Rateio?.R ?? Condominio.Rateio.Fracao, Formato.LerValor(Valor) ?? 0,
                                                                   Ext.Dia(Inicio), Ext.DiaOpc(Fim), Ativa), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = "Taxa gravada.";
    }, Permissao.CondominioFinanceiro);
}

// ---------------------------------------------------------------------------------------------- cobranças

public sealed record CobrancaLinha(Cobranca C)
{
    public string Unidade => C.Unidade;
    public string Responsavel => C.Responsavel ?? "";
    public string Competencia => C.Competencia.ToString("MM/yyyy");
    public string Vencimento => C.Vencimento.ToString("dd/MM/yyyy");
    public string Valor => Formato.Moeda(C.Valor);
    public string Situacao => C.Situacao == "ABERTA" && C.Vencimento < DateOnly.FromDateTime(DateTime.Today) ? "VENCIDA" : C.Situacao switch { "PAGA" => "Paga", "CANCELADA" => "Cancelada", _ => "Em aberto" };
    public string Pago => C.PagoEm is { } d ? $"{d:dd/MM/yyyy} – {Formato.Moeda(C.ValorPago ?? 0)}" : "";
    public string Composicao => string.Join(" + ", C.Itens.Select(i => $"{i.Descricao} {Formato.Moeda(i.Valor)}"));
}

/// <summary>Financeiro › Cobranças e recebimentos: geração mensal pelas taxas e recebimento com multa e juros.</summary>
public partial class CobrancasViewModel(IContexto contexto) : CondominioBase(contexto, Permissao.CondominioFinanceiro)
{
    public ObservableCollection<CobrancaLinha> Lista { get; } = [];
    [ObservableProperty] private DateTime? _competencia = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private bool _somenteAbertas;
    [ObservableProperty] private CobrancaLinha? _selecionada;
    [ObservableProperty] private DateTime? _dataPagamento = DateTime.Today;
    [ObservableProperty] private string _multa = "";
    [ObservableProperty] private string _juros = "";
    [ObservableProperty] private string _desconto = "";
    [ObservableProperty] private string _totais = "";

    partial void OnCompetenciaChanged(DateTime? value) => _ = CarregarAsync();
    partial void OnSomenteAbertasChanged(bool value) => _ = CarregarAsync();
    partial void OnSelecionadaChanged(CobrancaLinha? value) => _ = CalcularEncargosAsync();
    partial void OnDataPagamentoChanged(DateTime? value) => _ = CalcularEncargosAsync();

    private async Task CalcularEncargosAsync()
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Selecionada?.C is not { Situacao: "ABERTA" } c) return;
        var (m, j) = await app.Cond().EncargosAsync(empresa, c, Ext.Dia(DataPagamento));
        (Multa, Juros, Desconto) = (Formato.Num(m), Formato.Num(j), "");
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        var g = NovaGeracao();
        var itens = await app.Cond().CobrancasAsync(empresa, SomenteAbertas ? null : RegrasCondominio.Mes(Ext.Dia(Competencia)), SomenteAbertas, sessao.Id);
        if (!EhAtual(g)) return;
        Lista.Clear();
        foreach (var c in itens) Lista.Add(new CobrancaLinha(c));
        Totais = $"{itens.Count} cobrança(s) · emitido {Formato.Moeda(itens.Where(c => c.Situacao != "CANCELADA").Sum(c => c.Valor))} · "
                 + $"recebido {Formato.Moeda(itens.Sum(c => c.ValorPago ?? 0))} · em aberto {Formato.Moeda(itens.Where(c => c.Situacao == "ABERTA").Sum(c => c.Valor))}";
    }

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Cond().GerarCobrancasAsync(empresa, Ext.Dia(Competencia), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        SomenteAbertas = false;
        await CarregarAsync();
        Mensagem = r.Valor == 0 ? "Nenhuma cobrança nova: todas as unidades já têm a cobrança deste mês." : $"{r.Valor} cobrança(s) gerada(s).";
    }, Permissao.CondominioFinanceiro);

    [RelayCommand]
    private Task ReceberAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada?.C is not { } c) { Erros.Add("Selecione a cobrança na lista."); return; }
        var r = await app.Cond().ReceberAsync(empresa, c, Ext.Dia(DataPagamento), Formato.LerValor(Multa) ?? 0, Formato.LerValor(Juros) ?? 0, Formato.LerValor(Desconto) ?? 0, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = "Recebimento registrado.";
    }, Permissao.CondominioFinanceiro);

    [RelayCommand]
    private Task EstornarAsync() => Alterar(false, "Recebimento estornado: a cobrança voltou a ficar em aberto.");

    [RelayCommand]
    private Task CancelarAsync() => Alterar(true, "Cobrança cancelada.");

    private Task Alterar(bool cancelar, string msg) => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada?.C is not { } c) { Erros.Add("Selecione a cobrança na lista."); return; }
        if (!await Contexto.ConfirmarAsync(cancelar ? "Cancelar cobrança" : "Estornar recebimento", $"Confirma para a unidade {c.Unidade} ({c.Competencia:MM/yyyy})?")) return;
        var r = await app.Cond().EstornarOuCancelarAsync(empresa, c, cancelar, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = msg;
    }, Permissao.CondominioFinanceiro);
}

// ---------------------------------------------------------------------------------------------- despesas

public sealed record DespesaLinha(Despesa D)
{
    public string Vencimento => D.Vencimento.ToString("dd/MM/yyyy");
    public string Categoria => DespesasViewModel.RotuloCategoria(D.Categoria);
    public string Descricao => D.Descricao;
    public string Fornecedor => D.FornecedorNome ?? "";
    public string Valor => Formato.Moeda(D.Valor);
    public string Pago => D.PagoEm is { } p ? p.ToString("dd/MM/yyyy") : "em aberto";
}

/// <summary>Financeiro › Despesas do condomínio.</summary>
public partial class DespesasViewModel(IContexto contexto) : CondominioBase(contexto, Permissao.CondominioFinanceiro)
{
    public ObservableCollection<DespesaLinha> Lista { get; } = [];
    public static IReadOnlyList<string> Categorias { get; } =
        ["PESSOAL", "AGUA", "ENERGIA", "GAS", "MANUTENCAO", "LIMPEZA", "SEGURANCA", "ADMINISTRACAO", "SEGURO", "TRIBUTOS", "FUNDO_RESERVA", "OUTRAS"];
    public static string RotuloCategoria(string c) => c switch
    {
        "PESSOAL" => "Pessoal", "AGUA" => "Água", "ENERGIA" => "Energia", "GAS" => "Gás", "MANUTENCAO" => "Manutenção", "LIMPEZA" => "Limpeza",
        "SEGURANCA" => "Segurança", "ADMINISTRACAO" => "Administração", "SEGURO" => "Seguro", "TRIBUTOS" => "Tributos", "FUNDO_RESERVA" => "Fundo de reserva", _ => "Outras",
    };

    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _fim = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1).AddDays(-1);
    [ObservableProperty] private DespesaLinha? _selecionada;
    [ObservableProperty] private DateTime? _data = DateTime.Today;
    [ObservableProperty] private DateTime? _vencimento = DateTime.Today;
    [ObservableProperty] private Opcao? _fornecedor;
    [ObservableProperty] private string _categoria = "MANUTENCAO";
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private string _valor = "";
    [ObservableProperty] private DateTime? _pagoEm;
    [ObservableProperty] private string _totais = "";

    partial void OnInicioChanged(DateTime? value) => _ = CarregarAsync();
    partial void OnFimChanged(DateTime? value) => _ = CarregarAsync();
    partial void OnSelecionadaChanged(DespesaLinha? value)
    {
        if (value?.D is not { } d) return;
        (Data, Vencimento, Categoria, Descricao, Valor, PagoEm) = (Ext.Dt(d.Data), Ext.Dt(d.Vencimento), d.Categoria, d.Descricao, Formato.Num(d.Valor), d.PagoEm is { } p ? Ext.Dt(p) : null);
        Fornecedor = Pessoas.FirstOrDefault(p => p.Id == d.FornecedorId) ?? Pessoas.FirstOrDefault();
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        var g = NovaGeracao();
        await CarregarCombosAsync(app, empresa, sessao.Id);
        var itens = await app.Cond().DespesasAsync(empresa, Ext.Dia(Inicio), Ext.Dia(Fim), sessao.Id);
        if (!EhAtual(g)) return;
        Lista.Clear();
        foreach (var d in itens) Lista.Add(new DespesaLinha(d));
        Totais = $"{itens.Count} despesa(s) · total {Formato.Moeda(itens.Sum(d => d.Valor))} · pagas {Formato.Moeda(itens.Where(d => d.PagoEm is not null).Sum(d => d.Valor))}";
    }

    [RelayCommand]
    private void Nova() { Selecionada = null; (Data, Vencimento, Descricao, Valor, PagoEm) = (DateTime.Today, DateTime.Today, "", "", null); Fornecedor = Pessoas.FirstOrDefault(); }

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Cond().GravarDespesaAsync(empresa, new Despesa(Selecionada?.D.Id ?? 0, Ext.Dia(Data), Ext.Dia(Vencimento), Fornecedor?.Id, Categoria, Descricao,
                                                                         Formato.LerValor(Valor) ?? 0, Ext.DiaOpc(PagoEm)), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = "Despesa gravada.";
    }, Permissao.CondominioFinanceiro);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada?.D is not { } d) { Erros.Add("Selecione a despesa."); return; }
        if (!await Contexto.ConfirmarAsync("Excluir despesa", $"Excluir \"{d.Descricao}\"?")) return;
        var r = await app.Cond().ExcluirDespesaAsync(empresa, d.Id, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Selecionada = null;
        await CarregarAsync();
        Mensagem = "Despesa excluída.";
    }, Permissao.CondominioFinanceiro);
}

// ---------------------------------------------------------------------------------------------- áreas comuns e reservas

public sealed record ReservaLinha(Reserva R)
{
    public string Data => R.Data.ToString("dd/MM/yyyy (ddd)", Formato.PtBr);
    public string Area => R.Area;
    public string Unidade => R.Unidade;
    public string Situacao => R.Situacao == "RESERVADA" ? "Reservada" : "Cancelada";
    public string Observacao => R.Observacao ?? "";
}

/// <summary>Cadastros › Áreas comuns e reservas.</summary>
public partial class ReservasViewModel(IContexto contexto) : CondominioBase(contexto, Permissao.CondominioCadastros)
{
    public ObservableCollection<AreaComum> Areas { get; } = [];
    public ObservableCollection<ReservaLinha> Lista { get; } = [];
    [ObservableProperty] private AreaComum? _areaSelecionada;
    [ObservableProperty] private string _nomeArea = "";
    [ObservableProperty] private decimal? _capacidade = 0;
    [ObservableProperty] private string _taxaReserva = "";
    [ObservableProperty] private Opcao? _unidade;
    [ObservableProperty] private DateTime? _dataReserva = DateTime.Today;
    [ObservableProperty] private string _observacao = "";
    [ObservableProperty] private ReservaLinha? _reservaSelecionada;

    partial void OnAreaSelecionadaChanged(AreaComum? value)
    {
        if (value is null) return;
        (NomeArea, Capacidade, TaxaReserva) = (value.Nome, value.Capacidade, Formato.Num(value.TaxaReserva));
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        await CarregarCombosAsync(app, empresa, sessao.Id);
        var areas = await app.Cond().AreasAsync(empresa, sessao.Id);
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var reservas = await app.Cond().ReservasAsync(empresa, hoje.AddDays(-30), hoje.AddYears(1), sessao.Id);
        var sel = AreaSelecionada?.Id;
        Areas.Clear();
        foreach (var a in areas) Areas.Add(a);
        AreaSelecionada = Areas.FirstOrDefault(a => a.Id == sel) ?? Areas.FirstOrDefault();
        Lista.Clear();
        foreach (var r in reservas) Lista.Add(new ReservaLinha(r));
    }

    [RelayCommand]
    private void NovaArea() { AreaSelecionada = null; (NomeArea, Capacidade, TaxaReserva) = ("", 0, ""); }

    [RelayCommand]
    private Task GravarAreaAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Cond().GravarAreaAsync(empresa, new AreaComum(AreaSelecionada?.Id ?? 0, NomeArea, (int)(Capacidade ?? 0), Formato.LerValor(TaxaReserva) ?? 0), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = "Área comum gravada.";
    }, Permissao.CondominioCadastros);

    [RelayCommand]
    private Task ReservarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (AreaSelecionada is null || Unidade?.Id is not { } u) { Erros.Add("Escolha a área e a unidade."); return; }
        var r = await app.Cond().ReservarAsync(empresa, AreaSelecionada.Id, u, Ext.Dia(DataReserva), string.IsNullOrWhiteSpace(Observacao) ? null : Observacao, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Observacao = "";
        await CarregarAsync();
        Mensagem = "Reserva registrada.";
    }, Permissao.CondominioCadastros);

    [RelayCommand]
    private Task CancelarReservaAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (ReservaSelecionada?.R is not { } r0) { Erros.Add("Selecione a reserva."); return; }
        var r = await app.Cond().CancelarReservaAsync(empresa, r0.Id, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = "Reserva cancelada.";
    }, Permissao.CondominioCadastros);
}

// ---------------------------------------------------------------------------------------------- ocorrências

public sealed record OcorrenciaLinha(Ocorrencia O)
{
    public string Data => O.Data.ToString("dd/MM/yyyy");
    public string Tipo => OcorrenciasViewModel.RotuloTipo(O.Tipo);
    public string Unidade => O.Unidade ?? "";
    public string Titulo => O.Titulo;
    public string Situacao => O.Situacao == "RESOLVIDA" ? $"Resolvida {O.ResolvidaEm:dd/MM/yyyy}" : "Aberta";
}

/// <summary>Administração › Ocorrências e assembleias.</summary>
public partial class OcorrenciasViewModel(IContexto contexto) : CondominioBase(contexto, Permissao.CondominioCadastros)
{
    public ObservableCollection<OcorrenciaLinha> Lista { get; } = [];
    public static IReadOnlyList<string> Tipos { get; } = ["RECLAMACAO", "MANUTENCAO", "ADVERTENCIA", "MULTA", "AVISO", "ASSEMBLEIA"];
    public static string RotuloTipo(string t) => t switch
    {
        "RECLAMACAO" => "Reclamação", "MANUTENCAO" => "Manutenção", "ADVERTENCIA" => "Advertência", "MULTA" => "Multa", "AVISO" => "Aviso", _ => "Assembleia",
    };
    [ObservableProperty] private bool _somenteAbertas = true;
    [ObservableProperty] private OcorrenciaLinha? _selecionada;
    [ObservableProperty] private DateTime? _data = DateTime.Today;
    [ObservableProperty] private string _tipo = "RECLAMACAO";
    [ObservableProperty] private Opcao? _unidade;
    [ObservableProperty] private string _titulo = "";
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private bool _resolvida;

    partial void OnSomenteAbertasChanged(bool value) => _ = CarregarAsync();
    partial void OnSelecionadaChanged(OcorrenciaLinha? value)
    {
        if (value?.O is not { } o) return;
        (Data, Tipo, Titulo, Descricao, Resolvida) = (Ext.Dt(o.Data), o.Tipo, o.Titulo, o.Descricao ?? "", o.Situacao == "RESOLVIDA");
        Unidade = UnidadesOpcoes.FirstOrDefault(u => u.Id == o.UnidadeId) ?? UnidadesOpcoes.FirstOrDefault();
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        await CarregarCombosAsync(app, empresa, sessao.Id);
        var itens = await app.Cond().OcorrenciasAsync(empresa, SomenteAbertas, sessao.Id);
        Lista.Clear();
        foreach (var o in itens) Lista.Add(new OcorrenciaLinha(o));
    }

    [RelayCommand]
    private void Nova() { Selecionada = null; (Data, Titulo, Descricao, Resolvida) = (DateTime.Today, "", "", false); Unidade = UnidadesOpcoes.FirstOrDefault(); }

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Cond().GravarOcorrenciaAsync(empresa, new Ocorrencia(Selecionada?.O.Id ?? 0, Unidade?.Id, null, Ext.Dia(Data), Tipo, Titulo,
                                                                               string.IsNullOrWhiteSpace(Descricao) ? null : Descricao, Resolvida ? "RESOLVIDA" : "ABERTA",
                                                                               Selecionada?.O.ResolvidaEm), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = "Ocorrência gravada.";
    }, Permissao.CondominioCadastros);
}

// ---------------------------------------------------------------------------------------------- prestação de contas

public sealed record LinhaValor(string Descricao, string Valor);

/// <summary>Relatórios › Prestação de contas e inadimplência.</summary>
public partial class PrestacaoViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<LinhaValor> Resumo { get; } = [];
    public ObservableCollection<LinhaValor> Inadimplentes { get; } = [];
    [ObservableProperty] private DateTime? _mes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private string _situacao = "";

    partial void OnMesChanged(DateTime? value) => _ = CarregarAsync();

    public Task CarregarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Cond().PrestacaoContasAsync(empresa, Ext.Dia(Mes), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var p = r.Valor;
        Resumo.Clear();
        Resumo.Add(new LinhaValor($"Cobranças emitidas para {p.Mes:MM/yyyy}", Formato.Moeda(p.Emitido)));
        Resumo.Add(new LinhaValor("Arrecadado no mês (cotas)", Formato.Moeda(p.Arrecadado)));
        Resumo.Add(new LinhaValor("Multas e juros recebidos", Formato.Moeda(p.EncargosRecebidos)));
        foreach (var (cat, valor) in p.Despesas) Resumo.Add(new LinhaValor($"Despesa: {DespesasViewModel.RotuloCategoria(cat)}", "−" + Formato.Moeda(valor)));
        Resumo.Add(new LinhaValor("Total das despesas pagas", "−" + Formato.Moeda(p.TotalDespesas)));
        Resumo.Add(new LinhaValor("SALDO DO MÊS", Formato.Moeda(p.Saldo)));
        Resumo.Add(new LinhaValor("Inadimplência acumulada (vencidas até o fim do mês)", Formato.Moeda(p.Inadimplencia)));
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var i = await app.Cond().InadimplenciaAsync(empresa, hoje, usuario);
        Inadimplentes.Clear();
        if (i.Sucesso)
            foreach (var (c, m, j) in i.Valor)
                Inadimplentes.Add(new LinhaValor($"{c.Unidade} – {c.Responsavel} – {c.Competencia:MM/yyyy} (venc. {c.Vencimento:dd/MM/yyyy})",
                                                 $"{Formato.Moeda(c.Valor)} + multa {Formato.Moeda(m)} + juros {Formato.Moeda(j)} = {Formato.Moeda(c.Valor + m + j)}"));
        Situacao = i.Sucesso ? $"{i.Valor.Count} cobrança(s) vencida(s) em aberto hoje; total atualizado {Formato.Moeda(i.Valor.Sum(x => x.C.Valor + x.Multa + x.Juros))}." : "";
    }, Permissao.CondominioRelatorios);
}

// ---------------------------------------------------------------------------------------------- parâmetros

/// <summary>Cadastros › Parâmetros (Condomínio): vencimento das cotas, multa e juros de atraso.</summary>
public sealed class ParametrosCondominioViewModel(IContexto contexto) : ParametrosViewModel(contexto)
{
    public override string Titulo => "Parâmetros do Condomínio";
    public override string Descricao => "Vencimento das cotas e encargos de atraso do condomínio ativo.";
    protected override Permissao PermissaoGravar => Permissao.CondominioFinanceiro;

    protected override IReadOnlyList<GrupoParametros> MontarGrupos() =>
    [
        new("Cobranças", "Valem para as próximas cobranças geradas e para o cálculo dos encargos no recebimento.",
        [
            new CampoParametro("dia", "Dia de vencimento das cotas (1 a 28)", TipoCampoParametro.Texto, "Dia do mês em que vencem as cobranças."),
            new CampoParametro("multa", "Multa por atraso (%)", TipoCampoParametro.Texto, "Máximo de 2% (Código Civil, art. 1.336, § 1º)."),
            new CampoParametro("juros", "Juros de mora ao mês (%)", TipoCampoParametro.Texto, "Calculados por dia de atraso; sem previsão na convenção, 1% ao mês."),
        ], [Atalho("Abrir Taxas condominiais", CondominioTelas.Taxas)]),
    ];

    protected override async Task CarregarValoresAsync(VorticisApp app, long empresa, long usuario)
    {
        var c = await app.Cond().ConfiguracaoAsync(empresa);
        Campo("dia").Texto = c.DiaVencimento.ToString();
        Campo("multa").Texto = Formato.Num(c.MultaPercentual);
        Campo("juros").Texto = Formato.Num(c.JurosMensal);
    }

    protected override async Task GravarValoresAsync(VorticisApp app, long empresa, long usuario)
    {
        if (!int.TryParse(Campo("dia").Texto.Trim(), out var dia)) { Erros.Add("Informe o dia de vencimento em número."); return; }
        var r = await app.Cond().GravarConfiguracaoAsync(empresa, new ConfiguracaoCondominio(dia, Formato.LerValor(Campo("multa").Texto) ?? 0, Formato.LerValor(Campo("juros").Texto) ?? 0), usuario);
        if (!r.Sucesso) MostrarResultado(r, "");
    }
}

// ---------------------------------------------------------------------------------------------- rateio entre unidades

public sealed record RateioLinha(ParcelaRateio P)
{
    public string Unidade => P.Unidade;
    public string Responsavel => P.Responsavel ?? "";
    public string Fracao => P.Fracao.ToString("0.########", Formato.PtBr);
    public string Valor => Formato.Moeda(P.Valor);
}

/// <summary>Financeiro › Rateio entre unidades: divide um valor avulso (obra, despesa extra, fundo) pelas unidades e lança nas cobranças da competência.</summary>
public partial class RateioViewModel(IContexto contexto) : CondominioBase(contexto, Permissao.CondominioFinanceiro)
{
    public ObservableCollection<RateioLinha> Previa { get; } = [];
    public static IReadOnlyList<TaxasViewModel.OpcaoRateio> Criterios { get; } = [new(Vorticis.Condominio.Rateio.Fracao), new(Vorticis.Condominio.Rateio.Igual)];
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private string _valor = "";
    [ObservableProperty] private TaxasViewModel.OpcaoRateio _criterio = Criterios[0];
    [ObservableProperty] private DateTime? _competencia = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private string _totais = "";

    public Task CarregarAsync() { Previa.Clear(); Totais = ""; return Task.CompletedTask; }

    [RelayCommand]
    private Task VerPreviaAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Cond().PreviaRateioAsync(empresa, Formato.LerValor(Valor) ?? 0, Criterio.R, usuario);
        Previa.Clear();
        if (!r.Sucesso) { MostrarResultado(r, ""); Totais = ""; return; }
        foreach (var p in r.Valor) Previa.Add(new RateioLinha(p));
        Totais = $"{r.Valor.Count} unidade(s) · total rateado {Formato.Moeda(r.Valor.Sum(p => p.Valor))}";
    });

    [RelayCommand]
    private Task LancarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Cond().LancarRateioAsync(empresa, Ext.Dia(Competencia), Descricao, Formato.LerValor(Valor) ?? 0, Criterio.R, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        foreach (var a in r.Valor.Avisos) Erros.Add(a);
        Mensagem = $"Rateio lançado nas cobranças de {Ext.Dia(Competencia):MM/yyyy} de {r.Valor.Lancadas} unidade(s). Confira em Financeiro › Cobranças e recebimentos.";
    }, Permissao.CondominioFinanceiro);
}

// ---------------------------------------------------------------------------------------------- atas de assembleia

public sealed record AssembleiaLinha(Assembleia A)
{
    public string Numero => A.Numero.ToString();
    public string Data => A.Data.ToString("dd/MM/yyyy");
    public string Tipo => A.TipoTexto;
    public string Local => A.Local;
    public string Presentes => A.UnidadesPresentes.ToString();
}

/// <summary>Relatórios › Atas de Assembleia: registra a assembleia (mesa, pauta, deliberações) e gera a ata em PDF com as assinaturas.</summary>
public partial class AtasAssembleiaViewModel(IContexto contexto) : CondominioBase(contexto, Permissao.CondominioCadastros)
{
    public ObservableCollection<AssembleiaLinha> Lista { get; } = [];
    public static IReadOnlyList<string> Tipos { get; } = ["Ordinária", "Extraordinária"];
    public static IReadOnlyList<string> Convocacoes { get; } = ["Primeira convocação", "Segunda convocação"];
    [ObservableProperty] private AssembleiaLinha? _selecionada;
    [ObservableProperty] private string _numero = "";
    [ObservableProperty] private DateTime? _data = DateTime.Today;
    [ObservableProperty] private string _horario = "19:00";
    [ObservableProperty] private string _tipo = "Ordinária";
    [ObservableProperty] private int _convocacao;
    [ObservableProperty] private string _local = "";
    [ObservableProperty] private string _presidente = "";
    [ObservableProperty] private string _secretario = "";
    [ObservableProperty] private decimal? _unidadesPresentes = 0;
    [ObservableProperty] private string _pauta = "";
    [ObservableProperty] private string _deliberacoes = "";
    [ObservableProperty] private bool _listaPresenca = true;

    partial void OnSelecionadaChanged(AssembleiaLinha? value)
    {
        if (value?.A is not { } a) return;
        (Numero, Data, Horario, Tipo, Convocacao, Local, Presidente, Secretario, UnidadesPresentes, Pauta, Deliberacoes) =
            (a.Numero.ToString(), Ext.Dt(a.Data), a.Horario, a.TipoTexto, a.Convocacao - 1, a.Local, a.Presidente, a.Secretario, a.UnidadesPresentes, a.Pauta, a.Deliberacoes);
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        var g = NovaGeracao();
        var itens = await app.Cond().AssembleiasAsync(empresa, sessao.Id);
        if (!EhAtual(g)) return;
        Lista.Clear();
        foreach (var a in itens) Lista.Add(new AssembleiaLinha(a));
    }

    [RelayCommand]
    private void Nova()
    {
        Selecionada = null;
        (Numero, Data, Horario, Tipo, Convocacao, Local, Presidente, Secretario, UnidadesPresentes, Pauta, Deliberacoes) =
            ("(automático)", DateTime.Today, "19:00", "Ordinária", 0, "", "", "", 0, "", "");
    }

    private Assembleia Montar() => new(Selecionada?.A.Id ?? 0, Selecionada?.A.Numero ?? 0, Ext.Dia(Data), Horario.Trim(),
                                       Tipo == "Extraordinária" ? TipoAssembleia.Extraordinaria : TipoAssembleia.Ordinaria, Convocacao + 1, Local, Presidente, Secretario,
                                       (int)(UnidadesPresentes ?? 0), Pauta, Deliberacoes);

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Cond().GravarAssembleiaAsync(empresa, Montar(), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Selecionada = null;
        await CarregarAsync();
        Selecionada = Lista.FirstOrDefault(l => l.A.Numero == r.Valor);
        Mensagem = $"Assembleia nº {r.Valor} gravada.";
    }, Permissao.CondominioCadastros);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada?.A is not { } a) { Erros.Add("Selecione a assembleia na lista."); return; }
        var r = await app.Cond().ExcluirAssembleiaAsync(empresa, a.Id, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Nova();
        await CarregarAsync();
        Mensagem = $"Assembleia nº {a.Numero} excluída.";
    }, Permissao.CondominioCadastros);

    [RelayCommand]
    private Task GerarAtaAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada?.A is not { } a) { Erros.Add("Grave a assembleia e selecione-a na lista para gerar a ata."); return; }
        var (nome, cnpj, unidades, sindico) = await app.Cond().DadosAtaAsync(empresa);
        var pdf = new GeradorPdf().Gerar(AtaAssembleia.Documento(a, nome, cnpj, unidades, sindico, ListaPresenca));
        Mensagem = "Ata gerada: " + ArquivosGerados.Salvar($"ata_assembleia_{a.Numero}.pdf", pdf.Conteudo);
    }, Permissao.CondominioRelatorios);
}
