using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Contabil;
using Vorticis.Application.Relatorios;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Desktop.ViewModels;

public partial class EmpresaDoGrupo(long id, string nome, bool marcada) : ObservableObject
{
    public long Id { get; } = id;
    public string Nome { get; } = nome;
    [ObservableProperty] private bool _marcada = marcada;
}

/// <summary>Relatórios › Contábeis › Balancete consolidado: grupo de empresas com o mesmo plano, com eliminações.</summary>
public partial class ConsolidadoViewModel(IContexto contexto) : RelatorioViewModel(contexto)
{
    public ObservableCollection<EmpresaDoGrupo> Empresas { get; } = [];
    [ObservableProperty] private DateTime? _data = DateTime.Today;
    [ObservableProperty] private string _eliminar = "";
    [ObservableProperty] private TabelaRelatorio? _tabela;
    private DateOnly _dataGerada;

    protected override string TituloRelatorio => "Balancete consolidado";
    protected override string NomeArquivo => $"balancete_consolidado_{_dataGerada:yyyyMMdd}";
    protected override string Parametros => $"Data-base {_dataGerada:dd/MM/yyyy}";
    protected override bool Paisagem => true;
    protected override IReadOnlyList<TabelaRelatorio> Tabelas() => Tabela is null ? [] : [Tabela];

    public void Limpar() { Tabela = null; TemDados = false; }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao) return;
        var marcadas = Empresas.Where(e => e.Marcada).Select(e => e.Id).ToHashSet();
        var lista = await app.Usuarios.EmpresasPermitidasAsync(sessao);
        Empresas.Clear();
        foreach (var e in lista) Empresas.Add(new EmpresaDoGrupo(e.Id, e.RazaoSocial, marcadas.Contains(e.Id) || e.Id == Contexto.EmpresaId));
    }

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var escolhidas = Empresas.Where(e => e.Marcada).ToList();
        var data = DateOnly.FromDateTime(Data ?? DateTime.Today);
        var contas = Eliminar.Split([';', ',', ' ', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var r = await app.Relatorios.ConsolidadoAsync(escolhidas.Select(e => e.Id).ToList(), data, contas, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        _dataGerada = data;
        Tabela = Consolidacao.Tabela(r.Valor, escolhidas.Select(e => e.Nome.Length > 22 ? e.Nome[..22] : e.Nome).ToList(), data);
        TemDados = r.Valor.Count > 0;
        AtualizarPermissoesExportacao();
        Mensagem = TemDados ? "Escolha o formato em Exportar: PDF, Excel (.xlsx) ou CSV." : "Nenhum saldo nas empresas escolhidas.";
    }, Permissao.RelatorioBalancete);
}
