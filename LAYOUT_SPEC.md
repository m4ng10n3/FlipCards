# FlipCards — descrizione del gioco e specifica di layout

Documento di riferimento per costruire il layout di gioco. Tutto quello che segue è
ricavato dal codice in `Assets/Scripts`.

Le sezioni 1–5 descrivono il gioco e non cambiano con il layout. Le sezioni 6–8
descrivono il layout **come è costruito oggi** da
`Assets/Editor/FlipCardsLayoutBuilder.cs`: se i numeri qui e le costanti in testa
a quel file divergono, il file ha ragione e questo documento va corretto.

Indice:
1. [Il gioco](#1-il-gioco)
2. [Entità e dati](#2-entità-e-dati)
3. [Logica di gioco](#3-logica-di-gioco)
4. [Interazioni](#4-interazioni)
5. [Stati dell'interfaccia](#5-stati-dellinterfaccia)
6. [Layout proposto](#6-layout-proposto)
7. [Vincoli imposti dal codice](#7-vincoli-imposti-dal-codice)
8. [Cosa manca oggi](#8-cosa-manca-oggi)

---

## 1. Il gioco

**FlipCards** è un duello a corsie, giocatore singolo contro un boss gestito da pattern
deterministici. Dura `turns = 12` turni.

Il tavolo è diviso in **N corsie parallele** (`CardsPerSide`, default 3). Ogni corsia ha
due caselle affacciate: una **carta** del giocatore in basso, uno **slot** nemico in alto.
Il combattimento si risolve corsia per corsia, in ordine di indice, quando il giocatore
preme ATTACCA.

Il meccanismo centrale è il **lato**. Carte e slot hanno due facce:

| | `Fronte` | `Retro` |
|---|---|---|
| **Carta** | attacca (`frontDamage`), blocca poco (`frontBlockValue`) | non attacca, blocca molto (`backBlockValue`), **accumula 1 carica a fine turno** |
| **Slot** | attacca (`atkDamage`), blocca `blockFront` | non attacca, blocca `blockRetro`, attiva effetti passivi retro |

Il giocatore **sceglie** i lati spendendo AP. Lo slot nemico **non sceglie**: segue un
`flipPattern` fisso che avanza di un passo a ogni fine turno. Il pattern è quindi
leggibile e prevedibile — è l'informazione su cui si costruisce la strategia, ed è
esattamente quella che oggi non viene mostrata da nessuna parte.

**Il loop di caricamento** è il cuore del gioco: una carta in Retro accumula `flipCharge`
(max 3), che vengono spese tutte insieme come danno bonus al primo attacco in Fronte.
Stare in difesa non è tempo perso: è il caricamento.

**A fine turno tutti gli slot nemici vengono sostituiti** con nuovi estratti a caso dal
pool (`SlotBatchManager.batch`), con un'animazione da slot machine. Il fronte nemico è
quindi effimero: dura un turno. Il layout deve comunicarlo, altrimenti il giocatore
pianifica su qualcosa che non esisterà più.

**Fine partita:** `player.hp <= 0`, `ai.hp <= 0`, oppure `currentTurn >= turns`. Il
risultato confronta gli HP residui: *Player ahead / Boss ahead / Tie*.

---

## 2. Entità e dati

Inventario completo dei dati esistenti. La colonna **Oggi** è la fotografia di
*prima* del layout a bande — la tengo perché spiega il perché di ogni scelta
della colonna **Serve**, che è quella realizzata. Per lo stato attuale di ogni
voce vedi §8.

### 2.1 Carta del giocatore — `CardDefinition.Spec` + `CardInstance`

| Campo | Tipo | Significato | Oggi | Serve |
|---|---|---|---|---|
| `cardName` | string | nome | `nameText` | sì, barra nome |
| `faction` | `A/B/C` | fazione, guida i bonus retro di fazione | `factionText` | badge d'angolo, colore |
| `cardClass` | `Assalto/Tecnico/Mistico/Guardia` | guida le combo di adiacenza | **no** | badge — è la chiave delle combo |
| `maxHealth` / `health` | int | vita | `hpText` `"h/max"` | barra + numero |
| `frontDamage` | int | attacco in Fronte | `AttackPwrText` | chip ATK |
| `frontBlockValue` | int | blocco in Fronte | `BlockPwrText` | chip BLOCK |
| `backBlockValue` | int | blocco in Retro | stesso chip, cambia col lato | chip BLOCK |
| `backDamageBonusSameFaction` | int | in Retro: +ATK alle carte Fronte della stessa fazione | **no** | riga passiva |
| `backBlockBonusSameFaction` | int | in Retro: +BLOCK alle carte stessa fazione | **no** | riga passiva |
| `backBonusPAIfTwoRetroSameFaction` | int | +AP se due Retro stessa fazione | **no** | riga passiva |
| `endTurnFlipChance` | 0–1 | probabilità di girarsi da sola a fine turno | **no** | indicatore "instabilità" |
| `side` | `Fronte/Retro` | lato corrente | `sideText` + sprite | fascia di lato a tutta larghezza |
| `flipCharge` | 0–3 | cariche accumulate | testo `"2+3"` / `"[2/3]"` | traccia a 3 tacche |
| `tempAtkBonus` / `tempBlockBonus` | int | bonus del turno da combo e abilità | solo via hint | delta sul chip (`5` → `5 +1`) |
| `incomingDamageOverride` | int? | annulla il danno in arrivo | solo hint | icona scudo pieno |
| abilità (componenti) | — | vedi §2.4 | **no** | icone + dettaglio nell'ispettore |

### 2.2 Slot nemico — `SlotDefinition.Spec` + `SlotInstance`

| Campo | Tipo | Significato | Oggi | Serve |
|---|---|---|---|---|
| `SlotName` | string | nome | `nameText` | barra nome |
| `faction` | `A/B/C` | fazione | **no** | badge |
| `maxHealth` / `health` | int | vita | `hpText` `"h/max"` | barra + numero |
| `atkDamage` | int | danno in Fronte | solo hint durante l'attacco | chip ATK **permanente** |
| `blockFront` / `blockRetro` | int | blocco per lato | `defText` = `"DEF n"` calcolato | chip DEF |
| `flipPattern` | `Side[]` | sequenza dei lati, avanza a ogni fine turno | **no** | **traccia pattern con indice corrente** |
| `side` | `Fronte/Retro` | lato corrente | implicito nel DEF | fascia di lato |
| `reelFrameIndex` | int | riga nello sprite sheet del reel | — | solo animazione |
| abilità (componenti) | — | vedi §2.4 | **no** | icone + ispettore |

La `flipPattern` è la voce più importante di tutta la tabella: senza di essa il giocatore
non può decidere se conviene flippare adesso o al turno dopo, e il gioco diventa casuale.

### 2.3 Stato globale — `PlayerState`, `GameManager`

| Dato | Fonte | Oggi | Serve |
|---|---|---|---|
| HP giocatore | `player.hp` / `player.maxHp` (20) | `hpText`, numero nudo | barra + `hp/maxHp` |
| HP boss | `ai.hp` / `ai.maxHp` (24) | `EnemyHptxt`, numero nudo | barra + `hp/maxHp` |
| AP | `player.actionPoints` / `MaxPlayerAP` (5) | `"ap/playerBaseAP"` → **stampa 5/4** | pallini spendibili, `ap/5` |
| Turno | `currentTurn` / `turns` (12) | solo nel log | contatore in alto |
| Mazzo residuo | `HandManager.deck.Count` | **no** | contatore |
| Mano | `handCards.Count` / `maxHandSize` (8) | **no** | contatore |
| Fase | `playerPhase`, `awaitingEndTurn`, `inputLocked`, `matchEnded` | solo bottoni grigi | etichetta di fase esplicita |
| Log | `_logBuf` | `logText` | pannello con autoscroll e tetto righe |

### 2.4 Abilità

Componenti `AbilityBase` montati sul prefab. Nessuna è visibile in interfaccia: si
manifestano solo come hint di una riga sulla carta e come riga di log.

**Carta**

| Abilità | Effetto |
|---|---|
| `VanguardStrike` | +danno in Fronte |
| `ChargeBoost` | oltre `chargeThreshold` cariche: +danno; a 3 cariche splash su tutti gli slot |
| `ClassSynergyBoost` | +danno se una carta adiacente è della stessa classe |
| `AdjacencyShield` | in Fronte: passa +block a una carta adiacente sotto attacco |
| `RetroShield` | in Retro: +block quando viene attaccata |
| `BlockAllAttacks` | azzera il danno in arrivo |
| `PulseHeal` | cura a ogni flip |
| `OnFlipDealDamage` | al flip colpisce lo slot della sua corsia (o il boss) |
| `OnFlipGainAP` | al flip restituisce AP |
| `OnEndTurnDealDamage` | a fine turno attacca |
| `GetBonusBack` | in Retro applica i bonus di fazione alle altre carte |

**Slot**

| Abilità | Effetto |
|---|---|
| `SlotArmorFront` | in Fronte riduce il danno in arrivo |
| `SlotBerserker` | accumula furia ogni turno in Fronte; a `furyThreshold` colpisce doppio |
| `SlotRetroRegen` | rigenera HP passando in Retro |
| `SlotAdjacentBuff` | in Retro dà +block agli slot adiacenti |
| `SlotStrikeOnAct` | firma parametrica che replica i comportamenti sopra |

`SlotBerserker` accumula uno stato interno (`_furyStacks`) che il giocatore **deve** poter
vedere: è progettata per essere prevedibile, ma senza contatore a schermo non lo è.

---

## 3. Logica di gioco

### 3.1 Economia

| Voce | Valore | Campo |
|---|---|---|
| AP per turno | 4 | `playerBaseAP` |
| AP massimi | 5 | `MaxPlayerAP = playerBaseAP + maxBonusAP` |
| Pescare | 1 AP | `drawCardCost` |
| Giocare una carta | 1 AP | `playCardCost` |
| Girare una carta | 1 AP | `flipCardCost` |
| Scambiare due corsie | 1 AP | `swapCardCost` |
| Attaccare | **0 AP** | — |
| Chiudere il turno | **0 AP** | — |

Attaccare è gratis ma **si può fare una sola volta**: dopo `OnAttack` si entra in
`awaitingEndTurn` e restano solo le azioni a costo zero. Il layout deve rendere evidente
che ATTACCA è irreversibile e chiude la fase di azione.

Mano massima 8 carte (`maxHandSize`, misura di layout: vedi §7.7). Il mazzo è costruito una sola volta da
`playerCards` meno le carte già in campo; a mazzo vuoto `DrawCard` esce in silenzio.

### 3.2 Sequenza del turno

**Fase azioni** — libera, finché ci sono AP: pesca, gioca, gira, scambia.

**ATTACCA** (`GameManager.OnAttack`), in quest'ordine:

1. `ResetCombatModifiers()` — azzera i bonus temporanei di carte e slot
2. evento `Custom / "PrepareBattle"` — le abilità applicano i bonus del turno
   (`ClassSynergyBoost`, `GetBonusBack`)
3. `SynergyResolver.Resolve()` — combo di adiacenza (§3.4)
4. per ogni corsia `0 … max(childCount)`: `LaneResolver.Resolve()` (§3.3)
5. `CleanupDestroyedSlots()`, `UpdateAllViews()`
6. `awaitingEndTurn = true` → ATTACCA e PESCA si disabilitano

**CHIUDI TURNO** (`GameManager.OnEndTurn`), in quest'ordine:

1. `RandomizePlayerBoard()` — **caos**: ogni carta tira contro il proprio
   `endTurnFlipChance`; tra le carte estratte, fino a `maxChaosFlipsPerTurn` (1) si girano
   con probabilità `chaosFlipChance` (0.45); poi con `chaosSwapChance` (0.30) due corsie
   adiacenti si scambiano
2. `AccumulateFlipCharges()` — ogni carta in Retro guadagna 1 carica (max 3)
3. `AdvanceSlotPatterns()` — ogni slot avanza di un passo la sua `flipPattern`
4. evento `TurnEnd` — abilità di fine turno (`OnEndTurnDealDamage`)
5. controllo fine partita → `EndMatch()`
6. `currentTurn++`
7. **reel**: le corsie nemiche vengono coperte, gli slot sostituiti con nuovi estratti dal
   pool, poi rivelati. `inputLocked = true` per tutta la durata
8. `StartTurn()` — AP a 4, evento `TurnStart`

I passi 1–3 sono cambiamenti di stato che il giocatore **subisce** e deve poter leggere:
il caos gli gira una carta sotto il naso, le cariche salgono, i pattern nemici avanzano.
Oggi sono tre righe di log identiche a tutte le altre.

### 3.3 Risoluzione di una corsia — `LaneResolver.Resolve`

```
carta presente E slot presente
├─ carta in Fronte
│    └─ la carta attacca lo slot
│       ├─ slot ucciso → il boss subisce bossDamageOnSlotBreak (1)
│       └─ slot vivo e in Fronte → contrattacca la carta
├─ carta in Retro E slot in Fronte
│    └─ lo slot attacca la carta
└─ slot vivo e in Retro → effetto passivo retro dello slot

solo carta (corsia nemica vuota)
└─ carta in Fronte → danno diretto agli HP del boss

solo slot (corsia giocatore vuota)
├─ slot in Fronte → danno diretto agli HP del giocatore
└─ slot in Retro → effetto passivo retro
```

Due letture che il layout deve rendere ovvie:

- **una corsia vuota è una falla**: lo slot nemico in Fronte colpisce direttamente gli HP;
- **una corsia nemica vuota è un varco**: la carta in Fronte colpisce direttamente il boss.

### 3.4 Aritmetica del danno

```
danno carta   = frontDamage + flipCharge + tempAtkBonus     (le cariche si consumano)
danno slot    = atkDamage + tempAtkBonus

blocco carta  = (Fronte ? frontBlockValue : backBlockValue) + tempBlockBonus
blocco slot   = (Fronte ? blockFront      : blockRetro)     + tempBlockBonus

danno finale  = max(0, danno − blocco)
```

`incomingDamageOverride` (se impostato da un'abilità) sostituisce il danno in arrivo
prima del blocco. **Il danno va sempre alla carta, mai al giocatore**: gli HP del
giocatore si toccano solo se la corsia è vuota.

### 3.5 Combo di adiacenza — `SynergyResolver`

Valutate su **coppie di corsie contigue**, prima della risoluzione:

| Combo | Condizione | Effetto |
|---|---|---|
| **Blade Pair** | due `Assalto` adiacenti, entrambe in Fronte | +1 ATK a entrambe |
| **Guard Link** | una `Guardia` adiacente a un'altra carta, almeno una in Retro | +1 BLOCK a entrambe |
| **Mystic Pulse** | un `Mistico` adiacente a una carta in Retro | +1 carica alla Retro, +1 HP al giocatore (una volta per turno) |

**Conseguenza diretta per il layout:** le combo vivono *fra* due corsie, non dentro una.
Lo spazio tra le colonne deve essere abbastanza largo da ospitare un connettore visibile.

---

## 4. Interazioni

| Azione | Input | Costo | Codice | Note |
|---|---|---|---|---|
| Selezionare una casella libera | click sull'`EmptySpot` | 0 | `OnEmptySpotClicked` | accende l'`Outline` |
| Giocare una carta | click sulla carta in mano **con casella già selezionata** | 1 AP | `OnCardClicked` → `PlayCardFromHand` | **senza casella selezionata non succede nulla, senza avviso** |
| Giocare una carta | trascinare dalla mano sulla casella | 1 AP | `HandleHandDrop` | percorso alternativo, stesso effetto |
| Selezionare una carta in campo | click | 0 | `SelectionManager.SelectOwned` | |
| Girare una carta | **doppio click** entro 0.3 s | 1 AP | `OnCardDoubleClicked` → `TryFlipCard` | il primo click seleziona anche |
| Scambiare due corsie | trascinare una carta in campo su un'altra | 1 AP | `HandleBoardDrop` → `SwapCardPositions` | |
| Riordinare la mano | trascinare dentro la mano | 0 | `ReorderHandDuringDrag` | |
| Pescare | clic sul **mazzo** nella colonna destra | 1 AP | `DeckView` → `HandManager.DrawCard` | ritorna `false` e scrive il motivo nel log a mazzo vuoto o mano piena |
| Attaccare | bottone ATTACCA | 0 | `OnAttack` → `ResolveAttackRoutine` | una sola volta per turno; la risoluzione dura ~1.5 s e blocca l'input |
| Chiudere il turno | bottone CHIUDI TURNO | 0 | `OnEndTurn` | fa partire rullo e ingresso degli slot, ~5 s di input bloccato |
| Slot nemico | — | — | — | **non cliccabile**: `Button` presente ma senza listener |

Due sequenze non scopribili da correggere nel layout:

1. **casella prima, carta poi.** Cliccare una carta in mano senza aver selezionato una
   casella esce con un `return` secco. Rimedio: quando una carta in mano è selezionata o
   trascinata, **accendere l'outline su tutte le caselle libere**.
2. **il flip è un doppio click.** Nessun elemento lo suggerisce. Rimedio: un bersaglio di
   flip esplicito sulla carta selezionata (mezzaluna / freccia di rotazione) con il costo
   in AP stampato sopra.

---

## 5. Stati dell'interfaccia

Il layout deve avere una resa dichiarata per ciascuno di questi nove stati.

| # | Stato | Condizione | Cosa deve leggersi | Etichetta di fase |
|---|---|---|---|---|
| 1 | **Riposo** | nessuna selezione | corsie leggibili, bilancio previsto per corsia, AP disponibili | `FASE AZIONI` |
| 2 | **Casella selezionata** | `SelectedEmptySpot != null` | casella marcata; carte in mano segnalate come giocabili | `FASE AZIONI` |
| 3 | **Carta in mano presa** | drag o selezione | **tutte** le caselle libere evidenziate; anteprima di destinazione | `FASE AZIONI` |
| 4 | **Carta in campo selezionata** | `SelectedOwned != null` | carta sollevata; bersaglio di flip col costo; corsia nemica affacciata evidenziata | `FASE AZIONI` |
| 5 | **Scambio armato** | `IsSwapArmed` | sorgente marcata, destinazioni valide marcate | `FASE AZIONI` |
| 6 | **Risoluzione** | `Resolving` | una corsia per volta: chi attacca scatta in avanti, chi para rimbalza, chi incassa trema; ogni riga di log cade quando succede | `RISOLUZIONE IN CORSO` |
| 7 | **Attacco fatto** | `awaitingEndTurn` | mazzo e ATTACCA spenti | `ATTACCO RISOLTO — CHIUDI IL TURNO` |
| 8 | **Reel e ingresso slot** | `inputLocked` | corsie nemiche coperte, poi gli slot entrano uno per volta con le abilità che li modificano; tutti i comandi spenti | `NUOVI SLOT IN ARRIVO` |
| 9 | **Fine partita** | `matchEnded` | pannello di risultato con HP finali e turni giocati | `PARTITA FINITA` |

Gli stati 6, 7 e 8 hanno tutti l'input bloccato e prima si distinguevano solo per
il grigio dei bottoni — con `DisabledColor = (0.784, 0.784, 0.784, 0.5)` su
`Image` bianche, cioè praticamente per niente. Ora ognuno ha la sua etichetta,
scritta da `HudController` in polling, e `UiBuild.Command` dà ai bottoni un
disabilitato con contrasto reale. `Resolving` va controllato **prima** di
`InputLocked`, o lo stato 6 si presenta come lo stato 8.

---

## 6. Layout proposto

### 6.0 Da dove vengono i numeri

Le misure **non sono scelte a mano**: sono `layouts.board` di
`Assets/Graphics/FlipCards_ArcadeHorrorUI/ArcadeHorrorUI/flipcards_ui_manifest.json`
(kit *Arcade Horror CRT*) moltiplicate per 2 — il tabellone 960 × 540 del kit
portato sul canvas 1920 × 1080.

Lo stesso kit fornisce `2x/board/board_bg.png`, un fondo 1920 × 1080 con **già
disegnati i pozzetti di ogni zona**. `FlipCardsLayoutBuilder` lo usa come
Backdrop quando lo trova: se le bande qui sotto e quelle del fondo coincidono, il
tabellone si presenta come `preview_board@2x.png`. Se le fai divergere, i
contenuti finiscono *accanto* ai pozzetti invece che dentro — è il modo più
rapido di accorgersi che un numero è stato cambiato solo da una parte.

Il fondo è opzionale: senza kit importato il builder ripiega su tinte piatte e il
layout resta identico, cambia solo la pelle. Gli overlay CRT
(`overlay_scanlines`, `overlay_vignette`) stanno sopra tutto, `Raycast Target`
spento, e sono l'ultima cosa creata prima del pannello di fine partita.

### 6.1 Impostazione del Canvas

- **Render Mode:** `Screen Space - Camera`, Render Camera = `Main Camera`
- **CanvasScaler:** `Scale With Screen Size`, riferimento **1920 × 1080**, match **0.5**
- **Scala:** `localScale = (1, 1, 1)` su tutte le radici (una `z = 0` azzera la
  matematica 3D di ombra e tilt in `CardView`)

### 6.2 Griglia

```
rail giocatore   x   12 …  306   (294)   stato, mazzo, legenda
campo di gioco   x  316 … 1494  (1178)
colonna destra   x 1504 … 1904   (400)
passo di corsia  396            uguale per i due lati
cella carta      224 × 330→336  gap fra corsie 172
cella nemica     352 × 288      gap fra corsie  44
corsie centrate  x = 508 / 904 / 1300   (3 corsie)
```

Il passo è **unico per i due lati** anche se le celle hanno forme diverse: rullo,
asse dei pronostici e corsie del giocatore devono stare sui medesimi tre centri,
o l'asse punterebbe fra due corsie. Il gap del giocatore (172) è anche lo spazio
che ospita i connettori di combo.

La casella nemica è **orizzontale** e la carta **verticale**: il fronte nemico è
un rullo da slot machine, non una fila di carte, e la forma lo dice prima di
qualunque etichetta.

### 6.3 Bande verticali — campo di gioco

| y | h | Zona | Contenuto |
|---|---|---|---|
| 12 | 48 | **Targa turno** | `TURNO 4 / 12` (larga 400) |
| 12 | 48 | **Banner di fase** | etichetta di fase (x 860 … 1494) |
| 68 | 56 | **Fascia boss** | nome, preavviso, barra HP `hp/maxHp` |
| 132 | 400 | **Cassa del rullo** | contiene le caselle e le fasce in cui scorrono quelle parziali |
| 188 | 288 | **Corsie nemiche** | celle 352 × 288 |
| 332 | 2 | **Payline** | riga ambra su cui la casella "si ferma"; disegnata **dopo** le corsie, deve attraversarle |
| 528 | 48 | **Asse delle corsie** | bilancio previsto per corsia + connettori di combo nei gap |
| 580 | 336 | **Corsie giocatore** | celle carta 224 × 336 |
| 924 | 156 | **Mano** | fino a 8 carte; a riposo si vede solo la linguetta alta, all'ingresso del puntatore la mano sale **in blocco** a quota 208 e copre in parte le corsie |

La mano sale tutta insieme, non carta per carta: l'area di attivazione contiene
la mano come figlio, così passare da una carta all'altra non genera un
`PointerExit`. Da alzata copre le corsie di proposito — è il momento in cui
scegli cosa giocare, non quello in cui leggi il tavolo.

### 6.3.1 Mano: sovrapposizione e pop-out

**Le carte in mano si sovrappongono, ed è voluto.** Il passo è 132 contro carte
da 224 — sono gli `hand_tab_slots` del kit, 8 linguette in 1148 px. È il
contrario dell'invariante precedente (§7.7, riscritta), che imponeva un passo più
largo della carta.

Quello che rende leggibile una fila di carte che si coprono a vicenda sono tre
cose, tutte in `CardView`:

1. **La spline** (`EvaluateHandCurve`): arco di posizione e rotazione a ventaglio
   presi da `CurveParameters`. L'ampiezza dell'arco cresce col numero di carte
   fino a un tetto di 8. Prima c'era una soglia secca — sotto le 5 carte l'arco
   era spento del tutto — e il ventaglio compariva di colpo alla quinta pesca,
   mentre le rotazioni c'erano già: carte inclinate su una riga piatta.
2. **Il pop-out**: la carta sotto il puntatore si solleva di `handHoverLift`,
   scala a `scaleOnHover`, **raddrizza il proprio angolo di ventaglio** e passa
   `overrideSorting` a un ordine superiore alle vicine. Senza l'ultimo punto si
   sollevava restando sepolta sotto la carta di destra, e il pop-out non si
   vedeva: è il pezzo che mancava.
3. Il sollevamento muove il **figlio grafico**, non la radice: il bersaglio di
   raycast resta fermo, quindi la carta non si sfila da sotto il puntatore.

I numeri di regia (`handHoverLift`, `scaleOnHover`, `scaleOnSelect`) li scrive il
builder sui prefab: dipendono da dove sta la mano, non dal gusto.

### 6.4 Bande verticali — rail del giocatore (294 di larghezza)

| y | h | Zona | Contenuto |
|---|---|---|---|
| 0 | 50 | Intestazione | `TU` |
| 56 | 46 | **HP** | barra orizzontale + `hp/maxHp` |
| 108 | 38 | **AP** | pallini spendibili + `ap/5` |
| 152 | 34 | Costi | `OGNI AZIONE 1 AP · ATTACCA 0` |
| 250 | 24 | Etichetta mazzo | `MAZZO` + conteggio a destra |
| 280 | 368 | **Mazzo** | pila di carte vere di dorso, cliccabile |
| 652 | 34 | Stato del mazzo | riga che dice perché il clic non fa nulla |
| 688 | 368 | **Legenda** | chiave dei colori: lato, numeri, fazioni |

Mazzo e legenda cadono nei due pozzetti a forma di carta che `board_bg` disegna
nel rail (`deck_slot` e `discard_slot` del manifest): sono le due bande da 368.

Il mazzo è passato dalla colonna destra al rail perché è un **oggetto del
giocatore**, come i suoi HP e i suoi AP, non un comando. La pila si assottiglia
con le carte che restano, il dorso in cima è quello della prossima carta (il
mazzo è mescolato una volta sola e si pesca dalla cima) e passandoci sopra
l'ispettore la mostra.

La legenda esiste perché la cella carta è **simbolica per scelta** — nessuna
etichetta testuale, solo numeri colorati e pastiglie. Senza una chiave, quella
scelta la paga alla prima partita chi non ha scritto il codice.

### 6.5 Bande verticali — colonna destra (400 di larghezza)

| y | h | Zona | Contenuto |
|---|---|---|---|
| 12 | 52 | Intestazione | `MANO 3/8` · `SEED n` |
| 72 | 528 | **Ispettore** | carta o slot sotto il puntatore: stat complete, passive, testo delle abilità |
| 608 | 304 | **Log** | autoscroll in fondo, tetto a 6000 caratteri |
| 920 | 148 | **Comandi** | ATTACCA · CHIUDI TURNO, 400 × 64, gap 20, costo AP stampato |

L'ispettore risolve in un colpo solo il problema più grosso: **abilità e passive non
hanno altrimenti nessuno spazio**, e la carta a 224 × 336 non può ospitarle.

Il seed è un'etichetta, non un valore che cambia: serve a poter ripetere una
partita identica.

### 6.6 Cella carta — 224 × 336

Anatomia da `layouts.card` del manifest, ×2. Le costanti stanno in `CardOverlay`,
che è anche chi disegna i fondi: il builder le rilegge da lì per posizionare i
`Text` del prefab, così i numeri vivono in un posto solo.

| Elemento | Banda (x, y, w, h) | Note |
|---|---|---|
| Template | 0, 0, 224, 336 | a tutta cella: è la cornice **e** il Graphic su cui gira `CardShaderGraph` |
| Barra nome | 12, 12, 200, 34 | |
| Badge fazione | 184, 16, 26, 26 | in coda alla barra nome |
| Finestra artwork | 32, 52, 160, 160 | solo in Fronte; in Retro il dorso a tutta cella |
| Chip statistiche | 3 × (64 × 36) a y 220 | ATK · HP · BLOCCO, con delta dei bonus (`5 +1`) |
| Sottolineature | y 256, h 3 | rosso attacco, verde vita, ciano blocco |
| Traccia cariche | 12, 264, 200, 22 | 3 tacche, piene = `flipCharge` |
| Fascia bassa | 12, 290, 200, 34 | badge di classe + `FRONTE` / `RETRO` |
| Overlay hint | fluttuante | **sopra** la carta, non nel flusso: non deve rimpaginare la cella |

**Il chrome è traslucido di proposito** (alpha ≤ 0.55). Il Template monta
`CardShaderGraph`, che nell'edizione POLYCHROME è legata alla rotazione della
carta: `ShaderCode` scrive `_Rotation` da `transform.parent.localRotation`, cioè
dal tilt che `CardView` anima. Con fondi opachi il riflesso che scorre al
passaggio del puntatore resta sotto e non lo vede nessuno. Per lo stesso motivo
la fascia bassa è un fondo scuro con testo colorato e non una fascia piena: una
banda opaca a tutta larghezza spegnerebbe il riflesso proprio dove si legge
meglio. Anche `_poly_power` conta: a 0.03 l'effetto era spento, il valore di
progetto del subgraph è 0.3.

### 6.7 Cella nemica — 352 × 288

Anatomia da `layouts.reel_cell` del manifest, ×2. Costanti in `SlotOverlay`.

| Elemento | Banda (x, y, w, h) | Note |
|---|---|---|
| Barra nome | 44, 6, 184, 28 | |
| Badge fazione | 12, 7, 26, 26 | |
| **Traccia pattern** | 232, 5, 112, 26 | `flipPattern` come sequenza di caselle, passo corrente pieno |
| Finestra artwork | 80, 40, 192, 192 | **figlio di nome `Sprite`** — il reel ne copia il rect |
| Chip statistiche | 3 × (108 × 32) a y 236 | ATK · HP · DEF, ATK sempre visibile, non solo durante l'attacco |
| Fascia di lato | 0, 270, 352, 16 | stessa codifica delle carte |
| Contatore furia | 12, 44, 130, 24 | se lo slot ha `SlotBerserker` |

### 6.8 Asse delle corsie — 320 × 48 per corsia

L'invenzione che rende leggibile l'intero gioco: per ogni corsia, l'esito previsto con i
lati attuali.

| Situazione | Resa |
|---|---|
| Carta Fronte vs slot | `↑ 5 − 1 = 4` (danno dopo la DEF nemica) |
| Slot Fronte vs carta | `↓ 2 − 2 = 0` (danno dopo il tuo blocco) |
| Carta Fronte, corsia nemica vuota | `↑ 5 → BOSS` |
| Slot Fronte, corsia tua vuota | `↓ 2 → HP` in rosso: è una falla |
| Entrambi in Retro | `—` stallo |

Nei **gap da 172** fra le colonne del giocatore: badge dei connettori di combo
(Blade Pair, Guard Link, Mystic Pulse) accesi quando la condizione di adiacenza è
vera. Le quote interne della colonna seguono l'altezza reale della banda: l'asse
si è già accorciato una volta col layout e le costanti tarate su 64 erano finite
fuori dal rect.

---

## 7. Vincoli imposti dal codice

Il layout non è libero: queste sono regole che, se violate, rompono la logica di gioco.

1. **`aiBoardRoot` può avere come figli diretti solo le corsie, in ordine.**
   `GetEnemySlotViewAtLane(lane)` fa `aiBoardRoot.GetChild(lane)`. Niente intestazioni,
   separatori o decorazioni come figli.

2. **`playerBoardRoot.childCount` definisce il numero di corsie di *entrambi* i lati.**
   Lo leggono `SpawnEnemySlots`, `RespawnEnemySlotsFromList` e `OnEndTurn`. Un figlio in
   più sotto `playerBoardRoot` crea una corsia fantasma.

3. **La cella della corsia giocatore è il `_BoardContainer`**, un `RectTransform` creato a
   runtime da `CardView.EnsurePlayerBoardContainer` e dimensionato dal rect della carta.
   O si attiva `Child Control Width/Height` sul layout group e si mette un `LayoutElement`
   sul container, o si dimensiona dal rect della carta — ma coerentemente.

4. **`PlayerBoardRoot_Clone` esiste.** `Start()` duplica l'intera board come fratello, e
   la usa come fantasma durante il drag. Deve sovrapporsi esattamente all'originale:
   stesso anchor, pivot, scala e posizione.

5. **`_ReelOverlayLayer` è fratello di `AIBoardRoot`**, con `ignoreLayout = true`, e copia
   il rect del figlio `Sprite` di ogni slot. Quindi: il prefab slot deve conservare un
   figlio chiamato esattamente `Sprite` (o si aggiorna `artChildName`), e `AIBoardRoot`
   non deve avere scala diversa dal fratello, altrimenti la copertura si disallinea.

6. **`EmptySpot` ed `EmptySlot` devono avere lo stesso rect** della carta e dello slot che
   sostituiscono, altrimenti le corsie saltano a ogni morte. **Sono due misure
   diverse**: 224 × 336 la casella del giocatore, 352 × 288 quella nemica.

7. **Nessun `LayoutGroup` sulla mano.** `HandManager.Update()` riscrive
   `container.localPosition` ogni frame; un `HorizontalLayoutGroup` attivo su `handRoot`
   ci combatte e produce uno scatto a ogni pesca.
   Il passo è il campo esplicito `handSpacing` (132), **non** più
   `handRect.width / maxHandSize`: quel ripiego dava per forza un passo più largo
   della carta, cioè una fila staccata, e la mano a ventaglio del kit vuole il
   contrario. Passo e `maxHandSize` (8) restano **misure di layout** e li scrive
   `WireHandManager` dagli `hand_tab_slots` del manifest: modificarli
   nell'Inspector non dura oltre il prossimo rebuild.
   La sovrapposizione è leggibile solo se restano in piedi le tre cose di §6.3.1
   — arco, rotazione a ventaglio e pop-out con `overrideSorting`. Toglierne una
   riporta le carte a coprirsi nome, vita e attacco a vicenda.

11. **Il pivot di `handRoot` sta al centro** e la mano è figlia dell'area di
    attivazione (`HandTray`), non sua sorella. `HandManager` posiziona i
    container con `localPosition` simmetrica intorno allo zero, che è il pivot
    del parent; e l'area deve contenere la mano, altrimenti passare da una carta
    all'altra genera un `PointerExit` e la mano scende sotto le dita.

12. **Il mazzo è mescolato una volta sola e si pesca dalla cima**
    (`HandManager.RebuildDeckFromBindings` mescola con `GameManager.Rng`).
    `DeckView` mostra i prossimi prefab con `PeekDeck`: tornare a estrarre a caso
    a ogni pesca farebbe mentire la pila.

8. **Nessuna `Image` con `Raycast Target` attivo sopra l'area di gioco.**
   `FindEmptySpotUnderPointer` e `FindBoardCardUnderPointer` usano
   `EventSystem.RaycastAll` e risalgono i parent: un pannello di sfondo che intercetta
   rompe drag-and-drop e swap. Vale anche per il fondo `board_bg` e per gli
   overlay CRT, che coprono tutto lo schermo: nascono da `UiBuild.Fill`, che ha
   il raycast spento di default, e devono restare così.

9. **Sorting delle carte.** Durante il drag la carta forza `sortingOrder = 10` sul
   proprio `Canvas` (`overrideSorting = true`); in mano, sotto il puntatore, forza
   `5` per stare sopra le vicine sovrapposte. Nessun pannello della colonna destra
   deve stare su un Canvas con sorting superiore, o la carta trascinata ci finisce
   sotto.

10. **Gerarchia del prefab carta**, richiesta da `CardView`:
    `CardDefinition` (radice, gestisce l'input) → figlio con `CardView` (`RectTransform` +
    `Canvas` proprio). `CardView` pretende assegnati: `Template` (Image), `artworkMonster`
    (Image), i sei `Text` legacy, `hintText`, `_shadow`.

11. **Il doppio click seleziona *e* gira.** `OnPointerClick` chiama sempre
    `OnCardClicked`, e in più `OnCardDoubleClicked` entro 0.3 s. L'animazione di selezione
    e quella di flip partono insieme: vanno progettate per convivere, non per sovrapporsi.

---

## 8. Cosa manca oggi

Elementi previsti dalla specifica perché il dato esiste nel codice. Tutti quelli
elencati sotto sono **fatti**, tranne l'ultimo.

| Elemento | Dove vive ora |
|---|---|
| **Traccia `flipPattern` sugli slot** | `SlotOverlay.RefreshPattern`, banda a y 290 della cella |
| **Contatore turno** `4/12` | `HudController.UpdateTurn`, barra superiore |
| **Pannello di fine partita** | `HudController.UpdateEndPanel` |
| **Etichetta di fase** | `HudController.UpdatePhase`, cinque etichette distinte (§5) |
| **Costo AP sui comandi** | `UiBuild.Command`, riga costo a destra; il rail dice `ogni azione 1 AP` |
| **Evidenziazione delle caselle libere** | `GameManager.HighlightFreeSpots`, accesa anche su clic a vuoto |
| **Icone e testo delle abilità** | `AbilityCatalog` + `InspectorPanel`; i nomi finiscono anche nel log all'ingresso degli slot |
| **Contatore furia del Berserker** | `SlotOverlay`, chip `FURIA n/soglia` |
| **Contatori mazzo e mano** | `MANO n/8` in intestazione della colonna destra, conteggio del mazzo sulla pila nel rail; `DrawCard` logga il rifiuto |
| **Traccia cariche a 3 tacche** | `CardOverlay` |
| **Barre HP con massimo** | `UiBar` — boss orizzontale, giocatore verticale sul rail |
| **`ap/MaxPlayerAP`** | `HudController.UpdateAp`, pallini + `4/5` |
| **Log con autoscroll e tetto** | `LogPanel` + `GameManager.MaxLogChars` (6000) |
| **Risoluzione leggibile** | `GameManager.ResolveAttackRoutine` una corsia per volta, con animazioni per attacco, parata e danno |
| **Ingresso degli slot nemici** | `GameManager.EnterEnemySlotsRoutine`, uno per volta con le abilità raccontate |
| **Segnalazione dei cambiamenti di fine turno** | **ancora da fare**: flip caotico, cariche e avanzamento pattern restano tre righe di log in un frame solo — vedi ROADMAP A5 |

---

Riferimenti correlati: [ROADMAP.md](ROADMAP.md) per i lavori aperti,
[ANALISI_UI.md](ANALISI_UI.md) — audit dei difetti dell'interfaccia precedente.
