using System.Text.RegularExpressions;
using Perseus.Application.Seguranca;
using Perseus.Domain.Comum;
using Perseus.Domain.Contabil;
using Perseus.Domain.Seguranca;

namespace Perseus.Application.Contabil;

public sealed record ContaDetalhe(
    long Id, string Codigo, string Descricao, bool Analitica, short Nivel, long? ContaPaiId, string CodNat,
    IndicadorDC NaturezaSaldo, bool ExigeCentroCusto, bool ExigeParticipante, DateOnly DataInclusao, bool Ativa,
    bool TemMovimento, bool TemFilhas, decimal Saldo);

public sealed record NovaConta(string Codigo, string Descricao, string CodNat, IndicadorDC NaturezaSaldo,
                               bool ExigeCentroCusto, bool ExigeParticipante, DateOnly DataInclusao);

public sealed record CentroCustoDetalhe(long Id, string Codigo, string Descricao, long? PaiId, bool Ativo, bool TemMovimento);

public interface IRepositorioPlanoContas
{
    Task<IReadOnlyList<ContaDetalhe>> ListarAsync(long empresaId, CancellationToken ct);

    /// <summary>Insere a conta; se <paramref name="paiParaSintetica"/> for informado, converte o pai em sintético na mesma transação.</summary>
    Task<long> IncluirAsync(long empresaId, NovaConta conta, long? contaPaiId, short nivel, bool analitica, long? paiParaSintetica,
                            long usuarioId, CancellationToken ct);
    Task AlterarAsync(long contaId, string descricao, IndicadorDC naturezaSaldo, bool exigeCentroCusto, bool exigeParticipante, bool ativa,
                      long usuarioId, CancellationToken ct);
    Task ExcluirAsync(long contaId, long usuarioId, CancellationToken ct);
    /// <summary>Apaga o plano da empresa (sem lançamentos) e grava o novo, numa transação.</summary>
    Task SubstituirAsync(long empresaId, IReadOnlyList<Cadastros.ContaModelo> plano, long usuarioId, CancellationToken ct);
    /// <summary>Troca os códigos das contas (mesma hierarquia), numa transação.</summary>
    Task RecodificarAsync(long empresaId, IReadOnlyDictionary<long, string> codigos, long usuarioId, CancellationToken ct);

    Task<IReadOnlyList<CentroCustoDetalhe>> ListarCentrosAsync(long empresaId, CancellationToken ct);
    Task<long> IncluirCentroAsync(long empresaId, string codigo, string descricao, long? paiId, DateOnly dataInclusao, CancellationToken ct);
    Task AlterarCentroAsync(long centroId, string descricao, bool ativo, CancellationToken ct);
    Task ExcluirCentroAsync(long centroId, CancellationToken ct);
}

/// <summary>
/// Manutenção do plano de contas e dos centros de custo (ECD I050/I100). Regras:
/// código hierárquico (pai = código sem o último segmento); nível 1 sempre sintético; só analíticas recebem
/// lançamentos; uma analítica com movimento não pode virar sintética; conta com movimento ou filhas não é
/// excluída (inativa-se); só se inativa conta com saldo zero.
/// </summary>
public sealed partial class ServicoPlanoContas(IRepositorioPlanoContas repositorio, Autorizador autorizador)
{
    [GeneratedRegex(@"^\d+(\.\d+)*$")]
    private static partial Regex FormatoCodigo();

    private static readonly string[] NaturezasValidas = ["01", "02", "03", "04", "05", "09"];

    public async Task<IReadOnlyList<ContaDetalhe>> ListarAsync(long empresaId, long usuarioId, CancellationToken ct = default)
        => (await autorizador.ObterAsync(usuarioId, empresaId, ct)).TemAcesso ? await repositorio.ListarAsync(empresaId, ct) : [];

    public async Task<Resultado<long>> IncluirAsync(long empresaId, NovaConta c, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<long>(usuarioId, empresaId, Permissao.PlanoContasManter, ct) is { } negado) return negado;
        var codigo = c.Codigo.Trim();
        var erros = new List<string>();
        if (!FormatoCodigo().IsMatch(codigo) || codigo.Length > 30) erros.Add("Código inválido: use números separados por ponto (ex.: 1.1.01.004), até 30 caracteres.");
        if (string.IsNullOrWhiteSpace(c.Descricao)) erros.Add("Informe a descrição.");
        if (!NaturezasValidas.Contains(c.CodNat)) erros.Add("Natureza (COD_NAT) inválida.");
        if (erros.Count > 0) return Resultado<long>.Falha(erros);

        var plano = await repositorio.ListarAsync(empresaId, ct);
        if (plano.Any(x => x.Codigo == codigo)) return Resultado<long>.Falha($"Já existe a conta {codigo}.");

        var segmentos = codigo.Split('.');
        if (segmentos.Length == 1)
            return Resultado<long>.Ok(await repositorio.IncluirAsync(empresaId, c with { Codigo = codigo, Descricao = c.Descricao.Trim() },
                null, 1, analitica: false, null, usuarioId, ct));

        var codigoPai = string.Join('.', segmentos[..^1]);
        var pai = plano.FirstOrDefault(x => x.Codigo == codigoPai);
        if (pai is null) return Resultado<long>.Falha($"A conta superior {codigoPai} não existe. Cadastre-a primeiro.");
        if (pai.Analitica && pai.TemMovimento)
            return Resultado<long>.Falha($"A conta {codigoPai} é analítica e já tem lançamentos; não pode receber subcontas. Crie a nova conta em outro grupo.");
        if (!pai.Ativa) return Resultado<long>.Falha($"A conta superior {codigoPai} está inativa.");
        if (c.CodNat != pai.CodNat && !(pai.CodNat == "02" && c.CodNat == "03"))   // PL pode ficar sob o grupo do Passivo
            return Resultado<long>.Falha($"A natureza da conta deve ser a mesma da conta superior ({pai.CodNat}).");
        if (c.DataInclusao < pai.DataInclusao)
            return Resultado<long>.Falha($"A data de inclusão não pode ser anterior à da conta superior ({pai.DataInclusao:dd/MM/yyyy}).");

        return Resultado<long>.Ok(await repositorio.IncluirAsync(empresaId, c with { Codigo = codigo, Descricao = c.Descricao.Trim() },
            pai.Id, (short)segmentos.Length, analitica: true, pai.Analitica ? pai.Id : null, usuarioId, ct));
    }

    public async Task<Resultado> AlterarAsync(long empresaId, long contaId, string descricao, IndicadorDC naturezaSaldo, bool exigeCentroCusto,
                                             bool exigeParticipante, bool ativa, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.PlanoContasManter, ct) is { } negado) return Resultado.Falha(negado);
        if (string.IsNullOrWhiteSpace(descricao)) return Resultado.Falha("Informe a descrição.");
        var plano = await repositorio.ListarAsync(empresaId, ct);
        var conta = plano.FirstOrDefault(x => x.Id == contaId);
        if (conta is null) return Resultado.Falha("Conta não encontrada.");
        if (conta.Ativa && !ativa)
        {
            var comSaldo = plano.Where(x => x.Codigo == conta.Codigo || x.Codigo.StartsWith(conta.Codigo + ".", StringComparison.Ordinal))
                                .FirstOrDefault(x => x.Analitica && x.Saldo != 0);
            if (comSaldo is not null)
                return Resultado.Falha($"Não é possível inativar: a conta {comSaldo.Codigo} tem saldo {LinhaBalancete.Formatar(comSaldo.Saldo)}. Transfira o saldo antes.");
        }
        await repositorio.AlterarAsync(contaId, descricao.Trim(), naturezaSaldo, exigeCentroCusto, exigeParticipante, ativa, usuarioId, ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> ExcluirAsync(long empresaId, long contaId, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.PlanoContasManter, ct) is { } negado) return Resultado.Falha(negado);
        var conta = (await repositorio.ListarAsync(empresaId, ct)).FirstOrDefault(x => x.Id == contaId);
        if (conta is null) return Resultado.Falha("Conta não encontrada.");
        if (conta.TemFilhas) return Resultado.Falha($"A conta {conta.Codigo} tem subcontas; exclua-as antes.");
        if (conta.TemMovimento) return Resultado.Falha($"A conta {conta.Codigo} tem lançamentos e não pode ser excluída; inative-a.");
        try
        {
            await repositorio.ExcluirAsync(contaId, usuarioId, ct);
            return Resultado.Ok();
        }
        catch (ViolacaoRegraException ex)
        {
            return Resultado.Falha($"Não foi possível excluir: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------ importação e máscara

    /// <summary>
    /// Substitui o plano da empresa pelo lido da planilha. Só é permitido enquanto nenhuma conta tem lançamentos
    /// (inclusive rascunhos e saldos iniciais); os vínculos com o plano referencial do plano anterior são descartados.
    /// </summary>
    public async Task<Resultado<int>> ImportarAsync(long empresaId, PlanoImportado plano, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<int>(usuarioId, empresaId, Permissao.PlanoContasManter, ct) is { } negado) return negado;
        if (plano.Erros.Count > 0) return Resultado<int>.Falha(plano.Erros);
        if (plano.Contas.Count == 0) return Resultado<int>.Falha("A planilha não tem contas.");
        if (plano.Contas.FirstOrDefault(c => !NaturezasValidas.Contains(c.CodNat)) is { } invalida)
            return Resultado<int>.Falha($"Conta {invalida.Codigo}: natureza (COD_NAT) inválida.");
        var atual = await repositorio.ListarAsync(empresaId, ct);
        if (atual.FirstOrDefault(c => c.TemMovimento) is { } comMovimento)
            return Resultado<int>.Falha($"O plano atual já tem lançamentos (ex.: conta {comMovimento.Codigo}); a importação substitui o plano inteiro e só é " +
                                        "permitida antes do primeiro lançamento. Para ajustar só os códigos, use a conversão de máscara.");
        try
        {
            await repositorio.SubstituirAsync(empresaId, plano.Contas.Select(c =>
                new Cadastros.ContaModelo(c.Codigo, c.Descricao, c.CodNat, c.NaturezaSaldo, c.Analitica)).ToList(), usuarioId, ct);
            return Resultado<int>.Ok(plano.Contas.Count);
        }
        catch (ViolacaoRegraException ex)
        {
            return Resultado<int>.Falha($"Não foi possível importar: {ex.Message}");
        }
    }

    /// <summary>Códigos atuais convertidos para a máscara (De → Para), ou os erros que impedem a conversão.</summary>
    public async Task<Resultado<IReadOnlyList<(ContaDetalhe Conta, string Novo)>>> SimularMascaraAsync(long empresaId, MascaraPlano mascara, long usuarioId,
                                                                                                       CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<IReadOnlyList<(ContaDetalhe, string)>>(usuarioId, empresaId, Permissao.PlanoContasManter, ct) is { } negado) return negado;
        var plano = await repositorio.ListarAsync(empresaId, ct);
        if (plano.Count == 0) return Resultado<IReadOnlyList<(ContaDetalhe, string)>>.Falha("A empresa não tem plano de contas.");
        var erros = new List<string>();
        var res = new List<(ContaDetalhe, string)>();
        foreach (var c in plano)
        {
            var r = mascara.Converter(c.Codigo);
            if (!r.Sucesso) erros.Add($"Conta {c.Codigo}: {r.Erros[0]}.");
            else if (r.Valor.Count(ch => ch == '.') != c.Codigo.Count(ch => ch == '.'))
                erros.Add($"Conta {c.Codigo}: a conversão mudaria o nível da conta.");
            else res.Add((c, r.Valor));
        }
        foreach (var g in res.GroupBy(x => x.Item2).Where(g => g.Count() > 1).Take(10))
            erros.Add($"As contas {string.Join(", ", g.Select(x => x.Item1.Codigo))} ficariam com o mesmo código {g.Key}.");
        return erros.Count > 0 ? Resultado<IReadOnlyList<(ContaDetalhe, string)>>.Falha(erros.Take(30))
                               : Resultado<IReadOnlyList<(ContaDetalhe, string)>>.Ok(res);
    }

    /// <summary>
    /// Converte os códigos do plano atual para a máscara. A hierarquia e os lançamentos ficam como estão
    /// (os lançamentos apontam para a conta, não para o código). Retorna quantas contas mudaram de código.
    /// </summary>
    public async Task<Resultado<int>> AplicarMascaraAsync(long empresaId, MascaraPlano mascara, long usuarioId, CancellationToken ct = default)
    {
        var sim = await SimularMascaraAsync(empresaId, mascara, usuarioId, ct);
        if (!sim.Sucesso) return Resultado<int>.Falha(sim.Erros);
        var mudancas = sim.Valor.Where(x => x.Conta.Codigo != x.Novo).ToDictionary(x => x.Conta.Id, x => x.Novo);
        if (mudancas.Count == 0) return Resultado<int>.Ok(0);
        try
        {
            await repositorio.RecodificarAsync(empresaId, mudancas, usuarioId, ct);
            return Resultado<int>.Ok(mudancas.Count);
        }
        catch (ViolacaoRegraException ex)
        {
            return Resultado<int>.Falha($"Não foi possível converter: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------ centros de custo

    public async Task<IReadOnlyList<CentroCustoDetalhe>> ListarCentrosAsync(long empresaId, long usuarioId, CancellationToken ct = default)
        => (await autorizador.ObterAsync(usuarioId, empresaId, ct)).TemAcesso ? await repositorio.ListarCentrosAsync(empresaId, ct) : [];

    public async Task<Resultado<long>> IncluirCentroAsync(long empresaId, string codigo, string descricao, string? codigoPai, long usuarioId,
                                                         DateOnly? dataInclusao = null, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<long>(usuarioId, empresaId, Permissao.CentroCustoManter, ct) is { } negado) return negado;
        codigo = codigo.Trim();
        if (codigo.Length is 0 or > 30) return Resultado<long>.Falha("Informe o código (até 30 caracteres).");
        if (string.IsNullOrWhiteSpace(descricao)) return Resultado<long>.Falha("Informe a descrição.");
        var centros = await repositorio.ListarCentrosAsync(empresaId, ct);
        if (centros.Any(c => c.Codigo == codigo)) return Resultado<long>.Falha($"Já existe o centro de custo {codigo}.");
        long? paiId = null;
        if (!string.IsNullOrWhiteSpace(codigoPai))
        {
            var pai = centros.FirstOrDefault(c => c.Codigo == codigoPai.Trim());
            if (pai is null) return Resultado<long>.Falha($"Centro de custo superior {codigoPai} não encontrado.");
            paiId = pai.Id;
        }
        return Resultado<long>.Ok(await repositorio.IncluirCentroAsync(empresaId, codigo, descricao.Trim(), paiId,
            dataInclusao ?? DateOnly.FromDateTime(DateTime.Today), ct));
    }

    public async Task<Resultado> AlterarCentroAsync(long empresaId, long centroId, string descricao, bool ativo, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.CentroCustoManter, ct) is { } negado) return Resultado.Falha(negado);
        if (string.IsNullOrWhiteSpace(descricao)) return Resultado.Falha("Informe a descrição.");
        if ((await repositorio.ListarCentrosAsync(empresaId, ct)).All(c => c.Id != centroId)) return Resultado.Falha("Centro de custo não encontrado.");
        await repositorio.AlterarCentroAsync(centroId, descricao.Trim(), ativo, ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> ExcluirCentroAsync(long empresaId, long centroId, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.CentroCustoManter, ct) is { } negado) return Resultado.Falha(negado);
        var centros = await repositorio.ListarCentrosAsync(empresaId, ct);
        var centro = centros.FirstOrDefault(c => c.Id == centroId);
        if (centro is null) return Resultado.Falha("Centro de custo não encontrado.");
        if (centro.TemMovimento) return Resultado.Falha($"O centro de custo {centro.Codigo} tem lançamentos; inative-o.");
        if (centros.Any(c => c.PaiId == centroId)) return Resultado.Falha($"O centro de custo {centro.Codigo} tem subcentros; exclua-os antes.");
        try
        {
            await repositorio.ExcluirCentroAsync(centroId, ct);
            return Resultado.Ok();
        }
        catch (ViolacaoRegraException ex)
        {
            return Resultado.Falha($"Não foi possível excluir: {ex.Message}");
        }
    }
}
