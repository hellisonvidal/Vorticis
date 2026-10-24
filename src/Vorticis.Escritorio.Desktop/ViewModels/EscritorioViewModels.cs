using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra;

namespace Vorticis.Escritorio.Desktop.ViewModels;

public static class EscritorioTelas
{
    public const string Carteira = "escritorio.carteira";
    public const string Tarefas = "escritorio.tarefas";
    public const string Protocolos = "escritorio.protocolos";
    public const string Honorarios = "escritorio.honorarios";
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

// ---------------------------------------------------------------------------------------------- carteira

public sealed record ClienteLinha(ClienteEscritorio C)
{
    public string Cnpj => Vorticis.Domain.Cadastros.Cnpj.Formatar(C.Cnpj);
    public string RazaoSocial => C.RazaoSocial;
    public string Regime => C.Regime switch { "SIMPLES_NACIONAL" => "Simples Nacional", "LUCRO_PRESUMIDO" => "Lucro Presumido", "LUCRO_REAL" => "Lucro Real", "MEI" => "MEI", null => "", var r => r };
    public string Tarefas => C.TarefasAtrasadas > 0 ? $"{C.TarefasPendentes} ({C.TarefasAtrasadas} atrasada(s))" : C.TarefasPendentes.ToString();
    public string Honorario => C.HonorarioMensal == 0 ? "" : Formato.Moeda(C.HonorarioMensal);
    public string EmAberto => C.HonorariosEmAberto == 0 ? "" : Formato.Moeda(C.HonorariosEmAberto);
    public string Certificado => C.CertificadoVence is { } v ? v.ToString("dd/MM/yyyy") : "sem certificado";
    public string Modulos => C.Modulos;
}

/// <summary>Escritório › Carteira de clientes: todas as empresas do escritório com tarefas, honorários e certificado.</summary>
public partial class CarteiraViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<ClienteLinha> Lista { get; } = [];
    [ObservableProperty] private string _totais = "";

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao) return;
        var g = NovaGeracao();
        var itens = await app.Escritorio().CarteiraAsync(sessao.Id, DateOnly.FromDateTime(DateTime.Today));
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
    public string Empresa => T.EmpresaNome ?? "";
    public string Titulo => T.Titulo;
    public string Responsavel => T.ResponsavelNome ?? "";
    public string Prioridade => T.Prioridade switch { 1 => "Alta", 3 => "Baixa", _ => "Normal" };
    public string Situacao => T.Atrasada(DateOnly.FromDateTime(DateTime.Today)) ? "ATRASADA" : T.Situacao.Rotulo();
    public string Repete => T.Recorrencia.Rotulo();
}

/// <summary>Escritório › Tarefas e prazos: tarefas da empresa ativa (ou de todas), com responsável, prioridade e recorrência.</summary>
public partial class TarefasViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<TarefaLinha> Lista { get; } = [];
    public ObservableCollection<Opcao<long?>> Responsaveis { get; } = [];
    public static IReadOnlyList<Opcao<int>> Prioridades { get; } = [new(1, "Alta"), new(2, "Normal"), new(3, "Baixa")];
    public static IReadOnlyList<Opcao<SituacaoTarefa>> Situacoes { get; } =
        [.. Enum.GetValues<SituacaoTarefa>().Select(s => new Opcao<SituacaoTarefa>(s, s.Rotulo()))];
    public static IReadOnlyList<Opcao<Recorrencia>> Recorrencias { get; } =
        [.. Enum.GetValues<Recorrencia>().Select(r => new Opcao<Recorrencia>(r, r.Rotulo()))];

    [ObservableProperty] private bool _todasEmpresas;
    [ObservableProperty] private bool _somenteAbertas = true;
    [ObservableProperty] private TarefaLinha? _selecionada;
    [ObservableProperty] private string _titulo = "";
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private DateTime? _vencimento = DateTime.Today;
    [ObservableProperty] private Opcao<long?>? _responsavel;
    [ObservableProperty] private Opcao<int>? _prioridade = Prioridades[1];
    [ObservableProperty] private Opcao<SituacaoTarefa>? _situacao = Situacoes[0];
    [ObservableProperty] private Opcao<Recorrencia>? _recorrencia = Recorrencias[0];

    public bool PodeEditar => Contexto.Pode(Permissao.EscritorioTarefas);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeEditar));

    partial void OnTodasEmpresasChanged(bool value) => _ = CarregarAsync();
    partial void OnSomenteAbertasChanged(bool value) => _ = CarregarAsync();

    partial void OnSelecionadaChanged(TarefaLinha? value)
    {
        if (value?.T is not { } t) return;
        (Titulo, Descricao, Vencimento) = (t.Titulo, t.Descricao ?? "", Ext.Dt(t.Vencimento));
        Responsavel = Responsaveis.FirstOrDefault(r => r.Valor == t.ResponsavelId) ?? Responsaveis.FirstOrDefault();
        Prioridade = Prioridades.First(p => p.Valor == t.Prioridade);
        Situacao = Situacoes.First(s => s.Valor == t.Situacao);
        Recorrencia = Recorrencias.First(r => r.Valor == t.Recorrencia);
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao) return;
        var g = NovaGeracao();
        var itens = await app.Escritorio().ListarTarefasAsync(TodasEmpresas ? null : Contexto.EmpresaId, SomenteAbertas, sessao.Id);
        var usuarios = await app.Escritorio().ListarUsuariosAsync();
        if (!EhAtual(g)) return;
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
        Responsavel = Responsaveis.FirstOrDefault(r => r.Valor == Contexto.Sessao?.Id) ?? Responsaveis.FirstOrDefault();
        Mensagem = "Preencha a tarefa e clique em Gravar.";
    }

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var t = new Tarefa(Selecionada?.T.Id ?? 0, Selecionada?.T.EmpresaId ?? empresa, Titulo, string.IsNullOrWhiteSpace(Descricao) ? null : Descricao,
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
        var r = await app.Escritorio().ExcluirTarefaAsync(t.EmpresaId, t.Id, usuario);
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
    public string Tipo => P.Tipo == TipoProtocolo.Entrega ? "Entrega ao cliente" : "Recebido do cliente";
    public string Documento => P.Documento;
    public string Pessoa => P.Pessoa;
    public string Usuario => P.UsuarioNome ?? "";
}

/// <summary>Escritório › Protocolo de documentos: entregas e recebimentos de documentos da empresa ativa, numerados.</summary>
public partial class ProtocolosViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<ProtocoloLinha> Lista { get; } = [];
    public static IReadOnlyList<Opcao<TipoProtocolo>> Tipos { get; } = [new(TipoProtocolo.Entrega, "Entrega ao cliente"), new(TipoProtocolo.Recebimento, "Recebido do cliente")];

    [ObservableProperty] private ProtocoloLinha? _selecionado;
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
        Lista.Clear();
        foreach (var p in itens) Lista.Add(new ProtocoloLinha(p));
    }

    [RelayCommand]
    private Task IncluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Escritorio().IncluirProtocoloAsync(new Protocolo(0, empresa, 0, Ext.Dia(Data), Tipo?.Valor ?? TipoProtocolo.Entrega, Documento,
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
    public string Empresa => C.EmpresaNome ?? "";
    public string Competencia => C.Competencia.ToString("MM/yyyy");
    public string Vencimento => C.Vencimento.ToString("dd/MM/yyyy");
    public string Valor => Formato.Moeda(C.Valor);
    public string Situacao => C.Vencida(DateOnly.FromDateTime(DateTime.Today)) ? "VENCIDA" : C.Situacao.Rotulo();
    public string Recebido => C.RecebidoEm is { } d ? $"{d:dd/MM/yyyy} – {Formato.Moeda(C.ValorRecebido ?? 0)}" : "";
}

/// <summary>Honorários › Contratos e cobranças: contrato da empresa ativa e as cobranças mensais de todas as empresas.</summary>
public partial class HonorariosViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<CobrancaLinha> Cobrancas { get; } = [];
    public static IReadOnlyList<string> Indices { get; } = ["NENHUM", "IPCA", "IGPM", "INPC", "SALARIO"];

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

    partial void OnCompetenciaChanged(DateTime? value) => _ = CarregarCobrancasAsync();
    partial void OnSomenteAbertasChanged(bool value) => _ = CarregarCobrancasAsync();
    partial void OnSelecionadaChanged(CobrancaLinha? value) => ValorRecebido = value is null ? "" : Formato.Num(value.C.Valor);

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        var c = (await app.Escritorio().ListarContratosAsync(sessao.Id)).FirstOrDefault(x => x.EmpresaId == empresa);
        if (c is null)
        {
            (Valor, Dia, Fim, Indice, Observacao) = ("", 10, null, "NENHUM", "");
            SituacaoContrato = "A empresa ativa ainda não tem contrato de honorários.";
        }
        else
        {
            (Valor, Dia, Inicio, Fim, Indice, Observacao) = (Formato.Num(c.Valor), c.DiaVencimento, Ext.Dt(c.Inicio), c.Fim is { } f ? Ext.Dt(f) : null, c.IndiceReajuste, c.Observacao ?? "");
            SituacaoContrato = $"Contrato: {Formato.Moeda(c.Valor)} por mês, vencimento dia {c.DiaVencimento}.";
        }
        await CarregarCobrancasAsync();
    }

    private async Task CarregarCobrancasAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao) return;
        var g = NovaGeracao();
        var itens = await app.Escritorio().ListarCobrancasAsync(SomenteAbertas ? null : Ext.Dia(Competencia), SomenteAbertas, sessao.Id);
        if (!EhAtual(g)) return;
        Cobrancas.Clear();
        foreach (var c in itens) Cobrancas.Add(new CobrancaLinha(c));
        Totais = $"{itens.Count} cobrança(s) · total {Formato.Moeda(itens.Where(c => c.Situacao != SituacaoCobranca.Cancelada).Sum(c => c.Valor))} · "
                 + $"recebido {Formato.Moeda(itens.Sum(c => c.ValorRecebido ?? 0))} · em aberto {Formato.Moeda(itens.Where(c => c.Situacao == SituacaoCobranca.Aberta).Sum(c => c.Valor))}";
    }

    [RelayCommand]
    private Task GravarContratoAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Escritorio().GravarContratoAsync(new ContratoHonorario(0, empresa, Formato.LerValor(Valor) ?? 0, (int)(Dia ?? 0), Ext.Dia(Inicio),
                                                                                 Fim is { } f ? DateOnly.FromDateTime(f) : null, Indice, string.IsNullOrWhiteSpace(Observacao) ? null : Observacao), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = "Contrato gravado.";
    }, Permissao.EscritorioHonorarios);

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Escritorio().GerarCobrancasAsync(Ext.Dia(Competencia), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        SomenteAbertas = false;
        await CarregarCobrancasAsync();
        Mensagem = r.Valor == 0 ? "Nenhuma cobrança nova: todas as empresas com contrato já têm a cobrança deste mês." : $"{r.Valor} cobrança(s) gerada(s).";
    }, Permissao.EscritorioHonorarios);

    [RelayCommand]
    private Task ReceberAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada?.C is not { } c) { Erros.Add("Selecione a cobrança na lista."); return; }
        var r = await app.Escritorio().ReceberAsync(c, Ext.Dia(DataRecebimento), Formato.LerValor(ValorRecebido) ?? 0, usuario);
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
        var r = await app.Escritorio().AlterarSituacaoAsync(c, nova, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarCobrancasAsync();
        Mensagem = msg;
    }, Permissao.EscritorioHonorarios);
}

// ---------------------------------------------------------------------------------------------- parâmetros

/// <summary>Cadastros › Parâmetros (Escritório): atalhos para as configurações do módulo, que ficam nas próprias telas.</summary>
public sealed class ParametrosViewModelEscritorio(IContexto contexto) : ParametrosViewModel(contexto)
{
    public override string Titulo => "Parâmetros do Escritório";
    public override string Descricao => "O Escritório usa os cadastros comuns da suíte. As configurações ficam nas telas abaixo.";
    protected override Permissao PermissaoGravar => Permissao.EscritorioHonorarios;

    protected override IReadOnlyList<GrupoParametros> MontarGrupos() =>
    [
        new("Honorários", "Valor, vencimento e reajuste de cada empresa ficam no contrato.", [], [Atalho("Abrir Contratos e cobranças", EscritorioTelas.Honorarios)]),
        new("Tarefas", "Prazos, responsáveis e recorrência.", [], [Atalho("Abrir Tarefas e prazos", EscritorioTelas.Tarefas)]),
        new("Cadastros comuns", "Empresas, sócios, contadores, clientes e fornecedores valem em todos os módulos.", [], [Atalho("Abrir Empresas", TelasComuns.Empresas)]),
    ];

    protected override Task CarregarValoresAsync(VorticisApp app, long empresa, long usuario) => Task.CompletedTask;
    protected override Task GravarValoresAsync(VorticisApp app, long empresa, long usuario) => Task.CompletedTask;
}
