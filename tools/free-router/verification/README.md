# Campioni Unity delle lampade v3

Questi script non appartengono agli assembly di gioco: si passano come `arguments.Code` a `Unity_RunCommand` tramite MCP. Usare il progetto ricostruito, in Play, con tre slot vive e rullo fermo.

- `review-lamps.cs`: verifica sette sedi per tipo, sprite, input, valori e bonus crescenti/decrescenti, Retro, morte e assenza. Salva un log e ripristina salute, lato, Spec e registri originali dei bonus.
- `resonance-lamps.cs`: richiede una carta in mano e corsie giocatore vuote. Crea copie temporanee complete delle carte, attiva/disattiva la risonanza, verifica la difesa e rimuove le copie.

Usare `tools/free-router/unity_probe.py REQUEST.json --output RESULT.json`, con richiesta `{"name":"Unity_RunCommand","arguments":{"Title":"Review lamps","Code":"...contenuto dello script..."}}`. Questi sono campioni deterministici del componente; non certificano una partita animata se `Time.frameCount` non avanza. Uscire da Play al termine.
