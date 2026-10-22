"""Gera os ícones da suíte Vorticis a partir da logo (logo-vorticis.jpg):

  VorticisContabil.ico / -256.png / -512.png  — espiral verde da logo (como é)
  VorticisFiscal.ico   / -256.png / -512.png  — espiral recolorida para o âmbar/laranja do módulo Fiscal
  VorticisFolha.ico    / -256.png / -512.png  — espiral recolorida para o roxo do módulo Folha de Pagamento

Usa só o símbolo (a espiral à esquerda da logo): o nome VORTICIS fica ilegível nos tamanhos de ícone.
O fundo branco da logo vira transparente. Nos módulos Fiscal e Folha só o verde da espiral é recolorido
(preservando o brilho de cada pixel); o contorno azul-marinho da logo é mantido.
As cores dos módulos devem ser as mesmas de IModuloDesktop.CorDestaque (ModuloFiscal, ModuloFolha).
Uso: python3 -I instalador/icone/gerar_icone.py   (requer Pillow e numpy)
"""
import colorsys
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops

PASTA = Path(__file__).resolve().parent
LOGO = PASTA / "logo-vorticis.jpg"
SIMBOLO = (100, 355, 409, 669)   # caixa do símbolo na logo de 1024 x 1024 (esq, topo, dir, base)
MARGEM = 0.04                    # folga em volta do símbolo, proporcional ao lado
TAMANHOS = [16, 20, 24, 32, 40, 48, 64, 128, 256]
VERDE_LOGO = "#1B7F5A"           # verde de referência da espiral (cor do módulo Contábil)

# Módulo -> cor da espiral (None = mantém o verde original da logo)
MODULOS = {
    "VorticisContabil": None,
    "VorticisFiscal": "#C2410C",
    "VorticisFolha": "#6D28D9",
}


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


def hex_rgb(cor: str) -> tuple[float, float, float]:
    return tuple(int(cor[i:i + 2], 16) / 255 for i in (1, 3, 5))  # type: ignore[return-value]


def recolorir(base: Image.Image, cor: str) -> Image.Image:
    """Troca o verde da espiral pela cor do módulo, mantendo o tom (claro/escuro) de cada pixel e o azul-marinho."""
    a = np.asarray(base).astype(np.float32) / 255.0
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    maximo, minimo = a[..., :3].max(axis=-1), a[..., :3].min(axis=-1)
    delta = maximo - minimo
    # matiz em graus (0-360); verde da logo ~ 140-170, azul-marinho ~ 210-225
    h = np.zeros_like(maximo)
    com_cor = delta > 1e-6
    mr = (maximo == r) & com_cor
    mg = (maximo == g) & com_cor & ~mr
    mb = com_cor & ~mr & ~mg
    h[mr] = ((g - b)[mr] / delta[mr]) % 6
    h[mg] = (b - r)[mg] / delta[mg] + 2
    h[mb] = (r - g)[mb] / delta[mb] + 4
    h *= 60
    sat = np.where(maximo > 0, delta / np.maximum(maximo, 1e-6), 0)
    # peso do "verde": 1 até 175 graus, cai a 0 em 195 (transição suave para o azul-marinho)
    peso = np.clip((195 - h) / 20, 0, 1) * np.clip(sat / 0.25, 0, 1)
    # brilho relativo ao verde de referência da logo
    ref = colorsys.rgb_to_hsv(*hex_rgb(VERDE_LOGO))[2]
    fator = np.clip(maximo / ref, 0, 1.25)
    ch, cs, cv = colorsys.rgb_to_hsv(*hex_rgb(cor))
    alvo = np.array(colorsys.hsv_to_rgb(ch, cs, 1.0), dtype=np.float32)
    pleno = alvo[None, None, :] * np.clip(fator * cv, 0, 1)[..., None]
    # pixels pouco saturados (borda suave, perto do branco) mantêm a cor original
    claro = 1 - np.clip(sat / 0.9, 0, 1)[..., None]
    novo = pleno * (1 - claro) + a[..., :3] * claro
    a[..., :3] = np.clip(a[..., :3] * (1 - peso[..., None]) + novo * peso[..., None], 0, 1)
    return Image.fromarray((a * 255).round().astype(np.uint8), "RGBA")


def main() -> None:
    base = simbolo()
    for nome, cor in MODULOS.items():
        img = base if cor is None else recolorir(base, cor)
        grande = img.resize((512, 512), Image.LANCZOS)
        grande.save(PASTA / f"{nome}-512.png")
        img.resize((256, 256), Image.LANCZOS).save(PASTA / f"{nome}-256.png")
        grande.save(PASTA / f"{nome}.ico", format="ICO", sizes=[(t, t) for t in TAMANHOS])


if __name__ == "__main__":
    main()
