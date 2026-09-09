# Generazione e rifinitura

Strumento: **image_gen integrato**. Successiva rifinitura geometrica autorizzata dall'utente: «Rifinitura geometrica precisa degli asset generati e punta di lancia piu' dettagliata e romboidale».

**Revisione corrente attacco:** l'utente ha scelto A come predefinita. Montaggio, PNG, SVG e atlante usano A (estensione 0). B (18), C (36) e gli studi successivi restano alternative storiche. Centro pieno, sagoma simmetrica, due facce scure. Cuore 88×88 e attacco 78×104. Anteprima corrente `CONTACT_SHEET_SOLID_SPEAR.png`; confronto `ATTACK_SOLID_STUDY.png`. Nessuna nuova generazione: selezione della variante A esistente.

Revisione precedente: lancia con spacco centrale, approvata come sagoma e conservata in `Studies/split_approved/`. **La punta guarda verso l'alto** anche nella versione corrente piena; il montaggio usa `attack_spade.png` a rotazione zero.

**Direzione corrente fazioni:** Sole ☉, Luna ☽, Saturno ♄, segni astronomici reali verificati online. Fonti in `ASTRONOMICAL_SOURCES.md`. Rasterizzazione dei glifi, correzione del peso e falce piena; nessuna nuova generazione image_gen per questi segni. Celle 256×256, ingombro massimo 192×192 con proporzioni conservate, montaggio della cella 224×224. Le sezioni successive e `ABSTRACT_SIGIL_PROMPTS.json` documentano revisioni superate.

## Foglio simboli — sorgente symbols_generated.png

Use case: style-transfer. ONE production sprite atlas for a vintage French playing card indie occult game. Reference image gives symbol IDENTITIES only; remove its red background, cream outlines, connecting stems and horizontal extensions. TRUE TRANSPARENT background, alpha=0 outside symbols, no colored paper, no checkerboard drawn. Square 1536x1536 canvas with EXACT equal 3x3 grid, NO visible grid, NO labels.
Nine isolated symbols, centered one in each cell. Every symbol fits the SAME 320x320 square inside its 512x512 cell. Consistent apparent size and ink weight; compact icons not tall narrow silhouettes. Row 1: short broad SWORD upright charcoal, compact SHIELD charcoal with narrow transparent center slit, simple filled HEART brick red. Row 2: upright active LOZENGE charcoal with thin transparent inner slit, small four-point STAR brick red, one thin CIRCULAR CHARGE RING charcoal (transparent hollow). Row 3: compact FLAME brick red, curling WAVE muted petrol teal, thorned sprig with two leaves moss green. Factions all equally sized 320x320 and equally bold, naturally adapt silhouettes to square, no horizontal connector lines, no medallions. Flat ink colors: charcoal #192124, red #B52B26, teal #3D7B75, moss #6C7542. Restrained tiny ink distress entirely confined inside silhouette. NO thick cream halo, all negative spaces transparent. Clean confident woodcut shapes, readable at 24px, restrained like a French playing card suit, minimal ornamental complexity. Single coherent library of NINE sprites.

Riferimento: `../card_engraved_parts.png`. Il generatore ha restituito 1254×1254 e scacchi incorporati. Le misure e il canale alpha richiesti sono stati ottenuti nella rifinitura, non dati per scontati.

## Goccia — sorgente drop_generated.png

Generate one TRANSPARENT PNG sprite asset for a vintage printed indie card game. ONE red BLOOD DROP pointing horizontally RIGHT, single round semicircular belly at LEFT and one tapering point at RIGHT. Not a leaf, not two points. Pure flat brick-red ink #B52B26, tiny restrained print wear inside silhouette. Square 1024x1024 canvas, the drop occupies exactly the centered horizontal bounding rectangle x=192..832, y=288..736 (640x448). Entire background genuinely transparent alpha zero, no checkerboard illustration, no paper, no shadow, no border, no glow. A confident clear icon legible at 16 pixels. This is the ACTIVE / FILLED life tally for a playing-card left margin: curved belly faces outer edge, pointed tip faces card center. One isolated icon only.

## Lancia — sorgente spear_generated.png

Questo prompt documenta la prima versione dettagliata, conservata solo tra le sorgenti. Su successiva richiesta dell'utente la versione finale è stata semplificata geometricamente in due facce opache contigue, senza incisioni o fessura centrale. La geometria corrente è in `build_assets.py`.

ONE isolated game UI icon: detailed RHOMBOID SPEARHEAD pointing horizontally RIGHT. True transparent PNG background, no checkerboard, no paper, no scene. Shape: elongated diamond-shaped forged spear blade, long acute right tip, shorter left tail, broad angular upper and lower shoulders at 43% length. Ratio bounding width to height 1.6:1. Rich but restrained vintage woodcut charcoal-black ink #192124. A clearly engraved fine central ridge from left tail to right point, two asymmetric bevel facets, tiny stepped socket at left tip, a few short carefully placed etched hatching strokes, lightly worn printed texture. This is a tiny tally pip, NOT an entire weapon: no long shaft, no crossguard, no sword grip. Both top/bottom edges angular and straight, recognizably a spear blade / lance head, not an arrow triangle, not chevron, not leaf. Silhouette 640x400 centered in 1024x1024 transparent canvas; generous equal transparent padding. Opaque dark main facets with genuine transparent fine internal engraving cuts. One confident elegant silhouette readable at 20px. Point right.

## Carta e assemblaggio finale

`paper_reference.png` è la prima prova generativa del fronte, basata sul template incompleto e sul concept indie. Della prova è stata utilizzata soltanto la carta avorio vuota nel rettangolo `(225,255)-(800,1260)`. La cornice e le tacche della prova non sono state usate negli export finali.

Specifica finale di rifinitura: carta 1024×1536; filetto esterno e due mezze cornici ruotate di 180°; sette scallop per lato con passo 155 px; terminali all'altezza dei centri delle fazioni opposte; simboli normalizzati in celle 256×256; goccia e lancia normalizzate nello stesso rettangolo 192×120; stato vuoto ricavato dalla stessa sagoma del pieno; titolo e abilità su carta continua senza riquadri; tre cariche discrete sulla linea inferiore. `build_assets.py` è la fonte esatta e riproducibile di queste operazioni.

Revisione successiva: assi verticali del filetto interno allineati a cuore e spada, rispettivamente x=110 e x=914; indicatori 72×45 con centri x=78 e x=946. Luce laterale uniforme di 11–12 px tra inchiostro e assi delle linee. Lancia a due facce senza spazio centrale. Aggiornati asset singoli, atlante, cornice, tutte le anteprime e manifest.

Revisione corrente, gerarchia e ingombri: spada slanciata con inchiostro 76×174 nel montaggio e cuore 88×88 centrato nella stessa altezza riservata, con 43 px liberi sopra e sotto. Fazioni ingrandite da 112×112 a 168×168, sagome ripulite, sottile contorno scuro e spine del rovo accentuate. Assi dei margini spostati a x=140 e x=884; spalle della cornice a x=250/774 per accogliere gli emblemi più grandi. Indicatori 94×59, centri x=94/930 e passo 140; sette per lato, fino al centro della fazione opposta. Fazioni centrate alle quote y=180/1356, indici y=385/1151. Spada sempre rivolta alla fazione inferiore. Specifica esatta nel manifest e nel codice di assemblaggio.
