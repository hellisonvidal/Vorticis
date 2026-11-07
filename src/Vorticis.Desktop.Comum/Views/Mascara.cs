using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>
/// Máscara de digitação: <c>v:Mascara.Tipo="Cnpj|Cpf|CpfCnpj|Cep|Data|Competencia|Valor"</c> num TextBox (ou CalendarDatePicker) formata
/// enquanto o usuário digita — 00.000.000/0000-00, 000.000.000-00, 00000-000, dd/mm/aaaa, mm/aaaa. Só age com o campo em foco:
/// valores vindos do sistema (ex.: documento mascarado de sócio) não são alterados.
/// "Valor" (campos de valor, base, alíquota, saldo...): aceita só números, vírgula, ponto e sinal enquanto digita, alinha à direita
/// e, ao sair do campo, mostra o número com separador de milhar e duas casas (1.234,56).
/// </summary>
public static class Mascara
{
    public static readonly AttachedProperty<string?> TipoProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Tipo", typeof(Mascara));

    public static string? GetTipo(Control c) => c.GetValue(TipoProperty);
    public static void SetTipo(Control c, string? v) => c.SetValue(TipoProperty, v);

    static Mascara()
    {
        TipoProperty.Changed.AddClassHandler<Control>((c, e) =>
        {
            switch (c)
            {
                case TextBox tb:
                    tb.TextChanged -= AoMudarTexto;
                    tb.LostFocus -= AoSairValor;
                    if (e.NewValue is string) tb.TextChanged += AoMudarTexto;
                    if (e.NewValue is "Valor")
                    {
                        tb.LostFocus += AoSairValor;
                        tb.TextAlignment = Avalonia.Media.TextAlignment.Right;
                    }
                    break;
                case CalendarDatePicker dp:
                    dp.TemplateApplied -= AoAplicarModelo;
                    dp.TemplateApplied += AoAplicarModelo;
                    break;
            }
        });
    }

    private static void AoAplicarModelo(object? sender, TemplateAppliedEventArgs e)
    {
        if (sender is CalendarDatePicker dp && e.NameScope.Find<TextBox>("PART_TextBox") is { } tb) SetTipo(tb, GetTipo(dp));
    }

    private static void AoMudarTexto(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox { IsFocused: true } tb || GetTipo(tb) is not { } tipo) return;
        var texto = tb.Text ?? "";
        var novo = Aplicar(tipo, texto);
        if (novo == texto) return;
        // Cursor depois do mesmo número de caracteres significativos.
        var antes = texto[..Math.Min(tb.CaretIndex, texto.Length)].Count(char.IsLetterOrDigit);
        var pos = 0;
        for (var n = 0; pos < novo.Length && n < antes; pos++) if (char.IsLetterOrDigit(novo[pos])) n++;
        tb.Text = novo;
        tb.CaretIndex = pos;
    }

    private static void AoSairValor(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is TextBox { IsReadOnly: false } tb && FormatarValor(tb.Text) is { } novo && novo != tb.Text) tb.Text = novo;
    }

    /// <summary>Valor digitado no padrão 1.234,56 (duas casas); null se o texto estiver vazio ou não for um número.</summary>
    public static string? FormatarValor(string? texto) =>
        Vorticis.Domain.Comum.Formato.LerValor(texto) is { } v ? Vorticis.Domain.Comum.Formato.Moeda(Math.Round(v, 2, MidpointRounding.AwayFromZero)) : null;

    /// <summary>Formata progressivamente o texto digitado no padrão do tipo.</summary>
    public static string Aplicar(string tipo, string texto)
    {
        switch (tipo)
        {
            case "Cnpj": return Formatar(Sig(texto, 14, alfanumerico: true), "00.000.000/0000-00");
            case "Cpf": return Formatar(Sig(texto, 11), "000.000.000-00");
            case "CpfCnpj":
                var d = Sig(texto, 14, alfanumerico: true);
                return d.Length <= 11 && d.All(char.IsAsciiDigit) ? Formatar(d, "000.000.000-00") : Formatar(d, "00.000.000/0000-00");
            case "Cep": return Formatar(Sig(texto, 8), "00000-000");
            case "Data": return Formatar(Sig(texto, 8), "00/00/0000");
            case "Competencia": return Formatar(Sig(texto, 6), "00/0000");
            case "Valor": return new string(texto.Where(ch => char.IsAsciiDigit(ch) || ch is ',' or '.' or '-').ToArray());
            default: return texto;
        }
    }

    private static string Sig(string texto, int max, bool alfanumerico = false) =>
        new(texto.ToUpperInvariant().Where(ch => alfanumerico ? char.IsAsciiLetterOrDigit(ch) : char.IsAsciiDigit(ch)).Take(max).ToArray());

    /// <summary>Encaixa os caracteres no molde ('0' = posição); separadores só aparecem entre caracteres digitados.</summary>
    private static string Formatar(string sig, string molde)
    {
        var sb = new System.Text.StringBuilder();
        var i = 0;
        foreach (var m in molde)
        {
            if (i >= sig.Length) break;
            sb.Append(m == '0' ? sig[i++] : m);
        }
        return sb.ToString();
    }
}
