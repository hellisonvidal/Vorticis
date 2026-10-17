using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Perseus.Desktop.ViewModels;

namespace Perseus.Desktop.Views;

/// <summary>
/// Digitação pelo teclado: Enter avança de campo (pula a conta quando o reduzido já a identificou), seta para baixo
/// desce de linha e, na última, cria outra; seta para cima sobe e cancela a linha recém-criada ainda vazia;
/// Ctrl+Delete exclui a linha. Valor formatado ao sair do campo; nome da conta resolvido ao sair do campo da conta.
/// </summary>
public partial class LancamentoView : UserControl
{
    private static readonly string[] CamposDobrada =
        ["data", "reduzido", "conta", "reduzidocredito", "contacredito", "valor", "codhistorico", "historico", "centrocusto"];
    private static readonly string[] CamposSimples = ["reduzido", "conta", "dc", "valor", "codhistorico", "historico", "centrocusto"];

    /// <summary>Sugestões de conta: reduzido, início do código ou parte do nome.</summary>
    public static AutoCompleteFilterPredicate<object?> FiltroConta { get; } =
        (texto, item) => item is ContaSelecionavel c && LancamentoViewModel.Combina(c, texto);

    private LancamentoViewModel? _vm;

    public LancamentoView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, AoTeclar, RoutingStrategies.Tunnel);
        AddHandler(GotFocusEvent, AoEntrarNoCampo, RoutingStrategies.Bubble);
        AddHandler(LostFocusEvent, AoSairDoCampo, RoutingStrategies.Bubble);
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.Linhas.CollectionChanged -= AoMudarLinhas;
            _vm = DataContext as LancamentoViewModel;
            if (_vm is not null) _vm.Linhas.CollectionChanged += AoMudarLinhas;
        };
    }

    private string[] Campos => _vm?.PartidaDobrada == true ? CamposDobrada : CamposSimples;
    private ItemsControl Linhas => _vm?.PartidaDobrada == true ? LinhasDobrada : LinhasSimples;

    /// <summary>Linhas recomeçadas (abrir, Limpar, Salvar): foco no primeiro campo.</summary>
    private void AoMudarLinhas(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset) Focar(0, Campos[0]);
    }

    /// <summary>Campo (TextBox ou AutoCompleteBox com Tag) que contém o elemento.</summary>
    private static (Control? Campo, string? Tag) CampoDe(object? origem)
    {
        for (var v = origem as Visual; v is not null; v = v.GetVisualParent())
            if (v is Control { Tag: string tag } c && (c is TextBox or AutoCompleteBox)) return (c, tag);
        return (null, null);
    }

    private void AoTeclar(object? sender, KeyEventArgs e)
    {
        if (_vm is null) return;
        var (campo, tag) = CampoDe(e.Source);
        if (campo?.DataContext is not LinhaLancamentoViewModel linha || tag is null) return;
        var i = _vm.Linhas.IndexOf(linha);
        if (i < 0) return;
        var listaAberta = campo is AutoCompleteBox { IsDropDownOpen: true };

        switch (e.Key)
        {
            case Key.Enter when e.KeyModifiers == KeyModifiers.None && !listaAberta:
                e.Handled = true;
                Avancar(linha, i, tag);
                break;
            case Key.Down when e.KeyModifiers == KeyModifiers.None && !listaAberta:
                e.Handled = true;
                if (i == _vm.Linhas.Count - 1)
                {
                    _vm.AdicionarLinha();
                    Focar(i + 1, Campos[0]);
                }
                else Focar(i + 1, tag);
                break;
            case Key.Up when e.KeyModifiers == KeyModifiers.None && !listaAberta:
                e.Handled = true;
                if (i == 0) break;
                _vm.CancelarLinhaNova(linha);
                Focar(i - 1, tag);
                break;
            case Key.Delete when e.KeyModifiers == KeyModifiers.Control:
                e.Handled = true;
                _vm.ExcluirLinhaCommand.Execute(null);
                Focar(Math.Min(i, _vm.Linhas.Count - 1), tag);
                break;
        }
    }

    /// <summary>Enter: próximo campo; no último, próxima linha (criada se preciso).</summary>
    private void Avancar(LinhaLancamentoViewModel linha, int i, string tag)
    {
        var campos = Campos;
        var p = Array.IndexOf(campos, tag) + 1;
        // Reduzido já identificou a conta: pula o campo da conta.
        if (tag == "reduzido" && linha.NomeConta.Length > 0 && linha.Reduzido.Trim().Length > 0) p++;
        if (tag == "reduzidocredito" && linha.NomeContaCredito.Length > 0 && linha.ReduzidoCredito.Trim().Length > 0) p++;
        if (p < campos.Length)
        {
            Focar(i, campos[p]);
            return;
        }
        if (i == _vm!.Linhas.Count - 1) _vm.AdicionarLinha();
        Focar(i + 1, campos[0]);
    }

    /// <summary>Foca o campo depois que a linha existir na tela.</summary>
    private void Focar(int linha, string tag) => Dispatcher.UIThread.Post(() =>
    {
        if (Linhas.ContainerFromIndex(linha) is not { } container) return;
        var alvo = container.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Tag as string == tag && (c is TextBox || c is AutoCompleteBox));
        if (alvo is AutoCompleteBox acb) alvo = acb.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() ?? alvo;
        alvo?.Focus(NavigationMethod.Tab);
        if (alvo is TextBox tb && tag != "historico") tb.SelectAll();
    }, DispatcherPriority.Loaded);

    private void AoEntrarNoCampo(object? sender, GotFocusEventArgs e)
    {
        var (campo, tag) = CampoDe(e.Source);
        if (_vm is null || campo?.DataContext is not LinhaLancamentoViewModel linha) return;
        _vm.LinhaSelecionada = linha;
        // Histórico: cursor no fim do texto fixo, para completar.
        if (tag == "historico" && campo is TextBox tb)
            Dispatcher.UIThread.Post(() => { tb.ClearSelection(); tb.CaretIndex = tb.Text?.Length ?? 0; });
    }

    private void AoSairDoCampo(object? sender, RoutedEventArgs e)
    {
        var (campo, tag) = CampoDe(e.Source);
        if (_vm is null || campo?.DataContext is not LinhaLancamentoViewModel linha) return;
        switch (tag)
        {
            case "valor": linha.FormatarValor(); break;
            case "conta" when campo is AutoCompleteBox { IsDropDownOpen: false }: _vm.ConfirmarConta(linha, credito: false); break;
            case "contacredito" when campo is AutoCompleteBox { IsDropDownOpen: false }: _vm.ConfirmarConta(linha, credito: true); break;
            case "data" when linha.Data.Trim().Length > 0: linha.Data = _vm.DataCompleta(linha.Data); break;
        }
    }
}
