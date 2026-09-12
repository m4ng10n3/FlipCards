# Montaggio strumenti slot v3 — incarico reale per Kilo Auto

L'utente richiede sette lampadine per OGNI statistica di ciascuna slot: vita rossa, attacco ambra, difesa ciano. Integra gli asset preparati, usando anche lo spazio verticale tra i rulli, e verifica il lavoro tramite Unity MCP. Questa è una modifica reale autorizzata; preserva il resto del progetto. Non cambiare router, harness, agenti, regole di combattimento o sequenza operativa stampata sul rullo.

## Direzione grafica e quote

Nuovo atlas già presente: Assets/Graphics/1_NeonMonte_Attivo/SceneKit_v2/09_Instruments_v3/lamps_atlas.png (1536×1024). Sei sprite: colonne vita/attacco/difesa; prima riga OFF, seconda ON. Il fondo scuro è intenzionale, va integrato in alloggiamenti petrolio scuro con sottili filetti ottone; non tentare scontorno o rigenerazione. Non modificare PNG o asset storici.

Ritagli in pixel con origine TOP LEFT (convertire y per Unity: 1024-y-height):
- vita OFF (112,44,384,440), ON (112,532,384,440)
- attacco OFF (638,44,260,440), ON (638,532,260,440)
- difesa OFF (1046,74,380,410), ON (1046,562,380,410)
Usa Sprite persistenti tramite MedallionSceneSkin.Slice già esistente, senza runtime Sprite in memoria dentro ScriptableObject. Carica atlas come texture senza ridurlo; usa nuove chiavi lamp_v3_round/spear/shield_off/on. Mantieni le vecchie chiavi disponibili.

La UI attiva è MedallionInstruments, creata da ReelChrome, montata da MedallionSceneBuilder. Non modificare solo ReelInstruments che è il fallback storico.
Ogni CabinetInstruments ha larghezza 301.312 circa, c=width/2, origine y=48 globale. Mantieni centratura LateUpdate sulla corsia vera. Coordinate UiBuild.Band locali (y cresce in basso):
- Health: x=c-126, y=140, w=252, h=62; 7 lampadine orizzontali, pitch36, immagine con preserveAspect. Occupano il vano superiore.
- Attack: x=c-178, y=207, w=26, h=245; 7 lampadine VERTICALI, pitch35, immagine circa22×32 centrata nella cella. Prima corsia globale x429..455, y255..500. Le successive usano il separatore SINISTRO del proprio rullo. Niente seconda colonna destra: troppo stretta e confonde le corsie.
- Guard: x=c-126, y=482, w=252, h=68; 7 lampadine orizzontali, pitch36, immagine circa32×44, centrate nel vano inferiore.
- Alloggiamenti semplici scuri (#050D0E) con filetto ottone sottile e discreto, senza coprire finestre/artwork o sequenza stati. Il totale numerico compare solo oltre7, in posizione leggibile senza sovrapporre lampadine (attacco sotto la colonna, vita/difesa sotto la relativa fila).
- Esattamente 21 lampadine/corsia, 63 totali con tre corsie. Tutti gli elementi decorativi raycastTarget=false. Rebuild idempotente.

Le finestre globali sono x460..754,811..1117,1175..1470 e y263..501. Non spostare né ridimensionare rulli, macchina, camera, tavolo, carte o pannelli.

## Semantica

Mantieni lettura dei dati veri in LateUpdate: salute attuale, atkDamage+tempAtkBonus solo Fronte e viva, ComputeSelfBlock() salvo risonanza, zero su slot morte/assenti. Le abilità SlotAdjacentBuff/SlotArmorFront/SlotBerserker possono aumentare i valori oltre la base. Non introdurre valori statici né cap a7 nella logica: a8/9 accendi7 e mostra il TOTALE8/9. Mantieni chase durante IsRolling. Programma di stati sul dorso destro invariato, l'attacco resta indicato solo dalle lampade ambra.

## Procedura ed evidenze

Leggi i proprietari mirati, poi harness_checkpoint plan con criteri osservabili (import/layout63; aggiornamento dinamico/limiti; compilazione e screenshot). Implementa modifiche mirate. Per un'estrazione utile puoi usare local_extract su breve estratto, senza duplicare contesto/deleghe.
Usa Unity MCP, non inventare un tool Python. C# CommandScript interno, IRunCommand, fully qualify UnityEngine.UI.Image (Image da solo collide nel compilatore MCP). Niente reflection. Console dopo ultima modifica; attesa reload se necessaria. Uscita da Play e FlipCardsLayoutBuilder.Rebuild() in due RunCommand separati. Cattura tramite Unity_Camera_Capture con ID della Main Camera appena letto, non SceneView. Salva prove sotto Logs/ o tools/free-router/runtime/.
Prova conteggio e sprite non nulli; campiona valori0,1,6,7,9 con bonus aggiunti/rimossi, Fronte/Retro, risonanza, morto/assente. Ripristina lo stato iniziale. Se Editor in background non avanza, campiona due frameCount e segnala il limite; puoi verificare deterministically LateUpdate con SendMessage ma non chiamarlo playtest temporizzato superato. Non alterare asset ScriptableObject di bilanciamento per test: usa bonus runtime.
Verifica visivamente tutte e tre le corsie, nessuna luce tagliata o sovrapposta al rullo. Concludi solo con evidenze reali; se non riesci dichiara precisamente ciò che manca. Non cambiare harness per far passare il controllo. Il supervisore valuterà diff, esecuzione e immagine.
