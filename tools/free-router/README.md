# FlipCards Auto gratuito

L'interfaccia di lavoro resta **Kilo in VS Code**. Il pannello aggiunto si apre da
**Ctrl+Shift+P → FlipCards Auto: apri pannello**, oppure dalla voce **Auto locale**
nella barra di stato. Contiene avvio/arresto, apertura/ricarica di Kilo, benchmark e diagnostica.
Al primo utilizzo dopo l'installazione ricarica la finestra di VS Code se il comando
o i pulsanti aggiornati non compaiono.

1. Premi **Avvia Auto + locale**. Avvia il router su `8099` e il worker su `8081`.
2. Premi **Ricarica Kilo** dopo modifiche alla configurazione.
3. Nella chat scegli **auto**. L'endpoint/modello è `router/auto`.
4. **coordinatore** abilita la coordinazione locale sperimentale: delega le letture
   al worker `rapido` e i cambiamenti allo `specialista` online. Rimane sperimentale
   per task Unity lunghi; la prova reale di delega/lettura è registrata nei benchmark.

Non serve scegliere il modello a ogni passo. Il menu nativo di Kilo permette ancora
di forzare `vista`, `codice`, `cervello`, `rapido` o `locale`.
`rapido` e selezionabile anche come agente autonomo per letture/estrazioni sul PC.
`cloud` e `pesante` rimangono alias compatibili con le vecchie sessioni.

## Hardware e ruoli locali

Macchina misurata: **ROG Ally X, 23,2 GB RAM utilizzabile, grafica AMD integrata**.
Il modello aggiunto è **Qwen3.5 2B Q4_K_M**, 1.280.835.840 byte sul disco.
Il server usa un caricamento dei pesi e **due slot fino a 32.768 token**, con KV q8,
ragionamento disabilitato e quattro thread CPU. Durante il collaudo il processo
occupava circa **2,92 GB di memoria privata** (il valore varia col contesto).
Kilo limita il worker a **16k** e il coordinatore a **32k**: il secondo deve poter
gestire anche il conteggio preventivo del catalogo MCP. Le definizioni operative
vengono tolte dalla richiesta del coordinatore prima dell'inferenza; i suoi messaggi
non vengono tagliati. La prova a 64k non e stata mantenuta: con Unity aperto il
monitor ha rilevato pressione eccessiva sul commit.

- **Classificatore:** riceve solo l'obiettivo, massimo 2.400 caratteri, nessuno
  strumento e nessuna cronologia intera. JSON con categorie vincolate. Deadline 6 s;
  in caso di errore o incertezza usa le regole deterministiche. Si usa solo a inizio
  sessione e solo con benchmark locale valido. Non decide quali modelli siano ammessi.
- **Rapido:** letture mirate/estrazioni, senza permessi di modifica, shell o MCP.
- **Coordinatore:** contesto separato, task/todo; non legge/modifica direttamente.
  La delega include obiettivo, vincoli, file, fatti verificati e lavoro aperto.
  Un hook conserva le richieste originali nelle deleghe, senza affidarle al riassunto
  del 2B. Se superano il limite, segnala l'errore: non taglia i vincoli di nascosto.
  Lo stesso hook riusa il task_id gia restituito per quello specialista. Il blocco
  operativo del coordinatore e applicato dall'hook, non con un divieto MCP/edit
  ereditabile: altrimenti Kilo bloccherebbe anche lo specialista delegato.
- **Specialista:** esegue un task coerente con gli strumenti nativi e Unity MCP;
  il coordinatore deve riusare il `task_id` invece di ricrearlo a ogni fase.

Il precedente **Qwen 9B** resta disponibile nell'agente `locale`, avviabile con il
task **LLM locale: avvia server** su `8080`. Non viene caricato insieme al 2B di default.
`router/locale` e `router/rapido` non ripiegano su servizi online.
Le operazioni accessorie di Kilo (`small_model`) usano `router/rapido`.

Il 2B ha superato le estrazioni, ma ha fallito le prove di aritmetica/regole:
**non viene promosso a esecutore generale del codice**. Il suo routing ha ottenuto
14/15 nella prima suite locale. Sono misure del nostro test, non una garanzia generale.

## Come viene scelto lo specialista

1. Catalogo corrente: soltanto ID `:free` con prezzi dichiarati zero.
2. Pool esplicito di modelli per agenti: nessuna aggiunta automatica di modelli
   medici, finanziari o di moderazione perché hanno capacità visiva.
3. Qualificazione: almeno due prove superate e punteggio ≥80% nella categoria,
   oltre alle prove di tool calling/continuazione. Misure valide per sette giorni.
4. Filtri inderogabili: contesto stimato + output, immagini, strumenti, formato
   strutturato e cooldown. Il base64 delle immagini non viene contato come testo.
   Una richiesta esplicita di modifica codice richiede un modello qualificato per
   il codice, anche se il classificatore la sottovaluta. Le negazioni sono escluse.
5. Qualità misurata prima, latenza mediana dopo. I fallimenti recenti penalizzano
   la scelta. Un modello già assegnato resta prioritario se ancora qualificato.
6. Session ID passato dall'hook Kilo: resta stabile anche dopo la compattazione.
   Al cambio di capacità necessario, passa la cronologia completa; nessun riassunto
   automatico del proxy sostituisce messaggi o risultati MCP.

La stima del contesto è prudenziale, **non il tokenizer esatto** di ogni fornitore.
Il router non conosce la quota IP condivisa con altre applicazioni: il budget locale
di 180 richieste/ora conta soltanto tentativi osservati da router e benchmark,
comprese le richieste fallite, e persiste ai riavvii. Rispetta `Retry-After`.
Nessun candidato idoneo significa errore esplicito, non fallback incompatibile/a pagamento.

## Streaming e completamento

Il proxy analizza eventi SSE completi, conserva testo/ragionamento/tool call originali
e non aggiunge note al ragionamento. Le decisioni sono nel pannello e negli header.
Può riprovare prima di inoltrare contenuti. Dopo il primo contenuto, un'interruzione
produce un errore: non dichiara il task finito e non ripete chiamate MCP già emesse.
La scelta di continuare il lavoro resta a Kilo, che conserva la cronologia.

## Benchmark e verifica

```powershell
python -B tools/free-router/benchmark.py --models all
python -B tools/free-router/benchmark.py --models local --workers 1
python -B tools/free-router/benchmark_local.py
python -B -m unittest discover -s tools/free-router -p test_router.py -v
```

Le fixture provano estrazioni, uso di tool e continuazione, espressioni di codice
verificate su casi limite senza eseguire codice arbitrario, regole Unity e immagini
sintetiche. Le risposte vengono verificate deterministicamente, senza un altro LLM
come giudice. I fallimenti contano nel punteggio. Un benchmark online consuma quota.

`benchmarks/results.json` contiene le misure correnti; `local-capabilities.json`
contiene routing e deleghe simulate. Le prove sono piccole: non rappresentano ancora
un'intera partita Unity o una valutazione statistica ampia. Un modello escluso oggi
può qualificarsi dopo un nuovo benchmark, una volta risolti errori/quota.

I risultati scaduti o mancanti bloccano il relativo gruppo finché non si esegue il
benchmark. La classificazione locale scaduta ricade sulle regole, mantenendo i filtri.
Il coordinatore resta esplicitamente sperimentale per modifiche complesse e task Unity lunghi.

## MCP Unity e configurazione

`kilo.json` contiene modelli, agenti e comando MCP del progetto. Le credenziali restano
nel file globale già esistente `~/.config/kilo/kilo.jsonc`; il progetto non contiene
la chiave. La chiave locale è letta da `~/.llama-local/api-key.txt`.

**Diagnostica** esegue controlli HTTP e `kilo mcp list`. Unity deve aprire il progetto
con una licenza valida. All'avvio sono comparsi errori di licenza; successivamente
MCP si è connesso e l'agente Auto ha eseguito `Unity_RunCommand`, leggendo realmente
Unity **6000.4.4f1**, scena **SampleScene** e Main Camera. La verifica non ha
modificato file, oggetti o Play Mode. Le modifiche visive e una partita completa
restano fuori dall'ambito di questa prova del setup.
Anche la catena **coordinatore locale → specialista online → MCP Unity** e stata
verificata: tre comandi C# compilati ed eseguiti correttamente, valori ritornati al
coordinatore. La traccia e in `benchmarks/integration.json`.

Il router e il worker ascoltano solo su loopback. I log e lo stato sono in `runtime/`
(ignorato da Git), senza prompt completi nel log del router. Il pannello mostra
metadati delle richieste e fixture sintetiche dei benchmark, non le conversazioni.

## Ripristino e manutenzione

- Task **Auto: riavvia router** dopo modifiche Python/impostazioni. Non interrompe
  una richiesta attiva. Il worker resta caricato.
- Il monitor locale ferma soltanto il proprio processo se il commit libero scende
  sotto 1 GB. Per riavviarlo usa **Avvia Auto + locale**.
- Per tornare al precedente setup seleziona `locale`/`llamacpp/qwen3.5-9b` in Kilo.
- La copia precedente del router è `router.v2.backup.txt`, locale e ignorata da Git.
- L'estensione VS Code è locale: `flipcards-local.flipcards-auto`. Puoi disinstallarla
  dalla vista Extensions senza rimuovere Kilo o i modelli.

Modello scaricato dalla repository ufficiale del quantizzatore:
[Qwen3.5 2B GGUF](https://huggingface.co/unsloth/Qwen3.5-2B-GGUF).
SHA256 del file Q4_K_M:
`aaf42c8b7c3cab2bf3d69c355048d4a0ee9973d48f16c731c0520ee914699223`.

Riferimenti di compatibilità: [provider e modelli personalizzati Kilo](https://kilo.ai/docs/code-with-ai/agents/custom-models),
[distinzione tra Auto Free ed Efficient](https://kilo.ai/docs/code-with-ai/agents/auto-model).
