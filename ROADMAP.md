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

Già presenti: campo 1440 + colonna destra 480, corsie centrate a x 432/720/1008,
asse delle corsie con pronostico e connettori di combo, traccia della `flipPattern`,
contatore turno, etichetta di fase, barre HP, pallini AP, contatori mazzo/mano,
ispettore, log con autoscroll, pannello di fine partita.

Quello che segue è il passo successivo: **recuperare spazio, ridurre il rumore
informativo e dare peso temporale agli eventi**.

---

## A — Spazio e leggibilità

### A1. Mano che sale e scende tutta insieme

**Obiettivo.** La mano vive quasi fuori dallo schermo. Quando il puntatore entra
nell'area della mano, l'intera mano sale come un blocco unico e diventa
pienamente visibile e cliccabile; quando il puntatore esce dall'area, torna giù.
Da alzata la mano **copre parzialmente le corsie del giocatore**: è il momento in
cui stai scegliendo cosa giocare, non in cui stai leggendo il tavolo.

**Perché.** Oggi ci sono troppi elementi contemporaneamente nello spazio
verticale. La mano è l'unica zona che serve a intermittenza, quindi è l'unica che
può permettersi di stare nascosta.

**Dove.**
- `Assets/Editor/FlipCardsLayoutBuilder.cs` → `BuildHandZone`: serve una zona di
  attivazione che copra la fascia bassa e l'altezza a cui la mano arriva da alzata.
- Nuovo componente, es. `Assets/Scripts/UI/HandTray.cs`, con
  `IPointerEnterHandler` / `IPointerExitHandler` che anima `handRoot.anchoredPosition`
  con DOTween fra due quote (`restY` / `raisedY`).
- `Assets/Scripts/Cards/CardView.cs` → `handHoverLift` va **rimosso o azzerato**:
  con la mano che sale in blocco, il sollevamento della singola carta duplica
  l'effetto e sfalsa il rect su cui si fa hover.

**Fatto quando.** Il puntatore entra dal basso → la mano sale con un tween unico
(≈0.18 s, `Ease.OutCubic`) e resta su finché il puntatore è dentro l'area; esce →
scende. Nessun tremolio quando si passa da una carta all'altra dentro la mano.

**Trappole note.**
- L'uscita del puntatore va valutata sull'**area della mano**, non sulle singole
  carte, altrimenti passare fra due carte genera un `PointerExit` e la mano
  scende. Serve un solo rect di attivazione che contiene tutto.
- `HandManager.Update()` riscrive `container.localPosition` ogni frame: animare
  `handRoot`, mai i container.
- Una zona di attivazione con `Raycast Target` **sopra l'area di gioco** viola il
  vincolo 8 di LAYOUT_SPEC e romperebbe drag-and-drop e swap. Va tenuta confinata
  alla fascia bassa, oppure l'ingresso va rilevato con un test di posizione
  (`RectTransformUtility.RectangleContainsScreenPoint`) invece che con un raycast.
  Da riverificare che trascinare una carta dalla mano a una casella funzioni ancora.

### A2. HUD del giocatore verticale, a lato

**Obiettivo.** Barra HP, pallini AP e contatori del giocatore diventano verticali
e si spostano su una colonna laterale, liberando la banda orizzontale
`PlayerBand` (52 px) e parte della fascia mano.

**Perché.** Recuperare altezza per le corsie e per la mano alzata di A1.

**Dove.** `FlipCardsLayoutBuilder.BuildPlayerBand` (da riscrivere come colonna),
`Assets/Scripts/UI/HudController.cs` (`playerHpBar`, `apPipsRoot`, `BuildPips`),
`Assets/Scripts/UI/UiBar.cs` (serve il riempimento anche in verticale: oggi
`UiBar` muove solo `anchorMax.x`).

**Fatto quando.** La banda orizzontale del giocatore non esiste più, il campo
guadagna almeno ~50 px di altezza, e HP/AP restano leggibili a colpo d'occhio.

### A3. Cella carta: artwork grande, informazioni simboliche

**Obiettivo.** L'immagine torna a occupare tutto il "buco" trasparente centrale
del template. Le statistiche sulla cella si riducono all'essenziale e diventano
**simboliche** (icona + numero, niente etichette testuali); tutto il resto —
passive, abilità, valori per lato — vive nell'ispettore.

**Perché.** La cella attuale accumula badge classe, badge fazione, tre chip, tre
icone abilità, traccia cariche e fascia di lato: è densa, si legge male, e per
farceli stare l'artwork è stato compresso a 220×158.

**Dove.**
- `FlipCardsLayoutBuilder.ResizeCardPrefab` → blocco `Place(cell, ...)`: è lì che
  sono definite le bande della cella.
- `Assets/Scripts/UI/CardOverlay.cs` → costanti in testa (`NameY`, `BadgeY`,
  `ChipY`, `ChargeY`, `AbilityY`, `SideBandY`) e `Build()`.
- `Assets/Scripts/UI/InspectorPanel.cs` → `ShowCard`, dove spostare il dettaglio.

**Fatto quando.** L'artwork riempie la finestra del template come prima della
modifica; sulla cella restano lato, cariche, classe e non più di tre valori
numerici; passando il puntatore l'ispettore mostra tutto il resto.

**Trappole note.** I `Text` legacy del prefab sono scritti da `CardView` e non si
possono rimuovere: `sideText` in particolare è usato da `CardView.Refresh` per
riconoscere il flip. Per nasconderli si usa `FlipCardsLayoutBuilder.Hide`.

### A4. Carta in mano leggibile + ispettore

**Obiettivo.** Una carta in mano mostra le info base (nome, **vita**, ATK, BLOCK,
lato, classe). Passandoci sopra col puntatore, l'ispettore mostra la scheda
completa come per le carte in campo.

**Perché.** Oggi la carta in mano non ha `CardInstance`, quindi `CardOverlay` non
si costruisce e `InspectorPanel` non viene invocato: si vedono solo artwork e nome.

**Dove.**
- `Assets/Scripts/UI/CardOverlay.cs`: `LateUpdate` esce se `_view.instance == null`.
  Serve un percorso "anteprima" che legga la `Spec` da `CardDefinition.BuildSpec()`
  quando l'istanza non c'è ancora.
- `Assets/Scripts/UI/InspectorPanel.cs`: aggiungere `ShowCardPreview(CardDefinition)`
  che non dipende da `CardInstance`.
- `Assets/Scripts/Cards/CardDefinition.cs` → `OnPointerEnter`: oggi chiama
  l'ispettore solo se `cardView.instance != null`.
- `Assets/Scripts/Cards/CardView.cs` → `Awake` fa già una preview dei testi quando
  non c'è istanza: è il punto giusto da estendere.

**Fatto quando.** Con la mano alzata, ogni carta mostra vita e statistiche, e
l'hover riempie l'ispettore.

---

## B — Mazzo

### B1. Mazzo cliccabile a lato

**Obiettivo.** Sostituire il bottone PESCA con un **mazzo fisico** nella colonna
laterale: costruito con i prefab carta veri, tutti in posizione Retro, con
**spessore proporzionale alle carte rimaste**. Cliccandolo si pesca. Il retro
della prossima carta è visibile, così il giocatore sa cosa sta per arrivare.

**Perché.** Il bottone PESCA non comunica quante carte restano né cosa esce, e
`DrawCard` esce in silenzio a mazzo vuoto o mano piena.

**Dove.**
- Nuovo `Assets/Scripts/UI/DeckView.cs`: impila istanze del prefab carta con un
  piccolo offset, aggiorna la pila quando `HandManager.DeckCount` cambia, e su
  click chiama `HandManager.DrawCard()`.
- `Assets/Scripts/Managers/HandManager.cs`: `deck` è privato e inizializzato
  pigramente in `DrawCard`, e la pesca è casuale (`Random.Range(0, deck.Count)`).
  Per mostrare "la prossima carta" serve rendere l'ordine deterministico
  (mescolare una volta e pescare dalla cima). `DeckCount` e
  `CountDeckFromBindings()` esistono già.
- `FlipCardsLayoutBuilder.BuildCommands`: togliere `BtnDraw`. Attenzione:
  `WireHandManager` lo assegna ancora, e `btnDraw` è usato da
  `GameManager.UpdateHUD` e `SetButtonsInteractable` — vanno aggiornati entrambi
  o diventano `NullReferenceException`.

**Fatto quando.** Il mazzo si vede a lato, si assottiglia man mano, il click
pesca, e a mazzo vuoto o mano piena lo comunica invece di non fare nulla.

**Trappole note.** Istanziare dieci prefab carta completi solo per la pila è
costoso, e ognuno porta con sé `CardDefinition` (che gestisce l'input) e un
`Canvas` annidato. Meglio istanziarne pochi (3–5 visibili) e disattivare i
componenti di input sulle copie decorative.

---

## C — Slot nemici

### C1. Lo slot rivelato dal reel deve essere quello che entra in campo

**Obiettivo.** L'immagine su cui il rullo si ferma è esattamente l'artwork dello
slot che compare.

**Perché — causa accertata.** Il reel disegna un frame preso da uno sprite sheet
statico (`slotperroll`, 144×1584, 11 righe) indicizzato da
`SlotDefinition.reelFrameIndex`. Gli indici sono coerenti (Slot 0→0 … Slot 9→9),
ma **l'artwork dei prefab non corrisponde alle righe dello sheet**: ispezionando i
prefab, il figlio `Sprite` di otto slot su dieci punta allo stesso asset
(`2 candle big size_0`), differenziato a runtime dal colore/materiale. Sheet e
prefab sono due sorgenti d'immagine scollegate, quindi il frame finale non può
coincidere con quello che entra in campo.

**Dove.** `Assets/Scripts/Managers/SlotBatchManager.cs` — `CreateCell`,
`AnimateCell`, `FrameAspect`, i campi `reelSpriteSheet` / `reelFrameCount`, e
`SlotDefinition.reelFrameIndex`.

**Approccio consigliato.** Togliere lo sprite sheet e costruire il rullo dagli
**sprite dei prefab candidati** (`prefab.transform.Find("Sprite")`, copiando
`sprite`, `color` e `material`): il rullo scorre fra le immagini reali del batch e
si ferma su quella scelta. Sparisce l'indice da tenere sincronizzato, che è
l'origine del disallineamento. In alternativa, allineare a mano i dieci `Sprite`
dei prefab alle righe dello sheet — più fragile.

**Fatto quando.** Su dieci roll consecutivi l'immagine al settle coincide sempre
con l'artwork rivelato, senza scatti di dimensione al reveal.

---

## D — Animazioni e ritmo degli eventi

Tutte con DOTween, coerenti con quelle già in `CardView` (flip, hover, punch,
`SetUpdate(true)`, `SetLink(gameObject)`).

### D1. Lo swap di fine turno si vede

**Obiettivo.** Quando il caos di fine turno scambia due corsie, le carte si
muovono con la stessa animazione dello swap per trascinamento.

**Perché.** Oggi `RandomizePlayerBoard` fa `SetSiblingIndex` e le carte
teletrasportano: il giocatore subisce il cambiamento senza vederlo.

**Dove.** `Assets/Scripts/Managers/GameManager.cs` → `RandomizePlayerBoard`
(blocco dello swap). `SwapCardPositions` fa già la cosa giusta — calcola le
posizioni e chiama `CardView.UpdateBoardContainerTarget`, che tweena il
container: va riusato lo stesso percorso.

**Fatto quando.** Lo scambio di fine turno è indistinguibile, come animazione,
dallo scambio fatto a mano.

### D2. Attacco, parata e colpo subito hanno una loro animazione

**Obiettivo.** Ogni carta e ogni slot reagisce visivamente all'evento che lo
riguarda, con animazione **specifica per l'azione** più un breve scarto di colore
in trasparenza:
- attacca → scatto in avanti verso il bersaglio
- para / blocca → piccolo rimbalzo all'indietro, tinta del lato
- subisce danno → scossa e lampo rosso

**Perché.** Oggi c'è solo `Blink()`, identico per tutto, più un hint testuale.

**Dove.** `Assets/Scripts/Cards/CardDefinition.cs` → `OnGameEvent`;
`Assets/Scripts/Slots/SlotView.cs` → `OnGameEvent` e `Blink`. Gli eventi ci sono
già: `AttackDeclared` e `AttackResolved` con `ctx.source` / `ctx.target` / `ctx.amount`.

**Fatto quando.** Guardando una risoluzione senza leggere il log si capisce chi
ha colpito, chi ha parato e chi ha incassato.

### D3. La risoluzione avviene a catena, non tutta insieme

**Obiettivo.** `OnAttack` risolve una corsia per volta, con una pausa breve fra
una e l'altra. I numeri (HP, ATK, BLOCK) cambiano con un'animazione, e la riga di
log corrispondente **compare nello stesso momento** dell'animazione.

**Perché.** Oggi `OnAttack` risolve tutte le corsie in un frame e poi chiama
`UpdateAllViews()`: il giocatore vede lo stato finale e deve ricostruire la
catena leggendo il log.

**Dove.** `Assets/Scripts/Managers/GameManager.cs` → `OnAttack`: il ciclo su
`LaneResolver.Resolve` va trasformato in coroutine con `inputLocked = true` per
tutta la durata. `Logger.Info` scrive già in `AppendLog`, quindi basta che le
chiamate cadano dentro la sequenza temporizzata.

**Fatto quando.** La risoluzione dura ~1–2 s, si segue a occhio corsia per corsia,
e ogni riga di log appare mentre succede la cosa che descrive.

**Trappole note.** `awaitingEndTurn = true` e la riabilitazione dei bottoni vanno
spostati alla fine della coroutine, altrimenti si può attaccare due volte o
chiudere il turno a metà risoluzione. `HudController` legge lo stato in polling,
quindi l'etichetta di fase si aggiorna da sola.

### D4. L'ingresso degli slot nemici mostra le abilità che si applicano

**Obiettivo.** Dopo il reel gli slot entrano **uno per volta**, e si vedono le
animazioni delle abilità che ne modificano le statistiche (armatura, furia,
rigenerazione, buff agli adiacenti), con il log sincronizzato.

**Perché.** Oggi gli slot compaiono già con i valori finali e le abilità che li
hanno prodotti sono invisibili.

**Dove.** `Assets/Scripts/Managers/GameManager.cs` → `RespawnEnemySlotsFromList`
e la callback `onComplete` di `SlotBatchManager.RollNewSlots`;
`Assets/Scripts/UI/SlotOverlay.cs` per l'animazione dei valori.

**Fatto quando.** Dopo il rullo si vede una sequenza leggibile di slot che entrano
e di statistiche che si assestano, con le righe di log in tempo.

---

## Ordine consigliato

1. **C1** (reel) — bug funzionale, indipendente da tutto il resto.
2. **A1 + A2** — insieme: liberano lo spazio su cui poggiano A3 e B1.
3. **A3 + A4** — richiedono lo spazio di A1/A2.
4. **B1** — tocca `HandManager` e i comandi, meglio dopo che il layout è stabile.
5. **D1 → D2 → D3 → D4** — dalla più isolata alla più invasiva. D3 cambia il
   flusso di `OnAttack` ed è la più delicata.

---

## Debito noto e decisioni aperte

- **`maxHandSize` è 8, LAYOUT_SPEC assume 5.** La regola di non sovrapposizione
  (`handRoot ≥ maxHandSize × larghezza carta`) chiederebbe 1760 px, che non stanno
  nel campo da 1440. Oggi `handRoot` è 1400 (passo 175 contro carte da 220): a
  mano piena le carte si sovrappongono di ~45 px. Con A1 la mano alzata può
  occupare più larghezza, oppure si porta `maxHandSize` a 5.
- **Hint della carta.** Sta sopra l'artwork e non sopra il bordo superiore come
  dice LAYOUT_SPEC §6.5: fuori dalla cella finiva addosso all'asse delle corsie.
  Da rivedere insieme ad A3.
- **`GameManager.hpText` / `apText` / `EnemyHptxt` sono lasciati a null**: la HUD
  la scrive `HudController`. Se si rimuove `HudController` la HUD sparisce senza
  errori.
