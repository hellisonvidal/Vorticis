using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Application.Cadastros;
using Perseus.Infra.Importacao;
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

/// <summary>Cadastros › Clientes.</summary>
public sealed class ClientesViewModel(IContexto contexto) : ParticipantesViewModel(contexto, fornecedores: false);

/// <summary>Cadastros › Fornecedores.</summary>
public sealed class FornecedoresViewModel(IContexto contexto) : ParticipantesViewModel(contexto, fornecedores: true);

/// <summary>
/// Cadastro de clientes ou de fornecedores (a mesma pessoa pode ser os dois). Dados podem vir da base pública do CNPJ
/// (como no cadastro de empresas) ou de uma planilha.
/// </summary>
public abstract partial class ParticipantesViewModel(IContexto contexto, bool fornecedores) : CadastroViewModel(contexto)
{
    private IReadOnlyList<Participante> _todos = [];

    public bool Fornecedores { get; } = fornecedores;
    public ObservableCollection<ParticipanteLinha> Lista { get; } = [];
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

    public string TituloLista => Fornecedores ? "Fornecedores" : "Clientes";
    public bool TemSelecionado => Selecionado is not null;
    public bool PodeEditar => Contexto.Pode(Permissao.LancamentoIncluir);
    public override bool PodeManter => PodeEditar;
    protected override string NomeRegistro => Fornecedores ? "fornecedor" : "cliente";
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

    partial void OnFiltroChanged(string value) => Filtrar();

    private void Filtrar()
    {
        var id = Selecionado?.P.Id;
        var f = Filtro.Trim();
        var digitos = Domain.Cadastros.Cnpj.SomenteDigitos(f);
        Lista.Clear();
        foreach (var p in _todos.Where(p => (Fornecedores ? p.Fornecedor : p.Cliente)
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
        (Cliente, Fornecedor, Conta, Ativo) = (p?.Cliente ?? !Fornecedores, p?.Fornecedor ?? Fornecedores, p?.CodigoConta ?? "", p?.Ativo ?? true);
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
            await CarregarAsync();
            Selecionado = Lista.FirstOrDefault(l => l.P.Id == r.Valor);
            Mensagem = inclusao ? $"{Nome.Trim()} incluído (código {Selecionado?.Codigo})." : "Cadastro alterado.";
        }, Permissao.LancamentoIncluir);
        return ok;
    }

    /// <summary>
    /// Buscar dados na Receita (como no cadastro de empresas): pelo CNPJ preenche nome, fantasia, endereço, e-mail e
    /// telefone, e procura a inscrição estadual na UF (sem inscrição encontrada → ISENTO). Grava só ao Salvar.
    /// </summary>
    [RelayCommand]
    private async Task BuscarCnpjAsync()
    {
        Erros.Clear();
        Mensagem = null;
        if (!EmEdicao) { Erros.Add("Clique em Incluir ou Alterar antes de buscar os dados."); return; }
        if (Contexto.App is not { } app) return;
        var cnpj = Domain.Cadastros.Cnpj.SomenteDigitos(CpfCnpj);
        if (cnpj.Length != 14) { Erros.Add("A busca na Receita é feita pelo CNPJ (14 dígitos)."); return; }
        try
        {
            Ocupado = true;
            var r = await app.Empresas.ConsultarCnpjAsync(cnpj);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            var d = r.Valor;
            (Nome, NomeFantasia, CpfCnpj) = (d.RazaoSocial, d.NomeFantasia ?? "", Domain.Cadastros.Cnpj.Formatar(d.Cnpj));
            (Uf, MunicipioIbge, Municipio, Email, Telefone) = (d.Uf ?? Uf, d.CodMunicipioIbge ?? MunicipioIbge, d.Municipio ?? Municipio,
                                                                d.Email ?? Email, d.Telefone ?? Telefone);
            var ie = await app.Empresas.ConsultarInscricaoEstadualAsync(cnpj, Uf, MunicipioIbge);
            var aviso = "";
            if (ie.Sucesso)
                InscricaoEstadual = ie.Valor.FirstOrDefault(i => i.Ativa)?.Inscricao ?? ie.Valor.FirstOrDefault()?.Inscricao ?? "ISENTO";
            else aviso = $" Inscrição estadual não consultada: {string.Join(" ", ie.Erros)}";
            Mensagem = $"Dados de {d.RazaoSocial} preenchidos (fonte: {d.Fonte}" +
                       (d.SituacaoCadastral is { } sit ? $", situação {sit}" : "") + "). Confira e clique em Salvar." + aviso;
        }
        catch (Exception ex) { Erros.Add(ex.Message); }
        finally { Ocupado = false; }
    }

    /// <summary>Importa vários cadastros de uma planilha (Excel ou CSV), como clientes ou fornecedores desta tela.</summary>
    public Task ImportarPlanilhaAsync(string arquivo) => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var t = LeitorPlanilha.Ler(arquivo);
        if (!t.Sucesso) { MostrarResultado(t, ""); return; }
        if (!await Contexto.ConfirmarAsync($"Importar {TituloLista.ToLowerInvariant()}",
                $"Importar {t.Valor.Linhas.Count} linha(s) da planilha como {TituloLista.ToLowerInvariant()}? " +
                "Quem já estiver cadastrado (mesmo CPF/CNPJ) não é duplicado.", "Importar", "Cancelar")) return;
        var r = await app.Participantes.ImportarAsync(empresa, t.Valor, cliente: !Fornecedores, fornecedor: Fornecedores, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        foreach (var e in r.Valor.Erros.Take(20)) Erros.Add(e);
        await CarregarAsync();
        Mensagem = $"{r.Valor.Incluidos} cadastro(s) incluído(s)" + (r.Valor.Atualizados > 0 ? $", {r.Valor.Atualizados} já existente(s) marcado(s) como {NomeRegistro}" : "") +
                   (r.Valor.Erros.Count > 0 ? $"; {r.Valor.Erros.Count} linha(s) com erro." : ".");
    }, Permissao.LancamentoIncluir);

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
