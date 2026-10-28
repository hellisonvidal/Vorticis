using Vorticis.Application.Contabil;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Contabil;
using Vorticis.Domain.Seguranca;
using Vorticis.Folha.Cadastros;
using Vorticis.Folha.Regras;

namespace Vorticis.Folha.Servicos;

/// <summary>Uma linha da prévia da contabilização por eventos.</summary>
public sealed record EventoPrevia(string Codigo, string Descricao, string Historico, decimal Valor, string Contas, string Centros, string Situacao,
                                  bool Pronto, bool Contabilizado, long? NumeroLancamento, IReadOnlyList<string> Linhas);

public sealed record EventoContabilizado(string Codigo, string Descricao, decimal Valor, long LancamentoId, long Numero);

/// <summary>
/// Contabilização da folha por eventos: em vez de um lançamento por competência, um lançamento para cada evento contábil (rubricas agrupadas em
/// <see cref="EventoContabil"/>; rubrica fora de grupo forma o seu próprio evento), com histórico do evento, partidas por centro de custo conforme a regra
/// da rubrica e, opcionalmente, uma partida por funcionário. Usa as mesmas regras rubrica → conta da contabilização resumida.
/// </summary>
public sealed partial class ServicoContabilizacaoFolha
{
    public const string TipoEvento = "EVENTO";
    public const string CalcMensal = "MENSAL", CalcFerias = "FERIAS", CalcDecimo = "DECIMO", CalcRescisao = "RESCISAO", CalcProvisao = "PROVISAO";

    public static readonly IReadOnlyList<(string Codigo, string Nome)> TiposCalculo =
        [(CalcMensal, "Folha mensal"), (CalcFerias, "Férias"), (CalcDecimo, "13º salário"), (CalcRescisao, "Rescisões"), (CalcProvisao, "Provisões de férias e 13º")];

    private static string CalculoDe(TipoFolha t) => t switch
    {
        TipoFolha.Ferias => CalcFerias, TipoFolha.Decimo1 or TipoFolha.Decimo2 => CalcDecimo, TipoFolha.Rescisao => CalcRescisao, _ => CalcMensal,
    };

    private static (string TipoCalculo, string Codigo) ReferenciaEvento(PendenciaContabil p)
    {
        var partes = p.Referencia.Split('|', 3);
        return (partes.Length > 1 ? partes[1] : CalcMensal, partes.Length > 2 ? partes[2] : "");
    }

    // ------------------------------------------------------------------ cadastro de eventos contábeis

    public async Task<IReadOnlyList<EventoContabil>> ListarEventosContabeisAsync(long empresaId, long usuarioId, CancellationToken ct = default)
        => await autorizador.PodeVerAsync(usuarioId, empresaId, ct) ? await repositorio.ListarEventosContabeisAsync(empresaId, ct) : [];

    public async Task<Resultado> GravarEventoContabilAsync(long empresaId, EventoContabil e, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.FolhaParametrosManter, ct) is { } negado) return Resultado.Falha(negado);
        var erros = new List<string>();
        var codigo = (e.Codigo ?? "").Trim().ToUpperInvariant();
        var chaves = (e.Chaves ?? []).Select(c => c.Trim()).Where(c => c.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (codigo.Length == 0) erros.Add("Informe o código do evento.");
        if (string.IsNullOrWhiteSpace(e.Descricao)) erros.Add("Informe a descrição do evento (ela vai no histórico do lançamento).");
        if (chaves.Count == 0) erros.Add("Informe ao menos uma rubrica que soma neste evento.");
        var existentes = await repositorio.ListarEventosContabeisAsync(empresaId, ct);
        if (existentes.Any(x => x.Id != e.Id && x.Codigo == codigo)) erros.Add($"Já existe o evento {codigo}.");
        foreach (var chave in chaves)
            if (existentes.FirstOrDefault(x => x.Id != e.Id && x.Chaves.Contains(chave)) is { } outro)
                erros.Add($"A rubrica {chave} já soma no evento {outro.Codigo} – {outro.Descricao}.");
        if (erros.Count > 0) return Resultado.Falha(erros);
        try
        {
            await repositorio.GravarEventoContabilAsync(empresaId, new EventoContabil(e.Id, codigo, e.Descricao.Trim(), chaves), ct);
            return Resultado.Ok();
        }
        catch (ViolacaoRegraException ex) { return Resultado.Falha(ex.Message); }
    }

    public async Task<Resultado> ExcluirEventoContabilAsync(long empresaId, long id, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.FolhaParametrosManter, ct) is { } negado) return Resultado.Falha(negado);
        await repositorio.ExcluirEventoContabilAsync(empresaId, id, ct);
        return Resultado.Ok();
    }

    // ------------------------------------------------------------------ montagem

    private sealed record Linha(string Chave, string Descricao, string? Centro, long FuncionarioId, string Matricula, string Funcionario, decimal Valor);

    private sealed record EventoMontado(string Codigo, string Descricao, string Historico, decimal Valor, IReadOnlyList<string> Contas, IReadOnlyList<string> Centros,
                                        IReadOnlyList<string> Erros, IReadOnlyList<string> Linhas, LancamentoProposto? Proposta);

    private async Task<List<Linha>> ColetarAsync(long empresaId, DateOnly competencia, string tipoCalculo, CancellationToken ct)
    {
        var res = new List<Linha>();
        if (tipoCalculo == CalcProvisao)
        {
            foreach (var p in await repositorio.ListarProvisoesAsync(empresaId, competencia, ct))
                foreach (var (chave, desc, valor) in ComponentesProvisao(p))
                    if (valor != 0) res.Add(new Linha(chave, desc, p.CentroCustoCodigo, p.FuncionarioId, p.Matricula, p.FuncionarioNome, valor));
            return res;
        }
        foreach (var c in await movimento.ListarCalculosAsync(empresaId, competencia, null, true, ct))
        {
            if (CalculoDe(c.Tipo) != tipoCalculo) continue;
            foreach (var i in c.Itens.Where(i => i.Tipo is 'P' or 'D'))
                if (i.Valor != 0) res.Add(new Linha(i.Codigo, i.Descricao, c.CentroCustoCodigo, c.FuncionarioId, c.Matricula, c.FuncionarioNome, i.Valor));
            foreach (var (chave, desc, valor) in ComponentesEncargos(c))
                if (valor != 0) res.Add(new Linha(chave, desc, c.CentroCustoCodigo, c.FuncionarioId, c.Matricula, c.FuncionarioNome, valor));
        }
        return res;
    }

    private async Task<Resultado<List<EventoMontado>>> MontarEventosAsync(long empresaId, DateOnly competencia, string tipoCalculo, bool detalhar, CancellationToken ct)
    {
        var linhas = await ColetarAsync(empresaId, competencia, tipoCalculo, ct);
        var nome = TiposCalculo.First(t => t.Codigo == tipoCalculo).Nome;
        if (linhas.Count == 0) return Resultado<List<EventoMontado>>.Falha($"Não há valores de {nome.ToLowerInvariant()} em {competencia:MM/yyyy} para contabilizar.");
        var regras = (await repositorio.ListarRegrasAsync(empresaId, ct)).ToDictionary(r => r.Chave);
        var eventos = await repositorio.ListarEventosContabeisAsync(empresaId, ct);
        var porChave = new Dictionary<string, EventoContabil>();
        foreach (var e in eventos) foreach (var k in e.Chaves) porChave[k] = e;

        var montados = new List<EventoMontado>();
        var grupos = linhas.GroupBy(l => porChave.TryGetValue(l.Chave, out var e) ? (e.Codigo, e.Descricao) : (l.Chave, l.Descricao)).OrderBy(g => g.Key.Item1, StringComparer.Ordinal);
        foreach (var g in grupos)
        {
            var (codigo, descricao) = g.Key;
            var historico = $"{descricao} {competencia:MM/yyyy}";
            var semRegra = g.Select(l => l.Chave).Distinct().Where(k => !regras.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
            var erros = new List<string>();
            if (semRegra.Count > 0)
                erros.Add($"Sem regra de contabilização para {string.Join(", ", semRegra)}. Cadastre-a em Cálculos › Folha › Contabilização.");
            var partidas = new List<PartidaProposta>();
            var textos = new List<string>();
            var contas = new SortedSet<string>(StringComparer.Ordinal);
            var centros = new SortedSet<string>(StringComparer.Ordinal);
            var agrupadas = g.Where(l => regras.ContainsKey(l.Chave))
                .GroupBy(l => (l.Chave, Centro: regras[l.Chave].UsaCentroCusto ? l.Centro : null, Func: detalhar ? l.FuncionarioId : 0L))
                .OrderBy(x => x.Key.Chave, StringComparer.Ordinal).ThenBy(x => x.Key.Centro, StringComparer.Ordinal).ThenBy(x => x.First().Funcionario, StringComparer.Ordinal);
            foreach (var x in agrupadas)
            {
                var regra = regras[x.Key.Chave];
                var valor = x.Sum(l => l.Valor);
                if (valor <= 0) { erros.Add($"Valor negativo ou zerado em {x.Key.Chave} – {x.First().Descricao}: confira os cálculos."); continue; }
                var h = historico + (x.Key.Centro is null ? "" : $" (C.C. {x.Key.Centro})") + (detalhar ? $" – {x.First().Funcionario} ({x.First().Matricula})" : "");
                partidas.Add(new PartidaProposta(regra.ContaDebito, IndicadorDC.D, valor, h, regra.LadoCentroCusto == 'D' ? x.Key.Centro : null));
                partidas.Add(new PartidaProposta(regra.ContaCredito, IndicadorDC.C, valor, h, regra.LadoCentroCusto == 'C' ? x.Key.Centro : null));
                contas.Add($"D {regra.ContaDebito} / C {regra.ContaCredito}");
                if (x.Key.Centro is not null) centros.Add(x.Key.Centro);
                textos.Add($"{x.Key.Chave} {x.First().Descricao}{(x.Key.Centro is null ? "" : $" [{x.Key.Centro}]")}{(detalhar ? $" – {x.First().Funcionario}" : "")}: " +
                           $"D {regra.ContaDebito} / C {regra.ContaCredito} = {Formato.Moeda(valor)}");
            }
            var total = partidas.Where(p => p.DC == IndicadorDC.D).Sum(p => p.Valor);
            var proposta = erros.Count == 0 && partidas.Count > 0 ? new LancamentoProposto(empresaId, Ultimo(competencia), OrigemLancamento.Folha, partidas) : null;
            montados.Add(new EventoMontado(codigo, descricao, historico, erros.Count == 0 ? total : g.Sum(l => l.Valor), [.. contas], [.. centros], erros, textos, proposta));
        }
        return Resultado<List<EventoMontado>>.Ok(montados);
    }

    // ------------------------------------------------------------------ prévia

    public async Task<Resultado<IReadOnlyList<EventoPrevia>>> PreviaEventosAsync(long empresaId, DateOnly competencia, string tipoCalculo, long usuarioId,
                                                                                 CancellationToken ct = default, bool? detalhar = null)
    {
        if (await autorizador.NegarAsync<IReadOnlyList<EventoPrevia>>(usuarioId, empresaId, Permissao.FolhaContabilizar, ct) is { } negado) return negado;
        if (TiposCalculo.All(t => t.Codigo != tipoCalculo)) return Resultado<IReadOnlyList<EventoPrevia>>.Falha("Tipo de cálculo inválido.");
        competencia = new DateOnly(competencia.Year, competencia.Month, 1);
        var det = detalhar ?? (await repositorio.ObterModoContabilizacaoAsync(empresaId, ct)).DetalharPorFuncionario;
        var montados = await MontarEventosAsync(empresaId, competencia, tipoCalculo, det, ct);
        if (!montados.Sucesso) return Resultado<IReadOnlyList<EventoPrevia>>.Falha(montados.Erros);
        var ativos = (await repositorio.ListarContabilizacoesEventoAsync(empresaId, competencia, tipoCalculo, true, ct)).ToDictionary(a => a.Evento);
        var res = montados.Valor.Select(m =>
        {
            ativos.TryGetValue(m.Codigo, out var ja);
            var situacao = ja is not null ? $"Contabilizado (lançamento nº {ja.NumeroLancamento})" : m.Erros.Count > 0 ? string.Join(" ", m.Erros) : "Pronto para contabilizar";
            return new EventoPrevia(m.Codigo, m.Descricao, m.Historico, m.Valor, string.Join("; ", m.Contas), string.Join(", ", m.Centros), situacao,
                                    ja is null && m.Erros.Count == 0, ja is not null, ja?.NumeroLancamento, m.Linhas);
        }).ToList();
        return Resultado<IReadOnlyList<EventoPrevia>>.Ok(res);
    }

    public async Task<IReadOnlyList<ContabilizacaoEventoRegistro>> ListarContabilizacoesEventoAsync(long empresaId, DateOnly? competencia, long usuarioId, CancellationToken ct = default)
        => await autorizador.PodeVerAsync(usuarioId, empresaId, ct) ? await repositorio.ListarContabilizacoesEventoAsync(empresaId, competencia, null, false, ct) : [];

    // ------------------------------------------------------------------ contabilizar e desfazer

    /// <summary>
    /// Contabiliza os eventos escolhidos (um lançamento por evento). Tudo é validado antes de gravar: se algum evento não puder ser contabilizado, nenhum é.
    /// Em contabilização em lote só registra uma pendência por evento (lançamento 0/0); <paramref name="imediato"/> ignora o modo (tela de lote).
    /// </summary>
    public async Task<Resultado<IReadOnlyList<EventoContabilizado>>> ContabilizarEventosAsync(long empresaId, DateOnly competencia, string tipoCalculo,
        IReadOnlyCollection<string> codigos, long usuarioId, CancellationToken ct = default, bool imediato = false)
    {
        static Resultado<IReadOnlyList<EventoContabilizado>> Falha(params string[] e) => Resultado<IReadOnlyList<EventoContabilizado>>.Falha(e);
        if (await autorizador.NegarAsync<IReadOnlyList<EventoContabilizado>>(usuarioId, empresaId, Permissao.FolhaContabilizar, ct) is { } negado) return negado;
        if (TiposCalculo.All(t => t.Codigo != tipoCalculo)) return Falha("Tipo de cálculo inválido.");
        if (codigos.Count == 0) return Falha("Marque ao menos um evento para contabilizar.");
        competencia = new DateOnly(competencia.Year, competencia.Month, 1);
        var provisao = tipoCalculo == CalcProvisao;
        var (modo, detalhar) = await repositorio.ObterModoContabilizacaoAsync(empresaId, ct);
        if (!imediato && modo != ConfiguracaoFolha.ModoEventos)
            return Falha("A empresa contabiliza a folha de forma resumida. Para contabilizar por eventos, escolha o modo \"Por eventos\" em Cadastros › Parâmetros › Folha.");
        if (await repositorio.ObterContabilizacaoAtivaAsync(empresaId, competencia, provisao ? TipoProvisao : TipoFolhaMensal, ct) is { } resumida)
            return Falha($"A {(provisao ? "provisão" : "folha")} de {competencia:MM/yyyy} já foi contabilizada de forma resumida (lançamento nº {resumida.NumeroLancamento}). Desfaça-a antes.");
        if (!provisao && !(await movimento.ObterCompetenciaAsync(empresaId, competencia, ct)).Fechada)
            return Falha($"Feche a competência {competencia:MM/yyyy} da folha antes de contabilizar.");

        var montados = await MontarEventosAsync(empresaId, competencia, tipoCalculo, detalhar, ct);
        if (!montados.Sucesso) return Resultado<IReadOnlyList<EventoContabilizado>>.Falha(montados.Erros);
        var ativos = (await repositorio.ListarContabilizacoesEventoAsync(empresaId, competencia, tipoCalculo, true, ct)).ToDictionary(a => a.Evento);
        var escolhidos = new List<EventoMontado>();
        var erros = new List<string>();
        foreach (var codigo in codigos.Distinct())
        {
            var m = montados.Valor.FirstOrDefault(x => x.Codigo == codigo);
            if (m is null) erros.Add($"O evento {codigo} não tem valores em {competencia:MM/yyyy}.");
            else if (ativos.TryGetValue(codigo, out var ja)) erros.Add($"Evento {codigo} já contabilizado em {ja.CriadaEm:dd/MM/yyyy} (lançamento nº {ja.NumeroLancamento}). Desfaça para contabilizar de novo.");
            else if (m.Erros.Count > 0) erros.AddRange(m.Erros.Select(e => $"Evento {codigo} – {m.Descricao}: {e}"));
            else escolhidos.Add(m);
        }
        if (erros.Count > 0) return Resultado<IReadOnlyList<EventoContabilizado>>.Falha(erros);

        var res = new List<EventoContabilizado>();
        if (!imediato && pendencias is not null && await pendencias.ModoDaEmpresaAsync(empresaId, ct) == ModoContabilizacao.Lote)
        {
            foreach (var m in escolhidos)
            {
                await pendencias.RegistrarAsync(empresaId, new NovaPendencia(OrigensPendencia.Folha, TipoEvento, $"{competencia:yyyy-MM}|{tipoCalculo}|{m.Codigo}", competencia,
                    $"{m.Historico} ({TiposCalculo.First(t => t.Codigo == tipoCalculo).Nome})", m.Valor), usuarioId, ct);
                res.Add(new EventoContabilizado(m.Codigo, m.Descricao, m.Valor, 0, 0));
            }
            return Resultado<IReadOnlyList<EventoContabilizado>>.Ok(res);
        }
        foreach (var m in escolhidos)
        {
            var r = await lancamentos.EfetivarAsync(m.Proposta!, usuarioId, ct);
            if (!r.Sucesso) return Falha([.. r.Erros.Select(e => $"Evento {m.Codigo}: {e}"), .. (res.Count > 0 ? new[] { $"{res.Count} evento(s) anterior(es) já foram contabilizados." } : [])]);
            try
            {
                await repositorio.RegistrarContabilizacaoEventoAsync(empresaId, competencia, tipoCalculo, m.Codigo, r.Valor.Id, r.Valor.Numero, m.Valor, usuarioId, ct);
            }
            catch (ViolacaoRegraException ex)
            {
                await lancamentos.EstornarAsync(empresaId, r.Valor.Id, m.Proposta!.Data, "contabilização duplicada da folha", usuarioId, ct);
                return Falha(ex.Message);
            }
            res.Add(new EventoContabilizado(m.Codigo, m.Descricao, m.Valor, r.Valor.Id, r.Valor.Numero));
        }
        return Resultado<IReadOnlyList<EventoContabilizado>>.Ok(res);
    }

    /// <summary>Desfaz a contabilização dos eventos escolhidos: estorna o lançamento de cada um (devolve a quantidade desfeita).</summary>
    public async Task<Resultado<int>> DesfazerEventosAsync(long empresaId, DateOnly competencia, string tipoCalculo, IReadOnlyCollection<string> codigos,
                                                           long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<int>(usuarioId, empresaId, Permissao.FolhaContabilizar, ct) is { } negado) return negado;
        competencia = new DateOnly(competencia.Year, competencia.Month, 1);
        if (codigos.Count == 0) return Resultado<int>.Falha("Marque ao menos um evento para desfazer.");
        var ativos = (await repositorio.ListarContabilizacoesEventoAsync(empresaId, competencia, tipoCalculo, true, ct)).ToDictionary(a => a.Evento);
        var faltam = codigos.Where(c => !ativos.ContainsKey(c)).ToList();
        if (faltam.Count > 0) return Resultado<int>.Falha(faltam.Select(c => $"O evento {c} não está contabilizado em {competencia:MM/yyyy}.").ToList());
        var feitos = 0;
        foreach (var codigo in codigos.Distinct())
        {
            var a = ativos[codigo];
            var estorno = await lancamentos.EstornarAsync(empresaId, a.LancamentoId, Ultimo(competencia), $"desfazer contabilização da folha {competencia:MM/yyyy} – evento {codigo}", usuarioId, ct);
            if (!estorno.Sucesso) return Resultado<int>.Falha([.. estorno.Erros.Select(e => $"Evento {codigo}: {e}"), .. (feitos > 0 ? new[] { $"{feitos} evento(s) anterior(es) já foram desfeitos." } : [])]);
            await repositorio.MarcarEventoDesfeitoAsync(empresaId, a.Id, estorno.Valor.Id, usuarioId, ct);
            feitos++;
        }
        return Resultado<int>.Ok(feitos);
    }
}
