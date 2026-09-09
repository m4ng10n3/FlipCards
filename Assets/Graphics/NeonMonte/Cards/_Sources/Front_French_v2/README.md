# Neon Monte — fronte francese, tacche a glifo

Revisione corrente **tattoo-relief-front-v7**. Cornice lavorata come un tatuaggio in rilievo sulla carta: contorno carbone, costa avorio, sottile ombra di incisione e punteggiatura artistica ricavata dalla sorgente generata. Il disegno mantiene curve e coordinate precise, allineamenti e tacche. Il template base e le preview standard non contengono il separatore del testo, conservato a parte.

Template/front_frame_tattoo.png e front_frame.png sono la cornice finale RGBA. front_frame.svg incorpora il raster finito per conservarne il dettaglio; front_frame_geometry.svg mantiene i tracciati vettoriali editabili. Sources/frame_tattoo_generated.png è la sorgente imagegen; tattoo_frame.py ne recupera gli inchiostri, elimina il checker RGB e registra i dettagli sulle coordinate esatte.

## Limitatore superiore del testo

Template/text_top_limiter.png è un asset trasparente indipendente, 480×24, centrato di default in (479,1292), sull'area testo safe_regions.ability. Disponibili anche SVG, maschera bianca da colorare e overlay a piena carta. Filetto simmetrico semplice da 2.5 px, inchiostro al 63%, estremità assottigliate. Non è incorporato in front_clean o nelle preview standard; front_*_with_text_limiter_preview.png ne mostra il montaggio.

Spostare il centro Y per regolare l'altezza dell'area testo senza deformare il segno. Nel builder: --text-top-y 1292. --text-rule-height 24 (16..48) regola il padding trasparente senza ispessire il tratto. Il manifest è la fonte delle misure effettive. Nessun raycast sull'asset.

## File correnti

- CONTACT_SHEET.png: tre fazioni, sprite e controllo a 224×336. Anche CONTACT_SHEET_SOLID_SPEAR.png mostra la revisione corrente.
- Template/front_clean.png: carta e cornice senza dati, titoli o artwork incorporati.
- Template/front_paper.png, front_frame.png, front_frame.svg: livelli separati.
- Template/front_sun_preview.png, front_moon_preview.png, front_saturn_preview.png: campioni con 3 vita e 4 attacco; overlay separati.
- Template/front_empty_preview.png e front_empty_overlay.png: tutte le tacche vuote.
- Indicators/drop_full.png / drop_empty.png: goccia nelle proporzioni originali, punta verso destra.
- Indicators/attack_full.png / attack_empty.png: punta di lancia romboidale a due facce, rivolta a sinistra, dentro la carta.
- symbols_normalized_atlas.png, layout_manifest.json, QA_REPORT.json.

## Misure

Canvas 1024×1536. Fazioni in (140,180) e (884,1356), seconda ruotata 180°. Nessuna intestazione statistica. Gocce 94×59, lance 94×59 di inchiostro visibile.

Sette tacche per lato, passo **166 px**. Gocce centrate su x=94, y=[360, 526, 692, 858, 1024, 1190, 1356]. Lance su x=930, y=[180, 346, 512, 678, 844, 1010, 1176]. I filetti passano per x=140/884, con cuspidi profonde 16 px, centrate esattamente sulle tacche. La cornice mantiene la simmetria di rotazione 180°.

I PNG hanno celle 256×256 e sagome normalizzate nelle proporzioni originali della goccia (192×120). Il manifest specifica ink_size, alpha_bbox e full_sprite_render_size per ogni tacca. Pieno/vuoto hanno stessi contorni esterni e pivot. Il verso della lancia è incorporato: runtime a zero.

Titolo, artwork e abilità mantengono le aree libere del fronte precedente. Le tre cariche restano a y=1458; applicare Template/charge_backplates.png prima dei cerchi per interrompere il filetto all'interno.

## Sorgenti e ricostruzione

../SharedCardAssets/tally_shapes.py è la fonte comune delle tacche di fronte e retro. Ripristina la goccia originale 192×120, la lancia romboidale e lo scudo semplice, nello stesso ingombro. Gli asset defense_full/empty sono disponibili anche qui come libreria comune, ma non sono montati sul fronte.

build_assets.py assembla con Pillow/NumPy e tattoo_frame.py la nuova lavorazione imagegen salvata in Sources. Le ricostruzioni successive non richiedono altre chiamate. Prompt e metodo sono in Sources/FRAME_TATTOO_PROMPT.md. La carta e i simboli astronomici restano quelli approvati.

Import Sprite, Point, niente compressione/mipmap, PPU 1, alpha come trasparenza. QA: conteggi, simmetria, orientamenti, bbox pieni/vuoti, alpha e assenza di sovrapposizioni tra tacche, cornici e fazioni. Nessun prefab o scena Unity modificato.

Il master spear_full precedente è la sorgente della lancia corrente, specchiata verso il centro. Cuore e studi restano storici.
