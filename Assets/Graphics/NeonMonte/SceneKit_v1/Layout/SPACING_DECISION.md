# Cassa, leva e passo delle carte

Scelta: **400 px fra i centri**; centri X = **608, 1008, 1408** sul canvas 1920 × 1080.
Carte 224 × 336, spazio libero fra carte 176 px. Rullo, lampade, pattern e asse
del pronostico condividono il centro della carta corrispondente.

La cassa è modulare: testata, zoccolo, fianchi e un modulo centrale ripetuto tre
volte. Il modulo ripetuto evita di trasferire al gioco le piccole differenze di
spaziatura prodotte dall'illustrazione generativa. Le cinque parti sono ritagli
della stessa cassa; la composizione si può ricostruire in Unity.

| Passo | Larghezza cassa | Spazio fra carte | Considerazione |
|---:|---:|---:|---|
| 375 | 1351 px | 151 px | Compatto, poco margine laterale nelle vaschette delle lampade. |
| **400** | **1441 px** | **176 px** | Buon rapporto tra finestre, spie e corsa laterale della leva. |
| 425 | 1531 px | 201 px | Occupa più larghezza senza aumentare l'artwork; porta la leva più vicina al margine. |

Le tre prove sono in `QA/spacing_375.png`, `spacing_400.png`, `spacing_425.png`.
Questa è una scelta di composizione, non una misura di usabilità da playtest.

## Ingombri finali

- Cassa: x 309.99, y 66, larghezza 1441.06, altezza 500.
- Tre finestre: circa 368 × 184; l'illustrazione resta quadrata dentro il tamburo orizzontale.
- Tre vaschette lampadine integrate, sette sedi per riga, totali numerici sempre visibili.
- Carte: y 580 → 916. Richiamo mano da y 924, alto 156: 8 px di separazione.
- Inspector: x 28, y 566, 252 × 304, sotto il mazzo.
- Perno leva: x 1756.08, y 320. È sul fianco, accanto ai rulli.
- Asse leva = asse rulli = X locale orizzontale `(1,0,0)`.
- La manopola percorre un arco verticale in profondità, con scorcio e variazione
  della scala apparente. Il perno resta fisso durante tutta l'animazione.

## Gerarchia

La legenda chiusa occupa solo il richiamo in alto a destra. Aperta, sovrappone un
pannello 1004 × 780 e blocca le interazioni sottostanti; non modifica le misure.
Il diario è richiamabile dal rail e non sottrae una colonna permanente al campo.

La preview serve alla revisione grafica. Valori, nomi e pattern sono fixture
dimostrative: in Unity vanno letti dagli oggetti reali. Le illustrazioni di carte
e simboli nemici sono dipendenze del set già approvato, non nuovi asset ridisegnati.
