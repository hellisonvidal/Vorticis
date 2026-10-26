using Npgsql;
using Vorticis.Application.Seguranca;
using Vorticis.Domain.Comum;
using Vorticis.Domain.Seguranca;
using Vorticis.Fiscal.Documentos;
using Vorticis.Fiscal.Sped;

namespace Vorticis.Fiscal.Sefaz;

public enum SituacaoChave { Importada, JaImportada, SomenteResumo, NaoLocalizada, Baixada, JaCadastrada }

public sealed record ResultadoChave(SituacaoChave Situacao, string Mensagem, ResultadoArquivo? Arquivo = null);

/// <summary>
/// Consulta de uma chave para conferência na digitação (nada é gravado). <see cref="SituacaoChave.JaCadastrada"/>: a nota já está na empresa
/// (<see cref="Existente"/>; <see cref="Nota"/> traz o XML guardado, se houver); <see cref="SituacaoChave.Baixada"/>: XML completo vindo da SEFAZ.
/// </summary>
public sealed record ConsultaChave(SituacaoChave Situacao, string Mensagem, NotaPreparada? Nota = null, DocumentoFiscal? Existente = null);

/// <summary>
/// Importação individual de NF-e pela chave de acesso: consulta o Ambiente Nacional (NFeDistribuicaoDFe, como no Portal da NF-e) com o
/// certificado da empresa destinatária e importa o XML completo pelo mesmo caminho da importação de arquivos. Se só houver o resumo,
/// a nota ainda não teve manifestação do destinatário: <see cref="ManifestarCienciaAsync"/> registra a "Ciência da operação".
/// </summary>
public sealed class ServicoImportacaoChave(NpgsqlDataSource ds, ServicoDocumentosFiscais documentos, Autorizador autorizador)
{
    public static string Limpar(string? chave) => new((chave ?? "").Where(char.IsAsciiDigit).ToArray());

    public static string? Validar(string? chave)
    {
        var c = Limpar(chave);
        if (c.Length != 44) return "A chave de acesso tem 44 dígitos.";
        if (!CamposEfd.ChaveValida(c)) return "Chave de acesso inválida (dígito verificador não confere). Confira a digitação.";
        if (c.Substring(20, 2) is not ("55" or "65")) return "A chave não é de NF-e (modelo 55) nem de NFC-e (modelo 65).";
        return null;
    }

    private async Task<(string Cnpj, string Uf)?> EmpresaAsync(long empresaId, CancellationToken ct)
    {
        await using var cmd = ds.CreateCommand("SELECT cnpj, uf FROM core.empresa WHERE id = @e");
        cmd.Parameters.AddWithValue("e", empresaId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? (r.GetString(0).Trim(), r.GetString(1).Trim()) : null;
    }

    /// <summary>Procura a chave entre as notas já lançadas/importadas da empresa (e o XML guardado delas). Nunca acessa a SEFAZ.</summary>
    public async Task<Resultado<ConsultaChave?>> ProcurarLocalAsync(long empresaId, string chave, long usuarioId, CancellationToken ct = default)
    {
        if (Validar(chave) is { } erro) return Resultado<ConsultaChave?>.Falha(erro);
        chave = Limpar(chave);
        var achada = await documentos.LocalizarPorChaveAsync(empresaId, chave, usuarioId, ct);
        if (!achada.Sucesso) return Resultado<ConsultaChave?>.Falha(achada.Erros);
        if (achada.Valor is not { } doc) return Resultado<ConsultaChave?>.Ok(null);
        NotaPreparada? nota = null;
        var xml = await documentos.ObterXmlAsync(empresaId, doc.Id, usuarioId, ct);
        if (xml.Sucesso && xml.Valor.Length > 0)
        {
            var p = await documentos.PrepararAsync(empresaId, xml.Valor, usuarioId, ct);
            if (p.Sucesso) nota = p.Valor;
        }
        return Resultado<ConsultaChave?>.Ok(new ConsultaChave(SituacaoChave.JaCadastrada,
            $"A NF-e {doc.Numero} já está lançada nesta empresa ({doc.Operacao.Rotulo().ToLowerInvariant()}, {(doc.Digitado ? "digitada" : "XML importado")}).", nota, doc));
    }

    /// <summary>Baixa o XML completo da NF-e na SEFAZ (Distribuição DF-e, Ambiente Nacional) e o lê para conferência, sem gravar.</summary>
    public async Task<Resultado<ConsultaChave>> BaixarAsync(long empresaId, string chave, IClienteNfe cliente, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<ConsultaChave>(usuarioId, empresaId, Permissao.FiscalDocumentosImportar, ct) is { } negado) return negado;
        if (Validar(chave) is { } erro) return Resultado<ConsultaChave>.Falha(erro);
        chave = Limpar(chave);
        if (await EmpresaAsync(empresaId, ct) is not { } emp) return Resultado<ConsultaChave>.Falha("Empresa não encontrada.");
        if (CodigosUf.De(emp.Uf) is not { } uf) return Resultado<ConsultaChave>.Falha($"UF da empresa inválida ({emp.Uf}). Corrija em Cadastros › Empresas.");
        RespostaDistribuicao resp;
        try { resp = await cliente.ConsultarChaveAsync(emp.Cnpj, uf, chave, ct); }
        catch (ErroComunicacaoSefaz ex) { return Resultado<ConsultaChave>.Falha(ex.Message); }
        if (!resp.Localizado) return Resultado<ConsultaChave>.Ok(new ConsultaChave(SituacaoChave.NaoLocalizada, $"SEFAZ: {resp.CStat} - {resp.Motivo}"));
        if (resp.Documentos.FirstOrDefault(d => d.NotaCompleta) is { } completa)
        {
            var p = await documentos.PrepararAsync(empresaId, System.Text.Encoding.UTF8.GetBytes(completa.Xml), usuarioId, ct);
            return p.Sucesso
                ? Resultado<ConsultaChave>.Ok(new ConsultaChave(SituacaoChave.Baixada, "XML completo obtido na SEFAZ. Confira os dados e grave a nota.", p.Valor))
                : Resultado<ConsultaChave>.Falha(p.Erros);
        }
        return Resultado<ConsultaChave>.Ok(resp.Documentos.Any(d => d.Resumo)
            ? new ConsultaChave(SituacaoChave.SomenteResumo,
                "A SEFAZ devolveu só o resumo da nota (resNFe): para baixar o XML completo é preciso a manifestação do destinatário. Clique em \"Manifestar ciência\" e tente de novo em alguns minutos.")
            : new ConsultaChave(SituacaoChave.NaoLocalizada, "A SEFAZ não devolveu o XML da nota para esta empresa."));
    }

    public async Task<Resultado<ResultadoChave>> ImportarAsync(long empresaId, string chave, IClienteNfe cliente, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.NegarAsync<ResultadoChave>(usuarioId, empresaId, Permissao.FiscalDocumentosImportar, ct) is { } negado) return negado;
        if (Validar(chave) is { } erro) return Resultado<ResultadoChave>.Falha(erro);
        chave = Limpar(chave);
        if (await EmpresaAsync(empresaId, ct) is not { } emp) return Resultado<ResultadoChave>.Falha("Empresa não encontrada.");
        if (CodigosUf.De(emp.Uf) is not { } uf) return Resultado<ResultadoChave>.Falha($"UF da empresa inválida ({emp.Uf}). Corrija em Cadastros › Empresas.");

        RespostaDistribuicao resp;
        try { resp = await cliente.ConsultarChaveAsync(emp.Cnpj, uf, chave, ct); }
        catch (ErroComunicacaoSefaz ex) { return Resultado<ResultadoChave>.Falha(ex.Message); }

        if (!resp.Localizado)
            return Resultado<ResultadoChave>.Ok(new ResultadoChave(SituacaoChave.NaoLocalizada, $"SEFAZ: {resp.CStat} - {resp.Motivo}"));
        if (resp.Documentos.FirstOrDefault(d => d.NotaCompleta) is { } nota)
        {
            var r = await documentos.ImportarAsync(empresaId, [new ArquivoXml($"{chave}.xml", System.Text.Encoding.UTF8.GetBytes(nota.Xml))], usuarioId, ct: ct);
            if (!r.Sucesso) return Resultado<ResultadoChave>.Falha(r.Erros);
            var a = r.Valor.Arquivos.Single();
            return Resultado<ResultadoChave>.Ok(a.Situacao switch
            {
                SituacaoImportacao.Importado => new ResultadoChave(SituacaoChave.Importada, $"NF-e {a.Numero} importada ({a.Mensagem}).", a),
                SituacaoImportacao.Duplicado => new ResultadoChave(SituacaoChave.JaImportada, "Esta NF-e já estava importada.", a),
                _ => new ResultadoChave(SituacaoChave.NaoLocalizada, a.Mensagem, a),
            });
        }
        return Resultado<ResultadoChave>.Ok(resp.Documentos.Any(d => d.Resumo)
            ? new ResultadoChave(SituacaoChave.SomenteResumo,
                "A SEFAZ devolveu só o resumo da nota: falta a manifestação do destinatário. Clique em \"Manifestar ciência\" e importe de novo em alguns minutos.")
            : new ResultadoChave(SituacaoChave.NaoLocalizada, "A SEFAZ não devolveu o XML da nota para esta empresa."));
    }

    public async Task<Resultado> ManifestarCienciaAsync(long empresaId, string chave, IClienteNfe cliente, long usuarioId, CancellationToken ct = default)
    {
        if (await autorizador.VerificarAsync(usuarioId, empresaId, Permissao.FiscalDocumentosImportar, ct) is { } negado) return Resultado.Falha(negado);
        if (Validar(chave) is { } erro) return Resultado.Falha(erro);
        if (await EmpresaAsync(empresaId, ct) is not { } emp) return Resultado.Falha("Empresa não encontrada.");
        try
        {
            var r = await cliente.ManifestarCienciaAsync(emp.Cnpj, Limpar(chave), ct);
            return r.Registrado || r.CStat == 573   // 573 = duplicidade de evento: a ciência já estava registrada
                ? Resultado.Ok()
                : Resultado.Falha($"SEFAZ: {r.CStat} - {r.Motivo}");
        }
        catch (ErroComunicacaoSefaz ex) { return Resultado.Falha(ex.Message); }
    }
}
