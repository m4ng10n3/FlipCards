"""La costellazione degli action point.

E' il simbolo del dorso delle carte (Cards/_Final/Back/Template/back_clean.png)
ridisegnato come una figura di mappa stellare nel cielo: il rombo, il rombo
tratteggiato interno, i tre occhi con i loro raggi e gli archi punteggiati
attorno. Le quattro stelle dei vertici sono gli AP e non stanno in questa
immagine: sono due sprite a parte (accesa e spenta) che
ActionPointConstellation anima una per una.

- ``ap_constellation.png``  linee e occhi, 512x512, trasparente.
- ``ap_star_on.png`` / ``ap_star_off.png``  la stella di un vertice, 160x160.

Le posizioni dei vertici sulla tela da 512 sono in ``VERTICES``: il builder le
riprende in MedallionSceneBuilder.ApConstellation.

Uso: python build_constellation.py [cartella_anteprime]
"""
from pathlib import Path
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = Path(__file__).resolve().parent
OUT = HERE.parent
PREVIEW = Path(sys.argv[1]) if len(sys.argv) > 1 else None
SS = 4
N = 512
CX, CY = 256, 256
HX, HY = 214, 214
VERTICES = [(CX, CY - HY), (CX + HX, CY), (CX, CY + HY), (CX - HX, CY)]  # alto, destra, basso, sinistra
LINE = (255, 226, 164)


def S(p):
    return (p[0] * SS, p[1] * SS)


def lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def segment(d, a, b, width, fill, gap_a=0.0, gap_b=0.0, dash=None):
    """Linea da a a b, accorciata ai capi (le stelle ci stanno sopra), piena o tratteggiata."""
    length = np.hypot(b[0] - a[0], b[1] - a[1])
    t0, t1 = gap_a / length, 1 - gap_b / length
    if dash is None:
        d.line([S(lerp(a, b, t0)), S(lerp(a, b, t1))], fill=fill, width=int(width * SS))
        return
    on, off = dash
    t = t0
    step_on, step_off = on / length, off / length
    while t < t1:
        e = min(t1, t + step_on)
        d.line([S(lerp(a, b, t)), S(lerp(a, b, e))], fill=fill, width=int(width * SS))
        t = e + step_off


def sparkle(d, c, r, fill):
    """Stellina a quattro punte, per i punti d'appoggio della figura."""
    x, y = c
    w = r * .28
    d.polygon([S((x, y - r)), S((x + w, y)), S((x, y + r)), S((x - w, y))], fill=fill)
    d.polygon([S((x - r, y)), S((x, y - w)), S((x + r, y)), S((x, y + w))], fill=fill)


def bezier(p0, p1, p2, n=40):
    t = np.linspace(0, 1, n)[:, None]
    pts = (1 - t) ** 2 * np.array(p0) + 2 * (1 - t) * t * np.array(p1) + t ** 2 * np.array(p2)
    return [tuple(p) for p in pts]


def eye(d, c, w, h, rays_up):
    x, y = c
    left, right = (x - w / 2, y), (x + w / 2, y)
    upper = bezier(left, (x, y - h), right)
    lower = bezier(left, (x, y + h), right)
    d.line([S(p) for p in upper], fill=LINE + (235,), width=int(2.6 * SS), joint='curve')
    d.line([S(p) for p in lower], fill=LINE + (235,), width=int(2.6 * SS), joint='curve')
    r = h * .52
    d.ellipse([S((x - r, y - r)), S((x + r, y + r))], outline=LINE + (230,), width=int(2.4 * SS))
    sparkle(d, (x, y), r * .62, LINE + (255,))
    for p in (left, right):
        sparkle(d, p, 7, LINE + (240,))
    # Raggi a ventaglio, come sul dorso: sopra l'occhio alto, sotto quelli bassi.
    sign = -1 if rays_up else 1
    for k in range(-2, 3):
        a = np.radians(90 + k * 17)
        inner = (x + np.cos(a) * (h + 10) * (1 if not rays_up else 1), y + sign * np.sin(a) * (h + 10))
        outer = (x + np.cos(a) * (h + 30), y + sign * np.sin(a) * (h + 30 + (6 if k == 0 else 0)))
        d.line([S(inner), S(outer)], fill=LINE + (170,), width=int(2 * SS))


def constellation():
    img = Image.new('RGBA', (N * SS, N * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # Il rombo: quattro tratti fra le stelle, interrotti vicino a ciascuna.
    for i in range(4):
        segment(d, VERTICES[i], VERTICES[(i + 1) % 4], 2.8, LINE + (215,), 40, 40)
    # Rombo interno tratteggiato.
    inner = [lerp((CX, CY), v, .80) for v in VERTICES]
    for i in range(4):
        segment(d, inner[i], inner[(i + 1) % 4], 2.2, LINE + (150,), 8, 8, dash=(9, 9))
    # Archi punteggiati ai lati, come gli anelli attorno al rombo del dorso.
    for side in (-1, 1):
        for radius, alpha in ((150, 120), (178, 80)):
            for a in np.linspace(-62, 62, 17 if radius == 150 else 21):
                t = np.radians(a)
                px = CX + side * (radius * np.cos(t) + 18)
                py = CY + radius * np.sin(t) * 1.08
                r = 2.2 if radius == 150 else 1.7
                d.ellipse([S((px - r, py - r)), S((px + r, py + r))], fill=LINE + (alpha,))
    # I tre occhi, uno sopra e due sotto.
    eye(d, (CX, CY - 44), 136, 34, True)
    eye(d, (CX - 64, CY + 44), 114, 29, False)
    eye(d, (CX + 64, CY + 44), 114, 29, False)

    sharp = img.resize((N, N), Image.LANCZOS)
    # Bagliore morbido sotto il tratto, come per le stelle vicine del cielo.
    glow = sharp.filter(ImageFilter.GaussianBlur(5))
    a = np.array(sharp).astype(np.float32)
    g = np.array(glow).astype(np.float32)
    alpha = np.clip(a[..., 3] + g[..., 3] * .9, 0, 255)
    rgb = np.where(a[..., 3:4] > 0, a[..., :3], g[..., :3])
    out = Image.fromarray(np.concatenate([rgb, alpha[..., None]], -1).astype(np.uint8), 'RGBA')
    out.save(OUT / 'ap_constellation.png')
    return out


def star(lit):
    n = 160
    yy, xx = np.mgrid[0:n * SS, 0:n * SS].astype(np.float32) / SS
    dx, dy = xx - n / 2, yy - n / 2
    r = np.sqrt(dx * dx + dy * dy)
    if lit:
        core = np.exp(-(r / 7) ** 2)
        halo = np.exp(-(r / 30) ** 2) * .55
        long_arms = (np.exp(-np.abs(dx) / 1.6) * np.exp(-(dy / 62) ** 2) + np.exp(-np.abs(dy) / 1.6) * np.exp(-(dx / 62) ** 2))
        ux, uy = (dx + dy) / np.sqrt(2), (dx - dy) / np.sqrt(2)
        short_arms = (np.exp(-np.abs(ux) / 1.2) * np.exp(-(uy / 22) ** 2) + np.exp(-np.abs(uy) / 1.2) * np.exp(-(ux / 22) ** 2)) * .6
        a = np.clip(core * 1.4 + halo + long_arms * .95 + short_arms, 0, 1)
        white = np.exp(-(r / 9) ** 2)
        rgb = np.stack([255 * np.ones_like(r), 214 + 41 * white, 140 + 115 * white], -1)
    else:
        # Stella spenta: il contorno di una stella a quattro punte, freddo e opaco.
        img = Image.new('L', (n * SS, n * SS), 0)
        d = ImageDraw.Draw(img)
        c, R, w = n / 2, 30, 8
        pts = [(c, c - R), (c + w, c - w), (c + R, c), (c + w, c + w), (c, c + R), (c - w, c + w), (c - R, c), (c - w, c - w)]
        d.polygon([(x * SS, y * SS) for x, y in pts], outline=255, width=int(2.4 * SS))
        d.ellipse([(c - 2.2) * SS, (c - 2.2) * SS, (c + 2.2) * SS, (c + 2.2) * SS], fill=255)
        a = np.array(img).astype(np.float32) / 255 * .8
        rgb = np.stack([np.full_like(r, 150), np.full_like(r, 170), np.full_like(r, 178)], -1)
    out = np.concatenate([rgb, (a * 255)[..., None]], -1).astype(np.uint8)
    im = Image.fromarray(out, 'RGBA').resize((n, n), Image.LANCZOS)
    im.save(OUT / ('ap_star_on.png' if lit else 'ap_star_off.png'))
    return im


if __name__ == '__main__':
    fig = constellation()
    on, off = star(True), star(False)
    print('vertices', VERTICES)
    if PREVIEW:
        PREVIEW.mkdir(parents=True, exist_ok=True)
        sheet = Image.new('RGBA', (N, N), (8, 16, 30, 255))
        sheet.alpha_composite(fig)
        for i, v in enumerate(VERTICES):
            s = on if i < 3 else off
            sheet.alpha_composite(s, (int(v[0] - 80), int(v[1] - 80)))
        sheet.convert('RGB').save(PREVIEW / 'ap_constellation.png')
