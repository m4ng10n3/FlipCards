"""La legenda arrotolata, posata sul tavolo.

Un rotolo di pergamena visto di fianco: il cilindro di carta con la sua luce
(parchment.png avvolto con la mappa dell'arcoseno, cosi' la grana si stringe
verso i bordi come su un cilindro vero) e il tratteggio d'inchiostro nella
parte in ombra; ai due capi i tappi d'ottone presi da roller.png; al centro un
nastro ottanio con il sigillo di ceralacca; il titolo scritto a inchiostro sulla
carta.

Un cilindro coricato si vede uguale da qualunque altezza, quindi il rotolo sta
sullo schermo come il fungo e la leva, non sul piano inclinato del tavolo:
sul tavolo ci va solo la sua ombra.

- ``scroll_closed.png``  1000x300, trasparente.

Uso: python build_scroll.py [anteprima.png]
"""
from pathlib import Path
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

HERE = Path(__file__).resolve().parent
OUT = HERE.parent
SRC = HERE.parents[1] / '11_GraphicUpgrade'
PREVIEW = Path(sys.argv[1]) if len(sys.argv) > 1 else None
W, H = 1000, 300
CY, R = 146, 62
X0, X1 = 222, 778
INK = np.array([22, 16, 10], np.float32)


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def roll_body():
    paper = Image.open(SRC / 'parchment.png').convert('RGB')
    paper = paper.crop((200, 150, paper.width - 200, paper.height - 150)).resize((X1 - X0, 2 * R * 3), Image.BICUBIC)
    tex = np.array(paper).astype(np.float32)
    out = np.zeros((H, W, 4), np.float32)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    t = (yy - CY) / R
    inside = (np.abs(t) <= 1) & (xx >= X0) & (xx < X1)
    tc = np.clip(t, -1, 1)
    # Mappa del cilindro: la riga della texture segue l'angolo, non l'altezza.
    v = ((np.arcsin(tc) / np.pi + .5) * (tex.shape[0] - 1)).astype(int)
    u = np.clip(xx - X0, 0, X1 - X0 - 1).astype(int)
    rgb = tex[v, u]
    # Luce dall'alto a sinistra: riflesso in alto, ombra calda sotto.
    lambert = np.sqrt(np.clip(1 - tc * tc, 0, 1))
    light = .50 + .42 * lambert + .22 * np.exp(-((tc + .45) / .22) ** 2) - .18 * smooth(.3, 1, tc)
    light *= .96 + .06 * (1 - (xx - X0) / (X1 - X0))
    rgb = rgb * light[..., None]
    # Tratteggio d'inchiostro lungo l'asse del rotolo, piu' fitto dove e' buio.
    darkness = np.clip(1 - light, 0, 1)
    hatch = (np.sin(yy * 1.9) > (1.1 - darkness * 2.4)) & (tc > .05)
    rgb[hatch] = rgb[hatch] * .55 + INK * .45
    # Il bordo libero del foglio che gira attorno al rotolo, poco sotto il riflesso.
    seam = np.abs(tc - .18) < .035
    rgb[seam & inside] = rgb[seam & inside] * .6 + INK * .4
    out[..., :3] = rgb
    out[..., 3] = inside * 255
    # Filetto d'inchiostro sopra e sotto.
    edge = inside & (np.abs(np.abs(t) - 1) < 3.2 / R)
    out[edge, :3] = INK
    img = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), 'RGBA')
    return img


def caps(img):
    roller = Image.open(SRC / 'roller.png').convert('RGBA')
    strip = roller.crop((0, 279, roller.width, 479))
    left = strip.crop((0, 0, 292, 200))
    right = strip.crop((strip.width - 292, 0, strip.width, 200))
    scale = (2 * R * 1.42) / 200
    size = (int(292 * scale), int(200 * scale))
    left = left.resize(size, Image.LANCZOS)
    right = right.resize(size, Image.LANCZOS)
    img.alpha_composite(left, (X0 + 26 - size[0], CY - size[1] // 2))
    img.alpha_composite(right, (X1 - 26, CY - size[1] // 2))


def ribbon(img):
    d = ImageDraw.Draw(img)
    cx = 610
    teal = (28, 84, 88, 255)
    gold = (206, 166, 92, 255)
    ink = (22, 16, 10, 255)
    # Fascia attorno al rotolo: segue la curva del cilindro.
    pts_top = [(cx - 24 + 4 * np.sin(a), CY - R * np.cos(a)) for a in np.linspace(0, 0, 1)]
    for off, col in ((-26, ink), (-23, teal), (23, ink)):
        pass
    band = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    bd = ImageDraw.Draw(band)
    for y in range(CY - R, CY + R + 1):
        t = (y - CY) / R
        bulge = 5 * np.sqrt(max(0.0, 1 - t * t))
        shade = .55 + .45 * np.sqrt(max(0.0, 1 - t * t)) - .15 * max(0.0, t)
        c = tuple(int(v * shade) for v in teal[:3]) + (255,)
        bd.line([(cx - 22 - bulge, y), (cx + 22 + bulge, y)], fill=c)
        bd.point((cx - 22 - bulge, y), fill=ink)
        bd.point((cx + 22 + bulge, y), fill=ink)
        g = tuple(int(v * shade) for v in gold[:3]) + (255,)
        bd.point((cx - 17 - bulge * .8, y), fill=g)
        bd.point((cx + 17 + bulge * .8, y), fill=g)
    img.alpha_composite(band)
    # Code del nastro che cadono davanti al rotolo.
    for side in (-1, 1):
        tail = [(cx + side * 6, CY + 18), (cx + side * 34, CY + R + 58), (cx + side * 20, CY + R + 48),
                (cx + side * 8, CY + R + 66), (cx - side * 6, CY + 26)]
        d.polygon(tail, fill=teal, outline=ink)
        d.line([(cx + side * 10, CY + 28), (cx + side * 24, CY + R + 46)], fill=gold, width=2)
    # Sigillo di ceralacca con la stella.
    r = 30
    d.ellipse([cx - r - 3, CY - r + 7, cx + r + 3, CY + r + 13], fill=(90, 18, 14, 255))
    d.ellipse([cx - r, CY - r + 4, cx + r, CY + r + 4], fill=(150, 30, 22, 255), outline=ink, width=3)
    d.ellipse([cx - r + 8, CY - r + 12, cx + r - 8, CY + r - 4], outline=(108, 20, 16, 255), width=3)
    for k in range(8):
        a = k * np.pi / 4 - np.pi / 2
        rr = 16 if k % 2 == 0 else 8
        d.polygon([(cx, CY + 4), (cx + np.cos(a - .28) * 5, CY + 4 + np.sin(a - .28) * 5),
                   (cx + np.cos(a) * rr, CY + 4 + np.sin(a) * rr), (cx + np.cos(a + .28) * 5, CY + 4 + np.sin(a + .28) * 5)],
                  fill=(206, 70, 50, 255))
    d.ellipse([cx - r + 6, CY - r + 8, cx - r + 18, CY - r + 16], fill=(230, 140, 110, 180))


def title(img):
    d = ImageDraw.Draw(img)
    font = ImageFont.truetype('C:/Windows/Fonts/georgiab.ttf', 44)
    text = 'LEGENDA'
    spacing = 9
    widths = [d.textlength(ch, font=font) for ch in text]
    total = sum(widths) + spacing * (len(text) - 1)
    x = 410 - total / 2
    y = CY - 30
    for ch, w in zip(text, widths):
        d.text((x + 1.5, y + 2), ch, font=font, fill=(255, 240, 205, 120))
        d.text((x, y), ch, font=font, fill=(34, 24, 14, 235))
        x += w + spacing


if __name__ == '__main__':
    img = roll_body()
    title(img)
    ribbon(img)
    caps(img)
    img.save(OUT / 'scroll_closed.png')
    print('saved', OUT / 'scroll_closed.png')
    if PREVIEW:
        bg = Image.new('RGBA', img.size, (24, 60, 50, 255))
        bg.alpha_composite(img)
        bg.convert('RGB').save(PREVIEW)
