# Auto v4: protocollo e verifica

Valutazione sul task Unity reale dell'anteprima danno:
[rapporto e limiti](reports/damage-preview.md). I blocchi dell'harness migliorano
il processo, ma non sostituiscono revisione semantica e collaudo in Play.

Usare **auto / router/auto** nella chat Kilo. Dopo questa modifica premere
**Ricarica Kilo** nel pannello Auto. Gli hook vengono caricati all'avvio del backend;
il router rifiuta richieste con strumenti prive di revisione v4 e ID di sessione.
Una chat vecchia non procede silenziosamente senza protezioni.

## Ciclo operativo

La prova dell'anteprima danno ha mostrato una lunga serie di letture integrali senza
contratto. Ora dopo quattro letture/ricerche riuscite durante l'ispezione il quinto
accesso richiede un checkpoint di piano. Il blocco non termina la sessione: fissati
ipotesi e criteri, le letture mirate delle API restano disponibili. L'estrattore
locale può elaborare gli estratti già raccolti anche prima del piano.
Le letture native dei sorgenti sono limitate a 240 righe per chiamata, mantenendo
offset e accesso alle righe successive. La compattazione scatta al 50% e usa
`router/codice`: il 2B locale resta adatto a classificazione ed estrazioni brevi,
non a riassumere una cronologia da 100.000 token. `small_model` rimane locale.
Le revisioni nella stessa sessione conservano il contratto incompleto invece di
azzerarlo mentre il modello ricorda ancora il piano precedente. Una sola estrazione
locale è ammessa per turno; il suo prompt chiede risposte entro 80 parole e segnala
esplicitamente output troncati. Un edit C#/shader richiede prove Unity anche se
l'agente non ha ancora chiamato RunCommand. Queste guardie non certificano la
correttezza semantica del codice: compilazione e collaudo restano indispensabili.

La CLI non interattiva rifiuta automaticamente una scrittura con permesso `ask`.
Per un incarico di modifica già autorizzato avviarla con un override limitato
`agent.auto.permission.edit = allow`, mantenendo i divieti e l'harness attivi.
Non confondere questo rifiuto interno con una decisione esplicita dell'utente.

1. Individuare il componente realmente attivo e leggere le API dei dati.
2. `harness_checkpoint(plan)`: fissare criteri osservabili e piano breve.
3. Modificare solo i file necessari, preservando lavoro non pertinente.
4. Verificare dopo l'ultima modifica. Per Unity: Console, controlli del comportamento,
   Main Camera per modifiche visive. Un'immagine statica non verifica dinamiche.
5. `harness_checkpoint(complete)`: associare ogni criterio agli ID delle prove reali
   e descrivere l'osservazione. Prove inesistenti, fallite o precedenti alla modifica
   vengono rifiutate. La valutazione semantica dell'osservazione resta dell'agente.

Tre ripetizioni di un ciclo identico di 1–4 strumenti, quattro errori consecutivi,
48 operazioni o tre compattazioni fermano il turno. I checkpoint non azzerano i
limiti. Il registro locale conserva criteri, risultati e punto di ripresa. Un nuovo
messaggio reale dell'utente apre un turno; una continuazione sintetica no.
Le istruzioni vietano di aggirare lo stop creando altre sessioni.

Gli errori finali del router sono terminali: Kilo non deve trasformare i tre
tentativi già esauriti in una nuova catena infinita. Nessun replay dopo uno stream
parziale. Quote/cooldown richiedono una ripresa successiva, non attesa indefinita.

## Ruoli e memoria

- **auto** mantiene obiettivo e verifiche; online soltanto candidati gratuiti.
- **local_extract** estrae fatti da massimo 2.400 caratteri già letti. Usa il 2B
  direttamente, con un contesto breve; nessun accesso a file, shell o MCP. L'output
  va confrontato con la fonte. È preferibile a una delega Kilo completa per una
  piccola estrazione: evita il catalogo e il prompt del subagente.
- **rapido** resta un subagente locale di sola lettura per quando c'è memoria.
- **coordinatore** resta sperimentale. La prova di una delega di sola lettura non
  lo qualifica a coordinare modifiche Unity complesse. Non è il predefinito.

Il profilo GPU ha due slot da 32k; quello minimo CPU ha un contesto da 2k per
classificazione/estrazioni brevi. In quest'ultimo profilo una sessione completa
rapido/coordinatore può eccedere il contesto e viene rifiutata esplicitamente.
Il monitor ferma solo il proprio modello sotto 1 GB di commit libero.
Se non c'è memoria, Auto continua con regole deterministiche e modelli online;
non finge che il classificatore locale sia attivo. Il pannello mostra lo stato.

Il template Qwen ammette un solo messaggio di sistema iniziale: l'adattatore locale
riunisce i blocchi di sistema/developer preservandone l'ordine. Messaggi utente,
assistant, ragionamento e risultati degli strumenti restano in ordine e intatti.

## Benchmark

`Benchmark-All.ps1` esegue le prove di base e `benchmark_workflow.py`. Quest'ultimo
trasmette **soltanto fixture sintetiche**, senza leggere sorgenti del progetto:
proprietario del comportamento, errore interno a MCP, ciclo e verifica mancante.
I risultati sono in `benchmarks/workflow.json`; non sono una prova completa di coding.

Esito di questa esecuzione: Step 2/3 (ha accettato erroneamente una verifica
incompleta), Super 2/3 (proprietario sbagliato), Nex 0/3 (mancata tool call).
Il ranking pesa riconoscimento errori 3, ciclo/verifiche 2, proprietario 1.
Tutti i gruppi automatici richiedono almeno 4/6, oltre alle rispettive prove di base.
Nessuno viene presentato come infallibile: il protocollo e i blocchi restano attivi.
Una modifica alle slot richiede capacità di codice anche senza la parola "codice";
dopo edit/RunCommand questa capacità resta necessaria anche quando arriva una foto.

## Verifiche ripetibili

- `python -B -m unittest discover -s tools/free-router -p test_router.py`
- `test_harness.mjs`: contratto, errori MCP, prove vecchie/inventate, cicli, ripresa.
- `test_handoff.mjs`: vincoli originali e nessun riuso implicito di deleghe completate.
- `test_native_harness.py`: **vera CLI Kilo, modello simulato solo locale**. Primo
  edit bloccato senza piano; poi read/edit/read e completamento con prova reale.
  Non misura l'intelligenza di un LLM e non invia nulla online.
- `test_dashboard.cjs`: rendering dei dati nel DOM simulato, non verifica visiva VS Code.

Il caso delle slot è stato corretto e controllato direttamente tramite MCP locale:
36 campioni di sequenze 0/1/5, colori/cursore e lampadine con valori 0/1/2/3/5 e
stato trattenuto. Cattura Main Camera ispezionata. Nessun nuovo errore Console.
I campioni richiamano l'aggiornamento dei componenti: l'Editor era fermo al frame 1
in secondo piano, quindi **non costituiscono un playtest temporizzato completo**.
Non è stata ripetuta online l'intera modifica del progetto con un LLM.

Log/immagini/trascrizioni e registro sono in `runtime/`, ignorato da Git.
Il registro può contenere richieste e risultati del progetto: resta locale.
Il pannello espone soltanto fase, contatori, criteri e motivi di arresto su loopback.

Compatibilità: [hook ufficiali Kilo](https://kilo.ai/docs/automate/extending/plugins).
Per il budget Unity è stato impostato un importer desiderato tramite
[EditorUserSettings.desiredImportWorkerCount](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorUserSettings-desiredImportWorkerCount.html).
Valore precedente 4, salvato anche in `runtime/unity-import-workers.previous.txt`;
si ripristina con `EditorUserSettings.desiredImportWorkerCount = 4` e
`AssetDatabase.ForceToDesiredWorkerCount()`.

## Stato finale di questa sessione

Dopo autorizzazione ? stato fermato il solo servizio MCP KiCAD (circa 3,2 GB).
Il 2B ? attivo con **due slot GPU da 32k**: lettura nativa Kilo riuscita (731),
`local_extract` reale riuscito dentro Kilo (numero e booleano conservati).
Il percorso CPU minimo ? un ripiego; non ? stato qualificato con un playtest.
32 test del router passati; integrazione nativa in 8 richieste passata.
Risultati riassunti in [benchmarks/harness-validation.json](benchmarks/harness-validation.json).
La chat VS Code gi? aperta deve ancora caricare gli hook aggiornati con Ricarica Kilo.
