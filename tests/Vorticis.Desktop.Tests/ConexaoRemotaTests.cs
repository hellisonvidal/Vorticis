using Npgsql;
using Vorticis.Desktop.Comum;

namespace Vorticis.Desktop.Tests;

/// <summary>Banco na nuvem: conexão segura (SSL), tempo limite e keepalive na conexão com o servidor PostgreSQL.</summary>
public class ConexaoRemotaTests
{
    [Fact]
    public void Monta_e_le_a_conexao_com_SSL_tempo_limite_e_keepalive()
    {
        var cs = ConexaoRemota.Montar(" banco.provedor.com.br ", 5432, "vorticis", "app", "s3nha", 1, 30, 60);
        var b = new NpgsqlConnectionStringBuilder(cs);
        Assert.Equal(("banco.provedor.com.br", SslMode.Require, 30, 60), (b.Host, b.SslMode, b.Timeout, b.KeepAlive));
        Assert.Equal((1, 30m, (decimal?)60m), ConexaoRemota.Ler(b));
        var semKeep = new NpgsqlConnectionStringBuilder(ConexaoRemota.Montar("localhost", 5432, "v", "u", "s", 0, 15, 0));
        Assert.Equal((SslMode.Prefer, 0), (semKeep.SslMode, semKeep.KeepAlive));
        Assert.Equal((0, 15m, (decimal?)null), ConexaoRemota.Ler(semKeep));
    }

    [Theory]
    [InlineData("localhost", false)]
    [InlineData("192.168.0.10", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.20.0.5", false)]
    [InlineData("servidor", false)]
    [InlineData("db.exemplo.com.br", true)]
    [InlineData("200.150.10.20", true)]
    public void Reconhece_servidor_fora_da_rede_interna(string servidor, bool nuvem) => Assert.Equal(nuvem, ConexaoRemota.PareceNuvem(servidor));
}
