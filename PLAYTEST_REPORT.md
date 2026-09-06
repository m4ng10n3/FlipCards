# FlipCards: playtest del game loop (6 settembre 2026)

## Metodo e limiti

Otto partite complete giocate nel vero Play Mode di Unity via MCP, pilotando le
azioni pubbliche (piazzamento, pesca, flip, attacco, fine turno) con quattro
strategie e AP reali: **rush** (tutto in Fronte, attacca sempre), **turtle**
(tutto in Retro, non attacca mai), **smart** (piazza per fazione, para solo i
colpi letali, attacca sempre) e **charge** (carica in Retro e attacca solo
quando il colpo uccide davvero). Quattro partite prima delle correzioni e
quattro dopo, tutte sullo stesso `seed = 12345`, cosi' il confronto misura le
regole e non la fortuna. Tempo accelerato a 3x; i log per turno stanno in
`Logs/playtest.txt`, la narrazione di gioco in `Logs/gamelog_<strategia>.txt`.

L'interazione del mouse e' misurata in due modi: **scansione dei raycast**
(`Logs/hoverscan.txt`, chi risponde sotto ogni pixel di una riga) e **tracciato
del cursore vero** (`Logs/hovertrace.txt`, il puntatore mosso via raw input e lo
stato di hover campionato a ogni frame).

**Limiti.** Un solo seed per strategia: le cifre sono ordini di grandezza, non
win rate. Nessun test di divertimento con persone. Le quattro strategie sono
bot deterministici: mostrano cosa il regolamento premia, non cosa fa un umano.

**Nota operativa.** L'editor Unity **congela il Play Mode quando la finestra non
ha il fuoco** (`Time.frameCount` resta a 1, `EditorApplication.update` non
scatta, e `runInBackground` non c'entra). Per giocare via MCP la finestra di
Unity deve stare in primo piano. Sta scritto qui e in AGENTS.md perche' non
lo ricostruisca nessun altro.

---

## Il difetto che teneva insieme tutti gli altri

**Il rullo cancellava il lavoro del giocatore.** Ogni fine turno tutte e tre le
caselle nemiche vengono sostituite. Il danno che non uccideva in un colpo solo
spariva con il giro. E uccidere in un colpo solo quasi non si poteva:

| | valori reali |
|---|---|
| ATK delle carte | 1–3 (piu' le cariche) |
| HP delle caselle | 3–5 |
| BLOCCO delle caselle in carica | 0–3 |

Colpo tipico: `ATK 3 − BLOCCO 1 = 2` contro 4 HP. Non uccide, quindi non fa
niente. Nelle 12 partite-turno della prima partita **smart** il boss e' passato
da 24 a 18: sei danni in tutto, cioe' due sole caselle rotte su trentasei
occasioni. Il resto degli attacchi era rumore.

Le conseguenze si vedevano tutte nei numeri:

- **Il bottone ATTACCA era una trappola.** Attacca tutte le corsie insieme e
  consuma le cariche anche dove il colpo non puo' uccidere. Attaccare ogni
  turno (12 attacchi) faceva 6 danni; attaccare 3 volte al momento giusto ne
  faceva 12. La mossa ovvia era la mossa sbagliata.
- **Il giocatore era invulnerabile.** Con le tre corsie occupate il boss non
  puo' toccare gli HP: il danno va sempre alla carta. `turtle` ha chiuso 12
  turni a **20/20 HP senza mai subire un colpo**.
- **Gli AP non servivano a niente.** Finita la mano e riempito il tabellone non
  c'era piu' niente da comprare: 24–37 AP sprecati per partita, 2–3 per turno.
  Il +1 AP della combinazione del rullo premiava con una valuta senza mercato.
- **Il flip caotico non succedeva mai.** `endTurnFlipChance × chaosFlipChance`
  dava ~13% per carta con un tetto di una carta per giro: **zero flip in 48
  carte-turno**. La randomicita' del flip, che doveva essere il gemello della
  randomicita' del rullo, era invisibile.

---

## Difetti diagnosticati nel giro precedente

Restano validi e sono gia' corretti nel codice; li lascio per memoria.

- **Passare evitava il combattimento.** Ora `DIFENDI E GIRA` risolve comunque i
  colpi nemici. Vedi pero' la voce aperta in fondo: con tutte le carte in Retro
  il nemico continua a non passare.
- **Attacco gratuito.** Attaccare costava zero AP.
- **Slot ripartiti sempre da zero**, pattern avanzato prima della distruzione.
- **Random non riproducibile**: il batch del rullo usava un RNG proprio invece
  del seed della partita.
- **Piazzamento imprevedibile**: una carta mostrata in Fronte poteva entrare in
  Retro.
- **Difese applicate in ritardo**: l'ordine di creazione delle istanze decideva
  se un'armatura arrivava prima o dopo il danno.
- **Effetti tra partite**: `AbilityBase` non si disiscriveva alla distruzione.
- **Berserk irraggiungibile**, **modificatori nemici persi**, **animazioni di
  flip e tilt in conflitto**, **selezione duplicata**, **fine striscia del reel
  visibile**.

---

## Cosa e' cambiato in questo giro

Tre regole e tre punti di leggibilita'. Tutte le manopole restano
nell'Inspector: sono numeri di bilanciamento, non costanti nascoste.

### 1. Le ferite non letali le paga il boss

`GameManager.CarryWoundsToBoss`, campo `woundCarryToBoss` (1 = tutto, 0 =
comportamento vecchio). Quando il rullo scarta una casella ferita ma viva, il
boss subisce il danno che quella casella aveva assorbito. Romperla resta
meglio: aggiunge `bossDamageOnSlotBreak` subito.

**Perche' cosi'.** La casella e' la corazza del boss *per questo giro*: quello
che le hai tolto e che la macchina butta via, lo paga chi c'e' dietro. Non
toglie identita' al rullo — la sostituzione totale resta — e sposta il momento
del pagamento sul giro, che e' esattamente dove una slot machine paga.

### 2. Gli AP tornano stretti

`playerBaseAP` da 4 a **3**, `maxBonusAP` da 1 a **2**. Con 3 AP il turno e'
una scelta: due flip e nessun attacco, oppure un flip, una carta e l'attacco. Il
+1 AP della combinazione adiacente adesso compra qualcosa, e il tetto a 5
lascia spazio a due giri fortunati di fila.

### 3. Il flip caotico e' l'altra faccia del giro

`chaosFlipChance` da 0.45 a **1**: la probabilita' torna a essere l'instabilita'
scritta sulla carta (0.2–0.55), non un prodotto di due numeri che si annullano.
E la scossa colpisce **per prime le corsie che il giro non ha abbinato**
(`SynergyResolver.Resonates` falsa): se il rullo ti ha dato la coppia, il
tabellone tiene; se ti ha dato tre simboli scoordinati, qualcosa ti si gira in
mano. Il flip si **anima** (`FlipSide`) invece di aggiornarsi e basta, e lascia
il suo hint sulla carta.

Misura: da **0** flip in 48 carte-turno a **6** flip in 36 carte-turno.

### 4. Il pronostico dice che colpire paga

`LaneAxisView`: sotto la corsia compare `rompe: boss -3` oppure
`boss -2 al giro`. Senza questa riga il giocatore legge "1" e conclude che non
serve a niente — che era vero prima e non lo e' piu'.

### 5. L'ispettore spiega la regola dove la si cerca

`InspectorPanel.ShowSlot`, sezione **"Cosa paga colpirla"**: quanto vale
romperla, quante ferite ha gia' addosso e quanto pagherebbe il boss al prossimo
giro.

### 6. La macchina paga a vista

`ReelChrome.FlashPayout`: quando il giro fa coppia o tris, le colonne che hanno
combinato **lampeggiano** (tre pulsazioni verdi, cinque ambra per il tris).
Prima la vincita esisteva solo come riga di testo nella HUD, cioe' nel punto in
cui non si sta guardando quando il rullo si ferma.

---

## Verifiche

Stesso seed, stesse quattro strategie, prima e dopo. `HP` e' il giocatore,
`BOSS` il boss; entrambi partono da 20 e 24.

| strategia | prima | dopo | esito prima → dopo |
|---|---|---|---|
| **smart** | HP 20 · BOSS 18 | HP 13 · **BOSS 3** | vittoria di misura → vittoria piena |
| **rush** | HP 17 · BOSS 18 | HP 9 · BOSS 10 | sconfitta → sconfitta per 1 punto |
| **charge** | HP 20 · BOSS 12 | HP 20 · BOSS 9 | vittoria → vittoria |
| **turtle** | HP 20 · BOSS 24 | HP 20 · BOSS 24 | sconfitta → sconfitta |

E le manopole toccate, misurate:

| misura | prima | dopo |
|---|---|---|
| danno totale al boss (smart) | 6 | **21** |
| AP sprecati per partita (smart / rush) | 24 / 25 | **10 / 9** |
| flip caotici per partita | 0 | **6** |
| combinazioni del rullo per partita | 4–5 su 12 | 3–7 su 12 |
| attacchi necessari per vincere | 3 ben scelti | attaccare ogni turno conviene |

**La cosa piu' importante di questa tabella** e' che la strategia migliore e'
cambiata. Prima vinceva `charge`, cioe' accumulare cariche e attaccare tre
volte in dodici turni: il gioco puniva chi premeva il bottone piu' evidente.
Adesso vince `smart`, che attacca ogni turno e para solo i colpi letali: la
mossa ovvia e' diventata la mossa buona, e la carica e' un amplificatore invece
che un segreto obbligatorio. `rush` perde per un punto perche' non para mai, e
`turtle` perde perche' non fa danni: le tre strade danno tre esiti diversi, che
prima non succedeva.

**Regressioni.** Nessun errore in console in 8 partite; nessuna
catena asincrona rimasta bloccata (`inputLocked` sempre rilasciato); il match
finisce sempre al turno 12 o alla morte di un lato.

---

## Bug di interazione: cosa dicono le misure

### Il ventaglio della mano regge

Scansione a 3 px lungo tutta la mano (`Logs/hoverscan.txt`) e cursore vero
mosso avanti e indietro sul ventaglio: **una transizione per confine, nessun
buco, nessuno sfarfallio**. C'e' un'isteresi di 40–80 px — la carta sotto il
puntatore mantiene l'hover finche' non sei ben dentro la vicina — e va bene
cosi': e' il motivo per cui non trema.

### Corretto: la carta scappava verso l'alto dal bordo basso

Tracciato, puntatore **fermo** a `(965, 19)`:

```
13,02s  pos=(965,19)  hover=MANO:Bastion  tray=SU
13,12s  pos=(965,19)  hover=-             tray=SU     <- hover perso senza muovere il mouse
```

Entrando dal bordo basso dello schermo la mano si solleva, le carte salgono e
sotto di loro resta un vuoto alto quanto la salita: il puntatore ci cade dentro
e perde hover e anteprima nell'ispettore, con il mouse fermo. Chiunque butti il
mouse in fondo allo schermo lo incontra.

Corretto in `CardView.UpdateHoverBounds`: finche' la mano e' sollevata, il
bersaglio di raycast di ogni carta si estende **verso il basso** di tutta la
salita, cosi' la colonna sotto la carta continua ad appartenerle.

### Corretto: la selezione a terra non si poteva togliere

Cliccando una carta in campo si selezionava; riclickandola non succedeva niente
(`SelectionManager.SelectOwned` usciva su `SelectedOwned == view`), quindi una
volta scelta una carta il tavolo restava per forza con una accesa. Ora il
riclic deseleziona, e annulla anche uno scambio armato per sbaglio. Verificato:
primo clic seleziona, secondo deseleziona, e la selezione passa comunque a
un'altra carta.

### Corretto: l'area della mano copriva le corsie

`HandZone` ha un'`Image` trasparente con `raycastTarget = true` per ricevere il
`PointerEnter`, e `HandTray.Apply` la faceva crescere da 156 a **440** quando la
mano saliva. Quei 440 sono un blocco invisibile steso sopra la metà bassa delle
corsie: il clic e l'hover sulle carte in campo finivano sull'area invece che
sulla carta — ma solo con la mano su, che è il motivo per cui sembrava capitare
"a volte".

Adesso il rettangolo resta sempre la striscia di richiamo in fondo allo schermo,
e a decidere se la mano resta su ci pensa `HandTray.ContainsPointer`, che misura
**le carte dove stanno adesso**: il figlio grafico (sollevamento e
ingrandimento compresi), non la radice e non il `raycastPadding`, che è più
largo della carta apposta per non perdere l'hover. Fuori dalle carte la mano
scende; con la mano vuota non sale affatto.

Verificato con il cursore vero:

```
mano con 1 carta   32,50s (965,540) hover=-  mano=SU
                   32,60s (965,540) hover=-  mano=giu     <- scende da sola
                   34,74s (965,499) hover=CAMPO:Bastion   <- la corsia risponde
mano vuota         altezza zona = 156 (non 440); mai sollevata; la carta in
                   campo risponde fino a y=250, dove prima c'era il blocco
```

### Resta: la carta sollevata copre la corsia sotto

Finché il puntatore è **sopra** una carta della mano sollevata, la mano resta
su — ed è la regola giusta. Ma la grafica di quella carta arriva a `y ≈ 517` e
la corsia sta fra 300 e 540, quindi l'ultimo tratto verso la corsia passa sopra
la carta. Si accorcia solo riducendo `handHoverLift` o `scaleOnHover`, cioè
cambiando la sensazione del pop-out: va deciso guardandolo.


## Cosa resta al playtest umano

- **Il valore di `woundCarryToBoss`.** A 1 il boss scende di ~1,75 per turno con
  gioco ordinato. Se il finale risulta troppo facile la manopola e' li': 0.5
  dimezza senza cambiare nessuna regola.
- **`turtle` non subisce ancora niente.** Dodici turni a 20/20 senza mai essere
  colpiti. Perde ai punti, quindi la pressione formalmente c'e', ma un muro che
  non viene mai scalfito e' una partita piatta. L'idea piu' semplice e' far
  passare al giocatore il danno che eccede gli HP della carta colpita.
- **Il ritmo a velocita' normale.** Tutte le misure sono a 3x. I tre arresti del
  rullo, il lampo di vincita e il flip animato vanno guardati a 1x, con l'audio.
- **La leggibilita' delle sinergie.** Sono cinque sistemi sovrapposti
  (risonanza di corsia, coppie Assalto, catene Guardia, pulsazione Mistico,
  combinazioni del rullo). Uno alla volta si leggono; tutti insieme e' da verificare.
- **La condizione di vittoria a confronto HP dopo 12 turni** premia ancora una
  difesa prolungata. E' rimasta la regola del progetto.
- **Rigenerazione e altre abilita' nate per slot persistenti** vanno ripensate
  nel modello a sostituzione totale; la furia e' gia' stata adattata ai simboli
  uguali estratti.
