# Neon Monte — fronte francese v2

Pacchetto grafico completato il 9 settembre 2026. Riferimenti: `../indie-concept-study.png`, `../base_front_unfinished.png` e `../card_engraved_parts.png`. Il percorso `base/_front/_unfinished.png` indicato nella richiesta non era presente: il riferimento corrispondente disponibile era `base_front_unfinished.png`.

Direzione corrente delle fazioni: **simboli astronomici esistenti**, verificati online: Sole ☉ (U+2609), Luna ☽ (U+263D), Saturno ♄ (U+2644). Le forme tradizionali sono state rese come semi a tinta unita con tratti robusti. Font sorgente locale: Segoe UI Symbol, non redistribuito. Fonti e motivazione in `ASTRONOMICAL_SOURCES.md`. Nessuna modifica ai nomi o alle regole delle fazioni nel gioco.

## File da aprire

- `CONTACT_SHEET_SOLID_SPEAR.png`: tre fazioni correnti, lancia piena A predefinita, libreria simboli e controllo a 224×336.
- `ATTACK_SOLID_COMPARISON.png`: confronto cuore/lancia piena e prova a dimensioni ridotte.
- `ATTACK_SOLID_STUDY.png`: confronto A (sagoma iniziale chiusa), B (estensione intermedia), C (estensione maggiore). Sprite separati in `Studies/`.
- `SUIT_LEGIBILITY.png`: confronto monocromatico dei tre segni a 16, 24 e 36 px.
- `Template/front_clean.png`: fronte avorio e cornice, senza fazione, statistiche o valori incorporati.
- `Template/front_sun_preview.png`, `front_moon_preview.png`, `front_saturn_preview.png`: campioni correnti assemblati, con centro e spazi testo vuoti. I campioni mostrano 3 punti vita e 4 attacco solo per distinguere pieno/vuoto.
- `Template/front_empty_preview.png`: campione con tutti gli indicatori vuoti.
- `Template/front_paper.png` e `front_frame.png`: carta e cornice separati. La cornice ha trasparenza vera.
- `Template/front_*_overlay.png`: livelli sovrapponibili per i campioni.

## Simboli e indicatori

`Symbols/` contiene picca d'attacco, scudo, cuore, rombo, stella, stelo, cerchio carica vuoto/pieno e i tre segni correnti: `faction_sun.png`, `faction_moon.png`, `faction_saturn.png`. I simboli sono PNG RGBA 256×256 con pivot centrale e area utile massima 192×192. I tre segni conservano proporzioni naturali nello stesso quadrato, senza stirare falce e Saturno per raggiungere la larghezza del Sole. Sono incluse le varianti `_black.png` nere e `_mask.png` bianche per tint runtime. I precedenti file sword, flame/wave/thorn e knot/orbit/threshold, insieme ai precedenti CONTACT_SHEET.png e CONTACT_SHEET_ASTRONOMICAL.png, sono revisioni superate conservate per confronto; non compaiono nell'atlante o nel manifest correnti.

`Indicators/` contiene `drop_full.png`, `drop_empty.png`, `spear_full.png`, `spear_empty.png`. Tutti sono PNG RGBA 256×256 e occupano esattamente il rettangolo `(32,68)-(224,188)`, cioè 192×120. Gli stati pieno/vuoto possono essere scambiati senza modificare rettangolo o pivot. La goccia ha pancia rotonda a sinistra e punta a destra, verso l'interno della carta. La punta di lancia è romboidale, con due facce piene in tonalità scura e chiara contigue: nessuna incisione, linea chiara o fessura centrale. È orientata verso destra come nella bozza di partenza.

La **punta d'attacco predefinita è la variante A**, sagoma iniziale con centro pieno e due campiture scure contigue. È applicata ai template delle tre fazioni e all'atlante. Nel fronte punta **verso l'alto**: usare `Symbols/attack_spade.png` senza rotazione. `Symbols/attack_spade.svg` conserva il vettoriale. Il PNG coincide con `Studies/attack_A_closed.png`. Le altre varianti e gli studi successivi restano confronti storici, non predefiniti.

Per aggiornare solo i simboli e le composizioni riutilizzando la cornice esistente: `build_assets.py --reuse-frame`. Riduce la memoria necessaria senza ridisegnare la cornice.

Nel montaggio il cuore occupa **88×88 px** e la picca allungata **78×104 px**, centrati nello stesso spazio verticale di 174 px: 43 px liberi sopra e sotto il cuore, 35 px sopra e sotto la picca. Entrambi gli spazi riservati distano 34 px dalla fazione e 16 px dall'estremità del filetto. Gli emblemi di fazione rimangono entro 168×168 px. La picca usa un ingombro di 144×192 px nel PNG sorgente 256×256.

`symbols_normalized_atlas.png` raccoglie gli stessi elementi in celle 256×256 su un foglio 1024×1024. La cella finale è vuota. Coordinate e nomi sono nel manifest; le immagini individuali non richiedono slicing.

## Geometria verificata

Canvas 1024×1536. Coordinate in pixel dall'angolo superiore sinistro:

- Sette tacche sinistre: y = 516, 656, 796, 936, 1076, 1216, 1356; x = 94.
- Sette tacche destre: y = 180, 320, 460, 600, 740, 880, 1020; x = 930.
- Filetto interno centrato su x = 140 a sinistra, come il cuore, e x = 884 a destra, come la picca d'attacco. Le tacche deviano di 16 px verso l'interno. Il filetto che affianca le fazioni passa per x=250 in alto a sinistra e x=774 in basso a destra, lasciando spazio agli emblemi maggiorati.
- Goccia e lancia occupano 94×59 px di inchiostro. Tra gli estremi orizzontali dei simboli e gli assi dei filetti rimangono 16 px verso il bordo esterno e 15 px verso la tacca interna, simmetricamente.
- Passo identico 140 px; estensione di ogni fila 840 px. Il tratto più corto rispetto alla revisione precedente riserva più spazio verticale ai due indici, mantenendo le quote terminali sulle fazioni opposte.
- Fazioni centrate in `(140,180)` e `(884,1356)`. L'ultima goccia è alla quota del centro della fazione opposta; la prima lancia è alla quota del centro della fazione superiore.
- Le due cornici interne e le stazioni delle tacche sono accoppiate con rotazione di 180°. Fazioni e stelline hanno copie ruotate, con centri accoppiati diagonalmente.
- I tre cerchi delle cariche rimangono sul bordo inferiore, come nel riferimento. Questa decorazione funzionale non è duplicata in alto.

`QA_REPORT.json` registra conteggi, confronto pixel della simmetria della cornice, quote terminali, dimensioni e bounding box alpha. Sono stati controllati visivamente il foglio completo e la riduzione 224×336. Non è stato eseguito un test dentro Unity: questo pacchetto consegna gli asset, non cambia prefab o scena.

## Montaggio e import

Usare `front_clean.png` come base; applicare simboli e indicatori dinamici secondo `layout_manifest.json`. Le aree testo restano avorio continuo: titolo `(300,112)-(790,214)`, abilità `(234,1322)-(724,1424)`. L'artwork centrale ha area libera `(254,282)-(770,1254)`.

Le misure di montaggio del manifest descrivono l'inchiostro visibile, escludendo il padding trasparente dei file. Per un indicatore con inchiostro 94×59 px, un'Image Unity con sprite intero 256×256 deve avere rettangolo di circa 125.33×125.87 px. Per le fazioni usare lo sprite completo in un rettangolo 224×224 px: l'inchiostro rimane entro 168×168 preservando le proporzioni naturali del segno. Non stirare l'intera cella quadrata nel rettangolo dell'inchiostro.

Import suggerito: Sprite, Point, niente compressione o mipmap, PPU 1, alpha come trasparenza. Il gioco può poi adattare il canvas 1024×1536 al rettangolo carta esistente. Nessuna regola di combattimento o limite dei valori è stato modificato: i sette indicatori sono la specifica grafica richiesta.

## Riproducibilità

Carta e statistiche derivano dalle generazioni image_gen precedenti, con rifinitura geometrica autorizzata dall'utente. I tre segni astronomici correnti sono rasterizzazioni dei glifi Unicode reali, non nuove invenzioni generative; il peso è stato corretto e la falce è stata riempita. `build_assets.py` ricrea gli export, i fogli di controllo e il manifest con Pillow e NumPy. `Sources/`, `PROMPTS.md` e `ABSTRACT_SIGIL_PROMPTS.json` conservano la storia delle revisioni precedenti. `ASTRONOMICAL_SOURCES.md` documenta la scelta corrente.
