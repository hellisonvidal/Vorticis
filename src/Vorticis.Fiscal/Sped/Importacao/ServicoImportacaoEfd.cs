using Vorticis.Application.Seguranca;
using Vorticis.Application.Sped.Importacao;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Domain.Sped.Importacao;

namespace Vorticis.Fiscal.Sped.Importacao;

public interface IRepositorioImportacaoEfd
{
    Task<SituacaoFiscal?> SituacaoAsync(long empresaId, CancellationToken ct);
    Task<(long Id, string Nome)?> EmpresaPorCnpjAsync(string cnpj, CancellationToken ct);
    Task<ResultadoGravacao> ImportarAsync(long empresaId, PlanoImportacaoEfd plano, MetaImportacao meta, bool ignorarComErro, long usuarioId,
                                          IProgress<ProgressoImportacao>? progresso, CancellationToken ct);
    Task<RelatorioImportacao> DesfazerAsync(long empresaId, long importacaoId, string tipo, long usuarioId, CancellationToken ct);
}

/// <summary>
/// Integrações › Importar › Arquivo SPED – EFD ICMS/IPI e EFD-Contribuições: cadastros (participantes, produtos), notas fiscais de entrada e saída
/// (NF-e, NFC-e, NFS-e e CT-e), apuração e ajustes, inventário e o histórico da apuração de PIS/COFINS de uma escrituração de outro sistema.
/// </summary>
public sealed class ServicoImportacaoEfd(TipoArquivoSped tipo, IRepositorioImportacaoEfd repositorio, IRepositorioHistoricoSped historico, Autorizador autorizador) : IImportadorSped
{
    public TipoArquivoSped Tipo => tipo;
    public string Titulo => tipo == TipoArquivoSped.EfdContribuicoes ? "Arquivo SPED – EFD-Contribuições" : "Arquivo SPED – EFD ICMS/IPI";

    public IReadOnlyList<ModoImportacao> Modos { get; } = tipo == TipoArquivoSped.EfdContribuicoes
        ?
        [
            new("TUDO", "Tudo (cadastros, notas e apuração de PIS/COFINS)", "Importa participantes, produtos, as notas de serviço e de mercadorias e o histórico da apuração de PIS e COFINS."),
            new("NOTAS", "Cadastros e notas", "Importa participantes, produtos e as notas fiscais, sem a apuração."),
            new("CADASTROS", "Só os cadastros", "Importa apenas participantes e produtos."),
        ]
        :
        [
            new("TUDO", "Tudo (cadastros, notas, apuração e inventário)", "Importa participantes, produtos, as notas de entrada e saída (NF-e, NFC-e e CT-e), o histórico e os ajustes da apuração do ICMS e o inventário."),
            new("NOTAS", "Cadastros e notas", "Importa participantes, produtos e as notas fiscais, sem a apuração e o inventário."),
            new("CADASTROS", "Só os cadastros", "Importa apenas participantes e produtos."),
        ];

    private string Codigo => tipo.Codigo();
    private Permissao[] Exigidas(string modo) => modo == "TUDO" ? [Permissao.FiscalDocumentosImportar, Permissao.FiscalApurar] : [Permissao.FiscalDocumentosImportar];

    private async Task<Resultado<(PlanoImportacaoEfd Plano, ArquivoSped Arquivo, PreviaImportacao Previa, long? Anterior)>> PlanejarAsync(
        long empresaId, byte[] bytes, OpcoesImportacaoSped opcoes, long usuarioId, CancellationToken ct)
    {
        if (!Modos.Any(m => m.Codigo == opcoes.Modo)) return Resultado<(PlanoImportacaoEfd, ArquivoSped, PreviaImportacao, long?)>.Falha("Escolha o que importar.");
        if (await AuxiliarImportacao.NegarAsync(autorizador, usuarioId, empresaId, Exigidas(opcoes.Modo)) is { } negado)
            return Resultado<(PlanoImportacaoEfd, ArquivoSped, PreviaImportacao, long?)>.Falha(negado);
        var aberto = AuxiliarImportacao.Abrir(bytes, tipo);
        if (!aberto.Sucesso) return Resultado<(PlanoImportacaoEfd, ArquivoSped, PreviaImportacao, long?)>.Falha(aberto.Erros);
        var arq = aberto.Valor;
        var sit = await repositorio.SituacaoAsync(empresaId, ct);
        if (sit is null) return Resultado<(PlanoImportacaoEfd, ArquivoSped, PreviaImportacao, long?)>.Falha("Empresa não encontrada.");
        var efd = LeitorEfd.Ler(arq);
        var plano = PlanejadorEfd.Planejar(efd, sit, opcoes);
        var oc = new List<OcorrenciaImportacao>(arq.Ocorrencias.Where(o => o.Gravidade != GravidadeOcorrencia.Informacao));
        oc.AddRange(plano.Ocorrencias.Where(o => !arq.Ocorrencias.Contains(o)));
        var confere = LeitorSped.Documento(sit.Cnpj) == efd.Cabecalho.Cnpj;
        long? idCorresp = null; string? nomeCorresp = null;
        if (!confere)
        {
            if (await repositorio.EmpresaPorCnpjAsync(efd.Cabecalho.Cnpj, ct) is { } e) (idCorresp, nomeCorresp) = (e.Id, e.Nome);
            oc.Insert(0, OcorrenciaImportacao.Erro(1, "0000", $"O CNPJ do arquivo ({ServicoImportacaoEcdFormato.Cnpj(efd.Cabecalho.Cnpj)} – {efd.Cabecalho.Nome}) não é o da empresa ativa " +
                $"({ServicoImportacaoEcdFormato.Cnpj(sit.Cnpj)} – {sit.Nome}). " +
                (idCorresp is null ? "Cadastre essa empresa em Cadastros › Empresas e abra-a antes de importar." : $"A empresa {nomeCorresp} já está cadastrada: selecione-a e importe de novo.")));
        }
        var anterior = await historico.ImportacaoDoArquivoAsync(empresaId, Codigo, arq.Sha256, ct);
        if (anterior is not null)
            oc.Insert(0, OcorrenciaImportacao.Erro(0, "", "Este mesmo arquivo já foi importado nesta empresa (veja o Histórico de importações). Para importar de novo, desfaça a importação anterior."));
        var previa = new PreviaImportacao(efd.Cabecalho, confere, idCorresp, nomeCorresp, plano.Linhas, oc, anterior);
        return Resultado<(PlanoImportacaoEfd, ArquivoSped, PreviaImportacao, long?)>.Ok((plano, arq, previa, anterior));
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
        if (previa.ErrosPontuais > 0 && !opcoes.IgnorarComErro)
            return Resultado<RelatorioImportacao>.Falha(new[] { $"{previa.ErrosPontuais} erro(s) nas notas impedem a importação. Corrija o arquivo ou marque \"Ignorar o que tiver erro\"." }
                .Concat(previa.Ocorrencias.Where(o => o.Gravidade == GravidadeOcorrencia.Erro).Take(10).Select(o => $"Linha {o.Linha}: {o.Motivo}")));
        var meta = new MetaImportacao(Path.GetFileName(nomeArquivo), arq.Sha256, opcoes.Modo, plano.Efd.Cabecalho);
        try
        {
            var g = await repositorio.ImportarAsync(empresaId, plano, meta, opcoes.IgnorarComErro, usuarioId, progresso, ct);
            return Resultado<RelatorioImportacao>.Ok(AuxiliarImportacao.Relatorio(g.ImportacaoId, meta.Arquivo, tipo, Modos.First(m => m.Codigo == opcoes.Modo).Titulo, plano.Linhas, g, plano.Ocorrencias));
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
        => await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.FiscalDocumentosImportar, ct) is null ? await historico.ListarAsync(empresaId, [Codigo], ct) : [];

    public async Task<Resultado<RelatorioImportacao>> DesfazerAsync(long empresaId, long importacaoId, long usuarioId, CancellationToken ct = default)
    {
        if (await AuxiliarImportacao.NegarAsync(autorizador, usuarioId, empresaId, Permissao.FiscalDocumentosImportar) is { } negado) return Resultado<RelatorioImportacao>.Falha(negado);
        try
        {
            return Resultado<RelatorioImportacao>.Ok(await repositorio.DesfazerAsync(empresaId, importacaoId, Codigo, usuarioId, ct));
        }
        catch (ViolacaoRegraException ex)
        {
            return Resultado<RelatorioImportacao>.Falha(ex.Message.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}

internal static class ServicoImportacaoEcdFormato
{
    public static string Cnpj(string cnpj) => Vorticis.Application.Sped.Importacao.ServicoImportacaoEcd.Formatar(cnpj);
}
