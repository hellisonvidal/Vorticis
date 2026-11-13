using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Folha.Dados;
using Vorticis.Folha.Regras;

namespace Vorticis.Folha.Servicos;

/// <summary>
/// Empréstimo consignado em folha: contratos dos funcionários, lançamento das parcelas da competência como desconto
/// (rubrica 2013, evento variável) e margem consignável pela remuneração disponível da última folha calculada.
/// </summary>
public sealed class ServicoConsignado(RepositorioConsignado repositorio, IRepositorioCadastrosFolha cadastros, IRepositorioMovimentoFolha movimento,
                                      ServicoFolha folha, Autorizador autorizador)
{
    private static DateOnly Mes(DateOnly d) => new(d.Year, d.Month, 1);

    public async Task<IReadOnlyList<ContratoConsignado>> ListarAsync(long empresaId, long usuarioId, CancellationToken ct = default)
        => await autorizador.PodeVerAsync(usuarioId, empresaId, ct) ? await repositorio.ListarAsync(empresaId, ct) : [];

    public async Task<Resultado<long>> GravarAsync(long empresaId, ContratoConsignado c, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<long>(usuarioId, empresaId, Permissao.FolhaEventosLancar, ct) is { } negado) return negado;
        if (RegrasConsignado.Validar(c) is { } erro) return Resultado<long>.Falha(erro);
        var existente = c.Id == 0 ? null : (await repositorio.ListarAsync(empresaId, ct)).FirstOrDefault(x => x.Id == c.Id);
        if (existente is { ParcelasDescontadas: > 0 } && (existente.FuncionarioId != c.FuncionarioId || existente.ValorParcela != c.ValorParcela
                                                          || Mes(existente.PrimeiraCompetencia) != Mes(c.PrimeiraCompetencia)))
            return Resultado<long>.Falha("O contrato já tem parcelas descontadas: só a quantidade de parcelas, a situação e a observação podem mudar.");
        if (existente is not null && c.TotalParcelas < existente.ParcelasDescontadas)
            return Resultado<long>.Falha($"Já foram descontadas {existente.ParcelasDescontadas} parcelas.");
        try { return Resultado<long>.Ok(await repositorio.GravarAsync(empresaId, c with { PrimeiraCompetencia = Mes(c.PrimeiraCompetencia) }, ct)); }
        catch (ViolacaoRegraException ex) { return Resultado<long>.Falha(ex.Message.Contains("duplicado") ? "Este contrato já está cadastrado para o funcionário." : ex.Message); }
    }

    public async Task<Resultado> ExcluirAsync(long empresaId, long id, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.FolhaEventosLancar, ct) is { } negado) return Resultado.Falha(negado);
        return await repositorio.ExcluirAsync(empresaId, id, ct)
            ? Resultado.Ok()
            : Resultado.Falha("O contrato já tem parcelas descontadas: em vez de excluir, mude a situação para Cancelado.");
    }

    /// <summary>Lança como desconto (evento variável) a parcela de cada contrato ativo devida na competência; devolve quantas lançou e os avisos.</summary>
    public async Task<Resultado<(int Lancadas, decimal Total, IReadOnlyList<string> Avisos)>> LancarParcelasAsync(long empresaId, DateOnly competencia, long usuarioId,
                                                                                                            CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<(int, decimal, IReadOnlyList<string>)>(usuarioId, empresaId, Permissao.FolhaEventosLancar, ct) is { } negado) return negado;
        competencia = Mes(competencia);
        var avisos = new List<string>();
        int n = 0; decimal total = 0;
        foreach (var c in await repositorio.ListarAsync(empresaId, ct))
        {
            if (RegrasConsignado.ParcelaDaCompetencia(c, competencia) is not { } numero) continue;
            var r = await folha.LancarEventoAsync(empresaId, c.FuncionarioId, competencia, RubricasPadrao.Consignado, 0, c.ValorParcela,
                                                  $"Consignado {c.Instituicao} – contrato {c.Contrato} – parcela {numero}/{c.TotalParcelas}", usuarioId, ct);
            if (!r.Sucesso) { avisos.Add($"{c.FuncionarioNome} ({c.Instituicao}): {string.Join("; ", r.Erros)}"); continue; }
            await repositorio.RegistrarParcelaAsync(c.Id, competencia, numero, c.ValorParcela, r.Valor, numero == c.TotalParcelas, ct);
            n++; total += c.ValorParcela;
        }
        foreach (var m in (await MargensAsync(empresaId, competencia, usuarioId, ct)).Valor ?? [])
            if (m.Livre < 0) avisos.Add($"{m.Nome}: as parcelas ({Formato.Moeda(m.Comprometido)}) passam da margem consignável ({Formato.Moeda(m.Margem)}).");
        return Resultado<(int, decimal, IReadOnlyList<string>)>.Ok((n, total, avisos));
    }

    /// <summary>Margem consignável de cada funcionário ativo, pela última folha mensal calculada até a competência.</summary>
    public async Task<Resultado<IReadOnlyList<MargemConsignavel>>> MargensAsync(long empresaId, DateOnly competencia, long usuarioId, CancellationToken ct = default)
    {
        if (!await autorizador.PodeVerAsync(usuarioId, empresaId, ct)) return Resultado<IReadOnlyList<MargemConsignavel>>.Falha("Sem acesso à empresa.");
        competencia = Mes(competencia);
        var (pct, _) = await repositorio.PercentuaisAsync(competencia, ct);
        var contratos = (await repositorio.ListarAsync(empresaId, ct)).Where(c => c.Situacao == "ATIVO").ToList();
        var ultimas = new Dictionary<long, Cadastros.CalculoGravado>();
        for (var c = competencia; c >= competencia.AddMonths(-6) && ultimas.Count == 0; c = c.AddMonths(-1))
            foreach (var calc in await movimento.ListarCalculosAsync(empresaId, c, TipoFolha.Mensal, true, ct))
                ultimas.TryAdd(calc.FuncionarioId, calc);
        var lista = new List<MargemConsignavel>();
        foreach (var f in (await cadastros.ListarFuncionariosAsync(empresaId, ct)).Where(f => f.Ativo))
        {
            var calc = ultimas.GetValueOrDefault(f.Id);
            var remuneracao = calc?.TotalProventos ?? f.Salario;
            var pensao = calc?.Itens.Where(i => i.Codigo == RubricasPadrao.PensaoAlimenticia).Sum(i => i.Valor) ?? 0;
            var obrigatorios = (calc?.Inss ?? 0) + (calc?.Irrf ?? 0) + pensao;
            var margem = RegrasConsignado.Margem(remuneracao, obrigatorios, pct);
            var comprometido = contratos.Where(c => c.FuncionarioId == f.Id).Sum(c => c.ValorParcela);
            lista.Add(new MargemConsignavel(f.Id, f.Matricula, f.Nome, remuneracao, obrigatorios, remuneracao - obrigatorios, margem, comprometido));
        }
        return Resultado<IReadOnlyList<MargemConsignavel>>.Ok(lista.OrderBy(m => m.Nome, StringComparer.CurrentCulture).ToList());
    }
}
