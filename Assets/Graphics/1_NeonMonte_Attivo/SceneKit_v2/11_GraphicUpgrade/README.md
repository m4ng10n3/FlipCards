# Graphic upgrade — 16 settembre 2026

Asset generati con image_gen integrato. Prompt completi in
`Assets/Graphics/2_Archivio/NeonMonte_Studi/graphic_upgrade_prompts.json`.

In uso:
- `book_closed.png`: copertina del libretto chiuso sul tavolo.
- `book_open.png`: **sorgente** della doppia pagina; il gioco monta la versione
  senza dorso di `12_TableProps` (`Tools/build_book.py`).
- `parchment.png`, `roller.png`: foglio e rulli della legenda. Il foglio sta
  tutto sotto il corpo avorio del rullo (dal 12,4% all'87,5% della texture);
  i rulli sporgono dal pannello per coprirlo.
- `Sky.mat`, `Roller.mat`: materiali dello shader `Animated Illustrated Surface`.

Superati e spostati in `2_Archivio/NeonMonte_Studi/graphic_upgrade_superati/`:
`cabinet_led.png` e `button_led.png` (gli inserti LED incollati sopra la cassa e
il fungo), `sky.png` (cielo 16:9 che poteva solo ondeggiare) e i due ritagli
`upgrade_*_inset`. I display LED e il cielo rotante ora stanno in
`12_TableProps`, con il loro README.

`MedallionSceneSkin` importa e registra gli asset; `MedallionSceneBuilder` è
l'unico proprietario delle misure. Ricostruire con il normale comando
`FlipCardsLayoutBuilder.Rebuild()`, fuori dal Play Mode.

`ParchmentRollerMotion` lega la rotazione della superficie cilindrica allo
spostamento reale del contenuto, con materiali privati rilasciati alla chiusura.

## Mazzo e pesca

Mazzo a (226,842) del canvas, rotazione locale +8°, scala 1,38 come le carte in
campo (i due piani hanno il centro alla stessa profondita'), sullo stesso
piano inclinato a 60° del tavolo, con ombra di contatto. **Nessun numero**: un
taglio per carta sotto quella in cima, e a mazzo pieno la pila è alta 52
(`DeckView.fullDeckHeight`), quindi l'altezza è proporzionale alle carte rimaste.

La pesca ha tre tempi, nell'ordine in cui si decide:
1. **estrazione** (`HandManager.TryExtractTop`): valida, spende gli AP una
   volta, toglie `deck[0]`. Il risultato è deciso qui;
2. **materializzazione** (`DeckView.MaterializeAndRelease`, .38 s): sulla carta
   vera in cima alla pila, che è proprio quella estratta, compaiono i segni del
   retro (`CardOverlay.PresentDrawBack`). La pila non si ricostruisce;
3. **consegna** (`HandManager.DeliverDrawn`): la pila scende di uno e la carta
   parte da quella posa con i segni accesi, .78 s di viaggio, .22 s di giro sul
   fronte all'arrivo.
