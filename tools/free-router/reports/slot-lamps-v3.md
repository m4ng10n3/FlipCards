# Prova reale Kilo Auto — slot con sette lampade

12 settembre 2026. Il layout finale è montato e verificato dal supervisore. **Il tentativo autonomo di Kilo non è superato.** Il risultato grafico corretto non va attribuito all'agente che ha eseguito il tentativo.

La scena usa sette luci rosse per la vita nel vano superiore, sette ambra verticali sul separatore sinistro di ogni rullo e sette ciano nel vano inferiore. Sono 21 per corsia, 63 complessive. L'atlas contiene sei ritagli, uno acceso e uno spento per tipo; forme e colori distinguono le statistiche. I valori oltre sette accendono tutte le sedi e mostrano il totale numerico. Gli indicatori leggono salute e bonus runtime, rispettano il lato e la risonanza. La sequenza di stati stampata sul rullo è conservata.

[Cattura dalla Main Camera](slot-lamps-v3.png). [Brief consegnato a Kilo](../brief-lamps-v3.md). [Risultato strutturato della prova](../benchmarks/unity-lamps-v3.json).

Kilo è stato eseguito realmente con l'agente `auto`, `router/auto`, MCP Unity attivo e il harness v4. Il router ha scelto `nvidia/nemotron-3-super-120b-a12b:free`; non c'è stato cambio di modello. Sessione `ses_f68af9402ffeFxmJ5qZo7b37Tb`.

- Primo turno: quattro ricerche, tre letture, quindi risposta troncata per limite di lunghezza: 6.903 token di ragionamento e nessuna modifica. È stato necessario un messaggio operativo del supervisore nella stessa sessione.
- Ripresa: checkpoint di piano e importazione corretta dei sei sprite persistenti. Una sostituzione fallita è stata recuperata rileggendo il file. La nuova UI aveva sette sedi per tipo, ma ancoraggi impliciti errati, immagini con raycast attivo, nessuno sprite iniziale assegnato, alloggiamenti assenti e totale d'attacco sovrapposto all'ultima sede.
- Verifica: Kilo ha scritto un messaggio di compilazione riuscita dentro un comando, creato due helper di attesa della camera e consultato log vuoti. Non ha ricostruito la skin, catturato la camera o provato i valori dinamici. Due dei tre tentativi di attesa da shell erano incompatibili con la shell effettiva. Il supervisore ha interrotto il processo dopo 23 chiamate a strumenti.
- Il harness ha imposto il piano, ma non ha fermato abbastanza presto questa sequenza di operazioni diverse e improduttive. Non è stato emesso un checkpoint di completamento. Nessun ciclo di fallback tra modelli è stato osservato: il problema qui è soprattutto nell'esecuzione e nella verifica.

I contatori Kilo dei passi conclusi sommano 1.171.700 token totali, inclusa la parte in cache; sono token di richieste ripetute, non altrettanti token unici. La misura non include un'eventuale risposta interrotta prima del relativo evento finale. Per una modifica così circoscritta, il costo in contesto è eccessivo. Il worker locale ha assistito la scelta iniziale del percorso, ma Kilo non ha usato `local_extract` né un coordinatore locale durante il montaggio.

Il supervisore ha mantenuto l'importazione di Kilo e corretto la costruzione dei banchi con `UiBuild.Band`, immagini decorative non interattive, sprite OFF iniziali, alloggiamenti petrolio con bordi ottone e spazio riservato ai totali. Ha ricostruito con `FlipCardsLayoutBuilder.Rebuild()` e rimosso gli helper temporanei di Kilo.

Prove indipendenti effettuate:

- 1.803 asserzioni su sprite validi, sette sedi per banca, raycast disattivati, valori 0/1/6/7/9, diminuzione 9→2→0, bonus, Retro, morte e percorso slot assente. Stato runtime ripristinato.
- Sei campioni di attivazione e cessazione della risonanza nelle tre corsie, con copie temporanee delle carte rimosse al termine. Un primo comando di preparazione ha cercato CardDefinition sul figlio grafico ed è stato corretto al parent prima della prova riuscita.
- Secondo rebuild seguito da un nuovo Play: tre pannelli, 63 lampade, nessun duplicato e sprite presenti.
- Console finale senza errori restituiti; comandi di ricostruzione e prova compilati ed eseguiti. Poiché in questo progetto il servizio Console può restituire vuoto, le asserzioni e gli esiti dei comandi costituiscono le prove principali.
- Cattura della Main Camera, ID69480: sedi nei vani e nei separatori, senza copertura delle finestre dei rulli. Unity lasciato in Edit Mode.

Limite: due campioni distanziati di `Time.frameCount` sono rimasti a1. Le prove dinamiche chiamano esplicitamente LateUpdate; l'animazione temporizzata del rullo non è stata validata in questa sessione. Il codice di chase è conservato.

L'esito reale è registrato separatamente dai benchmark sintetici e **non cambia automaticamente la graduatoria del router**. Prima di considerare Auto affidabile per Unity servono: un limite ai turni senza avanzamento anche quando gli strumenti cambiano, recupero esplicito da `finish_reason=length`, un catalogo strumenti e contesto iniziale più piccoli, comandi di verifica canonici per Unity e una valutazione che richieda prove pertinenti anziché semplici risposte tecnicamente riuscite.

Le prove ripetibili sono in `tools/free-router/verification/`; le tracce complete, il diff di Kilo prima della revisione e le risposte MCP restano in `tools/free-router/runtime/`. Le prove testuali runtime sono in `Logs/lamps-v3-independent-review.txt` e `Logs/lamps-v3-resonance-review.txt`.
