using Avalonia.Controls;
using Vorticis.Desktop.Comum.Views;

namespace Vorticis.Desktop.Views;

/// <summary>Ponto da tela Plano de contas que abre a janela "Importar contas do Excel" (o DataContext é o ImportarContasPlanoViewModel).</summary>
public sealed class ImportarContasHost : UserControl
{
    public ImportarContasHost() => FichaPopup.Ligar(this, () => new ImportarContasView(), 900, 640);
}
