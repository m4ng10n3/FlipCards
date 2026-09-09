# Semi astronomici — ricerca del 9 settembre 2026

La richiesta è passata da sigilli inventati a simboli esistenti con la semplicità grafica di un seme. È stata scelta una famiglia astronomica coerente: **Sole, Luna, Saturno**. La scelta del trio e il trattamento grafico sono decisioni di art direction; non si afferma che tutti i giocatori conoscano già Saturno quanto un cuore o una picca.

| Segno | Identità verificata | Forma conservata | Trattamento |
|---|---|---|---|
| ☉ U+2609 | Sole | Cerchio con punto centrale | Rosso, anello robusto e punto pieno |
| ☽ U+263D | First Quarter Moon / Luna | Falce lunare | Petrolio, falce piena per una sagoma leggibile |
| ♄ U+2644 | Saturno | Croce superiore e tratto ricurvo | Oliva, tratto pieno robusto |

## Fonti consultate

- [NASA — Solar System Symbols](https://science.nasa.gov/resource/solar-system-symbols/): documenta la famiglia dei segni di Sole, Luna e pianeti e la loro tradizione astronomica/astrologica. Descrive la Luna come falce e associa storicamente il segno di Saturno alla falce agricola.
- [Unicode — Miscellaneous Symbols](https://www.unicode.org/charts/nameslist/n_2600.html): identifica U+2609 come Sun, U+263D come First Quarter Moon, U+2644 come Saturn.

## Produzione

Sono usati i glifi effettivi di Segoe UI Symbol disponibile sul computer, esportati in PNG trasparenti senza copiare o redistribuire il font. Non sono state scaricate o riutilizzate immagini NASA. Le modifiche riguardano peso, riempimento della falce e dimensione; non sono stati aggiunti sigilli, ornamenti o pseudo-rune.

I tre file usano celle 256×256 e un ingombro massimo 192×192; le proporzioni native sono conservate. Sul fronte la cella misura 224×224, con altezza massima dell'inchiostro 168 px. Ogni coppia diagonale mantiene la rotazione di 180°. La direzione artistica non modifica le fazioni serializzate del gioco.

Verifica visiva in `SUIT_LEGIBILITY.png`, in nero a 16, 24 e 36 px, e in `CONTACT_SHEET_ASTRONOMICAL.png` sui tre template. L'iconicità universale richiederebbe una prova con giocatori: qui è stato verificato il disegno e il confronto in piccolo.
