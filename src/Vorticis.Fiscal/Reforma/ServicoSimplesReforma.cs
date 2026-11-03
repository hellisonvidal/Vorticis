using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Comum;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Simples;

namespace Vorticis.Fiscal.Reforma;

/// <summary>Sugestão de receita, compras e folha dos últimos 12 meses (histórico de receita e folha e notas de entrada), para preencher a tela.</summary>
public sealed record SugestaoSimulacao(decimal Receita12, decimal Compras12, decimal Folha12, IReadOnlyList<string> Avisos);

/// <summary>
/// Apuração › Simples Nacional × IBS/CBS: compara o imposto de hoje (DAS) com o de cada ano da reforma tributária, para as duas escolhas do Simples
/// (IBS/CBS dentro do DAS ou fora dele, pelo regime regular). Cronograma e alíquotas em core.parametro_legal (REFORMA_SIMPLES e REFORMA_IBS_CBS);
/// tabelas do Simples em SIMPLES_NACIONAL. FISCAL_APURAR ou FISCAL_RELATORIOS.
/// </summary>
public sealed class ServicoSimplesReforma(ServicoSimplesNacional simples, ServicoReformaTributaria reforma, IRepositorioSimples repositorioSimples, IRepositorioDocumentos documentos,
                                          IParametrosFiscais parametros, Autorizador autorizador)
{
    public const string Chave = "REFORMA_SIMPLES";

    private async Task<string?> PermitirAsync(long empresa, long usuario, CancellationToken ct)
    {
        var p = await autorizador.ObterAsync(usuario, empresa, ct);
        return p.Pode(Permissao.FiscalApurar) || p.Pode(Permissao.FiscalRelatorios) ? null : "Acesso negado: é preciso poder apurar ou emitir relatórios fiscais.";
    }

    public async Task<CronogramaReforma> CronogramaAsync(CancellationToken ct = default)
    {
        var json = await parametros.ObterJsonAsync(Chave, new DateOnly(2026, 1, 1), ct);
        var teste = (await reforma.AliquotasAsync(2026, ct)).FirstOrDefault(a => a.Ano == 2026);
        return CronogramaReforma.Carregar(json, teste);
    }

    /// <summary>Soma dos 12 meses anteriores ao mês corrente: receita e folha do histórico, compras das notas de entrada autorizadas com direito a crédito.</summary>
    public async Task<Resultado<SugestaoSimulacao>> SugerirAsync(long empresa, DateOnly hoje, long usuario, CancellationToken ct = default)
    {
        if (await PermitirAsync(empresa, usuario, ct) is { } erro) return Resultado<SugestaoSimulacao>.Falha(erro);
        var fim = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-1);
        var inicio = fim.AddMonths(-11);
        var avisos = new List<string>();
        var historico = await repositorioSimples.ListarBaseMensalAsync(empresa, inicio, fim, ct);
        var receita = historico.Sum(b => b.Receita ?? 0m);
        var folha = historico.Sum(b => b.Folha ?? 0m);
        var semReceita = Enumerable.Range(0, 12).Select(i => inicio.AddMonths(i)).Where(m => historico.All(b => b.Competencia != m || b.Receita is null)).ToList();
        if (semReceita.Count > 0)
            avisos.Add($"Receita sem lançamento em {semReceita.Count} dos 12 meses (complete em Apuração › Histórico de receita e folha ou digite o valor).");
        var itens = await documentos.ListarItensAsync(empresa, inicio, fim.AddMonths(1).AddDays(-1), TipoOperacao.Entrada, ct);
        var compras = itens.Where(i => i.Situacao == SituacaoDocumento.Autorizada && (i.Item.CreditaPis || i.Item.CreditaCofins || i.Item.CreditaIcms))
                           .Sum(i => i.Item.ValorContabil);
        if (compras == 0) avisos.Add("Nenhuma nota de entrada com direito a crédito nos últimos 12 meses: digite as compras se quiser simular o crédito.");
        return Resultado<SugestaoSimulacao>.Ok(new SugestaoSimulacao(receita, Dinheiro.Arredondar(compras), folha, avisos));
    }

    public async Task<Resultado<SimulacaoSimples>> SimularAsync(long empresa, DateOnly hoje, BaseSimulacao b, long usuario, CancellationToken ct = default)
    {
        if (await PermitirAsync(empresa, usuario, ct) is { } erro) return Resultado<SimulacaoSimples>.Falha(erro);
        if (b.Receita12 <= 0) return Resultado<SimulacaoSimples>.Falha("Informe a receita dos últimos 12 meses.");
        if (b.Compras12 < 0 || b.Folha12 < 0) return Resultado<SimulacaoSimples>.Falha("Compras e folha não podem ser negativas.");
        if (b.ReducaoAliquota is < 0 or > 100) return Resultado<SimulacaoSimples>.Falha("A redução de alíquota deve estar entre 0% e 100%.");
        var comp = new DateOnly(hoje.Year, hoje.Month, 1);
        var tabela = await simples.TabelaAsync(comp, ct);
        var mensal = CalculadoraSimples.Calcular(tabela, new EntradaSimples(comp, [new ReceitaSimples(b.Atividade, Dinheiro.Arredondar(b.Receita12 / 12m))], b.Receita12, b.Folha12));
        return Resultado<SimulacaoSimples>.Ok(CalculoSimulacaoSimples.Simular(b, mensal, await CronogramaAsync(ct)));
    }
}
