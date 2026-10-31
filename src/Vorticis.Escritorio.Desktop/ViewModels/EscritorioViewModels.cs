using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Relatorios;
using Vorticis.Desktop.Comum;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra;
using Vorticis.Infra.Relatorios;

namespace Vorticis.Escritorio.Desktop.ViewModels;

public static class EscritorioTelas
{
    public const string Carteira = "escritorio.carteira";
    public const string Tarefas = "escritorio.tarefas";
    public const string Protocolos = "escritorio.protocolos";
    public const string Clientes = "escritorio.clientes";
    public const string Contratos = "escritorio.contratos";
    public const string RelHonorarios = "escritorio.rel.honorarios";
}

internal static class Ext
{
    public static ServicoEscritorio Escritorio(this VorticisApp app) => ServicosEscritorio.De(app);
    public static DateOnly Dia(DateTime? d) => d is { } v ? DateOnly.FromDateTime(v) : DateOnly.FromDateTime(DateTime.Today);
    public static DateTime Dt(DateOnly d) => d.ToDateTime(TimeOnly.MinValue);
}

public sealed record Opcao<T>(T Valor, string Texto)
{
    public override string ToString() => Texto;
}

/// <summary>Lista de clientes (e/ou fornecedores) do cadastro próprio do escritório para as caixas de escolha das telas.</summary>
internal static class ListaPessoas
{
    public static async Task Preencher(ObservableCollection<Opcao<long?>> destino, VorticisApp app, long escritorio, long usuario, string vazio,
                                       Func<PessoaEscritorio, bool>? filtro = null)
    {
        var pessoas = await app.Escritorio().ListarPessoasAsync(escritorio, usuario);
        destino.Clear();
        destino.Add(new Opcao<long?>(null, vazio));
        foreach (var p in pessoas.Where(p => p.Ativo && (filtro ?? (x => x.Cliente))(p))) destino.Add(new Opcao<long?>(p.Id, $"{p.Codigo} – {p.Nome}"));
    }
}

// ---------------------------------------------------------------------------------------------- clientes e fornecedores do escritório

public sealed record PessoaLinha(PessoaEscritorio P)
{
    public int Codigo => P.Codigo;
    public string Nome => P.Nome;
    public string Documento => P.DocumentoFormatado;
    public string Papel => P.Papel;
    public string Contato => string.Join(" · ", new[] { P.Contato, P.Telefone, P.Email }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public string Situacao => P.Ativo ? "Ativo" : "Inativo";
}

/// <summary>
/// Cadastro próprio do Escritório (Cadastros › Clientes); a marca de fornecedor de cadastros antigos é preservada ao gravar.
/// O cliente tem os dados do cadastro de empresas (importados pela consulta do CNPJ), os módulos do contrato (com algum marcado, a
/// empresa é cadastrada nos demais módulos) e a aba Honorários e cobranças.
/// </summary>
public abstract partial class PessoasViewModel(IContexto contexto, bool clientes) : CadastroViewModel(contexto)
{
    public ObservableCollection<PessoaLinha> Lista { get; } = [];
    private IReadOnlyList<PessoaEscritorio> _todos = [];

    public bool ModoClientes => clientes;
    public string Titulo => clientes ? "Clientes do escritório" : "Fornecedores do escritório";
    public string TextoOutroPapel => clientes ? "Também é fornecedor do escritório" : "Também é cliente do escritório";
    public static IReadOnlyList<string> Regimes { get; } = RegrasEscritorio.Regimes.Select(r => r.Rotulo).ToList();

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TemSelecionado))] private PessoaLinha? _selecionado;
    [ObservableProperty] private string _filtro = "";
    [ObservableProperty] private string _codigo = "";
    [ObservableProperty] private string _nome = "";
    [ObservableProperty] private string _documento = "";
    [ObservableProperty] private bool _outroPapel;
    [ObservableProperty] private string _regime = "";
    [ObservableProperty] private string _contato = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _telefone = "";
    [ObservableProperty] private string _endereco = "";
    [ObservableProperty] private string _municipio = "";
    [ObservableProperty] private string _uf = "";
    [ObservableProperty] private bool _ativo = true;
    [ObservableProperty] private string _observacao = "";
    // dados de empresa do cliente (como no cadastro de empresas)
    [ObservableProperty] private string _nomeFantasia = "";
    [ObservableProperty] private string _naturezaJuridica = "";
    [ObservableProperty] private string _cnae = "";
    [ObservableProperty] private string _inscricaoEstadual = "";
    [ObservableProperty] private DateTime? _inicioAtividades;
    [ObservableProperty] private string _cep = "";
    [ObservableProperty] private string _logradouro = "";
    [ObservableProperty] private string _numero = "";
    [ObservableProperty] private string _complemento = "";
    [ObservableProperty] private string _bairro = "";
    [ObservableProperty] private string _codMunicipioIbge = "";
    [ObservableProperty] private string _origemDados = "";
    // módulos do contrato
    [ObservableProperty] private bool _modContabil;
    [ObservableProperty] private bool _modFiscal;
    [ObservableProperty] private bool _modFolha;
    [ObservableProperty] private bool _modCondominio;
    [ObservableProperty] private bool _modGerencial;
    [ObservableProperty] private bool _modFrota;
    [ObservableProperty] private string _situacaoModulos = "";

    public bool TemSelecionado => Selecionado is not null;
    public override bool PodeManter => Contexto.Pode(Permissao.EscritorioTarefas);
    protected override string NomeRegistro => clientes ? "cliente" : "fornecedor";
    protected override string? DescricaoSelecionado => Selecionado is { } s ? $"{s.Codigo} – {s.Nome}" : null;

    public virtual void AtualizarPermissoes() => AtualizarBarra();

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var g = NovaGeracao();
        var lista = await app.Escritorio().ListarPessoasAsync(empresa, sessao.Id);
        if (!EhAtual(g)) return;
        _todos = lista;
        Filtrar();
    }

    private bool DoPapel(PessoaEscritorio p) => clientes ? p.Cliente : p.Fornecedor;

    partial void OnFiltroChanged(string value) => Filtrar();

    private void Filtrar()
    {
        var id = Selecionado?.P.Id;
        var f = Filtro.Trim();
        Lista.Clear();
        foreach (var p in _todos.Where(p => DoPapel(p) && (f.Length == 0 || p.Nome.Contains(f, StringComparison.CurrentCultureIgnoreCase) || p.Codigo.ToString() == f
                                                         || (p.Documento ?? "").Contains(f))))
            Lista.Add(new PessoaLinha(p));
        Selecionado = Lista.FirstOrDefault(l => l.P.Id == id);
    }

    partial void OnSelecionadoChanged(PessoaLinha? value)
    {
        if (EmConsulta) MostrarSelecionado();
        AtualizarBarra();
        _ = AoSelecionarAsync(value?.P);
    }

    protected virtual Task AoSelecionarAsync(PessoaEscritorio? p) => Task.CompletedTask;

    /// <summary>Logo da empresa vinculada ao cliente (cadastro único de empresas, usado no cabeçalho dos relatórios).</summary>
    public Vorticis.Desktop.Comum.ViewModels.LogoEmpresaEditor Logo { get; } = new();

    protected async Task CarregarLogoAsync(long? empresaVinculada)
    {
        Vorticis.Application.Cadastros.LogoEmpresa? logo = null;
        if (empresaVinculada is { } id && Contexto.App is { } app) logo = await app.Empresas.ObterLogoAsync(id);
        Logo.Carregar(logo);
    }

    protected override void MostrarSelecionado()
    {
        var p = Selecionado?.P;
        (Codigo, Nome, Documento) = (p?.Codigo.ToString() ?? "", p?.Nome ?? "", p?.DocumentoFormatado ?? "");
        OutroPapel = p is not null && (clientes ? p.Fornecedor : p.Cliente);
        (Regime, Contato, Email, Telefone) = (p?.Regime ?? "", p?.Contato ?? "", p?.Email ?? "", p?.Telefone ?? "");
        (Endereco, Municipio, Uf, Ativo, Observacao) = (p?.Endereco ?? "", p?.Municipio ?? "", p?.Uf ?? "", p?.Ativo ?? true, p?.Observacao ?? "");
        (NomeFantasia, NaturezaJuridica, Cnae, InscricaoEstadual) = (p?.NomeFantasia ?? "", p?.NaturezaJuridica ?? "", p?.Cnae ?? "", p?.InscricaoEstadual ?? "");
        InicioAtividades = p?.InicioAtividades is { } d ? Ext.Dt(d) : null;
        (Cep, Logradouro, Numero, Complemento, Bairro, CodMunicipioIbge) =
            (p?.Cep ?? "", p?.Logradouro ?? "", p?.Numero ?? "", p?.Complemento ?? "", p?.Bairro ?? "", p?.CodMunicipioIbge ?? "");
        var m = p?.Modulos ?? Vorticis.Application.Cadastros.ModulosEmpresa.Nenhum;
        (ModContabil, ModFiscal, ModFolha) = (m.HasFlag(Vorticis.Application.Cadastros.ModulosEmpresa.Contabil), m.HasFlag(Vorticis.Application.Cadastros.ModulosEmpresa.Fiscal),
                                              m.HasFlag(Vorticis.Application.Cadastros.ModulosEmpresa.Folha));
        (ModCondominio, ModGerencial) = (m.HasFlag(Vorticis.Application.Cadastros.ModulosEmpresa.Condominio), m.HasFlag(Vorticis.Application.Cadastros.ModulosEmpresa.Erp));
        SituacaoModulos = p?.EmpresaVinculadaId is null
            ? "Marque os módulos do contrato: ao gravar, o cliente é cadastrado como empresa nesses módulos, com o mesmo código em todos eles (só o administrador)."
            : $"Cliente já cadastrado como empresa nos módulos marcados (código {p.EmpresaVinculadaId:0000}, o mesmo em todos os módulos). Marque outros para incluí-lo neles também.";
        OrigemDados = "";
    }

    private Vorticis.Application.Cadastros.ModulosEmpresa ModulosMarcados()
    {
        var m = Vorticis.Application.Cadastros.ModulosEmpresa.Nenhum;
        if (ModContabil) m |= Vorticis.Application.Cadastros.ModulosEmpresa.Contabil;
        if (ModFiscal) m |= Vorticis.Application.Cadastros.ModulosEmpresa.Fiscal;
        if (ModFolha) m |= Vorticis.Application.Cadastros.ModulosEmpresa.Folha;
        if (ModCondominio) m |= Vorticis.Application.Cadastros.ModulosEmpresa.Condominio;
        if (ModGerencial) m |= Vorticis.Application.Cadastros.ModulosEmpresa.Erp;
        return m;
    }

    protected override void PrepararInclusao()
    {
        Selecionado = null;
        Logo.Carregar(null);
        MostrarSelecionado();
        Codigo = Sequencia.MenorLivre(_todos.Select(p => p.Codigo)).ToString();
    }

    /// <summary>Preenche o cliente com os dados da Receita Federal (base pública), como em Cadastros › Empresas.</summary>
    [RelayCommand]
    private Task BuscarCnpjAsync() => ExecutarAsync(async (app, _, _) =>
    {
        var r = await app.Empresas.ConsultarCnpjAsync(Documento);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        AplicarConsulta(r.Valor);
        Mensagem = $"Dados importados de {r.Valor.Fonte}. Confira antes de gravar.";
    });

    public void AplicarConsulta(Vorticis.Application.Cadastros.DadosCnpj d)
    {
        Nome = d.RazaoSocial;
        static string Ou(string? novo, string atual) => string.IsNullOrWhiteSpace(novo) ? atual : novo;
        NomeFantasia = Ou(d.NomeFantasia, NomeFantasia);
        NaturezaJuridica = Ou(d.NaturezaJuridica, NaturezaJuridica);
        Cnae = Ou(d.CnaePrincipal, Cnae);
        Uf = Ou(d.Uf, Uf);
        CodMunicipioIbge = Ou(d.CodMunicipioIbge, CodMunicipioIbge);
        Municipio = Ou(d.Municipio, Municipio);
        if (d.DataInicioAtividades is { } ini) InicioAtividades = Ext.Dt(ini);
        (Cep, Logradouro, Numero, Complemento, Bairro) = (Ou(d.Cep, Cep), Ou(d.Logradouro, Logradouro), Ou(d.Numero, Numero), Ou(d.Complemento, Complemento), Ou(d.Bairro, Bairro));
        (Email, Telefone) = (Ou(d.Email, Email), Ou(d.Telefone, Telefone));
        if (d.RegimeSugerido is { } reg) Regime = RegrasEscritorio.Regimes.First(x => x.Regime == reg).Rotulo;
        OrigemDados = $"Dados de {d.Fonte} em {d.ConsultadoEm.LocalDateTime:dd/MM/yyyy HH:mm}" + (d.Ativa ? "" : $" — situação cadastral: {d.SituacaoCadastral}");
    }

    protected override async Task<bool> GravarAsync(bool inclusao)
    {
        var ok = false;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            if (!int.TryParse(Codigo.Trim(), out var cod)) { Erros.Add("Informe o código (número)."); return; }
            var anterior = inclusao ? null : Selecionado?.P;
            // Cliente novo nos módulos vira empresa compartilhada: exige confirmação antes de gravar.
            if (clientes && ModulosMarcados() != Vorticis.Application.Cadastros.ModulosEmpresa.Nenhum && anterior?.EmpresaVinculadaId is null
                && !await Contexto.ConfirmarAsync("Cadastrar esta empresa em quais módulos?", Vorticis.Application.Cadastros.MensagensEmpresa.AvisoCompartilhado, "Continuar", "Cancelar"))
            { Erros.Add("Cadastro cancelado."); return; }
            var p = new PessoaEscritorio(anterior?.Id ?? 0, cod, clientes || OutroPapel, !clientes || OutroPapel, Nome, Documento, Regime, Contato, Email, Telefone, Endereco,
                                         Municipio, Uf, Ativo, Observacao, NomeFantasia, NaturezaJuridica, Cnae, InscricaoEstadual,
                                         InicioAtividades is { } ini ? DateOnly.FromDateTime(ini) : null, Cep, Logradouro, Numero, Complemento, Bairro, CodMunicipioIbge,
                                         anterior?.EmpresaVinculadaId,
                                         // os módulos só são marcados no cadastro de clientes; o de fornecedores não mexe na empresa vinculada
                                         clientes ? ModulosMarcados() : Vorticis.Application.Cadastros.ModulosEmpresa.Nenhum);
            var r = await app.Escritorio().GravarPessoaAsync(empresa, p, usuario);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            ok = true;
            Modo = ModoCadastro.Consulta;
            await CarregarAsync();
            Selecionado = Lista.FirstOrDefault(l => l.P.Id == r.Valor);
                        if (clientes && Logo.Alterado && Selecionado?.P.EmpresaVinculadaId is { } empId)
            {
                var salva = await app.Empresas.SalvarLogoAsync(empId, Logo.Logo);
                if (!salva.Sucesso) foreach (var e in salva.Erros) Erros.Add(e);
                else { Logo.Carregar(Logo.Logo); await Contexto.AtualizarLogoRelatoriosAsync(); }
            }
            else if (clientes && Logo.Alterado) Erros.Add("A logo só pode ser gravada depois que o cliente for cadastrado como empresa: marque ao menos um módulo e salve de novo.");
            Mensagem = (inclusao ? "Cadastro incluído." : "Cadastro alterado.")
                       + (p.Modulos != Vorticis.Application.Cadastros.ModulosEmpresa.Nenhum ? " Empresa cadastrada/atualizada nos módulos marcados."
                          + (Selecionado?.P.EmpresaVinculadaId is { } eid ? " " + Vorticis.Application.Cadastros.MensagensEmpresa.CodigoUnico(eid) : "") : "");
        }, Permissao.EscritorioTarefas);
        return ok;
    }

    protected override async Task<bool> ExcluirSelecionadoAsync()
    {
        var ok = false;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            if (Selecionado is null) return;
            var r = await app.Escritorio().ExcluirPessoaAsync(empresa, Selecionado.P.Id, usuario);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            ok = true;
            Selecionado = null;
            await CarregarAsync();
            Mensagem = "Cadastro excluído.";
        }, Permissao.EscritorioTarefas);
        return ok;
    }
}

/// <summary>Cadastros › Clientes: dados cadastrais (como o cadastro de empresas), módulos do contrato e honorários e cobranças.</summary>
public sealed partial class ClientesEscritorioViewModel : PessoasViewModel
{
    public ClientesEscritorioViewModel(IContexto contexto) : base(contexto, true) => Honorarios = new HonorariosViewModel(contexto) { EmbutidoNoCliente = true };

    public HonorariosViewModel Honorarios { get; }

    public override void AtualizarPermissoes()
    {
        base.AtualizarPermissoes();
        Honorarios.AtualizarPermissoes();
    }

    protected override async Task AoSelecionarAsync(PessoaEscritorio? p)
    {
        await CarregarLogoAsync(p?.EmpresaVinculadaId);
        await Honorarios.MostrarClienteAsync(p?.Id);
    }
}


// ---------------------------------------------------------------------------------------------- carteira

public sealed record ClienteLinha(ClienteEscritorio C)
{
    public int Codigo => C.Codigo;
    public string Nome => C.Nome;
    public string Documento => C.Documento switch
    {
        { Length: 14 } d => Vorticis.Domain.Cadastros.Cnpj.Formatar(d),
        { Length: 11 } d => Vorticis.Domain.Cadastros.Cpf.Formatar(d),
        _ => "",
    };
    public string Regime => C.Regime ?? "";
    public string Tarefas => C.TarefasAtrasadas > 0 ? $"{C.TarefasPendentes} ({C.TarefasAtrasadas} atrasada(s))" : C.TarefasPendentes.ToString();
    public string Honorario => C.HonorarioMensal == 0 ? "" : Formato.Moeda(C.HonorarioMensal);
    public string EmAberto => C.HonorariosEmAberto == 0 ? "" : Formato.Moeda(C.HonorariosEmAberto);
    public string Contato => C.Contato ?? "";
}

/// <summary>Escritório › Carteira de clientes: clientes do cadastro próprio do escritório, com tarefas e honorários.</summary>
public partial class CarteiraViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<ClienteLinha> Lista { get; } = [];
    [ObservableProperty] private string _totais = "";

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        var g = NovaGeracao();
        var itens = await app.Escritorio().CarteiraAsync(empresa, sessao.Id, DateOnly.FromDateTime(DateTime.Today));
        if (!EhAtual(g)) return;
        Lista.Clear();
        foreach (var c in itens) Lista.Add(new ClienteLinha(c));
        Totais = $"{itens.Count} cliente(s) · honorários mensais {Formato.Moeda(itens.Sum(c => c.HonorarioMensal))} · em aberto {Formato.Moeda(itens.Sum(c => c.HonorariosEmAberto))} · "
                 + $"{itens.Sum(c => c.TarefasAtrasadas)} tarefa(s) atrasada(s)";
    }

    [RelayCommand]
    private Task AtualizarAsync() => CarregarAsync();
}

// ---------------------------------------------------------------------------------------------- tarefas

public sealed record TarefaLinha(Tarefa T)
{
    public string Vencimento => T.Vencimento.ToString("dd/MM/yyyy");
    public string Cliente => T.PessoaNome ?? "(escritório)";
    public string Titulo => T.Titulo;
    public string Responsavel => T.ResponsavelNome ?? "";
    public string Prioridade => T.Prioridade switch { 1 => "Alta", 3 => "Baixa", _ => "Normal" };
    public string Situacao => T.Atrasada(DateOnly.FromDateTime(DateTime.Today)) ? "ATRASADA" : T.Situacao.Rotulo();
    public string Repete => T.Recorrencia.Rotulo();
}

/// <summary>Escritório › Tarefas e prazos: tarefas do escritório (de um cliente ou internas), com responsável, prioridade e recorrência.</summary>
public partial class TarefasViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<TarefaLinha> Lista { get; } = [];
    public ObservableCollection<Opcao<long?>> Responsaveis { get; } = [];
    public ObservableCollection<Opcao<long?>> Clientes { get; } = [];
    public ObservableCollection<Opcao<long?>> FiltroClientes { get; } = [];
    public static IReadOnlyList<Opcao<int>> Prioridades { get; } = [new(1, "Alta"), new(2, "Normal"), new(3, "Baixa")];
    public static IReadOnlyList<Opcao<SituacaoTarefa>> Situacoes { get; } =
        [.. Enum.GetValues<SituacaoTarefa>().Select(s => new Opcao<SituacaoTarefa>(s, s.Rotulo()))];
    public static IReadOnlyList<Opcao<Recorrencia>> Recorrencias { get; } =
        [.. Enum.GetValues<Recorrencia>().Select(r => new Opcao<Recorrencia>(r, r.Rotulo()))];

    [ObservableProperty] private Opcao<long?>? _filtroCliente;
    [ObservableProperty] private bool _somenteAbertas = true;
    [ObservableProperty] private TarefaLinha? _selecionada;
    [ObservableProperty] private Opcao<long?>? _cliente;
    [ObservableProperty] private string _titulo = "";
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private DateTime? _vencimento = DateTime.Today;
    [ObservableProperty] private Opcao<long?>? _responsavel;
    [ObservableProperty] private Opcao<int>? _prioridade = Prioridades[1];
    [ObservableProperty] private Opcao<SituacaoTarefa>? _situacao = Situacoes[0];
    [ObservableProperty] private Opcao<Recorrencia>? _recorrencia = Recorrencias[0];
    private bool _carregando;

    public bool PodeEditar => Contexto.Pode(Permissao.EscritorioTarefas);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeEditar));

    partial void OnFiltroClienteChanged(Opcao<long?>? value) { if (!_carregando) _ = CarregarAsync(); }
    partial void OnSomenteAbertasChanged(bool value) => _ = CarregarAsync();

    partial void OnSelecionadaChanged(TarefaLinha? value)
    {
        if (value?.T is not { } t) return;
        (Titulo, Descricao, Vencimento) = (t.Titulo, t.Descricao ?? "", Ext.Dt(t.Vencimento));
        Cliente = Clientes.FirstOrDefault(c => c.Valor == t.PessoaId) ?? Clientes.FirstOrDefault();
        Responsavel = Responsaveis.FirstOrDefault(r => r.Valor == t.ResponsavelId) ?? Responsaveis.FirstOrDefault();
        Prioridade = Prioridades.First(p => p.Valor == t.Prioridade);
        Situacao = Situacoes.First(s => s.Valor == t.Situacao);
        Recorrencia = Recorrencias.First(r => r.Valor == t.Recorrencia);
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        var g = NovaGeracao();
        var filtro = FiltroCliente?.Valor;
        var itens = await app.Escritorio().ListarTarefasAsync(empresa, filtro, SomenteAbertas, sessao.Id);
        var usuarios = await app.Escritorio().ListarUsuariosAsync();
        if (!EhAtual(g)) return;
        _carregando = true;
        try
        {
            var clienteAtual = Cliente?.Valor;
            await ListaPessoas.Preencher(Clientes, app, empresa, sessao.Id, "(tarefa interna do escritório)");
            await ListaPessoas.Preencher(FiltroClientes, app, empresa, sessao.Id, "Todos os clientes");
            Cliente = Clientes.FirstOrDefault(c => c.Valor == clienteAtual) ?? Clientes[0];
            FiltroCliente = FiltroClientes.FirstOrDefault(c => c.Valor == filtro) ?? FiltroClientes[0];
        }
        finally { _carregando = false; }
        if (Responsaveis.Count != usuarios.Count + 1)
        {
            Responsaveis.Clear();
            Responsaveis.Add(new Opcao<long?>(null, "(ninguém)"));
            foreach (var (id, nome) in usuarios) Responsaveis.Add(new Opcao<long?>(id, nome));
        }
        Responsavel ??= Responsaveis.FirstOrDefault(r => r.Valor == sessao.Id) ?? Responsaveis[0];
        Lista.Clear();
        foreach (var t in itens) Lista.Add(new TarefaLinha(t));
    }

    [RelayCommand]
    private void Nova()
    {
        Selecionada = null;
        (Titulo, Descricao, Vencimento) = ("", "", DateTime.Today);
        (Prioridade, Situacao, Recorrencia) = (Prioridades[1], Situacoes[0], Recorrencias[0]);
        Cliente = Clientes.FirstOrDefault(c => c.Valor == FiltroCliente?.Valor) ?? Clientes.FirstOrDefault();
        Responsavel = Responsaveis.FirstOrDefault(r => r.Valor == Contexto.Sessao?.Id) ?? Responsaveis.FirstOrDefault();
        Mensagem = "Preencha a tarefa e clique em Gravar.";
    }

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var t = new Tarefa(Selecionada?.T.Id ?? 0, empresa, Cliente?.Valor, Titulo, string.IsNullOrWhiteSpace(Descricao) ? null : Descricao,
                           Ext.Dia(Vencimento), Responsavel?.Valor, Prioridade?.Valor ?? 2, Situacao?.Valor ?? SituacaoTarefa.Pendente, Recorrencia?.Valor ?? Escritorio.Recorrencia.Nenhuma);
        var r = await app.Escritorio().GravarTarefaAsync(t, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Selecionada = Lista.FirstOrDefault(l => l.T.Id == r.Valor);
        Mensagem = "Tarefa gravada.";
    }, Permissao.EscritorioTarefas);

    [RelayCommand]
    private Task ConcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada?.T is not { } t) { Erros.Add("Selecione a tarefa na lista."); return; }
        var r = await app.Escritorio().ConcluirTarefaAsync(t, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = r.Valor is null ? "Tarefa concluída." : "Tarefa concluída; a próxima ocorrência foi criada.";
    }, Permissao.EscritorioTarefas);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada?.T is not { } t) { Erros.Add("Selecione a tarefa na lista."); return; }
        if (!await Contexto.ConfirmarAsync("Excluir tarefa", $"Excluir a tarefa \"{t.Titulo}\"?")) return;
        var r = await app.Escritorio().ExcluirTarefaAsync(empresa, t.Id, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Selecionada = null;
        await CarregarAsync();
        Mensagem = "Tarefa excluída.";
    }, Permissao.EscritorioTarefas);
}

// ---------------------------------------------------------------------------------------------- protocolos

public sealed record ProtocoloLinha(Protocolo P)
{
    public int Numero => P.Numero;
    public string Data => P.Data.ToString("dd/MM/yyyy");
    public string Cliente => P.ClienteNome ?? "";
    public string Tipo => P.Tipo == TipoProtocolo.Entrega ? "Entrega ao cliente" : "Recebido do cliente";
    public string Documento => P.Documento;
    public string Pessoa => P.Pessoa;
    public string Usuario => P.UsuarioNome ?? "";
}

/// <summary>Escritório › Protocolo de documentos: entregas e recebimentos de documentos dos clientes do escritório, numerados.</summary>
public partial class ProtocolosViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<ProtocoloLinha> Lista { get; } = [];
    public ObservableCollection<Opcao<long?>> Clientes { get; } = [];
    public static IReadOnlyList<Opcao<TipoProtocolo>> Tipos { get; } = [new(TipoProtocolo.Entrega, "Entrega ao cliente"), new(TipoProtocolo.Recebimento, "Recebido do cliente")];

    [ObservableProperty] private ProtocoloLinha? _selecionado;
    [ObservableProperty] private Opcao<long?>? _cliente;
    [ObservableProperty] private DateTime? _data = DateTime.Today;
    [ObservableProperty] private Opcao<TipoProtocolo>? _tipo = Tipos[0];
    [ObservableProperty] private string _documento = "";
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private string _pessoa = "";

    public bool PodeEditar => Contexto.Pode(Permissao.EscritorioTarefas);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeEditar));

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        var g = NovaGeracao();
        var itens = await app.Escritorio().ListarProtocolosAsync(empresa, sessao.Id);
        if (!EhAtual(g)) return;
        var atual = Cliente?.Valor;
        await ListaPessoas.Preencher(Clientes, app, empresa, sessao.Id, "(escolha o cliente)");
        Cliente = Clientes.FirstOrDefault(c => c.Valor == atual) ?? Clientes[0];
        Lista.Clear();
        foreach (var p in itens) Lista.Add(new ProtocoloLinha(p));
    }

    [RelayCommand]
    private Task IncluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Cliente?.Valor is not { } pessoa) { Erros.Add("Escolha o cliente."); return; }
        var r = await app.Escritorio().IncluirProtocoloAsync(new Protocolo(0, empresa, pessoa, 0, Ext.Dia(Data), Tipo?.Valor ?? TipoProtocolo.Entrega, Documento,
                                                                           string.IsNullOrWhiteSpace(Descricao) ? null : Descricao, Pessoa), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        (Documento, Descricao, Pessoa) = ("", "", "");
        await CarregarAsync();
        Mensagem = $"Protocolo nº {r.Valor} registrado.";
    }, Permissao.EscritorioTarefas);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionado?.P is not { } p) { Erros.Add("Selecione o protocolo na lista."); return; }
        if (!await Contexto.ConfirmarAsync("Excluir protocolo", $"Excluir o protocolo nº {p.Numero}?")) return;
        var r = await app.Escritorio().ExcluirProtocoloAsync(empresa, p.Id, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = "Protocolo excluído.";
    }, Permissao.EscritorioTarefas);
}

// ---------------------------------------------------------------------------------------------- honorários

public sealed record CobrancaLinha(CobrancaHonorario C)
{
    public string Cliente => C.PessoaNome ?? "";
    public string Competencia => C.Competencia.ToString("MM/yyyy");
    public string Vencimento => C.Vencimento.ToString("dd/MM/yyyy");
    public string Valor => Formato.Moeda(C.Valor);
    public string Situacao => C.Vencida(DateOnly.FromDateTime(DateTime.Today)) ? "VENCIDA" : C.Situacao.Rotulo();
    public string Recebido => C.RecebidoEm is { } d ? $"{d:dd/MM/yyyy} – {Formato.Moeda(C.ValorRecebido ?? 0)}" : "";
}

/// <summary>Aba Honorários e cobranças do cadastro de clientes: honorário mensal do cliente e as cobranças dele.</summary>
public partial class HonorariosViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<CobrancaLinha> Cobrancas { get; } = [];
    public ObservableCollection<Opcao<long?>> Clientes { get; } = [];
    public static IReadOnlyList<string> Indices { get; } = ["NENHUM", "IPCA", "IGPM", "INPC", "SALARIO"];
    private IReadOnlyList<ContratoHonorario> _contratos = [];

    [ObservableProperty] private Opcao<long?>? _cliente;
    [ObservableProperty] private string _valor = "";
    [ObservableProperty] private decimal? _dia = 10;
    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _fim;
    [ObservableProperty] private string _indice = "NENHUM";
    [ObservableProperty] private string _observacao = "";
    [ObservableProperty] private string _situacaoContrato = "";
    [ObservableProperty] private DateTime? _competencia = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private bool _somenteAbertas;
    [ObservableProperty] private CobrancaLinha? _selecionada;
    [ObservableProperty] private DateTime? _dataRecebimento = DateTime.Today;
    [ObservableProperty] private string _valorRecebido = "";
    [ObservableProperty] private string _totais = "";

    public bool PodeEditar => Contexto.Pode(Permissao.EscritorioHonorarios);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeEditar));

    /// <summary>Na aba Honorários e cobranças do cadastro do cliente: o cliente é o selecionado no cadastro e a lista mostra só as cobranças dele.</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(EscolheCliente))] private long? _clienteFixo;
    public bool EmbutidoNoCliente { get; init; }
    public bool EscolheCliente => !EmbutidoNoCliente;

    /// <summary>Mostra o honorário e as cobranças do cliente escolhido no cadastro de clientes.</summary>
    public async Task MostrarClienteAsync(long? pessoa)
    {
        ClienteFixo = pessoa;
        if (!Clientes.Any(c => c.Valor == pessoa)) { await CarregarAsync(); return; }
        Cliente = Clientes.FirstOrDefault(c => c.Valor == pessoa) ?? Clientes.FirstOrDefault();
        MostrarContrato();
        await CarregarCobrancasAsync();
    }

    partial void OnCompetenciaChanged(DateTime? value) => _ = CarregarCobrancasAsync();
    partial void OnSomenteAbertasChanged(bool value) => _ = CarregarCobrancasAsync();
    partial void OnSelecionadaChanged(CobrancaLinha? value) => ValorRecebido = value is null ? "" : Formato.Num(value.C.Valor);
    partial void OnClienteChanged(Opcao<long?>? value) => MostrarContrato();

    private void MostrarContrato()
    {
        var c = _contratos.FirstOrDefault(x => x.PessoaId == Cliente?.Valor);
        if (c is null)
        {
            (Valor, Dia, Fim, Indice, Observacao) = ("", 10, null, "NENHUM", "");
            SituacaoContrato = Cliente?.Valor is null ? "Escolha o cliente para ver ou gravar o honorário." : "O cliente ainda não tem honorário cadastrado.";
        }
        else
        {
            (Valor, Dia, Inicio, Fim, Indice, Observacao) = (Formato.Num(c.Valor), c.DiaVencimento, Ext.Dt(c.Inicio), c.Fim is { } f ? Ext.Dt(f) : null, c.IndiceReajuste, c.Observacao ?? "");
            SituacaoContrato = $"Honorário: {Formato.Moeda(c.Valor)} por mês, vencimento dia {c.DiaVencimento}.";
        }
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        _contratos = await app.Escritorio().ListarHonorariosAsync(empresa, sessao.Id);
        var atual = Cliente?.Valor;
        await ListaPessoas.Preencher(Clientes, app, empresa, sessao.Id, "(escolha o cliente)");
        Cliente = Clientes.FirstOrDefault(c => c.Valor == (EmbutidoNoCliente ? ClienteFixo : atual)) ?? Clientes[0];
        MostrarContrato();
        await CarregarCobrancasAsync();
    }

    private async Task CarregarCobrancasAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        var g = NovaGeracao();
        var todas = EmbutidoNoCliente || SomenteAbertas;
        var itens = await app.Escritorio().ListarCobrancasAsync(empresa, todas ? null : Ext.Dia(Competencia), SomenteAbertas, sessao.Id);
        if (!EhAtual(g)) return;
        if (EmbutidoNoCliente) itens = itens.Where(c => c.PessoaId == ClienteFixo).OrderByDescending(c => c.Competencia).ToList();
        Cobrancas.Clear();
        foreach (var c in itens) Cobrancas.Add(new CobrancaLinha(c));
        Totais = $"{itens.Count} cobrança(s) · total {Formato.Moeda(itens.Where(c => c.Situacao != SituacaoCobranca.Cancelada).Sum(c => c.Valor))} · "
                 + $"recebido {Formato.Moeda(itens.Sum(c => c.ValorRecebido ?? 0))} · em aberto {Formato.Moeda(itens.Where(c => c.Situacao == SituacaoCobranca.Aberta).Sum(c => c.Valor))}";
    }

    [RelayCommand]
    private Task GravarContratoAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Cliente?.Valor is not { } pessoa) { Erros.Add("Escolha o cliente."); return; }
        var r = await app.Escritorio().GravarHonorarioAsync(empresa, new ContratoHonorario(0, pessoa, Formato.LerValor(Valor) ?? 0, (int)(Dia ?? 0), Ext.Dia(Inicio),
                                                                                           Fim is { } f ? DateOnly.FromDateTime(f) : null, Indice, string.IsNullOrWhiteSpace(Observacao) ? null : Observacao), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = "Honorário gravado.";
    }, Permissao.EscritorioHonorarios);

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Escritorio().GerarCobrancasAsync(empresa, Ext.Dia(Competencia), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        SomenteAbertas = false;
        await CarregarCobrancasAsync();
        Mensagem = r.Valor == 0 ? "Nenhuma cobrança nova: todos os clientes com honorário já têm a cobrança deste mês." : $"{r.Valor} cobrança(s) gerada(s).";
    }, Permissao.EscritorioHonorarios);

    [RelayCommand]
    private Task ReceberAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada?.C is not { } c) { Erros.Add("Selecione a cobrança na lista."); return; }
        var r = await app.Escritorio().ReceberAsync(empresa, c, Ext.Dia(DataRecebimento), Formato.LerValor(ValorRecebido) ?? 0, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarCobrancasAsync();
        Mensagem = "Recebimento registrado.";
    }, Permissao.EscritorioHonorarios);

    [RelayCommand]
    private Task CancelarAsync() => Alterar(SituacaoCobranca.Cancelada, "Cobrança cancelada.");

    [RelayCommand]
    private Task ReabrirAsync() => Alterar(SituacaoCobranca.Aberta, "Cobrança reaberta (recebimento desfeito).");

    private Task Alterar(SituacaoCobranca nova, string msg) => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada?.C is not { } c) { Erros.Add("Selecione a cobrança na lista."); return; }
        var r = await app.Escritorio().AlterarSituacaoAsync(empresa, c, nova, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarCobrancasAsync();
        Mensagem = msg;
    }, Permissao.EscritorioHonorarios);
}

// ---------------------------------------------------------------------------------------------- gerador de contratos

public sealed record ContratoLinha(ContratoEscritorio C)
{
    public int Numero => C.Numero;
    public string Tipo => C.Modelo == ModeloContrato.Cliente ? "Cliente" : "Fornecedor";
    public string Parte => C.PessoaNome ?? "";
    public string Valor => Formato.Moeda(C.Valor);
    public string Periodicidade => C.Periodicidade.Rotulo();
    public string Vigencia => C.Fim is { } f ? $"{C.Inicio:dd/MM/yyyy} a {f:dd/MM/yyyy}" : $"desde {C.Inicio:dd/MM/yyyy}";
    public string Situacao => C.Ativo ? "Ativo" : "Encerrado";
}

/// <summary>
/// Contratos › Gerador de contratos: monta o contrato de prestação de serviços contábeis (cliente) ou de fornecimento (fornecedor) com os
/// dados do escritório e do cadastro próprio, permite ajustar o texto, grava numerado e gera o PDF para assinatura.
/// </summary>
public partial class ContratosViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<ContratoLinha> Lista { get; } = [];
    public ObservableCollection<Opcao<long?>> Partes { get; } = [];
    public static IReadOnlyList<Opcao<ModeloContrato>> Modelos { get; } = [new(ModeloContrato.Cliente, "Cliente: prestação de serviços contábeis")];
    public static IReadOnlyList<Opcao<Periodicidade>> Periodicidades { get; } = [.. Enum.GetValues<Periodicidade>().Select(p => new Opcao<Periodicidade>(p, p.Rotulo()))];
    public static IReadOnlyList<string> Indices => HonorariosViewModel.Indices;
    private IReadOnlyList<PessoaEscritorio> _pessoas = [];

    [ObservableProperty] private ContratoLinha? _selecionado;
    [ObservableProperty] private Opcao<ModeloContrato>? _modelo = Modelos[0];
    [ObservableProperty] private Opcao<long?>? _parte;
    [ObservableProperty] private string _objeto = GeradorContratos.ObjetoPadrao(ModeloContrato.Cliente);
    [ObservableProperty] private string _valor = "";
    [ObservableProperty] private Opcao<Periodicidade>? _periodicidade = Periodicidades[0];
    [ObservableProperty] private decimal? _dia = 10;
    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _fim;
    [ObservableProperty] private string _indice = "IPCA";
    [ObservableProperty] private string _foro = "";
    [ObservableProperty] private string _texto = "";
    [ObservableProperty] private bool _ativo = true;

    public bool PodeEditar => Contexto.Pode(Permissao.EscritorioHonorarios);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeEditar));

    partial void OnModeloChanged(Opcao<ModeloContrato>? value)
    {
        if (value is null) return;
        if (Selecionado is null) Objeto = GeradorContratos.ObjetoPadrao(value.Valor);
        PreencherPartes();
    }

    private void PreencherPartes()
    {
        var atual = Parte?.Valor;
        var cliente = Modelo?.Valor != ModeloContrato.Fornecedor;
        Partes.Clear();
        Partes.Add(new Opcao<long?>(null, cliente ? "(escolha o cliente)" : "(escolha o fornecedor)"));
        foreach (var p in _pessoas.Where(p => p.Ativo && (cliente ? p.Cliente : p.Fornecedor))) Partes.Add(new Opcao<long?>(p.Id, $"{p.Codigo} – {p.Nome}"));
        Parte = Partes.FirstOrDefault(p => p.Valor == atual) ?? Partes[0];
    }

    partial void OnSelecionadoChanged(ContratoLinha? value)
    {
        if (value?.C is not { } c) return;
        Modelo = Modelos.First(m => m.Valor == c.Modelo);
        Parte = Partes.FirstOrDefault(p => p.Valor == c.PessoaId) ?? Partes.FirstOrDefault();
        (Objeto, Valor, Dia, Inicio, Fim, Indice, Foro, Texto, Ativo) =
            (c.Objeto, Formato.Num(c.Valor), c.DiaVencimento, Ext.Dt(c.Inicio), c.Fim is { } f ? Ext.Dt(f) : null, c.IndiceReajuste, c.Foro ?? "", c.Texto, c.Ativo);
        Periodicidade = Periodicidades.First(p => p.Valor == c.Periodicidade);
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        var g = NovaGeracao();
        var contratos = await app.Escritorio().ListarContratosAsync(empresa, sessao.Id);
        var pessoas = await app.Escritorio().ListarPessoasAsync(empresa, sessao.Id);
        if (!EhAtual(g)) return;
        _pessoas = pessoas;
        PreencherPartes();
        var id = Selecionado?.C.Id;
        Lista.Clear();
        foreach (var c in contratos) Lista.Add(new ContratoLinha(c));
        Selecionado = Lista.FirstOrDefault(l => l.C.Id == id);
    }

    private ContratoEscritorio Montar(long id = 0, int numero = 0) =>
        new(id, numero, Parte?.Valor ?? 0, Modelo?.Valor ?? ModeloContrato.Cliente, Objeto, Formato.LerValor(Valor) ?? 0, Periodicidade?.Valor ?? Escritorio.Periodicidade.Mensal,
            Dia is { } d ? (int)d : null, Ext.Dia(Inicio), Fim is { } f ? DateOnly.FromDateTime(f) : null, Indice, Foro, Texto, Ativo);

    [RelayCommand]
    private void Novo()
    {
        Selecionado = null;
        Modelo = Modelos[0];
        Objeto = GeradorContratos.ObjetoPadrao(ModeloContrato.Cliente);
        (Valor, Dia, Inicio, Fim, Indice, Foro, Texto, Ativo) = ("", 10, new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), null, "IPCA", "", "", true);
        Periodicidade = Periodicidades[0];
        Mensagem = "Escolha o tipo e a parte, preencha as condições e clique em Gerar texto.";
    }

    [RelayCommand]
    private Task GerarTextoAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (_pessoas.FirstOrDefault(p => p.Id == Parte?.Valor) is not { } pessoa) { Erros.Add(Modelo?.Valor == ModeloContrato.Fornecedor ? "Escolha o fornecedor." : "Escolha o cliente."); return; }
        var esc = await app.Escritorio().DadosEscritorioAsync(empresa);
        var c = Montar(Selecionado?.C.Id ?? 0, Selecionado?.C.Numero ?? 0) with { Texto = "-" };
        var erros = RegrasEscritorio.Validar(c);
        if (erros.Count > 0) { foreach (var e in erros) Erros.Add(e); return; }
        Texto = GeradorContratos.Gerar(esc, pessoa, c);
        Mensagem = "Texto gerado. Ajuste o que precisar e clique em Gravar.";
    }, Permissao.EscritorioHonorarios);

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Escritorio().GravarContratoAsync(empresa, Montar(Selecionado?.C.Id ?? 0), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Selecionado = null;
        await CarregarAsync();
        Selecionado = Lista.FirstOrDefault(l => l.C.Numero == r.Valor);
        Mensagem = $"Contrato nº {r.Valor} gravado.";
    }, Permissao.EscritorioHonorarios);

    /// <summary>Assina o PDF digitalmente com o certificado padrão do contador (ou do responsável), em Configurações › Certificados digitais.</summary>
    [ObservableProperty] private bool _assinarDigitalmente;

    private async Task<(ContratoEscritorio C, Vorticis.Application.Relatorios.DocumentoRelatorio Doc)?> DocumentoAsync(VorticisApp app, long empresa)
    {
        if (Selecionado?.C is not { } c) { Erros.Add("Grave o contrato e selecione-o na lista para exportar."); return null; }
        if (_pessoas.FirstOrDefault(p => p.Id == c.PessoaId) is not { } pessoa) return null;
        var esc = await app.Escritorio().DadosEscritorioAsync(empresa);
        return (c, GeradorContratos.Documento(esc, pessoa, c));
    }

    [RelayCommand]
    private Task PdfAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (await DocumentoAsync(app, empresa) is not var (c, doc)) return;
        Vorticis.Application.Relatorios.AssinaturaDigitalPdf? digital = null;
        if (AssinarDigitalmente)
        {
            var a = await CertificadosPadrao.ParaPdfAsync(Contexto, $"Contrato nº {c.Numero}");
            if (!a.Sucesso) { foreach (var e in a.Erros) Erros.Add(e); return; }
            digital = a.Valor;
        }
        var pdf = new GeradorPdf().Gerar(doc with { AssinaturaDigital = digital });
        Mensagem = (digital is null ? "PDF gerado: " : "PDF assinado digitalmente: ") + ArquivosGerados.Salvar($"contrato_{c.Numero}.pdf", pdf.Conteudo);
    }, Permissao.EscritorioHonorarios);

    [RelayCommand]
    private Task WordAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (await DocumentoAsync(app, empresa) is not var (c, doc)) return;
        Mensagem = "Word gerado: " + ArquivosGerados.Salvar($"contrato_{c.Numero}.docx", GeradorDocx.Gerar(doc));
    }, Permissao.EscritorioHonorarios);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionado?.C is not { } c) { Erros.Add("Selecione o contrato na lista."); return; }
        if (!await Contexto.ConfirmarAsync("Excluir contrato", $"Excluir o contrato nº {c.Numero}? Para guardar o histórico, prefira desmarcar Ativo.")) return;
        var r = await app.Escritorio().ExcluirContratoAsync(empresa, c.Id, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Selecionado = null;
        await CarregarAsync();
        Mensagem = "Contrato excluído.";
    }, Permissao.EscritorioHonorarios);
}

// ---------------------------------------------------------------------------------------------- relatórios

/// <summary>Linha de relatório do Escritório na grade (textos já formatados).</summary>
public sealed record LinhaRelatorioEscritorio(string[] V, bool Destaque)
{
    public string V0 => V.ElementAtOrDefault(0) ?? "";
    public string V1 => V.ElementAtOrDefault(1) ?? "";
    public string V2 => V.ElementAtOrDefault(2) ?? "";
    public string V3 => V.ElementAtOrDefault(3) ?? "";
    public string V4 => V.ElementAtOrDefault(4) ?? "";
    public string V5 => V.ElementAtOrDefault(5) ?? "";
    public string V6 => V.ElementAtOrDefault(6) ?? "";
    public string V7 => V.ElementAtOrDefault(7) ?? "";
    public string V8 => V.ElementAtOrDefault(8) ?? "";
}

/// <summary>Base dos relatórios do Escritório: a tabela exportável é mostrada na grade e exportada em PDF, Excel e CSV.</summary>
public abstract partial class RelatorioEscritorioViewModel(IContexto contexto) : RelatorioViewModel(contexto)
{
    private TabelaRelatorio? _tabela;
    private string _parametros = "";

    public ObservableCollection<LinhaRelatorioEscritorio> Linhas { get; } = [];
    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private DateTime? _fim = new DateTime(DateTime.Today.Year, 12, 1);

    public abstract string Titulo { get; }
    protected override string TituloRelatorio => Titulo;
    protected override string Parametros => _parametros;
    protected override bool Paisagem => true;
    protected override IReadOnlyList<TabelaRelatorio> Tabelas() => _tabela is null ? [] : [_tabela];

    protected void Exibir(TabelaRelatorio t, string parametros)
    {
        (_tabela, _parametros) = (t, parametros);
        Linhas.Clear();
        foreach (var l in t.Linhas)
            Linhas.Add(new LinhaRelatorioEscritorio(l.Valores.Select((v, i) => ExportacaoRelatorio.Texto(v, t.Colunas[i].Tipo)).ToArray(), l.Destaque));
        TemDados = true;
        AtualizarPermissoesExportacao();
    }
}

/// <summary>Relatórios › Honorários › Relatório de honorários (cobranças das competências do período).</summary>
public sealed partial class RelHonorariosViewModel(IContexto contexto) : RelatorioEscritorioViewModel(contexto)
{
    public override string Titulo => TabelasEscritorio.TituloHonorarios;
    protected override string NomeArquivo => "relatorio_honorarios";

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var ini = RegrasEscritorio.Mes(Ext.Dia(Inicio));
        var fim = RegrasEscritorio.Mes(Ext.Dia(Fim));
        var cobrancas = await app.Escritorio().ListarCobrancasAsync(empresa, ini, false, usuario, ate: fim);
        Exibir(TabelasEscritorio.Honorarios(cobrancas, DateOnly.FromDateTime(DateTime.Today)), $"Competências de {ini:MM/yyyy} a {fim:MM/yyyy}");
        Mensagem = cobrancas.Count == 0 ? "Nenhuma cobrança nas competências escolhidas." : null;
    }, Permissao.EscritorioRelatorios);
}

// ---------------------------------------------------------------------------------------------- parâmetros

/// <summary>Cadastros › Parâmetros (Escritório): atalhos para as configurações do módulo, que ficam nas próprias telas.</summary>
public sealed class ParametrosViewModelEscritorio(IContexto contexto) : ParametrosViewModel(contexto)
{
    public override string Titulo => "Parâmetros do Escritório";
    public override string Descricao => "O Escritório tem cadastros próprios, que não são compartilhados com os outros módulos. As configurações ficam nas telas abaixo.";
    protected override Permissao PermissaoGravar => Permissao.EscritorioHonorarios;

    protected override IReadOnlyList<GrupoParametros> MontarGrupos() =>
    [
        new("Clientes", "Cadastro dos clientes, módulos do contrato e honorários.", [], [Atalho("Abrir Clientes", EscritorioTelas.Clientes)]),
        new("Honorários", "Valor, vencimento e reajuste de cada cliente (aba Honorários e cobranças do cliente).", [], [Atalho("Abrir Clientes", EscritorioTelas.Clientes)]),
        new("Contratos", "Contratos de prestação de serviços com os clientes.", [], [Atalho("Abrir Gerador de contratos", EscritorioTelas.Contratos)]),
        new("Tarefas", "Prazos, responsáveis e recorrência.", [], [Atalho("Abrir Tarefas e prazos", EscritorioTelas.Tarefas)]),
        new("Menus do programa", "Escolha os itens de menu que cada usuário vê (ou que o administrador esconde para todos).", [],
            [Atalho("Abrir Menus do programa", TelasComuns.Menus)]),
    ];

    protected override Task CarregarValoresAsync(VorticisApp app, long empresa, long usuario) => Task.CompletedTask;
    protected override Task GravarValoresAsync(VorticisApp app, long empresa, long usuario) => Task.CompletedTask;
}
