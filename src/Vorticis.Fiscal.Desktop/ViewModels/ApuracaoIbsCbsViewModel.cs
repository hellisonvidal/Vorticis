using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vorticis.Fiscal.Desktop.ViewModels;

/// <summary>Apuração › IBS e CBS (reforma tributária): débitos das saídas − créditos das entradas do mês, pelo grupo IBSCBS das NF-e importadas.</summary>
public partial class ApuracaoIbsCbsViewModel(IContexto contexto) : RelatorioFiscalViewModel(contexto)
{
    [ObservableProperty] private DateTime? _competencia = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private string _avisosTexto = "";
    private DateOnly _apurada;

    protected override string TituloRelatorio => "Apuração de IBS e CBS";
    protected override string Parametros => $"Competência {_apurada:MM/yyyy}";
    protected override string NomeArquivo => $"ibs_cbs_{_apurada:yyyy_MM}";
    protected override bool Paisagem => false;

    [RelayCommand]
    private Task ApurarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var c = Competencia is { } d ? new DateOnly(d.Year, d.Month, 1) : new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        var r = await app.Fiscal().Reforma.ApurarAsync(empresa, c, usuario);
        if (!r.Sucesso) { Limpar(); MostrarResultado(r, ""); return; }
        _apurada = c;
        Mostrar(r.Valor!.Tabela());
        AvisosTexto = string.Join(Environment.NewLine, r.Valor.Avisos());
    });
}
