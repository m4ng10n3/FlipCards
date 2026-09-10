# FlipCards - Arcade Horror CRT UI Kit (v2)

Generato il 2026-08-09. 183 sprite, tutti PNG RGBA con trasparenza reale.

## Cosa cambia rispetto alla v1

- **Layout verticale compattato**: rullo nemici, corsie e mano stanno tutti nei 540 px.
- **Stato giocatore nel rail sinistro**, con barre orizzontali (HP, AP, scudo) + icone di stato.
- **Mano con due viste**: abbassata (solo le linguette delle carte) e in primo piano
  (carte intere a ventaglio, con il tabellone oscurato).
- **Niente bottone PESCA**: si pesca cliccando il mazzo, che si assottiglia a ogni pescata.
- **Informazioni divise fra le due facce**: FRONTE = ritratto + ATK/HP, RETRO = sigillo + DEF/HP.
- **Niente banner FRONTE/RETRO**: la faccia si legge dal template (ritratto vs sigillo,
  tacche piene vs tacche vuote sopra la finestra).
- **Nemici = caselle di un rullo da slot machine**, non carte: cassa, vetro, payline,
  caselle parziali sopra/sotto, blur di rotazione, blocco "held".

## Griglia

| | 1x | 2x (uso su canvas 1080p) | 4x |
|---|---|---|---|
| Tabellone | 960x540 | 1920x1080 | - |
| Carta | 112x168 | 224x336 | 448x672 |
| Finestra arte carta | 80x80 | 160x160 | 320x320 |
| Casella del rullo | 176x144 | 352x288 | 704x576 |
| Finestra simbolo nemico | 96x96 | 192x192 | 384x384 |
| Atlante | 1024x2048 | doppio | - |

Un pixel dell'asset = un pixel logico; le scale superiori sono ingrandimenti NEAREST esatti.

## Contenuto

| cartella | sprite | contenuto |
|---|---|---|
| `badge/` | 25 | badge statistiche, micro badge del rullo, celle flip, segmenti AP, pip, tag fazione |
| `banner/` | 5 | banner di fase, targhette 9-slice |
| `bar/` | 16 | barre HP / AP / scudo / boss: telaio 9-slice + riempimento tileable + terminali |
| `board/` | 7 | tabellone 960x540 e overlay CRT (scanline, vignetta, bezel, glitch, statica, dim) |
| `button/` | 16 | bottoni 4 tonalita' x 4 stati, 9-slice |
| `card/` | 14 | cornici carta FRONTE e RETRO, retro cieco, rim di fazione, ombra |
| `decal/` | 10 | decal horror trasparenti |
| `deck/` | 11 | mazzo e scarti con spessore variabile, alone di pesca |
| `fx/` | 4 | overlay di stato: selezione, bersaglio, danno, flip |
| `hand/` | 7 | mano abbassata (linguette + binario) e in primo piano (pannello + alone) |
| `icon/` | 26 | icone 16x16 con contorno |
| `mask/` | 3 | maschere per ritagliare ritratti e simboli |
| `panel/` | 11 | pannelli e cornici 9-slice |
| `reel/` | 22 | rullo nemici: cassa, vetro, payline, caselle, sliver, blur, medaglioni, pip |
| `slot/` | 1 | corsia giocatore vuota |
| `tile/` | 5 | texture tileable |

## Import in Unity

1. Copia la cartella in `Assets/Graphics/ArcadeHorrorUI`.
2. Menu **Tools > FlipCards > Import UI Kit**: imposta Sprite / Point / no compression /
   no mipmap / Full Rect, `Repeat` sui `tile_*`, applica i bordi 9-slice e taglia l'atlante.
3. Canvas Scaler: `Scale With Screen Size`, riferimento **1920x1080**, `Match = 0.5`,
   sprite dalla cartella `2x/`.

## Ordine dei livelli

**Carta** (una sola gerarchia per entrambe le facce):

```
z0   card_shadow
z10  ritratto            -> art_window (solo FRONTE, maschera mask_art_square)
z20  card_front_{fz} | card_back_{fz}   -> tutta la carta
z30  tag_faction_{fz}    -> faction_tag
z30  badge_atk|badge_def -> stat_slots[0]      (FRONTE = ATK, RETRO = DEF)
z30  badge_hp            -> stat_slots[1]
z30  flip_cell_*         -> flip_cells[0..2]
z30  icona abilita'      -> ability_icon
z40  nome + testo abilita' (TMP) -> name_plate / ability_strip
z50  fx_select | fx_target | fx_damage | fx_flip  (bleed 8 px)
```

**Rullo nemici**:

```
reel_backing                      (fondo cassa)
reel_col_blur                     (solo mentre gira)
reel_sliver_top / _bottom         (caselle parziali sopra e sotto)
simbolo + enemy_medallion_{fz}    -> symbol_window (96x96 trasparente)
reel_cell_{fz} | reel_cell_locked -> casella
tag fazione, reel_pip_*, micro_atk/hp/def, nome
reel_col_highlight                (colonna che sta per attaccare)
reel_frame -> reel_payline -> reel_glass
```

**Mano**: `hand_dock_low` + `hand_tab_{fz}` negli `hand_tab_slots` quando e' abbassata;
`overlay_dim` + `hand_dock_raised` + carte intere quando e' in primo piano.
`HandDock.cs` implementa i due stati e il ventaglio.

**Mazzo**: `deck_stack_0..5` in base alle carte rimaste (0 / 1-2 / 3-5 / 6-9 / 10-15 / 16+),
`deck_pulse` quando si puo' pescare, `discard_stack_0..3` per gli scarti.
Il click sul mazzo pesca: nessun bottone.

## Serializzazione

- `flipcards_ui_manifest.json` - per ogni sprite: size, rect nell'atlante, pivot, bordi
  9-slice, tileable, nota. Piu' `layouts` (anatomia di carta, casella e tabellone) e
  `recipes` (gli ordini di livelli qui sopra, in forma di dati).
- `Runtime/UIKit.cs` - le stesse coordinate come costanti C# + tutti i nomi degli sprite.
- `Runtime/SpriteLibrary.cs` - ScriptableObject nome -> Sprite, si popola dall'atlante.
- `Runtime/CardBuilder.cs` - monta carta (fronte/retro) e casella del rullo dai dati,
  piu' `DeckSprite(count)` / `DiscardSprite(count)`.
- `Runtime/HandDock.cs` - le due viste della mano.

> I rect dell'atlante hanno origine in alto a sinistra. Unity usa l'origine in basso a
> sinistra: `y_unity = altezzaAtlante - y - h` (l'importer lo fa gia').

## 9-slice (bordi in ordine Unity: left, bottom, right, top)

| sprite | dimensione | bordo |
|---|---|---|
| `banner_enemy` | 40x17 | 10, 4, 10, 4 |
| `banner_flat` | 40x17 | 8, 4, 8, 4 |
| `banner_neutral` | 40x17 | 10, 4, 10, 4 |
| `banner_phase` | 48x26 | 14, 6, 14, 6 |
| `banner_warn` | 40x17 | 10, 4, 10, 4 |
| `bar_frame_ap` | 32x15 | 6, 4, 6, 4 |
| `bar_frame_boss` | 32x20 | 8, 4, 8, 4 |
| `bar_frame_charge` | 32x12 | 6, 4, 6, 4 |
| `bar_frame_hp` | 32x19 | 8, 4, 8, 4 |
| `bar_frame_shield` | 32x13 | 6, 4, 6, 4 |
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

Fazioni come in gioco: **A** rosso sangue, **B** ciano, **C** verde fosforo.

## Note

- Il testo non e' mai inciso negli sprite: tutte le targhette sono vuote, pensate per
  TextMeshPro con un font pixel.
- I simboli nemici vanno disegnati a 96x96 sulla griglia 1x (i vecchi 144x144 vanno
  ridisegnati, non riscalati).
- Gli overlay (`overlay_scanlines`, `overlay_vignette`, `overlay_bezel`, `overlay_dim`,
  `overlay_glitch`, `overlay_static`) vanno sopra a tutto con raycast disattivato.
