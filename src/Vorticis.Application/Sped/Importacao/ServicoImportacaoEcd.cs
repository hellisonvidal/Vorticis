using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Domain.Sped.Importacao;

namespace Vorticis.Application.Sped.Importacao;

/// <summary>Regras comuns dos importadores: leitura, conferência do tipo e do CNPJ, relatório.</summary>
public static class AuxiliarImportacao
{
    public static Resultado<ArquivoSped> Abrir(byte[] arquivo, TipoArquivoSped esperado)
    {
        if (arquivo.Length == 0) return Resultado<ArquivoSped>.Falha("O arquivo está vazio.");
        var a = LeitorSped.Ler(arquivo);
        if (a.Tipo == TipoArquivoSped.Desconhecido)
            return Resultado<ArquivoSped>.Falha(a.Ocorrencias.FirstOrDefault(o => o.Gravidade == GravidadeOcorrencia.Erro)?.Motivo
                                                ?? "Não foi possível reconhecer o arquivo: ele não parece ser um arquivo SPED (ECD, ECF, EFD ICMS/IPI ou EFD-Contribuições).");
        if (a.Tipo != esperado)
            return Resultado<ArquivoSped>.Falha($"Este arquivo é {a.Tipo.Nome()}, mas esta tela importa {esperado.Nome()}. Use a tela de importação correspondente.");
        return Resultado<ArquivoSped>.Ok(a);
    }

    public static RelatorioImportacao Relatorio(long id, string arquivo, TipoArquivoSped tipo, string modo, IReadOnlyList<LinhaPrevia> previa, ResultadoGravacao g,
                                                IReadOnlyList<OcorrenciaImportacao> planejadas, bool incluirInfos = true)
    {
        var linhas = previa.Select(l =>
        {
            var imp = g.Importados.GetValueOrDefault(l.Registro);
            return new LinhaRelatorio(l.Registro, l.Descricao, imp, l.Existentes, Math.Max(l.Conflitos, l.Novos - imp > 0 ? l.Novos - imp : 0));
        }).ToList();
        foreach (var (reg, qtd) in g.Importados.Where(x => previa.All(p => p.Registro != x.Key)))
            linhas.Add(new LinhaRelatorio(reg, reg, qtd, 0, 0));
        var oc = planejadas.Concat(g.Ocorrencias).Where(o => incluirInfos || o.Gravidade != GravidadeOcorrencia.Informacao).ToList();
        return new RelatorioImportacao(id, arquivo, tipo.Nome(), modo, linhas, oc);
    }

    public static async Task<string?> NegarAsync(Autorizador a, long usuarioId, long empresaId, params Permissao[] permissoes)
    {
        foreach (var p in permissoes)
            if (await a.VerificarAsync(usuarioId, empresaId, p) is { } motivo) return motivo;
        return null;
    }
}

/// <summary>Integrações › Importar › Arquivo SPED – ECD: plano de contas, saldos e lançamentos de uma ECD de outro sistema.</summary>
public sealed class ServicoImportacaoEcd(IRepositorioImportacaoEcd repositorio, IRepositorioHistoricoSped historico, Autorizador autorizador) : IImportadorSped
{
    public TipoArquivoSped Tipo => TipoArquivoSped.Ecd;
    public string Titulo => "Arquivo SPED – ECD";

    public IReadOnlyList<ModoImportacao> Modos { get; } =
    [
        new("TUDO", "Tudo (plano, saldos e lançamentos)", "Importa o plano de contas, os centros de custo, os históricos, os saldos iniciais e todos os lançamentos do arquivo."),
        new("PLANO_SALDOS", "Plano de contas e saldos iniciais", "Importa o plano de contas, os centros de custo, os históricos e os saldos do início do período, sem os lançamentos."),
        new("PLANO", "Só o plano de contas", "Importa apenas o plano de contas, os centros de custo e os históricos."),
    ];

    private static Permissao[] Exigidas(string modo) => modo switch
    {
        "PLANO" => [Permissao.PlanoContasManter],
        "PLANO_SALDOS" => [Permissao.PlanoContasManter, Permissao.SaldosIniciais],
        _ => [Permissao.PlanoContasManter, Permissao.SaldosIniciais, Permissao.LancamentoIncluir],
    };

    private async Task<Resultado<(PlanoImportacaoEcd Plano, SituacaoContabil Situacao, ArquivoSped Arquivo, PreviaImportacao Previa, long? Anterior)>> PlanejarAsync(
        long empresaId, byte[] bytes, OpcoesImportacaoSped opcoes, long usuarioId, CancellationToken ct)
    {
        if (!Modos.Any(m => m.Codigo == opcoes.Modo)) return Resultado<(PlanoImportacaoEcd, SituacaoContabil, ArquivoSped, PreviaImportacao, long?)>.Falha("Escolha o que importar.");
        if (await AuxiliarImportacao.NegarAsync(autorizador, usuarioId, empresaId, Exigidas(opcoes.Modo)) is { } negado)
            return Resultado<(PlanoImportacaoEcd, SituacaoContabil, ArquivoSped, PreviaImportacao, long?)>.Falha(negado);
        var aberto = AuxiliarImportacao.Abrir(bytes, TipoArquivoSped.Ecd);
        if (!aberto.Sucesso) return Resultado<(PlanoImportacaoEcd, SituacaoContabil, ArquivoSped, PreviaImportacao, long?)>.Falha(aberto.Erros);
        var arq = aberto.Valor;
        var sit = await repositorio.SituacaoAsync(empresaId, ct);
        if (sit is null) return Resultado<(PlanoImportacaoEcd, SituacaoContabil, ArquivoSped, PreviaImportacao, long?)>.Falha("Empresa não encontrada.");
        var ecd = LeitorEcd.Ler(arq);
        var plano = PlanejadorEcd.Planejar(ecd, sit, opcoes);
        var oc = new List<OcorrenciaImportacao>(arq.Ocorrencias.Where(o => o.Gravidade != GravidadeOcorrencia.Informacao));
        oc.AddRange(plano.Ocorrencias.Where(o => !arq.Ocorrencias.Contains(o)));
        var confere = LeitorSped.Documento(sit.Cnpj) == ecd.Cabecalho.Cnpj;
        long? idCorresp = null; string? nomeCorresp = null;
        if (!confere)
        {
            if (await repositorio.EmpresaPorCnpjAsync(ecd.Cabecalho.Cnpj, ct) is { } e) (idCorresp, nomeCorresp) = (e.Id, e.Nome);
            oc.Insert(0, OcorrenciaImportacao.Erro(1, "0000", $"O CNPJ do arquivo ({Formatar(ecd.Cabecalho.Cnpj)} – {ecd.Cabecalho.Nome}) não é o da empresa ativa ({Formatar(sit.Cnpj)} – {sit.Nome}). " +
                (idCorresp is null ? "Cadastre essa empresa em Cadastros › Empresas e abra-a antes de importar." : $"A empresa {nomeCorresp} já está cadastrada: selecione-a e importe de novo.")));
        }
        var anterior = await historico.ImportacaoDoArquivoAsync(empresaId, "ECD", arq.Sha256, ct);
        if (anterior is not null)
            oc.Insert(0, OcorrenciaImportacao.Erro(0, "", "Este mesmo arquivo já foi importado nesta empresa (veja o Histórico de importações). Para importar de novo, desfaça a importação anterior."));
        var previa = new PreviaImportacao(ecd.Cabecalho, confere, idCorresp, nomeCorresp, plano.Linhas, oc, anterior);
        return Resultado<(PlanoImportacaoEcd, SituacaoContabil, ArquivoSped, PreviaImportacao, long?)>.Ok((plano, sit, arq, previa, anterior));
    }

    public static string Formatar(string cnpj) => cnpj.Length == 14 && cnpj.All(char.IsAsciiDigit)
        ? $"{cnpj[..2]}.{cnpj[2..5]}.{cnpj[5..8]}/{cnpj[8..12]}-{cnpj[12..]}" : cnpj;

    public async Task<Resultado<PreviaImportacao>> PreviaAsync(long empresaId, byte[] arquivo, string nomeArquivo, OpcoesImportacaoSped opcoes, long usuarioId,
                                                               CancellationToken ct = default)
    {
        var r = await PlanejarAsync(empresaId, arquivo, opcoes, usuarioId, ct);
        return r.Sucesso ? Resultado<PreviaImportacao>.Ok(r.Valor.Previa) : Resultado<PreviaImportacao>.Falha(r.Erros);
    }

    public async Task<Resultado<RelatorioImportacao>> ImportarAsync(long empresaId, byte[] arquivo, string nomeArquivo, OpcoesImportacaoSped opcoes, long usuarioId,
                                                                    IProgress<ProgressoImportacao>? progresso = null, CancellationToken ct = default)
    {
        var r = await PlanejarAsync(empresaId, arquivo, opcoes, usuarioId, ct);
        if (!r.Sucesso) return Resultado<RelatorioImportacao>.Falha(r.Erros);
        var (plano, _, arq, previa, anterior) = r.Valor;
        if (anterior is not null) return Resultado<RelatorioImportacao>.Falha("Este arquivo já foi importado nesta empresa. Desfaça a importação anterior para importar de novo.");
        if (!previa.CnpjConfere) return Resultado<RelatorioImportacao>.Falha(previa.Ocorrencias.First(o => o.Gravidade == GravidadeOcorrencia.Erro).Motivo);
        if (!previa.PodeImportar)
            return Resultado<RelatorioImportacao>.Falha(previa.Ocorrencias.Where(o => o.Gravidade == GravidadeOcorrencia.Erro && !o.Pontual).Take(10)
                .Select(o => o.Linha > 0 ? $"Linha {o.Linha}: {o.Motivo}" : o.Motivo));
        if (previa.ErrosPontuais > 0 && !opcoes.IgnorarComErro)
            return Resultado<RelatorioImportacao>.Falha(new[] { $"{previa.ErrosPontuais} erro(s) nos lançamentos impedem a importação. Corrija o arquivo ou marque \"Ignorar o que tiver erro\"." }
                .Concat(previa.Ocorrencias.Where(o => o.Gravidade == GravidadeOcorrencia.Erro).Take(10).Select(o => $"Linha {o.Linha}: {o.Motivo}")));
        var meta = new MetaImportacao(Path.GetFileName(nomeArquivo), arq.Sha256, opcoes.Modo, plano.Ecd.Cabecalho);
        try
        {
            var g = await repositorio.ImportarAsync(empresaId, plano, meta, opcoes.IgnorarComErro, usuarioId, progresso, ct);
            return Resultado<RelatorioImportacao>.Ok(AuxiliarImportacao.Relatorio(g.ImportacaoId, meta.Arquivo, TipoArquivoSped.Ecd, Modos.First(m => m.Codigo == opcoes.Modo).Titulo,
                                                                                    plano.Linhas, g, plano.Ocorrencias));
        }
        catch (OperationCanceledException)
        {
            return Resultado<RelatorioImportacao>.Falha("Importação cancelada. Nada foi gravado.");
        }
        catch (ViolacaoRegraException ex)
        {
            return Resultado<RelatorioImportacao>.Falha($"A importação foi desfeita porque o banco recusou um dado: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<HistoricoImportacao>> HistoricoAsync(long empresaId, long usuarioId, CancellationToken ct = default)
        => await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.PlanoContasManter, ct) is null ? await historico.ListarAsync(empresaId, ["ECD"], ct) : [];

    public async Task<Resultado<RelatorioImportacao>> DesfazerAsync(long empresaId, long importacaoId, long usuarioId, CancellationToken ct = default)
    {
        if (await AuxiliarImportacao.NegarAsync(autorizador, usuarioId, empresaId, Permissao.PlanoContasManter, Permissao.LancamentoIncluir) is { } negado)
            return Resultado<RelatorioImportacao>.Falha(negado);
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
