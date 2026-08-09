# FlipCards — Roadmap

Lavori aperti sul layout e sulla presentazione del gioco. Riferimenti:
[LAYOUT_SPEC.md](LAYOUT_SPEC.md) per la specifica di layout, [AGENTS.md](AGENTS.md)
per come operare sul progetto.

Ogni voce ha **Obiettivo**, **Perché**, **Dove**, **Fatto quando** e — dove serve —
**Trappole note**, cioè cose che sono già costate un giro di debug.

---

## Stato attuale

Il layout a bande della specifica è in piedi e ricostruibile da menu
(**FlipCards → Ricostruisci layout di gioco**, `Assets/Editor/FlipCardsLayoutBuilder.cs`).

**Chiuso finora** — campo 1344 + rail giocatore 96 + colonna destra 480; corsie
centrate; asse delle corsie con pronostico e connettori di combo; traccia della
`flipPattern`; contatore turno; etichetta di fase; barre HP; pallini AP;
ispettore; log con autoscroll; pannello di fine partita; mano che sale in blocco
(A1); HUD giocatore in colonna (A2); cella carta con artwork grande e statistiche
simboliche (A3); ispettore anche per le carte in mano (A4); mazzo cliccabile a
lato (B1); reel che si ferma sull'immagine dello slot che entra davvero (C1);
swap di fine turno animato (D1); animazioni specifiche di attacco, parata e
danno (D2); risoluzione a catena corsia per corsia (D3); ingresso degli slot uno
per volta con le abilità raccontate (D4).

**Tabellone rifatto sul kit Arcade Horror CRT** (E1): le misure vengono da
`layouts.board` del manifest del kit ×2 — rail 294 con dentro stato, mazzo e
legenda; campo 1178; colonna destra 400; caselle nemiche orizzontali 352×288 come
caselle di un rullo, con cassa e payline; carte 224×336; mano a ventaglio da 8
carte sovrapposte con pop-out all'hover. Il fondo `board_bg` del kit e gli
overlay CRT si montano da soli se il kit è importato.

Quello che segue è quel che resta, in ordine di quanto pesa sulla leggibilità.

---

## E — Kit Arcade Horror v2: dagli sprite alla logica di lettura

Il kit è passato alla **v2** (`ArcadeHorrorUI/README.md`, `changelog_v2` del
manifest): 191 sprite in `2x/`, tutti già importati e registrati in
`Assets/Resources/FlipCardsUiSkin.asset` (`UiSkin.Sprite(nome)`). Finora ne
usavamo tre — `board_bg` e i due overlay CRT — e tutto il resto del tabellone era
disegnato a tinte piatte. Le tre voci che seguono non sono "attaccare le
immagini": la v2 porta con sé **tre decisioni di lettura** che cambiano dove
stanno le informazioni.

### E2. La faccia della carta si vede, non si legge

**Obiettivo.** Sulla cella carta non compare più la parola FRONTE o RETRO. La
faccia si riconosce dal **template**: `card_front_{fazione}` con il ritratto nella
finestra, `card_back_{fazione}` con il sigillo. La fascia bassa che oggi scrive il
lato diventa la striscia dell'abilità (`ability_strip` del manifest).

**Perché.** È una carta: il lato è la cosa che si vede girando, non un'etichetta
da rileggere. Scriverlo costa la banda più preziosa della cella — quella bassa,
l'unica libera per l'abilità — per un dato che l'immagine già dà. Ed è anche
l'unico modo di far leggere il tavolo a colpo d'occhio: sei ritratti in alto e due
sigilli in basso si contano senza leggere niente.

**Le informazioni si dividono fra le due facce.** È la parte che conta, non lo
sprite: la cella ha **due** caselle statistica, non tre, e la prima cambia
significato con la faccia. Quello che non è utile *adesso* non sta sulla carta;
sta nell'ispettore, che è la superficie fatta per il dettaglio.

| dato | in mano | Fronte | Retro | ispettore |
|---|---|---|---|---|
| nome, fazione | sì | sì | sì | sì |
| ritratto | sì | sì | — (sigillo) | — |
| ATK (+ cariche) | sì | **sì** | no | sì |
| BLOCCO | no | no | **sì** | sì (tutti e due i lati) |
| HP | sì (massimo) | sì | sì | sì |
| cariche di flip | no | sì | sì | sì |
| abilità | icona + nome | icona + nome | icona + nome | testo completo |
| instabilità, passive | no | no | no | sì |

**Dove.** `Assets/Scripts/UI/CardOverlay.cs` (ancore di `layouts.card` ×2, badge
`badge_atk`/`badge_def`/`badge_hp`, `tag_faction_*`, `flip_cell_*`, sigillo
`decal_sigil_*`), `Assets/Scripts/Cards/CardView.cs` (`RefreshStatTexts`:
il numero della prima casella cambia con il lato, e l'altro Text si spegne),
`FlipCardsLayoutBuilder.ResizeCardPrefab` (assegna al `Template` del prefab lo
sprite di fronte del kit e il campo `backImage`, per fazione).

**Fatto quando.** Girando una carta cambiano template, ritratto→sigillo e il
numero della prima casella; da nessuna parte sulla cella compaiono le parole
FRONTE o RETRO.

**Trappole note.** Il `Template` è il `Graphic` su cui gira `CardShaderGraph`:
cambiargli lo sprite va bene, sostituire il componente no. `CardView.Init` legge
`frontImage` dallo sprite del Template al momento dell'Init, quindi il fronte va
scritto nel prefab e non a runtime.

### E3. I nemici sono rulli, non carte

**Obiettivo.** La casella nemica smette di presentarsi come una carta a due lati.
Non ha fronte e retro: ha uno **stato del rullo**, e una logica di interazione e
di attacco tutta sua.

- **Stato**: `reel_cell_{fazione}` quando la casella è **carica** (questo giro
  colpisce) e `reel_cell_locked` quando è **trattenuta** (questo giro non
  colpisce, para e basta). La colonna che sta per colpire si accende con
  `reel_col_highlight`: è il preavviso d'attacco, e non ha un equivalente sul
  lato del giocatore.
- **Programma**: i tre `reel_pip_*` in cima alla casella sono i giri futuri, con
  `reel_pip_current` sul giro in corso. Non è un "lato" da girare: è la sequenza
  che il rullo esegue da solo.
- **Interazione**: sul nemico non si flippa, non si trascina, non si scambia.
  L'unica azione è **guardarlo**: hover mostra la scheda, clic la **aggancia**
  nell'ispettore così si può leggere senza tenere il mouse fermo (chiude la voce
  "slot nemico cliccabile" della sezione C).
- **Vocabolario**: nell'interfaccia il nemico non dice mai FRONTE/RETRO ma
  **CARICO** / **TRATTENUTO**, e il pattern si chiama *programma del rullo*.
  Nel codice `SlotInstance.side` resta `Side`: è la stessa macchina, cambia come
  la si racconta.
- **Cassa**: `reel_backing` sotto, `reel_sliver_top/bottom` sopra e sotto ogni
  casella, poi `reel_frame` → `reel_payline` → `reel_glass` davanti a tutto, e
  `reel_col_blur` sulle colonne mentre il rullo gira.

**Perché.** Erano due carte grandi in cima allo schermo, quindi il giocatore
provava a ragionarci come sulle proprie: girarle, spostarle, contarci sopra il
blocco. Non si può fare nessuna delle tre. La forma da rullo e il preavviso di
colonna dicono la regola vera — *arriva da solo, tu puoi solo pararlo* — senza una
riga di tutorial.

**Dove.** `Assets/Scripts/UI/SlotOverlay.cs` (ancore di `layouts.reel_cell` ×2,
`micro_atk`/`micro_hp`/`micro_def`, pip, medaglione, clic che aggancia),
`Assets/Scripts/UI/ReelChrome.cs` (nuovo: cassa, vetro, payline, evidenziazione
di colonna, blur mentre `SlotBatchManager.IsRolling`), `InspectorPanel.ShowSlot`
(vocabolario del rullo), `FlipCardsLayoutBuilder.BuildEnemyLanes`.

**Fatto quando.** Guardando il fronte nemico si vede quale colonna colpirà questo
turno prima di leggere qualunque numero; da nessuna parte compaiono le parole
FRONTE o RETRO riferite a un nemico; il clic su una casella tiene la scheda
nell'ispettore.

**Trappole note.** Il vetro e la cornice vanno creati **dopo** `AIBoardRoot` e
sempre senza Raycast Target: le caselle sono opache e li coprirebbero, e un
Raycast Target sopra il campo rompe hover e swap. `_ReelOverlayLayer` di
`SlotBatchManager` nasce come fratello di `AIBoardRoot`: la cassa deve stargli
sotto e il vetro sopra, o il reel di fine turno scorre davanti alla cornice.

### E4. Il resto del tabellone prende la pelle del kit

**Obiettivo.** Barre (`bar_frame_*` + `bar_fill_*`), bottoni (`btn_{tono}_{stato}`
a quattro stati disegnati), pannelli (`panel_*`, `plate_counter`), banner di turno
e fase, caselle vuote (`card_slot_empty`, `enemy_slot_empty`), segmenti AP
(`ap_seg_*`), pila del mazzo (`deck_stack_*` + `deck_pulse`), binario della mano
(`hand_dock_low`), frecce del pronostico (`readout_up`/`readout_block`).

**Perché.** Gli helper per farlo esistono già e sono inutilizzati: `UiBuild.Bar`
accetta un `kind`, `UiBuild.Command` un `tone`, `DeckView` uno `stackImage`, e il
builder non passa nessuno dei tre. Finché non li passa, il kit è importato ma non
si vede: il tabellone resta a rettangoli tinti.

**Dove.** `FlipCardsLayoutBuilder` (quasi tutto), `HudController.BuildPips`,
`LaneAxisView.CreateColumn`.

**Fatto quando.** Il tabellone in Play somiglia a `preview_board@2x.png` del kit,
e spegnendo la skin (`UiSkin` assente) resta esattamente il layout di prima a
tinte piatte.

---

## A — Cambiamenti che il giocatore subisce

### A5. I cambiamenti di fine turno si vedono, non solo si leggono

**Obiettivo.** Le tre cose che `OnEndTurn` fa addosso al giocatore — flip caotico,
accumulo cariche, avanzamento dei pattern nemici — devono avere una resa visiva e
un ritmo, come ce l'ha ora la risoluzione dell'attacco (D3).

**Perché.** Sono ancora tre righe di log identiche a tutte le altre, eseguite in
un frame. Il flip caotico ha già l'animazione di `FlipSide`, ma parte insieme a
tutto il resto e passa inosservata; cariche e pattern non hanno nessuna resa.
È l'ultima voce rimasta della tabella "Cosa manca oggi" di LAYOUT_SPEC §8.

**Dove.**
- `Assets/Scripts/Managers/GameManager.cs` → `OnEndTurn`: `RandomizePlayerBoard`,
  `AccumulateFlipCharges` e `AdvanceSlotPatterns` vanno in una coroutine
  temporizzata, come `ResolveAttackRoutine`. I campi di ritmo esistono già
  (`resolveLaneDelay` e compagnia): serve l'equivalente per il fine turno.
- `Assets/Scripts/UI/CardOverlay.cs` per l'animazione della traccia cariche.
- `Assets/Scripts/UI/SlotOverlay.cs` → `RefreshPattern`: il passo che avanza
  dovrebbe scorrere, non saltare.

**Fatto quando.** Chiudendo il turno si vede, in ordine: la carta che si gira da
sola, le tacche di carica che si riempiono, l'indice del pattern che scorre di un
passo — ciascuna con la sua riga di log nel momento in cui succede.

**Trappole note.** `OnEndTurn` finisce chiamando `slotBatchManager.RollNewSlots`,
che è già asincrono e riabilita l'input dal suo `onComplete` (oggi via
`EnterEnemySlotsRoutine`). La coroutine nuova deve stare **prima** del roll e
lasciare `inputLocked = true` per tutta la sua durata, altrimenti si può
attaccare mentre le cariche salgono.

---

## B — Residui da verificare

### B2. Aspetto della casella dopo una morte

**Obiettivo.** Quando uno slot nemico muore, la casella che lo sostituisce deve
leggersi come le caselle vuote del giocatore: interno quasi trasparente e cornice.

**Perché.** In un test di risoluzione la corsia nemica di uno slot ucciso si è
resa come un rettangolo grigio pieno, senza cornice. I due prefab però sono
identici e correttamente stilati (`EmptySlot` ed `EmptySpot`: root a
`RGBA(1,1,1,0.045)` più quattro strisce di bordo), quindi **la causa non è nel
prefab** e resta da trovare — probabile un residuo a schermo, non la casella.

**Dove.** `GameManager.RemoveSlotView`, `FlipCardsLayoutBuilder.ApplyPlaceholder`,
`SlotBatchManager.DestroyCells`.

**Fatto quando.** Uccidendo uno slot la corsia mostra la stessa casella vuota
delle corsie del giocatore.

**Trappole note.** Una cattura fatta subito dopo un `RunCommand` è indietro di un
frame e i `Destroy` differiti non sono ancora applicati: **ricatturare prima di
indagare**. Metà dei sospetti di questa voce potrebbero essere questo.

---

## C — Idee non ancora specificate

Nessuna di queste è decisa: sono i buchi rimasti fra il codice e la specifica.

- **Slot nemico cliccabile.** Ha un `Button` senza listener e si ispeziona solo
  passandoci sopra (LAYOUT_SPEC §4). Un clic che "aggancia" l'ispettore renderebbe
  leggibile il tavolo senza tenere il mouse fermo.
- **Bersaglio di flip esplicito.** Il flip è un doppio clic entro 0.3 s e nessun
  elemento lo suggerisce (LAYOUT_SPEC §4, sequenza non scopribile numero 2).
- **Hint sopra il bordo della cella.** Oggi galleggia sopra l'artwork invece che
  sopra il bordo superiore come dice LAYOUT_SPEC §6.5: fuori dalla cella finiva
  addosso all'asse delle corsie. Con le animazioni di D2 il testo è meno
  necessario di prima e il problema si può anche risolvere togliendo, non
  spostando.

---

## Debito noto e decisioni prese

- **`maxHandSize` e il passo della mano li detta il layout, non il bilanciamento.**
  Erano legati da `spacing = handRoot.width / maxHandSize`, che produceva per
  forza un passo più largo della carta: una fila staccata. Il kit vuole il
  contrario — 8 linguette da 132 contro carte da 224, cioè carte che si coprono a
  metà — quindi il passo è ora un campo esplicito (`handSpacing`) e la
  leggibilità la danno l'arco della spline, la rotazione a ventaglio e il pop-out
  della carta sotto il puntatore. Li scrive
  `FlipCardsLayoutBuilder.WireHandManager`: cambiarli a mano nell'Inspector viene
  sovrascritto al prossimo rebuild.
- **Il kit va importato una volta.** `Tools → FlipCards → Import UI Kit` applica
  filtro Point, niente compressione e bordi 9-slice. Finché non lo si lancia gli
  sprite del kit restano bilineari: il fondo `board_bg` e gli overlay CRT si
  vedono lo stesso (sono 1920×1080 a 1:1), i 9-slice no.
- **Il mazzo è mescolato una volta sola e si pesca dalla cima.** Serviva a B1:
  con l'estrazione casuale a ogni pesca "la prossima carta" non esisteva e la
  pila non poteva mostrarla. La sequenza di pesca è quindi riproducibile a parità
  di `GameManager.seed`.
- **`GameManager.hpText` / `apText` / `EnemyHptxt` sono lasciati a null**: la HUD
  la scrive `HudController`. Se si rimuove `HudController` la HUD sparisce senza
  errori.
- **Il ritmo della risoluzione è esposto nell'Inspector** (`resolveOpeningDelay`,
  `resolveLaneDelay`, `resolveLaneGap`, `slotEnterDelay`). Sono numeri di regia,
  non di bilanciamento: alzarli non cambia nessun esito, allunga solo la lettura.
- **La carta in mano non ha un lato, e non è un difetto.** `CardOverlay` la
  gestisce già (nome, badge di classe e fazione, statistiche sottolineate) e
  scrive `IN MANO` al posto della fascia di lato, perché il lato lo decide
  `CardInstance` quando la carta entra in campo — con un tiro a testa o croce.
  Se un giorno il lato d'ingresso diventa una scelta del giocatore, è lì che va
  messo.
