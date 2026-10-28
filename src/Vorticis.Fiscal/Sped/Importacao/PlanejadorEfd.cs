using Vorticis.Domain.Sped.Importacao;

namespace Vorticis.Fiscal.Sped.Importacao;

/// <summary>O que a empresa já tem no Fiscal, para a importação não duplicar nada.</summary>
public sealed record SituacaoFiscal(
    string Cnpj, string Nome, string? InscricaoEstadual, bool Simples,
    IReadOnlyDictionary<string, decimal> DocsPorChave, IReadOnlyDictionary<string, decimal> DocsPorNumero,
    IReadOnlyDictionary<string, long> ProdutoPorVinculo, IReadOnlyDictionary<string, long> ProdutoPorGtin,
    IReadOnlyDictionary<string, long> ParticipantePorDocumento, IReadOnlyDictionary<string, long> ParticipantePorNome,
    IReadOnlySet<DateOnly> MesesFechados, IReadOnlySet<DateOnly> ApuracoesIcms, IReadOnlySet<DateOnly> ApuracoesPisCofins,
    IReadOnlySet<(DateOnly Data, string Motivo)> Inventarios, IReadOnlySet<(DateOnly Competencia, string Codigo, decimal Valor)> Ajustes)
{
    public static string ChaveNumero(bool entrada, string modelo, string serie, string numero, string? documentoParticipante) =>
        $"{(entrada ? 'E' : 'S')}|{modelo}|{serie.Trim()}|{numero.Trim().TrimStart('0')}|{documentoParticipante ?? ""}";
}

public sealed record ParticipantePlanejado(ParticipanteLidoEfd Origem, long? ExistenteId, bool Cliente, bool Fornecedor);
public sealed record ProdutoPlanejado(ProdutoLidoEfd Origem, long? ExistenteId);
public sealed record DocumentoPlanejado(DocumentoLidoEfd Doc, bool Existente, IReadOnlyList<string> Erros)
{
    public bool Importavel => !Existente && Erros.Count == 0;
}
public sealed record AjustePlanejado(AjusteLidoEfd Origem, string Imposto, char Sentido);

public sealed class PlanoImportacaoEfd
{
    public required EfdLida Efd { get; init; }
    public bool ImportaNotas { get; init; }
    public bool ImportaApuracao { get; init; }
    public IReadOnlyList<ParticipantePlanejado> Participantes { get; init; } = [];
    public IReadOnlyList<ProdutoPlanejado> Produtos { get; init; } = [];
    public IReadOnlyList<DocumentoPlanejado> Documentos { get; init; } = [];
    public IReadOnlyList<AjustePlanejado> Ajustes { get; init; } = [];
    public IReadOnlyList<ApuracaoIcmsLida> ApuracoesIcms { get; init; } = [];
    public IReadOnlyList<InventarioLidoEfd> Inventarios { get; init; } = [];
    public IReadOnlyList<ConsolidadoLidoEfd> Consolidados { get; init; } = [];
    public IReadOnlyList<ApuracaoContribLida> ApuracoesContrib { get; init; } = [];
    public IReadOnlyList<LinhaPrevia> Linhas { get; init; } = [];
    public IReadOnlyList<OcorrenciaImportacao> Ocorrencias { get; init; } = [];
}

/// <summary>Monta o plano de importação da EFD (sem gravar): o que é novo, o que já existe e o que tem erro.</summary>
public static class PlanejadorEfd
{
    private static readonly string[] ModelosSuportados = ["55", "65", "57", "NFSE"];

    public static PlanoImportacaoEfd Planejar(EfdLida efd, SituacaoFiscal sit, OpcoesImportacaoSped op)
    {
        var oc = new List<OcorrenciaImportacao>(efd.Ocorrencias);
        var cont = new ContadorImportacao();
        var importaNotas = op.Modo is "TUDO" or "NOTAS";
        var importaApuracao = op.Modo == "TUDO";
        var contrib = efd.Contribuicoes;

        // ------------------------------------------------------------ identificação da empresa
        if (!contrib && efd.Cabecalho.InscricaoEstadual is { Length: > 0 } ie && sit.InscricaoEstadual is { Length: > 0 } ieEmp
            && LeitorSped.Documento(ie) != LeitorSped.Documento(ieEmp))
            oc.Add(OcorrenciaImportacao.Aviso(1, "0000", $"A inscrição estadual do arquivo ({ie}) é diferente da cadastrada na empresa ({ieEmp})."));

        var porCod = efd.Participantes.GroupBy(p => p.Codigo).ToDictionary(g => g.Key, g => g.First());
        var docsCliente = new HashSet<string>(); var docsFornecedor = new HashSet<string>();
        foreach (var d in efd.Documentos.Where(d => d.CodPart is not null))
            (d.Entrada ? docsFornecedor : docsCliente).Add(d.CodPart!);

        // ------------------------------------------------------------ participantes
        var parts = new List<ParticipantePlanejado>();
        foreach (var p in efd.Participantes)
        {
            long? ex = null;
            if (p.Documento is { Length: > 0 } doc) { if (sit.ParticipantePorDocumento.TryGetValue(doc, out var id)) ex = id; }
            else if (sit.ParticipantePorNome.TryGetValue(p.Nome.Trim().ToUpperInvariant(), out var id2)) ex = id2;
            cont.Registrar("0150", "Participantes (clientes e fornecedores)", novo: ex is null, conflito: false, amostra: p.Nome);
            parts.Add(new ParticipantePlanejado(p, ex, docsCliente.Contains(p.Codigo), docsFornecedor.Contains(p.Codigo)));
        }

        // ------------------------------------------------------------ produtos
        var prods = new List<ProdutoPlanejado>();
        foreach (var p in efd.Produtos)
        {
            long? ex = null;
            if (sit.ProdutoPorVinculo.TryGetValue(p.Codigo, out var v)) ex = v;
            else if (p.Gtin is { Length: > 0 } g && sit.ProdutoPorGtin.TryGetValue(g, out var vg)) ex = vg;
            cont.Registrar("0200", "Produtos", novo: ex is null, conflito: false, amostra: $"{p.Codigo} {p.Descricao}");
            prods.Add(new ProdutoPlanejado(p, ex));
        }
        var unidades = efd.Produtos.Select(p => p.Unidade).Distinct().Count();
        if (unidades > 0) cont.Registrar("0190", "Unidades de medida (usadas nos produtos)", novo: false, conflito: false);

        // ------------------------------------------------------------ notas
        var docs = new List<DocumentoPlanejado>();
        if (importaNotas)
        {
            var vistasChave = new HashSet<string>(); var vistasNumero = new HashSet<string>();
            foreach (var d in efd.Documentos)
            {
                var reg = d.Registro;
                var descr = reg switch { "A100" => "Notas de serviço (NFS-e)", "D100" => "Conhecimentos de transporte (CT-e)", _ => "Notas fiscais (NF-e e NFC-e)" };
                var erros = new List<string>();
                if (d.Inutilizada)
                {
                    cont.Registrar(reg, descr, novo: false, conflito: false);
                    oc.Add(OcorrenciaImportacao.Info(d.Linha, reg, $"{d.Identificacao}: nota inutilizada, não importada."));
                    docs.Add(new DocumentoPlanejado(d, true, []));
                    continue;
                }
                if (!ModelosSuportados.Contains(d.Modelo)) erros.Add($"o modelo {d.Modelo} não é suportado pelo sistema (só NF-e 55, NFC-e 65, CT-e 57 e NFS-e)");
                if (d.DataEfetiva(efd.Cabecalho.Inicio) is null) erros.Add("data de emissão inválida");
                if (d.Numero.Length == 0) erros.Add("número da nota em branco");
                var data = d.DataEfetiva(efd.Cabecalho.Inicio);
                if (data is { } dt && sit.MesesFechados.Contains(new DateOnly(dt.Year, dt.Month, 1))) erros.Add($"o período fiscal de {dt:MM/yyyy} está fechado");
                if (d.CodPart is { } cp && !d.Cancelada && !d.Denegada && !porCod.ContainsKey(cp)) erros.Add($"participante {cp} não está no cadastro (0150) do arquivo");
                if (d.Chave is { Length: > 0 } ch && d.Modelo != "NFSE" && !(ch.Length == 44 && ch.All(char.IsAsciiDigit))) erros.Add($"chave de acesso inválida ({ch})");
                var docPart = d.CodPart is not null && porCod.TryGetValue(d.CodPart, out var pp) ? pp.Documento : null;
                var chaveNum = SituacaoFiscal.ChaveNumero(d.Entrada, d.Modelo, d.Serie, d.Numero, docPart);
                var existente = false; var conflito = false;
                if (erros.Count == 0)
                {
                    if (d.Chave is { Length: > 0 } c1)
                    {
                        if (!vistasChave.Add(c1)) erros.Add("a mesma chave de acesso aparece mais de uma vez no arquivo");
                        else if (sit.DocsPorChave.TryGetValue(c1, out var tot)) { existente = true; conflito = d.Situacao == "AUTORIZADA" && Math.Abs(tot - d.Total) > 0.01m; }
                    }
                    else
                    {
                        if (!vistasNumero.Add(chaveNum)) erros.Add("o mesmo número, série e participante aparecem mais de uma vez no arquivo");
                        else if (sit.DocsPorNumero.TryGetValue(chaveNum, out var tot)) { existente = true; conflito = d.Situacao == "AUTORIZADA" && Math.Abs(tot - d.Total) > 0.01m; }
                    }
                }
                if (existente && conflito)
                    oc.Add(OcorrenciaImportacao.Aviso(d.Linha, reg, $"{d.Identificacao}: a nota já existe no sistema com valor diferente do arquivo; foi mantida a do sistema."));
                if (erros.Count > 0) oc.Add(OcorrenciaImportacao.ErroPontual(d.Linha, reg, $"{d.Identificacao}: {string.Join("; ", erros)}."));
                cont.Registrar(reg, descr, novo: erros.Count == 0 && !existente, conflito: erros.Count > 0, amostra: d.Identificacao + (d.Total > 0 ? $" - {d.Total:N2}" : ""));
                if (reg == "C100" && erros.Count == 0 && !existente) cont.Registrar("C170", "Itens das notas", novo: true, conflito: false);
                docs.Add(new DocumentoPlanejado(d, existente && erros.Count == 0, erros));
            }
        }

        // ------------------------------------------------------------ apuração, ajustes, inventário e consolidados
        var ajustes = new List<AjustePlanejado>();
        var apIcms = new List<ApuracaoIcmsLida>();
        var invs = new List<InventarioLidoEfd>();
        var cons = new List<ConsolidadoLidoEfd>();
        var apContrib = new List<ApuracaoContribLida>();
        if (importaApuracao)
        {
            foreach (var a in efd.ApuracoesIcms)
            {
                var novo = !sit.ApuracoesIcms.Contains(a.Competencia);
                cont.Registrar("E110", "Apuração do ICMS (histórico)", novo, conflito: false, amostra: $"{a.Competencia:MM/yyyy} - a recolher {a.IcmsRecolher:N2}");
                if (novo) apIcms.Add(a);
            }
            foreach (var a in efd.Ajustes)
            {
                var (ok, imposto, sentido, motivo) = ClassificarAjuste(a);
                if (!ok) { oc.Add(OcorrenciaImportacao.ErroPontual(a.Linha, a.Registro, $"Ajuste {a.Codigo}: {motivo}.")); cont.Registrar(a.Registro, "Ajustes da apuração", novo: false, conflito: true); continue; }
                var novo = !sit.Ajustes.Contains((a.Competencia, a.Codigo, a.Valor));
                cont.Registrar(a.Registro, a.Registro == "E111" ? "Ajustes da apuração do ICMS" : "Ajustes da apuração do ICMS-ST", novo, conflito: false, amostra: $"{a.Codigo} {a.Valor:N2}");
                if (novo) ajustes.Add(new AjustePlanejado(a, imposto!, sentido));
            }
            foreach (var i in efd.Inventarios)
            {
                var novo = !sit.Inventarios.Contains((i.Data, i.Motivo.Length == 0 ? "01" : i.Motivo));
                cont.Registrar("H010", "Inventário (itens)", novo: novo && i.Itens.Count > 0, conflito: false, amostra: $"{i.Data:dd/MM/yyyy} - {i.Itens.Count} itens");
                if (novo && i.Itens.Count > 0) invs.Add(i);
            }
            foreach (var a in efd.ApuracoesContrib)
            {
                var novo = !sit.ApuracoesPisCofins.Contains(a.Competencia);
                cont.Registrar("M200", "Apuração de PIS/COFINS (histórico)", novo, conflito: false, amostra: $"{a.Competencia:MM/yyyy} - PIS {a.PisAPagar:N2} COFINS {a.CofinsAPagar:N2}");
                if (novo) apContrib.Add(a);
            }
        }
        if (importaNotas)
            foreach (var c in efd.Consolidados)
            {
                cont.Registrar(c.Registro, "Operações consolidadas (sem nota por nota)", novo: true, conflito: false, amostra: $"CFOP {c.Cfop} - {c.Valor:N2}");
                cons.Add(c);
            }
        foreach (var (reg, qtd) in efd.NaoImportados.OrderBy(x => x.Key))
        {
            oc.Add(OcorrenciaImportacao.Info(0, reg, $"O registro {reg} ({qtd} linha(s)) não é importado por esta versão do sistema."));
        }
        if (!importaNotas && efd.Documentos.Count > 0)
            oc.Add(OcorrenciaImportacao.Info(0, "", $"O arquivo tem {efd.Documentos.Count} nota(s); nesta opção só os cadastros são importados."));

        return new PlanoImportacaoEfd
        {
            Efd = efd, ImportaNotas = importaNotas, ImportaApuracao = importaApuracao, Participantes = parts, Produtos = prods, Documentos = docs,
            Ajustes = ajustes, ApuracoesIcms = apIcms, Inventarios = invs, Consolidados = cons, ApuracoesContrib = apContrib, Linhas = cont.Linhas(), Ocorrencias = oc,
        };
    }

    /// <summary>E111: código de 8 posições (UF + 0 + tipo + sequência); tipo 2 ou 3 = ajuste de entrada (créditos). E220: UF + 1 + tipo + sequência.</summary>
    public static (bool Ok, string? Imposto, char Sentido, string? Motivo) ClassificarAjuste(AjusteLidoEfd a)
    {
        var c = a.Codigo;
        if (a.Valor <= 0) return (false, null, 'S', "valor zerado ou negativo");
        if (c.Length != 8 || !char.IsAsciiLetterUpper(c[0]) || !char.IsAsciiLetterUpper(c[1]) || !c[3..].All(char.IsAsciiDigit) || c[2] is not ('0' or '1') || c[3] is < '0' or > '5')
            return (false, null, 'S', "código de ajuste fora do padrão da tabela 5.1.1 (UF + 8 posições)");
        var st = a.Registro == "E220";
        if ((c[2] == '1') != st) return (false, null, 'S', "o código não combina com o tipo do registro (ICMS próprio x ICMS-ST)");
        return (true, st ? "ICMS_ST" : "ICMS", c[3] is '2' or '3' ? 'E' : 'S', null);
    }
}
