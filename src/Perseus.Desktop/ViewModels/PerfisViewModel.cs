using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Application.Seguranca;
using Perseus.Domain.Seguranca;

namespace Perseus.Desktop.ViewModels;

public partial class FuncaoMarcavel(DefinicaoPermissao definicao) : ObservableObject
{
    public DefinicaoPermissao Definicao { get; } = definicao;
    public string Descricao => Definicao.Descricao;
    [ObservableProperty] private bool _marcada;
}

public sealed record GrupoFuncoes(string Nome, IReadOnlyList<FuncaoMarcavel> Funcoes);

public sealed record PerfilLinha(PerfilAcesso Perfil)
{
    public string Nome => Perfil.Nome;
    public string Resumo => $"{Perfil.Permissoes.Count} função(ões) · {Perfil.Usuarios} usuário(s){(Perfil.Padrao ? " · padrão" : "")}";
}

/// <summary>Configurações › Perfis de acesso: o administrador define, função a função, o que cada perfil libera.</summary>
public partial class PerfisViewModel : ModuloViewModel
{
    private readonly IReadOnlyList<FuncaoMarcavel> _funcoes;

    public PerfisViewModel(IContexto contexto) : base(contexto)
    {
        _funcoes = Permissoes.Catalogo.Select(d => new FuncaoMarcavel(d)).ToArray();
        Grupos = _funcoes.GroupBy(f => f.Definicao.Grupo).Select(g => new GrupoFuncoes(g.Key, g.ToArray())).ToArray();
    }

    public ObservableCollection<PerfilLinha> Lista { get; } = [];
    public IReadOnlyList<GrupoFuncoes> Grupos { get; }

    [ObservableProperty] private PerfilLinha? _selecionado;
    [ObservableProperty] private long? _editandoId;
    [ObservableProperty] private string _nome = "";
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private string _situacao = "Novo perfil";

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao) return;
        var id = EditandoId;
        var geracao = NovaGeracao();
        var perfis = await app.Perfis.ListarAsync(sessao);
        if (!EhAtual(geracao)) return;
        Lista.Clear();
        foreach (var p in perfis) Lista.Add(new PerfilLinha(p));
        Selecionado = Lista.FirstOrDefault(l => l.Perfil.Id == id) ?? (id is null ? null : Lista.FirstOrDefault());
    }

    partial void OnSelecionadoChanged(PerfilLinha? value)
    {
        if (value is null) return;
        var p = value.Perfil;
        (EditandoId, Nome, Descricao) = (p.Id, p.Nome, p.Descricao ?? "");
        foreach (var f in _funcoes) f.Marcada = p.Permissoes.Contains(f.Definicao.Permissao);
        Situacao = p.Padrao ? "Perfil padrão do sistema: pode ser editado, mas não excluído."
                 : p.Usuarios > 0 ? $"Atribuído a {p.Usuarios} usuário(s). Alterações valem a partir da próxima ação deles."
                 : "Não atribuído a nenhum usuário.";
        Erros.Clear();
        Mensagem = null;
    }

    [RelayCommand]
    private void Novo()
    {
        Selecionado = null;
        (EditandoId, Nome, Descricao, Situacao) = (null, "", "", "Novo perfil");
        foreach (var f in _funcoes) f.Marcada = false;
    }

    [RelayCommand]
    private void Duplicar()
    {
        if (EditandoId is null) return;
        Selecionado = null;
        (EditandoId, Nome, Situacao) = (null, $"{Nome} (cópia)", "Cópia ainda não salva");
    }

    [RelayCommand]
    private void MarcarTodas(bool marcar)
    {
        foreach (var f in _funcoes) f.Marcada = marcar;
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Erros.Clear();
        Mensagem = null;
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao) return;
        var r = await app.Perfis.SalvarAsync(sessao, EditandoId, Nome, Descricao,
                                             _funcoes.Where(f => f.Marcada).Select(f => f.Definicao.Permissao));
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        EditandoId = r.Valor;
        await CarregarAsync();
        Mensagem = $"Perfil \"{Nome.Trim()}\" salvo.";
    }

    [RelayCommand]
    private async Task ExcluirAsync()
    {
        Erros.Clear();
        Mensagem = null;
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || EditandoId is not long id) return;
        var r = await app.Perfis.ExcluirAsync(sessao, id);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Novo();
        await CarregarAsync();
        Mensagem = "Perfil excluído.";
    }
}
