using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Vorticis.Desktop.Comum;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Desktop.Comum.Views;
using Vorticis.Fiscal.Desktop.ViewModels;

namespace Vorticis.Fiscal.Desktop.Views;

/// <summary>
/// Notas de entrada/saída: grade das notas lançadas em tela cheia. "Novo lançamento", Editar, Consultar e o duplo clique (ou Enter) abrem a
/// digitação numa janela pop-up modal (<see cref="NotaDigitacaoView"/>); ao gravar, o pop-up fecha e a grade se atualiza.
/// </summary>
public partial class DigitacaoDocumentoView : UserControl
{
    private DigitacaoDocumentoViewModel? _vm;
    private Window? _popup;

    /// <summary>Janela pop-up da digitação, enquanto aberta.</summary>
    public Window? PopupAberto => _popup;

    public DigitacaoDocumentoView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= AoMudarVm;
            _vm = DataContext as DigitacaoDocumentoViewModel;
            if (_vm is not null) { _vm.PropertyChanged += AoMudarVm; if (_vm.DigitacaoAberta) AbrirPopup(); }
        };
    }

    private void AoMudarVm(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DigitacaoDocumentoViewModel.DigitacaoAberta) || _vm is null) return;
        if (_vm.DigitacaoAberta) AbrirPopup(); else FecharPopup();
    }

    private void AbrirPopup()
    {
        if (_popup is not null || _vm is null || TopLevel.GetTopLevel(this) is not Window dono) return;
        var largura = Math.Min(1280, Math.Max(900, dono.Bounds.Width - 24));
        var altura = Math.Min(700, Math.Max(560, dono.Bounds.Height - 24));
        var conteudo = new NotaDigitacaoView { DataContext = _vm };
        var popup = new Window
        {
            Title = _vm.TituloPopup,
            Width = largura, Height = altura, MinWidth = 900, MinHeight = 560,
            CanResize = true,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = conteudo,
            Icon = dono.Icon,
        };
        var principal = dono.Owner as MainWindow;
        popup.Closed += (_, _) =>
        {
            _popup = null;
            if (principal is not null && ReferenceEquals(principal.ModalAberta, popup)) principal.ModalAberta = null;
            if (_vm?.DigitacaoAberta == true) _vm.CancelarDigitacao();   // fechou pelo X/Alt+F4: descarta
            Dispatcher.UIThread.Post(() => Grade.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
        };
        async void Fechar()
        {
            if (_vm is { TemDados: true } && Contexto(principal) is { } ctx
                && !await ctx.ConfirmarAsync("Fechar a nota", "A nota digitada ainda não foi gravada. Deseja descartá-la e fechar?", "Descartar e fechar", "Continuar editando"))
                return;
            popup.Close();
        }
        conteudo.FecharSolicitado += Fechar;
        // Esc fecha o pop-up; com digitação não gravada, pergunta antes.
        popup.KeyDown += (_, e) => MainWindow.TratarEsc(e, () => { Fechar(); return null; });
        _popup = popup;
        if (principal is not null) principal.ModalAberta = popup;
        _ = popup.ShowDialog(dono);
    }

    private static MainViewModelBase? Contexto(MainWindow? principal) => principal?.DataContext as MainViewModelBase;

    private void FecharPopup() => _popup?.Close();

    private async void AoDuploClique(object? sender, TappedEventArgs e)
    {
        if (_vm is null || (e.Source as Control)?.DataContext is not DocumentoLinha) return;   // duplo clique no cabeçalho ou no vazio não abre
        if (_vm.EditarCommand.CanExecute(null)) await _vm.EditarCommand.ExecuteAsync(null);
    }

    private async void AoTeclarGrade(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _vm?.Selecionada is null) return;
        e.Handled = true;
        if (_vm.EditarCommand.CanExecute(null)) await _vm.EditarCommand.ExecuteAsync(null);
    }

    private async void ImportarArquivos(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DigitacaoDocumentoViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
        try
        {
            var caminhos = await SelecaoXml.ArquivosAsync(top);
            if (caminhos.Count > 0) await vm.Importador.ImportarArquivosAsync(caminhos);
        }
        catch (Exception ex) { vm.Importador.Erros.Add(ErrosInesperados.Registrar(ex)); }   // nunca fecha o módulo
    }

    private async void ImportarPasta(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DigitacaoDocumentoViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
        try { if (await SelecaoXml.PastaAsync(top) is { } pasta) await vm.Importador.ImportarPastaAsync(pasta, true); }
        catch (Exception ex) { vm.Importador.Erros.Add(ErrosInesperados.Registrar(ex)); }
    }
}
