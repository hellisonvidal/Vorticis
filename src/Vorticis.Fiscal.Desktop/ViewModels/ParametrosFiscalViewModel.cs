using Vorticis.Desktop.Comum.ViewModels;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Sped;
using Vorticis.Infra;

namespace Vorticis.Fiscal.Desktop.ViewModels;

/// <summary>
/// Cadastros › Parâmetros (Fiscal): centros de custo nas notas e padrões da escrituração SPED (EFD) da empresa ativa.
/// Usa os mesmos serviços das telas Configurações fiscais e SPED; as contas dos tributos e as regras de contabilização,
/// por serem tabelas, ficam em suas telas e aparecem aqui como atalhos.
/// </summary>
public sealed class ParametrosFiscalViewModel(IContexto contexto) : ParametrosViewModel(contexto)
{
    public override string Titulo => "Parâmetros do Fiscal";

    public override string Descricao =>
        "Configurações fiscais da empresa ativa. Cada grupo é gravado pelo serviço da própria função, com a permissão correspondente.";

    protected override Permissao PermissaoGravar => Permissao.FiscalDocumentosImportar;

    private bool PodeSped => Contexto.Pode(Permissao.FiscalSpedGerar);

    private static readonly IReadOnlyList<OpcaoParametro> Perfis = [new("A", "Perfil A"), new("B", "Perfil B"), new("C", "Perfil C")];
    private static readonly IReadOnlyList<OpcaoParametro> AtividadesIcms = [new("0", "Industrial ou equiparado"), new("1", "Outros")];
    private static readonly IReadOnlyList<OpcaoParametro> NaturezasPj =
        [new("00", "00 – Pessoa jurídica em geral"), new("01", "01 – Sociedade cooperativa"), new("02", "02 – Entidade sujeita ao PIS/Pasep exclusivamente sobre a folha")];
    private static readonly IReadOnlyList<OpcaoParametro> AtividadesContrib =
        [new("0", "Industrial ou equiparado"), new("1", "Prestador de serviços"), new("2", "Comércio"), new("3", "Financeira"), new("4", "Imobiliária"), new("9", "Outros")];

    protected override IReadOnlyList<GrupoParametros> MontarGrupos()
    {
        var notas = new CampoParametro[]
        {
            new("usaCentroCusto", "Informar centro de custo nos itens das notas", TipoCampoParametro.SimNao,
                "Só pode ser ligado se a empresa usa centros de custo na contabilidade."),
        };
        var icms = new CampoParametro[]
        {
            new("icmsPerfil", "Perfil da EFD ICMS/IPI", TipoCampoParametro.Escolha, null, Perfis),
            new("icmsAtividade", "Atividade (ICMS/IPI)", TipoCampoParametro.Escolha, null, AtividadesIcms),
            new("icmsReceita", "Código de receita do ICMS próprio", TipoCampoParametro.Texto, "Código definido pela UF da empresa."),
            new("icmsReceitaSt", "Código de receita do ICMS-ST", TipoCampoParametro.Texto),
            new("icmsDia", "Dia de vencimento do ICMS (1 a 28)", TipoCampoParametro.Texto),
        };
        var contrib = new CampoParametro[]
        {
            new("contribNatureza", "Natureza da pessoa jurídica", TipoCampoParametro.Escolha, null, NaturezasPj),
            new("contribAtividade", "Atividade (PIS/COFINS)", TipoCampoParametro.Escolha, null, AtividadesContrib),
        };
        Observar([.. notas, .. icms, .. contrib]);
        return
        [
            new("Lançamento das notas", "Opções usadas na digitação de notas de entrada e saída.", notas,
                [Atalho("Abrir Configurações fiscais da empresa", Telas.ConfiguracoesFiscais)]),
            new("EFD ICMS/IPI (SPED Fiscal)", "Escolhas que se repetem a cada mês; o assistente já abre preenchido. Exige permissão de gerar SPED.", icms),
            new("EFD-Contribuições (PIS/COFINS)", "Escolhas que se repetem a cada mês na EFD-Contribuições. Exige permissão de gerar SPED.", contrib),
            new("Contabilização e tributos", "Tabelas que têm tela própria.", [],
                [Atalho("Abrir Regras de contabilização", Telas.RegrasContabilizacao), Atalho("Abrir Contas dos tributos", Telas.ContasTributos)]),
        ];
    }

    protected override async Task CarregarValoresAsync(VorticisApp app, long empresa, long usuario)
    {
        var f = await app.Fiscal().Digitacao.ObterConfiguracaoAsync(empresa, usuario);
        if (f.Sucesso) Campo("usaCentroCusto").Marcado = f.Valor.UsaCentroCusto;
        var e = await app.Fiscal().Efd.ConfiguracaoAsync(empresa, usuario);
        var c = e.Sucesso ? e.Valor : new ConfiguracaoEfd();
        Campo("icmsPerfil").DefinirEscolha(c.IcmsPerfil.ToString());
        Campo("icmsAtividade").DefinirEscolha(c.IcmsIndicadorAtividade.ToString());
        Campo("icmsReceita").Texto = c.IcmsCodigoReceita ?? "";
        Campo("icmsReceitaSt").Texto = c.IcmsCodigoReceitaSt ?? "";
        Campo("icmsDia").Texto = c.IcmsDiaVencimento.ToString();
        Campo("contribNatureza").DefinirEscolha(c.ContribNaturezaPj);
        Campo("contribAtividade").DefinirEscolha(c.ContribIndicadorAtividade.ToString());
    }

    protected override async Task GravarValoresAsync(VorticisApp app, long empresa, long usuario)
    {
        var f = await app.Fiscal().Digitacao.GravarConfiguracaoAsync(empresa, Campo("usaCentroCusto").Marcado, usuario);
        if (!f.Sucesso) { MostrarResultado(f, ""); return; }
        if (!PodeSped) return;
        if (!int.TryParse(Campo("icmsDia").Texto.Trim(), out var dia)) { Erros.Add("Informe o dia de vencimento do ICMS em número (1 a 28)."); return; }
        static string? T(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        var atual = (await app.Fiscal().Efd.ConfiguracaoAsync(empresa, usuario)).Valor ?? new ConfiguracaoEfd();
        var r = await app.Fiscal().Efd.SalvarConfiguracaoAsync(empresa, atual with
        {
            IcmsPerfil = Campo("icmsPerfil").ValorEscolha![0], IcmsIndicadorAtividade = Campo("icmsAtividade").ValorEscolha![0],
            IcmsCodigoReceita = T(Campo("icmsReceita").Texto), IcmsCodigoReceitaSt = T(Campo("icmsReceitaSt").Texto), IcmsDiaVencimento = dia,
            ContribNaturezaPj = Campo("contribNatureza").ValorEscolha!, ContribIndicadorAtividade = Campo("contribAtividade").ValorEscolha![0],
        }, usuario);
        if (!r.Sucesso) MostrarResultado(r, "");
    }
}
