"""Le tacche che vengono da fuori: la stessa tacca, in un altro colore.

Una carta in campo somma alle sue statistiche le insegne delle vicine e i bonus
delle abilita'. Quelle tacche in piu' non sono un simbolo diverso — sarebbe un
secondo alfabeto da imparare — ma **la stessa tacca ricolorata**: oro sul fronte
(attacco), ottanio sul retro (guardia).

Ricolorare serve perche' la tinta di un'Image puo' solo scurire: la tacca
d'attacco e' inchiostro (18..105 di luminanza) e moltiplicandola per un colore
resta inchiostro sporco. Qui la scala di grigi del disegno viene rimappata su una
rampa del colore voluto, quindi forma, bordi e chiaroscuro sono quelli originali.

- ``pip_atk_bonus.png``  da Cards/_Final/Front/Indicators/attack_full.png
- ``pip_def_bonus.png``  da Cards/_Final/Back/Indicators/defense_full.png

Stessa tela e stessa posizione degli originali: il layout non cambia.

Uso: python build_pips.py [cartella_anteprime]
"""
from pathlib import Path
import sys
import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
OUT = HERE.parent
CARDS = HERE.parents[2] / 'Cards' / '_Final'
PREVIEW = Path(sys.argv[1]) if len(sys.argv) > 1 else None


def recolour(path, dark, light):
    """Rimappa la luminanza del disegno sulla rampa dark -> light, alpha intatto."""
    img = Image.open(path).convert('RGBA')
    a = np.array(img).astype(np.float32)
    mask = a[..., 3] > 0
    lum = a[..., :3].mean(-1)
    lo, hi = lum[mask].min(), lum[mask].max()
    t = np.clip((lum - lo) / max(1e-3, hi - lo), 0, 1)[..., None]
    ramp = np.array(dark, np.float32) + (np.array(light, np.float32) - np.array(dark, np.float32)) * t
    out = a.copy()
    out[..., :3] = np.where(mask[..., None], ramp, a[..., :3])
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), 'RGBA')


if __name__ == '__main__':
    atk = recolour(CARDS / 'Front' / 'Indicators' / 'attack_full.png', (122, 80, 14), (242, 190, 72))
    atk.save(OUT / 'pip_atk_bonus.png')
    dfn = recolour(CARDS / 'Back' / 'Indicators' / 'defense_full.png', (58, 158, 166), (156, 240, 242))
    dfn.save(OUT / 'pip_def_bonus.png')
    print('saved pip_atk_bonus.png pip_def_bonus.png')
    if PREVIEW:
        PREVIEW.mkdir(parents=True, exist_ok=True)
        sheet = Image.new('RGBA', (560, 280), (236, 226, 200, 255))
        base = Image.open(CARDS / 'Front' / 'Indicators' / 'attack_full.png').convert('RGBA').resize((128, 128))
        sheet.alpha_composite(base, (10, 10)); sheet.alpha_composite(atk.resize((128, 128)), (150, 10))
        red = Image.new('RGBA', (270, 140), (196, 58, 40, 255))
        sheet.alpha_composite(red, (10, 140))
        back = Image.open(CARDS / 'Back' / 'Indicators' / 'defense_full.png').convert('RGBA').resize((128, 128))
        sheet.alpha_composite(back, (10, 145)); sheet.alpha_composite(dfn.resize((128, 128)), (150, 145))
        sheet.convert('RGB').resize((1120, 560), Image.NEAREST).save(PREVIEW / 'pips.png')
