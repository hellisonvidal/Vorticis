using CommunityToolkit.Mvvm.ComponentModel;
using Vorticis.Application.Relatorios;

namespace Vorticis.Desktop.Comum.ViewModels;

/// <summary>Um dos até três períodos que o usuário escolhe para comparar (mês, trimestre, ano ou intervalo).</summary>
public sealed partial class EscolhaPeriodo : ObservableObject
{
    [ObservableProperty] private bool _usar;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(EhIntervalo))] private int _tipo;
    [ObservableProperty] private DateTime? _inicio;
    [ObservableProperty] private DateTime? _fim;

    public bool EhIntervalo => Tipo == 3;

    public PeriodoEscolhido? Converter()
    {
        if (!Usar || Inicio is not { } i) return null;
        var ini = DateOnly.FromDateTime(i);
        return (TipoPeriodoComparado)Tipo == TipoPeriodoComparado.Intervalo
            ? new PeriodoEscolhido(TipoPeriodoComparado.Intervalo, ini, Fim is { } f ? DateOnly.FromDateTime(f) : null)
            : new PeriodoEscolhido((TipoPeriodoComparado)Tipo, ini);
    }
}

/// <summary>Opção "Comparativo" comum a todos os relatórios contábeis (a mesma barra aparece em cada tela).</summary>
public sealed partial class ComparativoViewModel : ObservableObject
{
    public static IReadOnlyList<string> Tipos { get; } = ["Mês", "Trimestre", "Ano", "Intervalo"];

    [ObservableProperty] private bool _ativo;
    [ObservableProperty] private bool _periodoAnterior = true;
    [ObservableProperty] private bool _mesmoPeriodoAnoAnterior;
    [ObservableProperty] private bool _variacaoAbsoluta = true;
    [ObservableProperty] private bool _variacaoPercentual = true;
    [ObservableProperty] private bool _analiseVertical;
    /// <summary>Relatórios sem um total de referência (balancete, razão...) não têm análise vertical.</summary>
    [ObservableProperty] private bool _podeAnaliseVertical;

    public IReadOnlyList<EscolhaPeriodo> Escolhas { get; } = [new(), new(), new()];

    public OpcoesComparativo Opcoes() => new(PeriodoAnterior, MesmoPeriodoAnoAnterior,
        Escolhas.Select(e => e.Converter()).OfType<PeriodoEscolhido>().ToList(), VariacaoAbsoluta, VariacaoPercentual, AnaliseVertical && PodeAnaliseVertical);
}
