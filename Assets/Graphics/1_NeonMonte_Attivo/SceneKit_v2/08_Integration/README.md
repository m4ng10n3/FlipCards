# Scena attiva — Medallion

Il layout in uso è costruito da `Assets/Editor/MedallionSceneBuilder.cs`, richiamato
dal menu **FlipCards → Ricostruisci layout di gioco**. Riferimento:
`ArtReferences/2026-09-10_original_medallion_full.png`. Canvas 1920×1080, tre
corsie. Le misure sono costanti in testa al builder; `layout_manifest.json` ne è
la fotografia.

## Asset effettivamente montati

| Elemento | Fonte in `1_NeonMonte_Attivo` |
|---|---|
| Carte approvate | `Cards/_Final` |
| Tavolo e medaglione | `SceneKit_v2/03_Table/table_original_medallion_empty.png` |
| Cassa senza targhette | `08_Integration/cabinet_clean.png`, alpha da `02_Machine/cabinet_illustrated.png` |
| Faccia del rullo | `02_Machine/reel_face_ink.png`, da `Tools/build_reel_face.py` |
| Pulsante, cappello e basamento | `08_Integration/mushroom_clean.png`, tagliato a y=600 |
| Leva: tamburo, bocca, asta, manopola | `04_Controls/lever_ink_{hub,socket,shaft,knob}.png`, da `Tools/build_lever_parts.py` su `04_Controls/lever_rest.png` |
| Rulli della pergamena | `04_Controls/lever_ink_shaft.png`, coricato |
| Carta di libretto, pergamena e targhetta di stato | `06_UI/legend_paper.png` |
| Vita, attacco, guardia | `05_Lamps`, stati on/off |

Gli script in `Tools` non generano immagini: ritagliano, raddrizzano e ombreggiano
asset gia' approvati. Si rilanciano con Python + Pillow + NumPy, poi si ricostruisce.

## Il tavolo in prospettiva

La Main Camera e' **prospettica** (FOV verticale 17°). Tutto quello che sta sul
piano del canvas si vede come prima; carte in campo, mazzo e libretto stanno invece
su un piano inclinato di **60°**, ruotati attorno al **proprio centro**. Il centro
resta dove lo mette il layout e i bordi convergono verso lo stesso punto di fuga:
carte a trapezio come nel riferimento, lato lontano circa 0,9 del vicino.

- **Carte**: scala uniforme 1,34, fila centrata a y 868 sugli stessi centri dei
  rulli (607 / 964,5 / 1322). Il pronostico sta sopra, a y 694-734.
- **Mazzo**: a (178, 800), girato di −10° sul panno. In cima il prefab vero della
  prossima carta, sotto 18 tagli di carta lungo la normale del tavolo, scalati
  verso sinistra: lo spessore si vede sul fianco rivolto al centro del tavolo.
- **Libretto**: sullo stesso piano, girato di −7°.

Prima la prospettiva era finta: zona scalata 1,18×0,66, rotazioni per corsia in
`CardView` e mazzo schiacciato e ruotato. Una scala non uniforme sotto una rotazione
fa uno shear: e' da li' che venivano carte e mazzo "storti".

## Riquadro di stato

In alto a sinistra, sulla carta d'avorio con le proporzioni del suo sprite
(336×212): **BOSS** in inchiostro rosso, sotto **VITA**, **AP**, un filetto e
**TURNO**. La vita del boss non sta piu' sulla cassa.

## Rulli

La cella resta 352×288 ma sta a scala uniforme 0,856, cosi la faccia riempie la
finestra della cassa (294×238) e passa pochi pixel sotto gli anelli d'ottone, che
sono disegnati nella cassa. La faccia e' la carta delle carte finali avvolta su un
tamburo: grana compressa verso alto e basso, luce cilindrica, tratteggio a
inchiostro sui bordi, filetto doppio con gli angoli a unghia.

Impaginazione, dal riferimento (costanti in `SlotOverlay`):

- **seme** in alto a sinistra: lo stesso sprite delle carte, negli stessi colori,
  perche' la risonanza e' "stesso seme fra carta e casella";
- **nome** in basso a sinistra, sotto la colonna del seme;
- **simbolo** al centro, 200×200;
- **tacche d'attacco** in colonna a destra: tre sedi, piene dal basso come le lance
  delle carte, oltre tre compare il totale (`ReelPrintedAttack`);
- **numero della lastra** in alto a destra, **programma** in basso a destra,
  **risonanza** sotto il seme.

La vita non si stampa sul rullo: sono le lampade rosse. Le tre file di lampade si
centrano sulla colonna (`MedallionInstruments`), che ora e' larga quanto la faccia.

## Comandi fisici

**Leva** (`MedallionLever`): quattro pezzi dallo stesso disegno a inchiostro della
cassa, montati attorno all'asse del tamburo in (1612, 460) a scala 0,19. Il fianco
sinistro del tamburo tocca la guancia destra della cassa (x 1582 a quell'altezza):
e' montata sulla macchina, non galleggia accanto.

- Ordine di disegno a riposo: bocca scura, asta, tamburo, manopola. Oltre 40° di
  corsa l'asta viene verso la camera e passa davanti al tamburo; la bocca resta in
  fondo e copre il buco lasciato dall'asta.
- Proiezione dell'asse orizzontale: pitch 20°, D=900, corsa 0° → 105° in 340 ms,
  fermo 120, ritorno 420. Il disegno pende di 10,77° a destra, e la stessa
  inclinazione vale per tutta la corsa: a riposo i pezzi ricompongono l'originale.
- Campionato in Play: la manopola cresce da 90 a 107 px venendo avanti, l'asta si
  accorcia fino a ~57 px a 90° e poi punta in basso; lo scambio di ordine avviene
  a 40°. Dati in `Logs/lever-sample.txt`.

La leva precedente (perno a disco, asta e manopola realistici dello studio v1) e'
in `2_Archivio/NeonMonte_Studi/SceneKit_v1/Controls`.

**Fungo** (`TableControlFeedback.cap`): `mushroom_clean.png` e' un disegno solo,
tagliato in cappello (0..600) e basamento (600..1254) da `MedallionSceneSkin.Slice`.
Il basamento sta davanti, cosi' il cappello che scende sparisce dietro la ghiera.

## Lampade

Le sedi seguono i valori veri delle caselle: vita 3–5 → **7 sedi**, attacco 1–3 →
**3 sedi**, guardia 0–5 → **5 sedi larghe**. Il tubo a scarica e' quasi quadrato e
va sempre con `preserveAspect`. Oltre le sedi disponibili compare il totale numerico.

`MedallionSceneSkin` configura gli import e registra gli sprite nella skin runtime.
La cassa generata usa `CabinetMasked.mat` e l'alpha dell'originale
`cabinet_illustrated.png`: i due raster restano integri.

## Superfici di lettura

**Dettaglio** e' un **libretto**: chiuso sta sul tavolo oltre il mazzo — piu'
lontano dal giocatore, perche' e' roba da consultare, non da giocare — e aperto
mostra due pagine d'avorio, dorso di pelle opaco e linguette d'indice sul taglio
esterno (Campo, Mano, Rullo, Registro).

**Legenda** e' una **pergamena**: chiusa e' un rotolo in alto a destra, e al clic
va al centro e si srotola (`MedallionScroll`). I due rulli d'ottone stanno
**dentro** il contenuto scorrevole: se il testo supera l'altezza se ne vede uno per
volta, se ci sta si vedono tutti e due.

Le pagine sono chiare, quindi il testo e' in inchiostro (`GamePalette.Ink*`). Chiudi,
Esc e clic esterno chiudono; AP e selezione di gioco restano intatti.

## Generazione e fonti

`cabinet_clean.png` e `mushroom_clean.png` sono modifiche prodotte con lo strumento
integrato **image_gen**; prompt in `generation_prompt.json` e
`mushroom_generation_prompt.json`. Faccia del rullo e pezzi della leva vengono dagli
script in `Tools`, senza generazione. Sorgenti, composizioni e studi precedenti sono
in `2_Archivio/NeonMonte_Studi`.
