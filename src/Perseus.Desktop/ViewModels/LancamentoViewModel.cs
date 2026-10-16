using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Domain.Contabil;

namespace Perseus.Desktop.ViewModels;

public partial class LinhaPartidaViewModel : ObservableObject
{
    [ObservableProperty] private string _conta = "";
    [ObservableProperty] private string _dc = "D";
    [ObservableProperty] private string _valor = "";
    [ObservableProperty] private string _historico = "";
    [ObservableProperty] private string _centroCusto = "";

    public decimal? ValorDecimal => Numeros.LerValor(Valor);
    public IndicadorDC? Indicador => Dc.Trim().ToUpperInvariant() switch { "D" => IndicadorDC.D, "C" => IndicadorDC.C, _ => null };
}

public sealed record ContaSelecionavel(string Codigo, string Descricao)
{
    public string Rotulo => $"{Codigo}  {Descricao}";
}

/// <summary>Digitação de lançamento com totais D/C em tempo real.</summary>
public partial class LancamentoViewModel : ModuloViewModel
{
    private IReadOnlyList<ContaSelecionavel> _todasContas = [];

    public LancamentoViewModel(IContexto contexto) : base(contexto)
    {
        Linhas.CollectionChanged += AoMudarLinhas;
        Limpar();
    }

    public ObservableCollection<LinhaPartidaViewModel> Linhas { get; } = [];
    public ObservableCollection<ContaSelecionavel> Contas { get; } = [];

    [ObservableProperty] private DateTime? _data = DateTime.Today;
    [ObservableProperty] private LinhaPartidaViewModel? _linhaSelecionada;
    [ObservableProperty] private ContaSelecionavel? _contaSelecionada;
    [ObservableProperty] private string _filtroConta = "";
    [ObservableProperty] private string _totalDebito = "0,00";
    [ObservableProperty] private string _totalCredito = "0,00";
    [ObservableProperty] private string _diferenca = "0,00";
    [ObservableProperty] private bool _balanceado;

    public async Task AoTrocarEmpresaAsync()
    {
        Limpar();
        Contas.Clear();
        _todasContas = [];
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa) return;
        var plano = await app.Consultas.ObterPlanoAsync(empresa, CancellationToken.None);
        _todasContas = plano.Where(c => c.Analitica).Select(c => new ContaSelecionavel(c.Codigo, c.Descricao)).ToArray();
        Filtrar();
    }

    partial void OnFiltroContaChanged(string value) => Filtrar();

    private void Filtrar()
    {
        Contas.Clear();
        var f = FiltroConta.Trim();
        foreach (var c in _todasContas.Where(c => f.Length == 0
                     || c.Codigo.StartsWith(f, StringComparison.Ordinal)
                     || c.Descricao.Contains(f, StringComparison.CurrentCultureIgnoreCase)))
            Contas.Add(c);
    }

    /// <summary>Escolher uma conta na lista preenche a linha selecionada.</summary>
    partial void OnContaSelecionadaChanged(ContaSelecionavel? value)
    {
        if (value is not null && LinhaSelecionada is not null) LinhaSelecionada.Conta = value.Codigo;
    }

    [RelayCommand]
    private void AdicionarLinha()
    {
        var anterior = Linhas.LastOrDefault();
        var nova = new LinhaPartidaViewModel
        {
            Historico = anterior?.Historico ?? "",
            Dc = anterior?.Indicador == IndicadorDC.D ? "C" : "D",
        };
        // Sugere o valor que equilibra o lançamento.
        var dif = SomaDC(IndicadorDC.D) - SomaDC(IndicadorDC.C);
        if (dif != 0)
        {
            nova.Dc = dif > 0 ? "C" : "D";
            nova.Valor = Numeros.Moeda(Math.Abs(dif));
        }
        Linhas.Add(nova);
        LinhaSelecionada = nova;
    }

    [RelayCommand]
    private void RemoverLinha()
    {
        if (LinhaSelecionada is not null) Linhas.Remove(LinhaSelecionada);
    }

    [RelayCommand]
    private void Limpar()
    {
        Linhas.Clear();
        Linhas.Add(new LinhaPartidaViewModel { Dc = "D" });
        Linhas.Add(new LinhaPartidaViewModel { Dc = "C" });
        LinhaSelecionada = Linhas[0];
    }

    [RelayCommand]
    private Task EfetivarAsync() => ExecutarAsync(async (app, empresa) =>
    {
        var preenchidas = Linhas.Where(l => l.Conta.Trim().Length > 0 || l.Valor.Trim().Length > 0).ToArray();
        var problemas = preenchidas.Select((l, i) =>
                l.Indicador is null ? $"Linha {i + 1}: indique D ou C."
                : l.ValorDecimal is null ? $"Linha {i + 1}: valor inválido." : null)
            .OfType<string>().ToArray();
        if (problemas.Length > 0) { foreach (var p in problemas) Erros.Add(p); return; }

        var proposta = new LancamentoProposto(empresa, Data(Data, DateOnly.FromDateTime(DateTime.Today)), OrigemLancamento.Manual,
            preenchidas.Select(l => new PartidaProposta(l.Conta.Trim(), l.Indicador!.Value, l.ValorDecimal!.Value, l.Historico,
                string.IsNullOrWhiteSpace(l.CentroCusto) ? null : l.CentroCusto.Trim())).ToArray());

        var r = await app.Lancamentos.EfetivarAsync(proposta, null);
        MostrarResultado(r, r.Sucesso ? $"Lançamento nº {r.Valor.Numero} efetivado." : "");
        if (r.Sucesso) Limpar();
    });

    private decimal SomaDC(IndicadorDC dc) => Linhas.Where(l => l.Indicador == dc).Sum(l => l.ValorDecimal ?? 0);

    private void Recalcular()
    {
        var d = SomaDC(IndicadorDC.D);
        var c = SomaDC(IndicadorDC.C);
        TotalDebito = Numeros.Moeda(d);
        TotalCredito = Numeros.Moeda(c);
        Diferenca = Numeros.Moeda(d - c);
        Balanceado = d == c && d > 0;
    }

    private void AoMudarLinhas(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (LinhaPartidaViewModel l in e.NewItems ?? Array.Empty<LinhaPartidaViewModel>()) l.PropertyChanged += AoMudarLinha;
        foreach (LinhaPartidaViewModel l in e.OldItems ?? Array.Empty<LinhaPartidaViewModel>()) l.PropertyChanged -= AoMudarLinha;
        Recalcular();
    }

    private void AoMudarLinha(object? sender, PropertyChangedEventArgs e) => Recalcular();
}
