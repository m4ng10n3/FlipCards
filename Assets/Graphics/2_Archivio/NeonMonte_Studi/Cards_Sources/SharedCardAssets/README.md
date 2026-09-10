# Tacche condivise — goccia, lancia romboidale, scudo semplice

Decisione corrente: le tacche identificano direttamente la statistica. Nessun simbolo separato di intestazione in front o back. Sette stazioni per colonna, stati pieno/vuoto e PNG con alpha reale.

Aprire FRONT_BACK_COMPARISON.png per il confronto completo, incluse le riduzioni a 224×336 e i tre glifi in tinta unica. QA_REPORT.json verifica anche che fronte e retro condividano esattamente le sagome alpha.

## Sorgenti e proporzioni

- Sources/attack_rhombic_spear.png: lancia romboidale originale a due facce contigue, specchiata orizzontalmente per rivolgere la punta a sinistra.
- Sources/defense_simple_shield_upright.png: scudo semplice originale, ruotato di 90° in senso orario per rivolgere la punta inferiore a sinistra.
- Sources/drop_before_rebalance.png: goccia originale ripristinata senza alterare la sagoma o le proporzioni 192×120, punta a destra.

Ingombro comune determinato dalla goccia originale: 94×59 sul fronte, 108×68 sul retro per tutte le tacche. Le punte sono riallineate alle cornici. Le misure per ciascuna faccia sono definite in FACE_SIZES.

Le tre sagome condividono le proporzioni originali della goccia. Ogni manifest di faccia fornisce il bounding box alpha e la misura dell'intera cella sprite da usare. I pieni e i vuoti sono ricavati dalla stessa sagoma; i vuoti mantengono gli ingombri e il pivot dei pieni.

## Ricostruzione

Con Python, Pillow e NumPy, eseguire in ordine:

1. ../Front_French_v2/build_assets.py
2. ../Back_French_v2/build_assets.py
3. build_comparison.py

tally_shapes.py è la fonte comune. I due builder leggono gli stessi master senza eseguire il codice dell'altra faccia. Il retro conserva il suo layout sagomato indipendente, il fronte la cornice francese. I vecchi simboli restano disponibili come sorgenti storiche e per l'insegna, ma non sono montati come intestazioni statistiche.

Nessuna nuova generazione image_gen per questa revisione: riuso dei master precedentemente generati e rifinitura geometrica richiesta dall'utente. Nessun prefab, scena o codice di combattimento modificato.
