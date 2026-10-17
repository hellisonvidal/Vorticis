"""Gera o ícone do Perseus Contábil (PerseusContabil.ico e PNGs).

Monograma "P" branco sobre azul-marinho, com a constelação de Perseu em dourado e a estrela Mirfak
(alfa de Perseu) brilhando no bojo do P; nos tamanhos grandes, o nome PERSEUS embaixo.
Uso: python3 instalador/icone/gerar_icone.py   (requer Pillow)
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

RAIZ = Path(__file__).resolve().parents[2]
FONTE = RAIZ / "src/Perseus.Infra/Dados/Fontes/LiberationSans-Bold.ttf"
SAIDA = Path(__file__).resolve().parent
N = 1024  # desenho em alta resolução, reduzido para cada tamanho

AZUL_TOPO, AZUL_BASE = (24, 48, 82), (43, 92, 143)
OURO, OURO_CLARO = (232, 184, 74), (255, 226, 150)


def fundo() -> Image.Image:
    grad = Image.new("RGB", (N, N))
    d = ImageDraw.Draw(grad)
    for y in range(N):
        t = y / (N - 1)
        d.line([(0, y), (N, y)], fill=tuple(round(a + (b - a) * t) for a, b in zip(AZUL_TOPO, AZUL_BASE)))
    mascara = Image.new("L", (N, N), 0)
    ImageDraw.Draw(mascara).rounded_rectangle([0, 0, N - 1, N - 1], radius=210, fill=255)
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    img.paste(grad, (0, 0), mascara)
    # brilho suave no canto superior
    luz = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    ImageDraw.Draw(luz).ellipse([-420, -560, 900, 360], fill=(255, 255, 255, 22))
    luz = luz.filter(ImageFilter.GaussianBlur(140))
    luz.putalpha(Image.composite(luz.getchannel("A"), Image.new("L", (N, N), 0), mascara))
    return Image.alpha_composite(img, luz)


def estrela(d: ImageDraw.ImageDraw, cx: float, cy: float, r: float, cor) -> None:
    """Estrela de 4 pontas (cintilação)."""
    f = r * 0.22
    d.polygon([(cx, cy - r), (cx + f, cy - f), (cx + r, cy), (cx + f, cy + f),
               (cx, cy + r), (cx - f, cy + f), (cx - r, cy), (cx - f, cy - f)], fill=cor)


def desenhar(com_nome: bool, detalhes: bool) -> Image.Image:
    img = fundo()
    camada = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(camada)

    # Constelação de Perseu (estilizada) em dourado, atrás do monograma
    pontos = [(800, 160), (742, 300), (668, 372), (842, 420), (900, 560), (610, 470), (560, 600), (760, 640)]
    ligacoes = [(0, 1), (1, 2), (1, 3), (3, 4), (2, 5), (5, 6), (4, 7)]
    if detalhes:
        for a, b in ligacoes:
            d.line([pontos[a], pontos[b]], fill=OURO + (110,), width=7)
        for i, (x, y) in enumerate(pontos):
            if i != 2:
                d.ellipse([x - 13, y - 13, x + 13, y + 13], fill=OURO + (230,))

    # Monograma P (geométrico): haste + bojo vazado
    y0 = 170 if com_nome else 200
    y1 = 760 if com_nome else 850
    haste_x0, haste_x1 = 230, 390
    bojo_y1 = y0 + (y1 - y0) * 0.66
    raio = (bojo_y1 - y0) / 2
    cx, cy = 560, y0 + raio
    p = Image.new("L", (N, N), 0)
    dp = ImageDraw.Draw(p)
    dp.rounded_rectangle([haste_x0, y0, haste_x1, y1], radius=22, fill=255)
    dp.rectangle([haste_x0, y0, cx, bojo_y1], fill=255)
    dp.ellipse([cx - raio, y0, cx + raio, bojo_y1], fill=255)
    furo = raio * 0.45
    dp.rectangle([haste_x1, cy - furo, cx, cy + furo], fill=0)
    dp.ellipse([cx - furo, cy - furo, cx + furo, cy + furo], fill=0)
    sombra = Image.new("RGBA", (N, N), (6, 18, 36, 0))
    sombra.putalpha(p.filter(ImageFilter.GaussianBlur(18)).point(lambda a: a * 0.45))
    camada = Image.alpha_composite(camada, Image.new("RGBA", (N, N), (0, 0, 0, 0)))
    img = Image.alpha_composite(img, camada)
    img.alpha_composite(sombra, (10, 16))
    branco = Image.new("RGBA", (N, N), (255, 255, 255, 255))
    img.paste(branco, (0, 0), p)

    # Mirfak, a estrela mais brilhante de Perseu, cintilando no bojo do P
    d2 = ImageDraw.Draw(img)
    halo = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    ImageDraw.Draw(halo).ellipse([cx - 90, cy - 90, cx + 90, cy + 90], fill=OURO + (90,))
    img = Image.alpha_composite(img, halo.filter(ImageFilter.GaussianBlur(40)))
    d2 = ImageDraw.Draw(img)
    estrela(d2, cx, cy, furo * 1.55, OURO_CLARO)
    estrela(d2, cx, cy, furo * 0.9, (255, 250, 230))

    if com_nome:
        fonte = ImageFont.truetype(str(FONTE), 150)
        texto = "PERSEUS"
        larg = d2.textlength(texto, font=fonte) + 6 * 18
        x = (N - larg) / 2
        for ch in texto:  # espaçamento entre letras
            d2.text((x, 800), ch, font=fonte, fill=(255, 255, 255))
            x += d2.textlength(ch, font=fonte) + 18
        d2.rounded_rectangle([300, 975, 724, 985], radius=5, fill=OURO)
    return img


def main() -> None:
    grande = desenhar(com_nome=True, detalhes=True)
    medio = desenhar(com_nome=False, detalhes=True)
    pequeno = desenhar(com_nome=False, detalhes=False)
    tamanhos = {256: grande, 128: grande, 64: medio, 48: medio, 32: pequeno, 24: pequeno, 16: pequeno}
    imagens = {t: src.resize((t, t), Image.LANCZOS) for t, src in tamanhos.items()}
    imagens[256].save(SAIDA / "PerseusContabil.ico", format="ICO",
                      sizes=[(t, t) for t in sorted(imagens)], append_images=[imagens[t] for t in sorted(imagens) if t != 256])
    grande.resize((512, 512), Image.LANCZOS).save(SAIDA / "PerseusContabil-512.png")
    imagens[256].save(SAIDA / "PerseusContabil-256.png")
    print("ícone gerado em", SAIDA)


if __name__ == "__main__":
    main()
