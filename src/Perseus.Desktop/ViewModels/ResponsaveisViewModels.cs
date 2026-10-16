using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Application.Cadastros;
using Perseus.Application.Relatorios;
using Perseus.Domain.Cadastros;
using Perseus.Domain.Seguranca;

namespace Perseus.Desktop.ViewModels;

public sealed record VinculoContabilLinha(VinculoContabil Vinculo)
{
    public string Tipo => Vinculo.Tipo;
    public string Nome => Vinculo.Nome;
    public string Documento => Vinculo.ContadorId is not null ? Cpf.Formatar(Vinculo.Documento) : ServicoLivros.FormatarCnpj(Vinculo.Documento);
    public string Crc => Vinculo.Crc ?? "";
    public string Inicio => Vinculo.Inicio.ToString("dd/MM/yyyy");
    public string Fim => Vinculo.Fim?.ToString("dd/MM/yyyy") ?? "vigente";
    public string Responsavel => Vinculo.ResponsavelTecnico ? "✓ responsável técnico" : "";
}

/// <summary>Cadastros › Contadores (contador responsável e escritório contábil) da empresa selecionada; também é a aba Contadores de Cadastros › Empresas.</summary>
public partial class ContabilidadeEmpresaViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<VinculoContabilLinha> Vinculos { get; } = [];
    public ObservableCollection<Contador> ContadoresCadastrados { get; } = [];
    public ObservableCollection<EscritorioContabil> EscritoriosCadastrados { get; } = [];
    public static IReadOnlyList<string> Ufs { get; } = Uf.Todas.Order().ToArray();

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TemSelecionado))] private VinculoContabilLinha? _selecionado;
    [ObservableProperty] private DateTime? _dataFim = DateTime.Today;

    // contador
    [ObservableProperty] private Contador? _contadorExistente;
    [ObservableProperty] private string _contadorNome = "";
    [ObservableProperty] private string _contadorCpf = "";
    [ObservableProperty] private string _contadorCrc = "";
    [ObservableProperty] private string _contadorUf = "SP";
    [ObservableProperty] private string _contadorEmail = "";
    [ObservableProperty] private string _contadorTelefone = "";
    [ObservableProperty] private DateTime? _contadorInicio = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private bool _contadorResponsavel = true;

    // escritório
    [ObservableProperty] private EscritorioContabil? _escritorioExistente;
    [ObservableProperty] private string _escritorioCnpj = "";
    [ObservableProperty] private string _escritorioRazao = "";
    [ObservableProperty] private string _escritorioCrc = "";
    [ObservableProperty] private string _escritorioUf = "SP";
    [ObservableProperty] private string _escritorioEmail = "";
    [ObservableProperty] private string _escritorioTelefone = "";
    [ObservableProperty] private DateTime? _escritorioInicio = new DateTime(DateTime.Today.Year, 1, 1);

    public bool TemSelecionado => Selecionado is not null;
    public bool PodeEditar => Contexto.Pode(Permissao.EmpresaResponsaveisManter);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeEditar));

    public async Task CarregarAsync()
    {
        OnPropertyChanged(nameof(PodeEditar));
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var g = NovaGeracao();
        var vinculos = await app.Responsaveis.ListarVinculosAsync(empresa, sessao.Id);
        var contadores = await app.Responsaveis.ListarContadoresAsync();
        var escritorios = await app.Responsaveis.ListarEscritoriosAsync();
        if (!EhAtual(g)) return;
        if (!vinculos.Sucesso) { MostrarResultado(vinculos, ""); return; }
        Vinculos.Clear();
        foreach (var v in vinculos.Valor) Vinculos.Add(new VinculoContabilLinha(v));
        ContadoresCadastrados.Clear();
        foreach (var c in contadores) ContadoresCadastrados.Add(c);
        EscritoriosCadastrados.Clear();
        foreach (var e in escritorios) EscritoriosCadastrados.Add(e);
    }

    partial void OnContadorExistenteChanged(Contador? value)
    {
        if (value is null) return;
        (ContadorNome, ContadorCpf, ContadorCrc, ContadorUf, ContadorEmail, ContadorTelefone) =
            (value.Nome, Cpf.Formatar(value.Cpf), value.Crc, value.UfCrc, value.Email ?? "", value.Telefone ?? "");
    }

    partial void OnEscritorioExistenteChanged(EscritorioContabil? value)
    {
        if (value is null) return;
        (EscritorioCnpj, EscritorioRazao, EscritorioCrc, EscritorioUf, EscritorioEmail, EscritorioTelefone) =
            (ServicoLivros.FormatarCnpj(value.Cnpj), value.RazaoSocial, value.Crc ?? "", value.UfCrc ?? "SP", value.Email ?? "", value.Telefone ?? "");
    }

    [RelayCommand]
    private Task VincularContadorAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Responsaveis.VincularContadorAsync(empresa,
            new Contador(0, ContadorNome, ContadorCpf, ContadorCrc, ContadorUf, ContadorEmail, ContadorTelefone, null),
            DateOnly.FromDateTime(ContadorInicio ?? DateTime.Today), ContadorResponsavel, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        (ContadorNome, ContadorCpf, ContadorCrc, ContadorEmail, ContadorTelefone, ContadorExistente) = ("", "", "", "", "", null);
        await CarregarAsync();
        Mensagem = "Contador vinculado à empresa.";
    }, Permissao.EmpresaResponsaveisManter);

    [RelayCommand]
    private async Task BuscarEscritorioAsync()
    {
        Erros.Clear();
        Mensagem = null;
        if (Contexto.App is not { } app) return;
        var r = await app.Responsaveis.ConsultarCnpjAsync(EscritorioCnpj);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        (EscritorioRazao, EscritorioEmail, EscritorioTelefone) = (r.Valor.RazaoSocial, r.Valor.Email ?? "", r.Valor.Telefone ?? "");
        EscritorioUf = r.Valor.Uf ?? EscritorioUf;
        Mensagem = $"Dados de {r.Valor.Fonte}. Informe o registro do escritório no CRC.";
    }

    [RelayCommand]
    private Task VincularEscritorioAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Responsaveis.VincularEscritorioAsync(empresa,
            new EscritorioContabil(0, EscritorioCnpj, EscritorioRazao, EscritorioCrc, EscritorioUf, EscritorioEmail, EscritorioTelefone),
            DateOnly.FromDateTime(EscritorioInicio ?? DateTime.Today), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        (EscritorioCnpj, EscritorioRazao, EscritorioCrc, EscritorioEmail, EscritorioTelefone, EscritorioExistente) = ("", "", "", "", "", null);
        await CarregarAsync();
        Mensagem = "Escritório contábil vinculado à empresa.";
    }, Permissao.EmpresaResponsaveisManter);

    [RelayCommand]
    private Task DefinirResponsavelAsync() => Acao(async (app, empresa, usuario, id) =>
        await app.Responsaveis.DefinirResponsavelAsync(empresa, id, usuario), "Responsável técnico definido.");

    [RelayCommand]
    private Task EncerrarAsync() => Acao(async (app, empresa, usuario, id) =>
        await app.Responsaveis.EncerrarVinculoAsync(empresa, id, DateOnly.FromDateTime(DataFim ?? DateTime.Today), usuario), "Vínculo encerrado.");

    [RelayCommand]
    private Task ExcluirAsync() => Acao(async (app, empresa, usuario, id) =>
        await app.Responsaveis.ExcluirVinculoAsync(empresa, id, usuario), "Vínculo excluído.");

    private Task Acao(Func<Infra.PerseusApp, long, long, long, Task<Domain.Comum.Resultado>> acao, string sucesso) => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionado is null) { Erros.Add("Selecione um vínculo na lista."); return; }
        var r = await acao(app, empresa, usuario, Selecionado.Vinculo.Id);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = sucesso;
    }, Permissao.EmpresaResponsaveisManter);
}

public sealed record SocioLinha(Socio Socio)
{
    public string Nome => Socio.Nome;
    public string Documento => Socio.CpfCnpj is { } d ? (d.Length == 11 ? Cpf.Formatar(d) : ServicoLivros.FormatarCnpj(d)) : Socio.DocumentoMascarado ?? "";
    public string Qualificacao => Socio.Qualificacao;
    public string Participacao => Socio.Participacao is { } p ? $"{p:0.##}%" : "";
    public string Entrada => Socio.DataEntrada?.ToString("dd/MM/yyyy") ?? "";
    public string Situacao => Socio.DataSaida is { } s ? $"saiu em {s:dd/MM/yyyy}" : "ativo";
    public string Representante => Socio.RepresentanteLegal ? "✓" : "";
}

public sealed record OpcaoTipoPessoa(char Codigo, string Nome)
{
    public static readonly IReadOnlyList<OpcaoTipoPessoa> Todas = [new('F', "Pessoa física"), new('J', "Pessoa jurídica"), new('E', "Estrangeiro")];
}

/// <summary>Cadastros › Sócios da empresa selecionada.</summary>
public partial class SociosViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public ObservableCollection<SocioLinha> Lista { get; } = [];
    public static IReadOnlyList<string> Qualificacoes { get; } =
        ["Sócio-Administrador", "Sócio", "Administrador", "Titular", "Diretor", "Presidente", "Procurador", "Sócio Comanditário"];

    [ObservableProperty] private SocioLinha? _selecionado;
    [ObservableProperty] private long _editandoId;
    [ObservableProperty] private string _nome = "";
    [ObservableProperty] private OpcaoTipoPessoa _tipo = OpcaoTipoPessoa.Todas[0];
    [ObservableProperty] private string _documento = "";
    [ObservableProperty] private string _documentoMascarado = "";
    [ObservableProperty] private string _qualificacao = "Sócio-Administrador";
    [ObservableProperty] private string _participacao = "";
    [ObservableProperty] private DateTime? _entrada;
    [ObservableProperty] private DateTime? _saida;
    [ObservableProperty] private bool _representante;
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _telefone = "";
    [ObservableProperty] private string _resumo = "";

    private Socio? _original;

    public bool PodeEditar => Contexto.Pode(Permissao.EmpresaResponsaveisManter);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeEditar));

    public async Task CarregarAsync()
    {
        OnPropertyChanged(nameof(PodeEditar));
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var g = NovaGeracao();
        var r = await app.Responsaveis.ListarSociosAsync(empresa, sessao.Id);
        if (!EhAtual(g)) return;
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Lista.Clear();
        foreach (var s in r.Valor) Lista.Add(new SocioLinha(s));
        var ativos = r.Valor.Where(s => s.Ativo).ToArray();
        Resumo = $"{ativos.Length} sócio(s) ativo(s) · participação informada: {ativos.Sum(s => s.Participacao ?? 0):0.##}%" +
                 (ativos.Any(s => s.RepresentanteLegal) ? "" : " · sem representante legal definido");
    }

    partial void OnSelecionadoChanged(SocioLinha? value)
    {
        if (value is null) return;
        var s = _original = value.Socio;
        (EditandoId, Nome, Documento, DocumentoMascarado, Qualificacao) = (s.Id, s.Nome, s.CpfCnpj ?? "", s.DocumentoMascarado ?? "", s.Qualificacao);
        Tipo = OpcaoTipoPessoa.Todas.First(t => t.Codigo == s.TipoPessoa);
        Participacao = s.Participacao?.ToString("0.####") ?? "";
        (Entrada, Saida) = (s.DataEntrada?.ToDateTime(TimeOnly.MinValue), s.DataSaida?.ToDateTime(TimeOnly.MinValue));
        (Representante, Email, Telefone) = (s.RepresentanteLegal, s.Email ?? "", s.Telefone ?? "");
    }

    [RelayCommand]
    private void Novo()
    {
        Selecionado = null;
        _original = null;
        (EditandoId, Nome, Documento, DocumentoMascarado, Qualificacao, Participacao) = (0, "", "", "", "Sócio-Administrador", "");
        (Entrada, Saida, Representante, Email, Telefone) = (null, null, false, "", "");
        Tipo = OpcaoTipoPessoa.Todas[0];
    }

    [RelayCommand]
    private Task SalvarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        decimal? participacao = null;
        if (Participacao.Trim().Length > 0)
        {
            if (Numeros.LerValor(Participacao.Replace("%", "")) is not { } p) { Erros.Add("Participação inválida."); return; }
            participacao = p;
        }
        var socio = new Socio(EditandoId, Nome, Tipo.Codigo, Documento, _original?.DocumentoMascarado, Qualificacao, _original?.QualificacaoCodigo,
            participacao, Entrada is { } e ? DateOnly.FromDateTime(e) : null, Saida is { } s ? DateOnly.FromDateTime(s) : null,
            Representante, Email, Telefone, _original?.Origem ?? "MANUAL");
        var r = await app.Responsaveis.SalvarSocioAsync(empresa, socio, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        EditandoId = r.Valor;
        await CarregarAsync();
        Selecionado = Lista.FirstOrDefault(l => l.Socio.Id == r.Valor);
        Mensagem = "Sócio salvo.";
    }, Permissao.EmpresaResponsaveisManter);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (EditandoId == 0) { Erros.Add("Selecione um sócio."); return; }
        var r = await app.Responsaveis.ExcluirSocioAsync(empresa, EditandoId, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Novo();
        await CarregarAsync();
        Mensagem = "Sócio excluído. (Para registrar a saída de um sócio, informe a data de saída em vez de excluir.)";
    }, Permissao.EmpresaResponsaveisManter);

    [RelayCommand]
    private Task ImportarDaReceitaAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Responsaveis.ImportarDaReceitaAsync(empresa, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = r.Valor == 0 ? "Nenhum sócio novo no quadro societário público."
            : $"{r.Valor} sócio(s) incluído(s) do quadro societário público. A base pública mascara o CPF: complete-o e indique o representante legal.";
    }, Permissao.EmpresaResponsaveisManter);
}
