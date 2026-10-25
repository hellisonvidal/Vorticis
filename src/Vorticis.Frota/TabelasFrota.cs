using Vorticis.Application.Relatorios;

namespace Vorticis.Frota;

/// <summary>Relatórios da frota (na tela e exportados em PDF, Excel e CSV).</summary>
public static class TabelasFrota
{
    public const string TituloCustos = "Custos por veículo";
    public const string TituloConsumo = "Consumo de combustível";
    public const string TituloViagens = "Resultado das viagens";
    public const string TituloVencimentos = "Vencimentos e manutenções";
    public const string TituloAcompanhamento = "Acompanhamento das ordens de serviço";
    public const string TituloAprovacoes = "Aprovações de serviços";
    public const string TituloNotas = "Notas fiscais de compra";

    private static LinhaRelatorio L(bool destaque, params object?[] v) => new(v, destaque);
    private static decimal? PorKm(decimal valor, decimal km) => km > 0 ? Math.Round(valor / km, 3) : null;

    /// <summary>Combustível, manutenção e despesas de viagem de cada veículo no período; km rodado pelos abastecimentos e viagens.</summary>
    public static TabelaRelatorio Custos(IReadOnlyList<Veiculo> veiculos, IReadOnlyList<Abastecimento> abastecimentos, IReadOnlyList<OrdemServico> ordens,
                                         IReadOnlyList<Viagem> viagens)
    {
        var linhas = new List<LinhaRelatorio>();
        decimal tComb = 0, tMan = 0, tDesp = 0, tKm = 0;
        foreach (var v in veiculos.OrderBy(v => v.Codigo))
        {
            var ab = abastecimentos.Where(a => a.VeiculoId == v.Id).ToList();
            var comb = ab.Sum(a => a.ValorTotal);
            var man = ordens.Where(o => o.VeiculoId == v.Id).Sum(o => o.Total);
            var vi = viagens.Where(x => x.VeiculoId == v.Id && x.Situacao != "CANCELADA").ToList();
            var desp = vi.Sum(x => x.Despesas);
            var kmAb = ab.Count >= 2 ? ab.Max(a => a.Km) - ab.Min(a => a.Km) : 0;
            var km = Math.Max(kmAb, vi.Sum(x => x.KmRodado ?? 0));
            if (comb + man + desp == 0 && km == 0) continue;
            var total = comb + man + desp;
            var media = RegrasFrota.Medias(ab) is { Count: > 0 } ms ? Math.Round(ms.Sum(m => m.KmRodado) / ms.Sum(m => m.Litros), 2) : (decimal?)null;
            linhas.Add(L(false, RegrasFrota.FormatarPlaca(v.Placa), $"{v.Marca} {v.Modelo}".Trim(), km, media, comb, man, desp, total, PorKm(total, km)));
            (tComb, tMan, tDesp, tKm) = (tComb + comb, tMan + man, tDesp + desp, tKm + km);
        }
        linhas.Add(L(true, "TOTAL", $"{linhas.Count} veículo(s)", tKm, null, tComb, tMan, tDesp, tComb + tMan + tDesp, PorKm(tComb + tMan + tDesp, tKm)));
        return new TabelaRelatorio(TituloCustos,
        [
            new("Placa", LarguraCm: 2), new("Veículo"), new("Km rodado", TipoColuna.Valor), new("Média km/l", TipoColuna.Valor), new("Combustível", TipoColuna.Valor),
            new("Manutenção", TipoColuna.Valor), new("Despesas de viagem", TipoColuna.Valor), new("Custo total", TipoColuna.Valor), new("Custo por km", TipoColuna.Valor),
        ], linhas);
    }

    /// <summary>Cada média (tanque cheio a tanque cheio), com o alerta de consumo abaixo do esperado.</summary>
    public static TabelaRelatorio Consumo(IReadOnlyList<Veiculo> veiculos, IReadOnlyList<Abastecimento> abastecimentos, decimal tolerancia)
    {
        var porId = veiculos.ToDictionary(v => v.Id);
        var linhas = RegrasFrota.Medias(abastecimentos).OrderBy(m => porId[m.VeiculoId].Codigo).ThenBy(m => m.Data)
            .Select(m =>
            {
                var v = porId[m.VeiculoId];
                var fora = RegrasFrota.ConsumoForaDoEsperado(m.Media, v.MediaEsperada, tolerancia);
                return L(fora, RegrasFrota.FormatarPlaca(v.Placa), m.Data, m.KmRodado, m.Litros, m.Media, v.MediaEsperada, fora ? "ABAIXO DO ESPERADO" : "");
            }).ToList();
        return new TabelaRelatorio(TituloConsumo,
        [
            new("Placa", LarguraCm: 2), new("Abastecimento", TipoColuna.Data), new("Km rodado", TipoColuna.Valor), new("Litros", TipoColuna.Valor),
            new("Média km/l", TipoColuna.Valor), new("Esperada km/l", TipoColuna.Valor), new("Situação"),
        ], linhas);
    }

    /// <summary>Frete menos as despesas da viagem e o combustível abastecido no veículo entre a saída e a chegada.</summary>
    public static TabelaRelatorio Viagens(IReadOnlyList<Viagem> viagens, IReadOnlyList<Abastecimento> abastecimentos)
    {
        var linhas = new List<LinhaRelatorio>();
        decimal tFrete = 0, tDesp = 0, tComb = 0;
        foreach (var v in viagens.Where(v => v.Situacao != "CANCELADA").OrderBy(v => v.Saida).ThenBy(v => v.Numero))
        {
            var fim = v.Chegada ?? v.Saida;
            var comb = abastecimentos.Where(a => a.VeiculoId == v.VeiculoId && a.Data >= v.Saida && a.Data <= fim && a.Km >= v.KmSaida
                                                 && (v.KmChegada is null || a.Km <= v.KmChegada)).Sum(a => a.ValorTotal);
            var resultado = v.ValorFrete - v.Despesas - comb;
            linhas.Add(L(resultado < 0, v.Numero, v.Saida, RegrasFrota.FormatarPlaca(v.Placa ?? ""), $"{v.Origem} → {v.Destino}", v.KmRodado, v.ValorFrete, v.Despesas, comb,
                         resultado, RegrasFrota.Rotulo(v.Situacao)));
            (tFrete, tDesp, tComb) = (tFrete + v.ValorFrete, tDesp + v.Despesas, tComb + comb);
        }
        linhas.Add(L(true, null, null, "TOTAL", $"{linhas.Count} viagem(ns)", null, tFrete, tDesp, tComb, tFrete - tDesp - tComb, ""));
        return new TabelaRelatorio(TituloViagens,
        [
            new("Nº", TipoColuna.Inteiro, 1.2), new("Saída", TipoColuna.Data), new("Placa", LarguraCm: 2), new("Trajeto"), new("Km rodado", TipoColuna.Valor),
            new("Frete", TipoColuna.Valor), new("Despesas", TipoColuna.Valor), new("Combustível", TipoColuna.Valor), new("Resultado", TipoColuna.Valor), new("Situação", LarguraCm: 2.2),
        ], linhas);
    }

    public static TabelaRelatorio Vencimentos(IReadOnlyList<AvisoFrota> avisos) => new(TituloVencimentos,
    [
        new("Situação", LarguraCm: 2.4), new("Assunto", LarguraCm: 5), new("Descrição"), new("Data", TipoColuna.Data),
    ], [.. avisos.Select(a => L(a.Gravidade == GravidadeAviso.Vencido,
        a.Gravidade switch { GravidadeAviso.Vencido => "VENCIDO", GravidadeAviso.AVencer => "A VENCER", _ => "ATENÇÃO" }, a.Assunto, a.Descricao, a.Data))]);

    /// <summary>Ordens abertas no período: situação, dias em aberto, valor da ordem, último valor autorizado e notas de compra lançadas.</summary>
    public static TabelaRelatorio Acompanhamento(IReadOnlyList<OrdemServico> ordens, IReadOnlyList<Autorizacao> autorizacoes, IReadOnlyList<NotaCompra> notas, DateOnly hoje)
    {
        var linhas = new List<LinhaRelatorio>();
        decimal tOrdem = 0, tAut = 0, tNotas = 0;
        foreach (var o in ordens.OrderBy(o => o.Data).ThenBy(o => o.Numero))
        {
            var aut = autorizacoes.Where(a => a.OrdemId == o.Id && a.Decisao == "APROVADA").MaxBy(a => a.Momento)?.Valor;
            var nf = notas.Where(n => n.OrdemId == o.Id).Sum(n => n.Total);
            var cancelada = o.Situacao == "CANCELADA";
            // destaque: aguardando autorização, ou notas acima do valor autorizado
            var atencao = o.Situacao == "AGUARDANDO" || (aut is { } a && nf > a);
            linhas.Add(L(atencao, o.Numero, o.Data, RegrasFrota.FormatarPlaca(o.Placa ?? ""), o.Descricao, RegrasFrota.RotuloManutencao(o.Situacao),
                         (long)RegrasFrota.DiasEmAberto(o, hoje), o.Total, aut, nf, aut is { } v ? nf - v : null));
            if (cancelada) continue;
            (tOrdem, tAut, tNotas) = (tOrdem + o.Total, tAut + (aut ?? 0), tNotas + nf);
        }
        linhas.Add(L(true, null, null, "TOTAL", $"{ordens.Count} ordem(ns), {ordens.Count(o => o.Situacao is not ("CONCLUIDA" or "CANCELADA"))} em andamento",
                     "", null, tOrdem, tAut, tNotas, tNotas - tAut));
        return new TabelaRelatorio(TituloAcompanhamento,
        [
            new("Nº", TipoColuna.Inteiro, 1.2), new("Abertura", TipoColuna.Data), new("Placa", LarguraCm: 2), new("Serviço"), new("Situação", LarguraCm: 3.2),
            new("Dias", TipoColuna.Inteiro, 1.2), new("Valor da ordem", TipoColuna.Valor), new("Autorizado", TipoColuna.Valor), new("Notas de compra", TipoColuna.Valor),
            new("Notas − autorizado", TipoColuna.Valor),
        ], linhas);
    }

    /// <summary>Decisões do período: quem aprovou ou reprovou, quando, o valor e o motivo; autorizações automáticas aparecem como tal.</summary>
    public static TabelaRelatorio Aprovacoes(IReadOnlyList<Autorizacao> autorizacoes)
    {
        var linhas = autorizacoes.Select(a => L(a.Decisao == "REPROVADA", a.Momento.ToString("dd/MM/yyyy HH:mm"), a.NumeroOrdem, RegrasFrota.FormatarPlaca(a.Placa ?? ""),
                                                 a.Decisao == "APROVADA" ? (a.Automatica ? "Autorizada (automática)" : "Autorizada") : "Reprovada", a.Valor,
                                                 a.Automatica ? "Sistema" : a.Usuario ?? "", a.Observacao ?? "")).ToList();
        var aprovadas = autorizacoes.Where(a => a.Decisao == "APROVADA").ToList();
        linhas.Add(L(true, "TOTAL", null, "", $"{aprovadas.Count} autorizada(s), {autorizacoes.Count - aprovadas.Count} reprovada(s)", aprovadas.Sum(a => a.Valor), "", ""));
        return new TabelaRelatorio(TituloAprovacoes,
        [
            new("Data e hora", LarguraCm: 3), new("OS nº", TipoColuna.Inteiro, 1.4), new("Placa", LarguraCm: 2), new("Decisão", LarguraCm: 3.6),
            new("Valor", TipoColuna.Valor), new("Usuário", LarguraCm: 3.5), new("Observação / motivo"),
        ], linhas);
    }

    /// <summary>Notas de compra emitidas no período, com a ordem de serviço e o veículo atendidos.</summary>
    public static TabelaRelatorio Notas(IReadOnlyList<NotaCompra> notas)
    {
        var linhas = notas.Select(n => L(n.OrdemId is null, n.Emissao, $"{n.Numero}/{n.Serie}", n.Fornecedor, n.Cnpj is { Length: 14 } c ? Vorticis.Domain.Cadastros.Cnpj.Formatar(c) : n.Cnpj ?? "",
                                          n.NumeroOrdem is { } os ? (long)os : null, RegrasFrota.FormatarPlaca(n.Placa ?? ""), n.ValorPecas, n.ValorServicos, n.Total)).ToList();
        linhas.Add(L(true, null, "TOTAL", $"{notas.Count} nota(s), {notas.Count(n => n.OrdemId is null)} sem ordem de serviço", "", null, "",
                     notas.Sum(n => n.ValorPecas), notas.Sum(n => n.ValorServicos), notas.Sum(n => n.Total)));
        return new TabelaRelatorio(TituloNotas,
        [
            new("Emissão", TipoColuna.Data), new("Número/série", LarguraCm: 2.4), new("Fornecedor"), new("CNPJ/CPF", LarguraCm: 3.6), new("OS nº", TipoColuna.Inteiro, 1.4),
            new("Placa", LarguraCm: 2), new("Peças", TipoColuna.Valor), new("Serviços", TipoColuna.Valor), new("Total", TipoColuna.Valor),
        ], linhas);
    }
}
