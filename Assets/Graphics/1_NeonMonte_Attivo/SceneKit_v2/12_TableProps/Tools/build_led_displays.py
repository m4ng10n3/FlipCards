"""Display a matrice LED dipinti dentro l'arte esistente.

Due pezzi, entrambi ricavati dai pixel originali e non incollati sopra:

- ``led_boss_cheek.png``: la striscia frontale della guancia sinistra della cassa
  (10_CabinetIntegrated/cabinet.png) con una finestra incassata. I filetti d'oro
  e le viti restano quelli del disegno; dentro la finestra c'e' una griglia
  bruna forata. I fori sono trasparenti: sotto ci disegna la luce LedMatrix.
- ``led_button_base.png``: il basamento del fungo (08_Integration/mushroom_clean.png,
  righe da MushroomCut in giu') con un canale curvo sulla gonna conica, forato
  allo stesso modo.

La geometria della griglia (angoli del quadrilatero, parametri dell'ellisse,
colonne e righe) e' scritta in ``led_displays.json``: LedMatrix costruisce la
mesh con gli stessi numeri, cosi' la luce cade esattamente sotto i fori.

Uso: python build_led_displays.py [cartella_anteprime]
"""
from pathlib import Path
import json
import sys
import numpy as np
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
OUT = HERE.parent
KIT = HERE.parents[1]
PREVIEW = Path(sys.argv[1]) if len(sys.argv) > 1 else None
SS = 4  # supersampling

INK = np.array([14, 10, 6], np.float32)
PLATE = np.array([30, 22, 14], np.float32)
GOLD_DARK = np.array([92, 58, 16], np.float32)
GOLD_MID = np.array([188, 132, 44], np.float32)
GOLD_LIGHT = np.array([246, 212, 120], np.float32)


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def downsample(rgba):
    h, w = rgba.shape[0] // SS, rgba.shape[1] // SS
    a = rgba[:h * SS, :w * SS].reshape(h, SS, w, SS, 4)
    alpha = a[..., 3:4].mean((1, 3))
    rgb = (a[..., :3] * a[..., 3:4]).mean((1, 3)) / np.maximum(alpha, 1e-4)
    return np.concatenate([rgb, alpha], -1)


def grain(shape, seed, scale=1.0):
    rng = np.random.default_rng(seed)
    return (rng.random(shape).astype(np.float32) - .5) * scale


# ─────────────────────────────────────────────────────────────────────────────
# Guancia della cassa
# ─────────────────────────────────────────────────────────────────────────────
def boss_cheek():
    src = np.array(Image.open(KIT / '10_CabinetIntegrated' / 'cabinet.png').convert('RGBA')).astype(np.float32)
    # Ritaglio della sola striscia frontale, fra le due viti.
    rx, ry, rw, rh = 94, 178, 86, 546
    region = src[ry:ry + rh, rx:rx + rw].copy()

    # Finestra: bordi interni del verde misurati sulle righe 220/420/700. La
    # cassa e' leggermente in prospettiva, quindi il quadrilatero e' un trapezio.
    y0, y1 = 204.0, 708.0
    tl, tr, bl, br = 123.0, 154.5, 109.5, 148.0
    cols, rows = 3, 62
    inset = 5.2  # dal bordo esterno della finestra al bordo della griglia

    H, W = rh * SS, rw * SS
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    x = xx / SS + rx + .5 / SS
    y = yy / SS + ry + .5 / SS
    v = (y - y0) / (y1 - y0)
    xl = tl + (bl - tl) * v
    xr = tr + (br - tr) * v
    w = xr - xl
    cxl = (xl + xr) * .5
    lx = x - cxl
    ly = y - (y0 + y1) * .5
    hx = w * .5
    hy = (y1 - y0) * .5
    r = 4.0
    qx = np.abs(lx) - (hx - r)
    qy = np.abs(ly) - (hy - r)
    sdf = np.sqrt(np.maximum(qx, 0) ** 2 + np.maximum(qy, 0) ** 2) + np.minimum(np.maximum(qx, qy), 0) - r

    out = np.repeat(np.repeat(region, SS, 0), SS, 1)

    # Normale verso l'interno: decide quale labbro prende luce (luce dall'alto a sinistra).
    horiz = qx > qy
    nx = np.where(horiz, -np.sign(lx), 0)
    ny = np.where(horiz, 0, -np.sign(ly))
    lit = np.clip(nx * -.55 + ny * -.83, -1, 1)  # >0 guarda la luce

    # Ombra portata sul verde attorno alla finestra: la finestra e' incassata.
    halo = (1 - smooth(0, 3.5, sdf)) * (sdf > 0)
    out[..., :3] *= (1 - .45 * halo)[..., None]

    ink = (sdf > -1.3) & (sdf < 1.4)
    lip = (sdf <= -1.3) & (sdf > -3.9)
    inner = (sdf <= -3.9) & (sdf > -4.9)
    plate = sdf <= -4.9

    g = grain(sdf.shape, 3, 18)
    hatch = (np.sin((xx + yy * .35) * .9) > .55).astype(np.float32)
    lipcol = np.where((lit > 0)[..., None],
                      GOLD_MID + (GOLD_LIGHT - GOLD_MID) * lit[..., None],
                      GOLD_MID + (GOLD_DARK - GOLD_MID) * (-lit)[..., None])
    lipcol = lipcol * (1 - .28 * hatch[..., None] * (lit < .2)[..., None]) + g[..., None]
    out[ink, :3] = INK
    out[lip, :3] = lipcol[lip]
    out[inner, :3] = INK
    platecol = PLATE[None, None, :] * (1 + .10 * (v[..., None] - .5)) + g[..., None] * .35
    platecol = platecol * (1 - .18 * hatch[..., None])
    out[plate, :3] = platecol[plate]
    out[(sdf < 1.4), 3] = 255

    # Fori: stessa parametrizzazione che usera' LedMatrix (quadrilatero della griglia).
    gy0, gy1 = y0 + inset, y1 - inset
    vg = (y - gy0) / (gy1 - gy0)
    gxl = (tl + inset) + ((bl + inset) - (tl + inset)) * vg
    gxr = (tr - inset) + ((br - inset) - (tr - inset)) * vg
    ug = (x - gxl) / (gxr - gxl)
    inside = (ug >= 0) & (ug < 1) & (vg >= 0) & (vg < 1) & plate
    pitch_x = (gxr - gxl) / cols
    pitch_y = (gy1 - gy0) / rows
    fu = (ug * cols) % 1 - .5
    fv = (vg * rows) % 1 - .5
    px = fu * pitch_x
    py = fv * pitch_y
    radius = .36 * np.minimum(pitch_x, pitch_y)
    d = np.sqrt(px * px + py * py)
    rim = inside & (d < radius + .9) & (d >= radius)
    hole = inside & (d < radius)
    out[rim, :3] = INK * .6
    catch = rim & (px + py > .5)
    out[catch, :3] = GOLD_DARK * .9
    out[hole, 3] = 0

    res = downsample(out)
    img = Image.fromarray(np.clip(res, 0, 255).astype(np.uint8), 'RGBA')
    img.save(OUT / 'led_boss_cheek.png')

    corners = {
        'topLeft': [tl + inset, gy0], 'topRight': [tr - inset, gy0],
        'bottomLeft': [bl + inset, gy1], 'bottomRight': [br - inset, gy1],
    }
    return {
        'source': 'SceneKit_v2/10_CabinetIntegrated/cabinet.png',
        'sourceSize': [1774, 887],
        'overlayRect': [rx, ry, rw, rh],
        'gridCorners': corners,
        'columns': cols, 'rows': rows,
    }, img


# ─────────────────────────────────────────────────────────────────────────────
# Basamento del fungo
# ─────────────────────────────────────────────────────────────────────────────
CX, CY, A, B, CONE = 627.0, 880.0, 585.0, 231.0, .10
CUT = 600  # MedallionSceneSkin.MushroomCut


def plinth_point(theta, dy):
    s = 1 + CONE * dy / B
    return CX + A * s * np.sin(theta), CY + dy + B * s * np.cos(theta)


def button_base():
    src = Image.open(KIT / '08_Integration' / 'mushroom_clean.png').convert('RGBA')
    base = np.array(src.crop((0, CUT, src.width, src.height))).astype(np.float32)
    H, W = base.shape[0], base.shape[1]

    theta0, theta1 = -1.08, 1.08
    dy0, dy1 = -166.0, -112.0   # bordo alto e basso del canale
    cols, rows = 40, 2
    pad = 7.0                   # dal bordo del canale alla griglia, in pixel
    grid_dy0, grid_dy1 = dy0 + pad, dy1 - pad
    # Margine angolare equivalente a 'pad' pixel al centro della gonna.
    dth = pad / A
    grid_t0, grid_t1 = theta0 + dth, theta1 - dth

    hi = Image.fromarray(np.repeat(np.repeat(base, SS, 0), SS, 1).astype(np.uint8), 'RGBA')
    canvas = np.array(hi).astype(np.float32)

    def poly(t0, t1, d0, d1, n=160):
        # Estremita' arrotondate: mezza ellisse schiacciata dalla rotazione della gonna.
        mid = (d0 + d1) * .5
        half = (d1 - d0) * .5
        top = [plinth_point(t, d0) for t in np.linspace(t0, t1, n)]
        bottom = [plinth_point(t, d1) for t in np.linspace(t1, t0, n)]

        def cap(t, sign):
            s = 1 + CONE * mid / B
            squash = abs(np.cos(t)) * half / (A * s)
            pts = []
            for a in np.linspace(-np.pi / 2, np.pi / 2, 24):
                px, py = plinth_point(t + sign * squash * np.cos(a), mid + half * np.sin(a))
                pts.append((px, py))
            return pts if sign > 0 else pts[::-1]

        path = top + cap(t1, 1) + bottom + cap(t0, -1)
        return [((px) * SS, (py - CUT) * SS) for px, py in path]

    def mask_of(points, width=0):
        m = Image.new('L', (W * SS, H * SS), 0)
        dr = ImageDraw.Draw(m)
        dr.polygon(points, fill=255)
        if width:
            dr.line(points + [points[0]], fill=255, width=int(width * SS), joint='curve')
        return np.array(m).astype(np.float32) / 255

    outer = mask_of(poly(theta0 - 2.5 / A, theta1 + 2.5 / A, dy0 - 2.5, dy1 + 2.5))
    lip = mask_of(poly(theta0, theta1, dy0, dy1))
    plate = mask_of(poly(theta0 + 4.5 / A, theta1 - 4.5 / A, dy0 + 3.2, dy1 - 4.6))
    under = mask_of(poly(theta0 - 1 / A, theta1 + 1 / A, dy1 + 2.5, dy1 + 5.5))

    yy, xx = np.mgrid[0:H * SS, 0:W * SS].astype(np.float32)
    g = grain(outer.shape, 7, 16)
    hatch = (np.sin((xx * .8 + yy * .2)) > .5).astype(np.float32)

    # Quota relativa dentro il canale, colonna per colonna: si ricava l'angolo
    # dalla x e si leggono bordo alto e basso a quell'angolo.
    xs = xx / SS
    mid = (dy0 + dy1) * .5
    s_mid = 1 + CONE * mid / B
    theta = np.arcsin(np.clip((xs - CX) / (A * s_mid), -1, 1))
    ytop = plinth_point(theta, dy0)[1] - CUT
    ybot = plinth_point(theta, dy1)[1] - CUT
    rel = np.clip((yy / SS - ytop) / np.maximum(ybot - ytop, 1), 0, 1)

    # Solco d'inchiostro sottile, labbro d'ottone (in ombra in alto, lucido in
    # basso perche' il canale e' incassato e la luce viene dall'alto), piastra
    # bruna e un filo di luce sulla gonna subito sotto il canale.
    col = canvas[..., :3]
    ring = np.clip(outer - lip, 0, 1)
    col[:] = col * (1 - ring[..., None] * .85) + INK * ring[..., None] * .85
    lipcol = GOLD_DARK * .8 + (GOLD_LIGHT - GOLD_DARK * .8) * smooth(.35, 1.0, rel)[..., None]
    lipcol = lipcol * (1 - .22 * hatch[..., None]) + g[..., None]
    only_lip = np.clip(lip - plate, 0, 1)
    col[:] = col * (1 - only_lip[..., None]) + lipcol * only_lip[..., None]
    glint = under * .55
    col[:] = col * (1 - glint[..., None]) + GOLD_LIGHT * glint[..., None]
    platecol = PLATE * (1 - .16 * hatch[..., None]) + g[..., None] * .3
    col[:] = col * (1 - plate[..., None]) + platecol * plate[..., None]

    # Fori ellittici: la gonna gira, quindi verso i lati si stringono (cos theta).
    holes = Image.new('L', (W * SS, H * SS), 0)
    rims = Image.new('L', (W * SS, H * SS), 0)
    dh, dr_ = ImageDraw.Draw(holes), ImageDraw.Draw(rims)
    step_t = (grid_t1 - grid_t0) / cols
    step_d = (grid_dy1 - grid_dy0) / rows
    for i in range(cols):
        th = grid_t0 + (i + .5) * step_t
        for j in range(rows):
            d = grid_dy0 + (j + .5) * step_d
            px, py = plinth_point(th, d)
            s = 1 + CONE * d / B
            wx = abs(A * s * np.cos(th)) * step_t
            ry = step_d
            rx_ = .36 * min(wx, ry * 1.1)
            ry_ = .36 * ry
            cx_, cy_ = px * SS, (py - CUT) * SS
            dr_.ellipse([cx_ - (rx_ + .9) * SS, cy_ - (ry_ + .9) * SS, cx_ + (rx_ + .9) * SS, cy_ + (ry_ + .9) * SS], fill=255)
            dh.ellipse([cx_ - rx_ * SS, cy_ - ry_ * SS, cx_ + rx_ * SS, cy_ + ry_ * SS], fill=255)
    holes = np.array(holes).astype(np.float32) / 255
    rims = np.clip(np.array(rims).astype(np.float32) / 255 - holes, 0, 1) * plate
    col[:] = col * (1 - rims[..., None] * .7) + INK * rims[..., None] * .7
    canvas[..., 3] = canvas[..., 3] * (1 - holes * plate)

    res = downsample(canvas)
    img = Image.fromarray(np.clip(res, 0, 255).astype(np.uint8), 'RGBA')
    img.save(OUT / 'led_button_base.png')
    return {
        'source': 'SceneKit_v2/08_Integration/mushroom_clean.png',
        'sourceSize': [1254, 1254],
        'cutFromTop': CUT,
        'ellipse': {'center': [CX, CY], 'radii': [A, B], 'cone': CONE},
        'gridTheta': [grid_t0, grid_t1],
        'gridDy': [grid_dy0, grid_dy1],
        'columns': cols, 'rows': rows,
    }, img


if __name__ == '__main__':
    boss, boss_img = boss_cheek()
    button, button_img = button_base()
    manifest = {'boss': boss, 'button': button}
    (OUT / 'led_displays.json').write_text(json.dumps(manifest, indent=2))
    print(json.dumps(manifest, indent=2))
    if PREVIEW:
        PREVIEW.mkdir(parents=True, exist_ok=True)
        # Anteprima: fori riempiti di rosso/ciano sopra un fondo scuro.
        for name, img, color in [('boss', boss_img, (255, 40, 20, 255)), ('button', button_img, (40, 240, 210, 255))]:
            bg = Image.new('RGBA', img.size, color)
            bg.alpha_composite(img)
            big = bg.resize((img.width * (4 if name == 'boss' else 1), img.height * (4 if name == 'boss' else 1)), Image.NEAREST)
            big.convert('RGB').save(PREVIEW / f'led_{name}_preview.png')
