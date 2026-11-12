using Npgsql;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Comum;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Postgres;

namespace Vorticis.Fiscal.Reforma;

/// <summary>
/// Classificação tributária (cClassTrib) do IBS e da CBS: código de 6 dígitos da tabela oficial, o CST a que pertence, o percentual de redução
/// das alíquotas (ex.: 60% para os itens com alíquota reduzida a 40%) e se a aquisição gera crédito.
/// </summary>
public sealed record ClassificacaoTributaria(string Codigo, string Cst, string Descricao, decimal ReducaoPct = 0, bool GeraCredito = true, bool Ativo = true)
{
    public override string ToString() => $"{Codigo} – {Descricao}";
}

/// <summary>Cálculo do IBS, da CBS e do Imposto Seletivo de um item, pelas alíquotas do ano e pela classificação tributária.</summary>
public static class CalculadoraReforma
{
    /// <summary>
    /// Base sugerida do IBS/CBS: valor da operação (produtos − desconto + frete + seguro + outras despesas) sem o ICMS, o ISS, o PIS e a COFINS,
    /// que não integram a base durante a transição (LC 214/2025). O valor continua editável na nota.
    /// </summary>
    public static decimal Base(ItemFiscal i) =>
        Math.Max(0, Dinheiro.Arredondar(i.Quantidade * i.ValorUnitario - i.Desconto + i.Frete + i.Seguro + i.Outras - i.Icms - i.Iss - i.Pis - i.Cofins));

    /// <summary>
    /// IBS (UF e município) e CBS do item: alíquota do ano × (1 − redução da classificação); CST que não tributa (isenção, imunidade, diferimento,
    /// suspensão...) fica com alíquota zero. O Imposto Seletivo é a alíquota informada sobre a mesma base (só a partir de 2027).
    /// </summary>
    public static TributosReforma Calcular(decimal @base, AliquotasReforma? ano, string cst, string? classTrib, decimal reducaoPct, bool tributa,
                                           string? cstIs = null, string? classTribIs = null, decimal aliqIs = 0)
    {
        var fator = tributa ? 1 - Math.Clamp(reducaoPct, 0, 100) / 100m : 0m;
        decimal A(decimal? a) => Math.Round((a ?? 0) * fator, 4);
        var (uf, mun, cbs) = (A(ano?.IbsUf), A(ano?.IbsMun), A(ano?.Cbs));
        decimal V(decimal a) => Dinheiro.Arredondar(@base * a / 100m);
        var aIs = ano is { ImpostoSeletivo: true } ? aliqIs : 0;
        return new TributosReforma(cst, classTrib, @base, uf, V(uf), mun, V(mun), cbs, V(cbs), tributa ? reducaoPct : 0,
                                   aIs > 0 ? cstIs : null, aIs > 0 ? classTribIs : null, aIs, V(aIs));
    }

    /// <summary>Avisos da nota para o ano (PIS/COFINS ou IPI destacados depois da extinção, IBS/CBS sem classificação).</summary>
    public static IReadOnlyList<string> Avisos(IEnumerable<ItemFiscal> itens, AliquotasReforma? ano)
    {
        var l = itens.ToList();
        var a = new List<string>();
        if (ano is null) return a;
        if (!ano.PisCofins && l.Any(i => i.Pis != 0 || i.Cofins != 0))
            a.Add($"Em {ano.Ano} o PIS e a COFINS foram substituídos pela CBS: confira os itens com PIS/COFINS destacados.");
        if (!ano.Ipi && l.Any(i => i.Ipi != 0))
            a.Add($"Em {ano.Ano} o IPI está zerado (salvo produtos incentivados da Zona Franca de Manaus): confira os itens com IPI.");
        if (ano.Contabiliza && l.Any(i => i.Reforma is null || string.IsNullOrWhiteSpace(i.Reforma.Cst)))
            a.Add($"Há itens sem o CST do IBS/CBS: em {ano.Ano} o IBS e a CBS devem ser destacados por item (use Calcular IBS/CBS/IS).");
        return a;
    }
}

/// <summary>Cadastros › Classificação tributária (IBS/CBS): a tabela é da instalação (vale para todas as empresas). FISCAL_APURAR para alterar.</summary>
public sealed class ServicoClassificacaoTributaria(NpgsqlDataSource ds, IParametrosFiscais parametros, Autorizador autorizador)
{
    public async Task<IReadOnlyList<CstReforma>> CstsAsync(CancellationToken ct = default) =>
        ParametrosReforma.CarregarCst(await parametros.ObterJsonAsync(ServicoReformaTributaria.Chave, DateOnly.FromDateTime(DateTime.Today), ct));

    public async Task<List<ClassificacaoTributaria>> ListarAsync(bool somenteAtivas = false, CancellationToken ct = default)
    {
        var l = new List<ClassificacaoTributaria>();
        await using var cmd = ds.CreateCommand("""
            SELECT codigo, cst, descricao, reducao_pct, gera_credito, ativo FROM fiscal.classificacao_tributaria
             WHERE NOT @a OR ativo ORDER BY codigo
            """).Com("a", somenteAtivas);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) l.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetDecimal(3), r.GetBoolean(4), r.GetBoolean(5)));
        return l;
    }

    public static IReadOnlyList<string> Validar(ClassificacaoTributaria c)
    {
        var e = new List<string>();
        if (c.Codigo is not { Length: 6 } || !c.Codigo.All(char.IsAsciiDigit)) e.Add("O código da classificação tem 6 dígitos (cClassTrib da tabela oficial).");
        if (c.Cst is not { Length: 3 } || !c.Cst.All(char.IsAsciiDigit)) e.Add("O CST tem 3 dígitos.");
        if (string.IsNullOrWhiteSpace(c.Descricao)) e.Add("Informe a descrição.");
        if (c.ReducaoPct is < 0 or > 100) e.Add("A redução da alíquota vai de 0 a 100%.");
        return e;
    }

    public async Task<Resultado> GravarAsync(long empresa, ClassificacaoTributaria c, long usuario, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuario, empresa, Permissao.FiscalApurar, ct) is { } negado) return Resultado.Falha(negado);
        if (Validar(c) is { Count: > 0 } erros) return Resultado.Falha(erros);
        await using var cmd = ds.CreateCommand("""
            INSERT INTO fiscal.classificacao_tributaria (codigo, cst, descricao, reducao_pct, gera_credito, ativo) VALUES (@c, @s, @d, @r, @g, @a)
            ON CONFLICT (codigo) DO UPDATE SET cst = EXCLUDED.cst, descricao = EXCLUDED.descricao, reducao_pct = EXCLUDED.reducao_pct,
                                               gera_credito = EXCLUDED.gera_credito, ativo = EXCLUDED.ativo
            """).Com("c", c.Codigo).Com("s", c.Cst).Com("d", c.Descricao.Trim()).Com("r", c.ReducaoPct).Com("g", c.GeraCredito).Com("a", c.Ativo);
        await cmd.ExecuteNonQueryAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> ExcluirAsync(long empresa, string codigo, long usuario, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuario, empresa, Permissao.FiscalApurar, ct) is { } negado) return Resultado.Falha(negado);
        await using var cmd = ds.CreateCommand("DELETE FROM fiscal.classificacao_tributaria WHERE codigo = @c").Com("c", codigo);
        await cmd.ExecuteNonQueryAsync(ct);
        return Resultado.Ok();
    }
}
