using Vorticis.Application.Cadastros;
using Vorticis.Application.Contabil;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Contabil;
using Vorticis.Domain.Imobilizado;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Application.Imobilizado;

/// <summary>Grupo de bens: regra fiscal de depreciação (taxa anual da RFB) e contas contábeis sugeridas.</summary>
public sealed record GrupoBem(long Id, string Codigo, string Descricao, decimal TaxaAnualFiscal, bool Deprecia, bool Intangivel,
                              string? ContaBem, string? ContaAcumulada, string? ContaDespesa, string? BaseLegal, bool EmUso = false)
{
    public int VidaUtilFiscalMeses => RegrasDepreciacao.VidaUtilFiscalMeses(TaxaAnualFiscal);
}

public sealed record BemImobilizado(
    long Id, int Codigo, string Descricao, long GrupoId, string? Plaqueta, string? Localizacao, string? NotaFiscal, string? CodigoFornecedor,
    DateOnly DataAquisicao, DateOnly InicioUso, decimal Valor, decimal Residual, int VidaUtilMeses, MetodoDepreciacao Metodo, int Turnos,
    decimal DepreciacaoAnterior, string ContaBem, string? ContaAcumulada, string? ContaDespesa, DateOnly? DataBaixa, string? MotivoBaixa,
    decimal DepreciacaoLancada = 0, decimal DepreciacaoFiscalLancada = 0, DateOnly? UltimaCompetencia = null, bool ProRataDia = false)
{
    public decimal Acumulada => DepreciacaoAnterior + DepreciacaoLancada;
    public decimal ValorContabil => Valor - Acumulada;
}

/// <summary>Linha do cálculo de uma competência: a quota (se houver) ou o motivo de não depreciar.</summary>
public sealed record ItemDepreciacao(BemImobilizado Bem, GrupoBem Grupo, QuotaDepreciacao? Quota, string Situacao);

/// <summary>
/// Item de nota fiscal de ENTRADA que é bem do ativo imobilizado e ainda precisa virar bem no cadastro (Imobilizado › Bens a cadastrar das notas).
/// <see cref="Motivo"/>: CFOP, TIPO_ITEM (produto tipo 08), REGRA_FISCAL ou CONTA (regra que debita conta de bem).
/// </summary>
public sealed record BemACadastrar(
    long Id, long DocumentoId, int NItem, string Descricao, decimal Valor, DateOnly DataAquisicao, DateOnly InicioUso, string? CodigoFornecedor,
    string? Fornecedor, string Nota, string Serie, string? Chave, string? Cfop, string? Ncm, string? ContaSugerida, string Motivo, string Situacao,
    long? BemId, long? GrupoSugeridoId = null)
{
    public const string Pendente = "PENDENTE", Cadastrado = "CADASTRADO", Descartado = "DESCARTADO";

    public string MotivoTexto => Motivo switch
    {
        "CFOP" => $"CFOP {Cfop} (compra para o ativo)", "TIPO_ITEM" => "produto do tipo ativo imobilizado", "REGRA_FISCAL" => "regra fiscal de imobilizado",
        _ => "regra de contabilização com conta de bem",
    };
    public string NotaTexto => string.IsNullOrEmpty(Serie) ? Nota : $"{Nota}/{Serie}";
}

/// <summary>Sugestão do grupo de bens (regra de depreciação) para um item de nota: pela conta da regra e, na falta dela, pelo NCM.</summary>
public static class SugestaoGrupoBem
{
    private static readonly (string Prefixo, string Grupo)[] PorNcm =
    [
        ("8471", "08"), ("8703", "05"), ("8704", "06"), ("8711", "06"), ("8701", "07"), ("8517", "10"), ("8205", "09"), ("8207", "09"),
        ("94", "04"), ("84", "03"), ("85", "03"),
    ];

    public static GrupoBem? Sugerir(IEnumerable<GrupoBem> grupos, string? conta, string? ncm)
    {
        var lista = grupos as IReadOnlyList<GrupoBem> ?? grupos.ToList();
        if (!string.IsNullOrWhiteSpace(conta) && lista.FirstOrDefault(g => g.ContaBem == conta.Trim()) is { } porConta) return porConta;
        var n = new string((ncm ?? "").Where(char.IsAsciiDigit).ToArray());
        foreach (var (prefixo, codigo) in PorNcm)
            if (n.StartsWith(prefixo, StringComparison.Ordinal) && lista.FirstOrDefault(g => g.Codigo == codigo) is { } g) return g;
        return null;
    }
}

public interface IRepositorioImobilizado
{
    /// <summary>Itens de nota de entrada que são bens (situação nula = todas); ids/documentos nulos = sem filtro.</summary>
    Task<IReadOnlyList<BemACadastrar>> ListarBensACadastrarAsync(long empresaId, string? situacao, IReadOnlyCollection<long>? ids,
                                                                 IReadOnlyCollection<long>? documentoIds, IReadOnlyCollection<string>? chaves,
                                                                 CancellationToken ct);
    /// <summary>Resolve uma pendência ainda aberta (cadastrada ou descartada). False = já estava resolvida ou não existe.</summary>
    Task<bool> ResolverBemACadastrarAsync(long empresaId, long id, string situacao, long? bemId, long usuarioId, CancellationToken ct);

    Task<IReadOnlyList<GrupoBem>> ListarGruposAsync(CancellationToken ct);
    Task<long> GravarGrupoAsync(GrupoBem grupo, CancellationToken ct);
    Task ExcluirGrupoAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<BemImobilizado>> ListarBensAsync(long empresaId, CancellationToken ct);
    Task<long> GravarBemAsync(long empresaId, BemImobilizado bem, long contaBemId, long? contaAcumuladaId, long? contaDespesaId, long? fornecedorId,
                              CancellationToken ct);
    Task ExcluirBemAsync(long id, CancellationToken ct);

    /// <summary>Grava o lançamento e as quotas de cada bem na mesma transação.</summary>
    Task<LancamentoEfetivado> LancarDepreciacaoAsync(LancamentoValidado lancamento, IReadOnlyList<QuotaDepreciacao> quotas, long usuarioId,
                                                     CancellationToken ct);
    /// <summary>Lançamentos de depreciação ativos (não estornados) da competência.</summary>
    Task<IReadOnlyList<long>> LancamentosDaCompetenciaAsync(long empresaId, DateOnly competencia, CancellationToken ct);
}

/// <summary>
/// Imobilizado: cadastro de bens, grupos (regras de depreciação) e o cálculo/lançamento mensal da depreciação.
/// O lançamento de cada competência debita a despesa e credita a depreciação (amortização) acumulada de cada bem,
/// agrupado por par de contas, no último dia do mês.
/// </summary>
public sealed class ServicoImobilizado(IRepositorioImobilizado repositorio, IRepositorioPlanoContas plano, IRepositorioParticipantes participantes,
                                       ServicoLancamento lancamentos, Autorizador autorizador)
{
    // ------------------------------------------------------------------ grupos

    public Task<IReadOnlyList<GrupoBem>> ListarGruposAsync(CancellationToken ct = default) => repositorio.ListarGruposAsync(ct);

    public async Task<Resultado<long>> GravarGrupoAsync(long empresaId, GrupoBem g, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<long>(usuarioId, empresaId, Permissao.LancamentoIncluir, ct) is { } negado) return negado;
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(g.Codigo) || g.Codigo.Trim().Length > 10) erros.Add("Informe o código do grupo (até 10 caracteres).");
        if (string.IsNullOrWhiteSpace(g.Descricao)) erros.Add("Informe a descrição do grupo.");
        if (g.TaxaAnualFiscal is < 0 or > 100) erros.Add("A taxa anual deve estar entre 0% e 100%.");
        if (g.Deprecia && g.TaxaAnualFiscal == 0) erros.Add("Grupo que deprecia precisa de taxa anual (ou desmarque Deprecia).");
        var existentes = await repositorio.ListarGruposAsync(ct);
        if (existentes.Any(x => x.Id != g.Id && x.Codigo == g.Codigo.Trim())) erros.Add($"Já existe o grupo {g.Codigo.Trim()}.");
        if (erros.Count > 0) return Resultado<long>.Falha(erros);
        static string? N(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        try
        {
            return Resultado<long>.Ok(await repositorio.GravarGrupoAsync(g with
            {
                Codigo = g.Codigo.Trim(), Descricao = g.Descricao.Trim(), ContaBem = N(g.ContaBem), ContaAcumulada = N(g.ContaAcumulada),
                ContaDespesa = N(g.ContaDespesa), BaseLegal = N(g.BaseLegal),
            }, ct));
        }
        catch (ViolacaoRegraException ex) { return Resultado<long>.Falha(ex.Message); }
    }

    public async Task<Resultado> ExcluirGrupoAsync(long empresaId, long id, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.LancamentoIncluir, ct) is { } negado) return Resultado.Falha(negado);
        var g = (await repositorio.ListarGruposAsync(ct)).FirstOrDefault(x => x.Id == id);
        if (g is null) return Resultado.Falha("Grupo não encontrado.");
        if (g.EmUso) return Resultado.Falha($"O grupo {g.Codigo} – {g.Descricao} tem bens cadastrados e não pode ser excluído.");
        await repositorio.ExcluirGrupoAsync(id, ct);
        return Resultado.Ok();
    }

    // ------------------------------------------------------------------ bens

    public async Task<IReadOnlyList<BemImobilizado>> ListarBensAsync(long empresaId, long usuarioId, CancellationToken ct = default)
        => (await autorizador.ObterAsync(usuarioId, empresaId, ct)).TemAcesso
            ? (await repositorio.ListarBensAsync(empresaId, ct)).OrderBy(b => b.Codigo).ToList()
            : [];

    // ------------------------------------------------------------------ bens a cadastrar (itens das notas de entrada)

    /// <summary>Pendências de bens das notas de entrada, com o grupo sugerido. Padrão: só as que ainda esperam cadastro.</summary>
    public async Task<IReadOnlyList<BemACadastrar>> ListarBensACadastrarAsync(long empresaId, long usuarioId, bool somentePendentes = true,
                                                                              IReadOnlyCollection<long>? ids = null, IReadOnlyCollection<long>? documentoIds = null,
                                                                              IReadOnlyCollection<string>? chaves = null, CancellationToken ct = default)
    {
        if (!(await autorizador.ObterAsync(usuarioId, empresaId, ct)).TemAcesso) return [];
        var lista = await repositorio.ListarBensACadastrarAsync(empresaId, somentePendentes ? BemACadastrar.Pendente : null, ids, documentoIds, chaves, ct);
        if (lista.Count == 0) return lista;
        var grupos = await repositorio.ListarGruposAsync(ct);
        return lista.Select(b => b with { GrupoSugeridoId = SugestaoGrupoBem.Sugerir(grupos, b.ContaSugerida, b.Ncm)?.Id }).ToList();
    }

    /// <summary>Grava o bem preparado a partir do item da nota e o vincula a ele; o mesmo item não vira dois bens.</summary>
    public async Task<Resultado<long>> GravarBemDaNotaAsync(long empresaId, long pendenciaId, BemImobilizado bem, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<long>(usuarioId, empresaId, Permissao.LancamentoIncluir, ct) is { } negado) return negado;
        var p = (await repositorio.ListarBensACadastrarAsync(empresaId, null, [pendenciaId], null, null, ct)).FirstOrDefault();
        if (p is null) return Resultado<long>.Falha("Item da nota não encontrado entre os bens a cadastrar.");
        if (p.Situacao != BemACadastrar.Pendente) return Resultado<long>.Falha("Este item da nota já foi tratado (bem cadastrado ou descartado).");
        var r = await GravarBemAsync(empresaId, bem with { Id = 0 }, usuarioId, ct);
        if (!r.Sucesso) return r;
        if (!await repositorio.ResolverBemACadastrarAsync(empresaId, pendenciaId, BemACadastrar.Cadastrado, r.Valor, usuarioId, ct))
        {
            await repositorio.ExcluirBemAsync(r.Valor, ct);   // outro usuário resolveu o item no meio do caminho
            return Resultado<long>.Falha("Este item da nota acabou de ser tratado por outro usuário; o bem não foi cadastrado.");
        }
        return r;
    }

    /// <summary>Descarta o item (não é bem do imobilizado, ou será cadastrado de outra forma).</summary>
    public async Task<Resultado> DescartarBemACadastrarAsync(long empresaId, long pendenciaId, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.LancamentoIncluir, ct) is { } negado) return Resultado.Falha(negado);
        return await repositorio.ResolverBemACadastrarAsync(empresaId, pendenciaId, BemACadastrar.Descartado, null, usuarioId, ct)
            ? Resultado.Ok() : Resultado.Falha("Item não encontrado ou já tratado.");
    }

    public static int ProximoCodigo(IEnumerable<BemImobilizado> bens) => Vorticis.Domain.Comum.Sequencia.MenorLivre(bens.Select(b => b.Codigo));

    public async Task<Resultado<long>> GravarBemAsync(long empresaId, BemImobilizado b, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<long>(usuarioId, empresaId, Permissao.LancamentoIncluir, ct) is { } negado) return negado;
        var bens = await repositorio.ListarBensAsync(empresaId, ct);
        var atual = bens.FirstOrDefault(x => x.Id == b.Id);
        if (b.Id != 0 && atual is null) return Resultado<long>.Falha("Bem não encontrado.");
        var grupo = (await repositorio.ListarGruposAsync(ct)).FirstOrDefault(g => g.Id == b.GrupoId);
        var codigo = b.Codigo > 0 ? b.Codigo : ProximoCodigo(bens);
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(b.Descricao)) erros.Add("Informe a descrição do bem.");
        if (grupo is null) erros.Add("Escolha o grupo do bem.");
        if (b.Valor <= 0) erros.Add("Informe o valor de aquisição.");
        if (b.Residual < 0 || b.DepreciacaoAnterior < 0) erros.Add("Valor residual e depreciação anterior não podem ser negativos.");
        if (b.Residual + b.DepreciacaoAnterior > b.Valor) erros.Add("Valor residual + depreciação anterior não podem passar do valor de aquisição.");
        if (b.InicioUso < b.DataAquisicao) erros.Add("O início de uso não pode ser anterior à aquisição.");
        if (b.DataBaixa is { } baixa && baixa < b.DataAquisicao) erros.Add("A baixa não pode ser anterior à aquisição.");
        if (grupo is { Deprecia: true } && b.VidaUtilMeses <= 0) erros.Add("Informe a vida útil (meses).");
        if (b.Turnos is < 1 or > 3) erros.Add("Turnos de uso: 1, 2 ou 3.");
        if (bens.Any(x => x.Id != b.Id && x.Codigo == codigo)) erros.Add($"Já existe o bem de código {codigo}.");
        if (atual is { DepreciacaoLancada: > 0 } && (atual.Valor != b.Valor || atual.Residual != b.Residual || atual.InicioUso != b.InicioUso
                                                    || atual.VidaUtilMeses != b.VidaUtilMeses || atual.Metodo != b.Metodo
                                                    || atual.DepreciacaoAnterior != b.DepreciacaoAnterior
                                                    || atual.ProRataDia != b.ProRataDia || (b.ProRataDia && atual.DataAquisicao != b.DataAquisicao)))
            erros.Add("O bem já tem depreciação lançada: valor, residual, aquisição/início de uso, vida útil, método e pro rata não podem mudar (estorne as competências antes).");

        var contas = await plano.ListarAsync(empresaId, ct);
        ContaDetalhe? Conta(string? texto, string nome, bool obrigatoria)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                if (obrigatoria) erros.Add($"Informe a conta {nome}.");
                return null;
            }
            var t = texto.Trim();
            var c = contas.FirstOrDefault(x => x.Codigo == t || x.CodigoReduzido?.ToString() == t);
            if (c is null || !c.Analitica) { erros.Add($"Conta {nome} {t} não encontrada entre as analíticas."); return null; }
            return c;
        }
        var contaBem = Conta(b.ContaBem, "do bem", true);
        var deprecia = grupo?.Deprecia == true;
        var contaAcum = Conta(b.ContaAcumulada, "de depreciação acumulada", deprecia);
        var contaDesp = Conta(b.ContaDespesa, "de despesa de depreciação", deprecia);

        long? fornecedorId = null;
        if (!string.IsNullOrWhiteSpace(b.CodigoFornecedor))
        {
            var f = (await participantes.ListarAsync(empresaId, ct)).FirstOrDefault(p => p.Codigo == b.CodigoFornecedor.Trim());
            if (f is null) erros.Add($"Fornecedor de código {b.CodigoFornecedor.Trim()} não encontrado.");
            else fornecedorId = f.Id;
        }
        if (erros.Count > 0) return Resultado<long>.Falha(erros);
        static string? N(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        try
        {
            return Resultado<long>.Ok(await repositorio.GravarBemAsync(empresaId, b with
            {
                Codigo = codigo, Descricao = b.Descricao.Trim(), Plaqueta = N(b.Plaqueta), Localizacao = N(b.Localizacao), NotaFiscal = N(b.NotaFiscal),
                VidaUtilMeses = deprecia ? b.VidaUtilMeses : 0, MotivoBaixa = b.DataBaixa is null ? null : N(b.MotivoBaixa),
            }, contaBem!.Id, contaAcum?.Id, contaDesp?.Id, fornecedorId, ct));
        }
        catch (ViolacaoRegraException ex) { return Resultado<long>.Falha(ex.Message); }
    }

    public async Task<Resultado> ExcluirBemAsync(long empresaId, long id, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.LancamentoIncluir, ct) is { } negado) return Resultado.Falha(negado);
        var b = (await repositorio.ListarBensAsync(empresaId, ct)).FirstOrDefault(x => x.Id == id);
        if (b is null) return Resultado.Falha("Bem não encontrado.");
        if (b.DepreciacaoLancada > 0)
            return Resultado.Falha($"O bem {b.Codigo} tem depreciação lançada: registre a baixa (Alterar › data da baixa) em vez de excluir.");
        await repositorio.ExcluirBemAsync(id, ct);
        return Resultado.Ok();
    }

    // ------------------------------------------------------------------ depreciação mensal

    /// <summary>Cálculo da competência para todos os bens (sem gravar).</summary>
    public async Task<Resultado<IReadOnlyList<ItemDepreciacao>>> CalcularAsync(long empresaId, DateOnly competencia, long usuarioId, CancellationToken ct = default)
    {
        if (!(await autorizador.ObterAsync(usuarioId, empresaId, ct)).TemAcesso)
            return Resultado<IReadOnlyList<ItemDepreciacao>>.Falha("Sem acesso a esta empresa.");
        competencia = RegrasDepreciacao.Competencia(competencia);
        var grupos = (await repositorio.ListarGruposAsync(ct)).ToDictionary(g => g.Id);
        var res = new List<ItemDepreciacao>();
        foreach (var b in (await repositorio.ListarBensAsync(empresaId, ct)).OrderBy(b => b.Codigo))
        {
            var g = grupos[b.GrupoId];
            if (b.UltimaCompetencia is { } ult && ult >= competencia)
            {
                res.Add(new ItemDepreciacao(b, g, null, $"Já lançado até {ult:MM/yyyy}"));
                continue;
            }
            var quota = RegrasDepreciacao.Calcular(Para(b, g), competencia, b.DepreciacaoLancada, b.DepreciacaoFiscalLancada);
            var situacao = quota is not null ? (quota.Contabil > 0 ? "Calculado" : "Só fiscal (vida útil contábil encerrada)")
                : !g.Deprecia ? "Não deprecia (" + g.Descricao + ")"
                : b.DataBaixa is { } bx && competencia >= RegrasDepreciacao.Competencia(bx) ? $"Baixado em {bx:dd/MM/yyyy}"
                : competencia < RegrasDepreciacao.Competencia(b.ProRataDia ? b.DataAquisicao : b.InicioUso) ? $"Deprecia a partir de {(b.ProRataDia ? b.DataAquisicao : b.InicioUso):dd/MM/yyyy}"
                : "Totalmente depreciado";
            res.Add(new ItemDepreciacao(b, g, quota, situacao));
        }
        return Resultado<IReadOnlyList<ItemDepreciacao>>.Ok(res);
    }

    private static BemParaDepreciar Para(BemImobilizado b, GrupoBem g) =>
        new(b.Id, b.Valor, b.Residual, b.VidaUtilMeses, b.Metodo, b.InicioUso, b.DataBaixa, b.DepreciacaoAnterior, g.Deprecia,
            g.TaxaAnualFiscal, g.Intangivel ? 1 : b.Turnos, b.ProRataDia, b.DataAquisicao);   // aceleração por turnos só vale para bens móveis tangíveis

    /// <summary>Gera o lançamento da depreciação da competência (último dia do mês) e registra as quotas por bem.</summary>
    public async Task<Resultado<LancamentoEfetivado>> LancarAsync(long empresaId, DateOnly competencia, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<LancamentoEfetivado>(usuarioId, empresaId, Permissao.LancamentoIncluir, ct) is { } negado) return negado;
        var calc = await CalcularAsync(empresaId, competencia, usuarioId, ct);
        if (!calc.Sucesso) return Resultado<LancamentoEfetivado>.Falha(calc.Erros);
        var itens = calc.Valor.Where(i => i.Quota is { Contabil: > 0 }).ToList();
        if (itens.Count == 0) return Resultado<LancamentoEfetivado>.Falha($"Nenhuma depreciação a lançar em {competencia:MM/yyyy}.");
        var semConta = itens.Where(i => i.Bem.ContaAcumulada is null || i.Bem.ContaDespesa is null).Select(i => i.Bem.Codigo).ToList();
        if (semConta.Count > 0)
            return Resultado<LancamentoEfetivado>.Falha($"Informe as contas de depreciação acumulada e de despesa dos bens: {string.Join(", ", semConta)}.");

        var comp = RegrasDepreciacao.Competencia(competencia);
        var data = comp.AddMonths(1).AddDays(-1);
        var partidas = new List<PartidaProposta>();
        foreach (var par in itens.GroupBy(i => (Desp: i.Bem.ContaDespesa!, Acum: i.Bem.ContaAcumulada!, i.Grupo.Intangivel)))
        {
            var total = par.Sum(i => i.Quota!.Contabil);
            var hist = $"{(par.Key.Intangivel ? "Amortização" : "Depreciação")} de {comp:MM/yyyy} - bens {string.Join(", ", par.Select(i => i.Bem.Codigo))}";
            if (hist.Length > 200) hist = $"{(par.Key.Intangivel ? "Amortização" : "Depreciação")} de {comp:MM/yyyy} - {par.Count()} bens";
            partidas.Add(new PartidaProposta(par.Key.Desp, IndicadorDC.D, total, hist));
            partidas.Add(new PartidaProposta(par.Key.Acum, IndicadorDC.C, total, hist));
        }
        var proposta = new LancamentoProposto(empresaId, data, OrigemLancamento.Depreciacao, partidas);
        var validacao = (await lancamentos.ValidarLoteAsync([proposta], ct))[0];
        if (!validacao.Sucesso) return Resultado<LancamentoEfetivado>.Falha(validacao.Erros);
        try
        {
            return Resultado<LancamentoEfetivado>.Ok(await repositorio.LancarDepreciacaoAsync(validacao.Valor, itens.Select(i => i.Quota!).ToList(), usuarioId, ct));
        }
        catch (ViolacaoRegraException ex) { return Resultado<LancamentoEfetivado>.Falha(ex.Message); }
    }

    /// <summary>Desfaz a depreciação da competência estornando o(s) lançamento(s); as quotas deixam de contar.</summary>
    public async Task<Resultado<int>> EstornarAsync(long empresaId, DateOnly competencia, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<int>(usuarioId, empresaId, Permissao.LancamentoEstornar, ct) is { } negado) return negado;
        var comp = RegrasDepreciacao.Competencia(competencia);
        var ids = await repositorio.LancamentosDaCompetenciaAsync(empresaId, comp, ct);
        if (ids.Count == 0) return Resultado<int>.Falha($"Não há depreciação lançada em {comp:MM/yyyy}.");
        var posteriores = (await repositorio.ListarBensAsync(empresaId, ct)).Any(b => b.UltimaCompetencia > comp);
        if (posteriores) return Resultado<int>.Falha("Há depreciação lançada em competências posteriores: estorne primeiro a mais recente.");
        foreach (var id in ids)
        {
            var r = await lancamentos.EstornarAsync(empresaId, id, comp.AddMonths(1).AddDays(-1), $"Estorno da depreciação de {comp:MM/yyyy}", usuarioId, ct);
            if (!r.Sucesso) return Resultado<int>.Falha(r.Erros);
        }
        return Resultado<int>.Ok(ids.Count);
    }
}
