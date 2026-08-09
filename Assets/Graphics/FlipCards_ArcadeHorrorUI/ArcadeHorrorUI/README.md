# FlipCards - Arcade Horror CRT UI Kit

Generato il 2026-08-08. 135 sprite, tutti PNG RGBA con trasparenza reale.

## Griglia

| | |
|---|---|
| Tabellone | **960x540** a 1x -> 1920x1080 a 2x |
| Carta / pannello nemico | **112x168** a 1x -> 224x336 a 2x |
| Finestra arte (trasparente) | **80x80** a 1x |
| Atlante | 1024x1024 a 1x |
| Scale esportate | `1x/` master, `2x/` pronto per canvas 1080p, `4x/` per 4K |

Tutto e' disegnato sulla griglia 1x: **un pixel dell'asset = un pixel logico**.
Le scale superiori sono ingrandimenti NEAREST esatti, quindi restano pixel-perfect.
Per un canvas 1920x1080 usa la cartella `2x/` (o l'atlante `@2x`).

## Contenuto

| cartella | sprite | contenuto |
|---|---|---|
| `badge/` | 15 | badge statistiche, celle pattern di flip, pip AP, tag fazione |
| `banner/` | 7 | banner di stato e di fase 9-slice |
| `bar/` | 13 | barre HP / boss / scudo: telaio 9-slice + riempimento tileable + terminali |
| `board/` | 6 | tabellone 960x540, overlay CRT (scanline, vignetta, bezel, glitch, statica) |
| `button/` | 16 | bottoni 4 tonalita' x 4 stati, 9-slice |
| `card/` | 10 | cornici carta giocatore, retro, rim di fazione, ombra |
| `decal/` | 10 | decal horror trasparenti: colate, crepe, macchie, graffi, sigilli |
| `enemy/` | 8 | pannelli nemico corrotti e medaglioni circolari |
| `fx/` | 4 | overlay di stato: selezione, bersaglio, danno, flip |
| `icon/` | 26 | icone 16x16 con contorno |
| `mask/` | 2 | maschere per ritagliare i ritratti |
| `panel/` | 11 | pannelli e cornici 9-slice (scuro, console, fosforo, sangue, schermo, incavo, divisori) |
| `slot/` | 2 | alloggiamenti vuoti per corsia |
| `tile/` | 5 | texture tileable: scanline, griglia, grana, statica, strisce di pericolo |

## Import in Unity

1. Copia la cartella in `Assets/Graphics/ArcadeHorrorUI`.
2. Menu **Tools > FlipCards > Import UI Kit**: imposta su tutte le texture
   `Sprite`, `Filter Mode = Point`, `Compression = None`, `Mip Maps = off`,
   `Mesh Type = Full Rect`, `Wrap = Repeat` sui `tile_*`, e applica i bordi
   9-slice leggendoli dal manifest. Taglia anche l'atlante in sprite multipli.
3. Canvas Scaler: `Scale With Screen Size`, riferimento **1920x1080**,
   `Match = 0.5`, e sprite dalla cartella `2x/`.
4. Sulle `Image` 9-slice imposta `Image Type = Sliced` (e `Tiled` sui riempimenti barra).

## Come si monta una carta (ordine dei livelli)

La finestra dell'arte nella cornice e' **completamente trasparente**: il ritratto
va messo *sotto*, non sopra.

```
z0   card_shadow                (opzionale)
z10  ritratto              -> anatomia.art_window   (80x80, maschera mask_art_square)
z20  card_front_{fazione}   -> tutta la carta        (la cornice copre i bordi del ritratto)
z30  tag_faction_{fazione}  -> anatomia.faction_tag
z30  badge_atk/hp/def       -> anatomia.stat_slots[0..2]  + numero TMP
z30  flip_cell_*            -> anatomia.flip_cells[0..2]
z30  banner_front/back      -> anatomia.state_banner (9-slice)
z40  nome (TMP)             -> anatomia.name_plate
z50  fx_select / fx_target / fx_damage -> stessa area con 8 px di bleed
```

I nemici usano la stessa identica anatomia con `enemy_panel_{fazione}`, cosi'
un solo prefab e un solo builder servono entrambi. Le coordinate sono nel
manifest (`layouts.card`) e nel file generato `Runtime/UIKit.cs`.

## Serializzazione

- `flipcards_ui_manifest.json` - per ogni sprite: dimensione, rect nell'atlante,
  pivot, bordi 9-slice, se e' tileable, e una nota in italiano.
  Contiene anche `layouts` (anatomia di carta e tabellone) e `recipes`
  (l'ordine dei livelli qui sopra, in forma di dati).
- `Runtime/UIKit.cs` - le stesse coordinate come costanti C# + i nomi degli sprite.
- `Runtime/SpriteLibrary.cs` - ScriptableObject nome -> Sprite, si auto-popola
  dall'atlante.
- `Runtime/CardBuilder.cs` - esempio funzionante che monta carta e nemico dai dati.

> I rect dell'atlante nel manifest hanno **origine in alto a sinistra**.
> Unity usa l'origine in basso a sinistra: `y_unity = altezzaAtlante - y - h`
> (l'importer lo fa gia').

## 9-slice (bordi in ordine Unity: left, bottom, right, top)

| sprite | dimensione | bordo |
|---|---|---|
| `banner_back` | 40x17 | 10, 4, 10, 4 |
| `banner_enemy` | 40x17 | 10, 4, 10, 4 |
| `banner_flat` | 40x17 | 8, 4, 8, 4 |
| `banner_front` | 40x17 | 10, 4, 10, 4 |
| `banner_neutral` | 40x17 | 10, 4, 10, 4 |
| `banner_phase` | 48x26 | 14, 6, 14, 6 |
| `banner_warn` | 40x17 | 10, 4, 10, 4 |
| `bar_frame_boss` | 32x20 | 8, 4, 8, 4 |
| `bar_frame_charge` | 32x12 | 6, 4, 6, 4 |
| `bar_frame_hp` | 32x14 | 6, 4, 6, 4 |
| `bar_frame_shield` | 32x12 | 6, 4, 6, 4 |
| `btn_amber_disabled` | 40x32 | 12, 10, 12, 10 |
| `btn_amber_hover` | 40x32 | 12, 10, 12, 10 |
| `btn_amber_idle` | 40x32 | 12, 10, 12, 10 |
| `btn_amber_press` | 40x32 | 12, 10, 12, 10 |
| `btn_blood_disabled` | 40x32 | 12, 10, 12, 10 |
| `btn_blood_hover` | 40x32 | 12, 10, 12, 10 |
| `btn_blood_idle` | 40x32 | 12, 10, 12, 10 |
| `btn_blood_press` | 40x32 | 12, 10, 12, 10 |
| `btn_phos_disabled` | 40x32 | 12, 10, 12, 10 |
| `btn_phos_hover` | 40x32 | 12, 10, 12, 10 |
| `btn_phos_idle` | 40x32 | 12, 10, 12, 10 |
| `btn_phos_press` | 40x32 | 12, 10, 12, 10 |
| `btn_steel_disabled` | 40x32 | 12, 10, 12, 10 |
| `btn_steel_hover` | 40x32 | 12, 10, 12, 10 |
| `btn_steel_idle` | 40x32 | 12, 10, 12, 10 |
| `btn_steel_press` | 40x32 | 12, 10, 12, 10 |
| `divider_h` | 32x5 | 4, 0, 4, 0 |
| `divider_v` | 5x32 | 0, 4, 0, 4 |
| `panel_blood` | 32x32 | 10, 10, 10, 10 |
| `panel_console` | 32x32 | 10, 10, 10, 10 |
| `panel_dark` | 32x32 | 10, 10, 10, 10 |
| `panel_mag` | 32x32 | 10, 10, 10, 10 |
| `panel_phos` | 32x32 | 10, 10, 10, 10 |
| `panel_screen` | 32x32 | 6, 6, 6, 6 |
| `panel_well` | 32x32 | 8, 8, 8, 8 |
| `plate_counter` | 32x32 | 8, 8, 8, 8 |
| `plate_tooltip` | 32x32 | 8, 8, 8, 8 |

## Palette

| `void` | #040508 |
| `void2` | #090B10 |
| `ink` | #0F121A |
| `ink2` | #161B26 |
| `steel` | #262E3C |
| `steel_hi` | #3E4A5E |
| `steel_lo` | #181D28 |
| `bone` | #D8E4D6 |
| `bone_dim` | #8A988E |
| `bone_lo` | #4E5854 |
| `phos` | #3DFF7A |
| `phos_hi` | #AAFFC4 |
| `phos_mid` | #1AA84E |
| `phos_lo` | #0A4A26 |
| `phos_vlo` | #062616 |
| `blood` | #FF2B3C |
| `blood_hi` | #FF8A8A |
| `blood_mid` | #AA1422 |
| `blood_lo` | #560812 |
| `blood_vlo` | #2C060C |
| `mag` | #FF2FD0 |
| `mag_hi` | #FF9EEC |
| `mag_mid` | #AC188A |
| `mag_lo` | #520A42 |
| `amber` | #FFB000 |
| `amber_mid` | #B07000 |
| `amber_lo` | #563600 |
| `cyan` | #38E8FF |
| `cyan_mid` | #1884A8 |
| `cyan_lo` | #0A3A4E |
| `purple` | #843ED2 |
| `purple_lo` | #36165C |

Le fazioni riprendono la codifica gia' usata in gioco: **A** rosso sangue,
**B** ciano, **C** verde fosforo.

## Note

- I ritratti nemici esistenti (144x144) entrano nella finestra 80x80 della
  cornice: a 2x la finestra e' 160x160, quindi ci stanno quasi a scala 1:1.
  Per la resa migliore ridisegnali a 80x80 sulla griglia 1x.
- Gli overlay `overlay_scanlines`, `overlay_vignette`, `overlay_bezel`,
  `overlay_glitch`, `overlay_static` vanno sopra a tutto, con raycast disattivato.
- Il testo non e' mai inciso negli sprite: tutte le targhette sono vuote e
  pensate per TextMeshPro con un font pixel.
