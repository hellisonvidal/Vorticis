using Vorticis.Application.Relatorios;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Folha.Cadastros;
using Vorticis.Folha.Regras;

namespace Vorticis.Folha.Servicos;

/// <summary>
/// Relatórios da folha: holerite (recibo de pagamento), recibo de férias e de 13º, termo de rescisão simplificado (PDF),
/// resumo da folha, encargos e provisões (tabelas exportáveis para PDF, Excel e CSV pela tela).
/// </summary>
public sealed class ServicoRelatoriosFolha(IRepositorioCadastrosFolha cadastros, IRepositorioMovimentoFolha movimento, IRepositorioContabilizacaoFolha contabilizacao,
                                           IGeradorPdf gerador, Autorizador autorizador)
{
    private async Task<string?> NegarAsync(long empresaId, long usuarioId, CancellationToken ct)
        => await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.FolhaRelatorios, ct);

    private async Task<IReadOnlyList<string>> CabecalhoAsync(long empresaId, string linha2, CancellationToken ct)
    {
        var e = await cadastros.ObterEmpresaAsync(empresaId, ct);
        return [e is null ? "" : $"{e.RazaoSocial} – CNPJ {ServicoLivros.FormatarCnpj(e.Cnpj)}", linha2];
    }

    private static string Competencia(DateOnly c) => c.ToString("MM/yyyy");

    // ------------------------------------------------------------------ recibos (holerite, férias, 13º, rescisão)

    private static IReadOnlyList<Coluna> ColunasRecibo { get; } =
    [
        new("Cód.", 1.4), new("Descrição", 7.2), new("Referência", 2.2, Alinhamento.Direita), new("Proventos", 3.0, Alinhamento.Direita), new("Descontos", 3.0, Alinhamento.Direita),
    ];

    private static string Ref(ItemGravado i) => i.Referencia == 0 ? "" : i.Referencia.ToString("0.##", Formato.PtBr);

    private static IEnumerable<Bloco> BlocosRecibo(CalculoGravado c, Funcionario f, string titulo, string? observacao = null)
    {
        yield return new Bloco(Texto: $"{titulo}\nFuncionário: {c.Matricula} – {c.FuncionarioNome}   CPF {Domain.Cadastros.Cpf.Formatar(c.Cpf)}\n"
            + $"Cargo: {f.CargoNome ?? "—"}   Departamento: {f.DepartamentoNome ?? "—"}   Admissão: {f.Admissao:dd/MM/yyyy}"
            + $"{(f.Desligamento is { } d ? $"   Desligamento: {d:dd/MM/yyyy}" : "")}\n"
            + $"Competência: {Competencia(c.Competencia)}{(c.DataPagamento is { } p ? $"   Pagamento: {p:dd/MM/yyyy}" : "")}   {c.Tipo.Descricao()}");
        var linhas = c.Itens.Where(i => i.Tipo is 'P' or 'D').Select(i => new LinhaTabela(
            [i.Codigo, i.Descricao, Ref(i), i.Tipo == 'P' ? Formato.Moeda(i.Valor) : "", i.Tipo == 'D' ? Formato.Moeda(i.Valor) : ""])).ToList();
        linhas.Add(new LinhaTabela(["", "Totais", "", Formato.Moeda(c.TotalProventos), Formato.Moeda(c.TotalDescontos)], Negrito: true));
        linhas.Add(new LinhaTabela(["", "Líquido a receber", "", "", Formato.Moeda(c.Liquido)], Negrito: true));
        yield return new Bloco(Colunas: ColunasRecibo, Linhas: linhas);
        yield return new Bloco(Texto: $"Base INSS {Formato.Moeda(c.BaseInss)}   Base IRRF {Formato.Moeda(c.BaseIrrf)}   Base FGTS {Formato.Moeda(c.BaseFgts)}   FGTS do mês {Formato.Moeda(c.Fgts)}"
            + (c.SalarioFamilia > 0 ? $"   Salário-família {Formato.Moeda(c.SalarioFamilia)}" : "")
            + (f.Banco is null ? "" : $"\nCrédito em conta: banco {f.Banco} ag. {f.Agencia} conta {f.Conta}"));
        if (observacao is not null) yield return new Bloco(Texto: observacao);
    }

    private async Task<(CalculoGravado, Funcionario)?> CarregarAsync(long empresaId, long calculoId, CancellationToken ct)
    {
        var c = await movimento.ObterCalculoAsync(empresaId, calculoId, ct);
        if (c is null) return null;
        var f = await cadastros.ObterFuncionarioAsync(empresaId, c.FuncionarioId, ct);
        return f is null ? null : (c, f);
    }

    private static string TituloRecibo(TipoFolha t) => t switch
    {
        TipoFolha.Mensal => "RECIBO DE PAGAMENTO DE SALÁRIO",
        TipoFolha.Ferias => "RECIBO DE FÉRIAS",
        TipoFolha.Decimo1 => "RECIBO DE 13º SALÁRIO – 1ª PARCELA",
        TipoFolha.Decimo2 => "RECIBO DE 13º SALÁRIO – 2ª PARCELA",
        _ => "TERMO DE RESCISÃO DO CONTRATO DE TRABALHO (SIMPLIFICADO)",
    };

    private const string TextoRecebi = "Recebi o valor líquido discriminado neste recibo, dando plena quitação das parcelas nele constantes.";

    /// <summary>Holerite/recibo de um cálculo (mensal, férias ou 13º) em PDF.</summary>
    public async Task<Resultado<ArquivoGerado>> ReciboAsync(long empresaId, long calculoId, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(empresaId, usuarioId, ct) is { } negado) return Resultado<ArquivoGerado>.Falha(negado);
        var par = await CarregarAsync(empresaId, calculoId, ct);
        if (par is null) return Resultado<ArquivoGerado>.Falha("Cálculo não encontrado.");
        var (c, f) = par.Value;
        if (c.Tipo == TipoFolha.Rescisao) return await TrctAsync(empresaId, c, f, ct);
        var doc = new DocumentoRelatorio(TituloRecibo(c.Tipo), await CabecalhoAsync(empresaId, $"Competência {Competencia(c.Competencia)}", ct),
            BlocosRecibo(c, f, TituloRecibo(c.Tipo), c.Tipo == TipoFolha.Ferias ? await ObservacaoFeriasAsync(empresaId, c, ct) : null).Append(new Bloco(Texto: TextoRecebi)).ToList(),
            Assinaturas: [$"{c.FuncionarioNome}\nCPF {Domain.Cadastros.Cpf.Formatar(c.Cpf)}"]);
        return Resultado<ArquivoGerado>.Ok(gerador.Gerar(doc));
    }

    private async Task<string?> ObservacaoFeriasAsync(long empresaId, CalculoGravado c, CancellationToken ct)
    {
        if (c.OrigemId is not { } id || await movimento.ObterFeriasAsync(empresaId, id, ct) is not { } f) return null;
        return $"Período aquisitivo: {f.AquisitivoInicio:dd/MM/yyyy} a {f.AquisitivoFim:dd/MM/yyyy}. Gozo: {f.GozoInicio:dd/MM/yyyy} a {f.GozoInicio.AddDays(f.DiasGozo - 1):dd/MM/yyyy} ({f.DiasGozo} dias)"
            + (f.AbonoDias > 0 ? $" e abono pecuniário de {f.AbonoDias} dias" : "") + ". Retorno ao trabalho em " + f.GozoInicio.AddDays(f.DiasGozo).ToString("dd/MM/yyyy") + ".";
    }

    private async Task<Resultado<ArquivoGerado>> TrctAsync(long empresaId, CalculoGravado c, Funcionario f, CancellationToken ct)
    {
        var r = c.OrigemId is { } id ? await movimento.ObterRescisaoAsync(empresaId, id, ct) : null;
        var multa = c.Itens.Where(i => i.Codigo == RubricasPadrao.MultaFgts).Sum(i => i.Valor);
        var info = r is null ? "" : $"Motivo: {r.Motivo.Descricao()}\nData do desligamento: {r.Desligamento:dd/MM/yyyy}   Aviso prévio: {r.Aviso.ToString().ToLowerInvariant()}"
                                    + $"{(r.DiasAviso > 0 ? $" ({r.DiasAviso} dias)" : "")}   Pagamento até: {r.DataPagamento:dd/MM/yyyy}";
        var blocos = BlocosRecibo(c, f, TituloRecibo(c.Tipo), info).ToList();
        if (multa > 0)
            blocos.Add(new Bloco(Texto: $"Multa rescisória do FGTS: {Formato.Moeda(multa)} (depositada na conta vinculada, fora do valor líquido acima)."));
        blocos.Add(new Bloco(Texto: "Documento simplificado para conferência e recibo das verbas; não substitui o Termo de Rescisão (TRCT) e o Termo de Quitação oficiais, " +
                                    "nem a homologação/assistência quando exigida. " + TextoRecebi));
        var doc = new DocumentoRelatorio(TituloRecibo(c.Tipo), await CabecalhoAsync(empresaId, $"Rescisão – {Competencia(c.Competencia)}", ct), blocos,
            Assinaturas: ["Empregador", $"{c.FuncionarioNome}\nCPF {Domain.Cadastros.Cpf.Formatar(c.Cpf)}"]);
        return Resultado<ArquivoGerado>.Ok(gerador.Gerar(doc));
    }

    /// <summary>Holerites de toda a competência (um por página) em um só PDF.</summary>
    /// <summary>Holerites da competência: de todos os funcionários ou só de um (<paramref name="funcionarioId"/>).</summary>
    public async Task<Resultado<ArquivoGerado>> HoleritesAsync(long empresaId, DateOnly competencia, TipoFolha tipo, long usuarioId, CancellationToken ct = default,
                                                               long? funcionarioId = null)
    {
        if (await NegarAsync(empresaId, usuarioId, ct) is { } negado) return Resultado<ArquivoGerado>.Falha(negado);
        competencia = new DateOnly(competencia.Year, competencia.Month, 1);
        var calculos = (await movimento.ListarCalculosAsync(empresaId, competencia, tipo, true, ct))
            .Where(c => funcionarioId is null || c.FuncionarioId == funcionarioId).ToList();
        if (calculos.Count == 0)
            return Resultado<ArquivoGerado>.Falha(funcionarioId is null
                ? $"Não há {tipo.Descricao().ToLowerInvariant()} calculada em {Competencia(competencia)}."
                : $"O funcionário escolhido não tem {tipo.Descricao().ToLowerInvariant()} calculada em {Competencia(competencia)}.");
        var funcionarios = (await cadastros.ListarFuncionariosAsync(empresaId, ct)).ToDictionary(x => x.Id);
        var blocos = new List<Bloco>();
        var primeiro = true;
        foreach (var c in calculos)
        {
            var bs = BlocosRecibo(c, funcionarios[c.FuncionarioId], TituloRecibo(c.Tipo)).Append(new Bloco(Texto: TextoRecebi + "\n\n_______________________________________\n" + c.FuncionarioNome)).ToList();
            blocos.Add(bs[0] with { NovaPagina = !primeiro });
            blocos.AddRange(bs.Skip(1));
            primeiro = false;
        }
        return Resultado<ArquivoGerado>.Ok(gerador.Gerar(new DocumentoRelatorio($"Holerites – {Competencia(competencia)}",
            await CabecalhoAsync(empresaId, $"Competência {Competencia(competencia)}", ct), blocos)));
    }

    // ------------------------------------------------------------------ conferência do INSS (DCTFWeb)

    /// <summary>
    /// Conferência das contribuições previdenciárias da competência para a DCTFWeb: descontado dos segurados, patronal, RAT x FAP e terceiros,
    /// menos o salário-família, separando a folha do mês (com férias e rescisões) e o 13º salário (declaração anual, em dezembro).
    /// </summary>
    public async Task<Resultado<IReadOnlyList<TabelaRelatorio>>> ConferenciaInssAsync(long empresaId, DateOnly competencia, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(empresaId, usuarioId, ct) is { } negado) return Resultado<IReadOnlyList<TabelaRelatorio>>.Falha(negado);
        competencia = new DateOnly(competencia.Year, competencia.Month, 1);
        var calculos = await movimento.ListarCalculosAsync(empresaId, competencia, null, false, ct);
        var grupos = new (string Nome, Func<CalculoGravado, bool> Filtro)[]
        {
            ("Folha do mês (salários, férias e rescisões)", c => c.Tipo is not (TipoFolha.Decimo1 or TipoFolha.Decimo2)),
            ("13º salário (DCTFWeb anual)", c => c.Tipo is TipoFolha.Decimo1 or TipoFolha.Decimo2),
        };
        var linhas = new List<LinhaRelatorio>();
        foreach (var (nome, filtro) in grupos)
        {
            var cs = calculos.Where(filtro).ToList();
            if (cs.Count == 0) continue;
            var seg = cs.Sum(c => c.Inss); var pat = cs.Sum(c => c.EncInssPatronal); var rat = cs.Sum(c => c.EncRat); var ter = cs.Sum(c => c.EncTerceiros);
            var sf = cs.Sum(c => c.SalarioFamilia);
            linhas.Add(new LinhaRelatorio([nome, cs.Select(c => c.FuncionarioId).Distinct().Count(), cs.Sum(c => c.BaseInss), seg, pat, rat, ter, sf, seg + pat + rat + ter - sf]));
        }
        if (linhas.Count > 1)
            linhas.Add(new LinhaRelatorio(["TOTAL", null, linhas.Sum(l => (decimal)l.Valores[2]!), linhas.Sum(l => (decimal)l.Valores[3]!), linhas.Sum(l => (decimal)l.Valores[4]!),
                                           linhas.Sum(l => (decimal)l.Valores[5]!), linhas.Sum(l => (decimal)l.Valores[6]!), linhas.Sum(l => (decimal)l.Valores[7]!),
                                           linhas.Sum(l => (decimal)l.Valores[8]!)], Destaque: true));
        var resumo = new TabelaRelatorio($"Contribuições previdenciárias – competência {Competencia(competencia)}",
            [new("Apuração", TipoColuna.Texto, 6), new("Funcionários", TipoColuna.Inteiro, 2), new("Base de cálculo", V), new("Descontado dos segurados", V),
             new("Patronal", V), new("RAT x FAP", V), new("Terceiros", V), new("(–) Salário-família", V), new("A declarar", V)],
            linhas, "Confira com o total da DCTFWeb gerada pelo eSocial (eventos S-1200, S-2299 e S-1299) antes de emitir o DARF, que vence no dia 20 do mês seguinte. "
                    + "Pró-labore, contribuintes individuais e retenções sobre notas (EFD-Reinf) entram na DCTFWeb por fora desta folha.");
        var porFunc = calculos.GroupBy(c => (c.Matricula, c.FuncionarioNome)).OrderBy(g => g.Key.FuncionarioNome)
            .Select(g => new LinhaRelatorio([g.Key.Matricula, g.Key.FuncionarioNome, string.Join(", ", g.Select(c => c.Tipo.Descricao()).Distinct()),
                                             g.Sum(c => c.BaseInss), g.Sum(c => c.Inss), g.Sum(c => c.EncargosPrevidenciarios), g.Sum(c => c.SalarioFamilia)])).ToList();
        var detalhe = new TabelaRelatorio("Por funcionário",
            [new("Matrícula", TipoColuna.Texto, 1.8), new("Funcionário"), new("Cálculos", TipoColuna.Texto, 4), new("Base INSS", V), new("Descontado", V),
             new("Patronal + RAT + terceiros", V), new("Salário-família", V)], porFunc);
        return Resultado<IReadOnlyList<TabelaRelatorio>>.Ok([resumo, detalhe]);
    }

    // ------------------------------------------------------------------ controles de pessoal

    /// <summary>Contratos a termo e férias que vencem nos próximos <paramref name="dias"/> dias (e os já vencidos).</summary>
    public async Task<Resultado<TabelaRelatorio>> VencimentosAsync(long empresaId, DateOnly hoje, int dias, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(empresaId, usuarioId, ct) is { } negado) return Resultado<TabelaRelatorio>.Falha(negado);
        if (dias is < 0 or > 730) return Resultado<TabelaRelatorio>.Falha("Informe de 0 a 730 dias.");
        var lista = ControlesPessoal.Vencimentos(await cadastros.ListarFuncionariosAsync(empresaId, ct), await movimento.ListarFeriasAsync(empresaId, null, ct), hoje, dias);
        var linhas = lista.Select(v => new LinhaRelatorio([v.Matricula, v.Nome, v.Tipo, v.Limite, v.Situacao, v.Observacao], Destaque: v.Dias < 0)).ToList();
        return Resultado<TabelaRelatorio>.Ok(new TabelaRelatorio("Vencimentos de contratos e férias",
            [new("Matrícula", TipoColuna.Texto, 1.8), new("Funcionário", TipoColuna.Texto, 5), new("Prazo", TipoColuna.Texto, 4), new("Data limite", TipoColuna.Data, 2.3),
             new("Situação", TipoColuna.Texto, 3), new("O que fazer")],
            linhas, $"{lista.Count} prazo(s) até {hoje.AddDays(dias):dd/MM/yyyy}; em destaque, os vencidos."));
    }

    /// <summary>Relação de admitidos e desligados do período, com a rotatividade.</summary>
    public async Task<Resultado<IReadOnlyList<TabelaRelatorio>>> AdmitidosDesligadosAsync(long empresaId, DateOnly inicio, DateOnly fim, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(empresaId, usuarioId, ct) is { } negado) return Resultado<IReadOnlyList<TabelaRelatorio>>.Falha(negado);
        if (fim < inicio) return Resultado<IReadOnlyList<TabelaRelatorio>>.Falha("Data final anterior à inicial.");
        var (adm, desl, quadro, rot) = ControlesPessoal.AdmitidosDesligados((await cadastros.ListarFuncionariosAsync(empresaId, ct)).ToList(),
                                                                            await movimento.ListarRescisoesAsync(empresaId, ct), inicio, fim);
        var admitidos = new TabelaRelatorio($"Admitidos ({adm.Count})",
            [new("Admissão", TipoColuna.Data, 2.3), new("Matrícula", TipoColuna.Texto, 1.8), new("Funcionário"), new("Cargo", TipoColuna.Texto, 4), new("Salário", V)],
            [.. adm.Select(a => new LinhaRelatorio([a.Data, a.Matricula, a.Nome, a.Cargo ?? "", a.Salario])),
             new LinhaRelatorio(["", "", "TOTAL", "", adm.Sum(a => a.Salario)], Destaque: true)]);
        var desligados = new TabelaRelatorio($"Desligados ({desl.Count})",
            [new("Desligamento", TipoColuna.Data, 2.3), new("Matrícula", TipoColuna.Texto, 1.8), new("Funcionário"), new("Motivo", TipoColuna.Texto, 5), new("Salário", V),
             new("Líquido da rescisão", V)],
            [.. desl.Select(d => new LinhaRelatorio([d.Data, d.Matricula, d.Nome, d.Motivo ?? "", d.Salario, d.LiquidoRescisao])),
             new LinhaRelatorio(["", "", "TOTAL", "", desl.Sum(d => d.Salario), desl.Sum(d => d.LiquidoRescisao ?? 0)], Destaque: true)],
            $"Quadro no início do período: {quadro} funcionário(s). Rotatividade: {Formato.Num(rot)}% (média de admitidos e desligados dividida pelo quadro inicial).");
        return Resultado<IReadOnlyList<TabelaRelatorio>>.Ok([admitidos, desligados]);
    }

    // ------------------------------------------------------------------ informe de rendimentos

    /// <summary>Apura os informes de rendimentos do ano-calendário (pagamentos de janeiro a dezembro, regime de caixa).</summary>
    public async Task<Resultado<IReadOnlyList<InformeRendimentos>>> InformesRendimentosAsync(long empresaId, int ano, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(empresaId, usuarioId, ct) is { } negado) return Resultado<IReadOnlyList<InformeRendimentos>>.Falha(negado);
        if (ano is < 2000 or > 2100) return Resultado<IReadOnlyList<InformeRendimentos>>.Falha("Informe o ano-calendário.");
        var calculos = new List<CalculoGravado>();
        for (var c = new DateOnly(ano - 1, 12, 1); c <= new DateOnly(ano, 12, 1); c = c.AddMonths(1))
            calculos.AddRange(await movimento.ListarCalculosAsync(empresaId, c, null, true, ct));
        var incidencias = (await cadastros.ListarRubricasAsync(empresaId, ct)).ToDictionary(r => r.Codigo, r => r.IncIrrf);
        return Resultado<IReadOnlyList<InformeRendimentos>>.Ok(ApuracaoInformeRendimentos.Apurar(calculos, incidencias, ano));
    }

    /// <summary>Tabela da tela: um funcionário por linha com os valores dos quadros 3, 4 e 5 do comprovante.</summary>
    public static TabelaRelatorio TabelaInformes(IReadOnlyList<InformeRendimentos> informes, int ano)
    {
        var linhas = informes.Select(i => new LinhaRelatorio([i.Matricula, i.Nome, Domain.Cadastros.Cpf.Formatar(i.Cpf), i.Rendimentos, i.Previdencia, i.Pensao, i.Irrf,
                                                             i.Indenizacoes, i.OutrosIsentos, i.Decimo, i.IrrfDecimo])).ToList();
        linhas.Add(new LinhaRelatorio(["", "TOTAL", "", informes.Sum(i => i.Rendimentos), informes.Sum(i => i.Previdencia), informes.Sum(i => i.Pensao), informes.Sum(i => i.Irrf),
                                       informes.Sum(i => i.Indenizacoes), informes.Sum(i => i.OutrosIsentos), informes.Sum(i => i.Decimo), informes.Sum(i => i.IrrfDecimo)], Destaque: true));
        return new TabelaRelatorio($"Informe de rendimentos – ano-calendário {ano}",
            [new("Matrícula", TipoColuna.Texto, 1.8), new("Funcionário"), new("CPF", TipoColuna.Texto, 3), new("Rendimentos tributáveis", V), new("Previdência oficial", V),
             new("Pensão alimentícia", V), new("IRRF", V), new("Indenizações (rescisão)", V), new("Outros isentos", V), new("13º salário (líquido)", V), new("IRRF sobre 13º", V)],
            linhas, "Regime de caixa: entram os valores pagos no ano. Sem data de pagamento, a folha mensal conta como paga no 5º dia útil do mês seguinte.");
    }

    /// <summary>Comprovantes de rendimentos (modelo da IN RFB 2.060/2021) em PDF: de todos os funcionários (um por página) ou de um só.</summary>
    public async Task<Resultado<ArquivoGerado>> ComprovantesRendimentosAsync(long empresaId, int ano, long usuarioId, CancellationToken ct = default, long? funcionarioId = null)
    {
        var r = await InformesRendimentosAsync(empresaId, ano, usuarioId, ct);
        if (!r.Sucesso) return Resultado<ArquivoGerado>.Falha(r.Erros);
        var informes = r.Valor.Where(i => funcionarioId is null || i.FuncionarioId == funcionarioId).ToList();
        if (informes.Count == 0) return Resultado<ArquivoGerado>.Falha($"Não há rendimentos pagos em {ano} para gerar o comprovante.");
        var e = await cadastros.ObterEmpresaAsync(empresaId, ct);
        var fonte = e is null ? "" : $"Nome empresarial: {e.RazaoSocial}\nCNPJ: {ServicoLivros.FormatarCnpj(e.Cnpj)}";
        Coluna[] cols = [new("Descrição", 13.2), new("Valor (R$)", 3.6, Alinhamento.Direita)];
        static LinhaTabela L(string d, decimal v) => new([d, Formato.Moeda(v)]);
        var blocos = new List<Bloco>();
        foreach (var i in informes)
        {
            blocos.Add(new Bloco(Titulo: "1. Fonte pagadora pessoa jurídica", Texto: fonte, NovaPagina: blocos.Count > 0));
            blocos.Add(new Bloco(Titulo: "2. Pessoa física beneficiária dos rendimentos",
                                 Texto: $"CPF: {Domain.Cadastros.Cpf.Formatar(i.Cpf)}   Nome completo: {i.Nome}\nNatureza do rendimento: rendimentos do trabalho assalariado"));
            blocos.Add(new Bloco(Titulo: "3. Rendimentos tributáveis, deduções e imposto sobre a renda retido na fonte", Colunas: cols, Linhas:
            [
                L("01. Total dos rendimentos (inclusive férias)", i.Rendimentos), L("02. Contribuição previdenciária oficial", i.Previdencia),
                L("03. Contribuições a entidades de previdência complementar e a fundos de aposentadoria programada individual (Fapi)", 0),
                L("04. Pensão alimentícia (preencher também o quadro 7)", i.Pensao), L("05. Imposto sobre a renda retido na fonte", i.Irrf),
            ]));
            blocos.Add(new Bloco(Titulo: "4. Rendimentos isentos e não tributáveis", Colunas: cols, Linhas:
            [
                L("01. Parcela isenta dos proventos de aposentadoria, reserva remunerada, reforma e pensão (65 anos ou mais)", 0),
                L("02. Parcela isenta do 13º salário de aposentadoria, reserva remunerada, reforma e pensão (65 anos ou mais)", 0),
                L("03. Diárias e ajudas de custo", 0),
                L("04. Pensão e proventos de aposentadoria ou reforma por moléstia grave e aposentadoria ou reforma por acidente em serviço", 0),
                L("05. Lucros e dividendos apurados a partir de 1996, pagos por pessoa jurídica (lucro real, presumido ou arbitrado)", 0),
                L("06. Valores pagos ao titular ou sócio da microempresa ou empresa de pequeno porte, exceto pró-labore, aluguéis ou serviços prestados", 0),
                L("07. Indenizações por rescisão de contrato de trabalho, inclusive a título de PDV, e por acidente de trabalho", i.Indenizacoes),
                L("08. Juros de mora recebidos, devidos pelo atraso no pagamento de remuneração por exercício de emprego, cargo ou função", 0),
                L("09. Outros (especificar no quadro 7)", i.OutrosIsentos),
            ]));
            blocos.Add(new Bloco(Titulo: "5. Rendimentos sujeitos à tributação exclusiva (rendimento líquido)", Colunas: cols, Linhas:
            [
                L("01. 13º (décimo terceiro) salário", i.Decimo), L("02. Imposto sobre a renda retido na fonte sobre 13º (décimo terceiro) salário", i.IrrfDecimo),
                L("03. Outros", 0),
            ]));
            blocos.Add(new Bloco(Titulo: "6. Rendimentos recebidos acumuladamente (art. 12-A da Lei nº 7.713, de 1988)", Texto: "Não houve."));
            var compl = string.Join("\n", i.DetalheOutros.Select(d => $"Rendimento isento (quadro 4, linha 09): {d.Descricao} – {Formato.Moeda(d.Valor)}"));
            if (i.Pensao > 0) compl += (compl.Length > 0 ? "\n" : "") + $"Pensão alimentícia descontada: {Formato.Moeda(i.Pensao)} (informe o CPF e o nome do beneficiário).";
            blocos.Add(new Bloco(Titulo: "7. Informações complementares", Texto: compl.Length > 0 ? compl : "—"));
            blocos.Add(new Bloco(Titulo: "8. Responsável pelas informações",
                                 Texto: $"Data: {DateTime.Today:dd/MM/yyyy}\n\n_______________________________________\nAssinatura"));
        }
        return Resultado<ArquivoGerado>.Ok(gerador.Gerar(new DocumentoRelatorio("Comprovante de rendimentos pagos e de imposto sobre a renda retido na fonte",
            [$"Ministério da Fazenda – Secretaria da Receita Federal do Brasil", $"Imposto sobre a renda da pessoa física – exercício {ano + 1}, ano-calendário {ano}"], blocos)));
    }

    // ------------------------------------------------------------------ tabelas

    private static readonly TipoColuna V = TipoColuna.Valor;

    public async Task<Resultado<IReadOnlyList<TabelaRelatorio>>> ResumoFolhaAsync(long empresaId, DateOnly competencia, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(empresaId, usuarioId, ct) is { } negado) return Resultado<IReadOnlyList<TabelaRelatorio>>.Falha(negado);
        competencia = new DateOnly(competencia.Year, competencia.Month, 1);
        var calculos = await movimento.ListarCalculosAsync(empresaId, competencia, null, true, ct);
        var porFunc = calculos.GroupBy(c => (c.FuncionarioId, c.Matricula, c.FuncionarioNome)).OrderBy(g => g.Key.FuncionarioNome);
        var linhas = new List<LinhaRelatorio>();
        foreach (var g in porFunc)
            linhas.Add(new LinhaRelatorio([g.Key.Matricula, g.Key.FuncionarioNome, g.Sum(c => c.TotalProventos), g.Sum(c => c.TotalDescontos), g.Sum(c => c.Liquido),
                                           g.Sum(c => c.Inss), g.Sum(c => c.Irrf), g.Sum(c => c.Fgts)]));
        linhas.Add(new LinhaRelatorio(["", "TOTAL", calculos.Sum(c => c.TotalProventos), calculos.Sum(c => c.TotalDescontos), calculos.Sum(c => c.Liquido),
                                       calculos.Sum(c => c.Inss), calculos.Sum(c => c.Irrf), calculos.Sum(c => c.Fgts)], Destaque: true));
        var porFuncionario = new TabelaRelatorio("Resumo por funcionário",
            [new("Matrícula", TipoColuna.Texto, 1.8), new("Funcionário"), new("Proventos", V), new("Descontos", V), new("Líquido", V), new("INSS", V), new("IRRF", V), new("FGTS", V)],
            linhas);

        var rubricas = calculos.SelectMany(c => c.Itens).Where(i => i.Tipo is 'P' or 'D').GroupBy(i => (i.Codigo, i.Descricao, i.Tipo)).OrderBy(g => g.Key.Tipo == 'D').ThenBy(g => g.Key.Codigo)
            .Select(g => new LinhaRelatorio([g.Key.Codigo, g.Key.Descricao, g.Key.Tipo == 'P' ? "Provento" : "Desconto", g.Sum(i => i.Valor)])).ToList();
        var porRubrica = new TabelaRelatorio("Totais por rubrica", [new("Código", TipoColuna.Texto, 1.8), new("Rubrica"), new("Tipo", TipoColuna.Texto, 2.5), new("Valor", V)], rubricas);
        return Resultado<IReadOnlyList<TabelaRelatorio>>.Ok([porFuncionario, porRubrica]);
    }

    public async Task<Resultado<TabelaRelatorio>> EncargosAsync(long empresaId, DateOnly competencia, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(empresaId, usuarioId, ct) is { } negado) return Resultado<TabelaRelatorio>.Falha(negado);
        competencia = new DateOnly(competencia.Year, competencia.Month, 1);
        var calculos = await movimento.ListarCalculosAsync(empresaId, competencia, null, false, ct);
        var linhas = calculos.GroupBy(c => (c.Matricula, c.FuncionarioNome)).OrderBy(g => g.Key.FuncionarioNome).Select(g => new LinhaRelatorio(
            [g.Key.Matricula, g.Key.FuncionarioNome, g.Sum(c => c.BaseInss), g.Sum(c => c.EncInssPatronal), g.Sum(c => c.EncRat), g.Sum(c => c.EncTerceiros), g.Sum(c => c.Fgts),
             g.Sum(c => c.EncargosPrevidenciarios + c.Fgts)])).ToList();
        linhas.Add(new LinhaRelatorio(["", "TOTAL", calculos.Sum(c => c.BaseInss), calculos.Sum(c => c.EncInssPatronal), calculos.Sum(c => c.EncRat), calculos.Sum(c => c.EncTerceiros),
                                       calculos.Sum(c => c.Fgts), calculos.Sum(c => c.EncargosPrevidenciarios + c.Fgts)], Destaque: true));
        var sf = calculos.Sum(c => c.SalarioFamilia);
        var rodape = $"INSS retido dos empregados: {Formato.Moeda(calculos.Sum(c => c.Inss))} · IRRF retido: {Formato.Moeda(calculos.Sum(c => c.Irrf))}"
                     + (sf > 0 ? $" · Salário-família a compensar na GPS/DCTFWeb: {Formato.Moeda(sf)}" : "");
        return Resultado<TabelaRelatorio>.Ok(new TabelaRelatorio("Encargos patronais",
            [new("Matrícula", TipoColuna.Texto, 1.8), new("Funcionário"), new("Base INSS", V), new("INSS patronal", V), new("RAT x FAP", V), new("Terceiros", V), new("FGTS", V), new("Total", V)],
            linhas, rodape));
    }

    public async Task<Resultado<TabelaRelatorio>> ProvisoesAsync(long empresaId, DateOnly competencia, long usuarioId, CancellationToken ct = default)
    {
        if (await NegarAsync(empresaId, usuarioId, ct) is { } negado) return Resultado<TabelaRelatorio>.Falha(negado);
        var provisoes = await contabilizacao.ListarProvisoesAsync(empresaId, new DateOnly(competencia.Year, competencia.Month, 1), ct);
        var linhas = provisoes.Select(p => new LinhaRelatorio([p.Matricula, p.FuncionarioNome, p.Base, p.Ferias, p.EncFeriasInss + p.EncFeriasFgts, p.Decimo,
                                                              p.EncDecimoInss + p.EncDecimoFgts, p.Total])).ToList();
        linhas.Add(new LinhaRelatorio(["", "TOTAL", provisoes.Sum(p => p.Base), provisoes.Sum(p => p.Ferias), provisoes.Sum(p => p.EncFeriasInss + p.EncFeriasFgts),
                                       provisoes.Sum(p => p.Decimo), provisoes.Sum(p => p.EncDecimoInss + p.EncDecimoFgts), provisoes.Sum(p => p.Total)], Destaque: true));
        return Resultado<TabelaRelatorio>.Ok(new TabelaRelatorio("Provisões de férias e 13º salário",
            [new("Matrícula", TipoColuna.Texto, 1.8), new("Funcionário"), new("Base", V), new("Férias + 1/3", V), new("Encargos férias", V), new("13º salário", V), new("Encargos 13º", V), new("Total", V)],
            linhas));
    }
}
