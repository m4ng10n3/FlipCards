# Riferimenti artistici — Neon Monte

Qui restano solo i riferimenti correnti. Quelli superati sono in
`2_Archivio/NeonMonte_Studi/ArtReferences_superate`.

## Riferimento corrente del layout e del disegno sul tavolo

Usare `2026-09-10_original_medallion_full.png` e
`2026-09-10_original_medallion_empty.png`.
Il motivo è il livello originale `Cards/_Final/Back/Template/back_artwork_overlay.png`,
ritagliato al suo alpha, tinto oro e proiettato integralmente mediante UNA omografia.
Nessun occhio, rombo o intreccio è ridisegnato o riposizionato internamente.
Il medaglione occupa il piano fino in lontananza: a tavolo pieno gli oggetti
coprono il centro. NON tentare di mostrare i tre occhi negli spazi liberi.

Script riproducibile:
`2_Archivio/NeonMonte_Studi/SceneKit_v2_Studi/_Sources/project_original_medallion.py`
(scrive il tavolo montato in `SceneKit_v2/03_Table`).
Fonte, SHA-256 e trasformazione: `../SceneKit_v2/07_Spec/original_medallion_projection.json`.

Dal riferimento il gioco prende anche l'impaginazione: riquadro di stato in alto
a sinistra, cassa al centro con leva sul fianco destro, tre carte in fila
posate sul panno sotto i rulli, mazzo in prospettiva a sinistra, fungo rosso a
destra, e sui rulli seme in alto a sinistra e tacche d'attacco a destra. Come ci
si arriva è in [08_Integration](../SceneKit_v2/08_Integration/README.md).

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

## Superati (in archivio)

- `2026-09-10_starry_sky_gold_velvet.png`: proposta con cielo aperto e velluto
  ricamato, da cui viene il tavolo attuale; prompt in
  `SceneKit_v2_Studi/_Sources/sky_velvet_edit_prompt.json`.
- `2026-09-10_three_eyes_table_*`: revisioni generate del medaglione, **respinte**:
  deformavano il motivo originale e lo restringevano al primo piano.
- `2026-09-10_style_variant_with_reel_borders.png`: variante illustrata con i
  bordi dei rulli.
