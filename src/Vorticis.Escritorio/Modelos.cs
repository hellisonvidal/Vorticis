namespace Vorticis.Escritorio;

public enum SituacaoTarefa { Pendente, Andamento, Concluida, Cancelada }
public enum Recorrencia { Nenhuma, Mensal, Anual }
public enum SituacaoCobranca { Aberta, Recebida, Cancelada }
public enum TipoProtocolo { Entrega, Recebimento }

/// <summary>Cliente e/ou fornecedor do escritório: cadastro próprio do módulo, não compartilhado com os demais módulos.</summary>
public sealed record PessoaEscritorio(long Id, int Codigo, bool Cliente, bool Fornecedor, string Nome, string? Documento, string? Regime = null,
                                      string? Contato = null, string? Email = null, string? Telefone = null, string? Endereco = null, string? Municipio = null,
                                      string? Uf = null, bool Ativo = true, string? Observacao = null)
{
    public string Papel => (Cliente, Fornecedor) switch { (true, true) => "Cliente e fornecedor", (true, _) => "Cliente", _ => "Fornecedor" };
    public string DocumentoFormatado => Documento switch
    {
        { Length: 14 } d => Vorticis.Domain.Cadastros.Cnpj.Formatar(d),
        { Length: 11 } d => Vorticis.Domain.Cadastros.Cpf.Formatar(d),
        _ => "",
    };
}

public sealed record Tarefa(long Id, long EmpresaId, long? PessoaId, string Titulo, string? Descricao, DateOnly Vencimento, long? ResponsavelId, int Prioridade,
                            SituacaoTarefa Situacao, Recorrencia Recorrencia, string? PessoaNome = null, string? ResponsavelNome = null,
                            DateTimeOffset? ConcluidaEm = null)
{
    public bool Atrasada(DateOnly hoje) => Situacao is SituacaoTarefa.Pendente or SituacaoTarefa.Andamento && Vencimento < hoje;
}

public sealed record ContratoHonorario(long Id, long PessoaId, decimal Valor, int DiaVencimento, DateOnly Inicio, DateOnly? Fim, string IndiceReajuste,
                                       string? Observacao, string? PessoaNome = null);

public sealed record CobrancaHonorario(long Id, long PessoaId, DateOnly Competencia, decimal Valor, DateOnly Vencimento, SituacaoCobranca Situacao,
                                       DateOnly? RecebidoEm, decimal? ValorRecebido, string? PessoaNome = null)
{
    public bool Vencida(DateOnly hoje) => Situacao == SituacaoCobranca.Aberta && Vencimento < hoje;
}

public sealed record Protocolo(long Id, long EmpresaId, long? PessoaId, int Numero, DateOnly Data, TipoProtocolo Tipo, string Documento, string? Descricao, string Pessoa,
                               string? UsuarioNome = null, string? ClienteNome = null);

/// <summary>Linha da carteira de clientes do escritório (cadastro próprio do módulo).</summary>
public sealed record ClienteEscritorio(long PessoaId, int Codigo, string Nome, string? Documento, string? Regime, int TarefasPendentes, int TarefasAtrasadas,
                                       decimal HonorarioMensal, decimal HonorariosEmAberto, string? Contato);

public enum ModeloContrato { Cliente, Fornecedor }
public enum Periodicidade { Mensal, Anual, Unico }

/// <summary>Contrato gerado pelo escritório para um cliente (prestação de serviços contábeis) ou um fornecedor.</summary>
public sealed record ContratoEscritorio(long Id, int Numero, long PessoaId, ModeloContrato Modelo, string Objeto, decimal Valor, Periodicidade Periodicidade,
                                        int? DiaVencimento, DateOnly Inicio, DateOnly? Fim, string IndiceReajuste, string? Foro, string Texto, bool Ativo = true,
                                        string? PessoaNome = null);

/// <summary>Dados do escritório (a empresa ativa no módulo) que entram no contrato.</summary>
public sealed record DadosEscritorio(string RazaoSocial, string Cnpj, string? Endereco, string? Municipio, string? Uf);

/// <summary>
/// Gerador de contratos: monta o texto (cláusulas numeradas) a partir do modelo — prestação de serviços contábeis ao cliente ou
/// fornecimento ao escritório —, dos dados das partes e das condições. O texto gerado pode ser ajustado antes de gravar.
/// </summary>
public static class GeradorContratos
{
    public static string ObjetoPadrao(ModeloContrato m) => m == ModeloContrato.Cliente
        ? "Prestação de serviços de contabilidade: escrituração contábil e fiscal, apuração de tributos, folha de pagamento e entrega das obrigações acessórias da CONTRATANTE."
        : "Fornecimento de produtos ou serviços ao escritório, conforme proposta aceita pelas partes.";

    public static string Titulo(ModeloContrato m) => m == ModeloContrato.Cliente ? "CONTRATO DE PRESTAÇÃO DE SERVIÇOS CONTÁBEIS" : "CONTRATO DE FORNECIMENTO";

    private static string Qualificacao(string nome, string? doc, string? endereco, string? municipio, string? uf)
    {
        var d = doc switch
        {
            { Length: 14 } x => $", inscrita no CNPJ sob o nº {Vorticis.Domain.Cadastros.Cnpj.Formatar(x)}",
            { Length: 11 } x => $", inscrito(a) no CPF sob o nº {Vorticis.Domain.Cadastros.Cpf.Formatar(x)}",
            _ => "",
        };
        var e = string.Join(", ", new[] { endereco, municipio is null ? null : municipio + (uf is null ? "" : "/" + uf) }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return $"{nome}{d}{(e.Length > 0 ? $", com endereço em {e}" : "")}";
    }

    private static string Periodo(Periodicidade p) => p switch { Periodicidade.Anual => "por ano", Periodicidade.Unico => "em parcela única", _ => "por mês" };

    public static string Gerar(DadosEscritorio escritorio, PessoaEscritorio pessoa, ContratoEscritorio c)
    {
        var esc = Qualificacao(escritorio.RazaoSocial, escritorio.Cnpj, escritorio.Endereco, escritorio.Municipio, escritorio.Uf);
        var outra = Qualificacao(pessoa.Nome, pessoa.Documento, pessoa.Endereco, pessoa.Municipio, pessoa.Uf);
        var (contratante, contratada) = c.Modelo == ModeloContrato.Cliente ? (outra, esc) : (esc, outra);
        var valor = c.Valor.ToString("N2", Vorticis.Domain.Comum.Formato.PtBr);
        var venc = c.DiaVencimento is { } dia && c.Periodicidade != Periodicidade.Unico ? $", com vencimento no dia {dia}" : "";
        var vigencia = c.Fim is { } f ? $"de {c.Inicio:dd/MM/yyyy} a {f:dd/MM/yyyy}" : $"a partir de {c.Inicio:dd/MM/yyyy}, por prazo indeterminado";
        var reajuste = c.IndiceReajuste == "NENHUM" ? "O valor não terá reajuste automático; eventual alteração será acordada por escrito entre as partes."
            : c.IndiceReajuste == "SALARIO" ? "O valor será reajustado anualmente na mesma proporção do salário mínimo nacional."
            : $"O valor será reajustado anualmente pela variação do {c.IndiceReajuste} acumulada nos últimos 12 meses.";
        var clausulas = new List<string>
        {
            $"CONTRATANTE: {contratante}.",
            $"CONTRATADA: {contratada}.",
            $"CLÁUSULA 1ª – DO OBJETO. {c.Objeto.Trim()}",
            $"CLÁUSULA 2ª – DO PREÇO. Pelo objeto deste contrato, a CONTRATANTE pagará à CONTRATADA R$ {valor} {Periodo(c.Periodicidade)}{venc}.",
            $"CLÁUSULA 3ª – DO REAJUSTE. {reajuste}",
            $"CLÁUSULA 4ª – DA VIGÊNCIA. Este contrato vigora {vigencia}.",
            c.Modelo == ModeloContrato.Cliente
                ? "CLÁUSULA 5ª – DAS OBRIGAÇÕES. A CONTRATANTE entregará à CONTRATADA, nos prazos combinados, os documentos e informações necessários aos serviços; a CONTRATADA executará os serviços com zelo e sigilo, observadas as normas do Conselho Federal de Contabilidade."
                : "CLÁUSULA 5ª – DAS OBRIGAÇÕES. A CONTRATADA entregará os produtos ou serviços nas condições e prazos combinados; a CONTRATANTE efetuará os pagamentos nas datas acordadas.",
            "CLÁUSULA 6ª – DA RESCISÃO. Qualquer das partes poderá rescindir este contrato mediante aviso por escrito com 30 (trinta) dias de antecedência, sem prejuízo dos valores devidos até a data da rescisão.",
            $"CLÁUSULA 7ª – DO FORO. As partes elegem o foro da comarca de {(string.IsNullOrWhiteSpace(c.Foro) ? escritorio.Municipio ?? "sede da CONTRATADA" : c.Foro.Trim())} para dirimir as questões deste contrato.",
            $"E, por estarem de acordo, assinam este instrumento em duas vias de igual teor.",
        };
        return string.Join("\n\n", clausulas);
    }

    /// <summary>Contrato em PDF: título, cláusulas e assinaturas das partes.</summary>
    public static Vorticis.Application.Relatorios.DocumentoRelatorio Documento(DadosEscritorio escritorio, PessoaEscritorio pessoa, ContratoEscritorio c)
    {
        var (contratante, contratada) = c.Modelo == ModeloContrato.Cliente ? (pessoa.Nome, escritorio.RazaoSocial) : (escritorio.RazaoSocial, pessoa.Nome);
        var blocos = c.Texto.Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Select(t => new Vorticis.Application.Relatorios.Bloco(Texto: t.Trim())).ToList();
        return new Vorticis.Application.Relatorios.DocumentoRelatorio($"{Titulo(c.Modelo)} Nº {c.Numero}", [], blocos,
            Assinaturas: [$"{contratante}\nCONTRATANTE", $"{contratada}\nCONTRATADA"]);
    }
}

/// <summary>Regras puras do escritório: recorrência das tarefas e vencimento dos honorários.</summary>
public static class RegrasEscritorio
{
    public static DateOnly Mes(DateOnly d) => new(d.Year, d.Month, 1);

    /// <summary>Dia do mês limitado ao último dia (31 em fevereiro vira 28/29).</summary>
    public static DateOnly NoDia(DateOnly mes, int dia) => new(mes.Year, mes.Month, Math.Min(Math.Max(dia, 1), DateTime.DaysInMonth(mes.Year, mes.Month)));

    /// <summary>Próximo vencimento de uma tarefa recorrente (mantém o dia; no mês curto, o último dia).</summary>
    public static DateOnly? Proxima(DateOnly vencimento, Recorrencia r) => r switch
    {
        Recorrencia.Mensal => NoDia(Mes(vencimento).AddMonths(1), vencimento.Day),
        Recorrencia.Anual => NoDia(Mes(vencimento).AddYears(1), vencimento.Day),
        _ => null,
    };

    public static bool Vigente(ContratoHonorario c, DateOnly competencia)
        => Mes(c.Inicio) <= competencia && (c.Fim is null || Mes(c.Fim.Value) >= competencia);

    /// <summary>Cobranças da competência para os contratos vigentes cujo cliente ainda não tem cobrança nela.</summary>
    public static IReadOnlyList<CobrancaHonorario> GerarCobrancas(IEnumerable<ContratoHonorario> contratos, DateOnly competencia, IReadOnlySet<long> jaCobradas)
    {
        competencia = Mes(competencia);
        return contratos.Where(c => Vigente(c, competencia) && !jaCobradas.Contains(c.PessoaId))
                        .Select(c => new CobrancaHonorario(0, c.PessoaId, competencia, c.Valor, NoDia(competencia, c.DiaVencimento), SituacaoCobranca.Aberta, null, null, c.PessoaNome))
                        .ToList();
    }

    public static string Sql(this SituacaoTarefa s) => s switch
    {
        SituacaoTarefa.Andamento => "ANDAMENTO", SituacaoTarefa.Concluida => "CONCLUIDA", SituacaoTarefa.Cancelada => "CANCELADA", _ => "PENDENTE",
    };
    public static SituacaoTarefa SituacaoTarefaDe(string s) => s switch
    {
        "ANDAMENTO" => SituacaoTarefa.Andamento, "CONCLUIDA" => SituacaoTarefa.Concluida, "CANCELADA" => SituacaoTarefa.Cancelada, _ => SituacaoTarefa.Pendente,
    };
    public static string Rotulo(this SituacaoTarefa s) => s switch
    {
        SituacaoTarefa.Andamento => "Em andamento", SituacaoTarefa.Concluida => "Concluída", SituacaoTarefa.Cancelada => "Cancelada", _ => "Pendente",
    };
    public static string Sql(this Recorrencia r) => r switch { Recorrencia.Mensal => "MENSAL", Recorrencia.Anual => "ANUAL", _ => "NENHUMA" };
    public static Recorrencia RecorrenciaDe(string s) => s switch { "MENSAL" => Recorrencia.Mensal, "ANUAL" => Recorrencia.Anual, _ => Recorrencia.Nenhuma };
    public static string Rotulo(this Recorrencia r) => r switch { Recorrencia.Mensal => "Todo mês", Recorrencia.Anual => "Todo ano", _ => "Não repete" };
    public static string Sql(this SituacaoCobranca s) => s switch { SituacaoCobranca.Recebida => "RECEBIDA", SituacaoCobranca.Cancelada => "CANCELADA", _ => "ABERTA" };
    public static SituacaoCobranca SituacaoCobrancaDe(string s) => s switch { "RECEBIDA" => SituacaoCobranca.Recebida, "CANCELADA" => SituacaoCobranca.Cancelada, _ => SituacaoCobranca.Aberta };
    public static string Sql(this Periodicidade p) => p switch { Periodicidade.Anual => "ANUAL", Periodicidade.Unico => "UNICO", _ => "MENSAL" };
    public static Periodicidade PeriodicidadeDe(string s) => s switch { "ANUAL" => Periodicidade.Anual, "UNICO" => Periodicidade.Unico, _ => Periodicidade.Mensal };
    public static string Rotulo(this Periodicidade p) => p switch { Periodicidade.Anual => "Anual", Periodicidade.Unico => "Parcela única", _ => "Mensal" };

    /// <summary>Confere o cadastro do cliente/fornecedor do escritório (devolve os erros).</summary>
    public static IReadOnlyList<string> Validar(PessoaEscritorio p)
    {
        var erros = new List<string>();
        if (p.Codigo <= 0) erros.Add("Informe o código (número maior que zero).");
        if (string.IsNullOrWhiteSpace(p.Nome)) erros.Add("Informe o nome ou a razão social.");
        if (!p.Cliente && !p.Fornecedor) erros.Add("Marque se é cliente, fornecedor ou os dois.");
        if (p.Documento is { Length: > 0 } d && !(d.Length == 14 && Vorticis.Domain.Cadastros.Cnpj.Valido(d) || d.Length == 11 && Vorticis.Domain.Cadastros.Cpf.Valido(d)))
            erros.Add("CPF ou CNPJ inválido.");
        if (p.Uf is { Length: > 0 } uf && uf.Length != 2) erros.Add("UF com 2 letras.");
        return erros;
    }

    public static IReadOnlyList<string> Validar(ContratoEscritorio c)
    {
        var erros = new List<string>();
        if (c.PessoaId <= 0) erros.Add(c.Modelo == ModeloContrato.Cliente ? "Escolha o cliente." : "Escolha o fornecedor.");
        if (string.IsNullOrWhiteSpace(c.Objeto)) erros.Add("Informe o objeto do contrato.");
        if (c.Valor < 0) erros.Add("O valor não pode ser negativo.");
        if (c.DiaVencimento is < 1 or > 31) erros.Add("Dia de vencimento: de 1 a 31.");
        if (c.Fim is { } f && f < c.Inicio) erros.Add("O fim do contrato não pode ser anterior ao início.");
        if (string.IsNullOrWhiteSpace(c.Texto)) erros.Add("Gere o texto do contrato antes de gravar.");
        return erros;
    }

    public static string Rotulo(this SituacaoCobranca s) => s switch { SituacaoCobranca.Recebida => "Recebida", SituacaoCobranca.Cancelada => "Cancelada", _ => "Em aberto" };
}
