using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Contabil;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Domain.Contabil;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra.Importacao;

namespace Vorticis.Desktop.ViewModels;

/// <summary>Uma conta da planilha, com a marca de importar; as que já estão no plano vêm desmarcadas.</summary>
public partial class ContaParaImportar(ContaImportada conta, bool existe) : ObservableObject
{
    public ContaImportada Conta { get; } = conta;
    public bool Existe { get; } = existe;
    public string Codigo => Conta.Codigo;
    public string Descricao => Conta.Descricao;
    public string Tipo => Conta.Analitica ? "Analítica" : "Sintética";
    public string Natureza => OpcaoNatureza.Todas.FirstOrDefault(n => n.Codigo == Conta.CodNat)?.Nome ?? Conta.CodNat;
    public string Situacao => Existe ? "Já está no plano" : "Nova";
    [ObservableProperty] private bool _marcada = !existe;
}

/// <summary>
/// Plano de contas › Importar contas do Excel (janela por cima da lista): lê a planilha (código e descrição; natureza, tipo e saldo
/// se houver), mostra as contas e acrescenta ao plano só as marcadas — sem substituir o plano e sem mexer nas que já existem.
/// </summary>
public partial class ImportarContasPlanoViewModel(IContexto contexto, Func<Task> aoConcluir) : ModuloViewModel(contexto), IComFicha
{
    public ObservableCollection<ContaParaImportar> Contas { get; } = [];
    public ObservableCollection<string> Problemas { get; } = [];

    [ObservableProperty] private bool _fichaAberta;
    [ObservableProperty] private string _arquivo = "";
    [ObservableProperty] private string _resumo = "";
    [ObservableProperty] private DateTime? _dataInclusao = new DateTime(DateTime.Today.Year, 1, 1);

    public string TituloFicha => "Importar contas do Excel";

    [RelayCommand]
    private void Abrir()
    {
        (Arquivo, Resumo, Mensagem) = ("", "", null);
        Contas.Clear();
        Problemas.Clear();
        Erros.Clear();
        FichaAberta = true;
    }

    [RelayCommand]
    public void FecharFicha() => FichaAberta = false;

    partial void OnArquivoChanged(string value)
    {
        if (File.Exists(value)) _ = LerAsync();
    }

    /// <summary>Lê a planilha e marca as contas que ainda não estão no plano.</summary>
    public async Task LerAsync()
    {
        Erros.Clear();
        Mensagem = null;
        Contas.Clear();
        Problemas.Clear();
        var t = LeitorPlanilha.Ler(Arquivo);
        if (!t.Sucesso) { MostrarResultado(t, ""); return; }
        var lido = LeitorPlanoContas.Ler(t.Valor, ColunasPlano.Detectar(t.Valor), null);
        var existentes = Contexto.App is { } app && Contexto.Sessao is { } s && Contexto.EmpresaId is { } e
            ? (await app.PlanoContas.ListarAsync(e, s.Id)).Select(c => c.Codigo).ToHashSet()
            : [];
        foreach (var c in lido.Contas)
        {
            var linha = new ContaParaImportar(c, existentes.Contains(c.Codigo));
            linha.PropertyChanged += (_, _) => AtualizarResumo();
            Contas.Add(linha);
        }
        // aqui a conta superior pode já estar no plano (só a planilha não a tem): isso não é problema
        foreach (var p in lido.Erros.Where(p => SuperiorAusente().Match(p) is not { Success: true } m || !existentes.Contains(m.Groups[1].Value))) Problemas.Add(p);
        AtualizarResumo();
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"conta superior (\S+) de ")]
    private static partial System.Text.RegularExpressions.Regex SuperiorAusente();

    private void AtualizarResumo()
    {
        var marcadas = Contas.Count(c => c.Marcada);
        Resumo = Contas.Count == 0 ? "" : $"{Contas.Count} conta(s) na planilha, {Contas.Count(c => c.Existe)} já no plano · {marcadas} marcada(s) para acrescentar";
        ImportarCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void MarcarNovas() { foreach (var c in Contas) c.Marcada = !c.Existe; }

    [RelayCommand]
    private void Desmarcar() { foreach (var c in Contas) c.Marcada = false; }

    private bool PodeImportar() => Contas.Any(c => c.Marcada);

    [RelayCommand(CanExecute = nameof(PodeImportar))]
    private Task ImportarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var data = DateOnly.FromDateTime(DataInclusao ?? new DateTime(DateTime.Today.Year, 1, 1));
        var contas = Contas.Where(c => c.Marcada)
                           .Select(c => new NovaConta(c.Conta.Codigo, c.Conta.Descricao, c.Conta.CodNat, c.Conta.NaturezaSaldo, false, false, data)).ToList();
        var r = await app.PlanoContas.AcrescentarAsync(empresa, contas, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var (incluidas, jaExistiam, problemas) = r.Valor;
        await aoConcluir();
        await LerAsync();
        foreach (var p in problemas) Problemas.Add(p);
        Mensagem = $"{incluidas} conta(s) acrescentada(s) ao plano" + (jaExistiam > 0 ? $"; {jaExistiam} já existia(m)" : "") +
                   (problemas.Count > 0 ? $"; {problemas.Count} não entraram (veja abaixo)." : ".");
    }, Permissao.PlanoContasManter);
}
