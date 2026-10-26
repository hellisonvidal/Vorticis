using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Desktop.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Regras;

namespace Vorticis.Fiscal.Desktop.ViewModels;

public sealed record RegraFiscalLinha(RegraFiscal Regra)
{
    public string Operacao => Regra.Operacao.Rotulo();
    public int Ordem => Regra.Ordem;
    public string CfopOrigem => Regra.CfopOrigem ?? "";
    public string CfopDestino => Regra.CfopDestino ?? "";
    public string Cst => Regra.CstDestino ?? "";
    public string Condicoes => string.Join("; ", new[]
    {
        Regra.ParticipanteNome is { } n ? $"Participante {n}" : null, Regra.Ncm is { } ncm ? $"NCM {ncm}" : null,
        Regra.ProdutoDescricao is { } p ? $"Produto {p}" : null, Regra.CstOrigem is { } c ? $"CST origem {c}" : null,
        Regra.Destino == "I" ? "Dentro do estado" : Regra.Destino == "X" ? "Fora do estado" : null,
    }.Where(x => x is not null));
    public string Creditos => Regra.Operacao == TipoOperacao.Saida ? "" : string.Join(" ", new[]
    {
        Regra.CreditaIcms ? "ICMS" : null, Regra.CreditaIpi ? "IPI" : null, Regra.CreditaPis ? "PIS" : null, Regra.CreditaCofins ? "COFINS" : null,
    }.Where(x => x is not null)) is { Length: > 0 } t ? t : "Nenhum";
    public string Ativa => Regra.Ativo ? "Sim" : "Não";
}

/// <summary>Cadastros › Regras fiscais: de-para de CFOP das notas de entrada importadas e sugestões para as notas de saída.</summary>
public partial class RegrasFiscaisViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    public static IReadOnlyList<string> Operacoes { get; } = ["Entrada (importação de XML)", "Saída (sugestão na digitação)"];
    public static IReadOnlyList<string> Destinos { get; } = ["Qualquer", "Dentro do estado", "Outro estado"];

    public ObservableCollection<RegraFiscalLinha> Regras { get; } = [];

    [ObservableProperty] private RegraFiscalLinha? _selecionada;
    [ObservableProperty] private int _operacao;
    [ObservableProperty] private string _ordem = "100";
    [ObservableProperty] private string _descricao = "";
    [ObservableProperty] private string _cfopOrigem = "";
    [ObservableProperty] private string _participante = "";
    [ObservableProperty] private string _ncm = "";
    [ObservableProperty] private string _produto = "";
    [ObservableProperty] private string _cstOrigem = "";
    [ObservableProperty] private int _destino;
    [ObservableProperty] private string _cfopDestino = "";
    [ObservableProperty] private string _cstDestino = "";
    [ObservableProperty] private bool _creditaIcms = true;
    [ObservableProperty] private bool _creditaIpi = true;
    [ObservableProperty] private bool _creditaPis = true;
    [ObservableProperty] private bool _creditaCofins = true;
    [ObservableProperty] private string _contaDebito = "";
    [ObservableProperty] private string _contaCredito = "";
    [ObservableProperty] private bool _ativa = true;

    public bool PodeManter => Contexto.Pode(Permissao.FiscalDocumentosImportar);
    public void AtualizarPermissoes() => OnPropertyChanged(nameof(PodeManter));

    public Task CarregarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Fiscal().Regras.ListarAsync(empresa, null, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Regras.Clear();
        foreach (var x in r.Valor) Regras.Add(new RegraFiscalLinha(x));
        Mensagem = r.Valor.Count == 0 ? "Nenhuma regra cadastrada. Clique em \"Gerar regras padrão\" para começar." : null;
    }, Permissao.FiscalDocumentosConsultar);

    partial void OnSelecionadaChanged(RegraFiscalLinha? value)
    {
        if (value is null) return;
        var r = value.Regra;
        Operacao = r.Operacao == TipoOperacao.Entrada ? 0 : 1;
        (Ordem, Descricao, CfopOrigem, Ncm, CstOrigem, CfopDestino, CstDestino) = (r.Ordem.ToString(), r.Descricao ?? "", r.CfopOrigem ?? "", r.Ncm ?? "", r.CstOrigem ?? "", r.CfopDestino ?? "", r.CstDestino ?? "");
        (Participante, Produto) = (r.ParticipanteId is null ? "" : r.ParticipanteNome ?? "", r.ProdutoId is null ? "" : r.ProdutoDescricao ?? "");
        Destino = r.Destino == "I" ? 1 : r.Destino == "X" ? 2 : 0;
        (CreditaIcms, CreditaIpi, CreditaPis, CreditaCofins, Ativa) = (r.CreditaIcms, r.CreditaIpi, r.CreditaPis, r.CreditaCofins, r.Ativo);
        (ContaDebito, ContaCredito) = (r.ContaDebito ?? "", r.ContaCredito ?? "");
    }

    [RelayCommand]
    private void Nova()
    {
        Selecionada = null;
        (Ordem, Descricao, CfopOrigem, Participante, Ncm, Produto, CstOrigem, CfopDestino, CstDestino, ContaDebito, ContaCredito) = ("100", "", "", "", "", "", "", "", "", "", "");
        (Destino, CreditaIcms, CreditaIpi, CreditaPis, CreditaCofins, Ativa) = (0, true, true, true, true, true);
        Erros.Clear();
        Mensagem = null;
    }

    [RelayCommand]
    private Task SalvarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        long? participanteId = null, produtoId = null;
        if (!string.IsNullOrWhiteSpace(Participante))
        {
            var doc = new string(Participante.Where(char.IsAsciiDigit).ToArray());
            var lista = await app.Participantes.ListarAsync(empresa, usuario);
            var achados = lista.Where(p => (doc.Length > 0 && p.CpfCnpj == doc) || p.Nome.Contains(Participante.Trim(), StringComparison.CurrentCultureIgnoreCase)).ToList();
            if (achados.Count != 1) { Erros.Add($"Participante \"{Participante.Trim()}\" não encontrado ou ambíguo: informe o CNPJ/CPF."); return; }
            participanteId = achados[0].Id;
        }
        if (!string.IsNullOrWhiteSpace(Produto))
        {
            var p = await Resolvedores.ProdutoAsync(app, empresa, usuario, Produto, true);
            if (p is null) { Erros.Add($"Produto \"{Produto.Trim()}\" não encontrado ou ambíguo (cadastre em Cadastros › Produtos)."); return; }
            produtoId = p.Id;
        }
        if (!int.TryParse(Ordem.Trim(), out var ordem)) { Erros.Add("Ordem: informe um número."); return; }
        var op = Operacao == 0 ? TipoOperacao.Entrada : TipoOperacao.Saida;
        var regra = new RegraFiscal(Selecionada?.Regra.Id ?? 0, op, ordem, Ativa, Descricao, CfopOrigem, participanteId, Ncm, produtoId, CstOrigem,
            Destino == 1 ? "I" : Destino == 2 ? "X" : null, CfopDestino, CstDestino, CreditaIcms, CreditaIpi, CreditaPis, CreditaCofins, ContaDebito, ContaCredito);
        var r = await app.Fiscal().Regras.GravarAsync(empresa, regra, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Selecionada = Regras.FirstOrDefault(x => x.Regra.Id == r.Valor);
        Mensagem = "Regra gravada. Ela vale para as próximas importações; use \"Reaplicar regras\" nas notas de entrada para as já importadas.";
    }, Permissao.FiscalDocumentosImportar);

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (Selecionada is not { } s) { Erros.Add("Selecione uma regra."); return; }
        if (!await Contexto.ConfirmarAsync("Excluir regra", "Excluir a regra selecionada?", "Excluir", "Cancelar")) return;
        var r = await app.Fiscal().Regras.ExcluirAsync(empresa, s.Regra.Id, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Nova();
        Mensagem = "Regra excluída.";
    }, Permissao.FiscalDocumentosImportar);

    [RelayCommand]
    private Task GerarPadraoAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Fiscal().Regras.GerarPadraoAsync(empresa, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await CarregarAsync();
        Mensagem = r.Valor == 0 ? "As regras padrão já estavam cadastradas." : $"{r.Valor} regra(s) padrão criada(s). Operações sem correspondência segura ficam sem regra e são marcadas para revisão.";
    }, Permissao.FiscalDocumentosImportar);
}
