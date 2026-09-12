# FlipCards — guida compatta per il modello locale

Versione ridotta di `AGENTS.md`, che intero vale ~12.700 token: troppo per il
contesto locale. Quando serve un dettaglio, trova la sezione in `AGENTS.md` con
grep sui titoli (`^## `) e leggi solo quella con offset e limit. Sezioni utili:
Le cinque regole del combattimento, Le tre catene asincrone, Mappa dei file,
Ricette di test, Trappole già pagate, Invarianti da non rompere.
Lavori aperti: `ROADMAP.md`. Vincoli di layout: `LAYOUT_SPEC.md` §7.

## Il gioco

Duello a corsie contro il rullo di un boss, 12 turni. Ogni corsia ha una carta del
giocatore in basso e una casella nemica in alto. Le carte hanno due lati: il Fronte
attacca, il Retro para ed è un'insegna. Il giocatore spende AP su lati e posizioni;
a fine turno il rullo estrae le caselle e le carte a terra si girano e si scambiano
di posto da sole.

## Regole del combattimento

Quasi ogni bug di bilanciamento nasce dal violarne una.

1. La casella è corazza: il colpo oltre la sua vita va al boss
   (`SlotInstance.ResolveIncomingAttack` → `GameManager.OverflowToBoss`).
   Simmetrico per la carta: `CardInstance.ResolveIncomingAttack` → `OverflowToPlayer`.
2. La corazza è un mazzo numerato (`BossPool`): il rullo pesca fra le lastre vive
   senza ripetizioni, le ferite restano, una lastra uccisa esce dal pool.
3. L'insegna dà il suo numero alle carte adiacenti della stessa fazione. È una
   regola, non un'abilità: sta solo in `SynergyResolver`.
4. Risonanza: carta e casella della stessa fazione nella stessa corsia, nessuno
   dei due para (`ignoreBlock`).
5. Una sola manopola: `GameManager.difficulty` (0..1). Nessun altro numero di
   bilanciamento.

Il pronostico (`LaneAxisView`) chiama gli stessi metodi della risoluzione: se i due
divergono, qualcuno ha messo un bonus fuori da `SynergyResolver`.

## File principali

- `Assets/Scripts/Managers/`: `GameManager.cs` (turni, AP, spawn, attacco, fine
  turno), `LaneResolver.cs`, `SynergyResolver.cs`, `HandManager.cs` (mano e
  mazzo), `SlotBatchManager.cs` (rullo di fine turno).
- `Assets/Scripts/Cards/`: `CardDefinition.cs` (dati e input), `CardView.cs`
  (presentazione e reazioni di combattimento).
- `Assets/Scripts/Slots/`: `SlotView.cs`, `SlotInstance.cs`.
- `Assets/Scripts/UI/`: `HudController.cs`, `CardOverlay.cs`, `SlotOverlay.cs`,
  `LaneAxisView.cs`, `InspectorPanel.cs`, `AbilityCatalog.cs`, `UiBuild.cs`,
  `UiBar.cs`, `GamePalette.cs`, `HandTray.cs`, `DeckView.cs`, `LogPanel.cs`.
- `Assets/Editor/FlipCardsLayoutBuilder.cs`: ricostruisce tutto il layout. Il
  layout non si modifica a mano nella scena: si modifica il builder.
- Grafica: `Assets/Graphics/1_NeonMonte_Attivo/` (in uso), `Assets/Graphics/2_Archivio/`.

## Invarianti

- Chi blocca l'input lo sblocca. `GameManager.ResolveAttackRoutine`,
  `SlotBatchManager.RollCoroutine` e `GameManager.EnterEnemySlotsRoutine` tengono
  `inputLocked = true` e lo rilasciano solo in fondo.
- I numeri di layout stanno solo in `FlipCardsLayoutBuilder` e nelle costanti di
  `CardOverlay`/`SlotOverlay`. Carta 224×336, casella 352×288, passo di corsia
  357,5, scale sempre uniformi.
- Ogni bonus passa da `AddAtkBonus`/`AddBlockBonus` con la ragione
  (`AbilityCatalog.Name(this)`); i reset da `ClearAtkBonus()`/`ClearBlockBonus()`.
- Le caselle si curano solo con `SlotInstance.Heal`.
- `GameManager.hpText`, `apText` ed `EnemyHptxt` sono null di proposito: la HUD
  la scrive `HudController`.
- `CardOverlay` e `SlotOverlay` costruiscono i figli a runtime dentro
  `_ChromeUnder`/`_ChromeOver`: non salvarli nel prefab.
- Nessun figlio di carta o slot deve essere Raycast Target.
- Unity 6: `FindAnyObjectByType` al posto di `FindObjectOfType`,
  `textWrappingMode` al posto di `enableWordWrapping`.
- Gli asset si spostano da filesystem insieme al loro `.meta`.
