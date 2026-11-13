using Vorticis.Folha.Cadastros;

namespace Vorticis.Folha.Regras;

/// <summary>Prazo de pessoal a acompanhar: término de contrato a termo (experiência, determinado, aprendiz, estágio) ou férias a conceder.</summary>
public sealed record VencimentoPessoal(long FuncionarioId, string Matricula, string Nome, string Tipo, DateOnly Limite, int Dias, string Situacao, string Observacao);

/// <summary>Admissão ou desligamento no período (relação de admitidos e desligados).</summary>
public sealed record MovimentoPessoal(DateOnly Data, string Matricula, string Nome, string? Cargo, decimal Salario, string? Motivo, decimal? LiquidoRescisao);

public static class ControlesPessoal
{
    /// <summary>Prazo máximo do contrato de experiência (CLT, art. 445, parágrafo único), com uma prorrogação (art. 451).</summary>
    public const int MaximoExperiencia = 90;

    private static string Situacao(int dias) => dias < 0 ? $"Vencido há {-dias} dia(s)" : dias == 0 ? "Vence hoje" : $"Vence em {dias} dia(s)";

    /// <summary>
    /// Contratos a termo que terminam até <paramref name="hoje"/> + <paramref name="dias"/> (ou já terminados sem desligamento) e férias cujo
    /// gozo precisa começar até lá para terminar dentro do período concessivo (depois disso, são pagas em dobro, CLT art. 137).
    /// </summary>
    public static IReadOnlyList<VencimentoPessoal> Vencimentos(IEnumerable<Funcionario> funcionarios, IEnumerable<FeriasRegistro> ferias, DateOnly hoje, int dias)
    {
        var limite = hoje.AddDays(dias);
        var usadas = ferias.Where(f => f.Status == "CALCULADA").ToLookup(f => (f.FuncionarioId, f.AquisitivoInicio));
        var lista = new List<VencimentoPessoal>();
        foreach (var f in funcionarios.Where(f => f.Ativo))
        {
            if (f.TipoContrato != "INDETERMINADO" && f.TerminoContrato is { } fim && fim <= limite)
            {
                var duracao = fim.DayNumber - f.Admissao.DayNumber + 1;
                var obs = f.TipoContrato == "EXPERIENCIA"
                    ? duracao < MaximoExperiencia
                        ? $"Experiência de {duracao} dias: pode ser prorrogada uma vez, até {MaximoExperiencia} dias no total ({f.Admissao.AddDays(MaximoExperiencia - 1):dd/MM/yyyy}), ou encerrada; sem isso, passa a prazo indeterminado."
                        : "Experiência no prazo máximo: encerre o contrato ou ele passa a prazo indeterminado."
                    : "Encerre o contrato no término ou registre a alteração para prazo indeterminado.";
                var tipo = f.TipoContrato switch
                {
                    "EXPERIENCIA" => "Contrato de experiência",
                    "APRENDIZ" => "Contrato de aprendizagem",
                    "ESTAGIO" => "Estágio",
                    _ => "Contrato por prazo determinado",
                };
                lista.Add(new(f.Id, f.Matricula, f.Nome, tipo, fim, fim.DayNumber - hoje.DayNumber, Situacao(fim.DayNumber - hoje.DayNumber), obs));
            }
            foreach (var p in PeriodosFerias.Calcular(f.Admissao, hoje).Where(p => p.Completo(hoje)))
            {
                var doPeriodo = usadas[(f.Id, p.Inicio)].ToList();
                var direito = PeriodosFerias.DiasDireito(doPeriodo.Select(u => u.Faltas).DefaultIfEmpty(0).Max());
                var saldo = direito - doPeriodo.Sum(u => u.DiasGozo + u.AbonoDias);
                if (saldo <= 0) continue;
                var inicioAte = p.FimConcessivo.AddDays(-saldo + 1);
                if (inicioAte > limite) continue;
                var d = inicioAte.DayNumber - hoje.DayNumber;
                var obs = p.ConcessivoVencido(hoje)
                    ? $"Período concessivo terminou em {p.FimConcessivo:dd/MM/yyyy}: as férias são devidas em dobro (CLT, art. 137)."
                    : $"Saldo de {saldo} dia(s) do período {p.Inicio:dd/MM/yyyy} a {p.Fim:dd/MM/yyyy}; o gozo deve terminar até {p.FimConcessivo:dd/MM/yyyy}.";
                lista.Add(new(f.Id, f.Matricula, f.Nome, "Férias (início do gozo até)", inicioAte, d, p.ConcessivoVencido(hoje) ? "Em dobro" : Situacao(d), obs));
            }
        }
        return lista.OrderBy(v => v.Limite).ThenBy(v => v.Nome, StringComparer.CurrentCulture).ToList();
    }

    /// <summary>Admitidos e desligados de <paramref name="inicio"/> a <paramref name="fim"/> e a rotatividade do período.</summary>
    public static (IReadOnlyList<MovimentoPessoal> Admitidos, IReadOnlyList<MovimentoPessoal> Desligados, int QuadroInicial, decimal Rotatividade)
        AdmitidosDesligados(IReadOnlyCollection<Funcionario> funcionarios, IEnumerable<RescisaoRegistro> rescisoes, DateOnly inicio, DateOnly fim)
    {
        var resc = rescisoes.Where(r => r.Status != "CANCELADA").GroupBy(r => r.FuncionarioId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Desligamento).First());
        var admitidos = funcionarios.Where(f => f.Admissao >= inicio && f.Admissao <= fim).OrderBy(f => f.Admissao).ThenBy(f => f.Nome)
            .Select(f => new MovimentoPessoal(f.Admissao, f.Matricula, f.Nome, f.CargoNome, f.Salario, null, null)).ToList();
        var desligados = funcionarios.Where(f => f.Desligamento is { } d && d >= inicio && d <= fim).OrderBy(f => f.Desligamento).ThenBy(f => f.Nome)
            .Select(f => new MovimentoPessoal(f.Desligamento!.Value, f.Matricula, f.Nome, f.CargoNome, f.Salario,
                                              resc.TryGetValue(f.Id, out var r) ? r.Motivo.Descricao() : f.MotivoDesligamento,
                                              resc.TryGetValue(f.Id, out var r2) ? r2.Liquido : null)).ToList();
        var quadro = funcionarios.Count(f => f.Admissao < inicio && (f.Desligamento is null || f.Desligamento >= inicio));
        var rot = quadro == 0 ? 0 : Math.Round((admitidos.Count + desligados.Count) / 2m / quadro * 100m, 2);
        return (admitidos, desligados, quadro, rot);
    }
}
