using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Application.Cadastros;
using Perseus.Domain.Comum;
using Perseus.Domain.Seguranca;

namespace Perseus.Desktop.ViewModels;

/// <summary>
/// Cadastros › Empresas: incluir (com pré-preenchimento pela base pública do CNPJ), alterar e excluir;
/// inscrição estadual consultada nos cadastros estaduais com confirmação antes de gravar.
/// </summary>
public partial class CadastroEmpresaViewModel(MainViewModel main) : CadastroViewModel(main)
{
    private DadosCnpj? _consultado;

    public static IReadOnlyList<RegimeTributario> Regimes { get; } = Enum.GetValues<RegimeTributario>();

    public MainViewModel Main => main;

    /// <summary>Abas: 0 = dados cadastrais, 1 = sócios, 2 = contadores (as duas últimas, da empresa selecionada).</summary>
    [ObservableProperty] private int _abaSelecionada;

    public const int AbaSocios = 1;
    public const int AbaContadores = 2;

    public bool TemEmpresaSelecionada => main.EmpresaSelecionada is not null;

    public string EmpresaSelecionadaRotulo => main.EmpresaSelecionada is { } e ? $"Empresa: {e.RazaoSocial}" : "";

    partial void OnAbaSelecionadaChanged(int value) => main.CarregarAbaEmpresa();

    public void AoTrocarEmpresa()
    {
        OnPropertyChanged(nameof(TemEmpresaSelecionada));
        OnPropertyChanged(nameof(EmpresaSelecionadaRotulo));
        if (!TemEmpresaSelecionada) AbaSelecionada = 0;
        if (EmConsulta) MostrarSelecionado();
    }

    private EmpresaDetalhe? _carregada;

    [ObservableProperty] private string _cnpj = "";
    [ObservableProperty] private string _razaoSocial = "";
    [ObservableProperty] private string _nomeFantasia = "";
    [ObservableProperty] private string _naturezaJuridica = "2062";
    [ObservableProperty] private string _cnae = "";
    [ObservableProperty] private string _inscricaoEstadual = "";
    [ObservableProperty] private string _uf = "SP";
    [ObservableProperty] private string _municipioIbge = "";
    [ObservableProperty] private string _municipio = "";
    [ObservableProperty] private string _cep = "";
    [ObservableProperty] private string _logradouro = "";
    [ObservableProperty] private string _numero = "";
    [ObservableProperty] private string _complemento = "";
    [ObservableProperty] private string _bairro = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _telefone = "";
    [ObservableProperty] private DateTime? _inicioAtividades = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private RegimeTributario _regime = RegimeTributario.SimplesNacional;
    [ObservableProperty] private decimal? _anoExercicio = DateTime.Today.Year;
    [ObservableProperty] private bool _importarPlanoModelo = true;
    [ObservableProperty] private string? _origemDados;
    [ObservableProperty] private string? _aviso;

    public string TituloFormulario => Modo switch
    {
        ModoCadastro.Inclusao => "Nova empresa",
        ModoCadastro.Alteracao => "Alterando a empresa",
        _ => _carregada is null ? "Empresa" : "Dados da empresa",
    };

    protected override string NomeRegistro => "empresa";

    /// <summary>Código sequencial da empresa (atribuído na inclusão).</summary>
    public string NumeroRegistro => EmInclusao ? "Nº (automático)" : _carregada is { } e ? $"Nº {e.Id:0000}" : "";
    protected override string? DescricaoSelecionado => _carregada is null ? null : $"{_carregada.RazaoSocial} ({Domain.Cadastros.Cnpj.Formatar(_carregada.Cnpj)})";
    public override bool PodeManter => main.Pode(Permissao.AdministrarSistema);

    protected override string DetalheExclusao =>
        "\n\nATENÇÃO: a empresa e TODOS os seus dados serão apagados do banco de dados — lançamentos, plano de contas, " +
        "períodos, sócios e acessos. Não há como desfazer. Antes de excluir, o sistema gera automaticamente um backup completo " +
        "(a legislação exige a guarda da escrituração; guarde esse arquivo).";

    protected override void AoMudarModo()
    {
        OnPropertyChanged(nameof(TituloFormulario));
        OnPropertyChanged(nameof(NumeroRegistro));
    }

    // ------------------------------------------------------------------ incluir / alterar / excluir

    protected override void PrepararInclusao()
    {
        LimparFormulario();
        (NaturezaJuridica, Uf, Regime, ImportarPlanoModelo) = ("2062", "SP", RegimeTributario.SimplesNacional, true);
        (InicioAtividades, AnoExercicio) = (new DateTime(DateTime.Today.Year, 1, 1), DateTime.Today.Year);
    }

    /// <summary>Mostra no formulário a empresa selecionada (lista à direita ou seletor do topo).</summary>
    protected override void MostrarSelecionado() => _ = CarregarSelecionadaAsync();

    public async Task CarregarSelecionadaAsync()
    {
        var geracao = NovaGeracao();
        EmpresaDetalhe? d = null;
        if (main.App is { } app && main.EmpresaSelecionada is { } sel && PodeManter) d = await app.Empresas.ObterAsync(sel.Id);
        if (!EhAtual(geracao) || EmEdicao) return;
        _carregada = d;
        if (d is null) LimparFormulario();
        else
        {
            (Cnpj, RazaoSocial, NomeFantasia, NaturezaJuridica, Cnae) = (Domain.Cadastros.Cnpj.Formatar(d.Cnpj), d.RazaoSocial, d.NomeFantasia ?? "", d.NaturezaJuridica, d.CnaePrincipal);
            (InscricaoEstadual, Uf, MunicipioIbge, Municipio) = (d.InscricaoEstadual ?? "", d.Uf, d.CodMunicipioIbge, d.Municipio ?? "");
            (Cep, Logradouro, Numero, Complemento, Bairro) = (d.Cep ?? "", d.Logradouro ?? "", d.Numero ?? "", d.Complemento ?? "", d.Bairro ?? "");
            (Email, Telefone) = (d.Email ?? "", d.Telefone ?? "");
            InicioAtividades = d.DataInicioAtividades?.ToDateTime(TimeOnly.MinValue);
            if (d.Regime is { } r) Regime = r;
            OrigemDados = d.FonteDados is null ? null : $"Dados obtidos de {d.FonteDados}. Situação cadastral: {d.SituacaoCadastral ?? "não informada"}.";
            Aviso = null;
        }
        OnPropertyChanged(nameof(TituloFormulario));
        OnPropertyChanged(nameof(NumeroRegistro));
        AtualizarBarra();
    }

    protected override async Task<bool> GravarAsync(bool inclusao)
    {
        if (main.App is not { } app) { Erros.Add("Sem conexão com o banco de dados."); return false; }
        if (!PodeManter) { Erros.Add("Somente administradores mantêm o cadastro de empresas."); return false; }
        try
        {
            Ocupado = true;
            return inclusao ? await IncluirEmpresaAsync(app) : await AlterarEmpresaAsync(app);
        }
        catch (Exception ex)
        {
            Erros.Add(ex.Message);
            return false;
        }
        finally
        {
            Ocupado = false;
        }
    }

    private ComplementoEmpresa Complementos(DadosCnpj? origem) =>
        new(Cep, Logradouro, Numero, Complemento, Bairro, Municipio, Email, Telefone, origem?.SituacaoCadastral, origem?.Fonte, origem?.ConsultadoEm);

    private async Task<bool> IncluirEmpresaAsync(Infra.PerseusApp app)
    {
        // A origem só é registrada se o CNPJ cadastrado for o mesmo que foi consultado.
        var origem = _consultado is not null && Domain.Cadastros.Cnpj.SomenteDigitos(_consultado.Cnpj) == Domain.Cadastros.Cnpj.SomenteDigitos(Cnpj)
            ? _consultado : null;
        var r = await app.Empresas.CadastrarAsync(new NovaEmpresa(
            Cnpj, RazaoSocial, string.IsNullOrWhiteSpace(NomeFantasia) ? null : NomeFantasia.Trim(),
            NaturezaJuridica.Trim(), Cnae, Uf.Trim(), MunicipioIbge.Trim(),
            DateOnly.FromDateTime(InicioAtividades ?? DateTime.Today), Regime, (int)(AnoExercicio ?? DateTime.Today.Year),
            ImportarPlanoModelo, Complementos(origem), InscricaoEstadual));
        MostrarResultado(r, $"Empresa cadastrada com exercício {AnoExercicio} e períodos mensais abertos.");
        if (!r.Sucesso) return false;
        var socios = origem?.QuadroSocietario ?? [];
        if (socios.Count > 0 && main.Sessao is { } sessao
            && await app.Responsaveis.IncluirDoQsaAsync(r.Valor, socios, origem!.Fonte, sessao.Id) is { Sucesso: true, Valor: > 0 } incl)
            Mensagem += $" {incl.Valor} sócio(s) incluído(s) do quadro societário (complete CPF e representante na aba Sócios).";
        _consultado = null;
        Modo = ModoCadastro.Consulta;
        await main.RecarregarEmpresasAsync(r.Valor);
        return true;
    }

    private async Task<bool> AlterarEmpresaAsync(Infra.PerseusApp app)
    {
        if (_carregada is not { } atual) { Erros.Add("Selecione a empresa a alterar."); return false; }
        var r = await app.Empresas.AlterarAsync(new AlteracaoEmpresa(
            atual.Id, RazaoSocial, string.IsNullOrWhiteSpace(NomeFantasia) ? null : NomeFantasia.Trim(), NaturezaJuridica.Trim(), Cnae,
            Uf.Trim(), MunicipioIbge.Trim(), DateOnly.FromDateTime(InicioAtividades ?? DateTime.Today), InscricaoEstadual, Complementos(null)));
        MostrarResultado(r, "Alterações da empresa salvas.");
        if (!r.Sucesso) return false;
        Modo = ModoCadastro.Consulta;
        await main.RecarregarEmpresasAsync(atual.Id);
        await CarregarSelecionadaAsync();
        return true;
    }

    protected override async Task<bool> ExcluirSelecionadoAsync()
    {
        if (main.App is not { } app || _carregada is not { } atual || main.Sessao is not { } sessao) return false;
        // Backup de segurança antes de apagar (a escrituração tem guarda obrigatória).
        string? copia = null;
        try
        {
            Ocupado = true;
            var b = await app.Backup.GerarAsync(sessao, await app.ConfiguracoesGerais.PastaBackupAsync());
            if (b.Sucesso) copia = b.Valor;
        }
        catch (Exception)
        {
            // segue para a pergunta abaixo
        }
        finally
        {
            Ocupado = false;
        }
        if (copia is null && !await Contexto.ConfirmarAsync("Backup não gerado",
                "Não foi possível gerar o backup automático antes da exclusão. Excluir a empresa mesmo assim?", "Excluir sem backup", "Cancelar"))
            return false;

        var r = await app.Empresas.ExcluirAsync(atual.Id);
        if (!r.Sucesso) { MostrarResultado(r, ""); return false; }
        Mensagem = $"Empresa {atual.RazaoSocial} excluída do banco de dados ({r.Valor} lançamento(s) apagado(s))." +
                   (copia is null ? "" : $" Backup anterior à exclusão: {copia}");
        _carregada = null;
        await main.RecarregarEmpresasAsync();
        return true;
    }

    // ------------------------------------------------------------------ consultas externas

    /// <summary>Busca os dados do CNPJ na base pública da Receita e preenche o formulário (o usuário revisa antes de salvar).</summary>
    [RelayCommand]
    private async Task ConsultarCnpjAsync()
    {
        Erros.Clear();
        (Mensagem, Aviso, OrigemDados) = (null, null, null);
        if (main.App is not { } app) { Erros.Add("Sem conexão com o banco de dados."); return; }
        try
        {
            Ocupado = true;
            var r = await app.Empresas.ConsultarCnpjAsync(Cnpj);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            Preencher(r.Valor);
        }
        finally
        {
            Ocupado = false;
        }
        // Em seguida, a inscrição estadual na UF obtida (o usuário decide se usa).
        await BuscarInscricaoEstadualAsync();
    }

    private void Preencher(DadosCnpj d)
    {
        _consultado = d;
        RazaoSocial = d.RazaoSocial;
        NomeFantasia = d.NomeFantasia ?? "";
        NaturezaJuridica = d.NaturezaJuridica ?? NaturezaJuridica;
        Cnae = d.CnaePrincipal ?? "";
        Uf = d.Uf ?? Domain.Cadastros.Uf.DeCodigoIbge(d.CodMunicipioIbge) ?? Uf;
        MunicipioIbge = d.CodMunicipioIbge ?? "";
        Municipio = d.Municipio ?? "";
        Cep = d.Cep ?? "";
        Logradouro = d.Logradouro ?? "";
        Numero = d.Numero ?? "";
        Complemento = d.Complemento ?? "";
        Bairro = d.Bairro ?? "";
        Email = d.Email ?? "";
        Telefone = d.Telefone ?? "";
        if (d.DataInicioAtividades is { } ini)
        {
            InicioAtividades = ini.ToDateTime(TimeOnly.MinValue);
            if (AnoExercicio < ini.Year) AnoExercicio = ini.Year;
        }
        if (d.RegimeSugerido is { } regime) Regime = regime;

        OrigemDados = $"Dados de {d.Fonte} em {d.ConsultadoEm.ToLocalTime():dd/MM/yyyy HH:mm}. Situação cadastral: {d.SituacaoCadastral ?? "não informada"}." +
                      (d.RegimeSugerido is null ? " Regime não consta da base pública (Presumido/Real): confirme abaixo." : "");
        if (!d.Ativa) Aviso = $"Atenção: a situação cadastral do CNPJ é {d.SituacaoCadastral ?? "desconhecida"}.";
        Mensagem = "Dados preenchidos. Revise e clique em Salvar.";
    }

    /// <summary>
    /// Inscrição estadual na UF da empresa (UF do cadastro ou do código IBGE do município), nos cadastros estaduais.
    /// Sempre pergunta antes: em edição, preenche o campo (grava ao Salvar); em consulta, grava na empresa.
    /// </summary>
    [RelayCommand]
    private async Task BuscarInscricaoEstadualAsync()
    {
        Erros.Clear();
        if (main.App is not { } app) { Erros.Add("Sem conexão com o banco de dados."); return; }
        if (!EmEdicao && (_carregada is null || !PodeManter)) { Erros.Add("Selecione uma empresa (ou clique em Incluir)."); return; }
        var uf = Domain.Cadastros.Uf.Valida(Uf) ? Uf.Trim().ToUpperInvariant() : Domain.Cadastros.Uf.DeCodigoIbge(MunicipioIbge);
        Resultado<IReadOnlyList<InscricaoEstadualEncontrada>> r;
        try
        {
            Ocupado = true;
            r = await app.Empresas.ConsultarInscricaoEstadualAsync(Cnpj, Uf, MunicipioIbge);
        }
        finally
        {
            Ocupado = false;
        }
        if (!r.Sucesso) { foreach (var e in r.Erros) Erros.Add(e); return; }
        if (r.Valor.Count == 0)
        {
            // Sem inscrição na UF: a empresa é tratada como não contribuinte do ICMS (ISENTO), sem digitação manual.
            if (!string.IsNullOrWhiteSpace(InscricaoEstadual) && InscricaoEstadual != "ISENTO")
            {
                Aviso = $"Nenhuma inscrição estadual encontrada em {uf} para este CNPJ; o número já informado ({InscricaoEstadual}) foi mantido. " +
                        "Confira no SINTEGRA do estado.";
                return;
            }
            InscricaoEstadual = "ISENTO";
            if (EmEdicao)
            {
                Mensagem = $"Nenhuma inscrição estadual encontrada em {uf}: preenchido ISENTO (gravado ao Salvar).";
                return;
            }
            var isento = await app.Empresas.SalvarInscricaoEstadualAsync(_carregada!.Id, "ISENTO");
            if (!isento.Sucesso) { MostrarResultado(isento, ""); return; }
            Mensagem = $"Nenhuma inscrição estadual encontrada em {uf}: a empresa foi registrada como ISENTO.";
            return;
        }

        var ie = r.Valor[0];
        var outras = r.Valor.Count > 1 ? $"\nOutras inscrições em {uf}: {string.Join(", ", r.Valor.Skip(1).Select(i => i.Inscricao))}." : "";
        var atual = string.IsNullOrWhiteSpace(InscricaoEstadual) ? "" : $"\nNo cadastro hoje: {InscricaoEstadual}.";
        var gravarAgora = !EmEdicao;
        var ok = await Contexto.ConfirmarAsync("Inscrição estadual encontrada",
            $"{uf}: {ie.Inscricao} ({(ie.Ativa ? "ativa" : "INATIVA")}){outras}{atual}\n" +
            $"Fonte: {ie.Fonte}, consultada em {ie.ConsultadoEm.ToLocalTime():dd/MM/yyyy HH:mm}.\n\n" +
            (gravarAgora ? "Deseja salvar este número no cadastro da empresa?" : "Deseja usar este número? Ele será gravado quando você clicar em Salvar."),
            gravarAgora ? "Salvar" : "Usar", "Não salvar");
        if (!ok) { Mensagem = "Inscrição estadual não alterada."; return; }

        if (!gravarAgora)
        {
            InscricaoEstadual = ie.Inscricao;
            Mensagem = "Inscrição estadual preenchida. Clique em Salvar para gravar.";
            return;
        }
        var salvo = await app.Empresas.SalvarInscricaoEstadualAsync(_carregada!.Id, ie.Inscricao);
        if (!salvo.Sucesso) { MostrarResultado(salvo, ""); return; }
        InscricaoEstadual = ie.Inscricao;
        Mensagem = $"Inscrição estadual {ie.Inscricao} salva no cadastro da empresa.";
    }

    /// <summary>Abre o portal nacional do SINTEGRA (links para a consulta de cada estado) para conferência manual.</summary>
    [RelayCommand]
    private void AbrirSintegra()
    {
        try
        {
            Process.Start(new ProcessStartInfo("http://www.sintegra.gov.br/") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            Erros.Add($"Não foi possível abrir o navegador: {ex.Message}");
        }
    }

    private void LimparFormulario()
    {
        (Cnpj, RazaoSocial, NomeFantasia, Cnae, MunicipioIbge, Municipio, InscricaoEstadual) = ("", "", "", "", "", "", "");
        (Cep, Logradouro, Numero, Complemento, Bairro, Email, Telefone) = ("", "", "", "", "", "", "");
        (OrigemDados, Aviso, _consultado) = (null, null, null);
    }
}
