using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Desktop.Comum.Modulos;

namespace Vorticis.Desktop.Comum.ViewModels;

public sealed class DicaLinha(Dica dica, bool podeAbrir)
{
    public Dica Dica { get; } = dica;
    public string Area => Dica.Area;
    public string Titulo => Dica.Titulo;
    public string Texto => Dica.Texto;
    public bool PodeAbrir { get; } = podeAbrir;
}

/// <summary>
/// Ajuda › Dicas: as dicas gerais e as do módulo aberto (contábeis no Contábil, fiscais no Fiscal...), com a dica do dia, busca por texto,
/// filtro por área e o botão Abrir a função para ir direto à tela de que a dica fala.
/// </summary>
public partial class DicasViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public const string TodasAreas = "Todas as áreas";

    private IReadOnlyList<Dica> _todas = [];
    public ObservableCollection<DicaLinha> Lista { get; } = [];
    public ObservableCollection<string> Areas { get; } = [];

    [ObservableProperty] private string _area = TodasAreas;
    [ObservableProperty] private string _busca = "";
    [ObservableProperty] private DicaLinha? _doDia;

    /// <summary>Dicas do módulo antes das gerais (no módulo, o que é do módulo vem primeiro).</summary>
    public static IReadOnlyList<Dica> DoModulo(IModuloDesktop modulo) => [.. modulo.Dicas, .. DicasGerais.Todas];

    public void Carregar()
    {
        _todas = Contexto is MainViewModelBase m ? DoModulo(m.Modulo) : DicasGerais.Todas;
        Areas.Clear();
        Areas.Add(TodasAreas);
        foreach (var a in _todas.Select(d => d.Area).Distinct()) Areas.Add(a);
        if (!Areas.Contains(Area)) Area = TodasAreas;
        var dia = DateTime.Today.DayOfYear;
        DoDia = _todas.Count == 0 ? null : Linha(_todas[dia % _todas.Count]);
        Filtrar();
    }

    private DicaLinha Linha(Dica d) => new(d, d.Tela is { } t && Contexto is MainViewModelBase m && m.ConheceTela(t));

    partial void OnAreaChanged(string value) => Filtrar();
    partial void OnBuscaChanged(string value) => Filtrar();

    private void Filtrar()
    {
        var termo = SuiteViewModel.Normalizar(Busca ?? "");
        Lista.Clear();
        foreach (var d in _todas.Where(d => (Area == TodasAreas || d.Area == Area)
                                             && (termo.Length == 0 || SuiteViewModel.Normalizar(d.Titulo + " " + d.Texto).Contains(termo, StringComparison.Ordinal))))
            Lista.Add(Linha(d));
    }

    [RelayCommand]
    private void OutraDica()
    {
        if (_todas.Count == 0) return;
        var i = DoDia is null ? 0 : _todas.ToList().IndexOf(DoDia.Dica);
        DoDia = Linha(_todas[(i + 1) % _todas.Count]);
    }

    [RelayCommand]
    private void Abrir(DicaLinha? d)
    {
        if (d?.Dica.Tela is { } t) Contexto.AbrirTela(t);
    }
}
