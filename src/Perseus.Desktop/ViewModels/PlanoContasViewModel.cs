using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Application.Contabil;
using Perseus.Domain.Contabil;
using Perseus.Domain.Seguranca;

namespace Perseus.Desktop.ViewModels;

public sealed record OpcaoNatureza(string Codigo, string Nome)
{
    public static readonly IReadOnlyList<OpcaoNatureza> Todas =
    [
        new("01", "01 - Ativo"), new("02", "02 - Passivo"), new("03", "03 - Patrimônio líquido"),
        new("04", "04 - Resultado"), new("05", "05 - Compensação"), new("09", "09 - Outras"),
    ];
}

public sealed record OpcaoSaldo(IndicadorDC DC, string Nome)
{
    public static readonly IReadOnlyList<OpcaoSaldo> Todas = [new(IndicadorDC.D, "Devedora"), new(IndicadorDC.C, "Credora")];
    public static OpcaoSaldo De(IndicadorDC dc) => Todas.First(o => o.DC == dc);
}

public sealed record ContaLinha(ContaDetalhe Conta)
{
    public string Codigo => new string(' ', (Conta.Nivel - 1) * 3) + Conta.Codigo;
    public string Descricao => Conta.Descricao;
    public string Tipo => Conta.Analitica ? "Analítica" : "Sintética";
    public string Natureza => OpcaoNatureza.Todas.FirstOrDefault(n => n.Codigo == Conta.CodNat)?.Nome[5..] ?? Conta.CodNat;
    public string Saldo => Conta.Analitica ? LinhaBalancete.Formatar(Conta.Saldo) : "";
    public string Situacao => Conta.Ativa ? "" : "Inativa";
}

/// <summary>Cadastros › Plano de contas.</summary>
public partial class PlanoContasViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    private IReadOnlyList<ContaDetalhe> _plano = [];

    public ObservableCollection<ContaLinha> Lista { get; } = [];

    [ObservableProperty] private string _filtro = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TemSelecionada))] private ContaLinha? _selecionada;

    // edição
    [ObservableProperty] private string _descricaoEdicao = "";
    [ObservableProperty] private OpcaoSaldo _saldoEdicao = OpcaoSaldo.Todas[0];
    [ObservableProperty] private bool _exigeCcEdicao;
    [ObservableProperty] private bool _exigeParticipanteEdicao;
    [ObservableProperty] private bool _ativaEdicao = true;

    // inclusão
    [ObservableProperty] private string _novoCodigo = "";
    [ObservableProperty] private string _novaDescricao = "";
    [ObservableProperty] private OpcaoNatureza _novaNatureza = OpcaoNatureza.Todas[0];
    [ObservableProperty] private OpcaoSaldo _novoSaldo = OpcaoSaldo.Todas[0];
    [ObservableProperty] private bool _novaExigeCc;
    [ObservableProperty] private DateTime? _novaDataInclusao = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private string _dicaPai = "";

    public bool TemSelecionada => Selecionada is not null;
    public bool PodeEditar => Contexto.Pode(Permissao.PlanoContasManter);

    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeEditar));

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var id = Selecionada?.Conta.Id;
        var geracao = NovaGeracao();
        var plano = await app.PlanoContas.ListarAsync(empresa, sessao.Id);
        if (!EhAtual(geracao)) return;
        _plano = plano;
        Filtrar();
        Selecionada = Lista.FirstOrDefault(l => l.Conta.Id == id);
    }

    partial void OnFiltroChanged(string value) => Filtrar();

    private void Filtrar()
    {
        var f = Filtro.Trim();
        Lista.Clear();
        foreach (var c in _plano.Where(c => f.Length == 0 || c.Codigo.StartsWith(f, StringComparison.Ordinal)
                                            || c.Descricao.Contains(f, StringComparison.CurrentCultureIgnoreCase)))
            Lista.Add(new ContaLinha(c));
    }

    partial void OnSelecionadaChanged(ContaLinha? value)
    {
        if (value is null) return;
        var c = value.Conta;
        (DescricaoEdicao, SaldoEdicao, ExigeCcEdicao, ExigeParticipanteEdicao, AtivaEdicao) =
            (c.Descricao, OpcaoSaldo.De(c.NaturezaSaldo), c.ExigeCentroCusto, c.ExigeParticipante, c.Ativa);
        // Sugere uma subconta da selecionada (ou irmã, se ela for analítica)
        var baseCodigo = c.Analitica && c.ContaPaiId is not null ? c.Codigo[..c.Codigo.LastIndexOf('.')] : c.Codigo;
        NovoCodigo = SugerirProximo(baseCodigo);
    }

    private string SugerirProximo(string pai)
    {
        var filhos = _plano.Where(x => x.Codigo.StartsWith(pai + ".", StringComparison.Ordinal) && x.Codigo.Count(ch => ch == '.') == pai.Count(ch => ch == '.') + 1)
                           .Select(x => x.Codigo[(pai.Length + 1)..]).ToArray();
        if (filhos.Length == 0) return pai + ".01";
        var ultimo = filhos.Max(s => (s.Length, s)).s;
        return long.TryParse(ultimo, out var n) ? $"{pai}.{(n + 1).ToString().PadLeft(ultimo.Length, '0')}" : pai + ".";
    }

    /// <summary>Ao digitar o código, herda natureza e saldo da conta superior.</summary>
    partial void OnNovoCodigoChanged(string value)
    {
        var codigo = value.Trim();
        var i = codigo.LastIndexOf('.');
        var pai = i > 0 ? _plano.FirstOrDefault(c => c.Codigo == codigo[..i]) : null;
        if (pai is null) { DicaPai = i > 0 ? $"Conta superior {codigo[..i]} não encontrada." : "Conta de nível 1 (sintética)."; return; }
        NovaNatureza = OpcaoNatureza.Todas.FirstOrDefault(n => n.Codigo == pai.CodNat) ?? NovaNatureza;
        NovoSaldo = OpcaoSaldo.De(pai.NaturezaSaldo);
        DicaPai = $"Subconta de {pai.Codigo} - {pai.Descricao}" +
                  (pai.Analitica ? (pai.TemMovimento ? " (ATENÇÃO: superior tem lançamentos)" : " (a superior passará a sintética)") : "");
    }

    [RelayCommand]
    private Task IncluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.PlanoContas.IncluirAsync(empresa, new NovaConta(NovoCodigo, NovaDescricao, NovaNatureza.Codigo, NovoSaldo.DC,
            NovaExigeCc, false, DateOnly.FromDateTime(NovaDataInclusao ?? DateTime.Today)), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var codigo = NovoCodigo.Trim();
        NovaDescricao = "";
        await CarregarAsync();
        Selecionada = Lista.FirstOrDefault(l => l.Conta.Id == r.Valor);
        Mensagem = $"Conta {codigo} incluída.";
    }, Permissao.PlanoContasManter);

    [RelayCommand]
    private Task SalvarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada is null) return;
        var r = await app.PlanoContas.AlterarAsync(empresa, Selecionada.Conta.Id, DescricaoEdicao, SaldoEdicao.DC, ExigeCcEdicao,
                                                   ExigeParticipanteEdicao, AtivaEdicao, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = "Conta alterada.";
    }, Permissao.PlanoContasManter);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada is null) return;
        var codigo = Selecionada.Conta.Codigo;
        var r = await app.PlanoContas.ExcluirAsync(empresa, Selecionada.Conta.Id, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Selecionada = null;
        await CarregarAsync();
        Mensagem = $"Conta {codigo} excluída.";
    }, Permissao.PlanoContasManter);
}
