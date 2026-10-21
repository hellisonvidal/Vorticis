"""Gera o ícone do Vorticis Contábil (VorticisContabil.ico e PNGs) a partir da logo (logo-vorticis.jpg).

Usa só o símbolo (a espiral à esquerda da logo): o nome VORTICIS fica ilegível nos tamanhos de ícone.
O fundo branco da logo vira transparente.
Uso: python3 instalador/icone/gerar_icone.py   (requer Pillow)
"""
from pathlib import Path

from PIL import Image, ImageChops

PASTA = Path(__file__).resolve().parent
LOGO = PASTA / "logo-vorticis.jpg"
SIMBOLO = (100, 355, 409, 669)   # caixa do símbolo na logo de 1024 x 1024 (esq, topo, dir, base)
MARGEM = 0.04                    # folga em volta do símbolo, proporcional ao lado
TAMANHOS = [16, 20, 24, 32, 40, 48, 64, 128, 256]


def simbolo() -> Image.Image:
    rgb = Image.open(LOGO).convert("RGB").crop(SIMBOLO)
    # transparência pela distância ao branco (bordas suaves, sem halo claro)
    escuro = ImageChops.invert(rgb.convert("L"))
    alfa = escuro.point(lambda v: 0 if v < 12 else min(255, round((v - 12) * 255 / 120)))
    img = rgb.convert("RGBA")
    img.putalpha(alfa)
    lado = round(max(img.size) * (1 + 2 * MARGEM))
    quadro = Image.new("RGBA", (lado, lado), (0, 0, 0, 0))
    quadro.paste(img, ((lado - img.width) // 2, (lado - img.height) // 2), img)
    return quadro


def main() -> None:
    base = simbolo()
    grande = base.resize((512, 512), Image.LANCZOS)
    grande.save(PASTA / "VorticisContabil-512.png")
    base.resize((256, 256), Image.LANCZOS).save(PASTA / "VorticisContabil-256.png")
    grande.save(PASTA / "VorticisContabil.ico", format="ICO", sizes=[(t, t) for t in TAMANHOS])


if __name__ == "__main__":
    main()
