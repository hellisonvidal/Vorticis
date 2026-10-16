using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Application.Contabil;
using Perseus.Domain.Seguranca;

namespace Perseus.Desktop.ViewModels;

public sealed record CentroLinha(CentroCustoDetalhe Centro, string? CodigoPai)
{
    public string Codigo => Centro.Codigo;
    public string Descricao => Centro.Descricao;
    public string Superior => CodigoPai ?? "";
    public string Situacao => (Centro.Ativo ? "Ativo" : "Inativo") + (Centro.TemMovimento ? " · com lançamentos" : "");
}

/// <summary>Cadastros › Centros de custo.</summary>
public partial class CentrosCustoViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<CentroLinha> Lista { get; } = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TemSelecionado))] private CentroLinha? _selecionado;
    [ObservableProperty] private string _descricaoEdicao = "";
    [ObservableProperty] private bool _ativoEdicao = true;
    [ObservableProperty] private string _novoCodigo = "";
    [ObservableProperty] private string _novaDescricao = "";
    [ObservableProperty] private string _novoPai = "";

    public bool TemSelecionado => Selecionado is not null;
    public bool PodeEditar => Contexto.Pode(Permissao.CentroCustoManter);

    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeEditar));

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var geracao = NovaGeracao();
        var centros = await app.PlanoContas.ListarCentrosAsync(empresa, sessao.Id);
        if (!EhAtual(geracao)) return;
        var codigos = centros.ToDictionary(c => c.Id, c => c.Codigo);
        Lista.Clear();
        foreach (var c in centros) Lista.Add(new CentroLinha(c, c.PaiId is long p ? codigos.GetValueOrDefault(p) : null));
    }

    partial void OnSelecionadoChanged(CentroLinha? value)
    {
        if (value is null) return;
        (DescricaoEdicao, AtivoEdicao) = (value.Descricao, value.Centro.Ativo);
        NovoPai = value.Codigo;
    }

    [RelayCommand]
    private Task IncluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.PlanoContas.IncluirCentroAsync(empresa, NovoCodigo, NovaDescricao, NovoPai, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var codigo = NovoCodigo.Trim();
        (NovoCodigo, NovaDescricao) = ("", "");
        await CarregarAsync();
        Mensagem = $"Centro de custo {codigo} incluído.";
    }, Permissao.CentroCustoManter);

    [RelayCommand]
    private Task SalvarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionado is null) return;
        var r = await app.PlanoContas.AlterarCentroAsync(empresa, Selecionado.Centro.Id, DescricaoEdicao, AtivoEdicao, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = "Centro de custo alterado.";
    }, Permissao.CentroCustoManter);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionado is null) return;
        var r = await app.PlanoContas.ExcluirCentroAsync(empresa, Selecionado.Centro.Id, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Selecionado = null;
        await CarregarAsync();
        Mensagem = "Centro de custo excluído.";
    }, Permissao.CentroCustoManter);
}
