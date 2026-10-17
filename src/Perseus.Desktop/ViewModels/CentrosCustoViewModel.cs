using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Application.Contabil;
using Perseus.Domain.Seguranca;

namespace Perseus.Desktop.ViewModels;

public sealed record CentroLinha(CentroCustoDetalhe Centro, string? CodigoPai)
{
    public string Numero => Centro.Id.ToString("0000");
    public string Codigo => Centro.Codigo;
    public string Descricao => Centro.Descricao;
    public string Superior => CodigoPai ?? "";
    public string Situacao => (Centro.Ativo ? "Ativo" : "Inativo") + (Centro.TemMovimento ? " · com lançamentos" : "");
}

/// <summary>Cadastros › Centros de custo (Incluir / Alterar / Excluir).</summary>
public partial class CentrosCustoViewModel(IContexto contexto) : CadastroViewModel(contexto)
{
    public ObservableCollection<CentroLinha> Lista { get; } = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TemSelecionado))] private CentroLinha? _selecionado;
    [ObservableProperty] private string _codigo = "";
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private string _codigoPai = "";
    [ObservableProperty] private bool _ativo = true;

    public bool TemSelecionado => Selecionado is not null;
    public bool PodeEditar => Contexto.Pode(Permissao.CentroCustoManter);
    public override bool PodeManter => PodeEditar;
    protected override string NomeRegistro => "centro de custo";
    protected override string? DescricaoSelecionado => Selecionado is { } s ? $"{s.Codigo} – {s.Descricao}" : null;
    protected override string DetalheExclusao => "\n\nCentros com lançamentos não podem ser excluídos: use Alterar e desmarque Ativo.";

    public void AtualizarPermissoes()
    {
        OnPropertyChanged(nameof(PodeEditar));
        AtualizarBarra();
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var geracao = NovaGeracao();
        var centros = await app.PlanoContas.ListarCentrosAsync(empresa, sessao.Id);
        if (!EhAtual(geracao)) return;
        var id = Selecionado?.Centro.Id;
        var codigos = centros.ToDictionary(c => c.Id, c => c.Codigo);
        Lista.Clear();
        foreach (var c in centros) Lista.Add(new CentroLinha(c, c.PaiId is long p ? codigos.GetValueOrDefault(p) : null));
        Selecionado = Lista.FirstOrDefault(l => l.Centro.Id == id);
    }

    partial void OnSelecionadoChanged(CentroLinha? value)
    {
        if (EmConsulta) MostrarSelecionado();
        AtualizarBarra();
    }

    protected override void MostrarSelecionado()
    {
        if (Selecionado is { } s) (Codigo, Descricao, CodigoPai, Ativo) = (s.Codigo, s.Descricao, s.Superior, s.Centro.Ativo);
        else (Codigo, Descricao, CodigoPai, Ativo) = ("", "", "", true);
    }

    /// <summary>Novo centro: sugere como superior o centro selecionado.</summary>
    protected override void PrepararInclusao() => (Codigo, Descricao, CodigoPai, Ativo) = ("", "", Selecionado?.Codigo ?? "", true);

    protected override async Task<bool> GravarAsync(bool inclusao)
    {
        var ok = false;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            Domain.Comum.Resultado r;
            long? id = null;
            if (inclusao)
            {
                var inc = await app.PlanoContas.IncluirCentroAsync(empresa, Codigo, Descricao, CodigoPai, usuario);
                r = inc;
                if (inc.Sucesso) id = inc.Valor;
            }
            else
            {
                if (Selecionado is null) { Erros.Add("Selecione um centro de custo."); return; }
                id = Selecionado.Centro.Id;
                r = await app.PlanoContas.AlterarCentroAsync(empresa, Selecionado.Centro.Id, Descricao, Ativo, usuario);
            }
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            ok = true;
            Modo = ModoCadastro.Consulta;
            var codigo = Codigo.Trim();
            await CarregarAsync();
            Selecionado = Lista.FirstOrDefault(l => l.Centro.Id == id) ?? Lista.FirstOrDefault(l => l.Codigo == codigo);
            Mensagem = inclusao ? $"Centro de custo {codigo} incluído." : "Centro de custo alterado.";
        }, Permissao.CentroCustoManter);
        return ok;
    }

    protected override async Task<bool> ExcluirSelecionadoAsync()
    {
        var ok = false;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            if (Selecionado is null) return;
            var r = await app.PlanoContas.ExcluirCentroAsync(empresa, Selecionado.Centro.Id, usuario);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            ok = true;
            Selecionado = null;
            await CarregarAsync();
            Mensagem = "Centro de custo excluído.";
        }, Permissao.CentroCustoManter);
        return ok;
    }
}
