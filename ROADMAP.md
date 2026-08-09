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
