using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Imobilizado;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Imobilizado;
using Vorticis.Domain.Seguranca;
using Vorticis.Desktop.Comum.ViewModels;

namespace Vorticis.Desktop.ViewModels;

public sealed record OpcaoMetodo(MetodoDepreciacao Metodo, string Nome)
{
    public static IReadOnlyList<OpcaoMetodo> Todos { get; } =
        [new(MetodoDepreciacao.Linear, "Linear (quotas constantes)"), new(MetodoDepreciacao.SomaDigitos, "Soma dos dígitos (decrescente)")];
}

public sealed record BemLinha(BemImobilizado Bem, string Grupo)
{
    public int Codigo => Bem.Codigo;
    public string Descricao => Bem.Descricao;
    public string Aquisicao => Bem.DataAquisicao.ToString("dd/MM/yyyy");
    public string Valor => Formato.Moeda(Bem.Valor);
    public string Acumulada => Formato.Moeda(Bem.Acumulada);
    public string ValorContabil => Formato.Moeda(Bem.ValorContabil);
    public string Situacao => Bem.DataBaixa is { } b ? $"Baixado {b:dd/MM/yyyy}" : Bem.UltimaCompetencia is { } u ? $"Depreciado até {u:MM/yyyy}" : "Ativo";
}

/// <summary>Imobilizado › Cadastro de bens.</summary>
public partial class BensViewModel(IContexto contexto) : CadastroViewModel(contexto)
{
    private IReadOnlyList<BemImobilizado> _todos = [];

    public ObservableCollection<BemLinha> Lista { get; } = [];
    public ObservableCollection<GrupoBem> Grupos { get; } = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TemSelecionado))] private BemLinha? _selecionado;
    [ObservableProperty] private string _filtro = "";
    [ObservableProperty] private string _resumo = "";

    [ObservableProperty] private string _codigo = "";
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RegraFiscal))] private GrupoBem? _grupo;
    [ObservableProperty] private string _plaqueta = "";
    [ObservableProperty] private string _localizacao = "";
    [ObservableProperty] private string _notaFiscal = "";
    [ObservableProperty] private string _fornecedor = "";
    [ObservableProperty] private DateTime? _dataAquisicao = DateTime.Today;
    [ObservableProperty] private DateTime? _inicioUso = DateTime.Today;
    [ObservableProperty] private string _valor = "";
    [ObservableProperty] private string _residual = "0,00";
    [ObservableProperty] private string _vidaUtil = "";
    [ObservableProperty] private OpcaoMetodo _metodo = OpcaoMetodo.Todos[0];
    [ObservableProperty] private int _turnos = 1;
    [ObservableProperty] private bool _proRataDia;
    [ObservableProperty] private string _depreciacaoAnterior = "0,00";
    [ObservableProperty] private string _contaBem = "";
    [ObservableProperty] private string _contaAcumulada = "";
    [ObservableProperty] private string _contaDespesa = "";
    [ObservableProperty] private DateTime? _dataBaixa;
    [ObservableProperty] private string _motivoBaixa = "";

    // Itens de notas fiscais de entrada a cadastrar (vindos do Fiscal ou da lista "Bens a cadastrar das notas"): um de cada vez, já preenchidos.
    private readonly Queue<BemACadastrar> _fila = new();
    private BemACadastrar? _daNota;
    private bool _gravouDaNota;
    private int _cadastradosDaNota;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TemAvisoNota))] private string _avisoNota = "";
    public bool TemAvisoNota => AvisoNota.Length > 0;

    public static IReadOnlyList<int> OpcoesTurnos { get; } = [1, 2, 3];
    public bool TemSelecionado => Selecionado is not null;
    public bool PodeEditar => Contexto.Pode(Permissao.LancamentoIncluir);
    public override bool PodeManter => PodeEditar;
    protected override string NomeRegistro => "bem";
    protected override string? DescricaoSelecionado => Selecionado is { } s ? $"{s.Codigo} – {s.Descricao}" : null;
    protected override string DetalheExclusao => "\n\nBem com depreciação lançada não é excluído: registre a baixa.";

    public string RegraFiscal => Grupo is not { } g ? ""
        : !g.Deprecia ? $"{g.Descricao}: não se deprecia. {g.BaseLegal}"
        : $"RFB: {g.TaxaAnualFiscal.ToString("0.##", Formato.PtBr)}% ao ano ({g.VidaUtilFiscalMeses} meses). {g.BaseLegal}";

    public void AtualizarPermissoes()
    {
        OnPropertyChanged(nameof(PodeEditar));
        AtualizarBarra();
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var g = NovaGeracao();
        var grupos = await app.Imobilizado.ListarGruposAsync();
        var bens = await app.Imobilizado.ListarBensAsync(empresa, sessao.Id);
        if (!EhAtual(g)) return;
        var grupoAtual = Grupo?.Id;
        Grupos.Clear();
        foreach (var x in grupos) Grupos.Add(x);
        Grupo = Grupos.FirstOrDefault(x => x.Id == grupoAtual);
        _todos = bens;
        Filtrar();
        var ativos = bens.Where(b => b.DataBaixa is null).ToList();
        Resumo = $"{ativos.Count} bem(ns) ativo(s) · custo {Formato.Moeda(ativos.Sum(b => b.Valor))} · depreciação acumulada {Formato.Moeda(ativos.Sum(b => b.Acumulada))}" +
                 $" · valor contábil {Formato.Moeda(ativos.Sum(b => b.ValorContabil))}";
        if (BensDasNotas.Retirar(Contexto) is { Count: > 0 } entregues) await IniciarPelasNotasAsync(entregues);
    }

    /// <summary>
    /// Itens de notas de entrada entregues pelo Fiscal ou pela lista "Bens a cadastrar das notas": abre a inclusão já preenchida com o
    /// primeiro (descrição, valor, datas, fornecedor, nota, grupo e contas sugeridos) e segue para o próximo a cada Salvar.
    /// </summary>
    public async Task IniciarPelasNotasAsync(IReadOnlyCollection<long> ids)
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var itens = await app.Imobilizado.ListarBensACadastrarAsync(empresa, sessao.Id, true, ids);
        if (itens.Count == 0) { Mensagem = "Os itens das notas já foram tratados."; return; }
        if (!PodeManter) { Mensagem = $"{itens.Count} bem(ns) das notas esperam cadastro, mas seu perfil não permite incluir bens. Eles ficam em Bens a cadastrar das notas."; return; }
        foreach (var i in itens.Where(i => i.Id != _daNota?.Id && _fila.All(f => f.Id != i.Id))) _fila.Enqueue(i);
        if (EmEdicao) { Mensagem = "Termine o bem em edição: os itens das notas serão abertos em seguida."; return; }
        _cadastradosDaNota = 0;
        ProximoDaNota();
    }

    private void ProximoDaNota()
    {
        if (_fila.Count == 0) { _daNota = null; AvisoNota = ""; return; }
        var item = _fila.Dequeue();
        IncluirCommand.Execute(null);
        if (!EmInclusao) { _fila.Clear(); _daNota = null; return; }
        _daNota = item;
        (Descricao, NotaFiscal, Fornecedor) = (item.Descricao, item.NotaTexto, item.CodigoFornecedor ?? "");
        (DataAquisicao, InicioUso) = (item.DataAquisicao.ToDateTime(TimeOnly.MinValue), item.InicioUso.ToDateTime(TimeOnly.MinValue));
        Valor = Formato.Moeda(item.Valor);
        Grupo = Grupos.FirstOrDefault(g => g.Id == item.GrupoSugeridoId);   // traz vida útil e contas do grupo
        if (!string.IsNullOrWhiteSpace(item.ContaSugerida)) ContaBem = item.ContaSugerida;
        var restam = _fila.Count;
        AvisoNota = $"Bem da nota {item.NotaTexto}" + (item.Fornecedor is { Length: > 0 } f ? $" de {f}" : "") + $" (item {item.NItem}, {item.MotivoTexto})" +
                    (item.Chave is { Length: > 0 } ch ? $", chave {ch}" : "") + ". " +
                    (Grupo is null ? "Escolha o grupo do bem (regra de depreciação). " : "") +
                    $"Confira os dados e clique em Salvar" + (restam > 0 ? $" (depois, mais {restam} item(ns) das notas)." : ".") +
                    " Cancelar deixa o item em Bens a cadastrar das notas.";
    }

    /// <summary>Salvar/Cancelar de um bem vindo de nota: segue para o próximo item ou encerra a fila.</summary>
    protected override void AoMudarModo()
    {
        base.AoMudarModo();
        if (_daNota is null || Modo != ModoCadastro.Consulta) return;
        if (_gravouDaNota)
        {
            _gravouDaNota = false;
            _cadastradosDaNota++;
            _daNota = null;
            if (_fila.Count > 0) { ProximoDaNota(); return; }
            AvisoNota = "";
            Mensagem = _cadastradosDaNota == 1 ? "Bem da nota cadastrado." : $"{_cadastradosDaNota} bens das notas cadastrados.";
            _cadastradosDaNota = 0;
            return;
        }
        // cancelou: o item e os que faltavam continuam pendentes
        var faltam = _fila.Count + 1;
        (_daNota, AvisoNota) = (null, "");
        _fila.Clear();
        _cadastradosDaNota = 0;
        Mensagem = $"{faltam} item(ns) das notas continuam em Imobilizado › Bens a cadastrar das notas.";
    }

    partial void OnFiltroChanged(string value) => Filtrar();

    private void Filtrar()
    {
        var id = Selecionado?.Bem.Id;
        var f = Filtro.Trim();
        var nomes = Grupos.ToDictionary(x => x.Id, x => x.Descricao);
        Lista.Clear();
        foreach (var b in _todos.Where(b => f.Length == 0 || b.Codigo.ToString() == f || b.Descricao.Contains(f, StringComparison.CurrentCultureIgnoreCase)
                                            || (b.Plaqueta ?? "").Contains(f, StringComparison.CurrentCultureIgnoreCase)))
            Lista.Add(new BemLinha(b, nomes.GetValueOrDefault(b.GrupoId, "")));
        Selecionado = Lista.FirstOrDefault(l => l.Bem.Id == id);
    }

    partial void OnSelecionadoChanged(BemLinha? value)
    {
        if (EmConsulta) MostrarSelecionado();
        AtualizarBarra();
    }

    /// <summary>Ao escolher o grupo na inclusão: vida útil sugerida pela taxa fiscal e contas do grupo.</summary>
    partial void OnGrupoChanged(GrupoBem? value)
    {
        if (!EmInclusao || value is null) return;
        VidaUtil = value.Deprecia ? value.VidaUtilFiscalMeses.ToString() : "0";
        (ContaBem, ContaAcumulada, ContaDespesa) = (value.ContaBem ?? "", value.ContaAcumulada ?? "", value.ContaDespesa ?? "");
    }

    protected override void MostrarSelecionado()
    {
        var b = Selecionado?.Bem;
        Codigo = b?.Codigo.ToString() ?? "";
        (Descricao, Plaqueta, Localizacao, NotaFiscal, Fornecedor) = (b?.Descricao ?? "", b?.Plaqueta ?? "", b?.Localizacao ?? "", b?.NotaFiscal ?? "", b?.CodigoFornecedor ?? "");
        Grupo = b is null ? null : Grupos.FirstOrDefault(g => g.Id == b.GrupoId);
        DataAquisicao = b is null ? DateTime.Today : b.DataAquisicao.ToDateTime(TimeOnly.MinValue);
        InicioUso = b is null ? DateTime.Today : b.InicioUso.ToDateTime(TimeOnly.MinValue);
        (Valor, Residual, VidaUtil) = (b is null ? "" : Formato.Moeda(b.Valor), Formato.Moeda(b?.Residual ?? 0), b?.VidaUtilMeses.ToString() ?? "");
        Metodo = OpcaoMetodo.Todos.First(m => m.Metodo == (b?.Metodo ?? MetodoDepreciacao.Linear));
        (Turnos, DepreciacaoAnterior, ProRataDia) = (b?.Turnos ?? 1, Formato.Moeda(b?.DepreciacaoAnterior ?? 0), b?.ProRataDia ?? false);
        (ContaBem, ContaAcumulada, ContaDespesa) = (b?.ContaBem ?? "", b?.ContaAcumulada ?? "", b?.ContaDespesa ?? "");
        DataBaixa = b?.DataBaixa?.ToDateTime(TimeOnly.MinValue);
        MotivoBaixa = b?.MotivoBaixa ?? "";
    }

    protected override void PrepararInclusao()
    {
        Selecionado = null;
        MostrarSelecionado();
        Codigo = ServicoImobilizado.ProximoCodigo(_todos).ToString();
        Grupo = null;
    }

    protected override async Task<bool> GravarAsync(bool inclusao)
    {
        var ok = false;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            if (Grupo is null) { Erros.Add("Escolha o grupo do bem."); return; }
            if (Formato.LerValor(Valor) is not { } valor) { Erros.Add("Valor de aquisição inválido."); return; }
            var residual = Formato.LerValor(Residual) ?? 0;
            var anterior = Formato.LerValor(DepreciacaoAnterior) ?? 0;
            _ = int.TryParse(VidaUtil.Trim(), out var vida);
            _ = int.TryParse(Codigo.Trim(), out var codigo);
            var id = inclusao ? 0 : Selecionado?.Bem.Id ?? 0;
            var daNota = inclusao ? _daNota : null;
            var bem = new BemImobilizado(id, codigo, Descricao, Grupo.Id, Plaqueta, Localizacao, NotaFiscal,
                Fornecedor, Data(DataAquisicao, DateOnly.FromDateTime(DateTime.Today)), Data(InicioUso, DateOnly.FromDateTime(DateTime.Today)),
                valor, residual, vida, Metodo.Metodo, Turnos, anterior, ContaBem, ContaAcumulada, ContaDespesa,
                DataBaixa is { } bx ? DateOnly.FromDateTime(bx) : null, MotivoBaixa, ProRataDia: ProRataDia);
            var r = daNota is null ? await app.Imobilizado.GravarBemAsync(empresa, bem, usuario)
                                   : await app.Imobilizado.GravarBemDaNotaAsync(empresa, daNota.Id, bem, usuario);   // vincula o bem ao item da nota
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            ok = true;
            if (daNota is not null)
            {
                // o Salvar da base volta para consulta (e o AoMudarModo abre o próximo item da nota)
                _gravouDaNota = true;
                await CarregarAsync();
                Mensagem = "Bem incluído e vinculado ao item da nota.";
                return;
            }
            Modo = ModoCadastro.Consulta;
            await CarregarAsync();
            Selecionado = Lista.FirstOrDefault(l => l.Bem.Id == r.Valor);
            Mensagem = inclusao ? $"Bem {Selecionado?.Codigo} incluído." : "Bem alterado.";
        }, Permissao.LancamentoIncluir);
        return ok;
    }

    protected override async Task<bool> ExcluirSelecionadoAsync()
    {
        var ok = false;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            if (Selecionado is null) return;
            var r = await app.Imobilizado.ExcluirBemAsync(empresa, Selecionado.Bem.Id, usuario);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            ok = true;
            Selecionado = null;
            await CarregarAsync();
            Mensagem = "Bem excluído.";
        }, Permissao.LancamentoIncluir);
        return ok;
    }
}

public sealed record GrupoLinha(GrupoBem Grupo)
{
    public string Codigo => Grupo.Codigo;
    public string Descricao => Grupo.Descricao;
    public string Taxa => Grupo.Deprecia ? Grupo.TaxaAnualFiscal.ToString("0.##", Formato.PtBr) + "%" : "—";
    public string VidaUtil => Grupo.Deprecia ? $"{Grupo.VidaUtilFiscalMeses / 12m:0.#} anos" : "não deprecia";
    public string Contas => string.Join(" / ", new[] { Grupo.ContaBem, Grupo.ContaAcumulada, Grupo.ContaDespesa }.Where(c => c is not null));
    public string BaseLegal => Grupo.BaseLegal ?? "";
}

/// <summary>Imobilizado › Regras de depreciação: grupos de bens com a taxa fiscal (RFB) e as contas sugeridas.</summary>
public partial class GruposDepreciacaoViewModel(IContexto contexto) : CadastroViewModel(contexto)
{
    public ObservableCollection<GrupoLinha> Lista { get; } = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TemSelecionado))] private GrupoLinha? _selecionado;
    [ObservableProperty] private string _codigo = "";
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private string _taxa = "";
    [ObservableProperty] private bool _deprecia = true;
    [ObservableProperty] private bool _intangivel;
    [ObservableProperty] private string _contaBem = "";
    [ObservableProperty] private string _contaAcumulada = "";
    [ObservableProperty] private string _contaDespesa = "";
    [ObservableProperty] private string _baseLegal = "";

    public bool TemSelecionado => Selecionado is not null;
    public bool PodeEditar => Contexto.Pode(Permissao.LancamentoIncluir);
    public override bool PodeManter => PodeEditar;
    protected override string NomeRegistro => "grupo";
    protected override string? DescricaoSelecionado => Selecionado is { } s ? $"{s.Codigo} – {s.Descricao}" : null;

    public void AtualizarPermissoes()
    {
        OnPropertyChanged(nameof(PodeEditar));
        AtualizarBarra();
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app) return;
        var g = NovaGeracao();
        var lista = await app.Imobilizado.ListarGruposAsync();
        if (!EhAtual(g)) return;
        var id = Selecionado?.Grupo.Id;
        Lista.Clear();
        foreach (var x in lista) Lista.Add(new GrupoLinha(x));
        Selecionado = Lista.FirstOrDefault(l => l.Grupo.Id == id);
    }

    partial void OnSelecionadoChanged(GrupoLinha? value)
    {
        if (EmConsulta) MostrarSelecionado();
        AtualizarBarra();
    }

    protected override void MostrarSelecionado()
    {
        var g = Selecionado?.Grupo;
        (Codigo, Descricao, Taxa, Deprecia, Intangivel) = (g?.Codigo ?? "", g?.Descricao ?? "", g?.TaxaAnualFiscal.ToString("0.##", Formato.PtBr) ?? "",
                                                          g?.Deprecia ?? true, g?.Intangivel ?? false);
        (ContaBem, ContaAcumulada, ContaDespesa, BaseLegal) = (g?.ContaBem ?? "", g?.ContaAcumulada ?? "", g?.ContaDespesa ?? "", g?.BaseLegal ?? "");
    }

    protected override void PrepararInclusao()
    {
        Selecionado = null;
        MostrarSelecionado();
        Codigo = Vorticis.Domain.Comum.Sequencia.MenorLivre(Lista.Select(l => l.Codigo)).ToString("00");
    }

    protected override async Task<bool> GravarAsync(bool inclusao)
    {
        var ok = false;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            var taxa = Formato.LerValor(Taxa.Replace("%", "")) ?? 0;
            var id = inclusao ? 0 : Selecionado?.Grupo.Id ?? 0;
            var r = await app.Imobilizado.GravarGrupoAsync(empresa,
                new GrupoBem(id, Codigo, Descricao, taxa, Deprecia, Intangivel, ContaBem, ContaAcumulada, ContaDespesa, BaseLegal), usuario);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            ok = true;
            Modo = ModoCadastro.Consulta;
            await CarregarAsync();
            Selecionado = Lista.FirstOrDefault(l => l.Grupo.Id == r.Valor);
            Mensagem = inclusao ? "Grupo incluído." : "Grupo alterado.";
        }, Permissao.LancamentoIncluir);
        return ok;
    }

    protected override async Task<bool> ExcluirSelecionadoAsync()
    {
        var ok = false;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            if (Selecionado is null) return;
            var r = await app.Imobilizado.ExcluirGrupoAsync(empresa, Selecionado.Grupo.Id, usuario);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            ok = true;
            Selecionado = null;
            await CarregarAsync();
            Mensagem = "Grupo excluído.";
        }, Permissao.LancamentoIncluir);
        return ok;
    }
}

public sealed record DepreciacaoLinha(ItemDepreciacao Item)
{
    public int Codigo => Item.Bem.Codigo;
    public string Descricao => Item.Bem.Descricao;
    public string Grupo => Item.Grupo.Descricao;
    public string Custo => Formato.Moeda(Item.Bem.Valor);
    public string AcumuladoAnterior => Formato.Moeda(Item.Quota?.AcumuladoAnterior ?? Item.Bem.Acumulada);
    public string Contabil => Item.Quota is { } q ? Formato.Moeda(q.Contabil) : "";
    public string Fiscal => Item.Quota is { } q ? Formato.Moeda(q.Fiscal) : "";
    public string Diferenca => Item.Quota is { Diferenca: not 0 } q ? Formato.Moeda(q.Diferenca) : "";
    public string Saldo => Formato.Moeda(Item.Bem.Valor - (Item.Quota?.AcumuladoApos ?? Item.Bem.Acumulada));
    public string Situacao => Item.Situacao;
}

/// <summary>Imobilizado › Cálculo e lançamento da depreciação mensal.</summary>
public partial class DepreciacaoViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public static IReadOnlyList<string> Meses { get; } =
        Enumerable.Range(1, 12).Select(m => $"{m:00} - {Formato.PtBr.DateTimeFormat.GetMonthName(m)}").ToList();

    public ObservableCollection<DepreciacaoLinha> Linhas { get; } = [];
    [ObservableProperty] private int _mes = DateTime.Today.Month - 1;
    [ObservableProperty] private decimal? _ano = DateTime.Today.Year;
    [ObservableProperty] private string _resumo = "";
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(LancarCommand))] private bool _podeLancar;

    private DateOnly Competencia => new((int)(Ano ?? DateTime.Today.Year), Mes + 1, 1);

    partial void OnMesChanged(int value) => Limpar();
    partial void OnAnoChanged(decimal? value) => Limpar();

    private void Limpar()
    {
        Linhas.Clear();
        Resumo = "";
        PodeLancar = false;
    }

    [RelayCommand]
    private Task CalcularAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        Limpar();
        var r = await app.Imobilizado.CalcularAsync(empresa, Competencia, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        foreach (var i in r.Valor) Linhas.Add(new DepreciacaoLinha(i));
        var quotas = r.Valor.Select(i => i.Quota).OfType<QuotaDepreciacao>().ToList();
        var contabil = quotas.Sum(q => q.Contabil);
        Resumo = r.Valor.Count == 0 ? "Nenhum bem cadastrado."
            : $"{Competencia:MM/yyyy}: depreciação contábil {Formato.Moeda(contabil)} · fiscal {Formato.Moeda(quotas.Sum(q => q.Fiscal))}" +
              $" · diferença (LALUR) {Formato.Moeda(quotas.Sum(q => q.Diferenca))}";
        PodeLancar = contabil > 0;
    });

    [RelayCommand(CanExecute = nameof(PodeLancar))]
    private Task LancarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (!await Contexto.ConfirmarAsync("Lançar depreciação",
                $"Lançar a depreciação de {Competencia:MM/yyyy} na contabilidade (último dia do mês)?", "Lançar", "Cancelar")) return;
        var r = await app.Imobilizado.LancarAsync(empresa, Competencia, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var msg = $"Depreciação de {Competencia:MM/yyyy} lançada: lançamento nº {r.Valor.Numero}.";
        await CalcularAsync();
        Mensagem = msg;
    }, Permissao.LancamentoIncluir);

    [RelayCommand]
    private Task EstornarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (!await Contexto.ConfirmarAsync("Estornar depreciação",
                $"Estornar a depreciação lançada em {Competencia:MM/yyyy}? Ela poderá ser calculada e lançada de novo.", "Estornar", "Cancelar")) return;
        var r = await app.Imobilizado.EstornarAsync(empresa, Competencia, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CalcularAsync();
        Mensagem = $"Depreciação de {Competencia:MM/yyyy} estornada.";
    }, Permissao.LancamentoEstornar);
}

/// <summary>Linha de Imobilizado › Bens a cadastrar das notas.</summary>
public sealed partial class BemACadastrarLinha(BemACadastrar item) : ObservableObject
{
    public BemACadastrar Item { get; } = item;
    [ObservableProperty] private bool _marcada;
    public string Nota => Item.NotaTexto;
    public string Fornecedor => Item.Fornecedor ?? "";
    public string Descricao => Item.Descricao;
    public string Data => Item.DataAquisicao.ToString("dd/MM/yyyy");
    public string Valor => Formato.Moeda(Item.Valor);
    public string Motivo => Item.MotivoTexto;
    public string Situacao => Item.Situacao switch
    {
        BemACadastrar.Cadastrado => "Cadastrado", BemACadastrar.Descartado => "Descartado", _ => "A cadastrar",
    };
}

/// <summary>
/// Imobilizado › Bens a cadastrar das notas: itens de notas fiscais de entrada que são bens do ativo imobilizado e ainda não viraram bem
/// (tanto os da contabilização on-line, cancelados na hora, quanto os da contabilização em lote). Daqui o contador cadastra (abre o
/// Cadastro de bens já preenchido) ou descarta.
/// </summary>
public sealed partial class BensACadastrarViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<BemACadastrarLinha> Linhas { get; } = [];
    [ObservableProperty] private bool _mostrarTratados;
    [ObservableProperty] private string _resumo = "";
    private int _geracao;

    public bool PodeManter => Contexto.Pode(Permissao.LancamentoIncluir);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeManter));

    partial void OnMostrarTratadosChanged(bool value) => _ = CarregarAsync();

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var geracao = Interlocked.Increment(ref _geracao);
        var itens = await app.Imobilizado.ListarBensACadastrarAsync(empresa, sessao.Id, !MostrarTratados);
        if (geracao != _geracao) return;
        Linhas.Clear();
        foreach (var i in itens) Linhas.Add(new BemACadastrarLinha(i));
        var abertos = itens.Where(i => i.Situacao == BemACadastrar.Pendente).ToList();
        Resumo = abertos.Count == 0 ? "Nenhum bem das notas esperando cadastro."
            : $"{abertos.Count} item(ns) de notas de entrada esperando cadastro como bem, no valor de {Formato.Moeda(abertos.Sum(i => i.Valor))}.";
    }

    private List<BemACadastrarLinha> Escolhidas(bool somentePendentes = true)
        => Linhas.Where(l => l.Marcada && (!somentePendentes || l.Item.Situacao == BemACadastrar.Pendente)).ToList();

    [RelayCommand]
    private void MarcarTodas()
    {
        foreach (var l in Linhas) l.Marcada = l.Item.Situacao == BemACadastrar.Pendente;
    }

    [RelayCommand]
    private void DesmarcarTodas()
    {
        foreach (var l in Linhas) l.Marcada = false;
    }

    /// <summary>Cadastrar: abre o Cadastro de bens, já preenchido, para cada item marcado (um de cada vez).</summary>
    [RelayCommand]
    private void Cadastrar()
    {
        Erros.Clear();
        Mensagem = null;
        var escolhidas = Escolhidas();
        if (escolhidas.Count == 0) { Erros.Add("Marque os itens que deseja cadastrar como bem."); return; }
        if (!PodeManter) { Erros.Add($"Seu perfil nesta empresa não permite: {Permissao.LancamentoIncluir.Descricao()}."); return; }
        BensDasNotas.Entregar(Contexto, escolhidas.Select(l => l.Item.Id));
        Contexto.AbrirTela(BensDasNotas.TelaCadastroBens);
    }

    [RelayCommand]
    private async Task DescartarAsync()
    {
        Erros.Clear();
        Mensagem = null;
        var escolhidas = Escolhidas();
        if (escolhidas.Count == 0) { Erros.Add("Marque os itens que deseja descartar."); return; }
        if (!await Contexto.ConfirmarAsync("Descartar itens",
                $"Descartar {escolhidas.Count} item(ns)? Eles saem da lista e não viram bem (use quando o item não é ativo imobilizado).", "Descartar", "Cancelar")) return;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            var feitos = 0;
            foreach (var l in escolhidas)
            {
                var r = await app.Imobilizado.DescartarBemACadastrarAsync(empresa, l.Item.Id, usuario);
                if (r.Sucesso) feitos++;
                else foreach (var e in r.Erros) Erros.Add($"{l.Descricao}: {e}");
            }
            await CarregarAsync();
            Mensagem = $"{feitos} item(ns) descartado(s).";
        }, Permissao.LancamentoIncluir);
    }

    [RelayCommand]
    private Task AtualizarAsync() => CarregarAsync();
}
