using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Vorticis.Desktop.Comum.ViewModels;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>
/// Ficha em janela: a tela mostra só a lista e o formulário do registro abre numa janela por cima dela enquanto o ViewModel
/// (<see cref="IComFicha"/>) estiver com a ficha aberta. Fechar a janela (X ou Esc) chama <see cref="IComFicha.FecharFicha"/>.
/// </summary>
public static class FichaPopup
{
    private sealed class Estado
    {
        public IComFicha? Vm;
        public Window? Janela;
        public bool FechandoPorCodigo;
    }

    private static readonly ConditionalWeakTable<UserControl, Estado> Estados = [];

    /// <summary>A janela da ficha aberta para a tela (null se fechada).</summary>
    public static Window? Aberta(UserControl tela) => Estados.TryGetValue(tela, out var e) ? e.Janela : null;

    /// <summary>
    /// Liga uma tela de lista (<see cref="CadastroViewModel"/> ou <see cref="ModuloComFichaViewModel"/>) à ficha em janela: além de <see cref="Ligar"/>, duplo clique ou Enter na
    /// lista abrem a ficha do registro selecionado.
    /// </summary>
    public static void LigarCadastro(UserControl tela, InputElement lista, Func<Control> criarConteudo, double largura = 680, double altura = 640)
    {
        Ligar(tela, criarConteudo, largura, altura);
        LigarAbertura(tela, lista);
    }

    /// <summary>Duplo clique ou Enter numa lista da tela abrem a ficha do registro selecionado (para telas com mais de uma lista).</summary>
    public static void LigarAbertura(UserControl tela, InputElement lista)
    {
        lista.DoubleTapped += (_, _) => ComandoAbrir(tela.DataContext)?.Execute(null);
        lista.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && ComandoAbrir(tela.DataContext) is { } cmd && cmd.CanExecute(null)) { cmd.Execute(null); e.Handled = true; }
        };
    }

    private static System.Windows.Input.ICommand? ComandoAbrir(object? vm) => vm switch
    {
        CadastroViewModel c => c.AbrirFichaCommand,
        ModuloComFichaViewModel m => m.AbrirFichaCommand,
        _ => null,
    };

    /// <summary>Liga a tela ao ViewModel: a janela abre e fecha com <see cref="IComFicha.FichaAberta"/>.</summary>
    public static void Ligar(UserControl tela, Func<Control> criarConteudo, double largura = 680, double altura = 640)
    {
        var estado = Estados.GetOrCreateValue(tela);
        void AoMudar(object? s, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IComFicha.FichaAberta)) Sincronizar(tela, estado, criarConteudo, largura, altura);
            else if (e.PropertyName == nameof(IComFicha.TituloFicha) && estado.Janela is { } j && estado.Vm is { } vm) j.Title = vm.TituloFicha;
        }
        tela.DataContextChanged += (_, _) =>
        {
            if (estado.Vm is { } antigo) antigo.PropertyChanged -= AoMudar;
            Fechar(estado);
            estado.Vm = tela.DataContext as IComFicha;
            if (estado.Vm is { } novo) novo.PropertyChanged += AoMudar;
            Sincronizar(tela, estado, criarConteudo, largura, altura);
        };
        tela.AttachedToVisualTree += (_, _) => Sincronizar(tela, estado, criarConteudo, largura, altura);
        tela.DetachedFromVisualTree += (_, _) => Fechar(estado);
    }

    private static void Sincronizar(UserControl tela, Estado estado, Func<Control> criarConteudo, double largura, double altura)
    {
        if (estado.Vm is not { FichaAberta: true } vm) { Fechar(estado); return; }
        if (estado.Janela is not null || TopLevel.GetTopLevel(tela) is not Window dono) return;
        var conteudo = criarConteudo();
        conteudo.DataContext = vm;
        var janela = new Window
        {
            Title = vm.TituloFicha,
            Content = conteudo,
            Width = Math.Min(largura, Math.Max(400, dono.Bounds.Width - 40)),
            Height = Math.Min(altura, Math.Max(300, dono.Bounds.Height - 40)),
            MinWidth = 420,
            MinHeight = 320,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Classes = { "ficha" },
        };
        janela.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && !e.Handled) { e.Handled = true; janela.Close(); }
        };
        janela.Closed += (_, _) =>
        {
            var porCodigo = estado.FechandoPorCodigo;
            (estado.Janela, estado.FechandoPorCodigo) = (null, false);
            if (!porCodigo && vm.FichaAberta) vm.FecharFicha();
        };
        estado.Janela = janela;
        _ = janela.ShowDialog(dono);
    }

    private static void Fechar(Estado estado)
    {
        if (estado.Janela is not { } j) return;
        estado.FechandoPorCodigo = true;
        j.Close();
    }
}
