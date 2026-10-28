using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Domain.Sped.Importacao;

namespace Vorticis.Application.Sped.Importacao;

public interface IRepositorioImportacaoEcf
{
    Task<SituacaoEcf?> SituacaoAsync(long empresaId, CancellationToken ct);
    Task<(long Id, string Nome)?> EmpresaPorCnpjAsync(string cnpj, CancellationToken ct);
    Task<ResultadoGravacao> ImportarAsync(long empresaId, PlanoImportacaoEcf plano, MetaImportacao meta, long usuarioId, IProgress<ProgressoImportacao>? progresso, CancellationToken ct);
    Task<RelatorioImportacao> DesfazerAsync(long empresaId, long importacaoId, long usuarioId, CancellationToken ct);
}

/// <summary>Integrações › Importar › Arquivo SPED – ECF: e-Lalur/e-Lacs (Parte A), saldos da Parte B, apurações trimestrais do IRPJ/CSLL e, se faltar, o plano de contas.</summary>
public sealed class ServicoImportacaoEcf(IRepositorioImportacaoEcf repositorio, IRepositorioHistoricoSped historico, Autorizador autorizador) : IImportadorSped
{
    public TipoArquivoSped Tipo => TipoArquivoSped.Ecf;
    public string Titulo => "Arquivo SPED – ECF";

    public IReadOnlyList<ModoImportacao> Modos { get; } =
    [
        new("TUDO", "Tudo (e-Lalur/e-Lacs, Parte B, apurações e plano de contas)", "Importa os lançamentos do e-Lalur e do e-Lacs, os saldos e movimentos da Parte B, as apurações do IRPJ e da CSLL (como referência) e o plano de contas do arquivo, se a empresa ainda não tiver um."),
        new("LALUR", "Só e-Lalur/e-Lacs, Parte B e apurações", "Importa os lançamentos do e-Lalur e do e-Lacs, a Parte B e as apurações, sem mexer no plano de contas."),
    ];

    private static Permissao[] Exigidas(string modo) => modo == "TUDO" ? [Permissao.SpedEcfGerar, Permissao.PlanoContasManter] : [Permissao.SpedEcfGerar];

    private async Task<Resultado<(PlanoImportacaoEcf Plano, ArquivoSped Arquivo, PreviaImportacao Previa, long? Anterior)>> PlanejarAsync(
        long empresaId, byte[] bytes, OpcoesImportacaoSped opcoes, long usuarioId, CancellationToken ct)
    {
        if (!Modos.Any(m => m.Codigo == opcoes.Modo)) return Resultado<(PlanoImportacaoEcf, ArquivoSped, PreviaImportacao, long?)>.Falha("Escolha o que importar.");
        if (await AuxiliarImportacao.NegarAsync(autorizador, usuarioId, empresaId, Exigidas(opcoes.Modo)) is { } negado)
            return Resultado<(PlanoImportacaoEcf, ArquivoSped, PreviaImportacao, long?)>.Falha(negado);
        var aberto = AuxiliarImportacao.Abrir(bytes, TipoArquivoSped.Ecf);
        if (!aberto.Sucesso) return Resultado<(PlanoImportacaoEcf, ArquivoSped, PreviaImportacao, long?)>.Falha(aberto.Erros);
        var arq = aberto.Valor;
        var sit = await repositorio.SituacaoAsync(empresaId, ct);
        if (sit is null) return Resultado<(PlanoImportacaoEcf, ArquivoSped, PreviaImportacao, long?)>.Falha("Empresa não encontrada.");
        var ecf = LeitorEcf.Ler(arq);
        var plano = PlanejadorEcf.Planejar(ecf, sit, opcoes);
        var oc = new List<OcorrenciaImportacao>(arq.Ocorrencias.Where(o => o.Gravidade != GravidadeOcorrencia.Informacao));
        oc.AddRange(plano.Ocorrencias.Where(o => !arq.Ocorrencias.Contains(o)));
        var confere = LeitorSped.Documento(sit.Contabil.Cnpj) == ecf.Cabecalho.Cnpj;
        long? idCorresp = null; string? nomeCorresp = null;
        if (!confere)
        {
            if (await repositorio.EmpresaPorCnpjAsync(ecf.Cabecalho.Cnpj, ct) is { } e) (idCorresp, nomeCorresp) = (e.Id, e.Nome);
            oc.Insert(0, OcorrenciaImportacao.Erro(1, "0000", $"O CNPJ do arquivo ({ServicoImportacaoEcd.Formatar(ecf.Cabecalho.Cnpj)} – {ecf.Cabecalho.Nome}) não é o da empresa ativa " +
                $"({ServicoImportacaoEcd.Formatar(sit.Contabil.Cnpj)} – {sit.Contabil.Nome}). " +
                (idCorresp is null ? "Cadastre essa empresa em Cadastros › Empresas e abra-a antes de importar." : $"A empresa {nomeCorresp} já está cadastrada: selecione-a e importe de novo.")));
        }
        var anterior = await historico.ImportacaoDoArquivoAsync(empresaId, "ECF", arq.Sha256, ct);
        if (anterior is not null)
            oc.Insert(0, OcorrenciaImportacao.Erro(0, "", "Este mesmo arquivo já foi importado nesta empresa (veja o Histórico de importações). Para importar de novo, desfaça a importação anterior."));
        var previa = new PreviaImportacao(ecf.Cabecalho, confere, idCorresp, nomeCorresp, plano.Linhas, oc, anterior);
        return Resultado<(PlanoImportacaoEcf, ArquivoSped, PreviaImportacao, long?)>.Ok((plano, arq, previa, anterior));
    }

    public async Task<Resultado<PreviaImportacao>> PreviaAsync(long empresaId, byte[] arquivo, string nomeArquivo, OpcoesImportacaoSped opcoes, long usuarioId, CancellationToken ct = default)
    {
        var r = await PlanejarAsync(empresaId, arquivo, opcoes, usuarioId, ct);
        return r.Sucesso ? Resultado<PreviaImportacao>.Ok(r.Valor.Previa) : Resultado<PreviaImportacao>.Falha(r.Erros);
    }

    public async Task<Resultado<RelatorioImportacao>> ImportarAsync(long empresaId, byte[] arquivo, string nomeArquivo, OpcoesImportacaoSped opcoes, long usuarioId,
                                                                    IProgress<ProgressoImportacao>? progresso = null, CancellationToken ct = default)
    {
        var r = await PlanejarAsync(empresaId, arquivo, opcoes, usuarioId, ct);
        if (!r.Sucesso) return Resultado<RelatorioImportacao>.Falha(r.Erros);
        var (plano, arq, previa, anterior) = r.Valor;
        if (anterior is not null) return Resultado<RelatorioImportacao>.Falha("Este arquivo já foi importado nesta empresa. Desfaça a importação anterior para importar de novo.");
        if (!previa.CnpjConfere) return Resultado<RelatorioImportacao>.Falha(previa.Ocorrencias.First(o => o.Gravidade == GravidadeOcorrencia.Erro).Motivo);
        if (!previa.PodeImportar)
            return Resultado<RelatorioImportacao>.Falha(previa.Ocorrencias.Where(o => o.Gravidade == GravidadeOcorrencia.Erro && !o.Pontual).Take(10)
                .Select(o => o.Linha > 0 ? $"Linha {o.Linha}: {o.Motivo}" : o.Motivo));
        var meta = new MetaImportacao(Path.GetFileName(nomeArquivo), arq.Sha256, opcoes.Modo, plano.Ecf.Cabecalho);
        try
        {
            var g = await repositorio.ImportarAsync(empresaId, plano, meta, usuarioId, progresso, ct);
            return Resultado<RelatorioImportacao>.Ok(AuxiliarImportacao.Relatorio(g.ImportacaoId, meta.Arquivo, TipoArquivoSped.Ecf, Modos.First(m => m.Codigo == opcoes.Modo).Titulo, plano.Linhas, g, plano.Ocorrencias));
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
        => await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.SpedEcfGerar, ct) is null ? await historico.ListarAsync(empresaId, ["ECF"], ct) : [];

    public async Task<Resultado<RelatorioImportacao>> DesfazerAsync(long empresaId, long importacaoId, long usuarioId, CancellationToken ct = default)
    {
        if (await AuxiliarImportacao.NegarAsync(autorizador, usuarioId, empresaId, Permissao.SpedEcfGerar) is { } negado) return Resultado<RelatorioImportacao>.Falha(negado);
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
