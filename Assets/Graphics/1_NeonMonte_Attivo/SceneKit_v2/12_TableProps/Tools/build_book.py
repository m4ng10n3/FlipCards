"""Il libretto: doppia pagina senza dorso, fogli da sfogliare, copertina,
segnalibri e l'ombra di contatto degli oggetti sul tavolo.

Tutto parte dai disegni di 11_GraphicUpgrade (book_open.png, book_closed.png):
niente e' ridisegnato da zero dove c'era gia' un'illustrazione buona.

- ``book_spread_left.png`` / ``book_spread_right.png``  la doppia pagina aperta, in due meta'. Il dorso di pelle che il disegno
  mostrava al centro — un libro aperto visto da sopra il dorso non lo mostra —
  diventa la piega: ogni pagina prosegue fino al centro con la sua carta, e la
  piega si scurisce e si solleva come fa la carta vicino alla cucitura.
- ``book_leaf_left.png`` / ``book_leaf_right.png``  la faccia di una pagina,
  ritagliata dalla doppia pagina: sono il fronte e il retro del foglio che gira.
- ``book_cover_front.png``  il piatto anteriore, grande quanto meta' libro
  aperto: e' il foglio che gira quando il libretto si apre.
- ``bookmark_<sezione>.png``  quattro linguette di pelle con l'icona incisa.
- ``table_shadow.png``  ombra di contatto morbida per mazzo e libretto.

Le misure in pixel stampate in fondo sono quelle copiate in
MedallionSceneBuilder (Booklet): se si rigenera vanno ricopiate.

Uso: python build_book.py [cartella_anteprime]
"""
from pathlib import Path
import json
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageOps

HERE = Path(__file__).resolve().parent
OUT = HERE.parent
SRC = HERE.parents[1] / '11_GraphicUpgrade'
PREVIEW = Path(sys.argv[1]) if len(sys.argv) > 1 else None
SS = 4
rng = np.random.default_rng(7)

INK = (22, 16, 10)
GOLD = (206, 166, 92)
GOLD_LIGHT = (246, 214, 142)
GOLD_DARK = (120, 86, 36)


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


# ─────────────────────────────────────────────────────────────────────────────
# Doppia pagina
# ─────────────────────────────────────────────────────────────────────────────
GUTTER = 826          # centro della piega sulla tela da 1651
LEFT_KEEP = 776       # la pagina sinistra resta originale fino al filetto interno compreso
RIGHT_KEEP = 877
FACE_TOP, FACE_BOTTOM = 34, 896   # righe della faccia pagina (sopra/sotto: tagli e piatto)
PAGE_LEFT, PAGE_RIGHT = 92, 1560  # bordo esterno delle facce


def spread():
    src = np.array(Image.open(SRC / 'book_open.png').convert('RGBA')).astype(np.float32)
    H, W = src.shape[:2]
    out = src.copy()

    # Ogni pagina scivola di SHIFT pixel verso il centro. Il vuoto si apre a meta'
    # pagina, dove ci sono solo carta e filetti orizzontali: ripetere qualche
    # colonna li prolunga senza giunte. Il filetto interno finisce cosi' a ~25 px
    # dalla piega, come in un libro vero; tenerlo dov'era lasciava due righe
    # parallele ai lati della piega, e la piega sembrava una striscia incollata.
    SHIFT = 26
    for x in range(400, GUTTER):
        xs = x - SHIFT
        if x >= 816:
            xs = 789 - (x - 816)          # oltre il margine originale: carta specchiata
        out[:, x] = src[:, xs]
    for x in range(GUTTER, 1251):
        xs = x + SHIFT
        if x < 837:
            xs = 863 + (837 - x)
        out[:, x] = src[:, xs]
    # Fra il filetto interno e la piega: carta pulita di meta' pagina. Le righe
    # dei filetti orizzontali prendono la carta di una riga vicina, perche' oltre
    # il filetto verticale la cornice non prosegue.
    def clean_row(y):
        if y < 120: return y + 120
        if y > 820: return y - 120
        return y
    for y in range(FACE_TOP, FACE_BOTTOM + 1):
        ys = clean_row(y)
        for x in range(804, GUTTER):
            out[y, x] = src[ys, 640 + (x - 804)]
        for x in range(GUTTER, 849):
            out[y, x] = src[ys, 1010 + (x - GUTTER)]
    # Sopra e sotto la faccia (tagli delle pagine, piatto) le colonne spostate
    # prenderebbero la cuffia del dorso: si prolungano i tagli vicini.
    for rows_ in (slice(0, FACE_TOP), slice(FACE_BOTTOM + 1, H)):
        for x in range(740, GUTTER):
            out[rows_, x] = src[rows_, x - 110]
        for x in range(GUTTER, 912):
            out[rows_, x] = src[rows_, x + 110]

    # Il disegno scuriva la carta verso il vecchio dorso: si appiattisce quella
    # sfumatura colonna per colonna sul tono di meta' pagina; i filetti (colonne
    # molto piu' scure) prendono il guadagno della colonna vicina e restano filetti.
    rows = slice(140, 820)
    for columns, reference, start in ((range(680, GUTTER), 640, 680), (range(GUTTER, 972)[::-1], 1010, 972)):
        ref = np.median(out[rows, reference, :3], axis=0)
        gain = np.ones(3, np.float32)
        for x in columns:
            med = np.median(out[rows, x, :3], axis=0)
            if med.mean() > ref.mean() * .86:
                # Rampa di 50 px: l'appiattimento entra piano, senza gradino.
                w = smooth(0, 50, abs(x - start))
                gain = 1 + (ref / np.maximum(med, 1) - 1) * w
            out[FACE_TOP:FACE_BOTTOM + 1, x, :3] *= gain

    # Facce delle pagine per il foglio che gira, prima dell'ombra della piega:
    # il foglio e' carta da bordo a bordo, l'ombra resta sulla doppia pagina.
    flat = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), 'RGBA')
    for box, name in (((PAGE_LEFT, FACE_TOP - 10, GUTTER, FACE_BOTTOM + 10), 'book_leaf_left.png'),
                      ((GUTTER, FACE_TOP - 10, PAGE_RIGHT, FACE_BOTTOM + 10), 'book_leaf_right.png')):
        leaf = np.array(flat.crop(box)).astype(np.float32)
        leaf[..., 3] = 255
        Image.fromarray(leaf.astype(np.uint8), 'RGBA').save(OUT / name)

    # La piega: ombra stretta al centro, larga e leggera attorno, e la carta che
    # si solleva un poco prima di scendere nella cucitura.
    xs = np.arange(W, dtype=np.float32)
    dx = xs - GUTTER
    shade = (1 - .38 * np.exp(-(dx / 9) ** 2) - .17 * np.exp(-(dx / 34) ** 2) - .08 * np.exp(-(dx / 110) ** 2)
             + .04 * np.exp(-((np.abs(dx) - 150) / 60) ** 2))
    rows = np.ones(H, np.float32)
    rows[:FACE_TOP] = .55
    rows[FACE_BOTTOM:] = .55
    k = 1 - (1 - shade[None, :]) * rows[:, None]
    # Ombra calda: sulla carta avorio il blu cala piu' del rosso, altrimenti
    # la piega diventa grigia.
    out[..., :3] *= np.stack([k ** 1.0, k ** 1.12, k ** 1.4], -1)
    crease = np.abs(dx) < 1.2
    out[FACE_TOP:FACE_BOTTOM, crease, :3] *= .55
    img = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), 'RGBA')
    # Le due meta': la sinistra si accende solo quando la copertina ci si posa
    # sopra, e fa da retro alla copertina che gira.
    img.crop((0, 0, GUTTER, H)).save(OUT / 'book_spread_left.png')
    img.crop((GUTTER, 0, W, H)).save(OUT / 'book_spread_right.png')
    return img, {'size': [W, H], 'gutter': GUTTER, 'faceTop': FACE_TOP - 10, 'faceBottom': FACE_BOTTOM + 10,
                 'pageLeft': PAGE_LEFT, 'pageRight': PAGE_RIGHT}


# ─────────────────────────────────────────────────────────────────────────────
# Piatto anteriore
# ─────────────────────────────────────────────────────────────────────────────
def cover_front(spread_img):
    closed = Image.open(SRC / 'book_closed.png').convert('RGBA')
    W, H = spread_img.width - GUTTER, spread_img.height
    # Pelle: la zona liscia sotto l'etichetta, specchiata per coprire il piatto.
    patch = closed.crop((240, 730, 1220, 890))
    leather = Image.new('RGBA', (W, H))
    y = 0
    flip = False
    while y < H:
        tile = ImageOps.flip(patch) if flip else patch
        tile = tile.resize((W, patch.height))
        leather.paste(tile, (0, y))
        y += patch.height
        flip = not flip
    a = np.array(leather).astype(np.float32)
    # Vignetta dei bordi consumati e una sagoma leggermente arrotondata.
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    edge = np.minimum.reduce([xx, W - 1 - xx, yy, H - 1 - yy])
    a[..., :3] *= (.72 + .28 * smooth(0, 40, edge))[..., None]
    a[..., 3] = 255
    board = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA')
    mask = Image.new('L', (W, H), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, W - 1, H - 1], radius=22, fill=255)
    board.putalpha(mask)

    d = ImageDraw.Draw(board)
    d.rounded_rectangle([1, 1, W - 2, H - 2], radius=22, outline=INK, width=4)
    # Doppio filetto d'oro, come sulla copertina chiusa.
    for inset, width in ((34, 5), (48, 2)):
        d.rectangle([inset, inset, W - 1 - inset, H - 1 - inset], outline=GOLD_DARK, width=width + 2)
        d.rectangle([inset + 1, inset + 1, W - 2 - inset, H - 2 - inset], outline=GOLD, width=width)
    # Angoli con la stella e lo stemma, ritagliati dal disegno della copertina chiusa.
    corner = closed.crop((150, 140, 232, 222))
    for cx, cy, fx, fy in ((56, 56, False, False), (W - 138, 56, True, False), (56, H - 138, False, True), (W - 138, H - 138, True, True)):
        c = corner
        if fx: c = ImageOps.mirror(c)
        if fy: c = ImageOps.flip(c)
        board.alpha_composite(c, (cx, cy))
    emblem = closed.crop((430, 139, 958, 423))
    emblem = emblem.resize((int(emblem.width * .92), int(emblem.height * .92)), Image.LANCZOS)
    board.alpha_composite(emblem, ((W - emblem.width) // 2, 150))
    plate = closed.crop((208, 433, 1187, 708))
    scale = (W - 190) / plate.width
    plate = plate.resize((int(plate.width * scale), int(plate.height * scale)), Image.LANCZOS)
    board.alpha_composite(plate, ((W - plate.width) // 2, 470))
    # Il titolo inciso sull'etichetta, come sul libretto chiuso del tavolo.
    from PIL import ImageFont
    font = ImageFont.truetype('C:/Windows/Fonts/georgiab.ttf', 64)
    title = 'DETTAGLIO'
    spacing = 12
    widths = [d.textlength(ch, font=font) for ch in title]
    total = sum(widths) + spacing * (len(title) - 1)
    x = (W - total) / 2
    y = 470 + plate.height / 2 - 40
    for ch, w in zip(title, widths):
        d.text((x + 1.5, y + 2), ch, font=font, fill=(255, 244, 214, 150))
        d.text((x, y), ch, font=font, fill=INK)
        x += w + spacing
    lower = emblem.resize((int(emblem.width * .55), int(emblem.height * .55)), Image.LANCZOS)
    lower = ImageOps.flip(lower)
    board.alpha_composite(lower, ((W - lower.width) // 2, H - 150 - lower.height))
    board.save(OUT / 'book_cover_front.png')
    return board


# ─────────────────────────────────────────────────────────────────────────────
# Segnalibri
# ─────────────────────────────────────────────────────────────────────────────
TABS = {
    'campo': ((126, 40, 32), 'field'),
    'mano': ((30, 86, 88), 'hand'),
    'rullo': ((158, 114, 36), 'reel'),
    'registro': ((88, 46, 80), 'quill'),
}
TAB_W, TAB_H = 196, 84


def draw_icon(d, kind, cx, cy, s, fill, outline):
    """Icone incise, disegnate a 4x: centro (cx, cy), lato s."""
    lw = max(2, int(s * .07))
    if kind == 'field':
        w, h = s * .26, s * .40
        for i in (-1, 0, 1):
            x = cx + i * s * .33
            d.rounded_rectangle([x - w / 2, cy - h / 2, x + w / 2, cy + h / 2], radius=s * .04, fill=fill, outline=outline, width=lw)
            d.line([x - w * .25, cy, x + w * .25, cy], fill=outline, width=lw)
    elif kind == 'hand':
        w, h = s * .30, s * .44
        for i, ang in enumerate((-24, 0, 24)):
            card = Image.new('RGBA', (int(w + lw * 4), int(h + lw * 4)))
            cd = ImageDraw.Draw(card)
            cd.rounded_rectangle([lw * 2, lw * 2, lw * 2 + w, lw * 2 + h], radius=s * .04, fill=fill, outline=outline, width=lw)
            cd.ellipse([lw * 2 + w * .32, lw * 2 + h * .30, lw * 2 + w * .68, lw * 2 + h * .56], outline=outline, width=lw)
            card = card.rotate(-ang, resample=Image.BICUBIC, expand=True)
            px = cx + (i - 1) * s * .18 - card.width / 2
            py = cy + abs(i - 1) * s * .05 - card.height / 2
            d._image.alpha_composite(card, (int(px), int(py)))
    elif kind == 'reel':
        r = s * .30
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=fill, outline=outline, width=lw)
        d.ellipse([cx - r * .45, cy - r * .45, cx + r * .45, cy + r * .45], outline=outline, width=lw)
        for k in range(8):
            a = k * np.pi / 4
            d.line([cx + np.cos(a) * r * .5, cy + np.sin(a) * r * .5, cx + np.cos(a) * r * .95, cy + np.sin(a) * r * .95], fill=outline, width=lw)
    elif kind == 'quill':
        pts = [(cx - s * .30, cy + s * .34), (cx + s * .30, cy - s * .36), (cx + s * .12, cy - s * .02), (cx - s * .22, cy + s * .24)]
        d.polygon(pts, fill=fill, outline=outline)
        d.line([pts[0], pts[1]], fill=outline, width=lw)
        for k in range(1, 5):
            t = k / 5
            px = cx - s * .30 + (s * .60) * t
            py = cy + s * .34 - (s * .70) * t
            d.line([px, py, px + s * .10, py + s * .02], fill=outline, width=max(1, lw // 2))
        d.line([cx - s * .40, cy + s * .40, cx - s * .12, cy + s * .40], fill=outline, width=lw)


def bookmark(name, color, icon):
    W, H = TAB_W * SS, TAB_H * SS
    img = Image.new('RGBA', (W, H))
    shape = Image.new('L', (W, H), 0)
    sd = ImageDraw.Draw(shape)
    r = H * .42
    # Base diritta a sinistra (sta infilata fra le pagine), capo arrotondato a destra.
    sd.rounded_rectangle([-r, 6 * SS, W - 4 * SS, H - 6 * SS], radius=r, fill=255)
    a = np.zeros((H, W, 4), np.float32)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    base = np.array(color, np.float32)
    grain = rng.random((H // 3 + 1, W // 3 + 1)).astype(np.float32)
    grain = np.array(Image.fromarray((grain * 255).astype(np.uint8)).resize((W, H), Image.BILINEAR)).astype(np.float32) / 255
    light = 1.08 - .30 * (yy / H) + (grain - .5) * .16
    a[..., :3] = base * light[..., None]
    # Ombra della pagina sulla base e bordo scurito.
    a[..., :3] *= (.55 + .45 * smooth(0, 38 * SS, xx))[..., None]
    a[..., 3] = np.array(shape).astype(np.float32)
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA')
    d = ImageDraw.Draw(img)
    # Filo d'inchiostro esterno e cucitura in oro spento.
    outline = shape.filter(ImageFilter.FIND_EDGES).filter(ImageFilter.MaxFilter(7))
    ink = Image.new('RGBA', (W, H), INK + (255,))
    img.paste(ink, (0, 0), outline)
    inner = Image.new('L', (W, H), 0)
    ImageDraw.Draw(inner).rounded_rectangle([-r, 15 * SS, W - 13 * SS, H - 15 * SS], radius=r * .78, outline=255, width=int(1.6 * SS))
    stitch = np.array(inner).astype(np.float32)
    dash = (np.sin(xx * .16) > 0) | (np.abs(xx - (W - 13 * SS)) < 30 * SS) & (np.sin(yy * .16) > 0)
    stitch *= dash
    stitch *= (xx > 22 * SS)
    gold = Image.new('RGBA', (W, H), GOLD + (255,))
    img.paste(gold, (0, 0), Image.fromarray((stitch * .85).astype(np.uint8)))
    # Riflesso sul bordo alto.
    hl = Image.new('L', (W, H), 0)
    ImageDraw.Draw(hl).rounded_rectangle([-r, 9 * SS, W - 7 * SS, H - 9 * SS], radius=r * .9, outline=255, width=int(1.2 * SS))
    hla = np.array(hl).astype(np.float32) * (yy < H * .45) * .35
    img.paste(Image.new('RGBA', (W, H), (255, 236, 196, 255)), (0, 0), Image.fromarray(hla.astype(np.uint8)))
    # Icona incisa sulla punta.
    draw_icon(d, icon, W - 40 * SS, H / 2, 40 * SS, GOLD, INK)
    img = img.resize((TAB_W, TAB_H), Image.LANCZOS)
    img.save(OUT / f'bookmark_{name}.png')
    return img


# ─────────────────────────────────────────────────────────────────────────────
# Oggetti di scena
# ─────────────────────────────────────────────────────────────────────────────
def table_shadow():
    n = 256
    m = Image.new('L', (n, n), 0)
    ImageDraw.Draw(m).rounded_rectangle([44, 44, n - 45, n - 45], radius=18, fill=255)
    m = m.filter(ImageFilter.GaussianBlur(17))
    img = Image.new('RGBA', (n, n), (0, 0, 0, 255))
    img.putalpha(m)
    img.save(OUT / 'table_shadow.png')
    return img


if __name__ == '__main__':
    sp, measures = spread()
    cover = cover_front(sp)
    tabs = [bookmark(k, *v) for k, v in TABS.items()]
    table_shadow()
    print(json.dumps(measures, indent=2))
    if PREVIEW:
        PREVIEW.mkdir(parents=True, exist_ok=True)
        bg = Image.new('RGBA', sp.size, (24, 60, 50, 255)); bg.alpha_composite(sp)
        bg.convert('RGB').resize((sp.width // 2, sp.height // 2)).save(PREVIEW / 'book_spread.png')
        bg.crop((640, 0, 1010, 260)).convert('RGB').save(PREVIEW / 'book_gutter_top.png')
        bg.crop((640, 380, 1010, 640)).convert('RGB').save(PREVIEW / 'book_gutter_mid.png')
        cb = Image.new('RGBA', cover.size, (24, 60, 50, 255)); cb.alpha_composite(cover)
        cb.convert('RGB').resize((cover.width // 2, cover.height // 2)).save(PREVIEW / 'book_cover.png')
        sheet = Image.new('RGBA', (TAB_W * 2 + 30, TAB_H * 2 + 30), (24, 60, 50, 255))
        for i, t in enumerate(tabs):
            sheet.alpha_composite(t, ((i % 2) * (TAB_W + 10) + 10, (i // 2) * (TAB_H + 10) + 10))
        sheet.convert('RGB').resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(PREVIEW / 'book_tabs.png')
