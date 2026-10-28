using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vorticis.Application.Contabil;
using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Desktop.ViewModels;

/// <summary>Lançamentos › Encerramento do exercício (apuração do resultado).</summary>
public partial class EncerramentoViewModel(IContexto contexto) : ModuloViewModel(contexto)
{
    private static readonly ContasEncerramento Padrao = new();

    [ObservableProperty] private decimal? _ano = DateTime.Today.Year;
    [ObservableProperty] private string _contaApuracao = Padrao.Apuracao;
    [ObservableProperty] private string _contaLucros = Padrao.LucrosAcumulados;
    [ObservableProperty] private string _contaPrejuizos = Padrao.PrejuizosAcumulados;
    [ObservableProperty] private string _previa = "";
    [ObservableProperty] private bool _encerrado;
    [ObservableProperty] private string _motivo = "";
    /// <summary>Contas do plano da empresa para a caixa de pesquisa (apuração = resultado; lucros e prejuízos = patrimônio líquido).</summary>
    [ObservableProperty] private IReadOnlyList<ContaPesquisavel> _opcoesApuracao = [];
    [ObservableProperty] private IReadOnlyList<ContaPesquisavel> _opcoesPatrimonio = [];

    /// <summary>Contas de apuração e de lucros/prejuízos configuradas na empresa (Cadastros › Empresas › Contabilidade); sem configuração, as do modelo básico.</summary>
    public async Task CarregarAsync()
    {
        if (Contexto.App is not { } app || Contexto.Sessao is not { } sessao || Contexto.EmpresaId is not { } empresa) return;
        var geracao = NovaGeracao();
        _contasCarregadas = false;
        var cfg = await app.PlanoContas.ObterConfiguracaoAsync(empresa, sessao.Id);
        var opcoes = OpcoesContasEncerramento.De(await app.PlanoContas.ListarAsync(empresa, sessao.Id));
        if (!EhAtual(geracao)) return;
        (OpcoesApuracao, OpcoesPatrimonio) = (opcoes.Apuracao, opcoes.Patrimonio);
        AplicarContas(cfg);
        (Previa, Encerrado) = ("", false);
    }

    private bool _contasCarregadas;

    private void AplicarContas(Vorticis.Application.Cadastros.ConfiguracaoContabil cfg)
    {
        (ContaApuracao, ContaLucros, ContaPrejuizos) =
            (cfg.ContaApuracao ?? Padrao.Apuracao, cfg.ContaLucros ?? Padrao.LucrosAcumulados, cfg.ContaPrejuizos ?? Padrao.PrejuizosAcumulados);
        _contasCarregadas = true;
    }

    private ContasEncerramento Contas => new(ContaApuracao.Trim(), ContaLucros.Trim(), ContaPrejuizos.Trim());
    private int AnoInt => (int)(Ano ?? DateTime.Today.Year);

    /// <summary>Aviso antes de executar: as três contas precisam existir no plano da empresa, estar na máscara, ser analíticas e da natureza certa.</summary>
    private async Task<bool> ContasConferidasAsync(Vorticis.Infra.VorticisApp app, long empresa, long usuario)
    {
        var avisos = await app.PlanoContas.ConferirContasEncerramentoAsync(empresa, Contas, usuario);
        if (avisos.Count == 0) return true;
        Erros.Add("Corrija as contas do encerramento antes de continuar:");
        foreach (var a in avisos) Erros.Add(a);
        return false;
    }

    [RelayCommand]
    private Task PreverAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        NovaGeracao();   // uma carga da tela ainda em andamento não apaga a prévia
        Previa = "";
        // a carga interrompida acima não chegou a trazer as contas da empresa: busca-as agora (senão valeriam as do modelo)
        if (!_contasCarregadas) AplicarContas(await app.PlanoContas.ObterConfiguracaoAsync(empresa, usuario));
        if (!await ContasConferidasAsync(app, empresa, usuario)) return;
        var r = await app.Encerramento.PreverAsync(empresa, AnoInt, Contas, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var p = r.Valor;
        Encerrado = p.LancamentoExistente is not null;
        Previa = Encerrado
            ? $"O exercício {p.Ano} já está encerrado (lançamento nº {p.LancamentoExistente})."
            : $"Data do encerramento: {p.Data:dd/MM/yyyy}\n" +
              $"Receitas (saldos credores): {Numeros.Moeda(p.Receitas)}\n" +
              $"Custos e despesas (saldos devedores): {Numeros.Moeda(p.CustosDespesas)}\n" +
              $"{(p.Resultado >= 0 ? "LUCRO" : "PREJUÍZO")} DO EXERCÍCIO: {Numeros.Moeda(Math.Abs(p.Resultado))}\n" +
              $"{p.ContasAZerar} conta(s) de resultado serão zeradas contra {Contas.Apuracao}, e o resultado irá para " +
              $"{(p.Resultado >= 0 ? Contas.LucrosAcumulados : Contas.PrejuizosAcumulados)}.";
    }, Permissao.ExercicioEncerrar);

    [RelayCommand]
    private Task EncerrarAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        if (!await ContasConferidasAsync(app, empresa, usuario)) return;
        var r = await app.Encerramento.EncerrarAsync(empresa, AnoInt, Contas, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        await PreverAsync();
        Mensagem = $"Exercício {AnoInt} encerrado: lançamento(s) nº {string.Join(", ", r.Valor.Select(l => l.Numero))} (IND_LCTO = E, em 31/12).";
    }, Permissao.ExercicioEncerrar);

    [RelayCommand]
    private Task DesfazerAsync() => ExecutarAsync(async (app, empresa, usuario) =>
    {
        var r = await app.Encerramento.DesfazerAsync(empresa, AnoInt, Motivo, usuario);
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        Motivo = "";
        await PreverAsync();
        Mensagem = $"Encerramento de {AnoInt} desfeito por estorno (lançamento(s) nº {string.Join(", ", r.Valor.Select(l => l.Numero))}).";
    }, Permissao.ExercicioEncerrar);
}
