"""Atlante delle icone per i testi con simboli (legenda e ispettore).

Le icone sono i disegni che il giocatore vede sul tavolo — gocce, lance, scudi,
cariche, fazioni, insegne, lampade della cassa, stella degli AP, fungo, leva,
libretto, legenda — ridotte in celle da 128 px. Il builder ne fa uno sprite
asset TMP (MedallionSceneSkin.IconSprites), cosi' nei testi si scrive
``<sprite name="drop">`` e compare la goccia vera, non una parola.

Le poche icone che non esistono come disegno (display LED, frecce di flip e
scambio) sono disegnate qui con lo stesso inchiostro.

- ``ui_icons.png``  1024x640, trasparente.
- ``ui_icons.json`` nome -> rettangolo (x, y dal basso, w, h), letto dal builder.

Uso: python build_icons.py [anteprima.png]
"""
from pathlib import Path
import json
import sys
import numpy as np
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
OUT = HERE.parent
KIT = HERE.parents[1]
ACTIVE = HERE.parents[2]
CARDS = ACTIVE / 'Cards' / '_Final'
PREVIEW = Path(sys.argv[1]) if len(sys.argv) > 1 else None
CELL, COLS, ROWS = 128, 8, 5
PAD = 8
INK = (22, 16, 10, 255)
GOLD = (206, 166, 92, 255)


def load(path):
    return Image.open(path).convert('RGBA')


def ink(img, color):
    """I simboli del dorso sono avorio per la copertina rossa: sulla carta chiara
    della legenda e dell'ispettore vanno in inchiostro. Resta la forma (alpha)
    e il chiaroscuro del disegno, scurito verso il colore dato."""
    a = np.array(img).astype(np.float32)
    lum = a[..., :3].mean(-1, keepdims=True) / 255
    target = np.array(color[:3], np.float32)
    a[..., :3] = target * (.55 + .45 * (1 - lum)) + 40 * (1 - lum)
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA')


def trim(img):
    box = img.getchannel('A').point(lambda a: 255 if a > 12 else 0).getbbox()
    return img.crop(box) if box else img


def lamp(name):
    # MedallionSceneSkin: rettangoli Unity (y dal basso) nell'atlante 1536x1024.
    rects = {'round': (112, 52, 384, 440), 'spear': (638, 52, 260, 440), 'shield': (1046, 52, 380, 410)}
    atlas = load(KIT / '09_Instruments_v3' / 'lamps_atlas.png')
    x, y, w, h = rects[name]
    top = atlas.height - y - h
    img = atlas.crop((x, top, x + w, top + h))
    # Il fondo dell'atlante e' nero pieno: si svuota partendo dagli angoli, cosi'
    # l'inchiostro dei contorni (anch'esso scuro) resta.
    for corner in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)):
        ImageDraw.floodfill(img, corner, (0, 0, 0, 0), thresh=46)
    return img


def led_boss():
    img = Image.new('RGBA', (80, 128), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([14, 4, 66, 124], radius=8, fill=(34, 24, 14, 255), outline=GOLD, width=4)
    d.rounded_rectangle([12, 2, 68, 126], radius=9, outline=INK, width=2)
    for row in range(9):
        for col in range(3):
            lit = row >= 3
            c = (255, 60, 30, 255) if lit else (80, 22, 14, 255)
            cx, cy = 26 + col * 14, 16 + row * 12
            d.ellipse([cx - 4, cy - 4, cx + 4, cy + 4], fill=c)
    return img


def led_player():
    img = Image.new('RGBA', (128, 70), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.arc([6, -60, 122, 60], 20, 160, fill=INK, width=18)
    d.arc([8, -58, 120, 58], 22, 158, fill=(160, 118, 48, 255), width=14)
    d.arc([12, -54, 116, 54], 24, 156, fill=(34, 24, 14, 255), width=8)
    for k in range(11):
        a = np.radians(28 + k * 12.4)
        cx, cy = 64 + np.cos(a) * 52, np.sin(a) * 52
        lit = 3 <= k <= 7
        c = (60, 240, 210, 255) if lit else (16, 60, 56, 255)
        d.ellipse([cx - 3.5, cy - 3.5, cx + 3.5, cy + 3.5], fill=c)
    return img


def arrow_flip():
    img = Image.new('RGBA', (128, 128), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.arc([22, 22, 106, 106], 200, 340, fill=INK, width=9)
    d.arc([22, 22, 106, 106], 20, 160, fill=INK, width=9)
    d.polygon([(100, 40), (116, 62), (88, 62)], fill=INK)
    d.polygon([(28, 88), (12, 66), (40, 66)], fill=INK)
    return img


def arrow_swap():
    img = Image.new('RGBA', (128, 128), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.line([(18, 46), (100, 46)], fill=INK, width=9)
    d.polygon([(110, 46), (88, 30), (88, 62)], fill=INK)
    d.line([(28, 84), (110, 84)], fill=INK, width=9)
    d.polygon([(18, 84), (40, 68), (40, 100)], fill=INK)
    return img


def arrow_right():
    img = Image.new('RGBA', (128, 128), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.line([(14, 64), (96, 64)], fill=INK, width=10)
    d.polygon([(116, 64), (88, 42), (88, 86)], fill=INK)
    return img


def star_ink(lit=True):
    """La stella degli AP per i testi: quella del cielo e' fatta di bagliore e
    sulla carta chiara della legenda spariva. Qui e' incisa: quattro punte in oro
    con il filetto d'inchiostro."""
    n = 128
    img = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c, R, w = n / 2, 56, 13
    pts = [(c, c - R), (c + w, c - w), (c + R, c), (c + w, c + w),
           (c, c + R), (c - w, c + w), (c - R, c), (c - w, c - w)]
    fill = (222, 178, 84, 255) if lit else (0, 0, 0, 0)
    d.polygon(pts, fill=fill, outline=INK)
    d.line(pts + [pts[0]], fill=INK, width=3, joint='curve')
    if lit:
        d.polygon([(c, c - R * .55), (c + w * .45, c), (c, c + R * .55), (c - w * .45, c)], fill=(248, 222, 150, 255))
    return img


def deck():
    back = trim(load(CARDS / 'Back' / 'Template' / 'back_clean.png'))
    back = back.resize((70, 105), Image.LANCZOS)
    img = Image.new('RGBA', (96, 128), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for k in range(4, 0, -1):
        d.rounded_rectangle([12 + k, 16 + k * 3, 12 + k + 70, 16 + k * 3 + 105], radius=5,
                            fill=(226, 214, 186, 255), outline=(90, 70, 44, 255))
    img.alpha_composite(back, (12, 10))
    return img


def framed(img, color=(60, 44, 24, 255)):
    """Un filetto attorno all'icona: la carta di fronte e' quasi bianca e sulla
    pergamena senza bordo non si distingueva dal fondo."""
    img = trim(img)
    out = Image.new('RGBA', (img.width + 10, img.height + 10), (0, 0, 0, 0))
    d = ImageDraw.Draw(out)
    d.rounded_rectangle([1, 1, out.width - 2, out.height - 2], radius=8, fill=color)
    out.alpha_composite(img, (5, 5))
    return out


def main():
    sources = [
        ('drop', load(CARDS / 'Front' / 'Indicators' / 'drop_full.png')),
        ('drop_empty', load(CARDS / 'Front' / 'Indicators' / 'drop_empty.png')),
        ('atk', load(CARDS / 'Front' / 'Indicators' / 'attack_full.png')),
        ('atk_empty', load(CARDS / 'Front' / 'Indicators' / 'attack_empty.png')),
        ('def', ink(load(CARDS / 'Back' / 'Indicators' / 'defense_full.png'), (28, 86, 92))),
        ('def_empty', ink(load(CARDS / 'Back' / 'Indicators' / 'defense_empty.png'), (28, 86, 92))),
        ('charge', load(CARDS / 'Front' / 'Symbols' / 'charge_full.png')),
        ('charge_empty', load(CARDS / 'Front' / 'Symbols' / 'charge_ring.png')),
        ('sun', load(CARDS / 'Front' / 'Symbols' / 'faction_sun.png')),
        ('moon', load(CARDS / 'Front' / 'Symbols' / 'faction_moon.png')),
        ('saturn', load(CARDS / 'Front' / 'Symbols' / 'faction_saturn.png')),
        ('spade', ink(load(CARDS / 'Back' / 'Symbols' / 'attack_spade.png'), (40, 30, 20))),
        ('club', ink(load(CARDS / 'Back' / 'Symbols' / 'defense_club_B.png'), (28, 86, 92))),
        ('sword', ink(load(ACTIVE / 'Runtime' / 'Glyphs' / 'glyph_sword.png'), (40, 30, 20))),
        ('shield', ink(load(ACTIVE / 'Runtime' / 'Glyphs' / 'glyph_shield.png'), (28, 86, 92))),
        ('broken', ink(load(ACTIVE / 'Runtime' / 'Glyphs' / 'glyph_broken.png'), (150, 28, 24))),
        ('lamp_hp', lamp('round')),
        ('lamp_atk', lamp('spear')),
        ('lamp_def', lamp('shield')),
        ('led_boss', led_boss()),
        ('led_player', led_player()),
        ('star', star_ink(True)),
        ('star_off', star_ink(False)),
        ('button', load(KIT / '08_Integration' / 'mushroom_clean.png')),
        ('lever', load(KIT / '04_Controls' / 'lever_rest.png')),
        ('deck', deck()),
        ('card_front', framed(load(CARDS / 'Front' / 'Previews' / 'front_moon_preview.png'))),
        ('card_back', framed(load(CARDS / 'Back' / 'Template' / 'back_clean.png'))),
        ('book', load(KIT / '11_GraphicUpgrade' / 'book_closed.png')),
        ('scroll', load(OUT / 'scroll_closed.png')),
        ('flip', arrow_flip()),
        ('swap', arrow_swap()),
        ('arrow', arrow_right()),
        ('reel', load(KIT / '02_Machine' / 'reel_face_ink.png')),
        ('eclipse', load(ACTIVE / 'Runtime' / 'Slots' / 'symbol_eclipse.png')),
    ]
    assert len(sources) <= COLS * ROWS
    atlas = Image.new('RGBA', (COLS * CELL, ROWS * CELL), (0, 0, 0, 0))
    rects = {}
    for i, (name, img) in enumerate(sources):
        img = trim(img)
        # Le lampade e il fungo sono alti: tutti stanno nella cella senza deformarsi.
        box = CELL - 2 * PAD
        scale = min(box / img.width, box / img.height)
        img = img.resize((max(1, int(img.width * scale)), max(1, int(img.height * scale))), Image.LANCZOS)
        col, row = i % COLS, i // COLS
        x0, y0 = col * CELL, row * CELL
        atlas.alpha_composite(img, (x0 + (CELL - img.width) // 2, y0 + (CELL - img.height) // 2))
        rects[name] = [x0, atlas.height - y0 - CELL, CELL, CELL]
    atlas.save(OUT / 'ui_icons.png')
    (OUT / 'ui_icons.json').write_text(json.dumps({'cell': CELL, 'icons': rects}, indent=1))
    print(len(rects), 'icons')
    if PREVIEW:
        bg = Image.new('RGBA', atlas.size, (236, 226, 200, 255))
        bg.alpha_composite(atlas)
        bg.convert('RGB').save(PREVIEW)


if __name__ == '__main__':
    main()
