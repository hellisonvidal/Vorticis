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

/// <summary>Cadastros › Plano de contas (Incluir / Alterar / Excluir).</summary>
public partial class PlanoContasViewModel(IContexto contexto) : CadastroViewModel(contexto)
{
    private IReadOnlyList<ContaDetalhe> _plano = [];

    public ObservableCollection<ContaLinha> Lista { get; } = [];

    [ObservableProperty] private string _filtro = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TemSelecionada))] private ContaLinha? _selecionada;

    // formulário (código, natureza e data de inclusão só na inclusão: exigência da ECD)
    [ObservableProperty] private string _codigo = "";
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private OpcaoNatureza _natureza = OpcaoNatureza.Todas[0];
    [ObservableProperty] private OpcaoSaldo _saldo = OpcaoSaldo.Todas[0];
    [ObservableProperty] private bool _exigeCc;
    [ObservableProperty] private bool _exigeParticipante;
    [ObservableProperty] private bool _ativa = true;
    [ObservableProperty] private DateTime? _dataInclusao = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private string _dicaPai = "";

    public bool TemSelecionada => Selecionada is not null;
    public bool PodeEditar => Contexto.Pode(Permissao.PlanoContasManter);
    public override bool PodeManter => PodeEditar;
    protected override string NomeRegistro => "conta";
    protected override string? DescricaoSelecionado => Selecionada is { } s ? $"{s.Conta.Codigo} – {s.Conta.Descricao}" : null;
    protected override string DetalheExclusao => "\n\nConta com lançamentos não é excluída: use Alterar e desmarque Ativa depois de zerar o saldo.";

    public void AtualizarPermissoes()
    {
        OnPropertyChanged(nameof(PodeEditar));
        AtualizarBarra();
    }

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
        if (EmConsulta) MostrarSelecionado();
        AtualizarBarra();
    }

    protected override void MostrarSelecionado()
    {
        if (Selecionada is not { } linha)
        {
            (Codigo, Descricao, DicaPai) = ("", "", "");
            return;
        }
        var c = linha.Conta;
        Codigo = c.Codigo;
        (Descricao, Saldo, ExigeCc, ExigeParticipante, Ativa) = (c.Descricao, OpcaoSaldo.De(c.NaturezaSaldo), c.ExigeCentroCusto, c.ExigeParticipante, c.Ativa);
        Natureza = OpcaoNatureza.Todas.FirstOrDefault(n => n.Codigo == c.CodNat) ?? Natureza;
        DicaPai = c.Analitica ? "Conta analítica (recebe lançamentos)." : "Conta sintética (agrupa subcontas).";
    }

    /// <summary>Nova conta: sugere uma subconta da selecionada (ou irmã, se ela for analítica).</summary>
    protected override void PrepararInclusao()
    {
        (Descricao, ExigeCc, ExigeParticipante, Ativa) = ("", false, false, true);
        DataInclusao = new DateTime(DateTime.Today.Year, 1, 1);
        if (Selecionada?.Conta is { } c)
        {
            var baseCodigo = c.Analitica && c.ContaPaiId is not null ? c.Codigo[..c.Codigo.LastIndexOf('.')] : c.Codigo;
            Codigo = SugerirProximo(baseCodigo);
        }
        else Codigo = "";
    }

    private string SugerirProximo(string pai)
    {
        var filhos = _plano.Where(x => x.Codigo.StartsWith(pai + ".", StringComparison.Ordinal) && x.Codigo.Count(ch => ch == '.') == pai.Count(ch => ch == '.') + 1)
                           .Select(x => x.Codigo[(pai.Length + 1)..]).ToArray();
        if (filhos.Length == 0) return pai + ".01";
        var ultimo = filhos.Max(s => (s.Length, s)).s;
        return long.TryParse(ultimo, out var n) ? $"{pai}.{(n + 1).ToString().PadLeft(ultimo.Length, '0')}" : pai + ".";
    }

    /// <summary>Na inclusão, ao digitar o código, herda natureza e saldo da conta superior.</summary>
    partial void OnCodigoChanged(string value)
    {
        if (!EmInclusao) return;   // ao preparar a inclusão, roda de novo quando o modo muda (AoMudarModo)
        var codigo = value.Trim();
        var i = codigo.LastIndexOf('.');
        var pai = i > 0 ? _plano.FirstOrDefault(c => c.Codigo == codigo[..i]) : null;
        if (pai is null) { DicaPai = i > 0 ? $"Conta superior {codigo[..i]} não encontrada." : "Conta de nível 1 (sintética)."; return; }
        Natureza = OpcaoNatureza.Todas.FirstOrDefault(n => n.Codigo == pai.CodNat) ?? Natureza;
        Saldo = OpcaoSaldo.De(pai.NaturezaSaldo);
        DicaPai = $"Subconta de {pai.Codigo} - {pai.Descricao}" +
                  (pai.Analitica ? (pai.TemMovimento ? " (ATENÇÃO: superior tem lançamentos)" : " (a superior passará a sintética)") : "");
    }

    protected override void AoMudarModo()
    {
        if (EmInclusao) OnCodigoChanged(Codigo);
    }

    protected override async Task<bool> GravarAsync(bool inclusao)
    {
        var ok = false;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            long id;
            if (inclusao)
            {
                var r = await app.PlanoContas.IncluirAsync(empresa, new NovaConta(Codigo, Descricao, Natureza.Codigo, Saldo.DC,
                    ExigeCc, ExigeParticipante, DateOnly.FromDateTime(DataInclusao ?? DateTime.Today)), usuario);
                if (!r.Sucesso) { MostrarResultado(r, ""); return; }
                id = r.Valor;
            }
            else
            {
                if (Selecionada is null) { Erros.Add("Selecione uma conta."); return; }
                id = Selecionada.Conta.Id;
                var r = await app.PlanoContas.AlterarAsync(empresa, id, Descricao, Saldo.DC, ExigeCc, ExigeParticipante, Ativa, usuario);
                if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            }
            ok = true;
            var codigo = Codigo.Trim();
            Modo = ModoCadastro.Consulta;
            await CarregarAsync();
            Selecionada = Lista.FirstOrDefault(l => l.Conta.Id == id);
            Mensagem = inclusao ? $"Conta {codigo} incluída." : "Conta alterada.";
        }, Permissao.PlanoContasManter);
        return ok;
    }

    protected override async Task<bool> ExcluirSelecionadoAsync()
    {
        var ok = false;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            if (Selecionada is null) return;
            var codigo = Selecionada.Conta.Codigo;
            var r = await app.PlanoContas.ExcluirAsync(empresa, Selecionada.Conta.Id, usuario);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            ok = true;
            Selecionada = null;
            await CarregarAsync();
            Mensagem = $"Conta {codigo} excluída.";
        }, Permissao.PlanoContasManter);
        return ok;
    }
}
