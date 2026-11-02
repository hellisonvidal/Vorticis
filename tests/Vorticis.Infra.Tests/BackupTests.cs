using Vorticis.Domain.Contabil;
using Vorticis.Domain.Seguranca;
using Vorticis.Infra.Sistema;

namespace Vorticis.Infra.Tests;

public class BackupTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    [Fact]
    public async Task Backup_automatico_roda_uma_vez_por_dia_e_guarda_so_os_ultimos()
    {
        BancoDeTeste.ExigirBanco();
        string? ferramenta = null;
        try { ferramenta = BackupPostgres.LocalizarFerramenta("pg_dump"); } catch (FileNotFoundException) { }
        Assert.SkipWhen(ferramenta is null, "pg_dump não disponível nesta máquina.");
        var ct = TestContext.Current.CancellationToken;
        var admin = new SessaoUsuario(await db.AdminAsync(), "admin_teste", "Admin", true, false);
        var cfg = db.App.ConfiguracoesGerais;
        var pasta = Path.Combine(Path.GetTempPath(), "vorticis_auto_" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.True((await cfg.SalvarPastaBackupAsync(admin, pasta, ct)).Sucesso);
            Assert.Equal(new Vorticis.Application.Sistema.BackupAutomatico(), await cfg.BackupAutomaticoAsync(ct));   // ligado, guarda 7
            Assert.False((await cfg.SalvarBackupAutomaticoAsync(admin with { Administrador = false }, true, 3, ct)).Sucesso);
            Assert.False((await cfg.SalvarBackupAutomaticoAsync(admin, true, 0, ct)).Sucesso);
            Assert.True((await cfg.SalvarBackupAutomaticoAsync(admin, true, 2, ct)).Sucesso);
            File.WriteAllText(Path.Combine(pasta, "manual_feito_a_mao.backup"), "x");                // os feitos à mão não são apagados

            var dia = new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.FromHours(-3));
            var primeiro = await db.App.Backup.AutomaticoDoDiaAsync(cfg, dia, ct);
            Assert.True(primeiro.Sucesso, string.Join("; ", primeiro.Erros));
            Assert.StartsWith("automatico_", Path.GetFileName(primeiro.Valor));
            Assert.Null((await db.App.Backup.AutomaticoDoDiaAsync(cfg, dia.AddHours(5), ct)).Valor);        // mesmo dia: não repete
            Assert.Equal(dia, (await cfg.BackupAutomaticoAsync(ct)).Ultimo);

            for (var i = 1; i <= 2; i++)
            {
                await Task.Delay(1100, ct);                                                             // nome do arquivo tem os segundos
                Assert.NotNull((await db.App.Backup.AutomaticoDoDiaAsync(cfg, dia.AddDays(i), ct)).Valor);
            }
            var arquivos = Vorticis.Application.Sistema.ServicoBackup.Listar(pasta).Select(a => a.Nome).ToList();
            Assert.Equal(2, arquivos.Count(n => n.StartsWith("automatico_")));                          // só os 2 últimos
            Assert.Contains("manual_feito_a_mao.backup", arquivos);

            Assert.True((await cfg.SalvarBackupAutomaticoAsync(admin, false, 2, ct)).Sucesso);
            Assert.Null((await db.App.Backup.AutomaticoDoDiaAsync(cfg, dia.AddDays(9), ct)).Valor);         // desligado
        }
        finally
        {
            await cfg.SalvarPastaBackupAsync(admin, null, ct);
            if (Directory.Exists(pasta)) Directory.Delete(pasta, true);
        }
    }

    [Fact]
    public async Task Restaura_o_estado_do_backup_e_guarda_copia_de_seguranca()
    {
        BancoDeTeste.ExigirBanco();
        string? ferramenta = null;
        try { ferramenta = BackupPostgres.LocalizarFerramenta("pg_dump"); } catch (FileNotFoundException) { }
        Assert.SkipWhen(ferramenta is null, "pg_dump não disponível nesta máquina.");

        var ct = TestContext.Current.CancellationToken;
        var admin = new SessaoUsuario(await db.AdminAsync(), "admin_teste", "Admin", true, false);
        var empresa = await db.CriarEmpresaAsync("11222333000181");
        async Task Lancar(int dia) => Assert.True((await db.App.Lancamentos.EfetivarAsync(new LancamentoProposto(empresa, new(2026, 6, dia),
            OrigemLancamento.Manual, [new("1.1.01.001", IndicadorDC.D, 10m, "x"), new("3.1.01.001", IndicadorDC.C, 10m, "x")]), admin.Id, ct)).Sucesso);
        Task<long> Contar() => db.EscalarAsync<long>("SELECT COUNT(*) FROM contabil.lancamento");

        await Lancar(1);
        var pasta = Path.Combine(Path.GetTempPath(), "vorticis_backup_" + Guid.NewGuid().ToString("N"));
        try
        {
            var gerado = await db.App.Backup.GerarAsync(admin, pasta, ct);
            Assert.True(gerado.Sucesso, string.Join("; ", gerado.Erros));
            Assert.True(new FileInfo(gerado.Valor).Length > 1000);

            await Lancar(2);
            Assert.Equal(2, await Contar());

            var comum = admin with { Administrador = false };
            Assert.Contains("Somente administradores", (await db.App.Backup.RestaurarAsync(comum, gerado.Valor, pasta, ct)).Erros[0]);

            var restaurado = await db.App.Backup.RestaurarAsync(admin, gerado.Valor, pasta, ct);
            Assert.True(restaurado.Sucesso, string.Join("; ", restaurado.Erros));
            Assert.Equal(1, await Contar());
            Assert.Contains("antes_da_restauracao", restaurado.Valor);
            Assert.Equal(2, Application.Sistema.ServicoBackup.Listar(pasta).Count);
            Assert.True(await db.EscalarAsync<bool>("SELECT core.fn_verificar_auditoria() IS NULL"));   // cadeia íntegra após restaurar
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }
}
