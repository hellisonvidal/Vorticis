using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perseus.Application.Cadastros;
using Perseus.Application.Contabil;
using Perseus.Application.Sistema;
using Perseus.Domain.Comum;
using Perseus.Domain.Contabil;
using Perseus.Domain.Seguranca;
using Perseus.Infra;

namespace Perseus.Desktop.ViewModels;

/// <summary>
/// Uma linha da digitação. Em partida dobrada é um lançamento inteiro (débito, crédito e valor); em partida simples
/// é uma partida (uma conta, D ou C). Todos os campos são texto livre, editados direto pelo teclado.
/// </summary>
public partial class LinhaLancamentoViewModel : ObservableObject
{
    [ObservableProperty] private string _data = "";
    [ObservableProperty] private string _reduzido = "";
    [ObservableProperty] private string _conta = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ResumoContas))] private string _nomeConta = "";
    [ObservableProperty] private string _reduzidoCredito = "";
    [ObservableProperty] private string _contaCredito = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ResumoContas))] private string _nomeContaCredito = "";
    [ObservableProperty] private string _dc = "D";
    [ObservableProperty] private string _valor = "";
    [ObservableProperty] private string _documento = "";
    [ObservableProperty] private string _codigoHistorico = "";
    [ObservableProperty] private string _historico = "";
    [ObservableProperty] private string _centroCusto = "";
    [ObservableProperty] private string _participante = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ResumoContas))] private string _nomeParticipante = "";

    /// <summary>Criada pela seta para baixo e ainda não alterada pelo usuário (a seta para cima a cancela).</summary>
    public bool Nova { get; set; }
    public bool Editada { get; private set; }

    public LinhaLancamentoViewModel() => PropertyChanged += (_, e) =>
    {
        if (e.PropertyName is not (nameof(NomeConta) or nameof(NomeContaCredito) or nameof(NomeParticipante) or nameof(ResumoContas))) Editada = true;
    };

    /// <summary>Marca a linha como recém-criada depois de preencher as sugestões (que não contam como edição).</summary>
    public LinhaLancamentoViewModel ComoNova()
    {
        (Nova, Editada) = (true, false);
        return this;
    }

    /// <summary>Nomes das contas da linha (rodapé da tela).</summary>
    public string ResumoContas => (NomeContaCredito.Length == 0 ? NomeConta
        : $"Débito: {(NomeConta.Length > 0 ? NomeConta : "—")}   ·   Crédito: {NomeContaCredito}")
        + (NomeParticipante.Length > 0 ? $"   ·   Participante: {NomeParticipante}" : "");

    public decimal? ValorDecimal => Formato.LerValor(Valor);
    public IndicadorDC? Indicador => Dc.Trim().ToUpperInvariant() switch { "D" => IndicadorDC.D, "C" => IndicadorDC.C, _ => null };
    public bool Vazia => Conta.Trim().Length == 0 && ContaCredito.Trim().Length == 0 && Valor.Trim().Length == 0;

    /// <summary>Valor com separador de milhar e duas casas (1.500,00), aplicado ao sair do campo.</summary>
    public void FormatarValor()
    {
        if (ValorDecimal is { } v) Valor = Formato.Moeda(v);
    }
}

/// <summary>Lançamento já efetuado (lista da tela de lançamento), inclusive os estornados.</summary>
public sealed record LancamentoEfetuadoLinha(long Id, long Numero, DateOnly DataLancamento, string Debito, string Credito, decimal Total,
                                             string Historico, string Documento, bool Estornado)
{
    public string Data => DataLancamento.ToString("dd/MM/yyyy");
    public string Valor => Formato.Moeda(Total);
    public string Situacao => Estornado ? "Estornado" : Historico.StartsWith("Estorno", StringComparison.OrdinalIgnoreCase) ? "Estorno" : "Efetivado";
}

public sealed record ContaSelecionavel(string Codigo, string Descricao, int? Reduzido = null)
{
    public string Rotulo => $"{(Reduzido is { } r ? $"{r,5}  " : "")}{Codigo}  {Descricao}";
}

/// <summary>
/// Lançamentos › Lançamento contábil, pensado para o teclado: Enter/Tab avançam, seta para baixo na última linha cria
/// outra, seta para cima numa linha recém-criada e intocada a cancela, Ctrl+Enter salva. O modo (partida dobrada numa
/// linha ou partida simples em várias) vem de Configurações gerais.
/// </summary>
public partial class LancamentoViewModel : ModuloViewModel
{
    private IReadOnlyList<ContaSelecionavel> _todasContas = [];
    private Dictionary<string, ContaSelecionavel> _porCodigo = [];
    private Dictionary<int, ContaSelecionavel> _porReduzido = [];
    private Dictionary<string, HistoricoPadrao> _historicos = [];
    private Dictionary<string, Participante> _participantes = [];
    private HashSet<string> _contasComParticipante = [];
    private bool _resolvendo;

    public LancamentoViewModel(IContexto contexto) : base(contexto)
    {
        Linhas.CollectionChanged += AoMudarLinhas;
        Limpar();
    }

    public ObservableCollection<LinhaLancamentoViewModel> Linhas { get; } = [];
    public ObservableCollection<ContaSelecionavel> Contas { get; } = [];
    public IReadOnlyList<ContaSelecionavel> TodasContas => _todasContas;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PartidaSimples), nameof(DescricaoModo))] private bool _partidaDobrada = true;
    [ObservableProperty] private DateTime? _data = DateTime.Today;
    [ObservableProperty] private LinhaLancamentoViewModel? _linhaSelecionada;
    [ObservableProperty] private ContaSelecionavel? _contaSelecionada;
    [ObservableProperty] private string _filtroConta = "";
    [ObservableProperty] private string _totalDebito = "0,00";
    [ObservableProperty] private string _totalCredito = "0,00";
    [ObservableProperty] private string _diferenca = "0,00";
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SalvarCommand))] private bool _balanceado;
    /// <summary>Plano com participantes: a coluna Part. aparece e as contas dos participantes exigem o código.</summary>
    [ObservableProperty] private bool _usaParticipantes;

    // Lançamentos efetuados (inclusive estornados) e o estorno do selecionado
    public ObservableCollection<LancamentoEfetuadoLinha> Efetuados { get; } = [];
    [ObservableProperty] private DateTime? _listaInicio = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private DateTime? _listaFim = new DateTime(DateTime.Today.Year, 12, 31);
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(EstornarCommand))] private LancamentoEfetuadoLinha? _efetuadoSelecionado;
    [ObservableProperty] private DateTime? _dataEstorno;
    [ObservableProperty] private string _motivoEstorno = "";
    public bool PodeEstornar => Contexto.Pode(Permissao.LancamentoEstornar);

    // Alteração de um lançamento efetuado (duplo clique na lista)
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(EmEdicao), nameof(TextoEdicao))] private LancamentoEfetuadoLinha? _editando;
    private bool _dobradaConfigurada = true;
    public bool EmEdicao => Editando is not null;
    public string TextoEdicao => Editando is { } e
        ? $"Alterando o lançamento nº {e.Numero} de {e.Data}. Ao salvar, o original é estornado e o corrigido é gravado. Limpar cancela a alteração."
        : "";

    public bool PartidaSimples => !PartidaDobrada;
    public string DescricaoModo => PartidaDobrada
        ? "Partida dobrada: cada linha é um lançamento (débito, crédito e valor)."
        : "Partida simples: uma conta por linha (D ou C); o lançamento fecha quando débitos = créditos.";

    /// <summary>Troca de empresa: limpa a digitação e recarrega contas, históricos e o modo.</summary>
    public async Task AoTrocarEmpresaAsync()
    {
        await CarregarAsync();
        Limpar();
    }

    /// <summary>Ao abrir a tela: contas analíticas (com reduzido), históricos padrão e o modo de lançamento.</summary>
    public async Task CarregarAsync()
    {
        var geracao = NovaGeracao();
        IReadOnlyList<ContaDetalhe> plano = [];
        IReadOnlyList<HistoricoPadrao> historicos = [];
        IReadOnlyList<Participante> participantes = [];
        var usa = false;
        var modo = ModoLancamento.PartidaDobrada;
        if (Contexto.App is { } app)
        {
            modo = (await app.ConfiguracoesGerais.ObterAsync()).ModoLancamento;
            if (Contexto.EmpresaId is { } empresa && Contexto.Sessao is { } sessao)
            {
                plano = await app.PlanoContas.ListarAsync(empresa, sessao.Id);
                historicos = await app.Historicos.ListarAsync(empresa, sessao.Id);
                usa = await app.Participantes.UsaParticipantesAsync(empresa);
                if (usa) participantes = await app.Participantes.ListarAsync(empresa, sessao.Id);
            }
        }
        if (!EhAtual(geracao)) return;
        _todasContas = plano.Where(c => c.Analitica && c.Ativa).Select(c => new ContaSelecionavel(c.Codigo, c.Descricao, c.CodigoReduzido)).ToArray();
        _porCodigo = _todasContas.ToDictionary(c => c.Codigo);
        _porReduzido = _todasContas.Where(c => c.Reduzido is not null).ToDictionary(c => c.Reduzido!.Value);
        _historicos = historicos.ToDictionary(h => h.Codigo);
        UsaParticipantes = usa;
        _participantes = participantes.Where(p => p.Ativo).ToDictionary(p => p.Codigo);
        _contasComParticipante = plano.Where(c => c.ExigeParticipante).Select(c => c.Codigo)
                                      .Concat(participantes.Where(p => p.ControleConta && p.CodigoConta is not null).Select(p => p.CodigoConta!))
                                      .ToHashSet();
        OnPropertyChanged(nameof(TodasContas));
        _dobradaConfigurada = modo == ModoLancamento.PartidaDobrada;
        if (EmEdicao) { Filtrar(); OnPropertyChanged(nameof(PodeEstornar)); await CarregarEfetuadosAsync(); return; }
        var trocouModo = PartidaDobrada != _dobradaConfigurada;
        PartidaDobrada = _dobradaConfigurada;
        if (trocouModo || Linhas.All(l => l.Vazia)) Limpar();
        Filtrar();
        OnPropertyChanged(nameof(PodeEstornar));
        await CarregarEfetuadosAsync();
    }

    /// <summary>Lista os lançamentos do período (os estornados continuam na lista, marcados).</summary>
    [RelayCommand]
    public async Task CarregarEfetuadosAsync()
    {
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var ini = Data(ListaInicio, new DateOnly(DateTime.Today.Year, 1, 1));
        var r = await app.Relatorios.DiarioAsync(empresa, ini, Data(ListaFim, ini.AddYears(1).AddDays(-1)), sessao.Id);
        if (!r.Sucesso) return;
        var id = EfetuadoSelecionado?.Id;
        Efetuados.Clear();
        foreach (var g in r.Valor.GroupBy(l => l.LancamentoId).OrderByDescending(g => g.First().Data).ThenByDescending(g => g.First().Numero))
        {
            var deb = g.Where(l => l.DC == IndicadorDC.D).ToList();
            var cred = g.Where(l => l.DC == IndicadorDC.C).ToList();
            static string Contas(List<LinhaDiario> ls) =>
                ls.Count == 0 ? "" : $"{ls[0].CodigoConta} {ls[0].DescricaoConta}" + (ls.Count > 1 ? $" (+{ls.Count - 1})" : "");
            var f = g.First();
            Efetuados.Add(new LancamentoEfetuadoLinha(g.Key, f.Numero, f.Data, Contas(deb), Contas(cred), deb.Sum(l => l.Valor), f.Historico,
                g.Select(l => l.Documento).FirstOrDefault(d => !string.IsNullOrEmpty(d)) ?? "", f.Status == StatusLancamento.Estornado));
        }
        EfetuadoSelecionado = Efetuados.FirstOrDefault(e => e.Id == id);
    }

    partial void OnEfetuadoSelecionadoChanged(LancamentoEfetuadoLinha? value)
    {
        if (value is not null) DataEstorno = value.DataLancamento.ToDateTime(TimeOnly.MinValue);
    }

    private bool PodeEstornarSelecionado() => EfetuadoSelecionado is { Estornado: false };

    /// <summary>Estorna o lançamento selecionado na lista (gera o lançamento inverso; o original fica marcado como estornado).</summary>
    [RelayCommand(CanExecute = nameof(PodeEstornarSelecionado))]
    private Task EstornarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (EfetuadoSelecionado is not { } sel) return;
        if (string.IsNullOrWhiteSpace(MotivoEstorno)) { Erros.Add("Informe o motivo do estorno."); return; }
        if (!await Contexto.ConfirmarAsync("Estornar lançamento",
                $"Estornar o lançamento nº {sel.Numero} de {sel.Data} ({sel.Valor})?\n\nSerá gerado o lançamento inverso; o original continua na lista, marcado como estornado.",
                "Estornar", "Cancelar")) return;
        var r = await app.Lancamentos.EstornarAsync(empresa, sel.Id, Data(DataEstorno, sel.DataLancamento), MotivoEstorno.Trim(), usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        MotivoEstorno = "";
        await CarregarEfetuadosAsync();
        Mensagem = $"Lançamento nº {sel.Numero} estornado pelo lançamento nº {r.Valor.Numero}.";
    }, Permissao.LancamentoEstornar);

    /// <summary>
    /// Duplo clique num lançamento efetuado: carrega as partidas na digitação para alterar. Débito e crédito únicos
    /// cabem numa linha da partida dobrada; os demais abrem em partida simples (uma conta por linha).
    /// </summary>
    public Task EditarAsync(LancamentoEfetuadoLinha efetuado) => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (efetuado.Estornado) { Erros.Add($"O lançamento nº {efetuado.Numero} está estornado e não pode ser alterado."); return; }
        if (!Contexto.Pode(Permissao.LancamentoEstornar)) { Erros.Add("Alterar um lançamento exige permissão para estornar."); return; }
        var r = await app.Lancamentos.ObterAsync(empresa, efetuado.Id, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var l = r.Valor;
        if (l.Origem != OrigemLancamento.Manual)
        {
            Erros.Add($"O lançamento nº {efetuado.Numero} não foi digitado manualmente; corrija-o por estorno.");
            return;
        }
        var ps = l.Partidas;
        var umaLinha = ps.Count == 2 && ps[0].DC == IndicadorDC.D && ps[1].DC == IndicadorDC.C && ps[0].Valor == ps[1].Valor
                       && ps[0].Historico == ps[1].Historico && ps[0].CodigoCentroCusto == ps[1].CodigoCentroCusto
                       && ps[0].NumeroArquivo == ps[1].NumeroArquivo;
        Editando = efetuado;
        PartidaDobrada = _dobradaConfigurada && umaLinha;
        Data = l.Data.ToDateTime(TimeOnly.MinValue);
        Linhas.Clear();
        var data = l.Data.ToString("dd/MM/yyyy");
        if (PartidaDobrada)
            Linhas.Add(new LinhaLancamentoViewModel
            {
                Data = data, Conta = ps[0].CodigoConta, ContaCredito = ps[1].CodigoConta, Valor = Formato.Moeda(ps[0].Valor),
                Documento = ps[0].NumeroArquivo ?? "", Historico = ps[0].Historico, CentroCusto = ps[0].CodigoCentroCusto ?? "",
                Participante = ps[0].CodigoParticipante ?? ps[1].CodigoParticipante ?? "",
            });
        else
            foreach (var p in ps)
                Linhas.Add(new LinhaLancamentoViewModel
                {
                    Data = data, Conta = p.CodigoConta, Dc = p.DC.ToString(), Valor = Formato.Moeda(p.Valor),
                    Documento = p.NumeroArquivo ?? "", Historico = p.Historico, CentroCusto = p.CodigoCentroCusto ?? "",
                    Participante = p.CodigoParticipante ?? "",
                });
        foreach (var linha in Linhas)                       // reduzidos e nomes das contas
        {
            ConfirmarConta(linha, credito: false);
            if (PartidaDobrada) ConfirmarConta(linha, credito: true);
            if (linha.Participante.Length > 0 && _participantes.TryGetValue(linha.Participante, out var pe)) linha.NomeParticipante = pe.Nome;
        }
        LinhaSelecionada = Linhas[0];
    }, Permissao.LancamentoIncluir);

    /// <summary>Enter no último campo: salva quando o lançamento já fecha; senão (false) segue para a próxima linha.</summary>
    public bool SalvarPeloEnter()
    {
        if (!Balanceado || !SalvarCommand.CanExecute(null)) return false;
        SalvarCommand.Execute(null);
        return true;
    }

    /// <summary>Grava a digitação: inclusão normal ou, em alteração, estorno do original + lançamento corrigido.</summary>
    private async Task<bool> GravarAsync(PerseusApp app, long empresa, long usuario, IReadOnlyList<LancamentoProposto> propostas)
    {
        if (Editando is { } ed)
        {
            if (propostas.Count != 1) { Erros.Add("Na alteração, o lançamento deve continuar sendo um só (uma linha)."); return false; }
            var s = await app.Lancamentos.SubstituirAsync(ed.Id, propostas[0], usuario);
            if (!s.Sucesso) { foreach (var e in s.Erros) Erros.Add(e); return false; }
            Mensagem = $"Lançamento nº {ed.Numero} alterado: estornado pelo nº {s.Valor[0].Numero} e regravado como nº {s.Valor[1].Numero}.";
            return true;
        }
        var r = await app.Lancamentos.EfetivarLoteAsync(propostas, null, usuario);
        if (!r.Sucesso)
        {
            // "Lançamento 2: ..." do serviço vira "Linha 2: ..." da tela.
            // Em partida simples é um lançamento só: sai o prefixo.
            foreach (var e in r.Erros)
                Erros.Add(!e.StartsWith("Lançamento ") ? e : PartidaDobrada ? "Linha " + e["Lançamento ".Length..] : e[(e.IndexOf(": ") + 2)..]);
            return false;
        }
        Mensagem = r.Valor.Count == 1 ? $"Lançamento nº {r.Valor[0].Numero} salvo."
                                      : $"{r.Valor.Count} lançamentos salvos (nº {r.Valor.Min(x => x.Numero)} a {r.Valor.Max(x => x.Numero)}).";
        return true;
    }

    partial void OnFiltroContaChanged(string value) => Filtrar();

    private void Filtrar()
    {
        Contas.Clear();
        foreach (var c in _todasContas.Where(c => Combina(c, FiltroConta))) Contas.Add(c);
    }

    /// <summary>Busca de conta: reduzido exato, início do código ou parte do nome.</summary>
    public static bool Combina(ContaSelecionavel c, string? texto)
    {
        var f = (texto ?? "").Trim();
        return f.Length == 0 || c.Codigo.StartsWith(f, StringComparison.Ordinal) || c.Reduzido?.ToString() == f
               || c.Descricao.Contains(f, StringComparison.CurrentCultureIgnoreCase);
    }

    /// <summary>Clique numa conta da lista: vai para a linha selecionada (débito, se vazio; senão crédito).</summary>
    partial void OnContaSelecionadaChanged(ContaSelecionavel? value)
    {
        if (value is null || LinhaSelecionada is not { } l) return;
        if (PartidaDobrada && l.Conta.Trim().Length > 0 && l.ContaCredito.Trim().Length == 0) l.ContaCredito = value.Codigo;
        else l.Conta = value.Codigo;
    }

    private LinhaLancamentoViewModel NovaLinha()
    {
        var anterior = Linhas.LastOrDefault();
        var nova = new LinhaLancamentoViewModel { Data = anterior?.Data ?? (Data ?? DateTime.Today).ToString("dd/MM/yyyy") };
        if (PartidaSimples)
        {
            nova.Historico = anterior?.Historico ?? "";
            nova.CodigoHistorico = anterior?.CodigoHistorico ?? "";
            nova.Documento = anterior?.Documento ?? "";
            nova.Dc = anterior?.Indicador == IndicadorDC.D ? "C" : "D";
            // Sugere o valor que equilibra o lançamento.
            var dif = SomaDC(IndicadorDC.D) - SomaDC(IndicadorDC.C);
            if (dif != 0)
            {
                nova.Dc = dif > 0 ? "C" : "D";
                nova.Valor = Formato.Moeda(Math.Abs(dif));
            }
        }
        return nova;
    }

    /// <summary>Seta para baixo na última linha: cria a próxima.</summary>
    public LinhaLancamentoViewModel AdicionarLinha()
    {
        var nova = NovaLinha().ComoNova();
        Linhas.Add(nova);
        LinhaSelecionada = nova;
        return nova;
    }

    /// <summary>Seta para cima numa linha recém-criada e não alterada: desfaz a criação. Retorna se cancelou.</summary>
    public bool CancelarLinhaNova(LinhaLancamentoViewModel linha)
    {
        if (!linha.Nova || linha.Editada || Linhas.Count <= MinimoLinhas || !ReferenceEquals(linha, Linhas[^1])) return false;
        Linhas.Remove(linha);
        LinhaSelecionada = Linhas[^1];
        return true;
    }

    private int MinimoLinhas => PartidaDobrada ? 1 : 2;

    [RelayCommand]
    private void Limpar()
    {
        if (Editando is not null)
        {
            Editando = null;
            PartidaDobrada = _dobradaConfigurada;
        }
        Linhas.Clear();
        var data = (Data ?? DateTime.Today).ToString("dd/MM/yyyy");
        if (PartidaDobrada) Linhas.Add(new LinhaLancamentoViewModel { Data = data });
        else
        {
            Linhas.Add(new LinhaLancamentoViewModel { Dc = "D" });
            Linhas.Add(new LinhaLancamentoViewModel { Dc = "C" });
        }
        LinhaSelecionada = Linhas[0];
    }

    /// <summary>Exclui a linha atual (Ctrl+Delete), mantendo o mínimo do modo.</summary>
    [RelayCommand]
    private void ExcluirLinha()
    {
        if (LinhaSelecionada is not { } l) return;
        if (Linhas.Count <= MinimoLinhas)
        {
            var i = Linhas.IndexOf(l);
            Linhas[i] = PartidaDobrada ? new LinhaLancamentoViewModel { Data = l.Data } : new LinhaLancamentoViewModel { Dc = l.Dc };
            LinhaSelecionada = Linhas[i];
            return;
        }
        var pos = Linhas.IndexOf(l);
        Linhas.Remove(l);
        LinhaSelecionada = Linhas[Math.Min(pos, Linhas.Count - 1)];
    }

    [RelayCommand(CanExecute = nameof(Balanceado))]
    private Task SalvarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (PartidaDobrada) await SalvarDobradaAsync(app, empresa, usuario);
        else await SalvarSimplesAsync(app, empresa, usuario);
    }, Permissao.LancamentoIncluir);

    private async Task SalvarDobradaAsync(PerseusApp app, long empresa, long usuario)
    {
        var preenchidas = Linhas.Where(l => !l.Vazia).ToList();
        var propostas = new List<LancamentoProposto>();
        for (var i = 0; i < preenchidas.Count; i++)
        {
            var l = preenchidas[i];
            var n = Linhas.IndexOf(l) + 1;
            var data = Formato.LerData(CompletarData(l.Data));
            if (data is null) Erros.Add($"Linha {n}: data inválida.");
            if (l.ValorDecimal is not > 0) Erros.Add($"Linha {n}: informe o valor.");
            if (l.Conta.Trim().Length == 0) Erros.Add($"Linha {n}: informe a conta débito.");
            if (l.ContaCredito.Trim().Length == 0) Erros.Add($"Linha {n}: informe a conta crédito.");
            if (data is null || l.ValorDecimal is not > 0 || l.Conta.Trim().Length == 0 || l.ContaCredito.Trim().Length == 0) continue;
            var cc = string.IsNullOrWhiteSpace(l.CentroCusto) ? null : l.CentroCusto.Trim();
            var (partD, partC) = (Part(l, l.Conta), Part(l, l.ContaCredito));
            if (l.Participante.Trim().Length > 0 && partD is null && partC is null)
            {
                Erros.Add($"Linha {n}: participante informado, mas nenhuma das contas controla participantes.");
                continue;
            }
            propostas.Add(new LancamentoProposto(empresa, data.Value, OrigemLancamento.Manual,
            [
                new PartidaProposta(l.Conta.Trim(), IndicadorDC.D, l.ValorDecimal.Value, l.Historico.Trim(), cc, partD, Doc(l)),
                new PartidaProposta(l.ContaCredito.Trim(), IndicadorDC.C, l.ValorDecimal.Value, l.Historico.Trim(), cc, partC, Doc(l)),
            ]));
        }
        if (Erros.Count > 0) return;
        if (propostas.Count == 0) { Erros.Add("Digite ao menos um lançamento."); return; }
        if (!await GravarAsync(app, empresa, usuario, propostas)) return;
        var ultimaData = preenchidas[^1].Data;
        Limpar();
        Linhas[0].Data = ultimaData;
        await CarregarEfetuadosAsync();
    }

    private async Task SalvarSimplesAsync(PerseusApp app, long empresa, long usuario)
    {
        var preenchidas = Linhas.Where(l => !l.Vazia).ToArray();
        var problemas = preenchidas.Select(l => (l, n: Linhas.IndexOf(l) + 1)).Select(x =>
                x.l.Indicador is null ? $"Linha {x.n}: indique D ou C."
                : x.l.ValorDecimal is not > 0 ? $"Linha {x.n}: valor inválido."
                : x.l.Conta.Trim().Length == 0 ? $"Linha {x.n}: informe a conta." : null)
            .OfType<string>().ToArray();
        if (problemas.Length > 0) { foreach (var p in problemas) Erros.Add(p); return; }
        if (preenchidas.Length < 2) { Erros.Add("Em partida simples o lançamento tem no mínimo 2 linhas."); return; }

        var proposta = new LancamentoProposto(empresa, Data(Data, DateOnly.FromDateTime(DateTime.Today)), OrigemLancamento.Manual,
            preenchidas.Select(l => new PartidaProposta(l.Conta.Trim(), l.Indicador!.Value, l.ValorDecimal!.Value, l.Historico.Trim(),
                string.IsNullOrWhiteSpace(l.CentroCusto) ? null : l.CentroCusto.Trim(), PartSimples(l), Doc(l))).ToArray());
        if (!await GravarAsync(app, empresa, usuario, [proposta])) return;
        Limpar();
        await CarregarEfetuadosAsync();
    }

    /// <summary>Participante da partida: só nas contas que controlam participantes (plano com participantes).</summary>
    private string? Part(LinhaLancamentoViewModel l, string conta) =>
        l.Participante.Trim() is { Length: > 0 } p && _contasComParticipante.Contains(conta.Trim()) ? p : null;

    /// <summary>Partida simples: a linha é uma conta só; o participante digitado vai na partida.</summary>
    private static string? PartSimples(LinhaLancamentoViewModel l) => l.Participante.Trim() is { Length: > 0 } p ? p : null;

    /// <summary>Nº do documento (NF, recibo, boleto...) — base da conciliação automática.</summary>
    private static string? Doc(LinhaLancamentoViewModel l) => string.IsNullOrWhiteSpace(l.Documento) ? null : l.Documento.Trim();

    /// <summary>Data digitada completada e no formato dd/mm/aaaa (ou como foi digitada, se inválida).</summary>
    public string DataCompleta(string texto) =>
        Formato.LerData(CompletarData(texto)) is { } d ? d.ToString("dd/MM/yyyy") : texto;

    /// <summary>"10/03" ou "10" usam o mês/ano da data padrão.</summary>
    private string CompletarData(string texto)
    {
        var t = texto.Trim();
        var padrao = Data ?? DateTime.Today;
        var partes = t.Split('/');
        return partes.Length switch
        {
            1 when int.TryParse(t, out var d) && d is >= 1 and <= 31 => $"{d:00}/{padrao:MM/yyyy}",
            2 when int.TryParse(partes[0], out var d) && int.TryParse(partes[1], out var m) => $"{d:00}/{m:00}/{padrao:yyyy}",
            _ => t,
        };
    }

    private decimal SomaDC(IndicadorDC dc) => Linhas.Where(l => l.Indicador == dc).Sum(l => l.ValorDecimal ?? 0);

    private void Recalcular()
    {
        if (PartidaDobrada)
        {
            var total = Linhas.Where(l => !l.Vazia).Sum(l => l.ValorDecimal ?? 0);
            TotalDebito = TotalCredito = Formato.Moeda(total);
            Diferenca = Formato.Moeda(0);
            Balanceado = Linhas.Any(l => !l.Vazia && l.ValorDecimal > 0 && l.Conta.Trim().Length > 0 && l.ContaCredito.Trim().Length > 0);
            return;
        }
        var d = SomaDC(IndicadorDC.D);
        var c = SomaDC(IndicadorDC.C);
        TotalDebito = Formato.Moeda(d);
        TotalCredito = Formato.Moeda(c);
        Diferenca = Formato.Moeda(d - c);
        Balanceado = d == c && d > 0;
    }

    partial void OnPartidaDobradaChanged(bool value) => Recalcular();

    private void AoMudarLinhas(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (LinhaLancamentoViewModel l in e.NewItems ?? Array.Empty<LinhaLancamentoViewModel>()) l.PropertyChanged += AoMudarLinha;
        foreach (LinhaLancamentoViewModel l in e.OldItems ?? Array.Empty<LinhaLancamentoViewModel>()) l.PropertyChanged -= AoMudarLinha;
        Recalcular();
    }

    /// <summary>Liga reduzido ↔ conta ↔ nome e o código do histórico ao texto fixo.</summary>
    private void AoMudarLinha(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is LinhaLancamentoViewModel l && !_resolvendo)
        {
            _resolvendo = true;
            try
            {
                switch (e.PropertyName)
                {
                    case nameof(LinhaLancamentoViewModel.Reduzido):
                        if (PorReduzido(l.Reduzido) is { } cr) (l.Conta, l.NomeConta) = (cr.Codigo, cr.Descricao);
                        else if (l.Reduzido.Trim().Length == 0) { } else l.NomeConta = "reduzido não encontrado";
                        break;
                    case nameof(LinhaLancamentoViewModel.Conta):
                        (l.Reduzido, l.NomeConta) = _porCodigo.TryGetValue(l.Conta.Trim(), out var cd)
                            ? (cd.Reduzido?.ToString() ?? "", cd.Descricao) : ("", "");
                        break;
                    case nameof(LinhaLancamentoViewModel.ReduzidoCredito):
                        if (PorReduzido(l.ReduzidoCredito) is { } cc) (l.ContaCredito, l.NomeContaCredito) = (cc.Codigo, cc.Descricao);
                        else if (l.ReduzidoCredito.Trim().Length > 0) l.NomeContaCredito = "reduzido não encontrado";
                        break;
                    case nameof(LinhaLancamentoViewModel.ContaCredito):
                        (l.ReduzidoCredito, l.NomeContaCredito) = _porCodigo.TryGetValue(l.ContaCredito.Trim(), out var cc2)
                            ? (cc2.Reduzido?.ToString() ?? "", cc2.Descricao) : ("", "");
                        break;
                    case nameof(LinhaLancamentoViewModel.Participante):
                        l.NomeParticipante = l.Participante.Trim() is { Length: > 0 } cp
                            ? _participantes.TryGetValue(cp, out var pa) ? pa.Nome : "participante não encontrado" : "";
                        break;
                    case nameof(LinhaLancamentoViewModel.CodigoHistorico):
                        if (_historicos.TryGetValue(l.CodigoHistorico.Trim(), out var h)) l.Historico = h.Descricao.TrimEnd() + " ";
                        break;
                }
            }
            finally { _resolvendo = false; }
        }
        Recalcular();
    }

    private ContaSelecionavel? PorReduzido(string texto) =>
        int.TryParse(texto.Trim(), out var n) && _porReduzido.TryGetValue(n, out var c) ? c : null;

    /// <summary>
    /// Ao sair do campo da conta: aceita o código, o reduzido ou o nome digitado (quando identifica uma única conta)
    /// e troca pelo código; senão avisa que a conta não foi encontrada.
    /// </summary>
    public void ConfirmarConta(LinhaLancamentoViewModel l, bool credito)
    {
        var (codigo, reduzido, nome) = Resolver(credito ? l.ContaCredito : l.Conta);
        _resolvendo = true;
        try
        {
            if (credito) (l.ContaCredito, l.ReduzidoCredito, l.NomeContaCredito) = (codigo, reduzido, nome);
            else (l.Conta, l.Reduzido, l.NomeConta) = (codigo, reduzido, nome);
        }
        finally { _resolvendo = false; }
        Recalcular();
    }

    /// <summary>Código digitado, reduzido ou nome (único ou parte única) → (código, reduzido, nome da conta).</summary>
    private (string Codigo, string Reduzido, string Nome) Resolver(string texto)
    {
        var t = texto.Trim();
        if (t.Length == 0) return ("", "", "");
        if (_porCodigo.TryGetValue(t, out var c)) return (c.Codigo, c.Reduzido?.ToString() ?? "", c.Descricao);
        if (PorReduzido(t) is { } r) return (r.Codigo, r.Reduzido?.ToString() ?? "", r.Descricao);
        var porNome = _todasContas.Where(x => string.Equals(x.Descricao, t, StringComparison.CurrentCultureIgnoreCase)).ToList();
        if (porNome.Count != 1) porNome = _todasContas.Where(x => x.Descricao.Contains(t, StringComparison.CurrentCultureIgnoreCase)).ToList();
        if (porNome.Count == 1) return (porNome[0].Codigo, porNome[0].Reduzido?.ToString() ?? "", porNome[0].Descricao);
        return (texto, "", _todasContas.Count == 0 ? "" : "conta não encontrada");
    }
}
