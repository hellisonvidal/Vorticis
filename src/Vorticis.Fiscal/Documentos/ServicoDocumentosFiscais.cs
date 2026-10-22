using System.IO.Compression;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Fiscal.Documentos;

/// <summary>Dados da empresa necessários para identificar entrada/saída (CNPJ da matriz e dos estabelecimentos).</summary>
public sealed record EmpresaFiscal(long Id, string Cnpj, IReadOnlyList<string> CnpjsEstabelecimentos, string RazaoSocial, DateOnly InicioAtividade)
{
    public IEnumerable<string> Cnpjs => CnpjsEstabelecimentos.Append(Cnpj);
}

/// <summary>Documento gravado (sem os itens, que vêm por <see cref="IRepositorioDocumentos.ListarItensAsync"/>).</summary>
public sealed record DocumentoFiscal(
    long Id, ModeloDocumento Modelo, string Chave, TipoOperacao Operacao, string Numero, string Serie, DateOnly Emissao, DateOnly DataLivro,
    string? NaturezaOperacao, SituacaoDocumento Situacao, ParteDocumento Emitente, ParteDocumento? Destinatario, long? ParticipanteId,
    string? ParticipanteCodigo, TotaisDocumento Totais, long? LancamentoId, bool Contabilizado,
    string Origem = "XML", long? CentroCustoId = null, string? CentroCustoCodigo = null)
{
    public bool Digitado => Origem == "DIGITADO";

    /// <summary>Nome da outra parte: cliente (saída) ou fornecedor (entrada).</summary>
    public string? NomeParticipante => Operacao == TipoOperacao.Entrada ? Emitente.Nome : Destinatario?.Nome;
    public string? DocumentoParticipante => Operacao == TipoOperacao.Entrada ? Emitente.Documento : Destinatario?.Documento;
}

public sealed record FiltroDocumentos(
    DateOnly? Inicio = null, DateOnly? Fim = null, TipoOperacao? Operacao = null, ModeloDocumento? Modelo = null,
    SituacaoDocumento? Situacao = null, string? Texto = null, string? Cfop = null, bool? Contabilizado = null, int Limite = 5000);

/// <summary>Resultado de gravar um documento: <see cref="Duplicado"/> quando a chave já existia na empresa.</summary>
public sealed record GravacaoDocumento(bool Duplicado, long DocumentoId, bool ParticipanteCriado, string? ParticipanteNome);

public sealed record CancelamentoRegistrado(bool Encontrado, bool JaCancelado, bool Contabilizado, long DocumentoId, string? Numero);

public interface IRepositorioDocumentos
{
    Task<EmpresaFiscal?> ObterEmpresaAsync(long empresaId, CancellationToken ct);

    /// <summary>Grava documento e itens numa transação; vincula (ou cria) o participante. Chave já importada: <see cref="GravacaoDocumento.Duplicado"/>.</summary>
    Task<GravacaoDocumento> GravarAsync(long empresaId, DocumentoLido documento, Identificacao identificacao, long usuarioId, CancellationToken ct);

    /// <summary>Marca o documento da chave como cancelado (evento de cancelamento importado).</summary>
    Task<CancelamentoRegistrado> CancelarPorChaveAsync(long empresaId, string chave, CancellationToken ct);

    Task<IReadOnlyList<DocumentoFiscal>> ListarAsync(long empresaId, FiltroDocumentos filtro, CancellationToken ct);
    Task<IReadOnlyList<ItemFiscal>> ListarItensDoDocumentoAsync(long documentoId, CancellationToken ct);

    /// <summary>Itens de todos os documentos da empresa no período (data do livro), com os dados do documento.</summary>
    Task<IReadOnlyList<LinhaItemFiscal>> ListarItensAsync(long empresaId, DateOnly inicio, DateOnly fim, TipoOperacao? operacao, CancellationToken ct);

    /// <summary>Exclui documentos que não estão contabilizados; devolve quantos foram excluídos.</summary>
    Task<int> ExcluirAsync(long empresaId, IReadOnlyCollection<long> ids, CancellationToken ct);

    /// <summary>XML original (descompactado) do documento.</summary>
    Task<byte[]?> ObterXmlAsync(long empresaId, long documentoId, CancellationToken ct);
}

public enum SituacaoImportacao { Importado, Duplicado, Cancelado, Ignorado, Erro }

public sealed record ArquivoXml(string Nome, byte[] Conteudo);

public sealed record ResultadoArquivo(string Arquivo, SituacaoImportacao Situacao, string Mensagem, string? Chave = null, string? Numero = null,
                                      TipoOperacao? Operacao = null, decimal Valor = 0);

public sealed record ResultadoImportacao(IReadOnlyList<ResultadoArquivo> Arquivos)
{
    public int Importados => Arquivos.Count(a => a.Situacao == SituacaoImportacao.Importado);
    public int Duplicados => Arquivos.Count(a => a.Situacao == SituacaoImportacao.Duplicado);
    public int Cancelamentos => Arquivos.Count(a => a.Situacao == SituacaoImportacao.Cancelado);
    public int Ignorados => Arquivos.Count(a => a.Situacao == SituacaoImportacao.Ignorado);
    public int Erros => Arquivos.Count(a => a.Situacao == SituacaoImportacao.Erro);

    public string Resumo => $"{Arquivos.Count} arquivo(s): {Importados} importado(s), {Duplicados} já importado(s), {Cancelamentos} cancelamento(s), " +
                            $"{Ignorados} ignorado(s), {Erros} com erro.";
}

/// <summary>
/// Documentos fiscais eletrônicos: importação de XML (NF-e/NFC-e e NFS-e nacional), consulta e livros. Toda operação verifica
/// a permissão do usuário na empresa antes de qualquer outra regra.
/// </summary>
public sealed class ServicoDocumentosFiscais(IRepositorioDocumentos repositorio, Autorizador autorizador)
{
    // ------------------------------------------------------------------ importação

    /// <summary>
    /// Importa os arquivos: cada um é gravado em transação própria, de modo que um arquivo com erro não impede os demais.
    /// Arquivos .zip têm os XMLs de dentro importados. Chaves já importadas são ignoradas (deduplicação por empresa).
    /// </summary>
    public async Task<Resultado<ResultadoImportacao>> ImportarAsync(long empresaId, IEnumerable<ArquivoXml> arquivos, long usuarioId,
                                                                   IProgress<int>? progresso = null, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<ResultadoImportacao>(usuarioId, empresaId, Permissao.FiscalDocumentosImportar, ct) is { } negado) return negado;
        var empresa = await repositorio.ObterEmpresaAsync(empresaId, ct);
        if (empresa is null) return Resultado<ResultadoImportacao>.Falha("Empresa não encontrada.");

        var resultados = new List<ResultadoArquivo>();
        foreach (var arquivo in Expandir(arquivos))
        {
            ct.ThrowIfCancellationRequested();
            resultados.Add(await ImportarUmAsync(empresa, arquivo, usuarioId, ct));
            progresso?.Report(resultados.Count);
        }
        return Resultado<ResultadoImportacao>.Ok(new ResultadoImportacao(resultados));
    }

    /// <summary>Importa os .xml (e .zip) de uma pasta, opcionalmente com as subpastas.</summary>
    public Task<Resultado<ResultadoImportacao>> ImportarPastaAsync(long empresaId, string pasta, bool subpastas, long usuarioId,
                                                                  IProgress<int>? progresso = null, CancellationToken ct = default)
    {
        if (!Directory.Exists(pasta)) return Task.FromResult(Resultado<ResultadoImportacao>.Falha("Pasta não encontrada."));
        var opcao = subpastas ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var caminhos = Directory.EnumerateFiles(pasta, "*.*", opcao)
            .Where(f => f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        return ImportarAsync(empresaId, LerArquivos(caminhos), usuarioId, progresso, ct);
    }

    public Task<Resultado<ResultadoImportacao>> ImportarArquivosAsync(long empresaId, IEnumerable<string> caminhos, long usuarioId,
                                                                     IProgress<int>? progresso = null, CancellationToken ct = default)
        => ImportarAsync(empresaId, LerArquivos(caminhos), usuarioId, progresso, ct);

    private static IEnumerable<ArquivoXml> LerArquivos(IEnumerable<string> caminhos)
    {
        foreach (var c in caminhos)
        {
            byte[]? conteudo = null;
            try { conteudo = File.ReadAllBytes(c); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            yield return new ArquivoXml(Path.GetFileName(c), conteudo ?? []);
        }
    }

    private static IEnumerable<ArquivoXml> Expandir(IEnumerable<ArquivoXml> arquivos)
    {
        foreach (var a in arquivos)
        {
            if (!a.Nome.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) { yield return a; continue; }
            foreach (var interno in ExtrairZip(a)) yield return interno;
        }
    }

    private static List<ArquivoXml> ExtrairZip(ArquivoXml a)
    {
        var internos = new List<ArquivoXml>();
        try
        {
            using var zip = new ZipArchive(new MemoryStream(a.Conteudo), ZipArchiveMode.Read);
            foreach (var e in zip.Entries.Where(e => e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
            {
                using var ms = new MemoryStream();
                using (var s = e.Open()) s.CopyTo(ms);
                internos.Add(new ArquivoXml($"{a.Nome}/{e.FullName}", ms.ToArray()));
            }
            return internos;
        }
        catch (InvalidDataException)
        {
            return [new ArquivoXml(a.Nome, [])];   // vira erro "arquivo vazio ou ilegível"
        }
    }

    private async Task<ResultadoArquivo> ImportarUmAsync(EmpresaFiscal empresa, ArquivoXml arquivo, long usuarioId, CancellationToken ct)
    {
        if (arquivo.Conteudo.Length == 0) return new(arquivo.Nome, SituacaoImportacao.Erro, "Arquivo vazio ou ilegível.");
        var leitura = LeitorXmlFiscal.Ler(arquivo.Conteudo);
        switch (leitura)
        {
            case LeituraInvalida inv:
                return new(arquivo.Nome, inv.Motivo.StartsWith("Evento", StringComparison.Ordinal) ? SituacaoImportacao.Ignorado : SituacaoImportacao.Erro, inv.Motivo);

            case LeituraCancelamento canc:
            {
                var r = await repositorio.CancelarPorChaveAsync(empresa.Id, canc.Chave, ct);
                if (!r.Encontrado) return new(arquivo.Nome, SituacaoImportacao.Ignorado, "Cancelamento de documento que não está importado.", canc.Chave);
                if (r.JaCancelado) return new(arquivo.Nome, SituacaoImportacao.Duplicado, "Documento já estava cancelado.", canc.Chave, r.Numero);
                return new(arquivo.Nome, SituacaoImportacao.Cancelado,
                    r.Contabilizado ? $"Documento {r.Numero} cancelado. Atenção: ele está contabilizado; desfaça a contabilização." : $"Documento {r.Numero} cancelado.",
                    canc.Chave, r.Numero);
            }

            case LeituraDocumento lido:
            {
                var doc = lido.Documento;
                var ident = IdentificadorOperacao.Identificar(doc, empresa.Cnpjs);
                if (!ident.Sucesso) return new(arquivo.Nome, SituacaoImportacao.Erro, string.Join(" ", ident.Erros), doc.Chave, doc.Numero);
                try
                {
                    var g = await repositorio.GravarAsync(empresa.Id, doc, ident.Valor, usuarioId, ct);
                    if (g.Duplicado) return new(arquivo.Nome, SituacaoImportacao.Duplicado, "Documento já importado (mesma chave de acesso).", doc.Chave, doc.Numero, ident.Valor.Operacao, doc.Totais.Total);
                    var aviso = Math.Abs(doc.DiferencaItens) > 0.05m ? $" Atenção: a soma dos itens difere do total da nota em {Formato.Moeda(doc.DiferencaItens)}." : "";
                    var part = g.ParticipanteCriado ? $" Participante cadastrado: {g.ParticipanteNome}." : "";
                    return new(arquivo.Nome, SituacaoImportacao.Importado,
                        $"{doc.Modelo.Rotulo()} {doc.Numero} ({ident.Valor.Operacao.Rotulo().ToLowerInvariant()}) importada.{part}{aviso}", doc.Chave, doc.Numero, ident.Valor.Operacao, doc.Totais.Total);
                }
                catch (ViolacaoRegraException ex)
                {
                    return new(arquivo.Nome, SituacaoImportacao.Erro, ex.Message, doc.Chave, doc.Numero);
                }
            }
            default:
                return new(arquivo.Nome, SituacaoImportacao.Erro, "Arquivo não reconhecido.");
        }
    }

    // ------------------------------------------------------------------ consultas

    public async Task<Resultado<IReadOnlyList<DocumentoFiscal>>> ConsultarAsync(long empresaId, FiltroDocumentos filtro, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<IReadOnlyList<DocumentoFiscal>>(usuarioId, empresaId, Permissao.FiscalDocumentosConsultar, ct) is { } negado) return negado;
        return Resultado<IReadOnlyList<DocumentoFiscal>>.Ok(await repositorio.ListarAsync(empresaId, filtro, ct));
    }

    public async Task<Resultado<IReadOnlyList<ItemFiscal>>> ItensAsync(long empresaId, long documentoId, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<IReadOnlyList<ItemFiscal>>(usuarioId, empresaId, Permissao.FiscalDocumentosConsultar, ct) is { } negado) return negado;
        return Resultado<IReadOnlyList<ItemFiscal>>.Ok(await repositorio.ListarItensDoDocumentoAsync(documentoId, ct));
    }

    public async Task<Resultado<byte[]>> ObterXmlAsync(long empresaId, long documentoId, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<byte[]>(usuarioId, empresaId, Permissao.FiscalDocumentosConsultar, ct) is { } negado) return negado;
        return await repositorio.ObterXmlAsync(empresaId, documentoId, ct) is { } xml ? Resultado<byte[]>.Ok(xml) : Resultado<byte[]>.Falha("Documento não encontrado.");
    }

    /// <summary>Linhas do Livro de Entradas (<paramref name="operacao"/> = Entrada) ou de Saídas, em ordem de data e número.</summary>
    public async Task<Resultado<IReadOnlyList<LinhaLivro>>> LivroAsync(long empresaId, TipoOperacao operacao, DateOnly inicio, DateOnly fim, long usuarioId,
                                                                      CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<IReadOnlyList<LinhaLivro>>(usuarioId, empresaId, Permissao.FiscalRelatorios, ct) is { } negado) return negado;
        if (fim < inicio) return Resultado<IReadOnlyList<LinhaLivro>>.Falha("Data final anterior à inicial.");
        var itens = await repositorio.ListarItensAsync(empresaId, inicio, fim, operacao, ct);
        return Resultado<IReadOnlyList<LinhaLivro>>.Ok(LivrosFiscais.Montar(itens, operacao));
    }

    /// <summary>Resumo por CFOP (entradas e/ou saídas) do período.</summary>
    public async Task<Resultado<IReadOnlyList<ResumoCfop>>> ResumoPorCfopAsync(long empresaId, TipoOperacao? operacao, DateOnly inicio, DateOnly fim,
                                                                              long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<IReadOnlyList<ResumoCfop>>(usuarioId, empresaId, Permissao.FiscalRelatorios, ct) is { } negado) return negado;
        if (fim < inicio) return Resultado<IReadOnlyList<ResumoCfop>>.Falha("Data final anterior à inicial.");
        var itens = await repositorio.ListarItensAsync(empresaId, inicio, fim, operacao, ct);
        return Resultado<IReadOnlyList<ResumoCfop>>.Ok(LivrosFiscais.ResumirPorCfop(itens));
    }

    public async Task<Resultado<int>> ExcluirAsync(long empresaId, IReadOnlyCollection<long> ids, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<int>(usuarioId, empresaId, Permissao.FiscalDocumentosImportar, ct) is { } negado) return negado;
        if (ids.Count == 0) return Resultado<int>.Falha("Selecione ao menos um documento.");
        var n = await repositorio.ExcluirAsync(empresaId, ids, ct);
        return n == 0 ? Resultado<int>.Falha("Nenhum documento excluído: documentos contabilizados precisam ter a contabilização desfeita antes.")
                      : Resultado<int>.Ok(n);
    }
}
