namespace Perseus.Domain.Cadastros;

public static class Cpf
{
    public static bool Valido(string? valor)
    {
        var c = Cnpj.SomenteDigitos(valor);
        if (c.Length != 11 || c.All(ch => ch == c[0])) return false;
        int Dv(int tamanho)
        {
            int soma = 0;
            for (int i = 0; i < tamanho; i++) soma += (c[i] - '0') * (tamanho + 1 - i);
            int resto = soma * 10 % 11;
            return resto == 10 ? 0 : resto;
        }
        return Dv(9) == c[9] - '0' && Dv(10) == c[10] - '0';
    }

    public static string Formatar(string c) => c.Length == 11 ? $"{c[..3]}.{c[3..6]}.{c[6..9]}-{c[9..]}" : c;
}

public static class Uf
{
    public static readonly IReadOnlySet<string> Todas = new HashSet<string>
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA", "PB", "PR",
        "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    };

    public static bool Valida(string? uf) => uf is not null && Todas.Contains(uf.Trim().ToUpperInvariant());
}
