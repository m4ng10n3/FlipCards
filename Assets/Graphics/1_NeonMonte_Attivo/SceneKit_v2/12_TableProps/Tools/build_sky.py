"""Cielo rotante: un disco di stelle e nebulosa da far girare attorno a un polo.

Il cielo sta dietro il tavolo e ruota lentamente attorno a un punto sotto il
bordo dello schermo (Animated Illustrated Surface, modo 0). Per coprire lo
schermo a qualunque angolo serve un'immagine quadrata grande quanto il
diametro del cerchio che contiene tutto il cielo visibile: il vecchio sky.png
16:9 si poteva solo far ondeggiare.

Niente punti focali: girando, ogni parte del disco passa sopra la cassa.
Nebulosa ottanio scura con filamenti, stelline calde d'oro antico, qualche
stella a quattro punte incisa. Le stelle grandi e scintillanti in primo piano
le aggiunge lo shader, su un secondo strato che gira un po' piu' veloce.

Uso: python build_sky.py [lato] [anteprima.png]
"""
from pathlib import Path
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = Path(__file__).resolve().parent
OUT = HERE.parent / 'sky_disc.png'
SIZE = int(sys.argv[1]) if len(sys.argv) > 1 else 4096
PREVIEW = Path(sys.argv[2]) if len(sys.argv) > 2 else None
rng = np.random.default_rng(20260916)


def fractal(n, beta, seed):
    """Rumore frattale periodico 1/f^beta, normalizzato a media 0 e deviazione 1."""
    r = np.random.default_rng(seed)
    white = r.standard_normal((n, n)).astype(np.float32)
    f = np.fft.fftfreq(n).astype(np.float32)
    k = np.sqrt(f[:, None] ** 2 + f[None, :] ** 2)
    k[0, 0] = 1
    spec = np.fft.fft2(white) / k ** (beta / 2)
    spec[0, 0] = 0
    out = np.real(np.fft.ifft2(spec)).astype(np.float32)
    return (out - out.mean()) / (out.std() + 1e-6)


def warp_sample(field, dx, dy):
    """Campionamento bilineare periodico con spostamento (in pixel)."""
    n = field.shape[0]
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    x = (xx + dx) % n
    y = (yy + dy) % n
    xf = np.floor(x); yf = np.floor(y)
    fx = x - xf; fy = y - yf
    # In float32 il modulo puo' arrotondare a n: si riporta l'indice nel campo.
    x0 = xf.astype(np.int32) % n; y0 = yf.astype(np.int32) % n
    x1 = (x0 + 1) % n; y1 = (y0 + 1) % n
    return (field[y0, x0] * (1 - fx) * (1 - fy) + field[y0, x1] * fx * (1 - fy)
            + field[y1, x0] * (1 - fx) * fy + field[y1, x1] * fx * fy)


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def main():
    work = 2048  # la nebulosa e' morbida: si calcola a meta' e si ingrandisce
    base = fractal(work, 3.1, 1)
    wx = fractal(work, 3.4, 2) * 70
    wy = fractal(work, 3.4, 3) * 70
    warped = warp_sample(base, wx, wy)
    detail = warp_sample(fractal(work, 2.5, 4), wx * .6, wy * .6)
    ridges = 1 - np.abs(warp_sample(fractal(work, 2.6, 5), wx * 1.4, wy * 1.4))
    ridges = ridges ** 5

    # Una fascia lattea larga che attraversa il disco, deformata dal rumore.
    yy, xx = np.mgrid[0:work, 0:work].astype(np.float32) / work
    band_axis = (xx * .62 + yy * .78) - .70 + fractal(work, 3.6, 6) * .06
    band = np.exp(-(band_axis / .23) ** 2)
    band2 = np.exp(-(((xx * -.8 + yy * .6) + .05 + fractal(work, 3.6, 7) * .05) / .16) ** 2) * .55

    cloud = smooth(.1, 2.4, warped + detail * .22) * (.35 + .65 * np.maximum(band, band2))
    filaments = ridges * smooth(-.4, 1.4, warped) * (.3 + .7 * np.maximum(band, band2))
    lanes = smooth(.6, 2.0, fractal(work, 2.8, 8)) * cloud  # polvere scura dentro la nebulosa

    deep = np.array([4, 8, 15], np.float32)
    navy = np.array([9, 18, 32], np.float32)
    teal = np.array([22, 62, 68], np.float32)
    glow = np.array([48, 104, 104], np.float32)
    rgb = deep + (navy - deep) * smooth(-1.5, 1.5, fractal(work, 3.8, 9))[..., None]
    rgb = rgb + (teal - rgb) * np.clip(cloud * .85, 0, 1)[..., None]
    rgb = rgb + (glow - rgb) * np.clip(filaments * .55, 0, 1)[..., None]
    rgb = rgb * (1 - .55 * np.clip(lanes, 0, 1))[..., None]

    img = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), 'RGB').resize((SIZE, SIZE), Image.BICUBIC)
    sky = np.array(img).astype(np.float32)
    density = np.array(Image.fromarray((np.maximum(band, band2) * 255).astype(np.uint8)).resize((SIZE, SIZE), Image.BILINEAR)).astype(np.float32) / 255

    # Grana d'incisione: rumore fine appena percettibile, come la carta stampata.
    sky += (rng.random((SIZE, SIZE, 1)).astype(np.float32) - .5) * 5

    # ── stelline: un pixel o due, piu' fitte dentro la fascia ───────────────
    count = int(SIZE * SIZE / 480)
    xs = rng.random(count * 3) * SIZE
    ys = rng.random(count * 3) * SIZE
    keep = rng.random(count * 3) < (.25 + .75 * density[ys.astype(int), xs.astype(int)])
    xs, ys = xs[keep][:count], ys[keep][:count]
    mag = rng.pareto(2.4, len(xs)) + .32
    warm = np.array([255, 214, 150], np.float32)
    white = np.array([240, 236, 220], np.float32)
    cold = np.array([150, 226, 220], np.float32)
    layer = np.zeros_like(sky)
    for x, y, m in zip(xs, ys, mag):
        ix, iy = int(x), int(y)
        b = min(1.0, m * .78)
        pick = rng.random()
        c = warm if pick < .6 else (white if pick < .9 else cold)
        layer[iy, ix] = np.maximum(layer[iy, ix], c * b)
        if m > 1.1:
            for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                jx, jy = (ix + ox) % SIZE, (iy + oy) % SIZE
                layer[jy, jx] = np.maximum(layer[jy, jx], c * b * .35)
    sky = np.maximum(sky, layer)

    # ── stelle incise a quattro punte, con il bagliore ───────────────────────
    burst = Image.new('L', (SIZE, SIZE), 0)
    halo = Image.new('L', (SIZE, SIZE), 0)
    db, dh = ImageDraw.Draw(burst), ImageDraw.Draw(halo)
    for i in range(int(SIZE * SIZE / 260000)):
        x, y = rng.random() * SIZE, rng.random() * SIZE
        big = rng.random() < .18
        arm = (14 + rng.random() * 16) if big else (5 + rng.random() * 7)
        thin = 1.4 if big else 1.0
        spin = rng.random() * .5 - .25
        for k in range(4):
            a = spin + k * np.pi / 2
            ax, ay = np.cos(a) * arm, np.sin(a) * arm
            px, py = -np.sin(a) * thin, np.cos(a) * thin
            db.polygon([(x + px, y + py), (x + ax, y + ay), (x - px, y - py)], fill=230)
        if big:
            for k in range(4):
                a = spin + np.pi / 4 + k * np.pi / 2
                ax, ay = np.cos(a) * arm * .38, np.sin(a) * arm * .38
                db.line([(x, y), (x + ax, y + ay)], fill=150, width=1)
        r = 2.2 if big else 1.2
        db.ellipse([x - r, y - r, x + r, y + r], fill=255)
        dh.ellipse([x - arm * .6, y - arm * .6, x + arm * .6, y + arm * .6], fill=90 if big else 50)
    halo = halo.filter(ImageFilter.GaussianBlur(SIZE / 900))
    b = np.array(burst).astype(np.float32)[..., None] / 255
    h = np.array(halo).astype(np.float32)[..., None] / 255
    gold = np.array([252, 212, 138], np.float32)
    sky = sky + gold * h * .35
    sky = sky * (1 - b) + gold * b

    # Il bordo del disco non si vede mai; si scurisce appena per sicurezza.
    yy, xx = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32)
    rr = np.sqrt((xx - SIZE / 2) ** 2 + (yy - SIZE / 2) ** 2) / (SIZE / 2)
    sky *= (1 - .4 * smooth(.97, 1.0, rr))[..., None]

    out = Image.fromarray(np.clip(sky, 0, 255).astype(np.uint8), 'RGB')
    out.save(OUT, optimize=True)
    print('saved', OUT, out.size)
    if PREVIEW:
        out.resize((1024, 1024), Image.LANCZOS).save(PREVIEW)
        out.crop((SIZE // 2 - 960, SIZE // 2 - 540, SIZE // 2 + 960, SIZE // 2 + 540)).save(PREVIEW.with_name(PREVIEW.stem + '_crop.png'))


if __name__ == '__main__':
    main()
