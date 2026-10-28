namespace Vorticis.Fiscal.Sped;

/// <summary>Natureza do ajuste da apuração do ICMS (4ª posição do código da tabela 5.1.1) e o campo do E110 que ele soma.</summary>
public enum TipoAjusteIcms { OutrosDebitos = 0, EstornoCreditos = 1, OutrosCreditos = 2, EstornoDebitos = 3, Deducoes = 4, DebitosEspeciais = 5 }

/// <summary>Imposto a que o ajuste pertence: ICMS próprio (E111), ICMS-ST (E220) ou IPI (E530).</summary>
public static class ImpostosAjuste
{
    public const string Icms = "ICMS", IcmsSt = "ICMS_ST", Ipi = "IPI";
    public static string Rotulo(string imposto) => imposto switch { IcmsSt => "ICMS-ST", Ipi => "IPI", _ => "ICMS" };
}

/// <summary>
/// Ajuste da apuração lançado na competência: do ICMS próprio (E111), do ICMS-ST (E220) ou do IPI (E530). <paramref name="Sentido"/> é E (entrada: outros créditos
/// e estornos de débito; no IPI, crédito) ou S (saída: outros débitos, estornos de crédito, deduções e débitos especiais; no IPI, débito). A observação vai no E112 e o
/// documento fiscal relacionado (participante, modelo, série, número e data) no E113 do ICMS próprio.
/// </summary>
public sealed record AjusteIcmsEfd(long Id, DateOnly Competencia, string Codigo, string Descricao, decimal Valor, string Imposto = ImpostosAjuste.Icms, char? Sentido = null,
                                   string? Observacao = null, long? DocParticipanteId = null, string? DocModelo = null, string? DocSerie = null, string? DocNumero = null,
                                   DateOnly? DocData = null, ParticipanteEfd? DocParticipante = null)
{
    /// <summary>Natureza (4ª posição do código); no IPI, crédito (entrada) ou débito (saída).</summary>
    public TipoAjusteIcms Tipo => Imposto == ImpostosAjuste.Ipi ? (Sentido == 'E' ? TipoAjusteIcms.OutrosCreditos : TipoAjusteIcms.OutrosDebitos) : (TipoAjusteIcms)(Codigo[3] - '0');

    public char SentidoEfetivo => Sentido ?? (Tipo is TipoAjusteIcms.OutrosCreditos or TipoAjusteIcms.EstornoDebitos ? 'E' : 'S');
    public bool TemDocumento => DocParticipante is not null && !string.IsNullOrWhiteSpace(DocNumero) && DocData is not null;
    public string Uf => Codigo[..2];
}

/// <summary>Código de ajuste cadastrado pela empresa (tabela 5.1.1 da UF; no IPI, 3 posições).</summary>
public sealed record CodigoAjuste(string Imposto, string Codigo, string Descricao);

/// <summary>Conta de consumo recebida: energia elétrica (06), gás (28), água (29) → C500/C590; comunicação (21) e telecomunicação (22) → D500/D590.</summary>
public sealed record ContaConsumoEfd(long Id, string Modelo, ParticipanteEfd Participante, string Numero, string Serie, DateOnly Emissao, DateOnly Entrada,
                                     string ClasseConsumo, decimal Valor, string Cfop, string CstIcms, decimal BaseIcms, decimal AliqIcms, decimal Icms,
                                     decimal Pis, decimal Cofins)
{
    public bool Comunicacao => Modelo is "21" or "22";
}

/// <summary>Item do inventário (H010) com o que o 0200 precisa.</summary>
public sealed record ItemInventarioEfd(int ProdutoCodigo, string Descricao, string Unidade, string? Ncm, string TipoItem, decimal Quantidade, decimal ValorUnitario, decimal Valor);

/// <summary>Inventário de estoque (H005) informado na EFD da competência escolhida.</summary>
public sealed record InventarioEfd(DateOnly Data, string Motivo, string ContaContabil, IReadOnlyList<ItemInventarioEfd> Itens)
{
    public decimal Total => Itens.Sum(i => i.Valor);
}

/// <summary>Regras dos outros lançamentos da EFD ICMS/IPI (Lançamentos › SPED Fiscal).</summary>
public static class OutrosLancamentosEfd
{
    public static IReadOnlyList<(string Codigo, string Nome)> Modelos { get; } =
        [("06", "06 – Nota fiscal/conta de energia elétrica"), ("28", "28 – Nota fiscal/conta de fornecimento de gás canalizado"),
         ("29", "29 – Nota fiscal/conta de fornecimento de água canalizada"), ("21", "21 – Nota fiscal de serviço de comunicação"),
         ("22", "22 – Nota fiscal de serviço de telecomunicação")];

    /// <summary>CFOP sugerido para a aquisição pelo estabelecimento comercial (pode ser alterado).</summary>
    public static string CfopSugerido(string modelo) => modelo switch { "06" => "1253", "21" or "22" => "1303", _ => "1556" };

    public static string RotuloTipo(TipoAjusteIcms t) => t switch
    {
        TipoAjusteIcms.OutrosDebitos => "Outros débitos", TipoAjusteIcms.EstornoCreditos => "Estorno de créditos", TipoAjusteIcms.OutrosCreditos => "Outros créditos",
        TipoAjusteIcms.EstornoDebitos => "Estorno de débitos", TipoAjusteIcms.Deducoes => "Deduções do imposto apurado", _ => "Débitos especiais",
    };

    /// <summary>Tipos de ajuste do ICMS aceitos em cada sentido: entrada (créditos) e saída (débitos, deduções e débitos especiais).</summary>
    public static bool TipoDoSentido(TipoAjusteIcms t, char sentido) =>
        sentido == 'E' ? t is TipoAjusteIcms.OutrosCreditos or TipoAjusteIcms.EstornoDebitos : t is not (TipoAjusteIcms.OutrosCreditos or TipoAjusteIcms.EstornoDebitos);

    /// <summary>
    /// Código do ajuste. ICMS próprio (tabela 5.1.1): UF + 0 + tipo 0 a 5 + 4 dígitos (ex.: SP020099). ICMS-ST: UF + 1 + tipo + 4 dígitos. IPI: 3 posições.
    /// Em "Ajustes de entrada" só valem outros créditos (2) e estornos de débito (3); em "Ajustes de saída", os demais.
    /// </summary>
    public static IReadOnlyList<string> ValidarAjuste(string? codigo, string? descricao, decimal valor, string ufEmpresa, string imposto = ImpostosAjuste.Icms, char? sentido = null)
    {
        var erros = new List<string>();
        var c = (codigo ?? "").Trim().ToUpperInvariant();
        switch (imposto)
        {
            case ImpostosAjuste.Ipi:
                if (!System.Text.RegularExpressions.Regex.IsMatch(c, "^[A-Z0-9]{3}$")) erros.Add("Código do ajuste do IPI com 3 posições (tabela 4.5.4 do Guia Prático da EFD).");
                break;
            case ImpostosAjuste.Icms or ImpostosAjuste.IcmsSt:
                var marca = imposto == ImpostosAjuste.Icms ? "0" : "1";
                if (!System.Text.RegularExpressions.Regex.IsMatch(c, $"^[A-Z]{{2}}{marca}[0-5][0-9]{{4}}$"))
                    erros.Add($"Código do ajuste com 8 posições, da tabela 5.1.1 da UF: UF + {marca} ({(imposto == ImpostosAjuste.Icms ? "ICMS próprio" : "ICMS-ST")}) + tipo (0 a 5) + 4 dígitos. Ex.: SP{marca}20099.");
                else
                {
                    if (c[..2] != ufEmpresa && imposto == ImpostosAjuste.Icms) erros.Add($"O código do ajuste deve começar pela UF da empresa ({ufEmpresa}).");
                    if (sentido is { } s && !TipoDoSentido((TipoAjusteIcms)(c[3] - '0'), s))
                        erros.Add(s == 'E' ? "Em Ajustes de entrada só valem outros créditos (tipo 2) e estornos de débito (tipo 3)."
                                           : "Em Ajustes de saída só valem outros débitos (0), estornos de crédito (1), deduções (4) e débitos especiais (5).");
                }
                break;
            default:
                erros.Add("Imposto do ajuste inválido.");
                break;
        }
        if (valor <= 0) erros.Add("Informe o valor do ajuste.");
        if (string.IsNullOrWhiteSpace(descricao)) erros.Add("Descreva o ajuste.");
        return erros;
    }

    public static IReadOnlyList<string> ValidarConta(string modelo, string numero, DateOnly emissao, DateOnly entrada, decimal valor, string cfop, string cst,
                                                     decimal baseIcms, decimal aliq, decimal icms)
    {
        var erros = new List<string>();
        if (Modelos.All(m => m.Codigo != modelo)) erros.Add("Modelo inválido.");
        if (string.IsNullOrWhiteSpace(numero) || !numero.Trim().All(char.IsAsciiDigit)) erros.Add("Número da nota só com dígitos.");
        if (entrada < emissao) erros.Add("A data de entrada não pode ser anterior à emissão.");
        if (valor <= 0) erros.Add("Informe o valor da nota.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(cfop ?? "", "^[123][0-9]{3}$")) erros.Add("CFOP de entrada com 4 dígitos (começando por 1, 2 ou 3).");
        if (!System.Text.RegularExpressions.Regex.IsMatch(cst ?? "", "^[0-8][0-9]{2}$")) erros.Add("CST do ICMS com 3 dígitos (origem + CST), ex.: 000 ou 090.");
        if (baseIcms < 0 || aliq < 0 || icms < 0) erros.Add("Base, alíquota e ICMS não podem ser negativos.");
        if (baseIcms > valor) erros.Add("A base do ICMS não pode passar do valor da nota.");
        if (icms > 0 && Math.Abs(Math.Round(baseIcms * aliq / 100m, 2) - icms) > 0.05m) erros.Add("O ICMS não confere com base × alíquota.");
        return erros;
    }

    /// <summary>Competência da EFD em que o inventário é informado: o de 31/12 vai na de fevereiro do ano seguinte; os demais, no mês seguinte.</summary>
    public static DateOnly CompetenciaSugerida(DateOnly data) =>
        data is { Month: 12, Day: 31 } ? new DateOnly(data.Year + 1, 2, 1) : new DateOnly(data.Year, data.Month, 1).AddMonths(1);
}
