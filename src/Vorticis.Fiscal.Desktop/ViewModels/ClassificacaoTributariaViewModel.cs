using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Reforma;

namespace Vorticis.Fiscal.Desktop.ViewModels;

public sealed record ClassificacaoLinha(ClassificacaoTributaria C)
{
    public string Codigo => C.Codigo;
    public string Cst => C.Cst;
    public string Descricao => C.Descricao;
    public string Reducao => C.ReducaoPct == 0 ? "" : $"{C.ReducaoPct:0.##}%";
    public string Credito => C.GeraCredito ? "Sim" : "Não";
    public string Situacao => C.Ativo ? "Ativa" : "Inativa";
}

/// <summary>
/// Cadastros › Classificação tributária (IBS/CBS): os códigos cClassTrib da tabela oficial que o escritório usa, com o CST, a redução da alíquota e
/// se a aquisição gera crédito. A lançamento das notas usa a classificação para calcular o IBS e a CBS do item. A tabela vale para todas as empresas.
/// </summary>
public partial class ClassificacaoTributariaViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<ClassificacaoLinha> Lista { get; } = [];
    public ObservableCollection<CstReforma> Csts { get; } = [];

    [ObservableProperty] private ClassificacaoLinha? _selecionada;
    [ObservableProperty] private string _codigo = "";
    [ObservableProperty] private CstReforma? _cst;
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private string _reducao = "";
    [ObservableProperty] private bool _geraCredito = true;
    [ObservableProperty] private bool _ativo = true;

    public bool PodeEditar => Contexto.Pode(Permissao.FiscalApurar);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeEditar));

    partial void OnSelecionadaChanged(ClassificacaoLinha? value)
    {
        if (value?.C is not { } c) return;
        (Codigo, Descricao, Reducao, GeraCredito, Ativo) = (c.Codigo, c.Descricao, c.ReducaoPct == 0 ? "" : c.ReducaoPct.ToString("0.####"), c.GeraCredito, c.Ativo);
        Cst = Csts.FirstOrDefault(x => x.Codigo == c.Cst);
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app) return;
        var g = NovaGeracao();
        var lista = await app.Fiscal().Classificacoes.ListarAsync();
        var csts = await app.Fiscal().Classificacoes.CstsAsync();
        if (!EhAtual(g)) return;
        if (Csts.Count == 0) foreach (var c in csts) Csts.Add(c);
        var cod = Selecionada?.Codigo;
        Lista.Clear();
        foreach (var c in lista) Lista.Add(new ClassificacaoLinha(c));
        Selecionada = Lista.FirstOrDefault(l => l.Codigo == cod);
    }

    [RelayCommand]
    private void Nova()
    {
        Selecionada = null;
        (Codigo, Descricao, Reducao, GeraCredito, Ativo) = ("", "", "", true, true);
        Cst = Csts.FirstOrDefault();
        Mensagem = "Informe o código de 6 dígitos da tabela oficial (cClassTrib), o CST, a descrição, a redução da alíquota e se gera crédito.";
    }

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var c = new ClassificacaoTributaria(Codigo.Trim(), Cst?.Codigo ?? "", Descricao, Numeros.LerValor(Reducao) ?? 0, GeraCredito, Ativo);
        var r = await app.Fiscal().Classificacoes.GravarAsync(empresa, c, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Selecionada = Lista.FirstOrDefault(l => l.Codigo == c.Codigo);
        Mensagem = $"Classificação {c.Codigo} gravada.";
    }, Permissao.FiscalApurar);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada is not { } s) { Erros.Add("Selecione a classificação na lista."); return; }
        if (!await Contexto.ConfirmarAsync("Excluir classificação", $"Excluir a classificação {s.Codigo} – {s.Descricao}? As notas já lançadas não mudam.", "Excluir", "Cancelar")) return;
        var r = await app.Fiscal().Classificacoes.ExcluirAsync(empresa, s.Codigo, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Selecionada = null;
        await CarregarAsync();
        Mensagem = "Classificação excluída.";
    }, Permissao.FiscalApurar);
}
