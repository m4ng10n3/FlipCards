# Oggetti di scena del tavolo — 16 settembre 2026

Asset disegnati via codice (Python/numpy) partendo dai disegni esistenti, non
generati da un modello di immagini. Gli script stanno in `Tools/` e si
rilanciano senza argomenti; con una cartella come argomento scrivono anche le
anteprime.

## Display LED — `Tools/build_led_displays.py`

- `led_boss_cheek.png`: la striscia frontale della guancia sinistra di
  `10_CabinetIntegrated/cabinet.png` (rettangolo 94,178,86,546) con una
  finestra incassata e una griglia forata 3×62. Filetti e viti sono i pixel
  originali.
- `led_button_base.png`: il basamento del fungo (`mushroom_clean.png` da
  y 600) con un canale curvo 40×2 sulla gonna conica.
- `led_displays.json`: geometria delle griglie. I numeri sono copiati in
  `MedallionSceneSkin.ConfigureBossLed` / `ConfigurePlayerLed`: se si rigenera,
  vanno ricopiati.

I fori sono trasparenti. Sotto, `LedMatrix` (shader `FlipCards/LED Matrix`)
disegna un LED per cella da una texture di stato con un texel per LED; sopra,
un secondo `LedMatrix` in additivo fa il bagliore. `LedHealthStrip` scrive la
vita nel display: niente numeri né percentuali.

## Cielo — `Tools/build_sky.py`

`sky_disc.png` 4096×4096: nebulosa e stelle su un disco che lo shader
`Animated Illustrated Surface` (modo 0) fa ruotare attorno a un polo sotto il
tavolo (x 960, y 1250 del canvas, un giro ogni ~14 minuti). Un secondo strato di
stelle a quattro punte, procedurale nello shader, gira 1,35 volte più veloce e
scintilla. Importato come texture compressa HQ, non come sprite.

## Libretto — `Tools/build_book.py`

- `book_spread_left.png` / `book_spread_right.png`: la doppia pagina di
  `11_GraphicUpgrade/book_open.png` senza il dorso di pelle al centro, che da
  aperto non si vede. Le pagine sono spostate di 26 px verso la piega e la
  piega ha un'ombra calda.
- `book_leaf_left.png` / `book_leaf_right.png`: le facce di una pagina, retro
  e fronte del foglio che gira (`BookLeaf`, shader `FlipCards/Book Leaf`).
- `book_cover_front.png`: il piatto anteriore con il titolo inciso; è il foglio
  che gira quando il libretto si apre.
- `bookmark_campo|mano|rullo|registro.png`: linguette di pelle con icona.
- `table_shadow.png`: ombra di contatto per mazzo e libretto chiuso.

Misure della tela (1651×953): piega a 826, facce delle pagine x 92–1560 e
y 24–906. Sono le costanti `BookSrc*` di `MedallionSceneBuilder`.
