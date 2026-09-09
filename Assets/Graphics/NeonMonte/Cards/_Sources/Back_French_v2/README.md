# Neon Monte — retro sagomato, tacche a glifo

Revisione corrente **unified-border-back-v7**. Tutto il bordo interno condivide i filetti scuro/avorio delle curve; usura esterna più fine. Le tacche identificano da sole la statistica: **goccia per vita, scudo semplice per difesa**. Vita e difesa partono dall'alto. Nessun cuore, elmo o simbolo aggiuntivo di intestazione. Consegna di produzione: ../../_Final/README.md.

## File correnti

- CONTACT_SHEET.png: tre fazioni, sprite e riduzione a 224×336.
- Template/back_sun_preview.png, back_moon_preview.png, back_saturn_preview.png: esempi con overlay separati.
- Template/back_clean.png: base senza seme, statistiche, insegna o cariche.
- Template/back_paper.png, back_artwork_overlay.png, back_frame.png, back_frame.svg: livelli separati.
- Indicators/drop_full.png / drop_empty.png: goccia nelle proporzioni originali, rivolta a destra.
- Indicators/defense_full.png / defense_empty.png: scudo semplice con la **punta inferiore rivolta a sinistra**, verso il centro.
- Symbols/defense_club_B.png: master verticale usato soltanto per l'eventuale insegna di difesa.
- symbols_normalized_atlas.png, layout_manifest.json, QA_REPORT.json.

## Distribuzione

Sette tacche per lato alle y **[324, 472, 620, 768, 916, 1064, 1212]**, passo **148**. Gocce su x=154, scudi su x=870. Vita e difesa si riempiono entrambe dall'alto verso il basso: per valore N, sono piene le prime N posizioni. Il manifest dichiara tally_fill_order; la verifica raster copre 0, 1, 4 e 7 scudi.

Inchiostro delle gocce: **108×68**. Inchiostro dello scudo: **108×68**. Lo scudo segue l’ingombro della goccia originale. Le sagome sono condivise con il fronte tramite ../SharedCardAssets/tally_shapes.py.

Seme unico al centro (512,334), dentro il pannello interno. Insegna nella fascia superiore e cariche nella fascia inferiore: indicano informazioni diverse dalle statistiche laterali e rimangono. Esempi: vita 3/7, difesa 4/7, insegna attacco 2, cariche 1/3.

Le cornici laterali restano a x=210/814, con cuspidi di 18 px esattamente centrate sulle tacche e filetto secondario verso il margine. Le cornici, le fasce curve e l'ornamento centrale mantengono la composizione del retro precedente.

Bordo interno unificato: tutto il perimetro a (51,49)-(973,1487), raggio 24, usa lo stesso doppio filetto scuro/avorio 11.5/5.5 px delle curve. Filetto secondario a 8 px, agganciato alle curve, spessori 4.5/2.5 px. Stessa lieve grana su tutto l'inchiostro, senza cambi di stile nei punti d'incontro. Il bordo cartaceo esterno usa abrasioni più fini della nuova sorgente imagegen; l'interno rosso originale è conservato da 128 px in poi. back_frame.png e back_frame.svg mostrano la stessa finitura; back_frame_geometry.svg conserva i tracciati editabili. Prompt in Sources/BACK_BORDER_REFINEMENT_PROMPT.md.

## Montaggio

Usare back_clean oppure paper → artwork → frame, poi seme, tacche, insegna e cariche. Le rotazioni degli ideogrammi sono già incorporate: **runtime a zero**. Lo scudo semplice è ruotato di 90° in senso orario: la punta inferiore guarda il centro.

I PNG sono celle 256×256 con sagome normalizzate a 192×120. Il manifest specifica ink_size, alpha_bbox e full_sprite_render_size: usare le misure del manifest per mantenere l’ingombro leggero della goccia. Pieno/vuoto mantengono gli stessi ingombri e pivot.

Il filetto inferiore è già interrotto attorno alle cariche. charge_backplates.png è un livello vuoto storico e non va montato.

## Verifica e ricostruzione

build_assets.py usa Pillow/NumPy e le sorgenti locali; nessuna nuova generazione image_gen per questa modifica geometrica. Conserva i master approvati e aggiorna frame, sprite, overlay, atlante e manifest. Le sorgenti raster precedenti sono documentate in Sources/PRODUCTION_PROMPTS.md.

QA: sette stazioni, allineamenti, orientamento rigido delle punte, alpha, bbox pieno/vuoto e assenza di sovrapposizioni con cornici, seme e ornamento. Verifica visiva anche a 224×336. Import Sprite, Point, niente compressione/mipmap, PPU 1, alpha come trasparenza; nessun prefab o scena Unity modificato.

Il master dello scudo precedente è riutilizzato nelle tacche correnti. Cuori e campioni con intestazioni sono storici. Le versioni precedenti degli script sono in Sources/*before_glyph_tallies*.
