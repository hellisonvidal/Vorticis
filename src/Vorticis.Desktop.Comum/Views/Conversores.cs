using Avalonia.Data.Converters;
using Avalonia.Media;
using Vorticis.Application.Cadastros;

namespace Vorticis.Desktop.Comum.Views;

public static class Conversores
{
    /// <summary>Negrito quando verdadeiro (nome do módulo na árvore da Suite).</summary>
    public static readonly IValueConverter NegritoSe = new FuncValueConverter<bool, Avalonia.Media.FontWeight>(b => b ? Avalonia.Media.FontWeight.SemiBold : Avalonia.Media.FontWeight.Normal);

    /// <summary>Estrela cheia (favorito) ou só o contorno.</summary>
    public static readonly IValueConverter EstrelaSe = new FuncValueConverter<bool, Avalonia.Media.Geometry?>(b => b ? Icones.Star : Icones.StarOutline);

    public static readonly IValueConverter OkOuErro =
        new FuncValueConverter<bool, IBrush>(ok => ok ? new SolidColorBrush(Color.Parse(TemaCores.Escuro ? "#6FD39A" : "#1B6E3A")) : new SolidColorBrush(Color.Parse(TemaCores.Escuro ? "#FF8A80" : "#B3261E")));

    public static readonly IValueConverter NomeRegime = new FuncValueConverter<RegimeTributario, string>(r => r switch
    {
        RegimeTributario.MEI => "MEI",
        RegimeTributario.SimplesNacional => "Simples Nacional",
        RegimeTributario.LucroPresumido => "Lucro Presumido",
        RegimeTributario.LucroReal => "Lucro Real",
        RegimeTributario.LucroArbitrado => "Lucro Arbitrado",
        RegimeTributario.ImuneIsenta => "Imune / Isenta",
        _ => r.ToString(),
    });
}
