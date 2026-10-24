namespace Vorticis.Escritorio;

public enum SituacaoTarefa { Pendente, Andamento, Concluida, Cancelada }
public enum Recorrencia { Nenhuma, Mensal, Anual }
public enum SituacaoCobranca { Aberta, Recebida, Cancelada }
public enum TipoProtocolo { Entrega, Recebimento }

public sealed record Tarefa(long Id, long EmpresaId, string Titulo, string? Descricao, DateOnly Vencimento, long? ResponsavelId, int Prioridade,
                            SituacaoTarefa Situacao, Recorrencia Recorrencia, string? EmpresaNome = null, string? ResponsavelNome = null,
                            DateTimeOffset? ConcluidaEm = null)
{
    public bool Atrasada(DateOnly hoje) => Situacao is SituacaoTarefa.Pendente or SituacaoTarefa.Andamento && Vencimento < hoje;
}

public sealed record ContratoHonorario(long Id, long EmpresaId, decimal Valor, int DiaVencimento, DateOnly Inicio, DateOnly? Fim, string IndiceReajuste,
                                       string? Observacao, string? EmpresaNome = null);

public sealed record CobrancaHonorario(long Id, long EmpresaId, DateOnly Competencia, decimal Valor, DateOnly Vencimento, SituacaoCobranca Situacao,
                                       DateOnly? RecebidoEm, decimal? ValorRecebido, string? EmpresaNome = null)
{
    public bool Vencida(DateOnly hoje) => Situacao == SituacaoCobranca.Aberta && Vencimento < hoje;
}

public sealed record Protocolo(long Id, long EmpresaId, int Numero, DateOnly Data, TipoProtocolo Tipo, string Documento, string? Descricao, string Pessoa,
                               string? UsuarioNome = null);

/// <summary>Linha da carteira de clientes do escritório.</summary>
public sealed record ClienteEscritorio(long EmpresaId, string Cnpj, string RazaoSocial, string? Regime, int TarefasPendentes, int TarefasAtrasadas,
                                       decimal HonorarioMensal, decimal HonorariosEmAberto, DateTimeOffset? CertificadoVence, string Modulos);

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

    /// <summary>Cobranças da competência para os contratos vigentes que ainda não têm cobrança nela.</summary>
    public static IReadOnlyList<CobrancaHonorario> GerarCobrancas(IEnumerable<ContratoHonorario> contratos, DateOnly competencia, IReadOnlySet<long> jaCobradas)
    {
        competencia = Mes(competencia);
        return contratos.Where(c => Vigente(c, competencia) && !jaCobradas.Contains(c.EmpresaId))
                        .Select(c => new CobrancaHonorario(0, c.EmpresaId, competencia, c.Valor, NoDia(competencia, c.DiaVencimento), SituacaoCobranca.Aberta, null, null, c.EmpresaNome))
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
    public static string Rotulo(this SituacaoCobranca s) => s switch { SituacaoCobranca.Recebida => "Recebida", SituacaoCobranca.Cancelada => "Cancelada", _ => "Em aberto" };
}
