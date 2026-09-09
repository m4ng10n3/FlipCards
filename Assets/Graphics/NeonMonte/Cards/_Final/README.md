# Neon Monte — asset carte finali

Pacchetto di produzione approvato, 9 settembre 2026. Aprire FINAL_PREVIEW.png per il confronto; Front/Previews e Back/Previews contengono le tre fazioni con statistiche di esempio e le versioni con tacche vuote.

## Cartelle

- Front: template definitivo con cornice tatuata leggermente scurita, sprite vita/attacco, simboli astronomici, atlante e manifest.
- Back: template rosso con ornamento a tre occhi, sprite vita/difesa, insegne, atlante e manifest.
- Optional/TextTopLimiter: separatore leggero con PNG, maschera, SVG, overlay e placement.json. Non montato nelle basi o nelle anteprime finali.
- FILES.json: inventario, dimensioni e SHA-256 dei file consegnati. QA_REPORT.json: verifica del pacchetto.

## Montaggio

Usare Front/Template/front_clean.png oppure Back/Template/back_clean.png come base, poi montare simboli, tacche, testo dinamico e cariche secondo il layout_manifest.json della faccia. Le basi hanno già la cornice definitiva: non aggiungerla nuovamente sopra. I livelli paper/frame/artwork sono disponibili per chi vuole ricomporre la base.

Front: gocce dall'alto, lance romboidali dal basso. Back: gocce e scudi semplici entrambi dall'alto; un valore N riempie le prime N posizioni. Gli indici nei manifest sono ordinati per Y crescente. Sette tacche per colonna. Le punte sono già orientate verso il centro, rotazione runtime zero. Nessuna intestazione statistica aggiuntiva.

I simboli grandi d'insegna del back restano separati dalle tacche: attack_spade o defense_club_B con valore numerico dinamico. Le preview usano valore 2, vita 3, attacco/difesa 4. Il front mostra 0 cariche, il back 1. Non usare una preview con valori incorporati come base dinamica.

Sul front applicare charge_backplates.png prima dei tre cerchi per interrompere la cornice sotto le cariche. Sul back la linea è già interrotta. Gli estremi verticali interni del front restano allineati alla Y del centro dei simboli fazione.

PNG base 1024×1536; target 224×336, fattore 0.21875. Coordinate dall'alto a sinistra. I singoli simboli sono celle 256×256 con trasparenza: usare full_sprite_render_size e alpha_bbox del manifest, non confondere la cella con l'ingombro d'inchiostro. Preservare i colori originali o usare i file _mask per una tinta runtime. Gli overlay nelle Previews sono solo esempi di composizione.

Import Unity: Sprite, Point, compressione disattivata, niente mipmap, alpha come trasparenza, Pixels Per Unit 1. Nessun Raycast Target sui figli grafici. Per l'atlante front i rect sono rect_top_left, per il back rect: entrambi partono dall'alto; convertire Y per Sprite Editor. Le cornici SVG finali possono contenere raster incorporati; usare i PNG a runtime.

## Sorgenti e ricostruzione

Le cartelle di lavoro originali restano Front_French_v2, Back_French_v2 e SharedCardAssets accanto a Cards_Final. Sources e Studies conservano i riferimenti generati, i prompt e le revisioni storiche; non sono parte del pacchetto di produzione. Non usare le vecchie prove di elmo o glifi come tacche.

Con Python + Pillow + NumPy, dalla cartella NeonMonte:

1. python Front_French_v2/build_assets.py
2. python Back_French_v2/build_assets.py
3. python SharedCardAssets/build_comparison.py
4. python SharedCardAssets/package_final_cards.py

La ricostruzione usa le sorgenti già salvate e non effettua chiamate imagegen. I file finali sono una consegna riproducibile dei builder. Questa chiusura riguarda gli asset: nessun prefab, scena o codice di combattimento è stato modificato.
