# Riferimenti artistici — Neon Monte

## Riferimento corrente del disegno sul tavolo

Usare `2026-09-10_original_medallion_full.png` e
`2026-09-10_original_medallion_empty.png`.
Il motivo è il livello originale `Cards/_Final/Back/Template/back_artwork_overlay.png`,
ritagliato al suo alpha, tinto oro e proiettato integralmente mediante UNA omografia.
Nessun occhio, rombo o intreccio è ridisegnato o riposizionato internamente.
Il medaglione occupa il piano fino in lontananza: a tavolo pieno gli oggetti
coprono il centro. NON tentare di mostrare i tre occhi negli spazi liberi.

Le revisioni generate `three_eyes_table_*` descritte più sotto sono SUPERATE E
RESPINTE: deformavano il motivo originale e lo restringevano al primo piano.
Sono conservate soltanto come storico, non come riferimento da seguire.
Script riproducibile: `../SceneKit_v2/_Sources/project_original_medallion.py`.
Fonte, SHA-256 e trasformazione: `../SceneKit_v2/07_Spec/original_medallion_projection.json`.

## Composizione approvata, 10 settembre 2026

`2026-09-10_approved_scene_composition.png` è l'immagine indicata dall'utente:
**«questa è ottima»**. Copia originale conservata senza modifiche.

### Da mantenere

- Cassa importante, appoggiata sul tavolo, con top e fianco visibili e profondità.
- Tavolo e carte in prospettiva, ombre di contatto e bellezza della scena.
- Leva accanto ai rulli, asse orizzontale parallelo all'asse di rotazione dei rulli.
- Lampadine grandi e distanziate sopra e sotto le finestre, con forme e tecnologie
  diverse: globo, filamento allungato, tubo a scarica. Nessun bulbo a forma di icona.
- Mazzo a sinistra, inspector compatto sotto; fungo rosso sul tavolo a destra.
- Legenda richiamabile in overlay, informazioni globali leggibili a colpo d'occhio.

### Adattamenti richiesti

1. Artstyle **meno realistico**, coerente con la grafica illustrata delle carte
   finali: contorni a inchiostro, campiture e ombre controllate, grana di stampa.
   Conservare la profondità; non tornare a un layout frontale piatto.
2. Aggiungere **bordi ai rulli con il seme e l'attacco**. Le famiglie sono
   Sole, Luna e Saturno; usare i simboli approvati, non i vecchi simboli di fazione.
3. **Vita: bastano le lampadine.** Non aggiungere gocce, barre HP o conteggi di
   vita sul bordo del rullo. Non copiare l'intero template delle carte sui rulli.
4. Resta aperta la predisposizione a più di tre rulli, senza comprimere le luci
   fino a renderle illeggibili; le carte si possono selezionare per leggerle meglio.

La composizione è approvata; la resa realistica dell'immagine non è l'artstyle finale.
Le varianti generate dopo questo riferimento vanno conservate separatamente.

## Revisione: cielo aperto e velluto ricamato

`2026-09-10_starry_sky_gold_velvet.png` applica le successive indicazioni:
cielo stellato aperto senza stanza o oggetti decorativi, tavolo di velluto verde
con ricamo dorato in rilievo ispirato agli intrecci e alle stelle del dorso delle
carte. Restano mazzo, foglio di dettaglio, slot con leva, carte e fungo rosso.
Sono eliminate le targhette fisiche ATTACCA, GIRA, NEON MONTE e le tre targhette
vuote sopra le lampadine blu. HUD e richiamo della legenda restano overlay.
È una nuova proposta generata, conservata senza sovrascrivere il riferimento
compositivo approvato. Prompt esatto in
`../SceneKit_v2/_Sources/sky_velvet_edit_prompt.json` (generatore integrato).

## Revisione del medaglione sul tavolo: tre occhi e rombo

`2026-09-10_three_eyes_table_perspective.png` è la nuova composizione completa.
Il ricamo riprende esplicitamente il medaglione centrale del dorso: tre occhi
(uno posteriore, due anteriori), rombo a doppio bordo, stelle e intrecci.
Il motivo è scorciato sul piano del tavolo, con punte laterali oltre la fila
delle carte e punta anteriore visibile. La spaziatura delle carte lascia
riconoscibili il rombo e i tre occhi anche nella composizione occupata.

`2026-09-10_three_eyes_table_empty.png` mostra lo stesso motivo senza oggetti o
HUD. Il fondo di lavoro si trova anche in
`../SceneKit_v2/03_Table/table_three_eyes_empty.png`. È una derivazione generata:
non va considerata una ricostruzione pixel-identica degli elementi nascosti.
Prompt originali e correzione del margine anteriore in
`../SceneKit_v2/_Sources/three_eyes_table_prompts.json`.
