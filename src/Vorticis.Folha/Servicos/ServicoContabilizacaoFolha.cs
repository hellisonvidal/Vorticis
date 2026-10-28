using Vorticis.Application.Contabil;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Contabil;
using Vorticis.Domain.Seguranca;
using Vorticis.Folha.Cadastros;
using Vorticis.Folha.Regras;

namespace Vorticis.Folha.Servicos;

public sealed record PreviaContabilizacao(LancamentoProposto Proposta, decimal Total, IReadOnlyList<string> Linhas);

/// <summary>
/// Contabilização da folha e das provisões: regras (rubrica ou código especial -> conta de débito e de crédito, com centro de
/// custo opcional) transformadas em um lançamento contábil pelo <see cref="ServicoLancamento"/> (único ponto de gravação).
/// Desfazer estorna o lançamento. A folha só é contabilizada com a competência fechada.
/// </summary>
public sealed partial class ServicoContabilizacaoFolha(IRepositorioContabilizacaoFolha repositorio, IRepositorioMovimentoFolha movimento, ServicoLancamento lancamentos,
                                               Autorizador autorizador, ServicoPendencias? pendencias = null)
{
    public const string TipoFolhaMensal = "FOLHA";
    public const string TipoProvisao = "PROVISAO";

    /// <summary>Códigos especiais de regra (além dos códigos de rubrica).</summary>
    public static readonly IReadOnlyList<(string Chave, string Descricao)> ChavesEspeciais =
    [
        ("ENC_INSS", "INSS patronal (folha mensal e rescisões)"), ("ENC_RAT", "RAT/FAP (folha mensal e rescisões)"),
        ("ENC_TERCEIROS", "Contribuições a terceiros (folha mensal e rescisões)"), ("ENC_FGTS", "FGTS (folha mensal e rescisões)"),
        ("ENC_FERIAS_INSS", "Encargos previdenciários sobre férias"), ("ENC_FERIAS_FGTS", "FGTS sobre férias"),
        ("ENC_13_INSS", "Encargos previdenciários sobre 13º salário"), ("ENC_13_FGTS", "FGTS sobre 13º salário"),
        ("PROV_FERIAS", "Provisão de férias + 1/3"), ("PROV_13", "Provisão de 13º salário"),
        ("PROV_ENC_FERIAS", "Provisão de encargos previdenciários sobre férias"), ("PROV_FGTS_FERIAS", "Provisão de FGTS sobre férias"),
        ("PROV_ENC_13", "Provisão de encargos previdenciários sobre 13º"), ("PROV_FGTS_13", "Provisão de FGTS sobre 13º"),
    ];

    // ------------------------------------------------------------------ regras

    public async Task<IReadOnlyList<RegraContabil>> ListarRegrasAsync(long empresaId, long usuarioId, CancellationToken ct = default)
        => await autorizador.PodeVerAsync(usuarioId, empresaId, ct) ? await repositorio.ListarRegrasAsync(empresaId, ct) : [];

    public async Task<Resultado> GravarRegraAsync(long empresaId, RegraContabil r, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.FolhaParametrosManter, ct) is { } negado) return Resultado.Falha(negado);
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(r.Chave)) erros.Add("Informe a rubrica ou o código especial da regra.");
        if (string.IsNullOrWhiteSpace(r.ContaDebito) || string.IsNullOrWhiteSpace(r.ContaCredito)) erros.Add("Informe a conta de débito e a de crédito.");
        else if (r.ContaDebito.Trim() == r.ContaCredito.Trim()) erros.Add("As contas de débito e de crédito devem ser diferentes.");
        if (r.LadoCentroCusto is not ('D' or 'C')) erros.Add("O centro de custo vai na partida de débito (D) ou de crédito (C).");
        if (erros.Count > 0) return Resultado.Falha(erros);
        try
        {
            await repositorio.GravarRegraAsync(empresaId, r with { Chave = r.Chave.Trim(), Descricao = string.IsNullOrWhiteSpace(r.Descricao) ? r.Chave.Trim() : r.Descricao.Trim(),
                                                                   ContaDebito = r.ContaDebito.Trim(), ContaCredito = r.ContaCredito.Trim() }, ct);
            return Resultado.Ok();
        }
        catch (ViolacaoRegraException ex) { return Resultado.Falha(ex.Message); }
    }

    public async Task<Resultado> ExcluirRegraAsync(long empresaId, long id, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.FolhaParametrosManter, ct) is { } negado) return Resultado.Falha(negado);
        await repositorio.ExcluirRegraAsync(empresaId, id, ct);
        return Resultado.Ok();
    }

    /// <summary>
    /// Regras sugeridas sobre o plano de contas modelo do Vorticis (Salários a Pagar 2.1.02.001, Salários 4.2.01.001, INSS Patronal 4.2.01.005...).
    /// Só inclui as chaves que ainda não têm regra. <paramref name="comProvisoes"/>: férias e 13º pagos baixam a provisão (e não a despesa).
    /// </summary>
    public static IReadOnlyList<RegraContabil> RegrasSugeridas(bool comProvisoes)
    {
        const string SalariosAPagar = "2.1.02.001", FeriasAPagar = "2.1.02.003", DecimoAPagar = "2.1.02.004", InssARecolher = "2.1.03.001", FgtsARecolher = "2.1.03.002",
                     ProvEncFerias = "2.1.03.003", ProvEnc13 = "2.1.03.004", InssRetido = "2.1.04.010", IrrfARecolher = "2.1.04.008", Adiantamentos = "1.1.02.004",
                     ContasAPagar = "2.1.06.003", DespSalarios = "4.2.01.001", DespFerias = "4.2.01.003", Desp13 = "4.2.01.004", DespInss = "4.2.01.005", DespFgts = "4.2.01.006";
        var regras = new List<RegraContabil>();
        void R(string chave, string desc, string d, string c, bool centro = false, char lado = 'D') => regras.Add(new RegraContabil(0, chave, desc, d, c, centro, lado));
        string Desc(string cod) => RubricasPadrao.Obter(cod).Descricao;

        foreach (var cod in new[] { RubricasPadrao.Salario, RubricasPadrao.HoraExtra50, RubricasPadrao.HoraExtra100, RubricasPadrao.Dsr, RubricasPadrao.AdicionalNoturno,
                                    RubricasPadrao.Insalubridade, RubricasPadrao.Periculosidade, RubricasPadrao.Comissoes, RubricasPadrao.OutrosProventos,
                                    RubricasPadrao.SaldoSalario, RubricasPadrao.AvisoPrevioIndenizado })
            R(cod, Desc(cod), DespSalarios, SalariosAPagar, true);
        R(RubricasPadrao.SalarioFamilia, Desc(RubricasPadrao.SalarioFamilia), InssARecolher, SalariosAPagar);

        var debFerias = comProvisoes ? FeriasAPagar : DespFerias;
        var deb13 = comProvisoes ? DecimoAPagar : Desp13;
        foreach (var cod in new[] { RubricasPadrao.FeriasGozadas, RubricasPadrao.TercoFerias, RubricasPadrao.AbonoPecuniario, RubricasPadrao.TercoAbono,
                                    RubricasPadrao.FeriasVencidas, RubricasPadrao.TercoFeriasVencidas, RubricasPadrao.FeriasProporcionais, RubricasPadrao.TercoFeriasProporcionais })
            R(cod, Desc(cod), debFerias, SalariosAPagar, !comProvisoes);
        foreach (var cod in new[] { RubricasPadrao.Decimo1, RubricasPadrao.Decimo2, RubricasPadrao.DecimoProporcional })
            R(cod, Desc(cod), deb13, SalariosAPagar, !comProvisoes);
        // A 2ª parcela traz o 13º integral e desconta a 1ª: o desconto estorna o que a 1ª parcela já lançou
        R(RubricasPadrao.AdiantamentoDecimo, Desc(RubricasPadrao.AdiantamentoDecimo), SalariosAPagar, deb13, !comProvisoes, 'C');

        R(RubricasPadrao.Inss, Desc(RubricasPadrao.Inss), SalariosAPagar, InssRetido);
        R(RubricasPadrao.InssDecimo, Desc(RubricasPadrao.InssDecimo), SalariosAPagar, InssRetido);
        R(RubricasPadrao.Irrf, Desc(RubricasPadrao.Irrf), SalariosAPagar, IrrfARecolher);
        R(RubricasPadrao.IrrfDecimo, Desc(RubricasPadrao.IrrfDecimo), SalariosAPagar, IrrfARecolher);
        R(RubricasPadrao.Faltas, Desc(RubricasPadrao.Faltas), SalariosAPagar, DespSalarios, true, 'C');
        R(RubricasPadrao.DsrFaltas, Desc(RubricasPadrao.DsrFaltas), SalariosAPagar, DespSalarios, true, 'C');
        R(RubricasPadrao.Adiantamento, Desc(RubricasPadrao.Adiantamento), SalariosAPagar, Adiantamentos);
        foreach (var cod in new[] { RubricasPadrao.ValeTransporte, RubricasPadrao.PensaoAlimenticia, RubricasPadrao.OutrosDescontos })
            R(cod, Desc(cod), SalariosAPagar, ContasAPagar);
        R(RubricasPadrao.AvisoNaoCumprido, Desc(RubricasPadrao.AvisoNaoCumprido), SalariosAPagar, DespSalarios, true, 'C');

        foreach (var chave in new[] { "ENC_INSS", "ENC_RAT", "ENC_TERCEIROS" }) R(chave, ChavesEspeciais.First(x => x.Chave == chave).Descricao, DespInss, InssARecolher, true);
        R("ENC_FGTS", "FGTS (folha mensal e rescisões)", DespFgts, FgtsARecolher, true);
        R("ENC_FERIAS_INSS", "Encargos previdenciários sobre férias", comProvisoes ? ProvEncFerias : DespInss, InssARecolher, !comProvisoes);
        R("ENC_FERIAS_FGTS", "FGTS sobre férias", comProvisoes ? ProvEncFerias : DespFgts, FgtsARecolher, !comProvisoes);
        R("ENC_13_INSS", "Encargos previdenciários sobre 13º salário", comProvisoes ? ProvEnc13 : DespInss, InssARecolher, !comProvisoes);
        R("ENC_13_FGTS", "FGTS sobre 13º salário", comProvisoes ? ProvEnc13 : DespFgts, FgtsARecolher, !comProvisoes);

        R("PROV_FERIAS", "Provisão de férias + 1/3", DespFerias, FeriasAPagar, true);
        R("PROV_13", "Provisão de 13º salário", Desp13, DecimoAPagar, true);
        R("PROV_ENC_FERIAS", "Provisão de encargos previdenciários sobre férias", DespInss, ProvEncFerias, true);
        R("PROV_FGTS_FERIAS", "Provisão de FGTS sobre férias", DespFgts, ProvEncFerias, true);
        R("PROV_ENC_13", "Provisão de encargos previdenciários sobre 13º", DespInss, ProvEnc13, true);
        R("PROV_FGTS_13", "Provisão de FGTS sobre 13º", DespFgts, ProvEnc13, true);
        return regras;
    }

    public async Task<Resultado<int>> GerarRegrasSugeridasAsync(long empresaId, bool comProvisoes, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<int>(usuarioId, empresaId, Permissao.FolhaParametrosManter, ct) is { } negado) return negado;
        var existentes = (await repositorio.ListarRegrasAsync(empresaId, ct)).Select(r => r.Chave).ToHashSet();
        var novas = RegrasSugeridas(comProvisoes).Where(r => !existentes.Contains(r.Chave)).ToList();
        foreach (var r in novas) await repositorio.GravarRegraAsync(empresaId, r, ct);
        return Resultado<int>.Ok(novas.Count);
    }

    // ------------------------------------------------------------------ montagem do lançamento

    private sealed class Acumulador
    {
        public readonly Dictionary<(string Chave, string? Centro), decimal> Valores = [];
        public readonly Dictionary<string, string> Descricoes = [];

        public void Somar(string chave, string descricao, string? centro, decimal valor)
        {
            if (valor == 0) return;
            Valores[(chave, centro)] = Valores.GetValueOrDefault((chave, centro)) + valor;
            Descricoes.TryAdd(chave, descricao);
        }
    }

    private static DateOnly Ultimo(DateOnly competencia) => new DateOnly(competencia.Year, competencia.Month, 1).AddMonths(1).AddDays(-1);

    private Resultado<PreviaContabilizacao> Montar(long empresaId, DateOnly competencia, string titulo, Acumulador acc, IReadOnlyList<RegraContabil> regras)
    {
        if (acc.Valores.Count == 0) return Resultado<PreviaContabilizacao>.Falha($"Não há valores de {titulo} em {competencia:MM/yyyy} para contabilizar.");
        var porChave = regras.ToDictionary(r => r.Chave);
        var faltando = acc.Valores.Keys.Select(k => k.Chave).Distinct().Where(c => !porChave.ContainsKey(c)).ToList();
        if (faltando.Count > 0)
            return Resultado<PreviaContabilizacao>.Falha(faltando.Select(c =>
                $"Sem regra de contabilização para {c} – {acc.Descricoes[c]} ({Formato.Moeda(acc.Valores.Where(v => v.Key.Chave == c).Sum(v => v.Value))}). Cadastre-a em Cálculos › Folha › Contabilização."));

        var partidas = new List<PartidaProposta>();
        var linhas = new List<string>();
        foreach (var ((chave, centro), valor) in acc.Valores.OrderBy(v => v.Key.Chave, StringComparer.Ordinal).ThenBy(v => v.Key.Centro, StringComparer.Ordinal))
        {
            var regra = porChave[chave];
            if (valor <= 0) return Resultado<PreviaContabilizacao>.Falha($"Valor negativo ou zerado em {chave} – {acc.Descricoes[chave]}: confira os cálculos.");
            var historico = $"Folha {competencia:MM/yyyy} – {acc.Descricoes[chave]}" + (centro is null ? "" : $" (C.C. {centro})");
            var cc = regra.UsaCentroCusto ? centro : null;
            partidas.Add(new PartidaProposta(regra.ContaDebito, IndicadorDC.D, valor, historico, regra.LadoCentroCusto == 'D' ? cc : null));
            partidas.Add(new PartidaProposta(regra.ContaCredito, IndicadorDC.C, valor, historico, regra.LadoCentroCusto == 'C' ? cc : null));
            linhas.Add($"{chave} {acc.Descricoes[chave]}{(centro is null ? "" : $" [{centro}]")}: D {regra.ContaDebito} / C {regra.ContaCredito} = {Formato.Moeda(valor)}");
        }
        var proposta = new LancamentoProposto(empresaId, Ultimo(competencia), OrigemLancamento.Folha, partidas);
        return Resultado<PreviaContabilizacao>.Ok(new PreviaContabilizacao(proposta, partidas.Where(p => p.DC == IndicadorDC.D).Sum(p => p.Valor), linhas));
    }

    /// <summary>Encargos patronais e FGTS de um cálculo, com o código especial de regra de cada um.</summary>
    internal static IEnumerable<(string Chave, string Descricao, decimal Valor)> ComponentesEncargos(CalculoGravado c)
    {
        var inss = c.EncargosPrevidenciarios;
        switch (c.Tipo)
        {
            case TipoFolha.Ferias:
                yield return ("ENC_FERIAS_INSS", "Encargos previdenciários sobre férias", inss);
                yield return ("ENC_FERIAS_FGTS", "FGTS sobre férias", c.Fgts);
                break;
            case TipoFolha.Decimo1 or TipoFolha.Decimo2:
                yield return ("ENC_13_INSS", "Encargos previdenciários sobre 13º salário", inss);
                yield return ("ENC_13_FGTS", "FGTS sobre 13º salário", c.Fgts);
                break;
            default:
                yield return ("ENC_INSS", "INSS patronal", c.EncInssPatronal);
                yield return ("ENC_RAT", "RAT/FAP", c.EncRat);
                yield return ("ENC_TERCEIROS", "Contribuições a terceiros", c.EncTerceiros);
                yield return ("ENC_FGTS", "FGTS", c.Fgts);
                break;
        }
    }

    internal static IEnumerable<(string Chave, string Descricao, decimal Valor)> ComponentesProvisao(ProvisaoGravada p)
    {
        yield return ("PROV_FERIAS", "Provisão de férias + 1/3", p.Ferias);
        yield return ("PROV_13", "Provisão de 13º salário", p.Decimo);
        yield return ("PROV_ENC_FERIAS", "Provisão de encargos previdenciários sobre férias", p.EncFeriasInss);
        yield return ("PROV_FGTS_FERIAS", "Provisão de FGTS sobre férias", p.EncFeriasFgts);
        yield return ("PROV_ENC_13", "Provisão de encargos previdenciários sobre 13º", p.EncDecimoInss);
        yield return ("PROV_FGTS_13", "Provisão de FGTS sobre 13º", p.EncDecimoFgts);
    }

    private async Task<Resultado<PreviaContabilizacao>> MontarFolhaAsync(long empresaId, DateOnly competencia, CancellationToken ct)
    {
        var acc = new Acumulador();
        foreach (var c in await movimento.ListarCalculosAsync(empresaId, competencia, null, true, ct))
        {
            foreach (var i in c.Itens.Where(i => i.Tipo is 'P' or 'D')) acc.Somar(i.Codigo, i.Descricao, c.CentroCustoCodigo, i.Valor);
            foreach (var (chave, descricao, valor) in ComponentesEncargos(c)) acc.Somar(chave, descricao, c.CentroCustoCodigo, valor);
        }
        return Montar(empresaId, competencia, "folha", acc, await repositorio.ListarRegrasAsync(empresaId, ct));
    }

    private async Task<Resultado<PreviaContabilizacao>> MontarProvisaoAsync(long empresaId, DateOnly competencia, CancellationToken ct)
    {
        var acc = new Acumulador();
        foreach (var p in await repositorio.ListarProvisoesAsync(empresaId, competencia, ct))
        {
            foreach (var (chave, descricao, valor) in ComponentesProvisao(p)) acc.Somar(chave, descricao, p.CentroCustoCodigo, valor);
        }
        return Montar(empresaId, competencia, "provisões", acc, await repositorio.ListarRegrasAsync(empresaId, ct));
    }

    public async Task<Resultado<PreviaContabilizacao>> PreviaAsync(long empresaId, DateOnly competencia, string tipo, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<PreviaContabilizacao>(usuarioId, empresaId, Permissao.FolhaContabilizar, ct) is { } negado) return negado;
        competencia = new DateOnly(competencia.Year, competencia.Month, 1);
        return tipo == TipoProvisao ? await MontarProvisaoAsync(empresaId, competencia, ct) : await MontarFolhaAsync(empresaId, competencia, ct);
    }

    // ------------------------------------------------------------------ contabilizar e desfazer

    /// <summary>
    /// Contabiliza a folha (ou as provisões) da competência. Com a empresa em contabilização em lote, só registra a pendência
    /// (devolve o lançamento 0/0, <see cref="LancamentoEfetivado"/> sem número); <paramref name="imediato"/> ignora o modo (usado pela tela de lote).
    /// </summary>
    public async Task<Resultado<LancamentoEfetivado>> ContabilizarAsync(long empresaId, DateOnly competencia, string tipo, long usuarioId, CancellationToken ct = default,
                                                                         bool imediato = false)
    {
        if (await autorizador.NegarAsync<LancamentoEfetivado>(usuarioId, empresaId, Permissao.FolhaContabilizar, ct) is { } negado) return negado;
        if (tipo is not (TipoFolhaMensal or TipoProvisao)) return Resultado<LancamentoEfetivado>.Falha("Tipo de contabilização inválido.");
        competencia = new DateOnly(competencia.Year, competencia.Month, 1);
        if (await repositorio.ObterContabilizacaoAtivaAsync(empresaId, competencia, tipo, ct) is { } ja)
            return Resultado<LancamentoEfetivado>.Falha($"Já contabilizado em {ja.CriadaEm:dd/MM/yyyy} (lançamento nº {ja.NumeroLancamento}). Desfaça para contabilizar de novo.");
        if (await repositorio.ExisteContabilizacaoEventoAtivaAsync(empresaId, competencia, tipo == TipoProvisao, ct))
            return Resultado<LancamentoEfetivado>.Falha("Há eventos desta competência contabilizados em Contabilização por eventos. Desfaça-os antes de contabilizar de forma resumida.");
        if (!imediato && (await repositorio.ObterModoContabilizacaoAsync(empresaId, ct)).Modo == ConfiguracaoFolha.ModoEventos)
            return Resultado<LancamentoEfetivado>.Falha("A empresa contabiliza a folha por eventos (Cadastros › Parâmetros › Folha). Use a tela Contabilização por eventos.");
        if (tipo == TipoFolhaMensal && !(await movimento.ObterCompetenciaAsync(empresaId, competencia, ct)).Fechada)
            return Resultado<LancamentoEfetivado>.Falha($"Feche a competência {competencia:MM/yyyy} da folha antes de contabilizar.");

        var previa = tipo == TipoProvisao ? await MontarProvisaoAsync(empresaId, competencia, ct) : await MontarFolhaAsync(empresaId, competencia, ct);
        if (!previa.Sucesso) return Resultado<LancamentoEfetivado>.Falha(previa.Erros);

        if (!imediato && pendencias is not null && await pendencias.ModoDaEmpresaAsync(empresaId, ct) == ModoContabilizacao.Lote)
        {
            await pendencias.RegistrarAsync(empresaId, new NovaPendencia(OrigensPendencia.Folha, tipo, competencia.ToString("yyyy-MM"), competencia,
                $"{(tipo == TipoProvisao ? "Provisões da folha" : "Folha de pagamento")} {competencia:MM/yyyy}", previa.Valor.Total), usuarioId, ct);
            return Resultado<LancamentoEfetivado>.Ok(new LancamentoEfetivado(0, 0));
        }
        var r = await lancamentos.EfetivarAsync(previa.Valor.Proposta, usuarioId, ct);
        if (!r.Sucesso) return r;
        try
        {
            await repositorio.RegistrarContabilizacaoAsync(empresaId, competencia, tipo, r.Valor.Id, r.Valor.Numero, previa.Valor.Total, usuarioId, ct);
        }
        catch (ViolacaoRegraException ex)
        {
            // Outra contabilização concorrente: desfaz o lançamento recém-criado para não duplicar
            await lancamentos.EstornarAsync(empresaId, r.Valor.Id, previa.Valor.Proposta.Data, "contabilização duplicada da folha", usuarioId, ct);
            return Resultado<LancamentoEfetivado>.Falha(ex.Message);
        }
        return r;
    }

    public async Task<Resultado> DesfazerAsync(long empresaId, DateOnly competencia, string tipo, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.FolhaContabilizar, ct) is { } negado) return Resultado.Falha(negado);
        competencia = new DateOnly(competencia.Year, competencia.Month, 1);
        var c = await repositorio.ObterContabilizacaoAtivaAsync(empresaId, competencia, tipo, ct);
        if (c is null) return Resultado.Falha($"Não há contabilização ativa de {competencia:MM/yyyy}.");
        var estorno = await lancamentos.EstornarAsync(empresaId, c.LancamentoId, Ultimo(competencia),
            $"desfazer contabilização da folha {competencia:MM/yyyy}", usuarioId, ct);
        if (!estorno.Sucesso) return Resultado.Falha(estorno.Erros);
        await repositorio.MarcarDesfeitaAsync(empresaId, c.Id, estorno.Valor.Id, usuarioId, ct);
        return Resultado.Ok();
    }

    public async Task<IReadOnlyList<ContabilizacaoRegistro>> ListarAsync(long empresaId, DateOnly? competencia, long usuarioId, CancellationToken ct = default)
        => await autorizador.PodeVerAsync(usuarioId, empresaId, ct) ? await repositorio.ListarContabilizacoesAsync(empresaId, competencia, ct) : [];

    // ------------------------------------------------------------------ contabilização em lote

    /// <summary>Processador da origem FOLHA (folha e provisões pendentes da Contabilização em lote): as mesmas regras da contabilização on-line.</summary>
    public IProcessadorPendencia ProcessadorPendencias => new ProcessadorFolha(this);

    private sealed class ProcessadorFolha(ServicoContabilizacaoFolha s) : IProcessadorPendencia
    {
        public string Origem => OrigensPendencia.Folha;

        public async Task<IReadOnlyList<string>> PreviaAsync(long empresaId, PendenciaContabil p, long usuarioId, CancellationToken ct)
        {
            if (p.Tipo == TipoEvento)
            {
                var (tipoCalc, codigo) = ReferenciaEvento(p);
                var e = await s.PreviaEventosAsync(empresaId, p.Periodo, tipoCalc, usuarioId, ct);
                if (!e.Sucesso) return e.Erros;
                return e.Valor.FirstOrDefault(x => x.Codigo == codigo) is { } ev ? [.. ev.Linhas] : [$"Evento {codigo} sem valores em {p.Periodo:MM/yyyy}."];
            }
            var r = await s.PreviaAsync(empresaId, p.Periodo, p.Tipo, usuarioId, ct);
            return r.Sucesso ? r.Valor.Linhas : r.Erros;
        }

        public async Task<ResultadoProcessamento> ContabilizarAsync(long empresaId, PendenciaContabil p, long usuarioId, CancellationToken ct)
        {
            if (p.Tipo == TipoEvento)
            {
                var (tipoCalc, codigo) = ReferenciaEvento(p);
                var e = await s.ContabilizarEventosAsync(empresaId, p.Periodo, tipoCalc, [codigo], usuarioId, ct, imediato: true);
                if (e.Sucesso) return ResultadoProcessamento.Ok(e.Valor[0].LancamentoId);
                return e.Erros.Any(x => x.Contains("já contabilizado", StringComparison.Ordinal)) ? ResultadoProcessamento.Cancelada("já contabilizada pela folha.") : ResultadoProcessamento.Falha(e.Erros);
            }
            var r = await s.ContabilizarAsync(empresaId, p.Periodo, p.Tipo, usuarioId, ct, imediato: true);
            if (r.Sucesso) return ResultadoProcessamento.Ok(r.Valor.Id);
            return r.Erros.Any(e => e.StartsWith("Já contabilizado", StringComparison.Ordinal)) ? ResultadoProcessamento.Cancelada("já contabilizada pela folha.") : ResultadoProcessamento.Falha(r.Erros);
        }

        public async Task<Resultado> DesfazerAsync(long empresaId, PendenciaContabil p, long usuarioId, CancellationToken ct)
        {
            if (p.Tipo != TipoEvento) return await s.DesfazerAsync(empresaId, p.Periodo, p.Tipo, usuarioId, ct);
            var (tipoCalc, codigo) = ReferenciaEvento(p);
            var r = await s.DesfazerEventosAsync(empresaId, p.Periodo, tipoCalc, [codigo], usuarioId, ct);
            return r.Sucesso ? Resultado.Ok() : Resultado.Falha(r.Erros);
        }
    }
}
