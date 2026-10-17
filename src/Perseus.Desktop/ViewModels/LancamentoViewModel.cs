using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    [ObservableProperty] private string _codigoHistorico = "";
    [ObservableProperty] private string _historico = "";
    [ObservableProperty] private string _centroCusto = "";

    /// <summary>Criada pela seta para baixo e ainda não alterada pelo usuário (a seta para cima a cancela).</summary>
    public bool Nova { get; set; }
    public bool Editada { get; private set; }

    public LinhaLancamentoViewModel() => PropertyChanged += (_, e) =>
    {
        if (e.PropertyName is not (nameof(NomeConta) or nameof(NomeContaCredito) or nameof(ResumoContas))) Editada = true;
    };

    /// <summary>Marca a linha como recém-criada depois de preencher as sugestões (que não contam como edição).</summary>
    public LinhaLancamentoViewModel ComoNova()
    {
        (Nova, Editada) = (true, false);
        return this;
    }

    /// <summary>Nomes das contas da linha (rodapé da tela).</summary>
    public string ResumoContas => NomeContaCredito.Length == 0 ? NomeConta
        : $"Débito: {(NomeConta.Length > 0 ? NomeConta : "—")}   ·   Crédito: {NomeContaCredito}";

    public decimal? ValorDecimal => Formato.LerValor(Valor);
    public IndicadorDC? Indicador => Dc.Trim().ToUpperInvariant() switch { "D" => IndicadorDC.D, "C" => IndicadorDC.C, _ => null };
    public bool Vazia => Conta.Trim().Length == 0 && ContaCredito.Trim().Length == 0 && Valor.Trim().Length == 0;

    /// <summary>Valor com separador de milhar e duas casas (1.500,00), aplicado ao sair do campo.</summary>
    public void FormatarValor()
    {
        if (ValorDecimal is { } v) Valor = Formato.Moeda(v);
    }
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
        var modo = ModoLancamento.PartidaDobrada;
        if (Contexto.App is { } app)
        {
            modo = (await app.ConfiguracoesGerais.ObterAsync()).ModoLancamento;
            if (Contexto.EmpresaId is { } empresa && Contexto.Sessao is { } sessao)
            {
                plano = await app.PlanoContas.ListarAsync(empresa, sessao.Id);
                historicos = await app.Historicos.ListarAsync(empresa, sessao.Id);
            }
        }
        if (!EhAtual(geracao)) return;
        _todasContas = plano.Where(c => c.Analitica && c.Ativa).Select(c => new ContaSelecionavel(c.Codigo, c.Descricao, c.CodigoReduzido)).ToArray();
        _porCodigo = _todasContas.ToDictionary(c => c.Codigo);
        _porReduzido = _todasContas.Where(c => c.Reduzido is not null).ToDictionary(c => c.Reduzido!.Value);
        _historicos = historicos.ToDictionary(h => h.Codigo);
        OnPropertyChanged(nameof(TodasContas));
        var trocouModo = PartidaDobrada != (modo == ModoLancamento.PartidaDobrada);
        PartidaDobrada = modo == ModoLancamento.PartidaDobrada;
        if (trocouModo || Linhas.All(l => l.Vazia)) Limpar();
        Filtrar();
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
            propostas.Add(new LancamentoProposto(empresa, data.Value, OrigemLancamento.Manual,
            [
                new PartidaProposta(l.Conta.Trim(), IndicadorDC.D, l.ValorDecimal.Value, l.Historico.Trim(), cc),
                new PartidaProposta(l.ContaCredito.Trim(), IndicadorDC.C, l.ValorDecimal.Value, l.Historico.Trim(), cc),
            ]));
        }
        if (Erros.Count > 0) return;
        if (propostas.Count == 0) { Erros.Add("Digite ao menos um lançamento."); return; }
        var r = await app.Lancamentos.EfetivarLoteAsync(propostas, null, usuario);
        if (!r.Sucesso)
        {
            // "Lançamento 2: ..." do serviço vira "Linha 2: ..." da tela.
            foreach (var e in r.Erros) Erros.Add(e.StartsWith("Lançamento ") ? "Linha " + e["Lançamento ".Length..] : e);
            return;
        }
        Mensagem = r.Valor.Count == 1 ? $"Lançamento nº {r.Valor[0].Numero} salvo."
                                      : $"{r.Valor.Count} lançamentos salvos (nº {r.Valor.Min(x => x.Numero)} a {r.Valor.Max(x => x.Numero)}).";
        var ultimaData = preenchidas[^1].Data;
        Limpar();
        Linhas[0].Data = ultimaData;
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
                string.IsNullOrWhiteSpace(l.CentroCusto) ? null : l.CentroCusto.Trim())).ToArray());
        var r = await app.Lancamentos.EfetivarAsync(proposta, usuario);
        MostrarResultado(r, r.Sucesso ? $"Lançamento nº {r.Valor.Numero} salvo." : "");
        if (r.Sucesso) Limpar();
    }

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
