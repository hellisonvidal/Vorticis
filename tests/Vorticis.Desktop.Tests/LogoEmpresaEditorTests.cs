using System.IO.Compression;
using Avalonia.Media.Imaging;
using Vorticis.Application.Cadastros;
using Vorticis.Desktop.Comum.ViewModels;

namespace Vorticis.Desktop.Tests;

/// <summary>Campo de logo dos cadastros de empresa: valida, redimensiona, mostra a prévia e remove (headless).</summary>
public class LogoEmpresaEditorTests(SessaoHeadless headless)
{
    private static byte[] PngSolido(int largura, int altura, byte r, byte g, byte b)
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
    public async Task Editor_redimensiona_mostra_previa_remove_e_recusa_formato_invalido()
    {
        var ok = await Task.Run(() => headless.Sessao.Dispatch(() =>
        {
            var editor = new LogoEmpresaEditor();
            Assert.False(editor.TemLogo);
            Assert.Null(editor.Logo);

            Assert.Null(editor.DefinirImagem(PngSolido(2400, 1200, 10, 80, 160)));
            Assert.True(editor.TemLogo);
            Assert.NotNull(editor.Previa);
            Assert.True(editor.Alterado);
            var logo = editor.Logo!;
            Assert.True(logo.UsarNosRelatorios);
            Assert.True(logo.Imagem.Length <= LogoEmpresa.TamanhoMaximo);
            using (var lida = new Bitmap(new MemoryStream(logo.Imagem)))
                Assert.Equal((600, 300), (lida.PixelSize.Width, lida.PixelSize.Height));   // reduzida, mesma proporção

            editor.UsarNosRelatorios = false;
            Assert.False(editor.Logo!.UsarNosRelatorios);

            Assert.Contains("PNG ou JPG", editor.DefinirImagem("GIF89a-----------"u8.ToArray()));
            Assert.True(editor.TemLogo);   // a anterior permanece

            editor.Remover();
            Assert.False(editor.TemLogo);
            Assert.Null(editor.Previa);
            Assert.Null(editor.Logo);

            editor.Carregar(new LogoEmpresa(PngSolido(50, 50, 0, 0, 0), false));
            Assert.False(editor.Alterado);
            Assert.False(editor.UsarNosRelatorios);
            Assert.NotNull(editor.Previa);
            return true;
        }, TestContext.Current.CancellationToken));
        Assert.True(ok);
    }
}
