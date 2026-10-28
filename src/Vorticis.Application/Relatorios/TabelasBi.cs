using System.Globalization;
using Vorticis.Domain.Contabil;

namespace Vorticis.Application.Relatorios;

/// <summary>Tabelas de fatos e dimensões para BI externo (Power BI, Excel): nomes de colunas sem acento, chaves numéricas e datas reais.</summary>
public static class TabelasBi
{
    public const string Partidas = "fato_partidas";
    public const string PlanoContas = "dim_plano_contas";
    public const string CentrosCusto = "dim_centro_custo";
    public const string Participantes = "dim_participante";
    public const string Calendario = "dim_calendario";

    public static IReadOnlyList<TabelaRelatorio> Montar(DadosBi d, DateOnly inicio, DateOnly fim)
    {
        var contas = d.Plano.ToDictionary(c => c.Id);
        string Classe(ContaPlano c) => Gerenciais.Classe(c) switch { ClasseResultado.Receita => "Receita", ClasseResultado.CustoDespesa => "Custo ou despesa", _ => "" };

        var fatos = new TabelaRelatorio(Partidas,
            [new("id_lancamento", TipoColuna.Inteiro), new("numero_lancamento", TipoColuna.Inteiro), new("data", TipoColuna.Data), new("id_conta", TipoColuna.Inteiro),
             new("id_centro_custo", TipoColuna.Inteiro), new("id_participante", TipoColuna.Inteiro), new("tipo_dc"), new("valor", TipoColuna.Valor),
             new("valor_assinado", TipoColuna.Valor), new("historico")],
            d.Partidas.Select(p => new LinhaRelatorio([p.LancamentoId, p.Numero, p.Data, p.ContaId, p.CentroId == 0 ? null : p.CentroId, p.ParticipanteId == 0 ? null : p.ParticipanteId,
                                                       p.DC.ToString(), p.Valor, p.Assinado, p.Historico])).ToList(),
            "Uma linha por partida. valor_assinado: débito positivo, crédito negativo. Lançamentos de encerramento e rascunhos ficam de fora.");

        var plano = new TabelaRelatorio(PlanoContas,
            [new("id_conta", TipoColuna.Inteiro), new("codigo"), new("descricao"), new("nivel", TipoColuna.Inteiro), new("analitica"), new("natureza_saldo"), new("cod_nat"),
             new("id_conta_pai", TipoColuna.Inteiro), new("codigo_grupo"), new("descricao_grupo"), new("classe_resultado")],
            d.Plano.OrderBy(c => c.Codigo, StringComparer.Ordinal).Select(c =>
            {
                var g = CalculoBi.Grupo(c, contas);
                return new LinhaRelatorio([c.Id, c.Codigo, c.Descricao, (long)c.Nivel, c.Analitica ? "Sim" : "Não", c.NaturezaSaldo.ToString(), c.CodNat, c.ContaPaiId, g.Codigo, g.Descricao, Classe(c)]);
            }).ToList(), "codigo_grupo e descricao_grupo: a conta de nível 2 a que cada conta pertence.");

        var centros = new TabelaRelatorio(CentrosCusto,
            [new("id_centro_custo", TipoColuna.Inteiro), new("codigo"), new("descricao"), new("id_centro_pai", TipoColuna.Inteiro), new("ativo"), new("analitico")],
            d.Centros.OrderBy(c => c.Codigo, StringComparer.Ordinal).Select(c => new LinhaRelatorio([c.Id, c.Codigo, c.Descricao, c.PaiId, c.Ativo ? "Sim" : "Não", c.Analitico ? "Sim" : "Não"])).ToList());

        var part = new TabelaRelatorio(Participantes,
            [new("id_participante", TipoColuna.Inteiro), new("codigo"), new("nome")],
            d.Participantes.OrderBy(p => p.Codigo, StringComparer.Ordinal).Select(p => new LinhaRelatorio([p.Id, p.Codigo, p.Nome])).ToList());

        var pt = CultureInfo.GetCultureInfo("pt-BR");
        var dias = new List<LinhaRelatorio>();
        for (var dia = inicio; dia <= fim; dia = dia.AddDays(1))
            dias.Add(new LinhaRelatorio([dia, (long)dia.Year, (long)((dia.Month - 1) / 3 + 1), (long)dia.Month, pt.TextInfo.ToTitleCase(pt.DateTimeFormat.GetMonthName(dia.Month)),
                                         $"{dia:yyyy-MM}", (long)dia.Day, pt.TextInfo.ToTitleCase(pt.DateTimeFormat.GetDayName(dia.DayOfWeek))]));
        var cal = new TabelaRelatorio(Calendario,
            [new("data", TipoColuna.Data), new("ano", TipoColuna.Inteiro), new("trimestre", TipoColuna.Inteiro), new("mes", TipoColuna.Inteiro), new("nome_mes"), new("ano_mes"),
             new("dia", TipoColuna.Inteiro), new("dia_semana")], dias);
        return [fatos, plano, centros, part, cal];
    }
}
