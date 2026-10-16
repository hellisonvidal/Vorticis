using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Domain.Contabil;
using Perseus.Domain.Seguranca;

namespace Perseus.Desktop.ViewModels;

public sealed record LinhaBalanceteView(string Codigo, string Descricao, bool Analitica, string SaldoAnterior, string Debitos,
                                        string Creditos, string SaldoFinal, bool SaldoInvertido)
{
    public string CodigoIndentado { get; init; } = Codigo;
    public string Alerta => SaldoInvertido ? "⚠" : "";
}

public partial class BalanceteViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    private IReadOnlyList<LinhaBalancete> _ultimo = [];

    public ObservableCollection<LinhaBalanceteView> Linhas { get; } = [];

    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _fim = DateTime.Today;
    [ObservableProperty] private bool _incluirZeradas;
    [ObservableProperty] private string _totais = "";

    public bool PodeExportar => Contexto.Pode(Permissao.RelatorioExportar);

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var ini = Data(Inicio, DateOnly.FromDateTime(DateTime.Today));
        var r = await app.Relatorios.BalanceteAsync(empresa, ini, Data(Fim, ini), usuario, IncluirZeradas);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        _ultimo = r.Valor;
        Linhas.Clear();
        foreach (var l in _ultimo)
            Linhas.Add(new LinhaBalanceteView(l.Codigo, l.Descricao, l.Analitica, LinhaBalancete.Formatar(l.SaldoAnterior),
                Numeros.MoedaOuVazio(l.Debitos), Numeros.MoedaOuVazio(l.Creditos), LinhaBalancete.Formatar(l.SaldoFinal), l.SaldoInvertido)
            { CodigoIndentado = new string(' ', (l.Nivel - 1) * 3) + l.Codigo });

        var analiticas = _ultimo.Where(l => l.Analitica).ToArray();
        var deb = analiticas.Sum(l => l.Debitos);
        var cred = analiticas.Sum(l => l.Creditos);
        Totais = $"Movimento: débitos {Numeros.Moeda(deb)} | créditos {Numeros.Moeda(cred)}" + (deb == cred ? " ✓ fechado" : " ✗ DIFERENÇA");
        var invertidas = _ultimo.Count(l => l.Analitica && l.SaldoInvertido);
        Mensagem = invertidas > 0 ? $"{invertidas} conta(s) com saldo contrário à natureza (destacadas)." : null;
        OnPropertyChanged(nameof(PodeExportar));
    }, Permissao.RelatorioBalancete);

    [RelayCommand]
    private Task ExportarCsvAsync() => ExecutarAsync(async (_, _, _) =>
    {
        if (_ultimo.Count == 0) { Erros.Add("Gere o balancete antes de exportar."); return; }
        var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Perseus Contabil");
        Directory.CreateDirectory(pasta);
        var arquivo = Path.Combine(pasta, $"balancete_{Inicio:yyyyMMdd}_{Fim:yyyyMMdd}.csv");
        var sb = new StringBuilder("Código;Descrição;Tipo;Saldo anterior;Débitos;Créditos;Saldo final\n");
        foreach (var l in _ultimo)
            sb.AppendLine(string.Join(';', l.Codigo, '"' + l.Descricao.Replace("\"", "\"\"") + '"', l.Analitica ? "A" : "S",
                Numeros.Moeda(l.SaldoAnterior), Numeros.Moeda(l.Debitos), Numeros.Moeda(l.Creditos), Numeros.Moeda(l.SaldoFinal)));
        await File.WriteAllTextAsync(arquivo, sb.ToString(), new UTF8Encoding(true));   // BOM: Excel abre acentos corretamente
        Mensagem = $"Exportado para {arquivo}";
    }, Permissao.RelatorioExportar);
}

public sealed record LinhaRazaoView(string Data, long Numero, string Historico, string Debito, string Credito, string Saldo, bool Estornado)
{
    public string Situacao => Estornado ? "Estornado" : "";
}

public partial class RazaoViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<LinhaRazaoView> Linhas { get; } = [];

    [ObservableProperty] private string _conta = "";
    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private DateTime? _fim = DateTime.Today;
    [ObservableProperty] private string _cabecalho = "";

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        Linhas.Clear();
        Cabecalho = "";
        var ini = Data(Inicio, DateOnly.FromDateTime(DateTime.Today));
        var r = await app.Relatorios.RazaoAsync(empresa, Conta.Trim(), ini, Data(Fim, ini), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var razao = r.Valor;
        foreach (var l in razao.Linhas)
            Linhas.Add(new LinhaRazaoView(l.Data.ToString("dd/MM/yyyy"), l.Numero, l.Historico,
                l.DC == IndicadorDC.D ? Numeros.Moeda(l.Valor) : "", l.DC == IndicadorDC.C ? Numeros.Moeda(l.Valor) : "",
                LinhaBalancete.Formatar(l.SaldoAcumulado), l.Estornado));
        Cabecalho = $"{razao.Codigo} - {razao.Descricao} | Saldo anterior: {LinhaBalancete.Formatar(razao.SaldoAnterior)} | Saldo final: {LinhaBalancete.Formatar(razao.SaldoFinal)}";
    }, Permissao.RelatorioRazao);
}

public sealed record LinhaDiarioView(long LancamentoId, long Numero, string Data, string Conta, string Debito, string Credito,
                                     string Historico, string Situacao);

public partial class DiarioViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<LinhaDiarioView> Linhas { get; } = [];

    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _fim = DateTime.Today;
    [ObservableProperty] private LinhaDiarioView? _selecionada;
    [ObservableProperty] private DateTime? _dataEstorno = DateTime.Today;
    [ObservableProperty] private string _motivoEstorno = "";

    /// <summary>O painel de estorno só aparece para quem pode estornar.</summary>
    public bool PodeEstornar => Contexto.Pode(Permissao.LancamentoEstornar);

    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeEstornar));

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var ini = Data(Inicio, DateOnly.FromDateTime(DateTime.Today));
        var r = await app.Relatorios.DiarioAsync(empresa, ini, Data(Fim, ini), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Linhas.Clear();
        foreach (var l in r.Valor)
            Linhas.Add(new LinhaDiarioView(l.LancamentoId, l.Numero, l.Data.ToString("dd/MM/yyyy"), $"{l.CodigoConta} {l.DescricaoConta}",
                l.DC == IndicadorDC.D ? Numeros.Moeda(l.Valor) : "", l.DC == IndicadorDC.C ? Numeros.Moeda(l.Valor) : "",
                l.Historico, l.Status == StatusLancamento.Estornado ? "Estornado" : ""));
    });

    [RelayCommand]
    private Task EstornarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada is null) { Erros.Add("Selecione uma linha do lançamento a estornar."); return; }
        var r = await app.Lancamentos.EstornarAsync(empresa, Selecionada.LancamentoId,
            Data(DataEstorno, DateOnly.FromDateTime(DateTime.Today)), MotivoEstorno, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var msg = $"Lançamento nº {Selecionada.Numero} estornado pelo lançamento nº {r.Valor.Numero}.";
        MotivoEstorno = "";
        await GerarAsync();
        Mensagem = msg;
    }, Permissao.LancamentoEstornar);
}

public sealed record PeriodoView(DateOnly Competencia, bool Aberto)
{
    public string Rotulo => $"{Competencia:MM/yyyy}  —  {(Aberto ? "Aberto" : "Fechado")}";
}

public partial class PeriodosViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<PeriodoView> Periodos { get; } = [];

    [ObservableProperty] private decimal? _ano = DateTime.Today.Year;
    [ObservableProperty] private PeriodoView? _selecionado;

    public bool PodeFechar => Contexto.Pode(Permissao.PeriodoFechar);
    public bool PodeReabrir => Contexto.Pode(Permissao.PeriodoReabrir);

    [RelayCommand]
    public async Task CarregarAsync()
    {
        OnPropertyChanged(nameof(PodeFechar));
        OnPropertyChanged(nameof(PodeReabrir));
        var geracao = NovaGeracao();
        IReadOnlyList<Application.Contabil.CompetenciaStatus> lista = [];
        if (Contexto.App is { } app && Contexto.EmpresaId is { } empresa && Contexto.Sessao is { } sessao)
            lista = await app.Periodos.ListarAsync(empresa, (int)(Ano ?? DateTime.Today.Year), sessao.Id);
        if (!EhAtual(geracao)) return;
        Periodos.Clear();
        foreach (var p in lista) Periodos.Add(new PeriodoView(p.Competencia, p.Aberto));
    }

    [RelayCommand]
    private Task FecharAsync() => Alterar(fechar: true);

    [RelayCommand]
    private Task ReabrirAsync() => Alterar(fechar: false);

    private Task Alterar(bool fechar) => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionado is null) { Erros.Add("Selecione uma competência."); return; }
        var competencia = Selecionado.Competencia;
        var r = fechar
            ? await app.Periodos.FecharAsync(empresa, competencia, usuario)
            : await app.Periodos.ReabrirAsync(empresa, competencia, usuario);
        await CarregarAsync();
        MostrarResultado(r, $"Período {competencia:MM/yyyy} {(fechar ? "fechado" : "reaberto")}.");
    }, fechar ? Permissao.PeriodoFechar : Permissao.PeriodoReabrir);
}
