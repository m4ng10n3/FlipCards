"""Leva a inchiostro in quattro pezzi, tagliata da 04_Controls/lever_rest.png.

Nessun ridisegno. Il mozzo e' il tamburo originale senza l'asta che gli entra
da sopra; l'asta e' il tratto dritto, raddrizzato; la manopola e' la sfera col
colletto, raddrizzata; la bocca e' la sagoma della fessura del tamburo in
inchiostro pieno, da mettere dietro l'asta. MedallionSceneBuilder li rimonta
attorno al perno (l'asse del tamburo) e MedallionLever li anima. Le misure
stampate in fondo sono quelle copiate nelle costanti di MedallionSceneSkin.

Uso: python build_lever_parts.py [anteprima.png]
"""
from pathlib import Path
import json
import sys
import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
CONTROLS = HERE.parents[1] / '04_Controls'
src = Image.open(CONTROLS / 'lever_rest.png').convert('RGBA')
a = np.array(src)
alpha = a[..., 3]
H, W = alpha.shape
opaque = alpha > 128
lum = a[..., :3].astype(np.float32).mean(2)

# ── asse dell'asta ───────────────────────────────────────────────────────────
ys = np.arange(640, 1300, 4)
cx = np.array([np.where(opaque[y])[0].mean() for y in ys])
slope, intercept = np.polyfit(ys, cx, 1)          # x = slope * y + intercept
lean = float(np.degrees(np.arctan(-slope)))        # > 0: la cima pende a destra
half = float(np.median([(np.ptp(np.where(opaque[y])[0]) + 1) / 2 for y in ys]))
axis_x = lambda y: slope * y + intercept

# ── perno: asse dell'asta sul centro verticale del tamburo ───────────────────
drum_rows = [y for y in range(1300, H) if opaque[y].sum() > 250]
drum_top, drum_bottom = drum_rows[0], drum_rows[-1]
pivot = (float(axis_x((drum_top + drum_bottom) / 2)), (drum_top + drum_bottom) / 2)

# ── manopola ─────────────────────────────────────────────────────────────────
widths = [opaque[y].sum() for y in range(0, 640)]
knob_y = int(np.argmax(widths))
knob_r = max(widths) / 2
collar_y = next(y for y in range(knob_y, 700) if opaque[y].sum() <= 2 * half + 8)

def along(y):
    """Distanza dal perno lungo l'asta, per un punto dell'asse alla riga y."""
    return float(np.hypot(axis_x(y) - pivot[0], pivot[1] - y))

knob_len, collar_len = along(knob_y), along(collar_y)

# Raddrizzata attorno al perno, l'asta e' verticale e l'asse passa per pivot x.
straight = np.array(src.rotate(lean, resample=Image.Resampling.BICUBIC, center=pivot))
px, py = pivot

def crop(img, x0, y0, x1, y1):
    x0, y0, x1, y1 = (int(round(v)) for v in (x0, y0, x1, y1))
    return Image.fromarray(img[max(0, y0):y1, max(0, x0):x1]), (x0, y0, x1, y1)

# Manopola: sfera e colletto, fino al punto in cui l'asta entra nel colletto.
k_margin = 18
knob_img, kb = crop(straight, px - knob_r - k_margin, py - knob_len - knob_r - k_margin,
                    px + knob_r + k_margin, py - collar_len + 6)
knob_attach = ((px - kb[0]) / (kb[2] - kb[0]), 6 / (kb[3] - kb[1]))   # pivot Unity, y dal basso

# Asta: solo il tratto pulito, lontano da colletto e tamburo; si stira a runtime.
s_margin = 6
shaft_img, sb = crop(straight, px - half - s_margin, py - collar_len + 30,
                     px + half + s_margin, py - (pivot[1] - drum_top) - 90)
shaft_arr = np.array(shaft_img)
shaft_fill = float((shaft_arr[..., 3] > 128).sum(1).mean() / shaft_arr.shape[1])

# ── mozzo e bocca ────────────────────────────────────────────────────────────
# Sopra il tamburo l'asta si toglie e basta. Il bordo del tamburo dietro l'asta
# non e' disegnato: lo si interpola dai due lati, misurati su colonne fuori da
# TUTTO il tratto d'asta della finestra (l'asta pende: a sinistra conta il suo
# punto piu' basso, a destra il piu' alto).
hub = a.copy()
window = (drum_top - 160, drum_top + 220)
left = int(axis_x(window[1]) - half - 10)
right = int(axis_x(window[0]) + half + 10)

def first_opaque(x):
    rows = np.where(opaque[window[0]:window[1], x])[0]
    return window[0] + rows[0] if len(rows) else window[1]

edge_l = min(first_opaque(x) for x in range(left - 6, left))
edge_r = min(first_opaque(x) for x in range(right + 1, right + 7))

# Dentro il tamburo l'asta scende in una fessura: i suoi due contorni neri
# finiscono dove comincia la faccia dorata. Fin li' l'asta si toglie anche dal
# mozzo. A riposo il buco lo riempie l'asta vera, che gli passa dietro; quando
# la leva gira, dietro resta la bocca scura invece del cielo.
def outline_dark(y):
    c = axis_x(y)
    return all(lum[y, int(x) - 2:int(x) + 3].min() < 60 for x in (c - half + 4, c + half - 4))

socket_y = next((y for y in range(drum_top, int(py)) if not any(outline_dark(y + k) for k in range(5))), int(py) - 60)
mouth = np.zeros((H, W), bool)
for y in range(0, socket_y):
    for x in range(max(0, int(axis_x(y) - half - 10)), min(W, int(axis_x(y) + half + 10) + 1)):
        t = (x - left) / max(1, right - left)
        if y < edge_l + (edge_r - edge_l) * min(1, max(0, t)):
            hub[y, x, 3] = 0
        elif abs(x - axis_x(y)) <= half + 3:
            mouth[y, x] = opaque[y, x]
            hub[y, x, 3] = 0

hub_alpha = hub[..., 3] > 16
hy, hx = np.where(hub_alpha[window[0]:, :])
hub_box = (hx.min() - 6, window[0] + hy.min() - 6, hx.max() + 7, window[0] + hy.max() + 7)
hub_img, hb = crop(hub, *hub_box)
hub_pivot = ((px - hb[0]) / (hb[2] - hb[0]), (hb[3] - py) / (hb[3] - hb[1]))
socket = np.zeros_like(a)
socket[mouth] = (22, 18, 12, 255)
socket_img, _ = crop(socket, *hub_box)

for name, img in [('lever_ink_hub', hub_img), ('lever_ink_socket', socket_img),
                  ('lever_ink_shaft', shaft_img), ('lever_ink_knob', knob_img)]:
    img.save(CONTROLS / f'{name}.png')

report = {
    'source': 'SceneKit_v2/04_Controls/lever_rest.png',
    'lean_degrees': round(lean, 3),
    'pivot_px': [round(pivot[0], 1), round(pivot[1], 1)],
    'knob_attach_length_px': round(collar_len, 1),
    'socket_bottom_row': int(socket_y),
    'hub': {'size': list(hub_img.size), 'pivot': [round(v, 4) for v in hub_pivot]},
    'shaft': {'size': list(shaft_img.size), 'fill': round(shaft_fill, 4)},
    'knob': {'size': list(knob_img.size), 'pivot': [round(v, 4) for v in knob_attach]},
}
print(json.dumps(report, indent=2))

if len(sys.argv) > 1:
    # Ricomposizione a riposo, sullo spazio dell'originale: deve coincidere.
    canvas = Image.new('RGBA', (W, H), (40, 40, 60, 255))
    canvas.alpha_composite(Image.fromarray(socket).crop(hb), (hb[0], hb[1]))
    upright = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    tall = shaft_img.resize((shaft_img.width, int(collar_len)), Image.Resampling.BICUBIC)
    upright.alpha_composite(tall, (sb[0], int(py - collar_len)))
    canvas.alpha_composite(upright.rotate(-lean, resample=Image.Resampling.BICUBIC, center=pivot))
    canvas.alpha_composite(Image.fromarray(hub).crop(hb), (hb[0], hb[1]))
    knob_layer = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    knob_layer.alpha_composite(knob_img, (max(0, kb[0]), max(0, kb[1])))
    canvas.alpha_composite(knob_layer.rotate(-lean, resample=Image.Resampling.BICUBIC, center=pivot))
    # Terza colonna: il mozzo con la bocca, senza asta, com'e' a leva tirata.
    bare = Image.new('RGBA', (W, H), (40, 40, 60, 255))
    bare.alpha_composite(Image.fromarray(socket).crop(hb), (hb[0], hb[1]))
    bare.alpha_composite(Image.fromarray(hub).crop(hb), (hb[0], hb[1]))
    side = Image.new('RGBA', (W * 3, H), (40, 40, 60, 255))
    side.alpha_composite(src, (0, 0))
    side.alpha_composite(canvas, (W, 0))
    side.alpha_composite(bare, (W * 2, 0))
    side.resize((W * 3 // 2, H // 2)).save(sys.argv[1])
