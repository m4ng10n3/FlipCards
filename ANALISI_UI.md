# FlipCards — analisi UI e fix del resize sulle slot nemiche

Unity 6000.4.4f1 · URP 2D · repo `m4ng10n3/FlipCards` @ `f35ffe5`
Nessun commit e nessun push: i file sono solo scritti nella working copy.

---

## 1. Il bug segnalato

**Sintomo:** quando gira l'animazione slot-machine delle slot nemiche, il quadrato che contiene
l'immagine cambia dimensione.

**Causa (tre cose sovrapposte, tutte in `SlotBatchManager.cs`):**

| # | Cosa faceva il codice | Effetto |
|---|---|---|
| 1 | L'overlay `_BatchReelBG` veniva creato come figlio dello slot, in **stretch su tutto il rect** (`anchorMin 0` / `anchorMax 1`) | Lo slot è `100×150`, ma l'artwork vero vive nel figlio `Sprite` che è `100×100`. Il rullo disegnava quindi il frame in `100×150` e al reveal l'immagine **saltava a `100×100`**: 50 % di altezza in meno, di colpo. |
| 2 | Il frame dello sprite sheet è **quadrato** (`slotperroll.png` è `144×1584` = 11 frame da `144×144`) ma veniva stirato nel rect `100×150` | Immagine **deformata** per tutta la durata del rullo, poi improvvisamente corretta. |
| 3 | `bgGO.transform.DOPunchScale(Vector3.one * 0.25f, …)` su un rect con anchor stretched (`settlePunchScale: 0.25` in scena) | Al settle la copertura **si gonfiava del 25 % debordando oltre i bordi della lane**. |

**Bonus trovato strada facendo:** `slotperroll.png` aveva `wrapU/wrapV = 1` (**Clamp**).
Lo scroll UV del rullo fa superare `1.0` a `uvRect.y + frameH`, e con Clamp l'ultima riga di pixel
viene smussata a ogni giro. Serve **Repeat**.

**Problema di sequenza (che hai chiesto di correggere):** il rullo girava sopra la slot **vecchia**;
solo dopo `RespawnEnemySlotsFromList` distruggeva tutto e istanziava le nuove. Il giocatore vedeva
il rullo fermarsi su un'immagine, l'overlay sparire, e poi la slot nuova comparire di colpo.

---

## 2. Cosa ho cambiato

### `Assets/Scripts/Managers/SlotBatchManager.cs` — riscritto

- **Layer di copertura separato.** Le celle del rullo non sono più figlie dello slot: vivono in un
  `_ReelOverlayLayer` creato come **fratello** di `AIBoardRoot` (con `LayoutElement.ignoreLayout = true`).
  Così `AIBoardRoot.childCount` e `GetChild(lane)` restano puliti — ci si basano
  `GetEnemySlotViewAtLane`, `GetLaneIndexFor`, `OnEndTurnDealDamage` e `SlotAdjacentBuff` — e
  soprattutto la copertura **sopravvive al respawn degli slot**.
- **Il rect dell'immagine viene copiato dallo slot vero.** `GetArtRect()` legge a runtime il rect del
  figlio `Sprite` della lane e lo riproduce normalizzato nella cella. Al reveal l'immagine è
  **esattamente** dove e quanto era nel rullo. Se il figlio non c'è, fallback su `artAnchorMin/Max`
  configurabili in Inspector.
- **`AspectRatioFitter` (`FitInParent`)** sull'immagine: il rapporto del frame è bloccato, niente
  deformazione anche se cambi lo sprite sheet o il rect dello slot.
- **`RectMask2D`** sulla cella: nulla può più disegnare fuori dai bordi della lane, nemmeno un punch.
- **Punch disattivato di default.** Il campo è stato rinominato `settlePunch` (default `0`) proprio
  perché il vecchio `settlePunchScale: 0.25` serializzato in scena non venisse ereditato. Ora agisce
  sull'immagine ed è comunque ritagliato dal mask, quindi puoi rialzarlo senza rischi.
- **Il rullo rivela la slot nuova.** Nuova firma
  `RollNewSlots(laneCount, onPrefabsChosen, onComplete)`: `onPrefabsChosen` scatta **mentre le lane
  sono coperte**, lì `GameManager` fa il respawn; quando il rullo si ferma la copertura sfuma
  (`revealFade`) sullo slot vero già in posizione. Zero pop.
- **`try/finally` attorno alla coroutine.** Il respawn ora gira *dentro* il roll: se lancia
  un'eccezione, senza questo il gioco resterebbe bloccato dietro un pannello nero opaco con
  `_rolling` a `true` per sempre.
- Warning in `Awake` se la texture del rullo non è in `Repeat`.

### `Assets/Scripts/Managers/GameManager.cs`

- `OnEndTurn` usa la nuova firma a 3 argomenti.
- **`DetachAndDestroy()`**: `Destroy()` in Unity è differito a fine frame, quindi
  `foreach (Transform t in root) Destroy(t.gameObject)` lascia `childCount` gonfiato per un frame.
  Con un `HorizontalLayoutGroup` attivo significa **impaginare il doppio dei figli** per un frame —
  slot che si stringono e saltano di posto. Ora i figli vengono staccati (`SetParent(null, false)`)
  prima di `Destroy`. Applicato in `ClearChildrenUnder`, `SpawnEnemySlots`,
  `RespawnEnemySlotsFromList`, `RemoveSlotView`, `RemoveCard`, `PlayCardFromHand`.
- Rimossi i due loop `SetSiblingIndex(i)` su `aiBoardRoot`: erano no-op (assegnavano l'indice `i` al
  figlio `i`) e diventavano dannosi con figli in attesa di distruzione.
- **`inputLocked`**: `UpdateHUD()` riassegnava incondizionatamente `btnAttack.interactable` e
  `btnDraw.interactable`. Durante il rullo `playerPhase` è ancora `true`, quindi qualunque
  `UpdateHUD()` innescato da un'interazione **riaccendeva i bottoni a metà animazione**. Ora che il
  respawn avviene all'inizio, un Attack premuto lì avrebbe risolto contro gli slot **nuovi ma ancora
  invisibili**. Il flag è rispettato da `UpdateHUD`, `OnAttack`, `OnEndTurn`, `TryFlipCard`,
  `SwapCardPositions` e `OnCardClicked`.
- `EndMatch()` ora chiama `SetButtonsInteractable(false)`: `UpdateHUD` esce subito su `matchEnded`,
  quindi i bottoni restavano accesi e `btnDraw` (cablato direttamente su `HandManager.DrawCard`)
  lasciava pescare a partita finita.

### `Assets/Graphics/Atariboy cardgame/Big/slotperroll.png.meta`

`wrapU/wrapV/wrapW: 1 → 0` (Clamp → Repeat). Unity reimporta la texture da solo all'apertura.

### Da controllare in Inspector dopo il primo compile

Sul GameObject che ha `SlotBatchManager`:

- [ ] `Reel Frame Count` è `11`, ma nel batch ci sono **10 prefab** con `reelFrameIndex` 0-9 → il
      frame 10 dello sheet non esce mai. Se è voluto, ok.
- [ ] `Settle Punch` è un campo nuovo, parte da `0`. Alza a `0.06-0.10` se vuoi il "click".
- [ ] `Hold After Settle` (`0.5`), `Reveal Fade` (`0.22`), `Cover Color` (nero opaco): nuovi, hanno
      default sensati.
- [ ] `Art Child Name` = `Sprite`. Se rinomini quel figlio nei prefab slot, aggiornalo qui.
- [ ] `Reel Font` è vuoto: ora c'è un fallback automatico al font builtin, ma assegnarne uno è meglio.

---

## 3. Audit UI — cosa ho trovato nel resto del gioco

Ordinato per gravità. Tutto verificato leggendo scena, prefab e codice.

### Alta

**A1 — Il Canvas è in World Space: il CanvasScaler non fa niente.**
`Canvas.m_RenderMode: 2` (World Space) + `CanvasScaler` con `Scale With Screen Size 1920×1080, match 0.5`.
In World Space Unity usa solo `Dynamic Pixels Per Unit`: reference resolution e match sono **ignorati**.
Il framing dipende unicamente dalla `Main Camera` (ortho size `535.3`).
Conto: `ControlPanel` occupa x ∈ [1691.7, 1912.3], serve `aspect ≥ 1.768`. **In 16:10, 3:2 e 4:3 i
bottoni Draw / Attack / End Turn finiscono fuori schermo.** Il bordo alto di `LogScrollView`
(y 1081.2 contro 1074.3 visibili) è già tagliato oggi in 16:9.
→ Canvas su **Screen Space - Camera** (render camera = Main Camera), CanvasScaler
`Scale With Screen Size` 1920×1080 match 0.5, e riancorare `ControlPanel` a destra
(`anchorMin/Max = (1,1)` + offset negativo) invece di `anchoredPosition.x: 842`.

**A2 — Le carte in mano si rimpiccioliscono per sempre al primo hover.**
`HandManager.cs:123` spawna con `localScale = Vector3.one * spawnScaleMultiplier` (`1.5` in scena).
`CardView.ResetHoverVisual()` fa `DOScale(1f)` — **valore assoluto, non relativo**. Al primo
`OnPointerExit` la carta perde il 33 % e non torna più.
→ Salvare `_baseScale = transform.localScale` in `Awake` e usare `DOScale(_baseScale * fattore)` in
`ApplySelect`, `ApplyPointerEnter`, `ResetHoverVisual`.

**A3 — Selezione visiva e selezione logica sono due stati separati.**
`CardDefinition.cs:256` fa `ApplySelect(!Selected)` (toggle locale), mentre lo stato vero è in
`SelectionManager.SelectedOwned`. `SelectOwned()` non spegne mai la precedente → **più carte restano
ingrandite insieme**, e la carta "selezionata" secondo il codice può non essere quella evidenziata.
→ Togliere la riga 256; in `SelectOwned`/`SelectEmptySpot`/`ClearAll` fare
`_prev?.ApplySelect(false); view.ApplySelect(true);`.

**A4 — Cliccare una carta in mano senza aver prima scelto una lane non fa niente. In silenzio.**
`GameManager.cs:878-880`: se `SelectedEmptySpot == null` → `return` secco. Nessun log, nessun hint.
È la sequenza non scopribile numero uno.
→ `Logger.Info("Seleziona prima una lane libera")` e accendere l'`Outline` (già presente su
`EmptySpot.prefab`) su tutti gli spot liberi quando una carta in mano è selezionata.

**A5 — Gli slot nemici sembrano cliccabili ma non lo sono.**
`SlotView.Awake` aggiunge un `Button` e `Init` fa `onClick.RemoveAllListeners()` senza mai
aggiungerne uno. L'`Image` root ha `alpha: 0` e il Button è in ColorTint → hover e press sono
**invisibili**. `SetHighlight()` non è chiamato da nessun file del progetto.
→ O togliere il `Button` (meno raycast, meno confusione), o puntare `targetGraphic` sul figlio
`Sprite` e chiamare `SetHighlight(true)` sulla lane della carta selezionata.

**A6 — Non esiste una schermata di fine partita.**
`EndMatch()` scriveva solo una riga di log (i bottoni ora li spengo, vedi §2). Non c'è nessun
pannello nella gerarchia del Canvas. Manca anche l'indicatore di turno: `currentTurn`/`turns = 12`
esistono solo nel log.
→ Pannello "Fine partita" + un `Text` `$"Turno {currentTurn}/{turns}"` aggiornato in `UpdateHUD`.

**A7 — Il log cresce senza limite, non si autoscrolla e si può trascinare all'infinito.**
`GameManager.cs:840-844`: `_logBuf` è uno `StringBuilder static readonly` **senza tetto**, e ogni riga
fa `ToString()` dell'intero log → allocazione O(n) + rigenerazione completa della mesh del `Text`
legacy, che sopra ~16 000 caratteri (limite 65 000 vertici) **smette proprio di renderizzare**.
Nessuna scrittura a `verticalNormalizedPosition` in tutto il progetto → **niente autoscroll**.
`ScrollRect.m_MovementType: 2` (Unrestricted) con `m_Inertia: 0` → il contenuto non rientra mai.
→ `MovementType: Clamped`; in `AppendLog` tenere solo le ultime N righe
(`if (_logBuf.Length > 8000) _logBuf.Remove(0, _logBuf.Length - 6000);`) e
`Canvas.ForceUpdateCanvases(); scrollRect.verticalNormalizedPosition = 0f;`.

### Media

**M1 — `localScale.z = 0` su 8 oggetti di scena.**
`PlayerBoardRoot` e `AIBoardRoot` sono `{1.8, 1.8, 0}`; `PanelBoards`, `PlayerHand`, `LogScrollView`,
`LogText`, `HandManager`, `spawnPoint` sono `{1, 1, 0}`.
In `CardView.UpdateShadow()` questo rende `planeNormal = (0,0,0)` → fallback, `distToPlane = 0` →
**l'ombra sta sempre esattamente sotto il centro della carta e `_shadowPlaneZ: 10` è annullato**.
Anche il tilt 3D e il flip a 90° perdono la prospettiva e diventano solo schiacciamento.
→ Rimettere `z = 1` ovunque (per la UI 2D è irrilevante, ma rompe tutta la matematica 3D già scritta).

**M2 — `SlotView.preferredSize = (260, 160)` è codice morto.**
`AIBoardRoot` ha `childControlWidth: 0` e `childControlHeight: 0`: con `controlSize = false` il
LayoutGroup legge `child.sizeDelta` e **ignora del tutto il `LayoutElement`**. Il rect vero dello slot
è `100×150`. Quelle righe fanno credere a un sizing che non esiste — ed è una mina: il giorno che
attivi "Control Child Size" tutti gli slot saltano a `260×160`.
→ O attivare `Child Control Width/Height` e allineare `preferredSize` a `100×150`, o cancellare
`SlotView.cs:38-40` e il campo.

**M3 — Le lane sono allineate solo per taratura manuale.**
`VerticalLayoutGroup` di `PanelBoards` è **disabilitato** → le posizioni vengono da `anchoredPosition`
scritte a mano. Le due board usano ancoraggi diversi (`AIBoardRoot` pivot `(0,1)` + `{16,-16}`,
`PlayerBoardRoot` pivot `(0.5,0.5)` + `{-707, 13.42}`), compensati dalla scala `1.8`. I `LayoutElement`
con `flexibleHeight: 1` non hanno effetto senza layout group padre attivo.
Centri lane oggi: nemico `(-707.0, -512.6, -318.2)`, giocatore `(-707, -512.6, -318.2)`: coincidono,
ma cambiando `CardsPerSide`, la dimensione di `PanelBoards` o la risoluzione **si disallineano** e un
gioco a lane senza lane allineate è illeggibile.
→ Stesso anchor/pivot per le due board, `localScale = 1`, larghezza reale nel rect
(`n*cardW + (n-1)*spacing`) invece della scala 1.8.

**M4 — Le carte in mano si sovrappongono del 63 %.**
`HandManager.cs:169`: `spacing = 444.9877 / 8 = 55.6 px`, ma una carta è larga `100 × 1.5 = 150 px`.
Nome, HP e ATK delle carte a sinistra sono coperti. In più `PlayerHand` ha una
`HorizontalLayoutGroup` **attiva** che a ogni pesca ridriva anchor e `anchoredPosition` dei container
→ scatto di un frame prima che `Update()` riscriva `localPosition`.
→ Disabilitare quella `HorizontalLayoutGroup` (le posizioni le calcola già `HandManager`) e usare
`spacing = Mathf.Min(width / count, cardWidth * 0.7f)`.

**M5 — L'animazione di swap non si vede mai.**
`SwapCardPositions` fa `SetSiblingIndex` → il `HorizontalLayoutGroup` di `PlayerBoardRoot` riposiziona
i container a fine frame; solo dopo parte un `DOLocalMove` verso posizioni **già raggiunte**. Lo swap
è uno scatto istantaneo e il giocatore che ha speso 1 AP non vede nulla.
→ Non usare `SetSiblingIndex` per lo swap (o disattivare il layout group sulla board): animare col
tween e aggiornare l'indice a tween completato.

**M6 — L'HUD scrive "5/4".**
`apText.text = $"{actionPoints}/{playerBaseAP}"` ma `MaxPlayerAP = playerBaseAP + maxBonusAP = 5`.
E `hpText` / `EnemyHptxt` sono numeri nudi senza max e senza etichetta: il giocatore vede "13", "9",
"3/4" e deve indovinare.
→ `$"{hp}/{maxHp}"`, `$"{actionPoints}/{MaxPlayerAP}"` e tre label statiche.

**M7 — Gli hint si concatenano e vengono troncati.**
`ShowHint` fa `hintText.text + "\n" + msg`. Il rect è `89×15.6` sulla carta e `100×34.4` sullo slot,
Wrap + Truncate → si vede **1 riga sulla carta, 2 sullo slot**, il resto sparisce senza segnalazione.
L'hint di `AttackDeclared` non viene mai pulito (`ClearHint()` è chiamato solo nel ramo
`AttackResolved`) e resta appeso fino al turno dopo, mescolandosi coi messaggi del colpo successivo.
→ `ShowHint` sostituisce invece di concatenare (o coda max 2 righe con auto-clear), e `ClearHint()`
all'inizio del case `AttackDeclared`.

**M8 — I valori delle carte non ci stanno nei loro rect.**
`HP` è `26×28.96`, `FrontDamage` e `BackBlock` `26×26`, font 14, Wrap + Truncate, `BestFit: 0`.
`Refresh()` scrive `"12/12"` (≈35 px in 26 px) e in Retro `"[2/3]"` → va a capo e si taglia.
In più il formato oscilla: `Refresh()` scrive `"h/max"`, `UpdateHpOnly()` scrive solo `health` → il
**"/max" sparisce appena la carta viene colpita** e riappare al refresh dopo.
→ Allineare `UpdateHpOnly` al formato di `Refresh` e attivare Best Fit (min 8) sui tre Text.

**M9 — Tutorial e riepilogo di fine turno non compaiono mai.**
`TutorialLogger.enableTutorialTips` e `TurnScoreDisplay.enableTurnSummary` sono `false`, e in scena i
due componenti non hanno nessun campo serializzato (salvata prima dell'aggiunta dei bool). Sommato al
fatto che `LaneResolver.ResolveSlotPressure` logga senza l'importo del danno, **quando una carta perde
HP il log non dice nulla**: unico feedback un hint da una riga e un blink giallo da 0.08 s.
→ Mettere a `true` i due bool e aggiungere l'importo nel log.

### Bassa

**B1 —** `CardView.FlipSide` usa `_rt.DOKill()`, che uccide **tutti** i tween sul rect (anche
`_hoverPunchTween` e `_selectTween`) lasciando offset residui. E il flip parte da tre punti per un solo
evento logico; in `Refresh` il confronto è contro `sideText.text`, che alla prima `Init` vale `"Side"`
→ **ogni carta appena giocata fa un flip spurio da 0.25 s**.

**B2 —** `BlinkRoutine` cattura il colore corrente: due blink sovrapposti (`AttackResolved` + `Flip`)
fanno sì che il secondo catturi **giallo** come colore base → la carta **resta gialla per sempre**.
Guardia `if (_blinking) yield break;` o colore base salvato in `Awake`.

**B3 —** `btnDraw` resta interagibile con mazzo vuoto o mano piena: `DrawCard` fa `return` silenzioso.
Il `DisabledColor` è `(0.784, 0.784, 0.784, 0.5)` su Image bianche — quasi impercettibile.

**B4 —** Roba minore: su `Slot 0.prefab` **tutti** i Text e il figlio `Sprite` hanno
`RaycastTarget: 1` (6 target inutili per slot); `PanelBoards` ha un `Image` 1626×1080 con alpha 0.06 e
raycast attivo; `ShaderCode.cs:22` fa `new Material(image.material)` **per ogni carta** (batching rotto,
material mai distrutto) e ci scrive ogni frame; il label di `BtnAttack` è `'Attack` con un apostrofo di
troppo; c'è un `EmptySpot` figlio diretto del Canvas nascosto spingendolo **dietro la camera**
(`z: -93.2`) invece che con `SetActive(false)`; tipografia mista Text legacy (Arial builtin, 14 px
mostrati a 25 px → sfocati) e TextMeshPro solo sui 3 bottoni.

---

## 4. I 3 interventi con miglior rapporto impatto/sforzo

1. **Canvas → Screen Space - Camera + CanvasScaler funzionante** e `ControlPanel` riancorato a destra
   (A1). ~10 minuti di Inspector, zero codice: risolve i bottoni fuori schermo sotto 16:9 e rende
   stabile ogni correzione di layout successiva invece che tarata su una sola risoluzione.

2. **Scala base delle carte + selezione visiva** (A2 + A3). Una `_baseScale` in `CardView.Awake` usata
   nei tre `DOScale`, e `ApplySelect` spostato dentro `SelectionManager`. ~20 righe, elimina i due
   difetti visibili a ogni singolo click.

3. **Accendere il feedback già scritto** (A7 + M6 + M7 + M9): i due bool a `true`, `ShowHint` che
   sostituisce invece di concatenare, autoscroll + `Clamped` + trim del log, formati `hp/maxHp` e
   `ap/MaxPlayerAP`. È quasi tutto già implementato: si tratta di accenderlo e di non farlo
   overfloware. ~mezz'ora.
