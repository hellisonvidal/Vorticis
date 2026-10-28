using Vorticis.Domain.Comum;
using Vorticis.Domain.Sped.Importacao;

namespace Vorticis.Application.Sped.Importacao;

/// <summary>
/// Importador de um tipo de arquivo SPED, usado pelo assistente da tela (escolha do arquivo → validação → prévia → opções → importar → relatório)
/// e pela opção "Desfazer importação". Cada módulo (Contábil, Fiscal) registra os seus.
/// </summary>
public interface IImportadorSped
{
    TipoArquivoSped Tipo { get; }
    string Titulo { get; }
    /// <summary>Formas de importar (ex.: só o plano de contas, plano e saldos, tudo). A primeira é a sugerida.</summary>
    IReadOnlyList<ModoImportacao> Modos { get; }

    /// <summary>Valida o leiaute e o CNPJ e mostra o que seria importado, sem gravar nada.</summary>
    Task<Resultado<PreviaImportacao>> PreviaAsync(long empresaId, byte[] arquivo, string nomeArquivo, OpcoesImportacaoSped opcoes, long usuarioId, CancellationToken ct = default);

    /// <summary>Importa numa única transação; o cancelamento desfaz tudo o que foi feito até ali.</summary>
    Task<Resultado<RelatorioImportacao>> ImportarAsync(long empresaId, byte[] arquivo, string nomeArquivo, OpcoesImportacaoSped opcoes, long usuarioId,
                                                       IProgress<ProgressoImportacao>? progresso = null, CancellationToken ct = default);

    Task<IReadOnlyList<HistoricoImportacao>> HistoricoAsync(long empresaId, long usuarioId, CancellationToken ct = default);

    /// <summary>Remove o que a importação criou, se ainda não foi usado, contabilizado ou fechado.</summary>
    Task<Resultado<RelatorioImportacao>> DesfazerAsync(long empresaId, long importacaoId, long usuarioId, CancellationToken ct = default);
}

/// <summary>Identificação da importação gravada no histórico.</summary>
public sealed record MetaImportacao(string Arquivo, string Sha256, string Modo, CabecalhoSped Cabecalho);

/// <summary>Resultado da gravação feita pelo repositório: o que foi criado por tipo de registro e o que deu errado.</summary>
public sealed record ResultadoGravacao(long ImportacaoId, IReadOnlyDictionary<string, int> Importados, IReadOnlyList<OcorrenciaImportacao> Ocorrencias);

/// <summary>Histórico de importações (compartilhado entre Contábil e Fiscal).</summary>
public interface IRepositorioHistoricoSped
{
    Task<IReadOnlyList<HistoricoImportacao>> ListarAsync(long empresaId, IReadOnlyList<string> tipos, CancellationToken ct);
    /// <summary>Importação ainda válida (não desfeita) do mesmo arquivo, se houver.</summary>
    Task<long?> ImportacaoDoArquivoAsync(long empresaId, string tipo, string sha256, CancellationToken ct);
    Task<RelatorioImportacao?> ObterRelatorioAsync(long empresaId, long importacaoId, CancellationToken ct);
}

public interface IRepositorioImportacaoEcd
{
    Task<SituacaoContabil?> SituacaoAsync(long empresaId, CancellationToken ct);
    /// <summary>Empresa cadastrada com o CNPJ do arquivo (id e nome), para oferecer a troca de empresa.</summary>
    Task<(long Id, string Nome)?> EmpresaPorCnpjAsync(string cnpj, CancellationToken ct);
    Task<ResultadoGravacao> ImportarAsync(long empresaId, PlanoImportacaoEcd plano, MetaImportacao meta, bool ignorarComErro, long usuarioId,
                                          IProgress<ProgressoImportacao>? progresso, CancellationToken ct);
    /// <summary>Remove o que a importação criou; lança <see cref="ViolacaoRegraException"/> (mensagens) se algo já foi usado, contabilizado ou fechado.</summary>
    Task<RelatorioImportacao> DesfazerAsync(long empresaId, long importacaoId, long usuarioId, CancellationToken ct);
}
