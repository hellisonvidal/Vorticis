using Npgsql;
using Perseus.Application.Cadastros;
using Perseus.Domain.Comum;
using Perseus.Domain.Contabil;

namespace Perseus.Infra.Postgres;

/// <summary>Conversão entre enums do domínio e enums/textos do PostgreSQL.</summary>
internal static class Mapeamentos
{
    public static string Sql(this OrigemLancamento o) => o switch
    {
        OrigemLancamento.Manual => "MANUAL",
        OrigemLancamento.NFe => "NFE",
        OrigemLancamento.NFSe => "NFSE",
        OrigemLancamento.CTe => "CTE",
        OrigemLancamento.Extrato => "EXTRATO",
        OrigemLancamento.Planilha => "PLANILHA",
        OrigemLancamento.Folha => "FOLHA",
        OrigemLancamento.Encerramento => "ENCERRAMENTO",
        OrigemLancamento.Estorno => "ESTORNO",
        OrigemLancamento.SaldoInicial => "SALDO_INICIAL",
        OrigemLancamento.Depreciacao => "DEPRECIACAO",
        OrigemLancamento.ApuracaoTributo => "APURACAO_TRIBUTO",
        _ => throw new ArgumentOutOfRangeException(nameof(o)),
    };

    public static OrigemLancamento Origem(string s) =>
        Enum.GetValues<OrigemLancamento>().Single(o => o.Sql() == s);

    public static string Sql(this IndicadorLancamento i) => i switch
    {
        IndicadorLancamento.Normal => "N",
        IndicadorLancamento.Encerramento => "E",
        IndicadorLancamento.Extemporaneo => "X",
        _ => throw new ArgumentOutOfRangeException(nameof(i)),
    };

    public static StatusLancamento Status(string s) => s switch
    {
        "RASCUNHO" => StatusLancamento.Rascunho,
        "EFETIVADO" => StatusLancamento.Efetivado,
        "ESTORNADO" => StatusLancamento.Estornado,
        _ => throw new ArgumentOutOfRangeException(nameof(s)),
    };

    public static IndicadorDC DC(string s) => s == "D" ? IndicadorDC.D : IndicadorDC.C;

    public static string Sql(this RegimeTributario r) => r switch
    {
        RegimeTributario.MEI => "MEI",
        RegimeTributario.SimplesNacional => "SIMPLES_NACIONAL",
        RegimeTributario.LucroPresumido => "LUCRO_PRESUMIDO",
        RegimeTributario.LucroReal => "LUCRO_REAL",
        RegimeTributario.LucroArbitrado => "LUCRO_ARBITRADO",
        RegimeTributario.ImuneIsenta => "IMUNE_ISENTA",
        _ => throw new ArgumentOutOfRangeException(nameof(r)),
    };

    /// <summary>
    /// Erros levantados pelas regras do banco (RAISE, CHECK, UNIQUE, FK) viram violação de regra
    /// com a mensagem original; demais erros (conexão, sintaxe) continuam como exceção técnica.
    /// </summary>
    public static async Task<T> TraduzirErrosAsync<T>(Func<Task<T>> acao)
    {
        try
        {
            return await acao();
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.RaiseException
                                                       or PostgresErrorCodes.CheckViolation
                                                       or PostgresErrorCodes.UniqueViolation
                                                       or PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new ViolacaoRegraException(ex.MessageText, ex);
        }
    }

    public static NpgsqlCommand Com(this NpgsqlCommand cmd, string nome, object? valor)
    {
        cmd.Parameters.AddWithValue(nome, valor ?? DBNull.Value);
        return cmd;
    }

    public static async Task DefinirUsuarioAsync(NpgsqlConnection con, NpgsqlTransaction tx, long? usuarioId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT set_config('perseus.usuario',
                   COALESCE((SELECT login FROM core.usuario WHERE id = @u), current_user), true)
            """, con, tx);
        cmd.Com("u", usuarioId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
