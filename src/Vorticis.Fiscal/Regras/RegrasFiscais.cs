using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Documentos;

namespace Vorticis.Fiscal.Regras;

/// <summary>
/// Regra fiscal. ENTRADA: CFOP do emitente (<see cref="CfopOrigem"/>, 5/6/7) vira o CFOP de entrada da empresa (<see cref="CfopDestino"/>),
/// com condições opcionais (participante, NCM ou prefixo, produto, CST/CSOSN de origem) e campos de saída opcionais (CST, créditos, contas, centro de custo).
/// SAÍDA: sugere CFOP e/ou CST por produto, NCM, participante e destino (<see cref="Destino"/>: "I" = dentro do estado, "X" = outro estado).
/// </summary>
public sealed record RegraFiscal(
    long Id, TipoOperacao Operacao, int Ordem, bool Ativo, string? Descricao, string? CfopOrigem, long? ParticipanteId, string? Ncm, long? ProdutoId,
    string? CstOrigem, string? Destino, string? CfopDestino, string? CstDestino, bool CreditaIcms = true, bool CreditaIpi = true, bool CreditaPis = true,
    bool CreditaCofins = true, string? ContaDebito = null, string? ContaCredito = null, long? CentroCustoId = null,
    string? ParticipanteNome = null, string? ProdutoDescricao = null);

/// <summary>Dados do item usados para escolher a regra.</summary>
public sealed record ItemParaRegra(string Cfop, long? ParticipanteId, string? Ncm, long? ProdutoId, string? CstIcms, string? Destino = null);

/// <summary>Escrituração decidida para um item de entrada. <see cref="SemRegra"/>: nenhuma regra serviu e valeu a conversão padrão (5/6/7 para 1/2/3).</summary>
public sealed record DecisaoFiscal(
    string Cfop, string? CstIcms, bool CreditaIcms, bool CreditaIpi, bool CreditaPis, bool CreditaCofins, string? ContaDebito, string? ContaCredito,
    long? CentroCustoId, long? RegraId, bool SemRegra);

public static class MotorRegrasFiscais
{
    /// <summary>Conversão padrão do CFOP do emitente para o de entrada: 5 vira 1, 6 vira 2, 7 vira 3, mantendo os demais dígitos.</summary>
    public static string CfopPadraoEntrada(string cfop) => cfop.Length == 4 && cfop[0] is '5' or '6' or '7' && cfop.All(char.IsAsciiDigit)
        ? ((char)(cfop[0] - 4)) + cfop[1..] : cfop;

    /// <summary>Quanto mais específica, maior: produto vence NCM, que vence participante, que vence só CFOP (o CST de origem e o destino desempatam por último).</summary>
    public static int Especificidade(RegraFiscal r) =>
        (r.ProdutoId is not null ? 64 : 0) + (r.Ncm is not null ? 16 : 0) + (r.ParticipanteId is not null ? 4 : 0) + (r.CstOrigem is not null ? 2 : 0) + (r.Destino is not null ? 1 : 0);

    public static bool Casa(RegraFiscal r, TipoOperacao operacao, ItemParaRegra i) =>
        r.Ativo && r.Operacao == operacao
        && (r.CfopOrigem is null || r.CfopOrigem == i.Cfop)
        && (r.ParticipanteId is null || r.ParticipanteId == i.ParticipanteId)
        && (r.ProdutoId is null || r.ProdutoId == i.ProdutoId)
        && (r.Ncm is null || (i.Ncm is not null && i.Ncm.StartsWith(r.Ncm, StringComparison.Ordinal)))
        && (r.CstOrigem is null || r.CstOrigem == i.CstIcms)
        && (r.Destino is null || r.Destino == i.Destino);

    /// <summary>A regra mais específica; empate: NCM mais longo, depois a menor ordem, depois o cadastro mais antigo.</summary>
    public static RegraFiscal? Localizar(IEnumerable<RegraFiscal> regras, TipoOperacao operacao, ItemParaRegra item) =>
        regras.Where(r => Casa(r, operacao, item))
              .OrderByDescending(Especificidade).ThenByDescending(r => r.Ncm?.Length ?? 0).ThenBy(r => r.Ordem).ThenBy(r => r.Id).FirstOrDefault();

    /// <summary>Escrituração do item de entrada: pela regra encontrada ou, sem regra, pela conversão padrão (marcada como "sem regra").</summary>
    public static DecisaoFiscal DecidirEntrada(IEnumerable<RegraFiscal> regras, ItemParaRegra item)
    {
        if (item.Cfop == Cfop.Servico || Cfop.Operacao(item.Cfop) is not TipoOperacao.Saida)
            return new DecisaoFiscal(item.Cfop, null, true, true, true, true, null, null, null, null, false);   // NFS-e ou CFOP que já é de entrada: nada a converter
        return Localizar(regras, TipoOperacao.Entrada, item) is { } r
            ? new DecisaoFiscal(r.CfopDestino ?? CfopPadraoEntrada(item.Cfop), r.CstDestino, r.CreditaIcms, r.CreditaIpi, r.CreditaPis, r.CreditaCofins,
                                r.ContaDebito, r.ContaCredito, r.CentroCustoId, r.Id, false)
            : new DecisaoFiscal(CfopPadraoEntrada(item.Cfop), null, true, true, true, true, null, null, null, null, true);
    }

    /// <summary>Sugestão (CFOP e/ou CST) para o item de uma nota de saída digitada, ou null se nenhuma regra serve.</summary>
    public static RegraFiscal? SugerirSaida(IEnumerable<RegraFiscal> regras, ItemParaRegra item) => Localizar(regras, TipoOperacao.Saida, item);

    /// <summary>
    /// De-para padrão (CFOP do emitente, CFOP de entrada). Só entram correspondências seguras da tabela oficial de CFOP; operações sem
    /// correspondência clara (energia, combustíveis, exportação, devoluções específicas etc.) ficam sem regra e caem na conversão padrão marcada para revisão.
    /// </summary>
    public static IReadOnlyList<(string Origem, string Destino)> RegrasPadrao => _padrao.Value;
    private static readonly Lazy<IReadOnlyList<(string, string)>> _padrao = new(Montar);

    private static readonly string[] SufixosMesmoFinal =
    [
        "101", "102",                                   // venda de produção / de mercadoria adquirida  -> compra para industrialização / comercialização
        "151", "152",                                   // transferências
        "201", "202",                                   // devolução de compra -> devolução de venda
        "351", "352", "353", "354", "355", "356",       // transporte -> aquisição de serviço de transporte
        "401",                                          // ST: venda de produção -> compra para industrialização
        "410", "411",                                   // devolução ST
        "551", "552", "553",                            // bens do ativo imobilizado
        "901", "902", "903", "905", "906", "907", "908", "909", "910", "911", "912", "913", "915", "916", "920", "921", "924", "925", "932", "933", "949",
    ];

    private static IReadOnlyList<(string, string)> Montar()
    {
        var lista = new List<(string, string)>();
        foreach (var (de, para) in new[] { ('5', '1'), ('6', '2') })
        {
            foreach (var s in SufixosMesmoFinal) lista.Add(($"{de}{s}", $"{para}{s}"));
            lista.Add(($"{de}403", $"{para}403"));   // venda como substituto tributário -> compra com ST
            lista.Add(($"{de}405", $"{para}403"));   // venda como substituído -> compra com ST
        }
        return lista;
    }

    /// <summary>Valida e normaliza a regra (dígitos, tamanhos e campos obrigatórios por operação).</summary>
    public static Resultado<RegraFiscal> Validar(RegraFiscal r)
    {
        var erros = new List<string>();
        static string? Dig(string? t) => string.IsNullOrWhiteSpace(t) ? null : new string(t.Where(char.IsAsciiDigit).ToArray()) is { Length: > 0 } d ? d : null;
        static string? Txt(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();
        var origem = Dig(r.CfopOrigem);
        var destino = Dig(r.CfopDestino);
        var ncm = Dig(r.Ncm);
        var cstOrigem = Dig(r.CstOrigem);
        var cstDestino = Dig(r.CstDestino);
        var descricao = Txt(r.Descricao);
        if (r.Operacao == TipoOperacao.Entrada)
        {
            if (Cfop.Operacao(origem) != TipoOperacao.Saida) erros.Add("CFOP do emitente: informe 4 dígitos de uma operação de saída (começa por 5, 6 ou 7).");
            if (Cfop.Operacao(destino) != TipoOperacao.Entrada) erros.Add("CFOP de entrada: informe 4 dígitos começando por 1, 2 ou 3.");
        }
        else
        {
            if (origem is not null && Cfop.Operacao(origem) != TipoOperacao.Saida) erros.Add("CFOP atual: informe 4 dígitos começando por 5, 6 ou 7.");
            if (destino is not null && Cfop.Operacao(destino) != TipoOperacao.Saida) erros.Add("CFOP sugerido: informe 4 dígitos começando por 5, 6 ou 7.");
            if (destino is null && cstDestino is null) erros.Add("Informe o CFOP ou o CST a sugerir.");
        }
        if (ncm is { Length: > 8 }) erros.Add("NCM: no máximo 8 dígitos (pode ser só o início, para valer para um grupo).");
        if (cstOrigem is { Length: > 4 }) erros.Add("CST/CSOSN de origem: no máximo 4 dígitos.");
        if (cstDestino is { Length: > 4 }) erros.Add("CST/CSOSN: no máximo 4 dígitos.");
        if (r.Destino is not (null or "I" or "X")) erros.Add("Destino inválido.");
        if (r.Operacao == TipoOperacao.Entrada && r.Destino is not null) erros.Add("O destino (dentro ou fora do estado) só vale para regras de saída.");
        if (descricao is { Length: > 120 }) erros.Add("A descrição pode ter até 120 caracteres.");
        if (r.Ordem is < 0 or > 99999) erros.Add("Ordem: informe um número de 0 a 99999.");
        return erros.Count > 0
            ? Resultado<RegraFiscal>.Falha(erros)
            : Resultado<RegraFiscal>.Ok(r with
            {
                CfopOrigem = origem, CfopDestino = destino, Ncm = ncm, CstOrigem = cstOrigem, CstDestino = cstDestino, Descricao = descricao,
                ContaDebito = Txt(r.ContaDebito), ContaCredito = Txt(r.ContaCredito),
            });
    }
}

public interface IRepositorioRegrasFiscais
{
    Task<IReadOnlyList<RegraFiscal>> ListarAsync(long empresaId, TipoOperacao? operacao, CancellationToken ct);

    /// <summary>Inclui (Id = 0) ou altera a regra. Conta informada que não exista nas analíticas ativas é recusada.</summary>
    Task<long> GravarAsync(long empresaId, RegraFiscal regra, long usuarioId, CancellationToken ct);
    Task<bool> ExcluirAsync(long empresaId, long regraId, long usuarioId, CancellationToken ct);

    /// <summary>Cria as regras padrão que ainda não existem (mesmo CFOP do emitente sem condições); devolve quantas foram criadas.</summary>
    Task<int> GerarPadraoAsync(long empresaId, IReadOnlyList<(string Origem, string Destino)> pares, long usuarioId, CancellationToken ct);

    /// <summary>UF da empresa (para saber se o destino da nota de saída é dentro ou fora do estado).</summary>
    Task<string?> UfEmpresaAsync(long empresaId, CancellationToken ct);
    Task<bool> ContaAnaliticaExisteAsync(long empresaId, string codigo, CancellationToken ct);

    /// <summary>Reaplica as regras de entrada nas notas XML de entrada não contabilizadas do período; devolve (notas, itens com regra, itens sem regra, notas puladas).</summary>
    Task<ResultadoReaplicacao> ReaplicarAsync(long empresaId, DateOnly inicio, DateOnly fim, long usuarioId, CancellationToken ct);
}

public sealed record ResultadoReaplicacao(int Notas, int ItensComRegra, int ItensSemRegra, int NotasPuladas);

/// <summary>Cadastro de regras fiscais (Cadastros › Regras fiscais), regras padrão, sugestão para notas de saída e "Reaplicar regras".</summary>
public sealed class ServicoRegrasFiscais(IRepositorioRegrasFiscais repositorio, Autorizador autorizador)
{
    public async Task<Resultado<IReadOnlyList<RegraFiscal>>> ListarAsync(long empresaId, TipoOperacao? operacao, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<IReadOnlyList<RegraFiscal>>(usuarioId, empresaId, Permissao.FiscalDocumentosConsultar, ct) is { } negado) return negado;
        return Resultado<IReadOnlyList<RegraFiscal>>.Ok(await repositorio.ListarAsync(empresaId, operacao, ct));
    }

    public async Task<Resultado<long>> GravarAsync(long empresaId, RegraFiscal regra, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<long>(usuarioId, empresaId, Permissao.FiscalDocumentosImportar, ct) is { } negado) return negado;
        var v = MotorRegrasFiscais.Validar(regra);
        if (!v.Sucesso) return Resultado<long>.Falha(v.Erros);
        var r = v.Valor;
        var erros = new List<string>();
        foreach (var (codigo, rotulo) in new[] { (r.ContaDebito, "Conta de débito"), (r.ContaCredito, "Conta de crédito") })
            if (codigo is not null && !await repositorio.ContaAnaliticaExisteAsync(empresaId, codigo, ct))
                erros.Add($"{rotulo}: conta \"{codigo}\" não encontrada entre as analíticas ativas.");
        if (erros.Count > 0) return Resultado<long>.Falha(erros);
        try { return Resultado<long>.Ok(await repositorio.GravarAsync(empresaId, r, usuarioId, ct)); }
        catch (ViolacaoRegraException ex) { return Resultado<long>.Falha(ex.Message); }
    }

    public async Task<Resultado> ExcluirAsync(long empresaId, long regraId, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.FiscalDocumentosImportar, ct) is { } negado) return Resultado.Falha(negado);
        return await repositorio.ExcluirAsync(empresaId, regraId, usuarioId, ct) ? Resultado.Ok() : Resultado.Falha("Regra não encontrada.");
    }

    /// <summary>"Gerar regras padrão": cria o de-para usual (5 para 1, 6 para 2) que ainda não existir; as regras já cadastradas não são alteradas.</summary>
    public async Task<Resultado<int>> GerarPadraoAsync(long empresaId, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<int>(usuarioId, empresaId, Permissao.FiscalDocumentosImportar, ct) is { } negado) return negado;
        return Resultado<int>.Ok(await repositorio.GerarPadraoAsync(empresaId, MotorRegrasFiscais.RegrasPadrao, usuarioId, ct));
    }

    /// <summary>Sugestão de CFOP/CST para o item de uma nota de saída digitada: <paramref name="ufDestino"/> é a UF do cliente (vazia = não sabe).</summary>
    public async Task<Resultado<RegraFiscal?>> SugerirSaidaAsync(long empresaId, long? produtoId, string? ncm, long? participanteId, string? ufDestino, string? cfopAtual,
                                                                 long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<RegraFiscal?>(usuarioId, empresaId, Permissao.FiscalDocumentosConsultar, ct) is { } negado) return negado;
        var regras = await repositorio.ListarAsync(empresaId, TipoOperacao.Saida, ct);
        if (regras.Count == 0) return Resultado<RegraFiscal?>.Ok(null);
        string? destino = null;
        if (!string.IsNullOrWhiteSpace(ufDestino) && await repositorio.UfEmpresaAsync(empresaId, ct) is { } uf)
            destino = string.Equals(uf, ufDestino.Trim(), StringComparison.OrdinalIgnoreCase) ? "I" : "X";
        var digitos = string.IsNullOrWhiteSpace(ncm) ? null : new string(ncm.Where(char.IsAsciiDigit).ToArray());
        return Resultado<RegraFiscal?>.Ok(MotorRegrasFiscais.SugerirSaida(regras, new ItemParaRegra(cfopAtual?.Trim() ?? "", participanteId, digitos, produtoId, null, destino)));
    }

    /// <summary>"Reaplicar regras": refaz o CFOP, o CST e os créditos das notas de entrada importadas e ainda não contabilizadas do período.</summary>
    public async Task<Resultado<ResultadoReaplicacao>> ReaplicarAsync(long empresaId, DateOnly inicio, DateOnly fim, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<ResultadoReaplicacao>(usuarioId, empresaId, Permissao.FiscalDocumentosImportar, ct) is { } negado) return negado;
        if (fim < inicio) return Resultado<ResultadoReaplicacao>.Falha("Data final anterior à inicial.");
        return Resultado<ResultadoReaplicacao>.Ok(await repositorio.ReaplicarAsync(empresaId, inicio, fim, usuarioId, ct));
    }
}
