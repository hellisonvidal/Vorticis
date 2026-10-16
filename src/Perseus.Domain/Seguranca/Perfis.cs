namespace Perseus.Domain.Seguranca;

/// <summary>Perfil do usuário em uma empresa (core.usuario_empresa.perfil).</summary>
public enum Perfil { Contador, Assistente, Consulta }

public enum Permissao
{
    ConsultarRelatorios,
    Lancar,             // efetivar e estornar lançamentos
    FecharPeriodos,     // fechar e reabrir competências
    AdministrarSistema, // usuários, empresas, conexão
}

public sealed record SessaoUsuario(long Id, string Login, string Nome, bool Administrador, bool DeveTrocarSenha);

public static class Permissoes
{
    /// <param name="perfil">Perfil na empresa ativa; nulo se o usuário não tem acesso a ela.</param>
    public static bool Pode(SessaoUsuario sessao, Perfil? perfil, Permissao permissao)
    {
        if (sessao.Administrador) return true;
        return permissao switch
        {
            Permissao.ConsultarRelatorios => perfil is not null,
            Permissao.Lancar => perfil is Perfil.Contador or Perfil.Assistente,
            Permissao.FecharPeriodos => perfil is Perfil.Contador,
            _ => false,
        };
    }

    public static string Sql(this Perfil p) => p switch
    {
        Perfil.Contador => "CONTADOR",
        Perfil.Assistente => "ASSISTENTE",
        Perfil.Consulta => "CONSULTA",
        _ => throw new ArgumentOutOfRangeException(nameof(p)),
    };

    public static Perfil PerfilDeSql(string s) => s switch
    {
        "CONTADOR" => Perfil.Contador,
        "ASSISTENTE" => Perfil.Assistente,
        "CONSULTA" => Perfil.Consulta,
        _ => throw new ArgumentOutOfRangeException(nameof(s)),
    };
}
