using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Vorticis.Desktop.Comum.Views;

/// <summary>Rodapé da ficha em janela para as telas sem a barra padrão de cadastro: o comando de gravar é informado pela ficha.</summary>
public partial class RodapeFichaSimplesView : UserControl
{
    public static readonly StyledProperty<ICommand?> SalvarCommandProperty = AvaloniaProperty.Register<RodapeFichaSimplesView, ICommand?>(nameof(SalvarCommand));
    public static readonly StyledProperty<string> SalvarTextoProperty = AvaloniaProperty.Register<RodapeFichaSimplesView, string>(nameof(SalvarTexto), "Salvar");

    public RodapeFichaSimplesView() => InitializeComponent();

    /// <summary>Comando do botão Salvar (Gravar) da ficha.</summary>
    public ICommand? SalvarCommand { get => GetValue(SalvarCommandProperty); set => SetValue(SalvarCommandProperty, value); }

    /// <summary>Texto do botão (Salvar ou Gravar).</summary>
    public string SalvarTexto { get => GetValue(SalvarTextoProperty); set => SetValue(SalvarTextoProperty, value); }
}
