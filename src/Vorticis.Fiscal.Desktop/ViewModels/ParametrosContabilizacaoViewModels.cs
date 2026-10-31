using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Duplicatas;
using Vorticis.Infra;

namespace Vorticis.Fiscal.Desktop.ViewModels;

/// <summary>Regras de contabilização › aba Provisões de impostos: contas de débito e crédito da provisão de cada tributo apurado (FISCAL_APURAR).</summary>
public sealed class ProvisoesImpostosViewModel(IContexto contexto) : ParametrosViewModel(contexto)
{
    public override string Titulo => "Contabilização das provisões de impostos";
    public override string Descricao => "Contas usadas ao contabilizar a provisão de cada tributo apurado (Apuração › Simples Nacional, Lucro Presumido, Lucro Real, PIS e COFINS).";
    protected override Permissao PermissaoGravar => Permissao.FiscalApurar;

    protected override IReadOnlyList<GrupoParametros> MontarGrupos()
    {
        var provisoes = new List<CampoParametro>();
        foreach (var t in Vorticis.Fiscal.Guias.ServicoProvisoes.Tributos)
        {
            provisoes.Add(new($"provDebito{t}", $"{t} – conta de débito (despesa)", TipoCampoParametro.Conta, t == "DAS" ? "Conta analítica de despesa do tributo; é debitada na provisão da apuração." : null));
            provisoes.Add(new($"provCredito{t}", $"{t} – conta de crédito (imposto a recolher)", TipoCampoParametro.Conta, t == "DAS" ? "Conta analítica do passivo; é creditada na provisão da apuração." : null));
        }
        Observar(provisoes);
        return
        [
            new("Contabilização das provisões de impostos",
                "Contas de débito e de crédito usadas ao contabilizar a provisão de cada tributo apurado. Ficam liberados só os tributos do regime da empresa (Cadastros › Empresas, Dados cadastrais): " +
                "Simples Nacional, só o DAS; Lucro Presumido ou Real, IRPJ, CSLL, PIS e COFINS, e ICMS, IPI e ISS conforme a inscrição estadual e a atividade. Exige a função de apurar tributos.", provisoes),
        ];
    }

    protected override Task CarregarValoresAsync(VorticisApp app, long empresa, long usuario) => CarregarProvisoesAsync(app, empresa, usuario);

    protected override Task GravarValoresAsync(VorticisApp app, long empresa, long usuario) => GravarProvisoesAsync(app, empresa, usuario);

    private IReadOnlyList<string> _tributosLiberados = [];

    private async Task CarregarProvisoesAsync(VorticisApp app, long empresa, long usuario)
    {
        var detalhe = await app.Empresas.ObterAsync(empresa);
        _tributosLiberados = Vorticis.Fiscal.Guias.ServicoProvisoes.TributosDoRegime(detalhe?.Regime, detalhe?.InscricaoEstadual, detalhe?.CnaePrincipal);
        var opcoes = (await app.PlanoContas.ListarAsync(empresa, usuario)).Where(c => c.Analitica && c.Ativa).OrderBy(c => c.Codigo, StringComparer.Ordinal)
                        .Select(c => new ContaPesquisavel(c.Codigo, c.Descricao, c.CodigoReduzido)).ToList();
        var podeVer = Contexto.Pode(Permissao.FiscalApurar);
        var contas = podeVer ? (await app.Fiscal().Provisoes.ListarContasAsync(empresa, usuario)) : null;
        foreach (var t in Vorticis.Fiscal.Guias.ServicoProvisoes.Tributos)
        {
            var conta = contas is { Sucesso: true } ? contas.Valor.FirstOrDefault(x => x.Tributo == t) : null;
            // Tributo fora do regime, mas já configurado, continua liberado para o contador poder corrigir ou limpar.
            var liberado = podeVer && (_tributosLiberados.Contains(t) || conta is not null);
            foreach (var (chave, valor) in new[] { ($"provDebito{t}", conta?.ContaDespesa), ($"provCredito{t}", conta?.ContaPassivo) })
            {
                var campo = Campo(chave);
                campo.Contas = opcoes;
                campo.Texto = valor ?? "";
                campo.Habilitado = liberado;
            }
        }
    }

    private async Task GravarProvisoesAsync(VorticisApp app, long empresa, long usuario)
    {
        if (!Contexto.Pode(Permissao.FiscalApurar)) return;
        foreach (var t in Vorticis.Fiscal.Guias.ServicoProvisoes.Tributos)
        {
            var deb = Campo($"provDebito{t}");
            if (!deb.Habilitado) continue;
            var d = deb.Texto.Trim();
            var c = Campo($"provCredito{t}").Texto.Trim();
            Resultado r;
            if (d.Length == 0 && c.Length == 0) r = await app.Fiscal().Provisoes.ExcluirContaAsync(empresa, t, usuario);
            else if (d.Length == 0 || c.Length == 0) { Erros.Add($"{t}: informe as duas contas (débito e crédito) ou deixe as duas em branco."); continue; }
            else r = await app.Fiscal().Provisoes.GravarContaAsync(empresa, new Vorticis.Fiscal.Guias.ContaTributo(t, d, c), usuario);
            if (!r.Sucesso) MostrarResultado(r, "");
        }
    }

}

/// <summary>Regras de contabilização › aba Duplicatas e baixas: duplicata à vista, contas e históricos das baixas (FISCAL_DUPLICATAS_MANTER).</summary>
public sealed class DuplicatasBaixasViewModel(IContexto contexto) : ParametrosViewModel(contexto)
{
    public override string Titulo => "Duplicatas e baixas (contas a pagar e a receber)";
    public override string Descricao => "Contas contábeis e históricos usados na contabilização das notas com duplicatas e nas baixas (pagamentos e recebimentos).";
    protected override Permissao PermissaoGravar => Permissao.FiscalDuplicatasManter;

    protected override IReadOnlyList<GrupoParametros> MontarGrupos()
    {
        var duplicatas = new CampoParametro[]
        {
            new("dupAVista", "Gerar duplicata à vista nas notas sem cobrança", TipoCampoParametro.SimNao,
                "Nota sem parcelas (cobrança) no XML, ou digitada sem parcelas, gera uma duplicata única. NFC-e só gera se o XML trouxer cobrança."),
            new("dupAVistaDias", "Dias até o vencimento da duplicata à vista", TipoCampoParametro.Texto, "0 = vence na data de emissão."),
            new("contaFornecedores", "Conta de fornecedores", TipoCampoParametro.Texto,
                "Código da conta analítica. É a contrapartida das notas de entrada com duplicatas e a conta debitada no pagamento."),
            new("contaClientes", "Conta de clientes", TipoCampoParametro.Texto,
                "Código da conta analítica. É a contrapartida das notas de saída com duplicatas e a conta creditada no recebimento."),
            new("contaJurosPagos", "Juros pagos (despesa)", TipoCampoParametro.Texto),
            new("contaMultaPaga", "Multa paga (despesa)", TipoCampoParametro.Texto),
            new("contaDescontoObtido", "Descontos obtidos (receita)", TipoCampoParametro.Texto),
            new("contaJurosRecebidos", "Juros recebidos (receita)", TipoCampoParametro.Texto),
            new("contaMultaRecebida", "Multa recebida (receita)", TipoCampoParametro.Texto),
            new("contaDescontoConcedido", "Descontos concedidos (despesa)", TipoCampoParametro.Texto),
            new("historicoPagamento", "Histórico do pagamento", TipoCampoParametro.Texto, "Pode usar {DOCUMENTO}, {PARTICIPANTE}, {DATA} e {VALOR}."),
            new("historicoRecebimento", "Histórico do recebimento", TipoCampoParametro.Texto, "Pode usar {DOCUMENTO}, {PARTICIPANTE}, {DATA} e {VALOR}."),
        };
        Observar(duplicatas);
        return
        [
            new("Duplicatas e baixas (contas a pagar e a receber)", "Contas contábeis e históricos usados na contabilização das notas com duplicatas e nas baixas. Exige a função de manter duplicatas.", duplicatas),
        ];
    }

    protected override Task CarregarValoresAsync(VorticisApp app, long empresa, long usuario) => CarregarAsync(app, empresa, usuario);

    private async Task CarregarAsync(VorticisApp app, long empresa, long usuario)
    {
        var d = await app.Fiscal().Duplicatas.ObterConfiguracaoAsync(empresa, usuario);
        if (d.Sucesso)
        {
            var x = d.Valor;
            Campo("dupAVista").Marcado = x.GerarAVista;
            Campo("dupAVistaDias").Texto = x.AVistaDias.ToString();
            Campo("contaFornecedores").Texto = x.ContaFornecedores ?? "";
            Campo("contaClientes").Texto = x.ContaClientes ?? "";
            Campo("contaJurosPagos").Texto = x.ContaJurosPagos ?? "";
            Campo("contaMultaPaga").Texto = x.ContaMultaPaga ?? "";
            Campo("contaDescontoObtido").Texto = x.ContaDescontoObtido ?? "";
            Campo("contaJurosRecebidos").Texto = x.ContaJurosRecebidos ?? "";
            Campo("contaMultaRecebida").Texto = x.ContaMultaRecebida ?? "";
            Campo("contaDescontoConcedido").Texto = x.ContaDescontoConcedido ?? "";
            Campo("historicoPagamento").Texto = x.HistoricoPagamento;
            Campo("historicoRecebimento").Texto = x.HistoricoRecebimento;
        }
    }

    protected override async Task GravarValoresAsync(VorticisApp app, long empresa, long usuario)
    {
        if (Contexto.Pode(Permissao.FiscalDuplicatasManter))
        {
            if (!int.TryParse(Campo("dupAVistaDias").Texto.Trim(), out var diasAVista)) { Erros.Add("Informe os dias até o vencimento da duplicata à vista em número (0 a 365)."); return; }
            string? Conta(string chave) => Campo(chave).Texto;
            var dup = await app.Fiscal().Duplicatas.GravarConfiguracaoAsync(empresa, new ConfiguracaoDuplicatas(Campo("dupAVista").Marcado, diasAVista, Conta("contaFornecedores"),
                Conta("contaClientes"), Conta("contaJurosPagos"), Conta("contaMultaPaga"), Conta("contaDescontoObtido"), Conta("contaJurosRecebidos"), Conta("contaMultaRecebida"),
                Conta("contaDescontoConcedido"), Campo("historicoPagamento").Texto, Campo("historicoRecebimento").Texto), usuario);
            if (!dup.Sucesso) MostrarResultado(dup, "");
        }
    }
}
