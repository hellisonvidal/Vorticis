using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vorticis.Desktop.Comum.ViewModels;

/// <summary>
/// Configurações › Configurações gerais › Preferências: tema de cores, pergunta ao fechar o programa e quantos dias à frente a tela inicial
/// mostra os vencimentos. Valem para este computador, em todos os módulos.
/// </summary>
public partial class PreferenciasViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public static IReadOnlyList<string> Temas { get; } = [.. SuiteViewModel.Temas.Select(t => t.Nome)];

    [ObservableProperty] private int _tema;
    [ObservableProperty] private bool _confirmarAoFechar = true;
    [ObservableProperty] private int _diasVencimentos = 15;

    public int DiasMinimo => Preferencias.DiasMinimo;
    public int DiasMaximo => Preferencias.DiasMaximo;

    public void Carregar()
    {
        var p = PreferenciasGerais.Atual;
        (ConfirmarAoFechar, DiasVencimentos) = (p.ConfirmarAoFechar, p.DiasVencimentos);
        Tema = Math.Max(0, SuiteViewModel.Temas.ToList().FindIndex(t => t.Valor == PreferenciasGerais.Tema));
        Mensagem = null;
        Erros.Clear();
    }

    [RelayCommand]
    private void Gravar()
    {
        Erros.Clear();
        if (DiasVencimentos is < Preferencias.DiasMinimo or > Preferencias.DiasMaximo)
        {
            Erros.Add($"Os dias dos vencimentos devem ficar entre {Preferencias.DiasMinimo} e {Preferencias.DiasMaximo}.");
            return;
        }
        try
        {
            PreferenciasGerais.Gravar(new Preferencias(ConfirmarAoFechar, DiasVencimentos));
            PreferenciasGerais.GravarTema(SuiteViewModel.Temas[Math.Clamp(Tema, 0, SuiteViewModel.Temas.Count - 1)].Valor);
            Mensagem = "Preferências gravadas. Valem para este computador, em todos os módulos.";
        }
        catch (Exception ex) { Erros.Add($"Não foi possível gravar as preferências: {ex.Message}"); }
    }
}
