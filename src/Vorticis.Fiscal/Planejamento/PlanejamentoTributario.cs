using System.Text.Json;
using Vorticis.Domain.Comum;
using Vorticis.Fiscal.Lucro;
using Vorticis.Fiscal.Simples;

namespace Vorticis.Fiscal.Planejamento;

/// <summary>Tipo de receita do planejamento: define o anexo do Simples, a presunção do Lucro Presumido e se paga ICMS ou ISS.</summary>
public enum TipoReceitaPlanejamento { Comercio, Industria, ServicosAnexoIII, ServicosFatorR, ServicosAnexoIV, ServicosAnexoV, TransporteCargas }

public sealed record TipoReceitaInfo(TipoReceitaPlanejamento Tipo, string Descricao, AtividadeSimples Simples, string Presuncao, bool Servico);

public sealed record ReceitaPlanejamento(TipoReceitaPlanejamento Tipo, decimal Valor);

/// <summary>
/// Dados anuais da simulação. Folha: salários e pró-labore do ano. Encargos: % patronal sobre a folha (INSS, RAT e terceiros) fora do
/// Simples. Custos e despesas: tudo o que reduz o lucro, sem a folha e sem os tributos. Compras com crédito: base dos créditos de
/// PIS/COFINS no Lucro Real. ICMS e ISS: % efetivo sobre as vendas e sobre os serviços, usados fora do DAS.
/// </summary>
public sealed record EntradaPlanejamento(
    int Ano, IReadOnlyList<ReceitaPlanejamento> Receitas, decimal Folha, decimal EncargosPercentual, decimal CustosDespesas,
    decimal ComprasComCredito, decimal AliquotaIcms, decimal AliquotaIss)
{
    public decimal ReceitaTotal => Receitas.Sum(r => r.Valor);
}

/// <summary>Limite do Lucro Presumido e alíquota da contribuição patronal (core.parametro_legal, chave PLANEJAMENTO_TRIBUTARIO).</summary>
public sealed record ParametrosPlanejamento(decimal LimitePresumido, decimal CppAliquota)
{
    public static ParametrosPlanejamento Carregar(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        return new ParametrosPlanejamento(r.GetProperty("limite_presumido_anual").GetDecimal(), r.GetProperty("cpp_aliquota").GetDecimal());
    }
}

public enum RegimePlanejamento { SimplesNacional, LucroPresumido, LucroReal }

/// <summary>Tributos do ano num regime (no Simples, o DAS repartido por tributo, mais o que fica fora dele).</summary>
public sealed record TributosRegime(
    RegimePlanejamento Regime, bool Permitido, decimal Irpj, decimal Csll, decimal Pis, decimal Cofins, decimal Cpp, decimal Ipi, decimal Icms, decimal Iss,
    decimal Das, IReadOnlyList<string> Avisos, IReadOnlyList<string> Memoria)
{
    public decimal Total => Irpj + Csll + Pis + Cofins + Cpp + Ipi + Icms + Iss;

    public string Nome => PlanejamentoTributario.NomeRegime(Regime);
}

public sealed record ResultadoPlanejamento(EntradaPlanejamento Entrada, IReadOnlyList<TributosRegime> Regimes)
{
    /// <summary>Regime permitido de menor carga (null se nenhum é permitido).</summary>
    public TributosRegime? Melhor => Regimes.Where(r => r.Permitido).OrderBy(r => r.Total).FirstOrDefault();

    /// <summary>Quanto o melhor regime economiza em relação ao segundo melhor permitido.</summary>
    public decimal Economia => Regimes.Where(r => r.Permitido).OrderBy(r => r.Total).Skip(1).FirstOrDefault() is { } segundo && Melhor is { } m ? segundo.Total - m.Total : 0m;

    public decimal Percentual(TributosRegime r) => Entrada.ReceitaTotal > 0 ? Math.Round(r.Total * 100m / Entrada.ReceitaTotal, 2, MidpointRounding.AwayFromZero) : 0m;
}

/// <summary>
/// Planejamento tributário: compara a carga anual do Simples Nacional, do Lucro Presumido e do Lucro Real com os mesmos dados.
/// Simples: DAS de um mês médio (receita ÷ 12, RBT12 = receita do ano) × 12, com a CPP fora do DAS no Anexo IV e o ICMS/ISS fora do
/// DAS acima do sublimite. Presumido: IRPJ/CSLL trimestrais pela presunção da atividade e PIS/COFINS cumulativos. Real: lucro =
/// receita − custos e despesas − folha − encargos − PIS/COFINS − ICMS − ISS, IRPJ/CSLL trimestrais e PIS/COFINS não cumulativos com
/// crédito sobre as compras. É uma estimativa: não considera retenções, incentivos, ST, monofásicos nem adições e exclusões do LALUR.
/// </summary>
public static class PlanejamentoTributario
{
    public static IReadOnlyList<TipoReceitaInfo> Tipos { get; } =
    [
        new(TipoReceitaPlanejamento.Comercio, "Comércio (revenda de mercadorias)", AtividadeSimples.ComercioRevenda, "COMERCIO_INDUSTRIA", false),
        new(TipoReceitaPlanejamento.Industria, "Indústria (venda de produtos fabricados)", AtividadeSimples.IndustriaVenda, "COMERCIO_INDUSTRIA", false),
        new(TipoReceitaPlanejamento.ServicosAnexoIII, "Serviços do Anexo III (manutenção, locação de bens móveis...)", AtividadeSimples.ServicosAnexoIII, "SERVICOS_GERAIS", true),
        new(TipoReceitaPlanejamento.ServicosFatorR, "Serviços intelectuais com Fator R (consultoria, engenharia...)", AtividadeSimples.ServicosFatorR, "SERVICOS_GERAIS", true),
        new(TipoReceitaPlanejamento.ServicosAnexoIV, "Serviços do Anexo IV (construção, limpeza, vigilância, advocacia)", AtividadeSimples.ServicosAnexoIV, "SERVICOS_GERAIS", true),
        new(TipoReceitaPlanejamento.ServicosAnexoV, "Serviços do Anexo V sem Fator R", AtividadeSimples.ServicosAnexoV, "SERVICOS_GERAIS", true),
        new(TipoReceitaPlanejamento.TransporteCargas, "Transporte de cargas", AtividadeSimples.ServicosAnexoIII, "TRANSPORTE_CARGAS", false),
    ];

    public static TipoReceitaInfo Info(TipoReceitaPlanejamento t) => Tipos.First(x => x.Tipo == t);

    public static string NomeRegime(RegimePlanejamento r) => r switch
    {
        RegimePlanejamento.SimplesNacional => "Simples Nacional",
        RegimePlanejamento.LucroPresumido => "Lucro Presumido",
        _ => "Lucro Real",
    };

    private static decimal Pct(decimal valor, decimal aliquota) => Dinheiro.Arredondar(valor * aliquota / 100m);

    /// <summary>Confere os dados; vazio quando estão completos.</summary>
    public static IReadOnlyList<string> Validar(EntradaPlanejamento e)
    {
        var erros = new List<string>();
        if (e.Ano < 2000 || e.Ano > 2100) erros.Add("Informe o ano do planejamento.");
        if (e.ReceitaTotal <= 0) erros.Add("Informe ao menos uma receita do ano.");
        if (e.Receitas.Any(r => r.Valor < 0)) erros.Add("As receitas não podem ser negativas.");
        if (e.Folha < 0 || e.CustosDespesas < 0 || e.ComprasComCredito < 0) erros.Add("Folha, custos e compras não podem ser negativos.");
        if (e.EncargosPercentual is < 0 or > 100 || e.AliquotaIcms is < 0 or > 100 || e.AliquotaIss is < 0 or > 100) erros.Add("Os percentuais devem estar entre 0 e 100.");
        return erros;
    }

    public static ResultadoPlanejamento Comparar(EntradaPlanejamento e, TabelaSimples simples, ParametrosLucro lucro, ParametrosPlanejamento p) =>
        new(e, [Simples(e, simples), Presumido(e, lucro, p), Real(e, lucro)]);

    private static (decimal Icms, decimal Iss) IcmsIss(EntradaPlanejamento e) =>
        (Pct(e.Receitas.Where(r => !Info(r.Tipo).Servico).Sum(r => r.Valor), e.AliquotaIcms),
         Pct(e.Receitas.Where(r => Info(r.Tipo).Servico).Sum(r => r.Valor), e.AliquotaIss));

    public static TributosRegime Simples(EntradaPlanejamento e, TabelaSimples tabela)
    {
        var receita = e.ReceitaTotal;
        var avisos = new List<string>();
        var memoria = new List<string>();
        if (receita > tabela.LimiteRbt12)
        {
            avisos.Add($"Receita de {Formato.Moeda(receita)} acima do limite do Simples Nacional ({Formato.Moeda(tabela.LimiteRbt12)}): regime não permitido.");
            return new TributosRegime(RegimePlanejamento.SimplesNacional, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, avisos, memoria);
        }
        var mes = new EntradaSimples(new DateOnly(e.Ano, 1, 1),
            [.. e.Receitas.Where(r => r.Valor > 0).Select(r => new ReceitaSimples(Info(r.Tipo).Simples, Dinheiro.Arredondar(r.Valor / 12m)))], receita, e.Folha);
        var r = CalculadoraSimples.Calcular(tabela, mes);
        avisos.AddRange(r.Avisos.Where(a => !a.Contains("mínimo", StringComparison.Ordinal)));
        memoria.Add($"Mês médio: receita {Formato.Moeda(r.ReceitaMes)}, RBT12 {Formato.Moeda(receita)}, Fator R {r.FatorR * 100:0.00}%; DAS do mês {Formato.Moeda(r.ValorDas)} × 12.");
        memoria.AddRange(r.Memoria.Where(m => m.StartsWith("Anexo", StringComparison.Ordinal) || m.StartsWith("  Fator", StringComparison.Ordinal)));
        decimal T(string tributo) => r.TotalPorTributo.TryGetValue(tributo, out var v) ? v * 12 : 0m;
        var cpp = T("CPP");
        var receitaIv = e.Receitas.Where(x => x.Tipo == TipoReceitaPlanejamento.ServicosAnexoIV).Sum(x => x.Valor);
        if (receitaIv > 0)
        {
            var fora = Pct(e.Folha * receitaIv / receita, e.EncargosPercentual);
            cpp += fora;
            memoria.Add($"Anexo IV: a contribuição patronal fica fora do DAS — folha proporcional × {e.EncargosPercentual:0.##}% = {Formato.Moeda(fora)}.");
        }
        decimal icms = T("ICMS"), iss = T("ISS");
        if (receita > tabela.SublimiteIcmsIss)
        {
            var (fi, fs) = IcmsIss(e);
            (icms, iss) = (icms + fi, iss + fs);
            memoria.Add($"Acima do sublimite: ICMS {Formato.Moeda(fi)} e ISS {Formato.Moeda(fs)} recolhidos fora do DAS.");
        }
        return new TributosRegime(RegimePlanejamento.SimplesNacional, true, T("IRPJ"), T("CSLL"), T("PIS"), T("COFINS"), cpp, T("IPI"), icms, iss, r.ValorDas * 12,
            avisos, memoria);
    }

    public static TributosRegime Presumido(EntradaPlanejamento e, ParametrosLucro lucro, ParametrosPlanejamento p)
    {
        var receita = e.ReceitaTotal;
        var avisos = new List<string>();
        var memoria = new List<string>();
        var permitido = receita <= p.LimitePresumido;
        if (!permitido) avisos.Add($"Receita acima do limite do Lucro Presumido ({Formato.Moeda(p.LimitePresumido)}): a empresa é obrigada ao Lucro Real.");
        decimal irpj = 0, csll = 0;
        var receitas = e.Receitas.Where(r => r.Valor > 0).Select(r => new ReceitaPresumida(Info(r.Tipo).Presuncao, Dinheiro.Arredondar(r.Valor / 4m))).ToList();
        for (var t = 1; t <= 4; t++)
        {
            var r = CalculadoraLucro.CalcularPresumido(lucro, new EntradaPresumido(e.Ano, t, receitas, 0, 0, 0));
            (irpj, csll) = (irpj + r.IrpjAPagar, csll + r.CsllAPagar);
            if (t == 1) memoria.AddRange(r.Memoria.Skip(1).Select(m => "Por trimestre: " + m));
        }
        var pis = Pct(receita, lucro.PisCumulativo);
        var cofins = Pct(receita, lucro.CofinsCumulativo);
        var cpp = Pct(e.Folha, e.EncargosPercentual);
        var (icms, iss) = IcmsIss(e);
        memoria.Add($"PIS {lucro.PisCumulativo:0.##}% e COFINS {lucro.CofinsCumulativo:0.##}% cumulativos sobre a receita; encargos {e.EncargosPercentual:0.##}% sobre a folha.");
        return new TributosRegime(RegimePlanejamento.LucroPresumido, permitido, irpj, csll, pis, cofins, cpp, 0, icms, iss, 0, avisos, memoria);
    }

    public static TributosRegime Real(EntradaPlanejamento e, ParametrosLucro lucro)
    {
        var receita = e.ReceitaTotal;
        var avisos = new List<string>();
        var memoria = new List<string>();
        var pis = Math.Max(0, Pct(receita, lucro.PisNaoCumulativo) - Pct(e.ComprasComCredito, lucro.PisNaoCumulativo));
        var cofins = Math.Max(0, Pct(receita, lucro.CofinsNaoCumulativo) - Pct(e.ComprasComCredito, lucro.CofinsNaoCumulativo));
        var cpp = Pct(e.Folha, e.EncargosPercentual);
        var (icms, iss) = IcmsIss(e);
        var lucroAno = receita - e.CustosDespesas - e.Folha - cpp - pis - cofins - icms - iss;
        memoria.Add($"Lucro antes do IRPJ/CSLL: receita {Formato.Moeda(receita)} − custos e despesas {Formato.Moeda(e.CustosDespesas)} − folha {Formato.Moeda(e.Folha)} − encargos {Formato.Moeda(cpp)} − PIS/COFINS {Formato.Moeda(pis + cofins)} − ICMS/ISS {Formato.Moeda(icms + iss)} = {Formato.Moeda(lucroAno)}");
        memoria.Add($"PIS {lucro.PisNaoCumulativo:0.##}% e COFINS {lucro.CofinsNaoCumulativo:0.##}% não cumulativos, com crédito sobre {Formato.Moeda(e.ComprasComCredito)} de compras.");
        if (lucroAno <= 0) avisos.Add("Sem lucro no ano: no Lucro Real não há IRPJ nem CSLL (o prejuízo pode ser compensado nos anos seguintes).");
        decimal irpj = 0, csll = 0, saldoIrpj = 0, saldoCsll = 0;
        var trimestre = Dinheiro.Arredondar(lucroAno / 4m);
        for (var t = 1; t <= 4; t++)
        {
            var r = CalculadoraLucro.CalcularReal(lucro, new EntradaReal(e.Ano, t, trimestre, 0, 0, 0, 0, saldoIrpj, saldoCsll, 0, 0, 0, Dinheiro.Arredondar(receita / 4m)));
            (irpj, csll) = (irpj + r.IrpjAPagar, csll + r.CsllAPagar);
            saldoIrpj += r.PrejuizoIrpj - r.CompensacaoIrpj;
            saldoCsll += r.PrejuizoCsll - r.CompensacaoCsll;
        }
        return new TributosRegime(RegimePlanejamento.LucroReal, true, irpj, csll, pis, cofins, cpp, 0, icms, iss, 0, avisos, memoria);
    }
}

/// <summary>Tabelas do planejamento (tela, relatório e exportação): tributos por regime e dados usados.</summary>
public static class TabelasPlanejamento
{
    public const string Titulo = "Planejamento tributário";

    public static IReadOnlyList<Vorticis.Application.Relatorios.TabelaRelatorio> Tabelas(ResultadoPlanejamento r, string? descricao = null)
    {
        static object? V(TributosRegime x, decimal v) => x.Permitido ? v : null;
        var cols = new List<Vorticis.Application.Relatorios.ColunaRelatorio> { new("Tributo", Vorticis.Application.Relatorios.TipoColuna.Texto, 6) };
        cols.AddRange(r.Regimes.Select(x => new Vorticis.Application.Relatorios.ColunaRelatorio(x.Permitido ? x.Nome : $"{x.Nome} (não permitido)", Vorticis.Application.Relatorios.TipoColuna.Valor, 4.5)));
        Vorticis.Application.Relatorios.LinhaRelatorio L(string nome, Func<TributosRegime, decimal> f, bool destaque = false) =>
            new([nome, .. r.Regimes.Select(x => V(x, f(x)))], destaque);
        var linhas = new List<Vorticis.Application.Relatorios.LinhaRelatorio>
        {
            L("IRPJ", x => x.Irpj), L("CSLL", x => x.Csll), L("PIS", x => x.Pis), L("COFINS", x => x.Cofins),
            L("INSS patronal (CPP)", x => x.Cpp), L("IPI", x => x.Ipi), L("ICMS", x => x.Icms), L("ISS", x => x.Iss),
            L("Total de tributos no ano", x => x.Total, true),
            new(["% da receita bruta", .. r.Regimes.Select(x => x.Permitido ? (object)r.Percentual(x) : null)]),
        };
        if (r.Regimes.Any(x => x.Das > 0)) linhas.Insert(8, L("(DAS do Simples, já repartido acima)", x => x.Das));
        var rodape = r.Melhor is { } m
            ? $"Menor carga: {m.Nome}, {Formato.Moeda(m.Total)} ({r.Percentual(m):0.00}% da receita)" + (r.Economia > 0 ? $", economia de {Formato.Moeda(r.Economia)} no ano em relação ao segundo melhor regime." : ".")
            : "Nenhum regime permitido com estes dados.";
        var avisos = r.Regimes.SelectMany(x => x.Avisos.Select(a => $"{x.Nome}: {a}")).ToList();
        if (avisos.Count > 0) rodape += " " + string.Join(" ", avisos);
        var e = r.Entrada;
        var dados = new List<Vorticis.Application.Relatorios.LinhaRelatorio>();
        foreach (var rec in e.Receitas.Where(x => x.Valor > 0)) dados.Add(new([PlanejamentoTributario.Info(rec.Tipo).Descricao, rec.Valor]));
        dados.Add(new(["Receita bruta do ano", e.ReceitaTotal], true));
        dados.Add(new(["Folha do ano (salários e pró-labore)", e.Folha]));
        dados.Add(new(["Custos e despesas (sem folha e sem tributos)", e.CustosDespesas]));
        dados.Add(new(["Compras com crédito de PIS/COFINS", e.ComprasComCredito]));
        dados.Add(new([$"Percentuais: encargos {e.EncargosPercentual:0.##}% · ICMS {e.AliquotaIcms:0.##}% · ISS {e.AliquotaIss:0.##}%", null]));
        var titulo = descricao is null ? $"{Titulo} {e.Ano}" : $"{Titulo} {e.Ano} · {descricao}";
        return
        [
            new(titulo, cols, linhas, rodape),
            new("Dados da simulação", [new("Item", Vorticis.Application.Relatorios.TipoColuna.Texto, 11), new("Valor", Vorticis.Application.Relatorios.TipoColuna.Valor, 4)], dados,
                "Estimativa anual: não considera retenções, incentivos, substituição tributária, produtos monofásicos nem adições e exclusões do LALUR."),
        ];
    }
}
