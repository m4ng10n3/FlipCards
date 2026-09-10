# Neon Monte — Scene kit v1

Nuovo kit grafico coordinato alle carte finali. Canvas di riferimento 1920 × 1080.
Consegna: asset separati, layout interattivo, manifest geometrico e specifica di animazione.
Le carte approvate e i simboli nemici esistenti sono riutilizzati come dipendenze.
La scena Unity attuale non viene ricostruita da questa anteprima.

- `Layout/`: composizione verificabile e misure in coordinate banda.
- `Table/`: tovaglia continua, senza UI incorporata.
- `Machine/`: mobile e parti statiche.
- `Reels/`: supporto del rullo; figure e statistiche restano contenuti dinamici.
- `Controls/`: fungo rosso e leva.
- `UI/`: superfici di inspector e legenda.
- `Sources/`: prompt esatti e provenienza delle immagini.
- `QA/`: verifica di file, misure e anteprime.

## Direzione

Tavolo petrolio, smalto verde e ottone consumato, carta avorio e rosso delle carte.
Tre corsie con centro comune fra carta, pronostico e rullo. Inspector compatto sotto
il mazzo sul rail sinistro. Nessuna colonna informativa permanente sulla destra.
La legenda si apre sopra il campo senza ridimensionarlo; quando aperta intercetta
l'input del campo e si chiude con pulsante o Escape.

Il fungo rosso attacca; la leva gira i rulli quando il turno consente di chiudere.
La leva ruota nello spazio attorno all'asse laterale della macchina: la lunghezza
proiettata varia, mentre il perno resta immobile. Non usare una rotazione Z del PNG.
