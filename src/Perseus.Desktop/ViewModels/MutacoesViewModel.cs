using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Application.Relatorios;
using Perseus.Domain.Contabil;
using Perseus.Domain.Seguranca;

namespace Perseus.Desktop.ViewModels;

/// <summary>Linha da DMPL na tela: um valor formatado por coluna do PL e o total (última posição).</summary>
public sealed record LinhaMutacaoView(string Descricao, string[] Valores, bool Saldo);

public sealed record LinhaDlpaView(string Descricao, string Valor, bool Destaque);

/// <summary>Relatórios › DMPL e DLPA (modelos do CFC): mutações do patrimônio líquido e lucros ou prejuízos acumulados.</summary>
public partial class MutacoesViewModel(IContexto contexto) : RelatorioViewModel(contexto)
{
    private DemonstracaoMutacoes? _ultima;

    public ObservableCollection<LinhaMutacaoView> Dmpl { get; } = [];
    public ObservableCollection<LinhaDlpaView> Dlpa { get; } = [];
    /// <summary>Cabeçalhos das colunas da DMPL (as do PL que a empresa tem, mais o total).</summary>
    [ObservableProperty] private IReadOnlyList<string> _colunasDmpl = [];

    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private DateTime? _fim = new DateTime(DateTime.Today.Year, 12, 31);
    [ObservableProperty] private string _resumo = "";

    protected override string TituloRelatorio => "Demonstração das Mutações do Patrimônio Líquido e Demonstração de Lucros ou Prejuízos Acumulados";
    protected override string Parametros => _ultima is { } d ? $"Período: {d.Inicio:dd/MM/yyyy} a {d.Fim:dd/MM/yyyy}" : "";
    protected override string NomeArquivo => _ultima is { } d ? $"dmpl_dlpa_{d.Inicio:yyyyMMdd}_{d.Fim:yyyyMMdd}" : "dmpl_dlpa";
    protected override bool Paisagem => true;

    protected override IReadOnlyList<TabelaRelatorio> Tabelas()
    {
        if (_ultima is not { } d) return [];
        var colunas = new List<ColunaRelatorio> { new("Descrição", LarguraCm: 7) };
        colunas.AddRange(d.Colunas.Select(c => new ColunaRelatorio(DemonstracaoMutacoes.NomeColuna(c), TipoColuna.Valor)));
        colunas.Add(new("Total", TipoColuna.Valor));
        return
        [
            new("DMPL — Demonstração das Mutações do Patrimônio Líquido", colunas,
                d.Dmpl.Select(l => new LinhaRelatorio(
                    new object?[] { l.Descricao }.Concat(d.Colunas.Select(c => (object?)l.Valor(c))).Append(l.Total).ToList(), Destaque: l.Saldo)).ToList(),
                Resumo),
            new("DLPA — Demonstração de Lucros ou Prejuízos Acumulados", [new("Descrição"), new("R$", TipoColuna.Valor)],
                d.Dlpa.Select(l => new LinhaRelatorio([l.Descricao, l.Descricao == "Destinações:" ? null : l.Valor], Destaque: l.Destaque)).ToList()),
        ];
    }

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var ini = Data(Inicio, new DateOnly(DateTime.Today.Year, 1, 1));
        var r = await app.Demonstracoes.MutacoesAsync(empresa, ini, Data(Fim, ini), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var d = _ultima = r.Valor;
        Dmpl.Clear();
        foreach (var l in d.Dmpl)
            Dmpl.Add(new LinhaMutacaoView(l.Descricao, d.Colunas.Select(c => Numeros.MoedaOuVazio(l.Valor(c))).Append(Numeros.Moeda(l.Total)).ToArray(), l.Saldo));
        ColunasDmpl = d.Colunas.Select(DemonstracaoMutacoes.NomeColuna).Append("Total").ToList();
        Dlpa.Clear();
        foreach (var l in d.Dlpa)
            Dlpa.Add(new LinhaDlpaView(l.Descricao, l.Descricao == "Destinações:" ? "" : Numeros.Moeda(l.Valor), l.Destaque));
        Resumo = $"Patrimônio líquido: {Numeros.Moeda(d.Dmpl[0].Total)} → {Numeros.Moeda(d.Dmpl[^1].Total)}";
        TemDados = true;
        AtualizarPermissoesExportacao();
    }, Permissao.RelatorioDemonstracoes);
}
