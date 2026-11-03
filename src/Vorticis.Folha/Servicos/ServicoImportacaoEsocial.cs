using Vorticis.Application.Seguranca;
using Vorticis.Application.Sped.Importacao;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Domain.Sped.Importacao;
using Vorticis.Folha.Dados;
using Vorticis.Folha.Esocial.Importacao;

namespace Vorticis.Folha.Servicos;

/// <summary>Arquivos já lidos e consolidados (retificações e exclusões aplicadas), prontos para a prévia e a importação.</summary>
public sealed record AnaliseEsocial(DadosEsocial Dados, string Origem);

public sealed record PreviaEsocial(PlanoEsocial Plano, DadosEsocial Dados, long? ImportacaoAnteriorId)
{
    public bool PodeImportar(OpcoesImportacaoEsocial op)
        => !Plano.TemErroImpeditivo && ImportacaoAnteriorId is null && (Plano.ErrosPontuais == 0 || op.IgnorarComErro)
           && (Plano.Funcionarios.Count + Plano.Rubricas.Count + Plano.Calculos.Count + Plano.Ferias.Count + Plano.Afastamentos.Count + Plano.Departamentos.Count + Plano.Cargos.Count > 0 || Plano.Parametros is not null);

    public string Resumo(OpcoesImportacaoEsocial op)
    {
        var novos = Plano.Linhas.Sum(l => l.Novos);
        var existentes = Plano.Linhas.Sum(l => l.Existentes);
        var conflitos = Plano.Linhas.Sum(l => l.Conflitos);
        var erros = Plano.Ocorrencias.Count(o => o.Gravidade == GravidadeOcorrencia.Erro);
        var avisos = Plano.Ocorrencias.Count(o => o.Gravidade == GravidadeOcorrencia.Aviso);
        var s = $"{novos} registro(s) novo(s), {existentes} já existente(s) (ignorados), {conflitos} conflito(s), {erros} erro(s), {avisos} aviso(s)";
        if (ImportacaoAnteriorId is not null) return s + " · este material já foi importado; desfaça a importação anterior para importar de novo";
        if (Plano.TemErroImpeditivo) return s + " · há erro que impede a importação";
        if (Plano.ErrosPontuais > 0 && !op.IgnorarComErro) return s + " · marque \"Ignorar o que tiver erro\" para importar o restante";
        return PodeImportar(op) ? s + " · pronto para importar" : s + " · não há nada novo para importar";
    }
}

/// <summary>
/// Folha › Integrações › Importar eSocial: lê eventos do eSocial baixados do portal ou exportados por outro sistema (XML, pasta ou ZIP) e leva para a
/// folha o que a empresa precisa para continuar: cadastros, histórico de holerites (médias, bases), férias, afastamentos e desligamentos.
/// </summary>
public sealed class ServicoImportacaoEsocial(IRepositorioImportacaoEsocial repositorio, IRepositorioHistoricoSped historico, Autorizador autorizador)
{
    private const Permissao Exigida = Permissao.FolhaCadastrosManter;

    /// <summary>Lê os arquivos (XML, ZIP) e consolida os eventos; não grava nada.</summary>
    public async Task<Resultado<AnaliseEsocial>> AnalisarAsync(long empresaId, IReadOnlyList<ArquivoEsocial> arquivos, string origem, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Exigida, ct) is { } negado) return Resultado<AnaliseEsocial>.Falha(negado);
        if (arquivos.Count == 0) return Resultado<AnaliseEsocial>.Falha("Escolha os arquivos, a pasta ou o ZIP com os eventos do eSocial.");
        var expandidos = new List<ArquivoEsocial>();
        var avisos = new List<OcorrenciaImportacao>();
        foreach (var a in arquivos) expandidos.AddRange(LeitorEsocial.Expandir(a.Nome, a.Conteudo, avisos));
        var dados = await Task.Run(() => ConsolidadorEsocial.Consolidar(expandidos), ct);
        dados.Ocorrencias.InsertRange(0, avisos);
        if (dados.Eventos.Count == 0 && dados.Ocorrencias.All(o => o.Gravidade != GravidadeOcorrencia.Erro))
            return Resultado<AnaliseEsocial>.Falha("Não foi encontrado nenhum evento do eSocial importável nos arquivos (S-1000, S-1005, S-1010, S-1020, S-2200, S-2205, S-2206, S-2230, S-2299, S-2300, S-2399, S-1200 ou S-1210).");
        return Resultado<AnaliseEsocial>.Ok(new AnaliseEsocial(dados, origem));
    }

    public async Task<Resultado<PreviaEsocial>> PreviaAsync(long empresaId, AnaliseEsocial analise, OpcoesImportacaoEsocial opcoes, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Exigida, ct) is { } negado) return Resultado<PreviaEsocial>.Falha(negado);
        var sit = await repositorio.SituacaoAsync(empresaId, ct);
        if (sit is null) return Resultado<PreviaEsocial>.Falha("Empresa não encontrada.");
        var plano = await Task.Run(() => PlanejadorEsocial.Planejar(analise.Dados, sit, opcoes), ct);
        var anterior = await historico.ImportacaoDoArquivoAsync(empresaId, RepositorioImportacaoEsocial.TipoHistorico, analise.Dados.Sha256, ct);
        return Resultado<PreviaEsocial>.Ok(new PreviaEsocial(plano, analise.Dados, anterior));
    }

    public async Task<Resultado<RelatorioImportacao>> ImportarAsync(long empresaId, AnaliseEsocial analise, OpcoesImportacaoEsocial opcoes, long usuarioId,
                                                                    IProgress<ProgressoImportacao>? progresso = null, CancellationToken ct = default)
    {
        var previa = await PreviaAsync(empresaId, analise, opcoes, usuarioId, ct);
        if (!previa.Sucesso) return Resultado<RelatorioImportacao>.Falha(previa.Erros);
        var p = previa.Valor;
        if (p.ImportacaoAnteriorId is not null)
            return Resultado<RelatorioImportacao>.Falha("Este material já foi importado nesta empresa. Desfaça a importação anterior (Histórico de importações) para importar de novo.");
        if (p.Plano.TemErroImpeditivo)
            return Resultado<RelatorioImportacao>.Falha(p.Plano.Ocorrencias.Where(o => o.Gravidade == GravidadeOcorrencia.Erro && !o.Pontual).Take(10).Select(o => o.Motivo));
        if (p.Plano.ErrosPontuais > 0 && !opcoes.IgnorarComErro)
            return Resultado<RelatorioImportacao>.Falha(new[] { $"{p.Plano.ErrosPontuais} item(ns) com erro impedem a importação. Corrija os arquivos ou marque \"Ignorar o que tiver erro\"." }
                .Concat(p.Plano.Ocorrencias.Where(o => o.Gravidade == GravidadeOcorrencia.Erro).Take(10).Select(o => $"{o.Registro}: {o.Motivo}")));
        if (!p.PodeImportar(opcoes))
            return Resultado<RelatorioImportacao>.Falha("Não há nada novo para importar: tudo o que está nos arquivos já existe na folha.");
        try
        {
            var rel = await repositorio.ImportarAsync(empresaId, p.Plano, analise.Origem, analise.Dados.Sha256, usuarioId, progresso, ct);
            return Resultado<RelatorioImportacao>.Ok(rel);
        }
        catch (OperationCanceledException)
        {
            return Resultado<RelatorioImportacao>.Falha("Importação cancelada. Nada foi gravado.");
        }
        catch (ViolacaoRegraException ex)
        {
            return Resultado<RelatorioImportacao>.Falha($"A importação foi desfeita porque o banco recusou um dado: {ex.Message}");
        }
        catch (Npgsql.PostgresException ex)
        {
            return Resultado<RelatorioImportacao>.Falha($"A importação foi desfeita porque o banco recusou um dado: {ex.MessageText}");
        }
    }

    public async Task<IReadOnlyList<HistoricoImportacao>> HistoricoAsync(long empresaId, long usuarioId, CancellationToken ct = default)
        => await autorizador.VerificarAsync(usuarioId, empresaId, Exigida, ct) is null ? await historico.ListarAsync(empresaId, [RepositorioImportacaoEsocial.TipoHistorico], ct) : [];

    public async Task<RelatorioImportacao?> RelatorioAsync(long empresaId, long importacaoId, long usuarioId, CancellationToken ct = default)
        => await autorizador.VerificarAsync(usuarioId, empresaId, Exigida, ct) is null ? await historico.ObterRelatorioAsync(empresaId, importacaoId, ct) : null;

    public async Task<Resultado<RelatorioImportacao>> DesfazerAsync(long empresaId, long importacaoId, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Exigida, ct) is { } negado) return Resultado<RelatorioImportacao>.Falha(negado);
        try
        {
            return Resultado<RelatorioImportacao>.Ok(await repositorio.DesfazerAsync(empresaId, importacaoId, usuarioId, ct));
        }
        catch (ViolacaoRegraException ex)
        {
            return Resultado<RelatorioImportacao>.Falha(ex.Message.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
