using System.Globalization;
using System.Text.Json;
using Npgsql;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Comum;
using Vorticis.Fiscal.Postgres;

namespace Vorticis.Fiscal.Reforma;

/// <summary>Como o Simples Nacional recolhe o IBS e a CBS em um semestre: dentro do DAS (padrão) ou pelo regime regular (fora do DAS, com débitos e créditos).</summary>
public enum OpcaoIbsCbs { DentroDoDas, RegimeRegular }

public static class OpcaoIbsCbsTexto
{
    public static string Rotulo(this OpcaoIbsCbs o) => o == OpcaoIbsCbs.RegimeRegular ? "Regime regular (IBS/CBS fora do DAS)" : "Dentro do DAS";
    public static string Codigo(this OpcaoIbsCbs o) => o == OpcaoIbsCbs.RegimeRegular ? "REGIME_REGULAR" : "DENTRO_DO_DAS";
    public static OpcaoIbsCbs DeCodigo(string c) => c == "REGIME_REGULAR" ? OpcaoIbsCbs.RegimeRegular : OpcaoIbsCbs.DentroDoDas;
}

/// <summary>Linha do histórico da opção da empresa. A vigência começa em 1º/1 (1º semestre) ou 1º/7 (2º semestre) e vale até a seguinte.</summary>
public sealed record OpcaoIbsCbsEmpresa(long Id, DateOnly VigenciaInicio, DateOnly? VigenciaFim, OpcaoIbsCbs Opcao, DateOnly? DataPedido, string? Protocolo, string? Observacao)
{
    public int Semestre => VigenciaInicio.Month == 1 ? 1 : 2;
    public string SemestreTexto => $"{Semestre}º semestre de {VigenciaInicio.Year}";
}

public sealed record OpcaoGravada(OpcaoIbsCbsEmpresa Opcao, IReadOnlyList<string> Avisos);

/// <summary>Uma janela de datas (início e fim, inclusive).</summary>
public sealed record JanelaData(DateOnly De, DateOnly Ate)
{
    public bool Contem(DateOnly d) => d >= De && d <= Ate;
    public override string ToString() => $"{De:dd/MM/yyyy} a {Ate:dd/MM/yyyy}";
}

/// <summary>
/// Janelas de opção e de cancelamento do regime regular do IBS/CBS (parâmetro legal SIMPLES_OPCAO_IBSCBS): regras gerais por semestre, com exceções por
/// ano/semestre (como o 1º semestre de 2027, prorrogado pela Res. CGSN 194/2026) e o período de abertura de CNPJ em que a empresa escolhe na inscrição.
/// </summary>
public sealed class JanelasOpcaoIbsCbs
{
    private sealed record Regra(int Semestre, (int Mes, int Dia, int AnoRel)? OpcaoDe, (int Mes, int Dia, int AnoRel)? OpcaoAte,
                                (int Mes, int Dia, int AnoRel)? CancelaDe, (int Mes, int Dia, int AnoRel)? CancelaAte);
    private sealed record Excecao(int Ano, int Semestre, JanelaData? Opcao, JanelaData? Cancelamento, string? Fundamento);

    private readonly List<Regra> _regras = [];
    private readonly List<Excecao> _excecoes = [];
    public JanelaData? Abertura { get; private set; }
    public string Fundamento { get; private set; } = "";

    private static DateOnly Data(string s) => DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static JanelasOpcaoIbsCbs Carregar(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        var j = new JanelasOpcaoIbsCbs { Fundamento = r.TryGetProperty("fundamento", out var f) ? f.GetString() ?? "" : "" };
        static (int, int, int)? P(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Object
            ? (v.GetProperty("mes").GetInt32(), v.GetProperty("dia").GetInt32(), v.GetProperty("ano_relativo").GetInt32()) : null;
        foreach (var x in r.GetProperty("regras").EnumerateArray())
            j._regras.Add(new Regra(x.GetProperty("semestre").GetInt32(), P(x, "opcao_de"), P(x, "opcao_ate"), P(x, "cancelamento_de"), P(x, "cancelamento_ate")));
        if (r.TryGetProperty("excecoes", out var ex))
            foreach (var x in ex.EnumerateArray())
            {
                string? S(string n) => x.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                var op = S("opcao_regime_regular_de") is { } d1 && S("opcao_regime_regular_ate") is { } a1 ? new JanelaData(Data(d1), Data(a1)) : null;
                var ca = S("cancelamento_de") is { } d2 && S("cancelamento_ate") is { } a2 ? new JanelaData(Data(d2), Data(a2)) : null;
                j._excecoes.Add(new Excecao(x.GetProperty("ano").GetInt32(), x.GetProperty("semestre").GetInt32(), op, ca, S("fundamento")));
            }
        if (r.TryGetProperty("abertura_cnpj", out var ab) && ab.ValueKind == JsonValueKind.Object)
            j.Abertura = new JanelaData(Data(ab.GetProperty("de").GetString()!), Data(ab.GetProperty("ate").GetString()!));
        return j;
    }

    private static JanelaData? Montar((int Mes, int Dia, int AnoRel)? de, (int Mes, int Dia, int AnoRel)? ate, int ano) =>
        de is { } a && ate is { } b ? new JanelaData(new DateOnly(ano + a.AnoRel, a.Mes, a.Dia), new DateOnly(ano + b.AnoRel, b.Mes, b.Dia)) : null;

    /// <summary>Janela para pedir o regime regular no semestre (null se não há regra).</summary>
    public JanelaData? JanelaOpcao(int ano, int semestre) =>
        _excecoes.FirstOrDefault(e => e.Ano == ano && e.Semestre == semestre)?.Opcao is { } o ? o
        : _regras.FirstOrDefault(r => r.Semestre == semestre) is { } r ? Montar(r.OpcaoDe, r.OpcaoAte, ano) : null;

    /// <summary>Janela para cancelar a opção pelo regime regular no semestre (null se não há janela cadastrada).</summary>
    public JanelaData? JanelaCancelamento(int ano, int semestre) =>
        _excecoes.FirstOrDefault(e => e.Ano == ano && e.Semestre == semestre)?.Cancelamento is { } o ? o
        : _regras.FirstOrDefault(r => r.Semestre == semestre) is { } r ? Montar(r.CancelaDe, r.CancelaAte, ano) : null;

    /// <summary>
    /// Confere o pedido contra as janelas e devolve avisos (não bloqueia: o escritório pode registrar um pedido já protocolado). Pedir o regime regular usa
    /// a janela de opção; voltar ao DAS depois do regime regular usa a de cancelamento; empresa aberta na janela de abertura de CNPJ escolhe na inscrição.
    /// </summary>
    public IReadOnlyList<string> Verificar(OpcaoIbsCbs nova, OpcaoIbsCbs? anterior, DateOnly vigenciaInicio, DateOnly? dataPedido, DateOnly? inicioAtividadeEmpresa, bool primeiraOpcao)
    {
        var avisos = new List<string>();
        var semestre = vigenciaInicio.Month == 1 ? 1 : 2;
        var ano = vigenciaInicio.Year;
        var abrindo = primeiraOpcao && Abertura is { } ab && inicioAtividadeEmpresa is { } ini && ab.Contem(ini) && vigenciaInicio <= new DateOnly(ab.Ate.Year + 1, 1, 1);
        if (abrindo) return avisos;   // escolhe na inscrição do CNPJ: sem prazo de pedido
        var cancelando = nova == OpcaoIbsCbs.DentroDoDas && anterior == OpcaoIbsCbs.RegimeRegular;
        if (nova == OpcaoIbsCbs.DentroDoDas && !cancelando) return avisos;   // é o padrão: não precisa de pedido
        var janela = cancelando ? JanelaCancelamento(ano, semestre) : JanelaOpcao(ano, semestre);
        var oque = cancelando ? "cancelamento da opção pelo regime regular" : "opção pelo regime regular do IBS/CBS";
        if (janela is null)
            avisos.Add($"Não há janela cadastrada para o {oque} no {semestre}º semestre de {ano}: confira o prazo na resolução do CGSN e nos parâmetros legais (SIMPLES_OPCAO_IBSCBS).");
        else if (dataPedido is null)
            avisos.Add($"Informe a data do pedido: o prazo do {oque} no {semestre}º semestre de {ano} é de {janela}.");
        else if (!janela.Contem(dataPedido.Value))
            avisos.Add($"Pedido de {dataPedido:dd/MM/yyyy} fora da janela do {oque} no {semestre}º semestre de {ano} ({janela}): o pedido pode não ser aceito. Confira o prazo.");
        return avisos;
    }
}

/// <summary>
/// Opção do Simples Nacional pelo IBS/CBS por semestre (histórico por empresa, em fiscal.opcao_ibscbs_simples). A opção vale até ser renunciada: a linha de
/// um semestre fica em vigor até a vigência da seguinte. Sem registro, vale "dentro do DAS". Consultar: FISCAL_APURAR ou FISCAL_RELATORIOS; gravar: FISCAL_APURAR.
/// </summary>
public sealed class ServicoOpcaoIbsCbs(NpgsqlDataSource ds, IParametrosFiscais parametros, Autorizador autorizador)
{
    /// <summary>Primeira vigência possível: IBS e CBS dentro do Simples começam em 2027.</summary>
    public static readonly DateOnly PrimeiraVigencia = new(2027, 1, 1);

    public async Task<JanelasOpcaoIbsCbs> JanelasAsync(DateOnly data, CancellationToken ct = default) =>
        JanelasOpcaoIbsCbs.Carregar(await parametros.ObterJsonAsync(ChavesParametro.SimplesOpcaoIbsCbs, data, ct));

    /// <summary>Primeiro dia do semestre da data.</summary>
    public static DateOnly InicioDoSemestre(DateOnly d) => new(d.Year, d.Month <= 6 ? 1 : 7, 1);

    private async Task<IReadOnlyList<OpcaoIbsCbsEmpresa>> LerAsync(long empresa, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("""
            SELECT id, vigencia_inicio, vigencia_fim, opcao, data_pedido, protocolo, observacao
              FROM fiscal.opcao_ibscbs_simples WHERE empresa_id = @e ORDER BY vigencia_inicio DESC
            """).Com("e", empresa);
        var lista = new List<OpcaoIbsCbsEmpresa>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            lista.Add(new OpcaoIbsCbsEmpresa(r.GetInt64(0), r.GetFieldValue<DateOnly>(1), r.IsDBNull(2) ? null : r.GetFieldValue<DateOnly>(2), OpcaoIbsCbsTexto.DeCodigo(r.GetString(3)),
                r.IsDBNull(4) ? null : r.GetFieldValue<DateOnly>(4), r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6)));
        return lista;
    }

    /// <summary>Opção em vigor na data (sem permissão: uso interno dos cálculos). Sem registro, "dentro do DAS".</summary>
    public async Task<OpcaoIbsCbs> NaDataAsync(long empresa, DateOnly data, CancellationToken ct = default) =>
        (await LerAsync(empresa, ct)).FirstOrDefault(o => o.VigenciaInicio <= data && (o.VigenciaFim is null || o.VigenciaFim >= data))?.Opcao ?? OpcaoIbsCbs.DentroDoDas;

    private async Task<string?> PermitirConsultaAsync(long empresa, long usuario, CancellationToken ct)
    {
        var p = await autorizador.ObterAsync(usuario, empresa, ct);
        return p.Pode(Permissao.FiscalApurar) || p.Pode(Permissao.FiscalRelatorios) ? null : "Acesso negado: é preciso poder apurar ou emitir relatórios fiscais.";
    }

    public async Task<Resultado<IReadOnlyList<OpcaoIbsCbsEmpresa>>> ListarAsync(long empresa, long usuario, CancellationToken ct = default)
    {
        if (await PermitirConsultaAsync(empresa, usuario, ct) is { } erro) return Resultado<IReadOnlyList<OpcaoIbsCbsEmpresa>>.Falha(erro);
        return Resultado<IReadOnlyList<OpcaoIbsCbsEmpresa>>.Ok(await LerAsync(empresa, ct));
    }

    public async Task<Resultado<OpcaoIbsCbs>> AtualAsync(long empresa, DateOnly data, long usuario, CancellationToken ct = default)
    {
        if (await PermitirConsultaAsync(empresa, usuario, ct) is { } erro) return Resultado<OpcaoIbsCbs>.Falha(erro);
        return Resultado<OpcaoIbsCbs>.Ok(await NaDataAsync(empresa, data, ct));
    }

    /// <summary>
    /// Registra (ou corrige) a opção de um semestre. A vigência é ajustada para o 1º dia do semestre. O fim da linha anterior passa a ser o dia anterior à nova
    /// vigência. Pedido fora da janela vira aviso, não erro.
    /// </summary>
    public async Task<Resultado<OpcaoGravada>> GravarAsync(long empresa, OpcaoIbsCbsEmpresa o, long usuario, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<OpcaoGravada>(usuario, empresa, Permissao.FiscalApurar, ct) is { } negado) return negado;
        var vigencia = InicioDoSemestre(o.VigenciaInicio);
        if (vigencia < PrimeiraVigencia) return Resultado<OpcaoGravada>.Falha($"A opção vale a partir do 1º semestre de {PrimeiraVigencia.Year}: informe uma vigência de 01/01/{PrimeiraVigencia.Year} em diante.");
        if ((o.Protocolo?.Length ?? 0) > 60) return Resultado<OpcaoGravada>.Falha("O protocolo aceita até 60 caracteres.");
        if ((o.Observacao?.Length ?? 0) > 500) return Resultado<OpcaoGravada>.Falha("A observação aceita até 500 caracteres.");

        var historico = await LerAsync(empresa, ct);
        var existente = historico.FirstOrDefault(x => x.VigenciaInicio == vigencia);
        var anterior = historico.Where(x => x.VigenciaInicio < vigencia).OrderByDescending(x => x.VigenciaInicio).FirstOrDefault();
        var seguinte = historico.Where(x => x.VigenciaInicio > vigencia).OrderBy(x => x.VigenciaInicio).FirstOrDefault();
        DateOnly? inicioAtividade = null;
        await using (var cmd = ds.CreateCommand("SELECT data_inicio_ativ FROM core.empresa WHERE id = @e").Com("e", empresa))
            if (await cmd.ExecuteScalarAsync(ct) is DateTime dt) inicioAtividade = DateOnly.FromDateTime(dt);
        var avisos = (await JanelasAsync(vigencia, ct)).Verificar(o.Opcao, anterior?.Opcao, vigencia, o.DataPedido, inicioAtividade, anterior is null).ToList();
        var fim = seguinte is null ? (DateOnly?)null : seguinte.VigenciaInicio.AddDays(-1);

        await using var con = await ds.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        if (anterior is not null)
            await using (var up = new NpgsqlCommand("UPDATE fiscal.opcao_ibscbs_simples SET vigencia_fim = @f WHERE id = @i", con, tx).Com("f", vigencia.AddDays(-1)).Com("i", anterior.Id))
                await up.ExecuteNonQueryAsync(ct);
        long id;
        await using (var ins = new NpgsqlCommand("""
            INSERT INTO fiscal.opcao_ibscbs_simples (empresa_id, vigencia_inicio, vigencia_fim, opcao, data_pedido, protocolo, observacao, criado_por)
            VALUES (@e, @v, @f, @o, @p, @pr, @ob, @u)
            ON CONFLICT (empresa_id, vigencia_inicio) DO UPDATE
               SET vigencia_fim = EXCLUDED.vigencia_fim, opcao = EXCLUDED.opcao, data_pedido = EXCLUDED.data_pedido, protocolo = EXCLUDED.protocolo, observacao = EXCLUDED.observacao
            RETURNING id
            """, con, tx).Com("e", empresa).Com("v", vigencia).Com("f", NpgsqlTypes.NpgsqlDbType.Date, fim).Com("o", o.Opcao.Codigo())
                .Com("p", NpgsqlTypes.NpgsqlDbType.Date, o.DataPedido).ComTexto("pr", o.Protocolo?.Trim()).ComTexto("ob", o.Observacao?.Trim()).Com("u", usuario))
            id = (long)(await ins.ExecuteScalarAsync(ct))!;
        await tx.CommitAsync(ct);
        _ = existente;
        return Resultado<OpcaoGravada>.Ok(new OpcaoGravada(o with { Id = id, VigenciaInicio = vigencia, VigenciaFim = fim }, avisos));
    }

    public async Task<Resultado> ExcluirAsync(long empresa, long id, long usuario, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuario, empresa, Permissao.FiscalApurar, ct) is { } negado) return Resultado.Falha(negado);
        var historico = await LerAsync(empresa, ct);
        var alvo = historico.FirstOrDefault(x => x.Id == id);
        if (alvo is null) return Resultado.Falha("Opção não encontrada.");
        var anterior = historico.Where(x => x.VigenciaInicio < alvo.VigenciaInicio).OrderByDescending(x => x.VigenciaInicio).FirstOrDefault();
        await using var con = await ds.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        await using (var del = new NpgsqlCommand("DELETE FROM fiscal.opcao_ibscbs_simples WHERE id = @i AND empresa_id = @e", con, tx).Com("i", id).Com("e", empresa))
            await del.ExecuteNonQueryAsync(ct);
        if (anterior is not null)
            await using (var up = new NpgsqlCommand("UPDATE fiscal.opcao_ibscbs_simples SET vigencia_fim = @f WHERE id = @i", con, tx)
                             .Com("f", NpgsqlTypes.NpgsqlDbType.Date, alvo.VigenciaFim).Com("i", anterior.Id))
                await up.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return Resultado.Ok();
    }
}
