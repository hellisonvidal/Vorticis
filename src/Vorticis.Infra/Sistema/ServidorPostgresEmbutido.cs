using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Vorticis.Infra.Sistema;

/// <summary>
/// PostgreSQL privado do Vorticis: binários distribuídos junto com o programa (pasta <c>pgsql</c>), dados no perfil do
/// usuário e servidor iniciado/parado pelo próprio aplicativo. Dispensa instalar e configurar o PostgreSQL.
/// <list type="bullet">
/// <item>Escuta só em <c>localhost</c>, com autenticação SCRAM e senha aleatória gerada na criação do cluster.</item>
/// <item>Não é um serviço do Windows: roda enquanto o Vorticis está aberto (com o usuário do Windows, sem privilégios).</item>
/// <item>A porta é escolhida na inicialização (a preferida, se livre), então não conflita com outro PostgreSQL da máquina.</item>
/// </list>
/// </summary>
public sealed class ServidorPostgresEmbutido(string pastaBinarios, string pastaDados, string arquivoLog)
{
    public const string Usuario = "postgres";
    public const int PortaPreferida = 54329;

    public string PastaBinarios { get; } = pastaBinarios;
    public string PastaDados { get; } = pastaDados;
    public string ArquivoLog { get; } = arquivoLog;

    /// <summary>True se este objeto iniciou o servidor (e, portanto, deve pará-lo ao fechar o programa).</summary>
    public bool IniciadoPorEsteProcesso { get; private set; }

    public bool Inicializado => File.Exists(Path.Combine(PastaDados, "PG_VERSION"));

    /// <summary>
    /// Pasta <c>bin</c> dos binários distribuídos com o programa (<c>&lt;pasta do executável&gt;\pgsql\bin</c>),
    /// ou a indicada em VORTICIS_PG_EMBUTIDO_BIN. Null se o programa foi distribuído sem o PostgreSQL.
    /// </summary>
    public static string? LocalizarBinarios()
    {
        var candidatos = new List<string>();
        if (Environment.GetEnvironmentVariable("VORTICIS_PG_EMBUTIDO_BIN") is { Length: > 0 } env) candidatos.Add(env);
        candidatos.Add(Path.Combine(AppContext.BaseDirectory, "pgsql", "bin"));
        return candidatos.FirstOrDefault(d => File.Exists(Path.Combine(d, Exe("pg_ctl"))) && File.Exists(Path.Combine(d, Exe("initdb"))));
    }

    public static string GerarSenha() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    /// <summary>Cria o cluster (initdb). Só pode ser chamado uma vez; a senha do superusuário deve ser guardada pelo chamador.</summary>
    public async Task InicializarAsync(string senha, CancellationToken ct = default)
    {
        if (Inicializado) throw new InvalidOperationException($"O banco local já existe em {PastaDados}.");
        if (Directory.Exists(PastaDados) && Directory.EnumerateFileSystemEntries(PastaDados).Any())
            throw new InvalidOperationException($"A pasta {PastaDados} não está vazia e não contém um banco do PostgreSQL.");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(PastaDados))!);
        var arquivoSenha = Path.Combine(Path.GetTempPath(), $"vorticis-{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(arquivoSenha, senha, ct);
        try
        {
            string[] basicos = ["-D", PastaDados, "-U", Usuario, "-A", "scram-sha-256", $"--pwfile={arquivoSenha}", "-E", "UTF8", "--locale=C"];
            // Ordenação em português (acentos) via ICU; se a distribuição não tiver ICU, usa a ordenação binária.
            var (codigo, saida) = await ExecutarAsync("initdb", [.. basicos, "--locale-provider=icu", "--icu-locale=pt-BR"], ct);
            if (codigo != 0)
            {
                ApagarPastaDados();
                (codigo, saida) = await ExecutarAsync("initdb", basicos, ct);
            }
            if (codigo != 0)
            {
                ApagarPastaDados();
                throw new InvalidOperationException($"Não foi possível criar o banco local (initdb, código {codigo}): {saida.Trim()}");
            }
        }
        finally
        {
            File.Delete(arquivoSenha);
        }
    }

    /// <summary>Porta do servidor em execução neste cluster, ou null se parado.</summary>
    public async Task<int?> PortaEmExecucaoAsync(CancellationToken ct = default)
    {
        var (codigo, _) = await ExecutarAsync("pg_ctl", ["status", "-D", PastaDados], ct);
        if (codigo != 0) return null;
        // postmaster.pid: linha 4 = porta
        try
        {
            var linhas = await File.ReadAllLinesAsync(Path.Combine(PastaDados, "postmaster.pid"), ct);
            return linhas.Length > 3 && int.TryParse(linhas[3].Trim(), out var p) ? p : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Inicia o servidor (ou reaproveita o que já está rodando neste cluster) e devolve a porta.</summary>
    public async Task<int> IniciarAsync(int portaPreferida = PortaPreferida, CancellationToken ct = default)
    {
        if (!Inicializado) throw new InvalidOperationException("O banco local ainda não foi criado.");
        if (await PortaEmExecucaoAsync(ct) is { } emExecucao) return emExecucao;

        var porta = PortaLivre(portaPreferida) ? portaPreferida : PortaQualquer();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ArquivoLog))!);
        // Só TCP: fora do Windows, sem socket Unix (o diretório padrão das distribuições, /var/run/postgresql,
        // não é gravável pelo usuário comum).
        var opcoes = $"-p {porta} -c listen_addresses=localhost" + (OperatingSystem.IsWindows() ? "" : " -c unix_socket_directories=");
        var (codigo, saida) = await ExecutarAsync("pg_ctl",
            ["start", "-D", PastaDados, "-l", ArquivoLog, "-w", "-t", "120", "-o", opcoes], ct, capturarSaida: false);
        if (codigo != 0)
            throw new InvalidOperationException(
                $"Não foi possível iniciar o banco local (pg_ctl, código {codigo}). {saida}Detalhes no arquivo {ArquivoLog}:{Environment.NewLine}{UltimasLinhasLog()}");
        IniciadoPorEsteProcesso = true;
        return porta;
    }

    /// <summary>Para o servidor (desligamento rápido: conexões abertas são encerradas, transações pendentes desfeitas).</summary>
    public void Parar()
    {
        if (!Inicializado) return;
        try
        {
            using var p = Process.Start(Configurar("pg_ctl", ["stop", "-D", PastaDados, "-m", "fast", "-w", "-t", "60"], capturarSaida: false));
            if (p is not null && !p.WaitForExit(TimeSpan.FromSeconds(70))) p.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // servidor já parado ou binários removidos: nada a fazer
        }
        IniciadoPorEsteProcesso = false;
    }

    private string UltimasLinhasLog()
    {
        try
        {
            return string.Join(Environment.NewLine, File.ReadLines(ArquivoLog).TakeLast(8));
        }
        catch (IOException)
        {
            return "(log indisponível)";
        }
    }

    private void ApagarPastaDados()
    {
        if (Directory.Exists(PastaDados)) Directory.Delete(PastaDados, recursive: true);
    }

    private static bool PortaLivre(int porta)
    {
        try
        {
            var l = new TcpListener(IPAddress.Loopback, porta);
            l.Start();
            l.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static int PortaQualquer()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var porta = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return porta;
    }

    private static string Exe(string nome) => OperatingSystem.IsWindows() ? nome + ".exe" : nome;

    private ProcessStartInfo Configurar(string ferramenta, IEnumerable<string> argumentos, bool capturarSaida)
    {
        // Sem captura no "pg_ctl start": o processo do servidor herdaria o pipe e a leitura nunca terminaria.
        var psi = new ProcessStartInfo(Path.Combine(PastaBinarios, Exe(ferramenta)))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = capturarSaida,
            RedirectStandardError = capturarSaida,
        };
        foreach (var a in argumentos) psi.ArgumentList.Add(a);
        psi.Environment["LC_MESSAGES"] = "C";
        return psi;
    }

    private async Task<(int Codigo, string Saida)> ExecutarAsync(string ferramenta, IEnumerable<string> argumentos, CancellationToken ct,
                                                                bool capturarSaida = true)
    {
        using var p = Process.Start(Configurar(ferramenta, argumentos, capturarSaida))
                      ?? throw new InvalidOperationException($"Não foi possível executar {ferramenta}.");
        if (!capturarSaida)
        {
            await p.WaitForExitAsync(ct);
            return (p.ExitCode, "");
        }
        var saida = p.StandardOutput.ReadToEndAsync(ct);
        var erro = p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        return (p.ExitCode, (await erro) + (await saida));
    }
}
