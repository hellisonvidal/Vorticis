using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Relatorios;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Folha.Cadastros;
using Vorticis.Folha.Regras;

namespace Vorticis.Folha.Desktop.ViewModels;

/// <summary>
/// Cálculos › Folha › Empréstimos consignados: contratos dos funcionários, lançamento das parcelas da competência como desconto
/// e a margem consignável (tabela exportável).
/// </summary>
public partial class ConsignadoViewModel(IContexto contexto) : RelatorioViewModel(contexto)
{
    public static IReadOnlyList<string> Situacoes { get; } = ["ATIVO", "QUITADO", "CANCELADO"];

    public ObservableCollection<ContratoConsignado> Contratos { get; } = [];
    public ObservableCollection<Funcionario> Funcionarios { get; } = [];

    [ObservableProperty] private ContratoConsignado? _selecionado;
    [ObservableProperty] private Funcionario? _funcionario;
    [ObservableProperty] private string _instituicao = "";
    [ObservableProperty] private string _contrato = "";
    [ObservableProperty] private string _valorParcela = "";
    [ObservableProperty] private decimal? _parcelas = 12;
    [ObservableProperty] private DateTime? _primeiraCompetencia = FolhaExtensoes.Mes(contexto.PeriodoTrabalho);
    [ObservableProperty] private string _situacao = "ATIVO";
    [ObservableProperty] private string _observacao = "";
    [ObservableProperty] private DateTime? _competencia = FolhaExtensoes.Mes(contexto.PeriodoTrabalho);
    [ObservableProperty] private TabelaRelatorio? _tabela;
    private long _id;

    public bool PodeLancar => Contexto.Pode(Permissao.FolhaEventosLancar);
    public void AtualizarPermissoes() { OnPropertyChanged(nameof(PodeLancar)); AtualizarPermissoesExportacao(); }

    protected override string TituloRelatorio => "Margem consignável";
    protected override string NomeArquivo => $"margem_consignavel_{FolhaExtensoes.InicioMes(Competencia):yyyyMM}";
    protected override string Parametros => $"Competência {FolhaExtensoes.InicioMes(Competencia):MM/yyyy}";
    protected override bool Paisagem => true;
    protected override IReadOnlyList<TabelaRelatorio> Tabelas() => Tabela is null ? [] : [Tabela];

    public void Limpar() { Contratos.Clear(); Tabela = null; TemDados = false; Novo(); }

    partial void OnSelecionadoChanged(ContratoConsignado? value)
    {
        if (value is null) return;
        _id = value.Id;
        Funcionario = Funcionarios.FirstOrDefault(f => f.Id == value.FuncionarioId);
        Instituicao = value.Instituicao; Contrato = value.Contrato; ValorParcela = Formato.Num(value.ValorParcela);
        Parcelas = value.TotalParcelas; PrimeiraCompetencia = value.PrimeiraCompetencia.ToDateTime(TimeOnly.MinValue);
        Situacao = value.Situacao; Observacao = value.Observacao ?? "";
    }

    [RelayCommand]
    private void Novo()
    {
        _id = 0; Selecionado = null; Instituicao = Contrato = ValorParcela = Observacao = ""; Parcelas = 12; Situacao = "ATIVO";
        PrimeiraCompetencia = FolhaExtensoes.Mes(Contexto.PeriodoTrabalho);
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var s = app.Folha();
        var contratos = await s.Consignado.ListarAsync(empresa, sessao.Id);
        var funcionarios = await s.Cadastros.ListarFuncionariosAsync(empresa, sessao.Id);
        var fid = Funcionario?.Id;
        Funcionarios.Clear(); foreach (var f in funcionarios.Where(f => f.Ativo)) Funcionarios.Add(f);
        Funcionario = Funcionarios.FirstOrDefault(f => f.Id == fid);
        Contratos.Clear(); foreach (var c in contratos) Contratos.Add(c);
    }

    [RelayCommand]
    private Task GravarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Funcionario is null) { Erros.Add("Escolha o funcionário."); return; }
        var c = new ContratoConsignado(_id, Funcionario.Id, Instituicao, Contrato, Numeros.LerValor(ValorParcela) ?? 0, (int)(Parcelas ?? 0),
                                       FolhaExtensoes.InicioMes(PrimeiraCompetencia), Situacao, Observacao);
        var r = await app.Folha().Consignado.GravarAsync(empresa, c, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Novo();
        Mensagem = "Contrato gravado.";
    }, Permissao.FolhaEventosLancar);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionado is not { } c) { Erros.Add("Selecione o contrato na lista."); return; }
        if (!await Contexto.ConfirmarAsync("Excluir contrato", $"Excluir o contrato {c.Contrato} de {c.FuncionarioNome}?")) return;
        var r = await app.Folha().Consignado.ExcluirAsync(empresa, c.Id, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Novo();
        Mensagem = "Contrato excluído.";
    }, Permissao.FolhaEventosLancar);

    [RelayCommand]
    private Task LancarParcelasAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var comp = FolhaExtensoes.InicioMes(Competencia);
        var r = await app.Folha().Consignado.LancarParcelasAsync(empresa, comp, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        foreach (var a in r.Valor.Avisos) Erros.Add(a);
        await CarregarAsync();
        Mensagem = r.Valor.Lancadas == 0
            ? $"Nenhuma parcela a lançar em {comp:MM/yyyy}."
            : $"{r.Valor.Lancadas} parcela(s) lançada(s) em Eventos variáveis de {comp:MM/yyyy}, total {Formato.Moeda(r.Valor.Total)}. Calcule a folha.";
    }, Permissao.FolhaEventosLancar);

    [RelayCommand]
    private Task MargemAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Folha().Consignado.MargensAsync(empresa, FolhaExtensoes.InicioMes(Competencia), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var V = TipoColuna.Valor;
        var linhas = r.Valor.Select(m => new LinhaRelatorio([m.Matricula, m.Nome, m.Remuneracao, m.DescontosObrigatorios, m.Disponivel, m.Margem, m.Comprometido, m.Livre],
                                                           Destaque: m.Livre < 0)).ToList();
        Tabela = new TabelaRelatorio("Margem consignável",
            [new("Matrícula", TipoColuna.Texto, 1.8), new("Funcionário"), new("Remuneração", V), new("INSS, IRRF e pensão", V), new("Disponível", V),
             new("Margem (empréstimos)", V), new("Parcelas ativas", V), new("Margem livre", V)],
            linhas, "Remuneração da última folha mensal calculada (ou o salário do cadastro). Em destaque, quem passou da margem.");
        TemDados = linhas.Count > 0;
        AtualizarPermissoesExportacao();
    }, Permissao.FolhaRelatorios);
}
