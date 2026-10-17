using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Perseus.Application.Cadastros;
using Perseus.Domain.Seguranca;

namespace Perseus.Desktop.ViewModels;

public sealed record ParticipanteLinha(Participante P)
{
    public string Codigo => P.Codigo;
    public string Nome => P.Nome;
    public string Documento => P.CpfCnpj switch
    {
        { Length: 11 } d => Domain.Cadastros.Cpf.Formatar(d),
        { Length: 14 } d => Domain.Cadastros.Cnpj.Formatar(d),
        var d => d ?? "",
    };
    public string Cidade => string.Join("/", new[] { P.Municipio, P.Uf }.Where(s => !string.IsNullOrEmpty(s)));
    public string Tipo => P.Cliente && P.Fornecedor ? "Cliente e fornecedor" : P.Cliente ? "Cliente" : "Fornecedor";
    public string Situacao => P.Ativo ? "" : "Inativo";
}

/// <summary>Cadastros › Clientes e fornecedores, em abas (a mesma pessoa pode estar nas duas).</summary>
public partial class ParticipantesViewModel(IContexto contexto) : CadastroViewModel(contexto)
{
    public const int AbaClientes = 0;
    public const int AbaFornecedores = 1;
    private IReadOnlyList<Participante> _todos = [];

    public ObservableCollection<ParticipanteLinha> Lista { get; } = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TituloLista))] private int _abaSelecionada;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TemSelecionado))] private ParticipanteLinha? _selecionado;
    [ObservableProperty] private string _filtro = "";

    [ObservableProperty] private string _codigo = "";
    [ObservableProperty] private string _nome = "";
    [ObservableProperty] private string _nomeFantasia = "";
    [ObservableProperty] private string _cpfCnpj = "";
    [ObservableProperty] private string _inscricaoEstadual = "";
    [ObservableProperty] private string _uf = "";
    [ObservableProperty] private string _municipioIbge = "";
    [ObservableProperty] private string _municipio = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _telefone = "";
    [ObservableProperty] private bool _cliente = true;
    [ObservableProperty] private bool _fornecedor;
    [ObservableProperty] private string _conta = "";
    [ObservableProperty] private bool _ativo = true;

    public string TituloLista => AbaSelecionada == AbaClientes ? "Clientes" : "Fornecedores";
    public bool TemSelecionado => Selecionado is not null;
    public bool PodeEditar => Contexto.Pode(Permissao.LancamentoIncluir);
    public override bool PodeManter => PodeEditar;
    protected override string NomeRegistro => AbaSelecionada == AbaClientes ? "cliente" : "fornecedor";
    protected override string? DescricaoSelecionado => Selecionado is { } s ? $"{s.Codigo} – {s.Nome}" : null;
    protected override string DetalheExclusao => "\n\nQuem já aparece em lançamentos não é excluído: use Alterar e desmarque Ativo.";

    public void AtualizarPermissoes()
    {
        OnPropertyChanged(nameof(PodeEditar));
        AtualizarBarra();
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var g = NovaGeracao();
        var lista = await app.Participantes.ListarAsync(empresa, sessao.Id);
        if (!EhAtual(g)) return;
        _todos = lista;
        Filtrar();
    }

    partial void OnAbaSelecionadaChanged(int value)
    {
        if (EmEdicao) Modo = ModoCadastro.Consulta;
        Selecionado = null;
        Filtrar();
    }

    partial void OnFiltroChanged(string value) => Filtrar();

    private void Filtrar()
    {
        var id = Selecionado?.P.Id;
        var f = Filtro.Trim();
        var digitos = Domain.Cadastros.Cnpj.SomenteDigitos(f);
        Lista.Clear();
        foreach (var p in _todos.Where(p => (AbaSelecionada == AbaClientes ? p.Cliente : p.Fornecedor)
                                            && (f.Length == 0 || p.Codigo == f || p.Nome.Contains(f, StringComparison.CurrentCultureIgnoreCase)
                                                || (digitos.Length >= 3 && (p.CpfCnpj ?? "").Contains(digitos)))))
            Lista.Add(new ParticipanteLinha(p));
        Selecionado = Lista.FirstOrDefault(l => l.P.Id == id);
    }

    partial void OnSelecionadoChanged(ParticipanteLinha? value)
    {
        if (EmConsulta) MostrarSelecionado();
        AtualizarBarra();
    }

    protected override void MostrarSelecionado()
    {
        var p = Selecionado?.P;
        (Codigo, Nome, NomeFantasia, CpfCnpj, InscricaoEstadual) = (p?.Codigo ?? "", p?.Nome ?? "", p?.NomeFantasia ?? "", Selecionado?.Documento ?? "", p?.InscricaoEstadual ?? "");
        (Uf, MunicipioIbge, Municipio, Email, Telefone) = (p?.Uf ?? "", p?.CodMunicipioIbge ?? "", p?.Municipio ?? "", p?.Email ?? "", p?.Telefone ?? "");
        (Cliente, Fornecedor, Conta, Ativo) = (p?.Cliente ?? AbaSelecionada == AbaClientes, p?.Fornecedor ?? AbaSelecionada == AbaFornecedores, p?.CodigoConta ?? "", p?.Ativo ?? true);
    }

    protected override void PrepararInclusao()
    {
        Selecionado = null;
        MostrarSelecionado();
        Codigo = ServicoParticipantes.ProximoCodigo(_todos);
    }

    protected override async Task<bool> GravarAsync(bool inclusao)
    {
        var ok = false;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            var id = inclusao ? 0 : Selecionado?.P.Id ?? 0;
            if (!inclusao && id == 0) { Erros.Add("Selecione um cadastro."); return; }
            var r = await app.Participantes.GravarAsync(empresa, new Participante(id, Codigo, Nome, NomeFantasia, CpfCnpj, InscricaoEstadual, Uf,
                MunicipioIbge, Municipio, Email, Telefone, Cliente, Fornecedor, Conta, Ativo), usuario);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            ok = true;
            Modo = ModoCadastro.Consulta;
            if (!(AbaSelecionada == AbaClientes ? Cliente : Fornecedor)) AbaSelecionada = Cliente ? AbaClientes : AbaFornecedores;
            await CarregarAsync();
            Selecionado = Lista.FirstOrDefault(l => l.P.Id == r.Valor);
            Mensagem = inclusao ? $"{Nome.Trim()} incluído (código {Selecionado?.Codigo})." : "Cadastro alterado.";
        }, Permissao.LancamentoIncluir);
        return ok;
    }

    protected override async Task<bool> ExcluirSelecionadoAsync()
    {
        var ok = false;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            if (Selecionado is null) return;
            var nome = Selecionado.Nome;
            var r = await app.Participantes.ExcluirAsync(empresa, Selecionado.P.Id, usuario);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            ok = true;
            Selecionado = null;
            await CarregarAsync();
            Mensagem = $"{nome} excluído.";
        }, Permissao.LancamentoIncluir);
        return ok;
    }
}
