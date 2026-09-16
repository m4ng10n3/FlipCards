# Anteprima del danno sulla cassa

Implementa questa modifica in FlipCards usando l'agente auto e il router gratuito, seguendo AGENTS.md e il protocollo harness. L'utente autorizza codice, asset e immagini del gioco ai modelli online gratuiti. Non inviare credenziali o dati personali. Usa local_extract per un'estrazione breve utile dal codice letto; implementazione e ragionamento complesso spettano al modello online qualificato. Non usare il workflow art_bundle: questo task modifica comportamento e layout.

## Comportamento richiesto

- Rimuovi le frecce colorate con numeri del pronostico fra carte e slot. Conserva gli eventuali segnali di adiacenza/risonanza necessari; compatta e riposiziona il layout nel builder attivo, mantenendo gli allineamenti e il tavolo prospettico.
- Ogni clic su una carta gia in campo riavvia una piccola anteprima del danno che infliggerebbe dalla sua corsia, sul lato corrente. Non simulare un flip. Mantieni selezione, swap e drag esistenti.
- Le luci integrate nella cassa sono gestite da CabinetLampController (verifica il collegamento in MedallionSceneBuilder): fai lampeggiare solo le luci della guardia che assorbirebbe il colpo e della vita che verrebbe persa. Risonanza ignora la guardia; bonus e adiacenze devono coincidere con il combattimento reale. Non confondere SlotOverlay con il proprietario delle luci integrate.
- Se c'e overflow, anima il numero HP del boss fino al valore previsto e rendilo giallo. Anche la corsia senza lastra deve prevedere il danno diretto al boss.
- Durata leggibile indicativa 2.5–3 secondi, quindi ripristino dei valori/colori reali. Un altro clic riavvia senza sovrapposizioni; cambio carta/corsia/lato, attacco reale, fine turno, distruzione e fine partita annullano valori obsoleti. Retro/zero danno non devono inventare un attacco.
- Anteprima solo grafica: nessuna mutazione di HP, guardia, pool, AP, bonus, RNG o flusso del turno. Preferisci uno stato di presentazione esplicito, evitando la concorrenza con LateUpdate.

## Verifica e consegna

Piano con criteri osservabili, poi implementazione mirata. Verifica compilazione, rebuild tramite builder e Main Camera. Verifica l'animazione campionando piu frame reali (accertati che Time.frameCount avanzi): guardia parziale/totale, danno vita, overflow, risonanza, buco, Retro, clic ripetuti e annullamento al combattimento. Confronta stato logico prima/dopo. Scrivi prove in Logs/ se necessario; non dichiarare verificata un'animazione da un singolo screenshot. Se qualcosa fallisce cambia strategia motivatamente e conserva evidenze, senza cicli identici.

File iniziali mirati: Assets/Editor/MedallionSceneBuilder.cs, Assets/Scripts/UI/LaneAxisView.cs, Assets/Scripts/UI/CabinetLampController.cs, Assets/Scripts/UI/HudController.cs, Assets/Scripts/Managers/GameManager.cs (OnCardClicked), Assets/Scripts/Managers/LaneResolver.cs, Assets/Scripts/Slots/SlotInstance.cs. Scopri le API effettive prima di scrivere.

Non fare commit. Consegna cambiamenti, risultati verificati, limiti e checkpoint complete soltanto con prove vere. Il supervisore revisionera il diff e il gioco e potra chiedere una correzione nella stessa sessione.
