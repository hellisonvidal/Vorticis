using System.IO.Compression;
using Vorticis.Application.Cadastros;
using Vorticis.Application.Relatorios;
using Vorticis.Infra.Relatorios;

namespace Vorticis.Infra.Tests;

/// <summary>Logo da empresa: gravação no banco e cabeçalho do PDF.</summary>
public class LogoEmpresaTests(BancoDeTeste db) : IClassFixture<BancoDeTeste>
{
    /// <summary>PNG RGB de uma cor só, montado à mão (sem biblioteca de imagem).</summary>
    public static byte[] PngSolido(int largura, int altura, byte r, byte g, byte b)
    {
        static uint Crc(ReadOnlySpan<byte> d)
        {
            var c = 0xFFFFFFFFu;
            foreach (var x in d) { c ^= x; for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; }
            return ~c;
        }
        static void Chunk(Stream o, string tipo, byte[] dados)
        {
            var corpo = System.Text.Encoding.ASCII.GetBytes(tipo).Concat(dados).ToArray();
            o.Write(BitConverter.GetBytes(dados.Length).Reverse().ToArray());
            o.Write(corpo);
            o.Write(BitConverter.GetBytes(Crc(corpo)).Reverse().ToArray());
        }
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(ms, "IHDR", [.. BitConverter.GetBytes(largura).Reverse(), .. BitConverter.GetBytes(altura).Reverse(), 8, 2, 0, 0, 0]);
        var cru = new List<byte>();
        for (var y = 0; y < altura; y++) { cru.Add(0); for (var x = 0; x < largura; x++) cru.AddRange([r, g, b]); }
        using var z = new MemoryStream();
        using (var zs = new ZLibStream(z, CompressionLevel.Optimal, true)) zs.Write(cru.ToArray());
        Chunk(ms, "IDAT", z.ToArray());
        Chunk(ms, "IEND", []);
        return ms.ToArray();
    }

    [Fact]
    public void Validacao_aceita_png_e_jpg_e_recusa_outros_formatos_e_imagens_grandes()
    {
        Assert.Null(LogoEmpresa.Validar(PngSolido(20, 10, 0, 0, 255)));
        Assert.Contains("PNG ou JPG", LogoEmpresa.Validar("GIF89a-----------"u8.ToArray()));
        Assert.Contains("máximo", LogoEmpresa.Validar([.. PngSolido(20, 10, 0, 0, 255), .. new byte[LogoEmpresa.TamanhoMaximo]]));
        Assert.Null(LogoEmpresa.Validar([0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0]));
    }

    [Fact]
    public async Task Grava_le_atualiza_e_remove_a_logo_da_empresa()
    {
        BancoDeTeste.ExigirBanco();
        var ct = TestContext.Current.CancellationToken;
        var svc = db.App.Empresas;
        var id = await db.CriarEmpresaAsync("11444777000161");
        Assert.Null(await svc.ObterLogoAsync(id, ct));

        var png = PngSolido(120, 60, 200, 30, 30);
        Assert.True((await svc.SalvarLogoAsync(id, new LogoEmpresa(png, true), ct)).Sucesso);
        var lida = (await svc.ObterLogoAsync(id, ct))!;
        Assert.Equal(png, lida.Imagem);
        Assert.True(lida.UsarNosRelatorios);

        Assert.True((await svc.SalvarLogoAsync(id, lida with { UsarNosRelatorios = false }, ct)).Sucesso);
        Assert.False((await svc.ObterLogoAsync(id, ct))!.UsarNosRelatorios);

        Assert.False((await svc.SalvarLogoAsync(id, new LogoEmpresa("texto qualquer aqui"u8.ToArray(), true), ct)).Sucesso);
        Assert.False((await svc.SalvarLogoAsync(long.MaxValue, lida, ct)).Sucesso);

        Assert.True((await svc.SalvarLogoAsync(id, null, ct)).Sucesso);
        Assert.Null(await svc.ObterLogoAsync(id, ct));
    }

    [Fact]
    public void Pdf_com_logo_poe_a_imagem_no_cabecalho_sem_sobrepor_titulo_nem_corpo()
    {
        if (!File.Exists("/usr/bin/pdftotext")) return;
        var cab = new[] { "EMPRESA EXEMPLO LTDA", "CNPJ 11.444.777/0001-61", "Período: 01/01/2026 a 31/12/2026" };
        var linhas = Enumerable.Range(1, 5).Select(i => new LinhaTabela(["Item " + i, "10,00"])).ToList();
        var bloco = new Bloco(Titulo: "Bloco", Colunas: [new Coluna("Descrição", 8), new Coluna("Valor", 3, Alinhamento.Direita)], Linhas: linhas);
        var logo = PngSolido(300, 120, 20, 70, 160);
        var sem = new GeradorPdf().Gerar(new DocumentoRelatorio("BALANÇO PATRIMONIAL", cab, [bloco])).Conteudo;
        var com = new GeradorPdf().Gerar(new DocumentoRelatorio("BALANÇO PATRIMONIAL", cab, [bloco], Logo: logo)).Conteudo;
        Assert.True(com.Length > sem.Length);

        // a logo "ambiente" (empresa ativa) também entra; sem ela o PDF volta ao tamanho original
        LogoRelatorios.Atual = logo;
        try { Assert.Equal(com.Length, new GeradorPdf().Gerar(new DocumentoRelatorio("BALANÇO PATRIMONIAL", cab, [bloco])).Conteudo.Length); }
        finally { LogoRelatorios.Atual = null; }

        var dir = Environment.GetEnvironmentVariable("VORTICIS_LOGO_SAIDA") ?? Path.GetTempPath();
        var arq = Path.Combine(dir, "vorticis_logo.pdf");
        File.WriteAllBytes(arq, com);
        string Bbox(string a)
        {
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("/usr/bin/pdftotext", ["-bbox", a, "-"]) { RedirectStandardOutput = true })!;
            var x = p.StandardOutput.ReadToEnd(); p.WaitForExit(); return x;
        }
        var semArq = Path.Combine(dir, "vorticis_sem_logo.pdf");
        File.WriteAllBytes(semArq, sem);
        double XMin(string xml, string palavra) => System.Text.RegularExpressions.Regex.Matches(xml, $"xMin=\"([0-9.]+)\" yMin=\"[0-9.]+\" xMax=\"[0-9.]+\" yMax=\"[0-9.]+\">{palavra}<")
            .Select(m => double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).Min();
        double YMin(string xml, string palavra) => System.Text.RegularExpressions.Regex.Matches(xml, $"yMin=\"([0-9.]+)\" xMax=\"[0-9.]+\" yMax=\"[0-9.]+\">{palavra}<")
            .Select(m => double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).Min();
        var xmlCom = Bbox(arq);
        Assert.True(XMin(xmlCom, "EMPRESA") > XMin(Bbox(semArq), "EMPRESA") + 30, "a identificação deve ficar à direita da logo");
        Assert.True(YMin(xmlCom, "BALANÇO") > YMin(xmlCom, "CNPJ"), "título abaixo da identificação");
        Assert.True(YMin(xmlCom, "Item") > YMin(xmlCom, "BALANÇO") + 10, "corpo abaixo do título");
    }
}
