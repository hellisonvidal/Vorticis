using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Fiscal.Reforma;

namespace Vorticis.Fiscal.Desktop.ViewModels;

/// <summary>
/// Apuração › IBS e CBS (reforma tributária): débitos das saídas − créditos das entradas − saldo credor do mês anterior, com o Imposto Seletivo.
/// De 2027 em diante, Encerrar grava os saldos (o credor passa ao mês seguinte) e pode lançar a compensação na contabilidade; Reabrir desfaz.
/// </summary>
public partial class ApuracaoIbsCbsViewModel(IContexto contexto) : RelatorioFiscalViewModel(contexto)
{
    [ObservableProperty] private DateTime? _competencia = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private string _avisosTexto = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PodeEncerrar), nameof(PodeReabrir))] private ApuracaoReforma? _apuracao;
    /// <summary>Encerrar também lança na contabilidade a compensação dos créditos com os débitos.</summary>
    [ObservableProperty] private bool _contabilizar = true;
    private DateOnly _apurada;

    public bool PodeEncerrar => Apuracao is { Informativa: false, Encerrada: null };
    public bool PodeReabrir => Apuracao?.Encerrada is not null;

    protected override string TituloRelatorio => "Apuração de IBS e CBS";
    protected override string Parametros => $"Competência {_apurada:MM/yyyy}";
    protected override string NomeArquivo => $"ibs_cbs_{_apurada:yyyy_MM}";
    protected override bool Paisagem => false;

    private DateOnly Mes() => Competencia is { } d ? new DateOnly(d.Year, d.Month, 1) : new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);

    private void Exibir(ApuracaoReforma a, DateOnly c)
    {
        _apurada = c;
        Apuracao = a;
        Mostrar(a.Tabela());
        AvisosTexto = string.Join(Environment.NewLine, a.Avisos());
    }

    [RelayCommand]
    private Task ApurarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var c = Mes();
        var r = await app.Fiscal().Reforma.ApurarAsync(empresa, c, usuario);
        if (!r.Sucesso) { Limpar(); Apuracao = null; MostrarResultado(r, ""); return; }
        Exibir(r.Valor!, c);
    });

    [RelayCommand]
    private Task EncerrarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var c = Mes();
        if (!await Contexto.ConfirmarAsync("Encerrar a apuração", $"Encerrar a apuração do IBS e da CBS de {c:MM/yyyy}? O saldo credor passa para o mês seguinte"
                + (Contabilizar ? " e a compensação dos créditos com os débitos é lançada na contabilidade." : "."), "Encerrar", "Cancelar")) return;
        var r = await app.Fiscal().Reforma.EncerrarAsync(empresa, c, Contabilizar, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Exibir(r.Valor!, c);
        Mensagem = $"Apuração de {c:MM/yyyy} encerrada.";
    });

    [RelayCommand]
    private Task ReabrirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var c = Mes();
        if (!await Contexto.ConfirmarAsync("Reabrir a apuração", $"Reabrir a apuração de {c:MM/yyyy}? Se a compensação foi contabilizada, estorne o lançamento na contabilidade.",
                "Reabrir", "Cancelar")) return;
        var r = await app.Fiscal().Reforma.ReabrirAsync(empresa, c, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var a = await app.Fiscal().Reforma.ApurarAsync(empresa, c, usuario);
        if (a.Sucesso) Exibir(a.Valor!, c);
        Mensagem = $"Apuração de {c:MM/yyyy} reaberta.";
    });
}
