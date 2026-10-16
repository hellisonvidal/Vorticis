namespace Perseus.Domain.Seguranca;

/// <summary>
/// Funções do sistema que um perfil pode liberar, sempre no âmbito de uma empresa.
/// A administração do sistema (empresas, usuários, perfis, conexão, backup) não é delegável por perfil:
/// pertence ao usuário marcado como administrador.
/// </summary>
public enum Permissao
{
    LancamentoIncluir,
    LancamentoEstornar,
    SaldosIniciais,
    PeriodoFechar,
    PeriodoReabrir,
    ExercicioEncerrar,
    PlanoContasManter,
    CentroCustoManter,
    PlanoReferencialMapear,
    RelatorioBalancete,
    RelatorioRazao,
    RelatorioDiario,
    RelatorioDemonstracoes,
    RelatorioExportar,

    /// <summary>Exclusiva do administrador; não aparece no catálogo de perfis.</summary>
    AdministrarSistema,
}

public sealed record DefinicaoPermissao(Permissao Permissao, string Codigo, string Grupo, string Descricao);

public sealed record SessaoUsuario(long Id, string Login, string Nome, bool Administrador, bool DeveTrocarSenha);

/// <summary>Permissões que um usuário tem em uma empresa (o administrador tem todas).</summary>
public sealed record PermissoesEfetivas(bool Administrador, IReadOnlySet<Permissao> Liberadas, string? NomePerfil)
{
    public static readonly PermissoesEfetivas Nenhuma = new(false, new HashSet<Permissao>(), null);

    public bool Pode(Permissao p) => Administrador || (p != Permissao.AdministrarSistema && Liberadas.Contains(p));

    public bool TemAcesso => Administrador || NomePerfil is not null;
}

public static class Permissoes
{
    /// <summary>Catálogo exibido na tela de perfis. O código é o valor gravado em core.perfil_permissao.</summary>
    public static readonly IReadOnlyList<DefinicaoPermissao> Catalogo =
    [
        new(Permissao.LancamentoIncluir, "LANCAMENTO_INCLUIR", "Lançamentos", "Incluir lançamentos contábeis"),
        new(Permissao.LancamentoEstornar, "LANCAMENTO_ESTORNAR", "Lançamentos", "Estornar lançamentos"),
        new(Permissao.SaldosIniciais, "SALDOS_INICIAIS", "Lançamentos", "Lançar saldos iniciais (implantação)"),
        new(Permissao.PeriodoFechar, "PERIODO_FECHAR", "Lançamentos", "Fechar períodos"),
        new(Permissao.PeriodoReabrir, "PERIODO_REABRIR", "Lançamentos", "Reabrir períodos fechados"),
        new(Permissao.ExercicioEncerrar, "EXERCICIO_ENCERRAR", "Lançamentos", "Encerrar o exercício (apuração do resultado) e desfazer o encerramento"),
        new(Permissao.PlanoContasManter, "PLANO_CONTAS_MANTER", "Cadastros", "Incluir, alterar e excluir contas do plano de contas"),
        new(Permissao.CentroCustoManter, "CENTRO_CUSTO_MANTER", "Cadastros", "Incluir, alterar e excluir centros de custo"),
        new(Permissao.PlanoReferencialMapear, "PLANO_REFERENCIAL_MAPEAR", "Cadastros", "Vincular contas ao plano referencial da RFB"),
        new(Permissao.RelatorioBalancete, "RELATORIO_BALANCETE", "Relatórios", "Balancete de verificação"),
        new(Permissao.RelatorioRazao, "RELATORIO_RAZAO", "Relatórios", "Livro Razão"),
        new(Permissao.RelatorioDiario, "RELATORIO_DIARIO", "Relatórios", "Livro Diário"),
        new(Permissao.RelatorioDemonstracoes, "RELATORIO_DEMONSTRACOES", "Relatórios", "Balanço Patrimonial e DRE"),
        new(Permissao.RelatorioExportar, "RELATORIO_EXPORTAR", "Relatórios", "Exportar relatórios para planilha (CSV)"),
    ];

    private static readonly Dictionary<string, Permissao> PorCodigo = Catalogo.ToDictionary(d => d.Codigo, d => d.Permissao);
    private static readonly Dictionary<Permissao, DefinicaoPermissao> PorPermissao = Catalogo.ToDictionary(d => d.Permissao);

    public static string Codigo(this Permissao p) => PorPermissao.TryGetValue(p, out var d)
        ? d.Codigo
        : throw new ArgumentException($"{p} não é configurável por perfil.", nameof(p));

    public static string Descricao(this Permissao p) => p == Permissao.AdministrarSistema
        ? "Administração do sistema"
        : PorPermissao[p].Descricao;

    /// <summary>Converte códigos gravados no banco; códigos desconhecidos (de versões futuras) são ignorados.</summary>
    public static IReadOnlySet<Permissao> DeCodigos(IEnumerable<string> codigos) =>
        codigos.Select(c => PorCodigo.TryGetValue(c, out var p) ? (Permissao?)p : null).OfType<Permissao>().ToHashSet();
}
