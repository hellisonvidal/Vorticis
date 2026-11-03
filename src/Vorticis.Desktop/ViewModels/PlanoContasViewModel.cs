using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Cadastros;
using Vorticis.Application.Contabil;
using Vorticis.Domain.Contabil;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Desktop.ViewModels;

public sealed record OpcaoNatureza(string Codigo, string Nome)
{
    public static readonly IReadOnlyList<OpcaoNatureza> Todas =
    [
        new("01", "01 - Ativo"), new("02", "02 - Passivo"), new("03", "03 - Patrimônio líquido"),
        new("04", "04 - Resultado"), new("05", "05 - Compensação"), new("09", "09 - Outras"),
    ];
}

public sealed record OpcaoSaldo(IndicadorDC DC, string Nome)
{
    public static readonly IReadOnlyList<OpcaoSaldo> Todas = [new(IndicadorDC.D, "Devedora"), new(IndicadorDC.C, "Credora")];
    public static OpcaoSaldo De(IndicadorDC dc) => Todas.First(o => o.DC == dc);
}

public sealed record ContaLinha(ContaDetalhe Conta)
{
    public string Numero => Conta.Id.ToString("00000");
    public string Reduzido => Conta.CodigoReduzido?.ToString() ?? "";
    public string Codigo => Conta.Codigo;
    /// <summary>Descrição recuada pelo nível (o código fica alinhado à esquerda).</summary>
    public string Descricao => new string(' ', (Conta.Nivel - 1) * 3) + Conta.Descricao;
    public string Tipo => Conta.Analitica ? "Analítica" : "Sintética";
    public string Natureza => OpcaoNatureza.Todas.FirstOrDefault(n => n.Codigo == Conta.CodNat)?.Nome[5..] ?? Conta.CodNat;
    public string Situacao => Conta.Ativa ? "" : "Inativa";
}

/// <summary>Linha da prévia de conversão: código atual → código novo, com o aviso quando a conta perde zeros.</summary>
public sealed record LinhaPrevia(string Antigo, string Novo, string Situacao)
{
    public static LinhaPrevia De(ItemConversao i) => new(i.Antigo, i.Novo, i.Aviso is { } a ? "Atenção: " + a : i.Muda ? "Muda" : "Igual");
}

/// <summary>Conta sintética que pode ser o grupo onde as contas dos participantes são alocadas.</summary>
public sealed record OpcaoGrupo(long Id, string Codigo, string Descricao)
{
    public string Texto => $"{Codigo} - {Descricao}";
    public override string ToString() => Texto;
}

/// <summary>
/// Cadastros › Plano de contas (Incluir / Alterar / Excluir). A grade só aparece depois que a empresa tem plano: sem plano (ou ao
/// clicar em "Escolher plano"), a tela oferece os modelos embutidos — os mesmos do cadastro da empresa.
/// </summary>
public partial class PlanoContasViewModel(IContexto contexto) : CadastroViewModel(contexto)
{
    private IReadOnlyList<ContaDetalhe> _plano = [];

    public ObservableCollection<ContaLinha> Lista { get; } = [];

    /// <summary>A tela mostra as contas; a ficha da conta (incluir, alterar, consultar) abre numa janela por cima.</summary>
    protected override bool FichaEmJanela => true;

    private ImportarContasPlanoViewModel? _importar;

    /// <summary>Importar contas do Excel (acrescenta ao plano só as contas marcadas).</summary>
    public ImportarContasPlanoViewModel Importar => _importar ??= new ImportarContasPlanoViewModel(Contexto, CarregarAsync);

    // ------------------------------------------------------------------ escolha do plano (modelos)

    public static IReadOnlyList<ModeloPlano> ModelosPlano => CatalogoModelosPlano.Todos;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(MostrarEscolha), nameof(MostrarGrade), nameof(MostrarConversao))] private bool _temPlano;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(MostrarEscolha), nameof(MostrarGrade), nameof(MostrarConversao))] private bool _escolhendoPlano;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(MostrarGrade), nameof(MostrarConversao))] private bool _convertendoMascara;
    [ObservableProperty] private ModeloPlano? _modeloEscolhido;
    [ObservableProperty] private string _planoAtual = "";
    [ObservableProperty] private bool _usaCentroCusto;

    /// <summary>Oferece os modelos: empresa sem plano, ou o usuário pediu para escolher/trocar o plano.</summary>
    public bool MostrarEscolha => !TemPlano || EscolhendoPlano;

    /// <summary>Formulário e grade das contas: só com o plano escolhido (e fora da troca de plano).</summary>
    public bool MostrarGrade => TemPlano && !EscolhendoPlano && !ConvertendoMascara;

    /// <summary>Painel "Converter máscara" do plano da empresa (prévia de cada conta antes de converter).</summary>
    public bool MostrarConversao => TemPlano && !EscolhendoPlano && ConvertendoMascara;

    /// <summary>Contas sintéticas (nível 2 em diante): candidatas a grupo dos participantes.</summary>
    public ObservableCollection<OpcaoGrupo> Grupos { get; } = [];
    [ObservableProperty] private OpcaoGrupo? _grupoParticipantes;

    /// <summary>Centro de custo: a empresa usa (aba Contabilidade) ou a conta já exige.</summary>
    public bool PermiteCentroCusto => UsaCentroCusto || ExigeCc;
    partial void OnUsaCentroCustoChanged(bool value) => OnPropertyChanged(nameof(PermiteCentroCusto));
    partial void OnExigeCcChanged(bool value) => OnPropertyChanged(nameof(PermiteCentroCusto));

    [RelayCommand]
    private void EscolherPlano()
    {
        Erros.Clear();
        Mensagem = null;
        CriandoNovoPlano = false;
        EscolhendoPlano = true;
    }

    [RelayCommand]
    private void CancelarEscolha()
    {
        EscolhendoPlano = false;
        ModeloEscolhido = CatalogoModelosPlano.Todos.FirstOrDefault(m => m.Id == _modeloAplicado);
    }

    private string? _modeloAplicado;

    /// <summary>Aplica o modelo escolhido, substituindo o plano da empresa (só sem lançamentos; com plano, pede confirmação).</summary>
    [RelayCommand]
    private Task AplicarModeloAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (ModeloEscolhido is not { } modelo) { Erros.Add("Escolha um modelo de plano de contas."); return; }
        if (TemPlano && !await Contexto.ConfirmarAsync("Trocar o plano de contas",
                $"O plano de contas atual será substituído pelo modelo \"{modelo.Nome}\" ({modelo.QuantidadeContas} contas). " +
                "Isso só é possível enquanto não houver lançamentos. Deseja continuar?", "Trocar o plano", "Cancelar"))
            return;
        var mascara = MascaraModelo.Trim();
        var r = await app.PlanoContas.AplicarModeloAsync(empresa, modelo.Id, usuario, mascara: mascara.Length > 0 ? mascara : null);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        EscolhendoPlano = false;
        await CarregarAsync();
        Mensagem = mascara.Length > 0 ? $"Plano \"{modelo.Nome}\" aplicado na máscara {PlanoMascara(mascara)}: {r.Valor} contas."
                                      : $"Plano \"{modelo.Nome}\" aplicado: {r.Valor} contas.";
    }, Permissao.PlanoContasManter);

    private static string PlanoMascara(string m) => MascaraPlano.Ler(m) is { Sucesso: true } r ? r.Valor.Texto : m;

    public static IReadOnlyList<string> MascarasSugeridas => MascaraPlano.Sugestoes;

    // ------------------------------------------------------------------ modelo em outra máscara (cópia convertida)

    /// <summary>Máscara desejada para o modelo escolhido (vazio = como no modelo).</summary>
    [ObservableProperty] private string _mascaraModelo = "";
    [ObservableProperty] private string _resumoPreviaModelo = "";
    public ObservableCollection<LinhaPrevia> PreviaModelo { get; } = [];

    partial void OnMascaraModeloChanged(string value) { PreviaModelo.Clear(); ResumoPreviaModelo = ""; }
    partial void OnModeloEscolhidoChanged(ModeloPlano? value) { PreviaModelo.Clear(); ResumoPreviaModelo = ""; }

    /// <summary>Mostra, conta a conta, como o modelo ficaria na máscara informada (nada é gravado).</summary>
    [RelayCommand]
    private void VerPreviaModelo()
    {
        Erros.Clear();
        Mensagem = null;
        PreviaModelo.Clear();
        ResumoPreviaModelo = "";
        if (ModeloEscolhido is not { } modelo) { Erros.Add("Escolha um modelo de plano de contas."); return; }
        if (Contexto.App is not { } app) return;
        var r = app.PlanoContas.PreverModelo(modelo.Id, MascaraModelo);
        if (!r.Sucesso) { foreach (var e in r.Erros) Erros.Add(e); return; }
        MostrarPrevia(r.Valor, PreviaModelo, t => ResumoPreviaModelo = t);
    }

    private void MostrarPrevia(PreviaConversao previa, ObservableCollection<LinhaPrevia> destino, Action<string> resumo)
    {
        foreach (var i in previa.Itens.Take(500)) destino.Add(LinhaPrevia.De(i));
        if (!previa.Possivel)
        {
            foreach (var e in previa.Erros) Erros.Add(e);
            resumo($"Não é possível converter para a máscara {previa.Mascara}: {previa.Erros.Count} problema(s) — nada será alterado.");
        }
        else
            resumo($"Máscara {previa.Mascara}: {previa.Itens.Count} conta(s), {previa.Mudancas} com código novo" +
                   (previa.Avisos > 0 ? $", {previa.Avisos} com aviso (perdem zeros à esquerda)" : "") +
                   (previa.Itens.Count > 500 ? ". A lista mostra as 500 primeiras." : "."));
    }

    // ------------------------------------------------------------------ criar novo plano

    [ObservableProperty] private bool _criandoNovoPlano;
    [ObservableProperty] private string _novoPlanoNome = "";
    [ObservableProperty] private string _novoPlanoMascara = "";

    [RelayCommand]
    private void AbrirNovoPlano()
    {
        Erros.Clear();
        Mensagem = null;
        NovoPlanoNome = "";
        NovoPlanoMascara = MascaraAtual;
        CriandoNovoPlano = true;
    }

    [RelayCommand]
    private void CancelarNovoPlano() => CriandoNovoPlano = false;

    /// <summary>Cria um plano vazio (só os grupos de nível 1) com o nome e a máscara informados.</summary>
    [RelayCommand]
    private Task CriarNovoPlanoAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (TemPlano && !await Contexto.ConfirmarAsync("Criar novo plano de contas",
                "O plano de contas atual será substituído por um plano novo, vazio (só os grupos principais). Isso só é possível enquanto não houver lançamentos. Deseja continuar?",
                "Criar o plano", "Cancelar"))
            return;
        var nome = NovoPlanoNome.Trim();
        var r = await app.PlanoContas.CriarNovoPlanoAsync(empresa, nome, NovoPlanoMascara, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        CriandoNovoPlano = false;
        EscolhendoPlano = false;
        await CarregarAsync();
        Mensagem = $"Plano \"{nome}\" criado com {r.Valor} grupos principais. Use Incluir para cadastrar as demais contas.";
    }, Permissao.PlanoContasManter);

    private string MascaraAtual => _plano.Count == 0 ? "" : _mascaraEmpresa ?? MascaraPlano.Deduzir(_plano.Select(c => c.Codigo)) ?? "";
    private string? _mascaraEmpresa;

    // ------------------------------------------------------------------ converter a máscara do plano da empresa

    [ObservableProperty] private string _mascaraConversao = "";
    [ObservableProperty] private string _resumoConversao = "";
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ConfirmarConversaoCommand))] private bool _conversaoPossivel;
    public ObservableCollection<LinhaPrevia> PreviaConversaoItens { get; } = [];

    partial void OnMascaraConversaoChanged(string value)
    {
        PreviaConversaoItens.Clear();
        ResumoConversao = "";
        ConversaoPossivel = false;
    }

    [RelayCommand]
    private void AbrirConversao()
    {
        Erros.Clear();
        Mensagem = null;
        MascaraConversao = MascaraAtual;
        ConvertendoMascara = true;
    }

    [RelayCommand]
    private void CancelarConversao()
    {
        ConvertendoMascara = false;
        Erros.Clear();
    }

    /// <summary>Simula a conversão do plano inteiro (código atual → novo, avisos e conflitos) sem gravar nada.</summary>
    [RelayCommand]
    private Task VerPreviaConversaoAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        PreviaConversaoItens.Clear();
        ConversaoPossivel = false;
        ResumoConversao = "";
        var m = MascaraPlano.Ler(MascaraConversao);
        if (!m.Sucesso) { MostrarResultado(m, ""); return; }
        var r = await app.PlanoContas.PreverMascaraAsync(empresa, m.Valor, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        MostrarPrevia(r.Valor, PreviaConversaoItens, t => ResumoConversao = t);
        ConversaoPossivel = r.Valor.Possivel && r.Valor.Mudancas > 0;
        if (r.Valor.Possivel && r.Valor.Mudancas == 0) ResumoConversao = $"O plano já está na máscara {r.Valor.Mascara}; não há nada a converter.";
    }, Permissao.PlanoContasManter);

    /// <summary>Converte o plano da empresa para a máscara (uma transação; os lançamentos continuam nas mesmas contas).</summary>
    [RelayCommand(CanExecute = nameof(ConversaoPossivel))]
    private Task ConfirmarConversaoAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var m = MascaraPlano.Ler(MascaraConversao);
        if (!m.Sucesso) { MostrarResultado(m, ""); return; }
        if (!await Contexto.ConfirmarAsync("Converter máscara do plano",
                $"Os códigos de {PreviaConversaoItens.Count(l => l.Antigo != l.Novo)} conta(s) serão convertidos para a máscara {m.Valor.Texto}. " +
                "Os lançamentos, saldos, vínculos com o plano referencial e as contas das configurações acompanham a mudança, que fica na trilha de auditoria. " +
                "Arquivos já entregues (ECD de anos anteriores) mantêm os códigos antigos. Deseja converter?", "Converter", "Cancelar"))
            return;
        var r = await app.PlanoContas.AplicarMascaraAsync(empresa, m.Valor, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        ConvertendoMascara = false;
        await CarregarAsync();
        Mensagem = $"{r.Valor} código(s) convertido(s) para a máscara {m.Valor.Texto}.";
    }, Permissao.PlanoContasManter);

    [ObservableProperty] private string _filtro = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TemSelecionada))] private ContaLinha? _selecionada;

    // formulário (código, natureza e data de inclusão só na inclusão: exigência da ECD)
    [ObservableProperty] private string _codigo = "";
    [ObservableProperty] private string _reduzido = "";
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private OpcaoNatureza _natureza = OpcaoNatureza.Todas[0];
    [ObservableProperty] private OpcaoSaldo _saldo = OpcaoSaldo.Todas[0];
    [ObservableProperty] private bool _exigeCc;
    [ObservableProperty] private bool _exigeParticipante;
    [ObservableProperty] private bool _ativa = true;
    [ObservableProperty] private DateTime? _dataInclusao = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private string _dicaPai = "";

    public bool TemSelecionada => Selecionada is not null;
    public string TituloFormulario => EmInclusao ? "Nova conta" : Modo == ModoCadastro.Alteracao ? "Alterar conta" : "Dados da conta";
    public string NumeroRegistro => EmInclusao ? "" : Selecionada is { } s ? $"Cód. {s.Conta.Codigo}" + (s.Reduzido.Length > 0 ? $"  ·  Reduzido {s.Reduzido}" : "") : "";
    public bool PodeEditar => Contexto.Pode(Permissao.PlanoContasManter);
    public override bool PodeManter => PodeEditar;
    protected override string NomeRegistro => "conta";
    protected override string? DescricaoSelecionado => Selecionada is { } s ? $"{s.Conta.Codigo} – {s.Conta.Descricao}" : null;
    protected override string DetalheExclusao => "\n\nConta com lançamentos não é excluída: use Alterar e desmarque Ativa depois de zerar o saldo.";

    public void AtualizarPermissoes()
    {
        OnPropertyChanged(nameof(PodeEditar));
        AtualizarBarra();
    }

    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var id = Selecionada?.Conta.Id;
        var geracao = NovaGeracao();
        var plano = await app.PlanoContas.ListarAsync(empresa, sessao.Id);
        var cfg = await app.PlanoContas.ObterConfiguracaoAsync(empresa, sessao.Id);
        if (!EhAtual(geracao)) return;
        _plano = plano;
        TemPlano = plano.Count > 0;
        _modeloAplicado = cfg.ModeloPlano;
        _mascaraEmpresa = cfg.MascaraPlano;
        UsaCentroCusto = cfg.UsaCentroCusto;
        var modelo = CatalogoModelosPlano.Todos.FirstOrDefault(m => m.Id == cfg.ModeloPlano);
        PlanoAtual = !TemPlano ? "" : modelo is not null ? $"Plano de contas: {modelo.Nome} ({plano.Count} contas)"
                   : cfg.NomePlano is { Length: > 0 } nomePlano ? $"Plano de contas: {nomePlano} ({plano.Count} contas)" : $"Plano de contas próprio ({plano.Count} contas)";
        if (!TemPlano) { EscolhendoPlano = false; ConvertendoMascara = false; }
        ModeloEscolhido = modelo ?? (TemPlano ? null : CatalogoModelosPlano.Padrao);
        Grupos.Clear();
        foreach (var g in plano.Where(c => !c.Analitica && c.Nivel >= 2)) Grupos.Add(new OpcaoGrupo(g.Id, g.Codigo, g.Descricao));
        Filtrar();
        Selecionada = Lista.FirstOrDefault(l => l.Conta.Id == id);
    }

    partial void OnFiltroChanged(string value) => Filtrar();

    private void Filtrar()
    {
        var f = Filtro.Trim();
        Lista.Clear();
        foreach (var c in _plano.Where(c => f.Length == 0 || c.Codigo.StartsWith(f, StringComparison.Ordinal)
                                            || c.CodigoReduzido?.ToString() == f
                                            || c.Descricao.Contains(f, StringComparison.CurrentCultureIgnoreCase)))
            Lista.Add(new ContaLinha(c));
    }

    partial void OnSelecionadaChanged(ContaLinha? value)
    {
        OnPropertyChanged(nameof(NumeroRegistro));
        if (EmConsulta) MostrarSelecionado();
        AtualizarBarra();
    }

    protected override void MostrarSelecionado()
    {
        if (Selecionada is not { } linha)
        {
            (Codigo, Reduzido, Descricao, DicaPai) = ("", "", "", "");
            return;
        }
        var c = linha.Conta;
        Codigo = c.Codigo;
        Reduzido = c.CodigoReduzido?.ToString() ?? "";
        (Descricao, Saldo, ExigeCc, ExigeParticipante, Ativa) = (c.Descricao, OpcaoSaldo.De(c.NaturezaSaldo), c.ExigeCentroCusto, c.ExigeParticipante, c.Ativa);
        GrupoParticipantes = c.GrupoParticipantesId is { } g ? Grupos.FirstOrDefault(x => x.Id == g) : null;
        Natureza = OpcaoNatureza.Todas.FirstOrDefault(n => n.Codigo == c.CodNat) ?? Natureza;
        DicaPai = c.Analitica ? "Conta analítica (recebe lançamentos)." : "Conta sintética (agrupa subcontas).";
    }

    /// <summary>Nova conta: sugere uma subconta da selecionada (ou irmã, se ela for analítica).</summary>
    protected override void PrepararInclusao()
    {
        (Descricao, ExigeCc, ExigeParticipante, Ativa) = ("", false, false, true);
        GrupoParticipantes = null;
        Reduzido = Vorticis.Domain.Comum.Sequencia.MenorLivre(_plano.Where(x => x.CodigoReduzido.HasValue).Select(x => x.CodigoReduzido!.Value)).ToString();   // menor número livre; pode ser trocado
        DataInclusao = new DateTime(DateTime.Today.Year, 1, 1);
        if (Selecionada?.Conta is { } c)
        {
            var baseCodigo = c.Analitica && c.ContaPaiId is not null ? c.Codigo[..c.Codigo.LastIndexOf('.')] : c.Codigo;
            Codigo = SugerirProximo(baseCodigo);
        }
        else Codigo = "";
    }

    /// <summary>Marcar "exige participante" exige indicar o grupo; sugere a conta superior da conta (sintética), que o usuário pode trocar.</summary>
    partial void OnExigeParticipanteChanged(bool value)
    {
        if (!EmEdicao) return;
        GrupoParticipantes = value ? GrupoParticipantes ?? SugerirGrupo() : null;
    }

    private OpcaoGrupo? SugerirGrupo()
    {
        string? codigoPai = null;
        if (EmInclusao)
        {
            var codigo = Codigo.Trim();
            var i = codigo.LastIndexOf('.');
            if (i > 0) codigoPai = codigo[..i];
        }
        else if (Selecionada?.Conta.ContaPaiId is { } paiId) codigoPai = _plano.FirstOrDefault(c => c.Id == paiId)?.Codigo;
        return codigoPai is null ? null : Grupos.FirstOrDefault(g => g.Codigo == codigoPai);
    }

    private string SugerirProximo(string pai)
    {
        var filhos = _plano.Where(x => x.Codigo.StartsWith(pai + ".", StringComparison.Ordinal) && x.Codigo.Count(ch => ch == '.') == pai.Count(ch => ch == '.') + 1)
                           .Select(x => x.Codigo[(pai.Length + 1)..]).ToArray();
        if (filhos.Length == 0) return pai + ".01";
        var ultimo = filhos.Max(s => (s.Length, s)).s;
        return long.TryParse(ultimo, out var n) ? $"{pai}.{(n + 1).ToString().PadLeft(ultimo.Length, '0')}" : pai + ".";
    }

    /// <summary>Na inclusão, ao digitar o código, herda natureza e saldo da conta superior.</summary>
    partial void OnCodigoChanged(string value)
    {
        if (!EmInclusao) return;   // ao preparar a inclusão, roda de novo quando o modo muda (AoMudarModo)
        var codigo = value.Trim();
        var i = codigo.LastIndexOf('.');
        var pai = i > 0 ? _plano.FirstOrDefault(c => c.Codigo == codigo[..i]) : null;
        if (ExigeParticipante && GrupoParticipantes is null) GrupoParticipantes = SugerirGrupo();
        if (pai is null) { DicaPai = i > 0 ? $"Conta superior {codigo[..i]} não encontrada." : "Conta de nível 1 (sintética)."; return; }
        Natureza = OpcaoNatureza.Todas.FirstOrDefault(n => n.Codigo == pai.CodNat) ?? Natureza;
        Saldo = OpcaoSaldo.De(pai.NaturezaSaldo);
        DicaPai = $"Subconta de {pai.Codigo} - {pai.Descricao}" +
                  (pai.Analitica ? (pai.TemMovimento ? " (ATENÇÃO: superior tem lançamentos)" : " (a superior passará a sintética)") : "");
    }

    protected override void AoMudarModo()
    {
        OnPropertyChanged(nameof(TituloFormulario));
        OnPropertyChanged(nameof(NumeroRegistro));
        if (EmInclusao) OnCodigoChanged(Codigo);
    }

    protected override async Task<bool> GravarAsync(bool inclusao)
    {
        var ok = false;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            long id;
            if (inclusao)
            {
                int? reduzido = null;
                if (Reduzido.Trim().Length > 0)
                {
                    if (!int.TryParse(Reduzido.Trim(), out var n)) { Erros.Add("O código reduzido deve ser um número."); return; }
                    reduzido = n;
                }
                var r = await app.PlanoContas.IncluirAsync(empresa, new NovaConta(Codigo, Descricao, Natureza.Codigo, Saldo.DC,
                    ExigeCc, ExigeParticipante, DateOnly.FromDateTime(DataInclusao ?? DateTime.Today), reduzido,
                    ExigeParticipante ? GrupoParticipantes?.Id : null), usuario);
                if (!r.Sucesso) { MostrarResultado(r, ""); return; }
                id = r.Valor;
            }
            else
            {
                if (Selecionada is null) { Erros.Add("Selecione uma conta."); return; }
                id = Selecionada.Conta.Id;
                var r = await app.PlanoContas.AlterarAsync(empresa, id, Descricao, Saldo.DC, ExigeCc, ExigeParticipante, Ativa, usuario,
                                                           grupoParticipantesId: ExigeParticipante ? GrupoParticipantes?.Id : null);
                if (!r.Sucesso) { MostrarResultado(r, ""); return; }
                if (Selecionada.Conta.Analitica && Reduzido.Trim() != (Selecionada.Conta.CodigoReduzido?.ToString() ?? ""))
                {
                    if (!int.TryParse(Reduzido.Trim(), out var n)) { Erros.Add("O código reduzido deve ser um número."); return; }
                    var rr = await app.PlanoContas.AlterarReduzidoAsync(empresa, id, n, usuario);
                    if (!rr.Sucesso) { MostrarResultado(rr, ""); return; }
                }
            }
            ok = true;
            var codigo = Codigo.Trim();
            Modo = ModoCadastro.Consulta;
            await CarregarAsync();
            Selecionada = Lista.FirstOrDefault(l => l.Conta.Id == id);
            Mensagem = inclusao ? $"Conta {codigo} incluída." : "Conta alterada.";
        }, Permissao.PlanoContasManter);
        return ok;
    }

    protected override async Task<bool> ExcluirSelecionadoAsync()
    {
        var ok = false;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            if (Selecionada is null) return;
            var codigo = Selecionada.Conta.Codigo;
            var r = await app.PlanoContas.ExcluirAsync(empresa, Selecionada.Conta.Id, usuario);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            ok = true;
            Selecionada = null;
            await CarregarAsync();
            Mensagem = $"Conta {codigo} excluída.";
        }, Permissao.PlanoContasManter);
        return ok;
    }
}
