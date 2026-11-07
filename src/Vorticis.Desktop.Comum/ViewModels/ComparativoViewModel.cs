using CommunityToolkit.Mvvm.ComponentModel;
using Vorticis.Application.Relatorios;

namespace Vorticis.Desktop.Comum.ViewModels;

/// <summary>
/// Opção "Comparar com anos anteriores" de todos os relatórios contábeis (a mesma caixa em cada tela): o relatório sai com o período
/// escolhido e o mesmo período em cada um dos anos anteriores, com a variação em R$ e em %.
/// </summary>
public sealed partial class ComparativoViewModel : ObservableObject
{
    [ObservableProperty] private bool _ativo;
    /// <summary>Quantos anos anteriores entram (1 = só o ano anterior).</summary>
    [ObservableProperty] private decimal? _anos = 1;
    /// <summary>Relatórios sem um total de referência (balancete, razão...) não têm análise vertical (mantido para as telas).</summary>
    [ObservableProperty] private bool _podeAnaliseVertical;

    public OpcoesComparativo Opcoes() => new(AnosAnteriores: Math.Clamp((int)(Anos ?? 1), 1, OpcoesComparativo.MaximoAnos));
}
