# FlipCards — descrizione del gioco e specifica di layout

Documento di riferimento per costruire il layout di gioco. Tutto quello che segue è
ricavato dal codice in `Assets/Scripts` allo stato attuale (`f35ffe5` + working copy).

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

Inventario completo dei dati esistenti. La colonna **Oggi** dice se il dato è già a
schermo; la colonna **Serve** è la raccomandazione per il layout nuovo.

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
| Mano | `handCards.Count` / `maxHandSize` (5) | **no** | contatore |
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

Mano massima 5 carte (`maxHandSize`). Il mazzo è costruito una sola volta da
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
| Pescare | bottone PESCA | 1 AP | `HandManager.DrawCard` | silenzioso a mazzo vuoto o mano piena |
| Attaccare | bottone ATTACCA | 0 | `OnAttack` | una sola volta per turno |
| Chiudere il turno | bottone CHIUDI TURNO | 0 | `OnEndTurn` | |
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

| # | Stato | Condizione | Cosa deve leggersi |
|---|---|---|---|
| 1 | **Riposo** | nessuna selezione | corsie leggibili, bilancio previsto per corsia, AP disponibili |
| 2 | **Casella selezionata** | `SelectedEmptySpot != null` | casella marcata; carte in mano segnalate come giocabili |
| 3 | **Carta in mano presa** | drag o selezione | **tutte** le caselle libere evidenziate; anteprima di destinazione |
| 4 | **Carta in campo selezionata** | `SelectedOwned != null` | carta sollevata; bersaglio di flip col costo; corsia nemica affacciata evidenziata |
| 5 | **Scambio armato** | `IsSwapArmed` | sorgente marcata, destinazioni valide marcate |
| 6 | **Risoluzione** | durante `OnAttack` | ordine corsia per corsia, numeri del danno, combo che scattano |
| 7 | **Attacco fatto** | `awaitingEndTurn` | fase esplicita: *"attacco risolto — chiudi il turno"*; PESCA e ATTACCA spenti |
| 8 | **Reel in corso** | `inputLocked` | corsie nemiche coperte, tutti i comandi spenti, nessun click accettato |
| 9 | **Fine partita** | `matchEnded` | pannello di risultato con HP finali e turni giocati |

Stati 7 e 8 oggi si distinguono solo per il grigio dei bottoni, con
`DisabledColor = (0.784, 0.784, 0.784, 0.5)` su `Image` bianche: praticamente invisibile.
Servono una **etichetta di fase** e un trattamento di disabilitazione con contrasto reale.

---

## 6. Layout proposto

### 6.1 Impostazione del Canvas

- **Render Mode:** `Screen Space - Camera`, Render Camera = `Main Camera`
  (oggi è `World Space`, per cui il `CanvasScaler` è inerte e sotto 16:9 il pannello
  comandi finisce fuori schermo)
- **CanvasScaler:** `Scale With Screen Size`, riferimento **1920 × 1080**, match **0.5**
- **Scala:** `localScale = (1, 1, 1)` su tutte le radici (oggi diverse hanno `z = 0`, che
  azzera la matematica 3D di ombra e tilt in `CardView`)

### 6.2 Griglia

```
colonna corsia   L = 240
gap fra corsie   G = 48        ← deve ospitare il connettore di combo
larghezza board  n·240 + (n−1)·48      3 corsie = 816   5 corsie = 1392
campo di gioco   x   0 … 1440
colonna destra   x 1440 … 1920         (480, contenuto 400 con 40 di padding)
corsie centrate  x = 312 / 600 / 888   (3 corsie)
```

### 6.3 Bande verticali — campo di gioco

| y | h | Zona | Contenuto |
|---|---|---|---|
| 0 | 56 | **Barra superiore** | `TURNO 4 / 12` · etichetta di fase |
| 56 | 92 | **Fascia boss** | nome, barra HP `hp/maxHp`, preavviso del turno |
| 148 | 330 | **Corsie nemiche** | celle slot 240 × 330 |
| 478 | 68 | **Asse delle corsie** | bilancio previsto per corsia + connettori di combo nei gap |
| 546 | 340 | **Corsie giocatore** | celle carta 240 × 340 (carta 220 × 330) |
| 886 | 52 | **Fascia giocatore** | barra HP + pallini AP |
| 938 | 142 | **Mano** | fino a 5 carte 220 × 330, visibili per 142, sollevate all'hover |

### 6.4 Bande verticali — colonna destra

| y | h | Zona | Contenuto |
|---|---|---|---|
| 56 | 40 | Intestazione | `MAZZO 9` · `MANO 3/5` |
| 96 | 424 | **Ispettore** | carta o slot sotto il puntatore: stat complete, passive, testo delle abilità |
| 520 | 336 | **Log** | ~14 righe, autoscroll in fondo, tetto a 6000 caratteri |
| 856 | 224 | **Comandi** | PESCA · ATTACCA · CHIUDI TURNO, 400 × 56, gap 16, costo AP stampato |

L'ispettore risolve in un colpo solo il problema più grosso: **abilità e passive non
hanno oggi nessuno spazio**, e la carta a 220 × 330 non può ospitarle. Mostrare i dettagli
al passaggio del puntatore evita di gonfiare la cella.

### 6.5 Cella carta — 220 × 330

| Elemento | Dimensione | Note |
|---|---|---|
| Finestra artwork | 220 × 160 | solo in Fronte; in Retro il dorso a tutta cella |
| Barra nome | 220 × 28 | |
| Chip statistiche | 3 × (68 × 44) | ATK · HP · BLOCK, con delta dei bonus (`5 +1`) |
| Traccia cariche | 220 × 16 | 3 tacche, piene = `flipCharge` |
| Fascia di lato | 220 × 20 | ambra = Fronte, blu = Retro — leggibile a colpo d'occhio |
| Badge classe + fazione | angolo | la classe guida le combo: deve essere visibile |
| Icone abilità | fino a 3 × 24 | dettaglio nell'ispettore |
| Overlay hint | fluttuante | **sopra** la carta, non nel flusso: non deve rimpaginare la cella |

L'hint oggi concatena le righe (`text + "\n" + msg`) dentro un rect di 89 × 15.6 con
Truncate: si vede una riga e il resto sparisce. Deve **sostituire**, non accodare.

### 6.6 Cella slot — 220 × 330

| Elemento | Dimensione | Note |
|---|---|---|
| Finestra artwork | 220 × 220 | **figlio di nome `Sprite`** — il reel ne copia il rect |
| Barra nome | 220 × 24 | |
| Chip statistiche | 3 × (68 × 32) | ATK · HP · DEF, ATK sempre visibile, non solo durante l'attacco |
| **Traccia pattern** | 220 × 20 | `flipPattern` come sequenza di caselle, indice corrente marcato |
| Fascia di lato | 220 × 16 | stessa codifica delle carte |
| Contatore furia | opzionale | se lo slot ha `SlotBerserker` |

### 6.7 Asse delle corsie — 240 × 68 per corsia

L'invenzione che rende leggibile l'intero gioco: per ogni corsia, l'esito previsto con i
lati attuali.

| Situazione | Resa |
|---|---|
| Carta Fronte vs slot | `↑ 5 − 1 = 4` (danno dopo la DEF nemica) |
| Slot Fronte vs carta | `↓ 2 − 2 = 0` (danno dopo il tuo blocco) |
| Carta Fronte, corsia nemica vuota | `↑ 5 → BOSS` |
| Slot Fronte, corsia tua vuota | `↓ 2 → HP` in rosso: è una falla |
| Entrambi in Retro | `—` stallo |

Nei **gap da 48** fra le colonne: badge dei connettori di combo (Blade Pair, Guard Link,
Mystic Pulse) accesi quando la condizione di adiacenza è vera.

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
   sostituiscono, altrimenti le corsie saltano a ogni morte.

7. **Nessun `LayoutGroup` sulla mano.** `HandManager.Update()` riscrive
   `container.localPosition` ogni frame; un `HorizontalLayoutGroup` attivo su `handRoot`
   ci combatte e produce uno scatto a ogni pesca.
   Inoltre `spacing = handRect.width / maxHandSize` → **`handRoot` deve essere largo
   almeno `5 × larghezza carta` = 1100** (consigliato 1200), o le carte si sovrappongono
   coprendo nome, HP e ATK di quelle a sinistra.

8. **Nessuna `Image` con `Raycast Target` attivo sopra l'area di gioco.**
   `FindEmptySpotUnderPointer` e `FindBoardCardUnderPointer` usano
   `EventSystem.RaycastAll` e risalgono i parent: un pannello di sfondo che intercetta
   rompe drag-and-drop e swap. Oggi `PanelBoards` ha un'`Image` 1626 × 1080 ad alpha 0.06
   con raycast acceso.

9. **Durante il drag la carta forza `sortingOrder = 10`** sul proprio `Canvas`
   (`overrideSorting = true`). Nessun pannello della colonna destra deve stare su un
   Canvas con sorting superiore, o la carta trascinata ci finisce sotto.

10. **Gerarchia del prefab carta**, richiesta da `CardView`:
    `CardDefinition` (radice, gestisce l'input) → figlio con `CardView` (`RectTransform` +
    `Canvas` proprio). `CardView` pretende assegnati: `Template` (Image), `artworkMonster`
    (Image), i sei `Text` legacy, `hintText`, `_shadow`.

11. **Il doppio click seleziona *e* gira.** `OnPointerClick` chiama sempre
    `OnCardClicked`, e in più `OnCardDoubleClicked` entro 0.3 s. L'animazione di selezione
    e quella di flip partono insieme: vanno progettate per convivere, non per sovrapporsi.

---

## 8. Cosa manca oggi

Elementi da prevedere nel layout perché il dato esiste nel codice ma non ha una casa.

| Elemento | Perché serve |
|---|---|
| **Traccia `flipPattern` sugli slot** | è l'unica informazione che rende il gioco pianificabile |
| **Contatore turno** `4/12` | esiste solo come riga di log |
| **Pannello di fine partita** | `EndMatch()` scrive una riga e spegne i bottoni; nient'altro |
| **Etichetta di fase** | stati 7 e 8 non sono distinguibili |
| **Costo AP sui comandi** | pescare, giocare, girare e scambiare costano 1 AP: mai scritto |
| **Evidenziazione delle caselle libere** | la sequenza "casella prima, carta poi" non è scopribile |
| **Icone e testo delle abilità** | 16 abilità, zero superficie di visualizzazione |
| **Contatore furia del Berserker** | progettato per essere prevedibile, oggi invisibile |
| **Contatori mazzo e mano** | `DrawCard` esce in silenzio a mazzo vuoto o mano piena |
| **Traccia cariche a 3 tacche** | oggi è testo, `"[2/3]"`, in un rect da 26 px con Truncate |
| **Barre HP con massimo** | oggi giocatore e boss sono numeri nudi: `13`, `9` |
| **`ap/MaxPlayerAP`** | oggi stampa `ap/playerBaseAP`, cioè `5/4` |
| **Log con autoscroll e tetto** | non scrolla, cresce senza limite, sopra ~16 000 caratteri smette di renderizzare |
| **Segnalazione dei cambiamenti di fine turno** | flip caotico, cariche e avanzamento pattern sono tre righe di log indistinguibili |

---

Riferimento correlato: [ANALISI_UI.md](ANALISI_UI.md) — audit dei difetti dell'interfaccia
attuale e correzioni già applicate al reel delle slot nemiche.
