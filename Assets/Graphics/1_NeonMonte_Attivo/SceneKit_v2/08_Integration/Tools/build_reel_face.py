"""Faccia del rullo: la carta delle carte finali avvolta su un tamburo.

Nessuna generazione. La carta e' Cards/_Final/Front/Template/front_paper.png;
la curvatura la danno la grana che si stringe verso alto e basso, la luce
cilindrica e il tratteggio a inchiostro dove il tamburo gira via. Il filetto
doppio con gli angoli a unghia e' lo stesso inchiostro delle carte (#192124).

Seme, simbolo, nome, tacche e numero della lastra NON sono qui: li monta
SlotOverlay a runtime sulle misure di questa tela, divise per due.
Uscita: SceneKit_v2/02_Machine/reel_face_ink.png, 704x576 (cella 352x288 x2).
"""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
KIT = HERE.parents[1]
STYLE = KIT.parent
OUT = KIT / '02_Machine' / 'reel_face_ink.png'

W, H = 704, 576
INK = np.array([25, 33, 36], np.float32) / 255
FRAME_X, FRAME_Y = 32, 28   # filetto esterno: 16 e 14 unita' nella cella
GAP = 10                    # fra filetto esterno e interno
NOTCH = 22                  # raggio dell'unghia agli angoli
SPAN = .34 * np.pi          # meta' dell'arco di tamburo visibile nella finestra
rng = np.random.default_rng(20260910)

# ── carta ────────────────────────────────────────────────────────────────────
paper = Image.open(STYLE / 'Cards/_Final/Front/Template/front_paper.png').convert('RGB')
pw, ph = paper.size
crop_h = int(pw * H / W)
top = (ph - crop_h) // 2
paper = paper.crop((0, top, pw, top + crop_h)).resize((W, H), Image.Resampling.LANCZOS)
flat = np.asarray(paper).astype(np.float32) / 255

# Riga di schermo -> angolo sul tamburo -> riga di carta: verso i bordi una riga
# di schermo copre piu' carta, e la grana si comprime come su un cilindro.
t = (np.arange(H) + .5) / H
phi = np.arcsin(np.clip((t - .5) * 2 * np.sin(SPAN), -1, 1))
rows = np.clip(((.5 + phi / (2 * SPAN)) * H).astype(int), 0, H - 1)
img = flat[rows]

# ── luce ─────────────────────────────────────────────────────────────────────
# Luce appena dall'alto: il massimo cade poco sopra il centro, e i due bordi
# scendono nel buio abbastanza da leggere il tamburo anche a scala di gioco.
light = np.clip(np.cos(phi + .12), 0, 1) ** 2.2
shade = .30 + .70 * light
warm = np.array([.80, .70, .60], np.float32)
tint = warm[None, :] + (1 - warm[None, :]) * light[:, None]
img = img * shade[:, None, None] * tint[:, None, :]

# Ombra portata dagli anelli d'ottone della cassa ai due lati.
x = np.arange(W) + .5
side = 1 - .22 * np.exp(-x / 26) - .22 * np.exp(-(W - x) / 26)
img = img * side[None, :, None]

def over(base, alpha, color=INK):
    return base * (1 - alpha[..., None]) + color[None, None, :] * alpha[..., None]

# ── tratteggio ───────────────────────────────────────────────────────────────
# Righe d'incisione dove la luce cala: piu' fitte e scure verso i bordi, rotte a
# tratti come una stampa. Solo fuori dal filetto, per non sporcare il simbolo.
hatch = np.zeros((H, W), np.float32)
for y in range(0, H, 6):
    strength = np.clip((.62 - light[y]) / .62, 0, 1)
    if strength <= 0 or FRAME_Y + GAP < y < H - FRAME_Y - GAP:
        continue
    dashes = (rng.random(W) > .10 + .25 * (1 - strength)).astype(np.float32)
    dashes = np.convolve(dashes, np.ones(9) / 9, mode='same')
    hatch[y] = np.maximum(hatch[y], .50 * strength * (dashes > .55))
    if y + 1 < H:
        hatch[y + 1] = np.maximum(hatch[y + 1], .22 * strength * (dashes > .55))
img = over(img, hatch)

# Il bordo alto e basso sparisce nel buio della cassa.
edge = np.minimum(t * H, (1 - t) * H)
img = img * (1 - .55 * np.exp(-edge / 7))[:, None, None]

# ── filetto ──────────────────────────────────────────────────────────────────
S = 4

def notched(x0, y0, x1, y1, r, steps=16):
    pts = [(x0 + r, y0), (x1 - r, y0)]
    corners = [((x1, y0), 180, 90), ((x1, y1), 270, 180), ((x0, y1), 360, 270), ((x0, y0), 90, 0)]
    ends = [(x1, y1 - r), (x0 + r, y1), (x0, y0 + r), (x0 + r, y0)]
    for ((cx, cy), a0, a1), end in zip(corners, ends):
        for k in range(steps + 1):
            a = np.radians(a0 + (a1 - a0) * k / steps)
            pts.append((cx + r * np.cos(a), cy + r * np.sin(a)))
        pts.append(end)
    return [(px * S, py * S) for px, py in pts]

layer = Image.new('L', (W * S, H * S), 0)
draw = ImageDraw.Draw(layer)
draw.line(notched(FRAME_X, FRAME_Y, W - FRAME_X, H - FRAME_Y, NOTCH), fill=235, width=4 * S, joint='curve')
draw.line(notched(FRAME_X + GAP, FRAME_Y + GAP, W - FRAME_X - GAP, H - FRAME_Y - GAP, NOTCH + GAP),
          fill=150, width=2 * S, joint='curve')
for cx, cy in [(FRAME_X, FRAME_Y), (W - FRAME_X, FRAME_Y), (FRAME_X, H - FRAME_Y), (W - FRAME_X, H - FRAME_Y)]:
    draw.ellipse(((cx - 4) * S, (cy - 4) * S, (cx + 4) * S, (cy + 4) * S), fill=235)
frame = np.asarray(layer.resize((W, H), Image.Resampling.LANCZOS)).astype(np.float32) / 255
# L'inchiostro segue la grana: dove la carta e' piu' chiara la stampa cede.
grain = flat.mean(2)
frame *= np.clip(1.25 - .6 * (grain - grain.mean()) / (grain.std() + 1e-6) * .25, .75, 1)
frame *= shade[:, None] * .35 + .65
img = over(img, np.clip(frame, 0, 1))

out = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8), 'RGB')
out.save(OUT)
print({'out': str(OUT), 'size': out.size, 'frame_cell': [FRAME_X / 2, FRAME_Y / 2], 'inner_cell': [(FRAME_X + GAP) / 2, (FRAME_Y + GAP) / 2]})
