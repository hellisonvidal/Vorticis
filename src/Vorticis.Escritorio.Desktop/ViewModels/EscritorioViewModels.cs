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
    public const string Honorarios = "escritorio.honorarios";
}

internal static class Ext
{
    public static ServicoEscritorio Escritorio(this VorticisApp app) => ServicosEscritorio.De(app);
    public static DateOnly Dia(DateTime? d) => d is { } v ? DateOnly.FromDateTime(v) : DateOnly.FromDateTime(DateTime.Today);
    public static DateTime Dt(DateOnly d) => d.ToDateTime(TimeOnly.MinValue);
    private static readonly System.Globalization.CultureInfo Br = new("pt-BR");
    /// <summary>Data digitada dd/mm/aaaa; null enquanto incompleta ou inválida.</summary>
    public static DateTime? LerData(string? t) => DateTime.TryParseExact(t?.Trim(), "dd/MM/yyyy", Br, System.Globalization.DateTimeStyles.None, out var d) ? d : null;
    /// <summary>Competência digitada mm/aaaa (primeiro dia do mês); null enquanto incompleta ou inválida.</summary>
    public static DateTime? LerMes(string? t) => DateTime.TryParseExact(t?.Trim(), "MM/yyyy", Br, System.Globalization.DateTimeStyles.None, out var d) && d.Year >= 1900 ? d : null;
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
    /// <summary>Código com 4 dígitos (o escritório é o 0001), como o código das empresas.</summary>
    public string CodigoTexto => P.Codigo.ToString("0000");
    public string Nome => P.Nome;
    public string Documento => P.DocumentoFormatado;
    public string Papel => P.Papel;
    public string Contato => string.Join(" · ", new[] { P.Contato, P.Telefone, P.Email }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public string Situacao => P.Ativo ? "Ativo" : "Inativo";
    public string Fantasia => P.NomeFantasia ?? "";
    public string Regime => P.Regime ?? "";
    public string Cidade => P.Municipio is { Length: > 0 } m ? (P.Uf is { Length: > 0 } uf ? $"{m}/{uf}" : m) : "";
    public string Modulos => P.Modulos == Vorticis.Application.Cadastros.ModulosEmpresa.Nenhum ? "" : Vorticis.Application.Cadastros.ModulosEmpresaExtensoes.Nome(P.Modulos);
}

/// <summary>
/// Cadastro próprio do Escritório (Cadastros › Clientes); a marca de fornecedor de cadastros antigos é preservada ao gravar.
/// O cliente tem os dados do cadastro de empresas (importados pela consulta do CNPJ), os módulos do contrato (com algum marcado, a
/// empresa é cadastrada nos demais módulos) e o honorário, na aba Contrato.
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
        if (Contexto.ClienteEmFocoId is { } foco && Lista.FirstOrDefault(l => l.P.Id == foco) is { } emFoco) Selecionado = emFoco;   // cliente em foco na barra
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

    /// <summary>Depois de gravar o cadastro (antes de recarregar a lista): grava o que a ficha tem além do cadastro (o honorário do contrato).</summary>
    protected virtual Task AposGravarAsync(VorticisApp app, long escritorio, long pessoa, long usuario) => Task.CompletedTask;

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
            await AposGravarAsync(app, empresa, r.Valor, usuario);
            Modo = ModoCadastro.Consulta;
            await CarregarAsync();
            await Contexto.RecarregarClientesSeletorAsync();   // o seletor de clientes da barra mostra o cadastro atualizado
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
            await Contexto.RecarregarClientesSeletorAsync();
            Mensagem = "Cadastro excluído.";
        }, Permissao.EscritorioTarefas);
        return ok;
    }
}

/// <summary>
/// Cadastros › Clientes: dados cadastrais (como o cadastro de empresas) e a aba Contrato, com os módulos contratados e o honorário (valor,
/// dia do vencimento e periodicidade). As cobranças ficam em Financeiro › Honorários.
/// </summary>
public sealed partial class ClientesEscritorioViewModel(IContexto contexto) : PessoasViewModel(contexto, true)
{
    public static IReadOnlyList<Opcao<PeriodicidadeHonorario>> PeriodicidadesHonorario { get; } =
        [.. Enum.GetValues<PeriodicidadeHonorario>().Select(p => new Opcao<PeriodicidadeHonorario>(p, p.Rotulo()))];

    private ContratoHonorario? _honorario;
    [ObservableProperty] private string _valorHonorario = "";
    [ObservableProperty] private decimal? _diaHonorario = 10;
    [ObservableProperty] private Opcao<PeriodicidadeHonorario>? _periodicidadeHonorario = PeriodicidadesHonorario[0];
    [ObservableProperty] private string _situacaoHonorario = "";

    /// <summary>A tela mostra a lista de clientes; a ficha (dados e contrato) abre numa janela por cima.</summary>
    protected override bool FichaEmJanela => true;

    protected override void MostrarSelecionado()
    {
        base.MostrarSelecionado();
        MostrarHonorario();
    }

    private void MostrarHonorario()
    {
        var h = _honorario is { } x && x.PessoaId == Selecionado?.P.Id ? x : null;
        ValorHonorario = h is null ? "" : Formato.Moeda(h.Valor);
        DiaHonorario = h?.DiaVencimento ?? 10;
        PeriodicidadeHonorario = PeriodicidadesHonorario.First(p => p.Valor == (h?.Periodicidade ?? Escritorio.PeriodicidadeHonorario.Mensal));
        SituacaoHonorario = h is null ? "Sem honorário no contrato: informe o valor para as cobranças serem geradas em Financeiro › Honorários."
            : h.Periodicidade switch
            {
                Escritorio.PeriodicidadeHonorario.Mensal => $"Honorário de {Formato.Moeda(h.Valor)} por mês, vencimento no dia {h.DiaVencimento}.",
                Escritorio.PeriodicidadeHonorario.Anual => $"Honorário de {Formato.Moeda(h.Valor)} por ano, cobrado em {h.Inicio:MM} de cada ano, vencimento no dia {h.DiaVencimento}.",
                _ => $"Honorário especial de {Formato.Moeda(h.Valor)}: as cobranças são lançadas em Financeiro › Honorários (Novo honorário).",
            };
    }

    protected override async Task AoSelecionarAsync(PessoaEscritorio? p)
    {
        await CarregarLogoAsync(p?.EmpresaVinculadaId);
        _honorario = p is not null && Contexto.App is { } app && Contexto.Sessao is { } sessao && Contexto.EmpresaId is { } empresa
            ? (await app.Escritorio().ListarHonorariosAsync(empresa, sessao.Id)).FirstOrDefault(h => h.PessoaId == p.Id)
            : null;
        if (EmConsulta) MostrarHonorario();
    }

    protected override async Task AposGravarAsync(VorticisApp app, long escritorio, long pessoa, long usuario)
    {
        var valor = Formato.LerValor(ValorHonorario) ?? 0;
        var anterior = _honorario?.PessoaId == pessoa ? _honorario : null;
        Resultado r;
        if (valor > 0)
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            r = await app.Escritorio().GravarHonorarioAsync(escritorio, new ContratoHonorario(anterior?.Id ?? 0, pessoa, valor, (int)(DiaHonorario ?? 10),
                anterior?.Inicio ?? new DateOnly(hoje.Year, hoje.Month, 1), anterior?.Fim, anterior?.IndiceReajuste ?? "NENHUM", anterior?.Observacao, null,
                PeriodicidadeHonorario?.Valor ?? Escritorio.PeriodicidadeHonorario.Mensal), usuario);
        }
        else if (anterior is not null) r = await app.Escritorio().ExcluirHonorarioAsync(escritorio, pessoa, usuario);
        else return;
        if (!r.Sucesso) foreach (var e in r.Erros) Erros.Add("Honorário: " + e);
        _honorario = (await app.Escritorio().ListarHonorariosAsync(escritorio, usuario)).FirstOrDefault(h => h.PessoaId == pessoa);
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
        if (Contexto.ClienteEmFocoId is { } foco) itens = itens.Where(c => c.PessoaId == foco).ToList();   // cliente em foco na barra
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
public partial class TarefasViewModel(IContexto contexto) : ModuloViewModel(contexto), IComFicha
{
    /// <summary>A tela mostra a lista de tarefas; a ficha da tarefa abre em janela (Nova tarefa, Abrir, duplo clique ou Enter).</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TituloFicha))] private bool _fichaAberta;

    public string TituloFicha => Selecionada is { } t ? $"Tarefa: {t.Titulo}" : "Nova tarefa";

    /// <summary>A ficha é de uma tarefa já gravada (mostra Concluir e Excluir).</summary>
    public bool TarefaGravada => Selecionada is not null;

    [RelayCommand]
    private void Abrir()
    {
        if (Selecionada is null) { Erros.Add("Selecione a tarefa na lista."); return; }
        (Mensagem, FichaAberta) = (null, true);
        Erros.Clear();
    }

    /// <summary>Fechar sem gravar: a ficha volta ao que está gravado.</summary>
    [RelayCommand]
    public void FecharFicha()
    {
        FichaAberta = false;
        Mostrar(Selecionada);
    }

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
    private long? _foco;
    [ObservableProperty] private bool _somenteAbertas = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TituloFicha), nameof(TarefaGravada))] private TarefaLinha? _selecionada;
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

    partial void OnSelecionadaChanged(TarefaLinha? value) => Mostrar(value);

    private void Mostrar(TarefaLinha? value)
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
        if (Contexto.ClienteEmFocoId != _foco) filtro = _foco = Contexto.ClienteEmFocoId;   // o cliente em foco na barra vira o filtro
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
        Erros.Clear();
        FichaAberta = true;
    }

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var t = new Tarefa(Selecionada?.T.Id ?? 0, empresa, Cliente?.Valor, Titulo, string.IsNullOrWhiteSpace(Descricao) ? null : Descricao,
                           Ext.Dia(Vencimento), Responsavel?.Valor, Prioridade?.Valor ?? 2, Situacao?.Valor ?? SituacaoTarefa.Pendente, Recorrencia?.Valor ?? Escritorio.Recorrencia.Nenhuma);
        var r = await app.Escritorio().GravarTarefaAsync(t, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        FichaAberta = false;
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
        FichaAberta = false;
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
        FichaAberta = false;
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
public partial class ProtocolosViewModel(IContexto contexto) : ModuloViewModel(contexto), IComFicha
{
    /// <summary>A tela mostra os protocolos registrados; Registrar protocolo abre a ficha em janela.</summary>
    [ObservableProperty] private bool _fichaAberta;

    public string TituloFicha => "Registrar protocolo";

    [RelayCommand]
    private void Novo()
    {
        (Documento, Descricao, Pessoa, Data, Tipo) = ("", "", "", DateTime.Today, Tipos[0]);
        Erros.Clear();
        Mensagem = null;
        FichaAberta = true;
    }

    [RelayCommand]
    public void FecharFicha() => FichaAberta = false;

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
        if (Contexto.ClienteEmFocoId is { } foco) itens = itens.Where(p => p.PessoaId == foco).ToList();   // cliente em foco na barra
        var atual = Cliente?.Valor ?? Contexto.ClienteEmFocoId;
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
        FichaAberta = false;
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
    public string Tipo => C.Tipo.Rotulo();
    public string Descricao => C.Descricao ?? (C.Tipo == TipoCobranca.Honorario ? "Honorário" : "");
    public string Parcela => C.ParcelaTexto;
    public string Competencia => C.Competencia.ToString("MM/yyyy");
    public string Vencimento => C.Vencimento.ToString("dd/MM/yyyy");
    public string Valor => Formato.Moeda(C.Valor);
    public string Situacao => C.Vencida(DateOnly.FromDateTime(DateTime.Today)) ? "VENCIDA" : C.Situacao.Rotulo();
    public string Recebido => C.RecebidoEm is { } d ? $"{d:dd/MM/yyyy} – {Formato.Moeda(C.ValorRecebido ?? 0)}{(C.ObservacaoBaixa is { } o ? $" ({o})" : "")}" : "";
}

/// <summary>Parcela na prévia do lançamento (Financeiro › Honorários › Novo honorário / Cobrança avulsa).</summary>
public sealed record PreviaParcela(string Parcela, string Competencia, string Vencimento, string Valor);

public enum FiltroSituacaoCobranca { Todas, EmAberto, Vencidas, Recebidas, Canceladas }

/// <summary>
/// Financeiro › Honorários: as cobranças de honorários e avulsas de todos os clientes (ou do escolhido no topo), por competência.
/// Pesquisa pelo vencimento (de/até) e Carregar; Novo honorário (com parcelas: competência, valor, dia do vencimento e frequência), Cobrança
/// avulsa, Alterar, Baixar (recebimento), Estornar baixa, Cancelar e Excluir (uma ou várias marcadas). As parcelas dos contratos vêm do
/// Gerador de contratos (Gerar parcelas do contrato).
/// </summary>
public partial class HonorariosViewModel : ModuloViewModel
{
    public HonorariosViewModel(IContexto contexto) : base(contexto)
    {
        Lancamento = new LancamentoCobrancaViewModel(contexto, this);
        Baixa = new BaixaCobrancaViewModel(contexto, this);
    }

    public LancamentoCobrancaViewModel Lancamento { get; }
    public BaixaCobrancaViewModel Baixa { get; }
    public ObservableCollection<CobrancaLinha> Cobrancas { get; } = [];
    public static IReadOnlyList<Opcao<FiltroSituacaoCobranca>> Situacoes { get; } =
    [
        new(FiltroSituacaoCobranca.Todas, "Todas"), new(FiltroSituacaoCobranca.EmAberto, "Em aberto"), new(FiltroSituacaoCobranca.Vencidas, "Vencidas"),
        new(FiltroSituacaoCobranca.Recebidas, "Recebidas"), new(FiltroSituacaoCobranca.Canceladas, "Canceladas"),
    ];
    public static IReadOnlyList<Opcao<TipoCobranca?>> Tipos { get; } = [new(null, "Todos"), new(TipoCobranca.Honorario, "Honorários"), new(TipoCobranca.Avulsa, "Avulsas")];

    /// <summary>Período da pesquisa: vencimento de/até (datas; padrão, o ano corrente).</summary>
    [ObservableProperty] private DateTime? _vencimentoDe = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private DateTime? _vencimentoAte = new DateTime(DateTime.Today.Year, 12, 31);
    [ObservableProperty] private Opcao<FiltroSituacaoCobranca>? _situacao = Situacoes[0];
    [ObservableProperty] private Opcao<TipoCobranca?>? _tipo = Tipos[0];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TemSelecionada))] private CobrancaLinha? _selecionada;
    [ObservableProperty] private string _totais = "";
    [ObservableProperty] private string _clienteDoFiltro = "Todos os clientes";

    /// <summary>As cobranças marcadas na lista (Ctrl ou Shift + clique marcam várias); Excluir age em todas.</summary>
    public IReadOnlyList<CobrancaLinha> Marcadas { get; set; } = [];

    public bool TemSelecionada => Selecionada is not null;
    public bool PodeEditar => Contexto.Pode(Permissao.EscritorioHonorarios);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeEditar));

    partial void OnVencimentoDeChanged(DateTime? value) => _ = CarregarAsync();
    partial void OnVencimentoAteChanged(DateTime? value) => _ = CarregarAsync();
    partial void OnSituacaoChanged(Opcao<FiltroSituacaoCobranca>? value) => _ = CarregarAsync();
    partial void OnTipoChanged(Opcao<TipoCobranca?>? value) => _ = CarregarAsync();

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        var g = NovaGeracao();
        DateOnly? de = VencimentoDe is { } d ? DateOnly.FromDateTime(d) : null;
        DateOnly? ate = VencimentoAte is { } a ? DateOnly.FromDateTime(a) : null;
        if (de is { } x && ate is { } y && y < x) ate = x;
        var itens = await app.Escritorio().ListarCobrancasAsync(empresa, null, false, sessao.Id, vencimentoDe: de, vencimentoAte: ate);
        if (!EhAtual(g)) return;
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var foco = Contexto.ClienteEmFocoId;
        var lista = itens.Where(c => (foco is null || c.PessoaId == foco) && (Tipo?.Valor is not { } t || c.Tipo == t) && (Situacao?.Valor switch
        {
            FiltroSituacaoCobranca.EmAberto => c.Situacao == SituacaoCobranca.Aberta,
            FiltroSituacaoCobranca.Vencidas => c.Vencida(hoje),
            FiltroSituacaoCobranca.Recebidas => c.Situacao == SituacaoCobranca.Recebida,
            FiltroSituacaoCobranca.Canceladas => c.Situacao == SituacaoCobranca.Cancelada,
            _ => true,
        })).ToList();
        ClienteDoFiltro = foco is null ? "Todos os clientes (escolha um no topo, em Cliente:, para ver só os dele)" : $"Só do cliente escolhido no topo: {lista.FirstOrDefault()?.PessoaNome ?? "sem cobranças no período"}";
        var id = Selecionada?.C.Id;
        Marcadas = [];
        Cobrancas.Clear();
        foreach (var c in lista) Cobrancas.Add(new CobrancaLinha(c));
        Selecionada = Cobrancas.FirstOrDefault(c => c.C.Id == id);
        var validas = lista.Where(c => c.Situacao != SituacaoCobranca.Cancelada).ToList();
        Totais = $"{lista.Count} cobrança(s) · total {Formato.Moeda(validas.Sum(c => c.Valor))} · recebido {Formato.Moeda(lista.Sum(c => c.ValorRecebido ?? 0))} · "
                 + $"em aberto {Formato.Moeda(validas.Where(c => c.Situacao == SituacaoCobranca.Aberta).Sum(c => c.Valor))} · "
                 + $"vencido {Formato.Moeda(lista.Where(c => c.Vencida(hoje)).Sum(c => c.Valor))}";
    }

    [RelayCommand]
    private Task NovoHonorarioAsync() => Lancamento.AbrirAsync(TipoCobranca.Honorario);

    [RelayCommand]
    private Task CobrancaAvulsaAsync() => Lancamento.AbrirAsync(TipoCobranca.Avulsa);

    /// <summary>Abre a cobrança selecionada para alterar competência, vencimento, valor e descrição (só as em aberto).</summary>
    [RelayCommand]
    private async Task AlterarAsync()
    {
        Erros.Clear();
        if (Selecionada?.C is not { } c) { Erros.Add("Selecione a cobrança na lista."); return; }
        if (c.Situacao != SituacaoCobranca.Aberta)
        {
            Erros.Add(c.Situacao == SituacaoCobranca.Recebida ? "A cobrança já foi baixada: estorne a baixa antes de alterar." : "A cobrança está cancelada: estorne para alterá-la.");
            return;
        }
        await Lancamento.AbrirAlteracaoAsync(c);
    }

    /// <summary>Carrega (de novo) as cobranças lançadas, com os filtros da tela: as geradas pelos contratos, os honorários e as avulsas.</summary>
    [RelayCommand]
    private async Task CarregarDadosAsync()
    {
        Erros.Clear();
        await CarregarAsync();
        Mensagem = $"{Cobrancas.Count} cobrança(s) carregada(s).";
    }

    [RelayCommand]
    private void Baixar()
    {
        Erros.Clear();
        if (Selecionada?.C is not { } c) { Erros.Add("Selecione a cobrança na lista."); return; }
        if (c.Situacao != SituacaoCobranca.Aberta) { Erros.Add("Só cobranças em aberto podem ser baixadas."); return; }
        Baixa.Abrir(c);
    }

    [RelayCommand]
    private Task EstornarAsync() => Alterar(SituacaoCobranca.Aberta, "Baixa estornada: a cobrança voltou a ficar em aberto.", c => c.Situacao != SituacaoCobranca.Aberta);

    [RelayCommand]
    private Task CancelarAsync() => Alterar(SituacaoCobranca.Cancelada, "Cobrança cancelada.", c => c.Situacao == SituacaoCobranca.Aberta);

    private Task Alterar(SituacaoCobranca nova, string msg, Func<CobrancaHonorario, bool> pode) => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada?.C is not { } c) { Erros.Add("Selecione a cobrança na lista."); return; }
        if (!pode(c)) { Erros.Add(nova == SituacaoCobranca.Cancelada ? "Só cobranças em aberto podem ser canceladas." : "A cobrança já está em aberto."); return; }
        var r = await app.Escritorio().AlterarSituacaoAsync(empresa, c, nova, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = msg;
    }, Permissao.EscritorioHonorarios);

    /// <summary>Exclui as cobranças marcadas na lista (ou a selecionada), depois de confirmar; as recebidas ficam (estorne a baixa antes).</summary>
    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var alvos = Marcadas.Count > 0 ? Marcadas.Select(l => l.C).ToList() : Selecionada?.C is { } s ? [s] : [];
        if (alvos.Count == 0) { Erros.Add("Selecione a cobrança na lista (Ctrl ou Shift + clique marcam várias)."); return; }
        var podem = alvos.Where(c => c.Situacao != SituacaoCobranca.Recebida).ToList();
        var recebidas = alvos.Count - podem.Count;
        if (podem.Count == 0) { Erros.Add("As cobranças escolhidas já foram baixadas: estorne a baixa antes de excluir."); return; }
        var texto = podem.Count == 1
            ? $"Excluir a cobrança de {podem[0].PessoaNome} ({podem[0].Competencia:MM/yyyy}, {Formato.Moeda(podem[0].Valor)})?"
            : $"Excluir as {podem.Count} cobranças selecionadas (total {Formato.Moeda(podem.Sum(c => c.Valor))})?";
        if (recebidas > 0) texto += $"\n{recebidas} cobrança(s) já recebida(s) não será(ão) excluída(s): estorne a baixa antes.";
        if (!await Contexto.ConfirmarAsync(podem.Count == 1 ? "Excluir cobrança" : "Excluir cobranças", texto, "Excluir", "Cancelar")) return;
        var r = await app.Escritorio().ExcluirCobrancasAsync(empresa, podem.Select(c => c.Id).ToList(), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Selecionada = null;
        await CarregarAsync();
        Mensagem = (r.Valor == 1 ? "Cobrança excluída." : $"{r.Valor} cobranças excluídas.")
                   + (recebidas > 0 ? $" {recebidas} recebida(s) não foram excluídas (estorne a baixa antes)." : "");
    }, Permissao.EscritorioHonorarios);

    internal async Task AposLancarAsync(string mensagem)
    {
        await CarregarAsync();
        Mensagem = mensagem;
    }
}

/// <summary>
/// Janela "Novo honorário" / "Cobrança avulsa" / "Alterar cobrança": cliente, competência (digitada mm/aaaa), valor, dia do vencimento,
/// regime (competência: vence no mês seguinte; caixa: no mesmo mês), pagamento à vista (já entra recebida, vencendo na data do pagamento),
/// frequência e parcelas, com a prévia. Na alteração: competência, vencimento (digitado dd/mm/aaaa), valor e descrição.
/// </summary>
public partial class LancamentoCobrancaViewModel(IContexto contexto, HonorariosViewModel tela) : ModuloViewModel(contexto), IComFicha
{
    public ObservableCollection<Opcao<long?>> Clientes { get; } = [];
    public ObservableCollection<PreviaParcela> Previa { get; } = [];
    public static IReadOnlyList<Opcao<FrequenciaParcelas>> Frequencias { get; } =
        [.. Enum.GetValues<FrequenciaParcelas>().Select(f => new Opcao<FrequenciaParcelas>(f, f.Rotulo()))];

    private CobrancaHonorario? _alterando;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TituloFicha), nameof(EhAvulsa), nameof(DicaDescricao))] private TipoCobranca _tipo;
    [ObservableProperty] private bool _fichaAberta;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TituloFicha), nameof(EhNovo), nameof(MostraParcelamento), nameof(MostraQuantidade), nameof(MostraVencimento),
                                                   nameof(MostraDia), nameof(MostraRegime), nameof(TextoBotao))] private bool _emAlteracao;
    [ObservableProperty] private Opcao<long?>? _cliente;
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CompetenciaTexto))] private DateTime? _competencia = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private string _valor = "";
    [ObservableProperty] private decimal? _dia = 10;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(MostraQuantidade))] private Opcao<FrequenciaParcelas>? _frequencia = Frequencias[1];
    [ObservableProperty] private decimal? _quantidade = 12;
    [ObservableProperty] private string _resumo = "";
    /// <summary>Regime de competência: o vencimento fica no mês seguinte ao da competência (padrão).</summary>
    [ObservableProperty] private bool _regimeCompetencia = true;
    /// <summary>Regime de caixa: o vencimento fica no mesmo mês da competência.</summary>
    [ObservableProperty] private bool _regimeCaixa;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(MostraParcelamento), nameof(MostraQuantidade), nameof(MostraDia), nameof(MostraRegime))] private bool _aVista;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(DataPagamentoTexto))] private DateTime? _dataPagamento = DateTime.Today;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(VencimentoTexto))] private DateTime? _vencimento;

    /// <summary>Competência digitada (mm/aaaa); a data do pagamento e o vencimento, dd/mm/aaaa.</summary>
    public string CompetenciaTexto { get => Competencia?.ToString("MM/yyyy") ?? ""; set { if (Ext.LerMes(value) is { } d) Competencia = d; } }
    public string DataPagamentoTexto { get => DataPagamento?.ToString("dd/MM/yyyy") ?? ""; set { if (Ext.LerData(value) is { } d) DataPagamento = d; } }
    public string VencimentoTexto { get => Vencimento?.ToString("dd/MM/yyyy") ?? ""; set { if (Ext.LerData(value) is { } d) Vencimento = d; } }

    public string TituloFicha => EmAlteracao ? "Alterar cobrança" : Tipo == TipoCobranca.Avulsa ? "Cobrança avulsa" : "Novo honorário";
    public string TextoBotao => EmAlteracao ? "Gravar" : "Lançar";
    public bool EhAvulsa => Tipo == TipoCobranca.Avulsa;
    public bool EhNovo => !EmAlteracao;
    public string DicaDescricao => EhAvulsa ? "Serviço cobrado (ex.: abertura de empresa, IRPF, consultoria)" : "Opcional (ex.: honorário 2027)";
    public bool MostraParcelamento => !EmAlteracao && !AVista;
    public bool MostraQuantidade => MostraParcelamento && Frequencia?.Valor != FrequenciaParcelas.Unica;
    public bool MostraDia => MostraParcelamento;
    public bool MostraRegime => MostraParcelamento;
    public bool MostraVencimento => EmAlteracao;

    partial void OnClienteChanged(Opcao<long?>? value) => AtualizarPrevia();
    partial void OnCompetenciaChanged(DateTime? value) => AtualizarPrevia();
    partial void OnValorChanged(string value) => AtualizarPrevia();
    partial void OnDiaChanged(decimal? value) => AtualizarPrevia();
    partial void OnFrequenciaChanged(Opcao<FrequenciaParcelas>? value) => AtualizarPrevia();
    partial void OnQuantidadeChanged(decimal? value) => AtualizarPrevia();
    partial void OnAVistaChanged(bool value) => AtualizarPrevia();
    partial void OnDataPagamentoChanged(DateTime? value) => AtualizarPrevia();
    partial void OnVencimentoChanged(DateTime? value) => AtualizarPrevia();
    partial void OnDescricaoChanged(string value) => AtualizarPrevia();

    // as duas caixas se excluem: marcar uma desmarca a outra, e uma delas fica sempre marcada
    partial void OnRegimeCompetenciaChanged(bool value) { if (RegimeCaixa == value) RegimeCaixa = !value; AtualizarPrevia(); }
    partial void OnRegimeCaixaChanged(bool value) { if (RegimeCompetencia == value) RegimeCompetencia = !value; AtualizarPrevia(); }

    /// <summary>Abre a janela em branco para um honorário ou uma cobrança avulsa (os campos voltam ao padrão; o regime escolhido por último fica).</summary>
    public async Task AbrirAsync(TipoCobranca tipo)
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        FichaAberta = false;
        Erros.Clear();
        Mensagem = null;
        _alterando = null;
        EmAlteracao = false;
        Tipo = tipo;
        await ListaPessoas.Preencher(Clientes, app, empresa, sessao.Id, "(escolha o cliente)");
        Cliente = Clientes.FirstOrDefault(c => c.Valor == Contexto.ClienteEmFocoId) ?? Clientes[0];
        (Descricao, Valor, Dia, AVista, DataPagamento, Vencimento) = ("", "", 10, false, DateTime.Today, null);
        Competencia = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        Frequencia = tipo == TipoCobranca.Avulsa ? Frequencias[0] : Frequencias[1];
        Quantidade = tipo == TipoCobranca.Avulsa ? 1 : 12;
        // honorário: valor e dia do contrato do cliente, se houver
        if (tipo == TipoCobranca.Honorario && Cliente?.Valor is { } pessoa
            && (await app.Escritorio().ListarHonorariosAsync(empresa, sessao.Id)).FirstOrDefault(h => h.PessoaId == pessoa) is { } h)
            (Valor, Dia) = (Formato.Moeda(h.Valor), h.DiaVencimento);
        AtualizarPrevia();
        FichaAberta = true;
    }

    /// <summary>Abre a cobrança em aberto para alterar competência, vencimento, valor e descrição.</summary>
    public async Task AbrirAlteracaoAsync(CobrancaHonorario c)
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        FichaAberta = false;
        Erros.Clear();
        Mensagem = null;
        _alterando = c;
        EmAlteracao = true;
        Tipo = c.Tipo;
        await ListaPessoas.Preencher(Clientes, app, empresa, sessao.Id, "(escolha o cliente)");
        Cliente = Clientes.FirstOrDefault(x => x.Valor == c.PessoaId) ?? Clientes[0];
        (Descricao, Valor, AVista) = (c.Descricao ?? "", Formato.Moeda(c.Valor), false);
        Competencia = Ext.Dt(c.Competencia);
        Vencimento = Ext.Dt(c.Vencimento);
        AtualizarPrevia();
        FichaAberta = true;
    }

    private IReadOnlyList<CobrancaHonorario> Parcelas()
    {
        var descricao = string.IsNullOrWhiteSpace(Descricao) ? null : Descricao.Trim();
        var valor = Formato.LerValor(Valor) ?? 0;
        if (_alterando is { } c)
            return [c with { Competencia = RegrasEscritorio.Mes(Ext.Dia(Competencia)), Vencimento = Ext.Dia(Vencimento), Valor = valor, Descricao = descricao }];
        return RegrasEscritorio.Parcelas(Cliente?.Valor ?? 0, Ext.Dia(Competencia), valor, (int)(Dia ?? 10), Frequencia?.Valor ?? FrequenciaParcelas.Unica,
                                         (int)(Quantidade ?? 1), Tipo, descricao, vencimentoNoMesSeguinte: RegimeCompetencia,
                                         pagoAVistaEm: AVista ? Ext.Dia(DataPagamento) : null);
    }

    private void AtualizarPrevia()
    {
        var p = Parcelas();
        Previa.Clear();
        foreach (var c in p.Take(60)) Previa.Add(new PreviaParcela(c.ParcelaTexto is { Length: > 0 } t ? t : "única", c.Competencia.ToString("MM/yyyy"), c.Vencimento.ToString("dd/MM/yyyy"), Formato.Moeda(c.Valor)));
        Resumo = EmAlteracao ? $"Competência {p[0].Competencia:MM/yyyy}, vencimento {p[0].Vencimento:dd/MM/yyyy}, {Formato.Moeda(p[0].Valor)}."
               : AVista ? $"Pagamento à vista: a cobrança de {Formato.Moeda(p[0].Valor)} entra já recebida em {p[0].Vencimento:dd/MM/yyyy} (o vencimento é a data do pagamento)."
               : p.Count == 1 ? $"1 cobrança de {Formato.Moeda(p[0].Valor)}, vencimento {p[0].Vencimento:dd/MM/yyyy}."
               : $"{p.Count} parcelas de {Formato.Moeda(p[0].Valor)} (total {Formato.Moeda(p.Sum(x => x.Valor))}), de {p[0].Competencia:MM/yyyy} a {p[^1].Competencia:MM/yyyy}, "
                 + $"vencimento no dia {(int)(Dia ?? 10)} {(RegimeCompetencia ? "do mês seguinte ao da competência" : "do próprio mês da competência")} (nos meses mais curtos, no último dia).";
    }

    [RelayCommand]
    private Task LancarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Cliente?.Valor is null) { Erros.Add("Escolha o cliente."); return; }
        if (Competencia is null) { Erros.Add("Informe a competência (mês/ano)."); return; }
        if (_alterando is not null)
        {
            if (Vencimento is null) { Erros.Add("Informe o vencimento (dia/mês/ano)."); return; }
            var ra = await app.Escritorio().AlterarCobrancaAsync(empresa, Parcelas()[0], usuario);
            if (!ra.Sucesso) { MostrarResultado(ra, ""); return; }
            FichaAberta = false;
            await tela.AposLancarAsync("Cobrança alterada.");
            return;
        }
        if (AVista && DataPagamento is null) { Erros.Add("Informe a data do pagamento."); return; }
        if (!AVista && Dia is not (>= 1 and <= 31)) { Erros.Add("Dia do vencimento: de 1 a 31."); return; }
        var r = await app.Escritorio().LancarCobrancasAsync(empresa, Parcelas(), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        FichaAberta = false;
        await tela.AposLancarAsync(AVista ? $"{TituloFicha}: pagamento à vista lançado e já baixado."
                                   : r.Valor == 1 ? $"{TituloFicha}: cobrança lançada." : $"{TituloFicha}: {r.Valor} parcelas lançadas.");
    }, Permissao.EscritorioHonorarios);

    [RelayCommand]
    public void FecharFicha() => FichaAberta = false;
}

/// <summary>Janela "Baixar cobrança": data e valor recebidos e uma observação (forma de pagamento, juros, desconto...).</summary>
public partial class BaixaCobrancaViewModel(IContexto contexto, HonorariosViewModel tela) : ModuloViewModel(contexto), IComFicha
{
    private CobrancaHonorario? _cobranca;
    [ObservableProperty] private bool _fichaAberta;
    [ObservableProperty] private string _descricaoCobranca = "";
    [ObservableProperty] private DateTime? _data = DateTime.Today;
    [ObservableProperty] private string _valor = "";
    [ObservableProperty] private string _observacao = "";

    public string TituloFicha => "Baixar cobrança";

    public void Abrir(CobrancaHonorario c)
    {
        _cobranca = c;
        Erros.Clear();
        Mensagem = null;
        DescricaoCobranca = $"{c.PessoaNome} · {(c.Descricao ?? c.Tipo.Rotulo())}{(c.ParcelaTexto is { Length: > 0 } p ? $" (parcela {p})" : "")} · competência {c.Competencia:MM/yyyy} · "
                            + $"vencimento {c.Vencimento:dd/MM/yyyy} · {Formato.Moeda(c.Valor)}";
        (Data, Valor, Observacao) = (DateTime.Today, Formato.Moeda(c.Valor), "");
        FichaAberta = true;
    }

    [RelayCommand]
    private Task ConfirmarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (_cobranca is not { } c) return;
        var r = await app.Escritorio().ReceberAsync(empresa, c, Ext.Dia(Data), Formato.LerValor(Valor) ?? 0, usuario, observacao: Observacao);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        FichaAberta = false;
        await tela.AposLancarAsync($"Baixa registrada: {Formato.Moeda(Formato.LerValor(Valor) ?? 0)} em {Ext.Dia(Data):dd/MM/yyyy}.");
    }, Permissao.EscritorioHonorarios);

    [RelayCommand]
    public void FecharFicha() => FichaAberta = false;
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
public partial class ContratosViewModel(IContexto contexto) : ModuloComFichaViewModel(contexto)
{
    /// <summary>A tela mostra a lista; a ficha do registro abre numa janela por cima.</summary>
    public override string TituloFicha => Selecionado is { } s ? $"Contrato nº {s.Numero}" : "Novo contrato";
    protected override bool HaRegistroParaAbrir => Selecionado is not null;
    protected override string AvisoSelecione => "Selecione o contrato na lista.";
    protected override void AoDescartarFicha() { if (Selecionado is { } s) OnSelecionadoChanged(s); else LimparFormulario(); }

    public ObservableCollection<ContratoLinha> Lista { get; } = [];
    public ObservableCollection<Opcao<long?>> Partes { get; } = [];
    public static IReadOnlyList<Opcao<ModeloContrato>> Modelos { get; } = [new(ModeloContrato.Cliente, "Cliente: prestação de serviços contábeis")];
    public static IReadOnlyList<Opcao<Periodicidade>> Periodicidades { get; } = [.. Enum.GetValues<Periodicidade>().Select(p => new Opcao<Periodicidade>(p, p.Rotulo()))];
    public static IReadOnlyList<string> Indices { get; } = ["NENHUM", "IPCA", "IGPM", "INPC", "SALARIO"];
    private IReadOnlyList<PessoaEscritorio> _pessoas = [];

    [ObservableProperty] private ContratoLinha? _selecionado;
    [ObservableProperty] private Opcao<ModeloContrato>? _modelo = Modelos[0];
    [ObservableProperty] private Opcao<long?>? _parte;
    [ObservableProperty] private string _objeto = GeradorContratos.ObjetoPadrao(ModeloContrato.Cliente);
    [ObservableProperty] private string _valor = "";
    [ObservableProperty] private Opcao<Periodicidade>? _periodicidade = Periodicidades[0];
    [ObservableProperty] private decimal? _dia = 10;
    [ObservableProperty] private DateTime? _inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _fim = Ext.Dt(RegrasEscritorio.FimPeloPrazo(new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1), 12)!.Value);
    /// <summary>Prazo do contrato em meses (padrão 12; 0 = indeterminado): preenche o fim da vigência e diz quantas parcelas o contrato gera.</summary>
    [ObservableProperty] private decimal? _prazoMeses = 12;
    /// <summary>Vencimento das parcelas geradas: no mês seguinte ao da competência (padrão) ou no mesmo mês (regime de caixa).</summary>
    [ObservableProperty] private bool _regimeCompetencia = true;
    [ObservableProperty] private bool _regimeCaixa;
    private bool _carregandoFicha;
    [ObservableProperty] private string _indice = "IPCA";
    [ObservableProperty] private string _foro = "";
    [ObservableProperty] private string _texto = "";
    [ObservableProperty] private bool _ativo = true;

    public bool PodeEditar => Contexto.Pode(Permissao.EscritorioHonorarios);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeEditar));

    partial void OnPrazoMesesChanged(decimal? value) => AjustarFim();
    partial void OnInicioChanged(DateTime? value) => AjustarFim();
    partial void OnRegimeCompetenciaChanged(bool value) { if (RegimeCaixa == value) RegimeCaixa = !value; }
    partial void OnRegimeCaixaChanged(bool value) { if (RegimeCompetencia == value) RegimeCompetencia = !value; }

    private void AjustarFim()
    {
        if (_carregandoFicha || Inicio is null || PrazoMeses is not { } p) return;
        Fim = RegrasEscritorio.FimPeloPrazo(Ext.Dia(Inicio), (int)p) is { } f ? Ext.Dt(f) : null;
    }

    /// <summary>Prazo usado nas parcelas: o informado ou, sem prazo (indeterminado), 12 meses.</summary>
    private int PrazoParaParcelas => PrazoMeses is { } p and > 0 ? (int)p : 12;

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
        Partes.Add(new Opcao<long?>(null, cliente ? "(escolha o contratante)" : "(escolha o fornecedor)"));
        foreach (var p in _pessoas.Where(p => p.Ativo && (cliente ? p.Cliente : p.Fornecedor))) Partes.Add(new Opcao<long?>(p.Id, $"{p.Codigo} – {p.Nome}"));
        Parte = Partes.FirstOrDefault(p => p.Valor == atual) ?? Partes[0];
    }

    partial void OnSelecionadoChanged(ContratoLinha? value)
    {
        if (value?.C is not { } c) return;
        Modelo = Modelos.First(m => m.Valor == c.Modelo);
        Parte = Partes.FirstOrDefault(p => p.Valor == c.PessoaId) ?? Partes.FirstOrDefault();
        _carregandoFicha = true;
        try
        {
            (Objeto, Valor, Dia, Inicio, Fim, Indice, Foro, Texto, Ativo) =
                (c.Objeto, Formato.Num(c.Valor), c.DiaVencimento, Ext.Dt(c.Inicio), c.Fim is { } f ? Ext.Dt(f) : null, c.IndiceReajuste, c.Foro ?? "", c.Texto, c.Ativo);
            PrazoMeses = RegrasEscritorio.PrazoMeses(c.Inicio, c.Fim) ?? 0;
        }
        finally { _carregandoFicha = false; }
        Periodicidade = Periodicidades.First(p => p.Valor == c.Periodicidade);
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        var g = NovaGeracao();
        var contratos = await app.Escritorio().ListarContratosAsync(empresa, sessao.Id);
        var pessoas = await app.Escritorio().ListarPessoasAsync(empresa, sessao.Id);
        if (!EhAtual(g)) return;
        if (Contexto.ClienteEmFocoId is { } foco) contratos = contratos.Where(c => c.PessoaId == foco).ToList();   // cliente em foco na barra
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
        LimparFormulario();
        // o contratante já vem com o cliente escolhido no topo (Cliente:)
        if (Partes.FirstOrDefault(p => p.Valor is not null && p.Valor == Contexto.ClienteEmFocoId) is { } foco) Parte = foco;
        AbrirFichaNova();
        Mensagem = "Confira o contratante, preencha as condições e clique em Gerar texto.";
    }

    private void LimparFormulario()
    {
        Selecionado = null;
        Modelo = Modelos[0];
        Objeto = GeradorContratos.ObjetoPadrao(ModeloContrato.Cliente);
        (Valor, Dia, Indice, Foro, Texto, Ativo) = ("", 10, "IPCA", "", "", true);
        Inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        PrazoMeses = 12;
        AjustarFim();
        Periodicidade = Periodicidades[0];
    }

    /// <summary>
    /// Gera as parcelas do contrato (valor, periodicidade, dia e prazo, a partir do mês do início) e lança no Financeiro › Honorários, depois
    /// de confirmar. Recusa se o contratante já tem honorário em alguma das competências.
    /// </summary>
    [RelayCommand]
    private Task GerarParcelasAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Parte?.Valor is null) { Erros.Add("Escolha o contratante."); return; }
        var c = Montar(Selecionado?.C.Id ?? 0, Selecionado?.C.Numero ?? 0) with { Texto = "-" };
        if (c.Valor <= 0) { Erros.Add("Informe o valor do contrato."); return; }
        if (c.DiaVencimento is not (>= 1 and <= 31)) { Erros.Add("Informe o dia do vencimento (1 a 31)."); return; }
        var parcelas = RegrasEscritorio.ParcelasDoContrato(c, PrazoParaParcelas, RegimeCompetencia);
        var texto = parcelas.Count == 1
            ? $"Lançar 1 cobrança de {Formato.Moeda(parcelas[0].Valor)} ({parcelas[0].Competencia:MM/yyyy}, vencimento {parcelas[0].Vencimento:dd/MM/yyyy}) em Financeiro › Honorários?"
            : $"Lançar {parcelas.Count} parcelas de {Formato.Moeda(parcelas[0].Valor)} (total {Formato.Moeda(parcelas.Sum(p => p.Valor))}), de {parcelas[0].Competencia:MM/yyyy} a "
              + $"{parcelas[^1].Competencia:MM/yyyy}, vencendo de {parcelas[0].Vencimento:dd/MM/yyyy} a {parcelas[^1].Vencimento:dd/MM/yyyy}, em Financeiro › Honorários?";
        if (!await Contexto.ConfirmarAsync("Gerar parcelas do contrato", texto, "Gerar parcelas", "Cancelar")) return;
        var r = await app.Escritorio().LancarCobrancasAsync(empresa, parcelas, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Mensagem = $"{r.Valor} parcela(s) lançada(s) em Financeiro › Honorários.";
    }, Permissao.EscritorioHonorarios);

    [RelayCommand]
    private Task GerarTextoAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (_pessoas.FirstOrDefault(p => p.Id == Parte?.Valor) is not { } pessoa) { Erros.Add(Modelo?.Valor == ModeloContrato.Fornecedor ? "Escolha o fornecedor." : "Escolha o contratante."); return; }
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
        FichaAberta = false;
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
        new("Clientes", "Cadastro dos clientes, com o honorário e os módulos na aba Contrato.", [], [Atalho("Abrir Clientes", EscritorioTelas.Clientes)]),
        new("Honorários", "Honorários com parcelas, cobranças avulsas e baixas (Financeiro › Honorários).", [], [Atalho("Abrir Honorários", EscritorioTelas.Honorarios)]),
        new("Contratos", "Contratos de prestação de serviços com os clientes.", [], [Atalho("Abrir Gerador de contratos", EscritorioTelas.Contratos)]),
        new("Tarefas", "Prazos, responsáveis e recorrência.", [], [Atalho("Abrir Tarefas e prazos", EscritorioTelas.Tarefas)]),
        new("Menus do programa", "Escolha os itens de menu que cada usuário vê (ou que o administrador esconde para todos).", [],
            [Atalho("Abrir Menus do programa", TelasComuns.Menus)]),
    ];

    protected override Task CarregarValoresAsync(VorticisApp app, long empresa, long usuario) => Task.CompletedTask;
    protected override Task GravarValoresAsync(VorticisApp app, long empresa, long usuario) => Task.CompletedTask;
}
