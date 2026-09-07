# FlipCards — Bisca indie

Riferimento approvato: `ArtStudies/indie-concept-study.png`. Carta avorio consumata, inchiostro scuro, rosso mattone, petrolio e verde muschio; mostri e simboli come stampe pixel art.

## Asset

- `portrait_*.png`: dieci creature delle carte, collegate ai prefab per nome.
- `symbol_*.png`: dieci simboli delle slot, usati anche dal rullo attraverso i prefab candidati.
- `card_front.png` e `card_back.png`: supporto avorio e dorso a tre occhi. Le informazioni sono testo e UI reali, non incorporate nelle immagini.
- `board.png`, `reel_housing.png`: tavolo e cassa del rullo.
- `glyph_sword.png`, `glyph_shield.png`, `glyph_broken.png`: insegna e risonanza.
- `Chrome/`: UI deterministica costruita da NeonMonteSkinBuilder.
- `Fonts/`: VT323, SIL Open Font License; licenza inclusa. Testi descrittivi piccoli mantengono il font leggibile esistente.

## Impaginazione e materiale

Nome e fazione nel bordo alto; artwork 204x232 al centro; indici di attacco e vita agli angoli bassi, tre tacche compatte e abilita nel bordo inferiore. Le carte sul tavolo hanno inclinazione prospettica di 18 gradi e apertura laterale; in hover si raddrizzano per la lettura. Il dorso mostra insegna e difesa senza rivelare nome o abilita. Il mazzo usa copie decorative dei dorsi senza overlay informativi.

Edizioni Regular (Anchor, Reaper), Foil (Relay, Bulwark, Hex, Bastion) e Polychrome (Vanguard, Oracle, Spark, Scythe). Foil e Polychrome usano CardShaderGraph originale sul ritratto, con alpha trasparente per la UI e intensita polychrome 0.3. La cornice di tutte le edizioni mantiene PrintedCard.shader: il riflesso segue il tilt, mentre gli indici restano su carta avorio. Le edizioni sono estetiche e non modificano statistiche o regole. Ogni carta possiede il proprio materiale runtime, distrutto con la carta. Flip, hover, scambi, risoluzione e input restano gestiti dal flusso esistente.

## Ricostruzione

Menu **FlipCards → Ricostruisci layout di gioco**. Il builder importa gli sprite con filtro Point, PPU 1, senza mipmap o compressione, prepara font/materiale, aggiorna prefab e ricostruisce la scena. Le tre corsie conservano dimensioni e gerarchia funzionale. Non modificare manualmente scena o immagini Chrome.

Prompt: `PROMPTS.md` e `MONSTER_PROMPTS.md`; generazione tramite image_gen integrato. Studi scartati fuori da Assets, in ArtStudies.

La manovella reel_lever.png segue IsRolling tramite ReelLever. Campo largo 1100, passo corsie 370, centri x=496/866/1236: entrambi i lati e il pronostico condividono la stessa geometria. Dimensioni carta 224x336 e slot 352x288 invariate.
