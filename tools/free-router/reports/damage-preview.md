# Anteprima del danno — 16 settembre 2026

Implementazione completata e verificata in Unity. La delega Kilo ha prodotto una
patch intermedia utile, ma ha richiesto correzioni sostanziali del supervisore:
questo esito non dimostra autonomia su un task Unity di questa complessità.

## Risultato nel gioco

- Eliminati frecce e numeri del danno dall'asse; conservati risonanza e insegne.
- Carte sul tavolo: scala uniforme 1,38, centro verticale 844; asse ridotto a 20.
  Modifica nel builder, scena rigenerata, prospettiva e allineamenti conservati.
- Ogni clic su una carta in campo sul Fronte riavvia un'anteprima di 2,8 secondi.
  Lampeggiano solo guardia assorbita e vita persa, compreso lo splash di Charge.
  L'overflow anima gli HP del boss in giallo; valori e colori tornano reali.
- Il Retro non attacca. Risonanza ignora la guardia, corsia vuota colpisce il boss.
  L'anteprima non consuma AP, non infligge danni e non avanza il generatore casuale.
- Cambi di posizione, lato, statistiche, bersaglio e fase invalidano l'anteprima.
  Le formule di Vanguard, Charge e ClassSynergy sono condivise col combattimento;
  il calcolo considera anche gli eventi di armatura reattiva effettivamente legati.

## Delega e correzioni

Brief: [brief-damage-preview.md](../brief-damage-preview.md).
Sessione Kilo: `ses_f549bbcb9ffeggKu2QRFUr6YlR`, CLI 7.7.2, agente `auto`.
Le trascrizioni `runtime/kilo-damage-preview*.jsonl` restano locali e ignorate da Git.

Il primo tentativo leggeva troppo codice, ripeteva estrazioni e inventava API.
Le riprese hanno mantenuto la stessa sessione. Il supervisore ha riscritto il
calcolo centrale e fornito a Kilo un contratto preciso per cinque file di UI.
La patch intermedia è conservata in `runtime/kilo-presentation-before-review.patch`.
Nella revisione sono stati corretti:

- lampeggio shader inefficace (`max` non attenuava lampade già accese);
- posizione delle colonne dell'asse rimossa insieme ai numeri;
- ripristino del colore e della cache degli HP del boss;
- bonus/armatura reattiva mancanti o basati su API inesistenti;
- verifica Unity incompleta prima di dichiarare il lavoro concluso.

La revisione conclusiva in sola lettura non si è completata: Step ha restituito
un timeout dopo la compattazione, e il singolo tentativo successivo con il router
automatico si è fermato su Nemotron con HTTP 502. Le trascrizioni sono
`runtime/kilo-damage-preview-review.jsonl` e `runtime/kilo-damage-preview-review-retry.jsonl`.
Non si è forzato un completamento dell'harness né usato un modello a pagamento.
La valutazione positiva del risultato finale deriva dai collaudi del supervisore.

## Modelli e harness

Qwen3.5 2B offline è stato realmente usato per classificazione ed estrazioni
brevi; il lavoro di implementazione complesso è passato ai modelli gratuiti
qualificati. Step 3.7 Flash ha svolto la maggior parte del lavoro online;
Nemotron Super 120B ha incontrato errori 502/indisponibilità. Il router ammette
solo candidati `:free` a prezzo zero, senza ripiego a pagamento. L'utente ha
autorizzato espressamente codice pertinente, risultati e Main Camera verso
`https://api.kilo.ai/api/openrouter/v1/chat/completions`.

Miglioramenti permanenti all'harness:

- letture sorgente limitate a 240 righe, piano dopo quattro letture esplorative;
- una sola estrazione locale per turno, risposta breve e troncamento segnalato;
- contratto incompleto conservato nelle riprese, evitando reset e falsi blocchi;
- Console e Main Camera obbligatorie anche per modifiche Unity da filesystem,
  con obbligo di verifica conservato nel turno successivo;
- compattazione anticipata e affidata al modello di codice;
- diagnostica sicura di contesto/quota/timeout/indisponibilità, senza payload
  del provider né informazioni sensibili;
- permessi di scrittura limitati alla specifica esecuzione Kilo; revisione
  finale in sola lettura.

## Prove

Unity in primo piano, frame realmente in avanzamento. Compilazione e rebuild
riusciti; shader senza errori, Console finale con zero errori riportati. I test temporali campionano anche i pixel della
Main Camera, non deducono il lampeggio da uno screenshot statico.

| Prova | Esito | Evidenza locale |
|---|---|---|
| Guardia, vita, overflow, risonanza, buco, Retro, zero, riavvio, ripristino, stato logico | 1.050 controlli passati | `Logs/damage-preview-acceptance.txt` |
| Previsione contro risoluzione effettiva, bonus e armature | 10 scenari passati | `Logs/damage-forecast-rules.txt` |
| Annullamento su cambi di stato, drag, disattivazione, fine turno | 12 controlli passati | `Logs/damage-preview-cancel.txt` |
| Layout pulito con tre carte, AP reali, Main Camera | Ispezionato | `Logs/damage-preview-layout.png` |
| Overflow durante anteprima | Ispezionato | `Logs/damage-preview-overflow.png` |
| Router | 32 test passati | `test_router.py` |
| Diagnostica streaming | 3 test passati | `test_stream_diagnostics.py` |
| Harness e handoff | Suite passate | `test_harness.mjs`, `test_handoff.mjs` |
| Hook nativi Kilo e vera estrazione Qwen locale | Passati, 8 richieste del percorso controllato | `test_native_harness.py --with-local-extract` |

Gli script C# ripetibili sono in [verification](../verification/). Creano
fixture temporanee in Play: uscire dal Play scarta i dati di prova. Non è stato
ripetuto un playtest completo dei 12 turni. La suite nativa con upstream
simulato verifica gli hook, non misura la competenza del modello online.

Editor riportato fuori dal Play a fine verifica. Le differenze della scena sono
la rigenerazione del builder (inclusi nuovi ID); gli spazi finali dei campi vuoti
sono quelli del serializzatore Unity. `git diff --check` passa sui sorgenti e
sulla documentazione, escludendo tale scena generata.
