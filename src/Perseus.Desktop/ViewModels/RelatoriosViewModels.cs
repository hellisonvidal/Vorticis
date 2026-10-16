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

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa) =>
    {
        var ini = Data(Inicio, DateOnly.FromDateTime(DateTime.Today));
        var fim = Data(Fim, ini);
        _ultimo = await app.Relatorios.BalanceteAsync(empresa, ini, fim, IncluirZeradas);
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
    });

    [RelayCommand]
    private Task ExportarCsvAsync() => ExecutarAsync(async (_, _) =>
    {
        if (_ultimo.Count == 0) { Erros.Add("Gere o balancete antes de exportar."); return; }
        var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Perseus");
        Directory.CreateDirectory(pasta);
        var arquivo = Path.Combine(pasta, $"balancete_{Inicio:yyyyMMdd}_{Fim:yyyyMMdd}.csv");
        var sb = new StringBuilder("Código;Descrição;Tipo;Saldo anterior;Débitos;Créditos;Saldo final\n");
        foreach (var l in _ultimo)
            sb.AppendLine(string.Join(';', l.Codigo, '"' + l.Descricao.Replace("\"", "\"\"") + '"', l.Analitica ? "A" : "S",
                Numeros.Moeda(l.SaldoAnterior), Numeros.Moeda(l.Debitos), Numeros.Moeda(l.Creditos), Numeros.Moeda(l.SaldoFinal)));
        await File.WriteAllTextAsync(arquivo, sb.ToString(), new UTF8Encoding(true));   // BOM: Excel abre acentos corretamente
        Mensagem = $"Exportado para {arquivo}";
    });
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
    private Task GerarAsync() => ExecutarAsync(async (app, empresa) =>
    {
        Linhas.Clear();
        Cabecalho = "";
        var ini = Data(Inicio, DateOnly.FromDateTime(DateTime.Today));
        var razao = await app.Relatorios.RazaoAsync(empresa, Conta.Trim(), ini, Data(Fim, ini));
        if (razao is null) { Erros.Add($"Conta {Conta} não encontrada."); return; }
        foreach (var l in razao.Linhas)
            Linhas.Add(new LinhaRazaoView(l.Data.ToString("dd/MM/yyyy"), l.Numero, l.Historico,
                l.DC == IndicadorDC.D ? Numeros.Moeda(l.Valor) : "", l.DC == IndicadorDC.C ? Numeros.Moeda(l.Valor) : "",
                LinhaBalancete.Formatar(l.SaldoAcumulado), l.Estornado));
        Cabecalho = $"{razao.Codigo} - {razao.Descricao} | Saldo anterior: {LinhaBalancete.Formatar(razao.SaldoAnterior)} | Saldo final: {LinhaBalancete.Formatar(razao.SaldoFinal)}";
    });
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

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa) =>
    {
        var ini = Data(Inicio, DateOnly.FromDateTime(DateTime.Today));
        var linhas = await app.Relatorios.DiarioAsync(empresa, ini, Data(Fim, ini));
        Linhas.Clear();
        foreach (var l in linhas)
            Linhas.Add(new LinhaDiarioView(l.LancamentoId, l.Numero, l.Data.ToString("dd/MM/yyyy"), $"{l.CodigoConta} {l.DescricaoConta}",
                l.DC == IndicadorDC.D ? Numeros.Moeda(l.Valor) : "", l.DC == IndicadorDC.C ? Numeros.Moeda(l.Valor) : "",
                l.Historico, l.Status == StatusLancamento.Estornado ? "Estornado" : ""));
    });

    [RelayCommand]
    private Task EstornarAsync() => ExecutarAsync(async (app, empresa) =>
    {
        if (Selecionada is null) { Erros.Add("Selecione uma linha do lançamento a estornar."); return; }
        var r = await app.Lancamentos.EstornarAsync(empresa, Selecionada.LancamentoId,
            Data(DataEstorno, DateOnly.FromDateTime(DateTime.Today)), MotivoEstorno, Contexto.Sessao?.Id);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var msg = $"Lançamento nº {Selecionada.Numero} estornado pelo lançamento nº {r.Valor.Numero}.";
        MotivoEstorno = "";
        await GerarAsync();
        Mensagem = msg;
    }, Permissao.Lancar);
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

    [RelayCommand]
    public async Task CarregarAsync()
    {
        Periodos.Clear();
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa) return;
        foreach (var p in await app.Periodos.ListarAsync(empresa, (int)(Ano ?? DateTime.Today.Year)))
            Periodos.Add(new PeriodoView(p.Competencia, p.Aberto));
    }

    [RelayCommand]
    private Task FecharAsync() => Alterar(fechar: true);

    [RelayCommand]
    private Task ReabrirAsync() => Alterar(fechar: false);

    private Task Alterar(bool fechar) => ExecutarAsync(async (app, empresa) =>
    {
        if (Selecionado is null) { Erros.Add("Selecione uma competência."); return; }
        var competencia = Selecionado.Competencia;
        var r = fechar
            ? await app.Periodos.FecharAsync(empresa, competencia, Contexto.Sessao?.Id)
            : await app.Periodos.ReabrirAsync(empresa, competencia, Contexto.Sessao?.Id);
        await CarregarAsync();
        MostrarResultado(r, $"Período {competencia:MM/yyyy} {(fechar ? "fechado" : "reaberto")}.");
    }, Permissao.FecharPeriodos);
}
