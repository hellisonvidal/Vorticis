using Avalonia.Controls;
using Avalonia.Interactivity;
using Vorticis.Desktop.Comum;
using Vorticis.Fiscal.Desktop.ViewModels;

namespace Vorticis.Fiscal.Desktop.Views;

/// <summary>Conteúdo do pop-up de digitação da nota: abas Nota, Itens, Tributos, Parcelas e Escrituração.</summary>
public partial class NotaDigitacaoView : UserControl
{
    /// <summary>Quem hospeda o pop-up trata o fechamento (com a pergunta de descartar); sem hospedeiro, só cancela a digitação.</summary>
    public event Action? FecharSolicitado;

    public NotaDigitacaoView() => InitializeComponent();

    private async void ImportarXml(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DigitacaoDocumentoViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
        try
        {
            if (await SelecaoXml.UmArquivoAsync(top) is { } caminho) await vm.ImportarArquivoParaConferenciaAsync(caminho);
        }
        catch (Exception ex) { vm.Erros.Add(ErrosInesperados.Registrar(ex)); }   // nunca fecha o módulo
    }

    /// <summary>Ao sair do campo do fornecedor/cliente, busca pelo CPF/CNPJ digitado (CNPJ completo já busca ao digitar).</summary>
    private void ParticipanteSaiu(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DigitacaoDocumentoViewModel vm && vm.BuscarParticipanteCommand.CanExecute(null)) vm.BuscarParticipanteCommand.Execute(null);
    }

    /// <summary>
    /// A mesma lista de itens aparece em mais de uma aba. Só a grade visível escolhe o item: as das abas escondidas não devolvem "nenhum item"
    /// ao ViewModel quando a lista é recarregada (a seleção inválida é limpa pelo próprio ViewModel).
    /// </summary>
    private void AoSelecionarItem(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid { IsEffectivelyVisible: true } g && g.SelectedItem is ItemNotaLinha linha && DataContext is DigitacaoDocumentoViewModel vm
            && !ReferenceEquals(vm.ItemSelecionado, linha))
            vm.ItemSelecionado = linha;
    }

    private void Fechar(object? sender, RoutedEventArgs e)
    {
        if (FecharSolicitado is { } f) f();
        else (DataContext as DigitacaoDocumentoViewModel)?.CancelarDigitacao();
    }
}
