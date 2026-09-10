# Neon Monte — retro francese v2

Pacchetto di asset separati e montaggio geometrico riproducibile, 1024×1536. Sorgenti generate con **image_gen integrato**, rifinite e assemblate con Pillow/NumPy come il fronte. Riferimenti: `../card_back.png`, `../indie-concept-study.png`, `../Front_French_v2/` e il **B originale · Trifoglio** scelto dall'utente.

## File correnti

- `CONTACT_SHEET.png`: tre fazioni, sprite e verifica a 224×336.
- `Template/back_clean.png`: base pronta, senza statistiche, fazione, insegna o cariche incorporate.
- `Template/back_paper.png`, `back_artwork_overlay.png`, `back_frame.png`: carta, ornamento e cornice separati. I due overlay hanno alpha reale.
- `Artwork/three_eyes_ornament.png`: ornamento centrale separato con alpha reale.
- `Template/back_frame.svg`: cornice vettoriale con stazioni determinate matematicamente.
- `Template/back_sun_preview.png`, `back_moon_preview.png`, `back_saturn_preview.png`: esempi completi, ciascuno con overlay separato. Vita 3/7, difesa 4/7, insegna attacco 2, cariche 1/3 sono valori illustrativi.
- `Template/back_empty_preview.png`: indicatori vuoti; `back_defense_banner_preview.png`: esempio con insegna di difesa.
- `Symbols/`: B originale, cuore, picca, cariche, stellina e tre fazioni; PNG 256×256 avorio, maschere bianche e versioni nere.
- `Indicators/`: goccia e scudo pieno/vuoto, celle 256×256, ingombro comune 192×120. Gli scudi orizzontali sono già girati con la punta a sinistra: rotazione runtime zero.
- `symbols_normalized_atlas.png`: atlante 1024×1024, 13 celle occupate; coordinate nel manifest.
- `layout_manifest.json`: coordinate, layer, ingombri, rotazioni e import.

## Geometria

Origine in alto a sinistra. Stesse quote del fronte: fazioni `(140,180)` / `(884,1356)`; cuore `(140,385)`; indice difesa B `(884,1151)`. La seconda fazione è ruotata di 180°.

Sette gocce centrate su x=94, alle y 516, 656, 796, 936, 1076, 1216, 1356. Sette scudi su x=930, alle y 180, 320, 460, 600, 740, 880, 1020. Passo di entrambe le file 140. Ogni indicatore ha inchiostro 94×59; pieno e vuoto hanno stesso rettangolo e pivot. Punta della goccia verso destra; punta dello scudo verso sinistra, entrambe verso l'interno.

Assi dei filetti x=140 / x=884. A ogni tacca corrisponde una cuspide profonda 16 px (x=156 / x=868), esattamente alla stessa y della tacca. Ogni cuspide si raccorda al filetto entro 25 px sopra e sotto il centro. Rimangono 15 px tra punta della tacca e cuspide, 16 px fra il suo estremo esterno e l'asse del bordo.

L'ornamento mantiene le proporzioni originali entro un rettangolo 620×1040, centrato sulla carta; la misura effettiva è registrata nel manifest. La cornice ha simmetria di rotazione 180°; il motivo dei tre occhi resta orientato come nel riferimento.

## Montaggio

Usare `back_clean.png` come base, oppure sovrapporre paper → artwork → frame. Aggiungere fazione, cuore, B, tacche, cariche e insegna secondo il manifest. `back_clean.png` non include dati fissi.

Per le cariche applicare `Template/charge_backplates.png` prima dei cerchi: sono tre piccole porzioni della carta che interrompono il filetto dentro gli indicatori vuoti. Il valore dell'insegna è testo runtime; nei preview è disegnato con il font locale Consolas, non incluso nel pacchetto.

Le dimensioni del manifest sono quelle dell'inchiostro, escluso il padding. Una cella indicatore 256×256 va montata in un rect di circa 125.33×125.87 per ottenere inchiostro 94×59. Le fazioni mantengono le proporzioni del fronte usando l'intera cella a 224×224. I simboli sul rosso sono avorio; maschere bianche disponibili per il tint.

Import: Sprite, Point, nessuna compressione o mipmap, PPU 1, alpha come trasparenza. Tutti gli elementi grafici runtime devono avere raycast disabilitato, come nel fronte.

## Ricostruzione e verifica

Eseguire `build_assets.py` con Python, Pillow e NumPy. Lo script legge le sorgenti già salvate e i simboli del fronte: non usa API, non rigenera immagini e non modifica il pacchetto Front_French_v2.

`QA_REPORT.json` verifica corrispondenza delle quote con il fronte, sette stazioni per lato, allineamento cuspidi, simmetria della cornice, rotazione rigida dello scudo, alpha e identità dei bounding box pieno/vuoto. Controllati visivamente composizione e campione a 224×336. Nessuna modifica o prova in prefab/scena Unity.

Prompt produttivi in `Sources/PRODUCTION_PROMPTS.md`; sorgente e prompt del B in `DEFENSE_SELECTED.md`. La generazione ha restituito scacchi incorporati nelle sorgenti RGB: lo script li rimuove e consegna alpha reale, verificato negli export.

I file `back_sun_helmet_preview.png`, `defense_clubs_alternative_study.png` e gli studi generativi alla radice sono revisioni storiche. Non sono i template correnti; l'elmo è sostituito dal B originale.

## Storico — prompt del primo campione con elmo, superato

Use case: compositing.
Create ONE finished portrait card-back design for the existing Neon Monte game, 1024x1536, flat full-frame card asset, no perspective, no scene, no labels outside the card.
INPUTS: image 1 card_back.png is the EDIT TARGET. Image 2 CONTACT_SHEET_SOLID_SPEAR.png is the current approved FRONT layout and symbol library: use its three front cards to understand the elegant French playing-card information hierarchy, NOT its presentation sheet layout. Image 3 indie-concept-study.png is the original atmosphere reference.
PRIMARY REQUEST: integrate functional game information into the red card back with the simple elegant marginal system of the approved front. This is a populated sample of the SUN faction. Preserve the aged cream outer edge, vermilion printed red background, three cream/black eyes in central diamond, restrained occult arcade woodcut aesthetic. Preserve the recognizable original back design, not a new illustration. Thin and simplify the ornate looping lines around the center to give room to the information. Reduce central diamond and surrounding ornament to fit approximately x=265..759,y=410..1135; all three eyes stay prominent and clearly visible. Keep margins red, no cream panels or boxes.
INFORMATION HIERARCHY, use cream ink on red for excellent contrast:
- Sun faction symbol, a bold circle with centered dot EXACTLY like the sun in the front sheet, centered (140,180), approx 150px ink diameter. Its 180-degree paired copy centered (884,1356). No moon or Saturn in this one sample.
- A simple filled cream heart approx 88x88 centered (140,385), denoting health.
- SEVEN equally spaced horizontal right-pointing droplet pips on LEFT, centers x=100,y=516,656,796,936,1076,1216,1356. The first THREE (top) are solid cream and remaining FOUR are cream OUTLINES with red hollow centers. Each pip ink approx 85x54px; rounded belly left, point right. Fine cream inner margin line on x=166, subtly notched at each station like the front, not a heavy gauge.
- SEVEN shield pips on RIGHT centers x=924,y=180,320,460,600,740,880,1020. Exactly THREE empty OUTLINED shields at top followed by FOUR filled cream shields lower down. Small clear heraldic shield silhouette. Consistent size approx 70x65px. Fine cream inner margin line at x=858, subtly notched at stations. Below that column, centered (884,1151), one larger shield approx 80x100, reusing the shield identity from the symbol sheet. These are defense, so NO attack spear pips on the right.
- The game banner / adjacency bonus belongs in the spacious TOP center red lobe: at y=160, between x=380 and 645, a single compact cream upright ATTACK SPADE (the solid spear silhouette shown in the reference, not a detailed sword) and the number '2' beside it. Total group around 180px wide, balanced, no box, no circle, no ribbon, no text labels. This means attack banner 2, distinct from shield defense on right. Restrained vintage printed number, legible, no ornate lettering.
- Exactly THREE tiny circular charge rings centered near bottom y=1454, x=472,512,552, integrated into a thin lower cream border line, each ~24px diameter. One filled cream circle and two hollow. NO duplicated charges at top.
- Keep diagonal corner hierarchy / rotational correspondence of the front. Adjust original interior scallops only where needed to accommodate these symbols. Information should feel printed as part of the ornament rather than an overlay HUD.
Style: confidently simple shapes, worn printed ink, minimal fine speckle, no dense damage inside small symbols, sophisticated generous breathing room. Cream #E8D9B5, near-black #192124, existing vermilion red. Original print texture retained but symbol edges clear at 224x336.
Only text anywhere is '2'. No card name, no ability sentence, no HP/DEF labels, no arrows, no UI boxes, no legend, no glow, no new eyes, no added central emblem. Deliver only a SINGLE finished card.

## Correzioni delle tacche

Use case: precise-object-edit. Edit this single card image with ONE localized correction. The left health column currently has EIGHT droplet pips: three solid cream at top and five empty outline drops below. Remove ONLY the very lowest empty outline drop, centered approximately (125,1260), by restoring the matching red printed paper texture behind it. Keep the other SEVEN droplets exactly where they are: THREE solid plus FOUR outline. Do not move or redraw any other elements. Keep the heart, both sun emblems, top spear with number 2, all right shields, bottom three charge circles, central three eyes and diamond, all borders, all texture, aspect ratio and dimensions unchanged. The final LEFT column must contain exactly SEVEN droplet shapes excluding the heart. No additional changes.

Use case: precise-object-edit. ONE tiny localized addition to this existing card. Add exactly ONE cream outline right-pointing hollow droplet in the empty left red margin, centered at x=122, y=1156 in this 1024x1536 image, identical size and outline weight to the existing hollow droplet centered at x=122,y=1052 directly above it. This new droplet must be visibly separate from the existing one, with the same vertical gap as the existing row. Its rounded belly faces left and its point faces right. Its inside remains red. Do not delete, shift, repaint or modify any existing droplet or anything else. Preserve all other image contents. Only add this one outlined droplet at (122,1156).

## Revisione richiesta: elmo e scudi

Use case: precise-object-edit. Edit ONE localized symbol in the supplied 1024x1536 card-back image. The right column has SEVEN shield tally pips (three outlined at top, four filled below), and then one extra large shield serving as the defense STAT HEADER near center (898,1122), directly above the lower-right sun-circle emblem. Replace ONLY this LAST LARGE SHIELD at approximately bounding rectangle x=853..940, y=1076..1168 with a simple bold medieval HELMET icon in the same cream ink, same visual size and centered at the same point. The helmet must clearly look like a helmet, NOT a shield: side profile facing LEFT toward the card center, rounded dome, short projecting nose guard, small dark-red visor opening, neck guard at rear, compact silhouette, no plumes, no crest, no ornamental curls. Refined minimal French playing-card symbol readable at tiny size. Flat ivory silhouette with one simple red negative-space visor slit. Very restrained printed edge wear consistent with other symbols.
CRITICAL: keep all SEVEN smaller SHIELD TALLY PIPS above this helmet unchanged in shape, fill and position. Shield-shaped pips remain shields; ONLY the large defense header becomes a helmet.
Keep EVERYTHING else unchanged: left heart and seven health droplets (3 solid plus 4 hollow), both sun signs, red paper, cream edge, top attack spear and number 2, central three eyes diamond, decorative frame, three bottom charge circles, dimensions. No other edits.
