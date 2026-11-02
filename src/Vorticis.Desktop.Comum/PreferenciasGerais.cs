using System.Text.Json;
using Vorticis.Desktop.Comum.ViewModels;

namespace Vorticis.Desktop.Comum;

/// <summary>Preferências deste computador (Configurações › Configurações gerais › Preferências), em preferencias.json na pasta local.</summary>
/// <param name="MinutosBloqueio">Bloqueio por inatividade: minutos sem uso até pedir a senha (0 = desligado).</param>
public sealed record Preferencias(bool ConfirmarAoFechar = true, int DiasVencimentos = 15, int MinutosBloqueio = 15)
{
    public const int DiasMinimo = 1, DiasMaximo = 90;
    public const int BloqueioMaximo = 240;
}

/// <summary>
/// Leitura e gravação das preferências gerais. O tema de cores é o mesmo da Suite (arquivo do tema) e vale na hora para o programa aberto.
/// Arquivo ausente ou ilegível vale o padrão.
/// </summary>
public static class PreferenciasGerais
{
    public static string Arquivo { get; set; } = Path.Combine(Configuracao.PastaLocal, "preferencias.json");

    public static Preferencias Atual
    {
        get
        {
            try { return File.Exists(Arquivo) ? JsonSerializer.Deserialize<Preferencias>(File.ReadAllText(Arquivo)) ?? new() : new(); }
            catch (Exception) { return new(); }
        }
    }

    public static void Gravar(Preferencias p)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Arquivo)!);
        File.WriteAllText(Arquivo, JsonSerializer.Serialize(p with { DiasVencimentos = Math.Clamp(p.DiasVencimentos, Preferencias.DiasMinimo, Preferencias.DiasMaximo),
                                                                     MinutosBloqueio = Math.Clamp(p.MinutosBloqueio, 0, Preferencias.BloqueioMaximo) }));
    }

    public static TemaSuite Tema
    {
        get
        {
            try { return File.Exists(SuiteViewModel.ArquivoTema) && Enum.TryParse<TemaSuite>(File.ReadAllText(SuiteViewModel.ArquivoTema).Trim(), out var t) && Enum.IsDefined(t) ? t : TemaSuite.Claro; }
            catch (Exception) { return TemaSuite.Claro; }
        }
    }

    public static void GravarTema(TemaSuite tema)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SuiteViewModel.ArquivoTema)!);
        File.WriteAllText(SuiteViewModel.ArquivoTema, tema.ToString());
        void Aplicar()
        {
            if (Avalonia.Application.Current is { } app) app.RequestedThemeVariant = SuiteViewModel.VarianteDe(tema);
        }
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) Aplicar(); else Avalonia.Threading.Dispatcher.UIThread.Post(Aplicar);
    }
}
