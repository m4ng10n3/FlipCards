# Grafica attiva di FlipCards

La direzione grafica in uso è **Neon Monte**.

- [Carte finali e organizzazione](NeonMonte/README.md): `NeonMonte/Cards/_Final` è il set montato nei 10 prefab; `NeonMonte/Runtime` raccoglie le risorse condivise.
- `NeonMonte/Cards/_Sources` conserva i sorgenti riproducibili; `NeonMonte/_Archive` conserva revisioni e prove precedenti.
- `FlipCards_ArcadeHorrorUI` resta una dipendenza del builder per geometria e risorse UI di base. Non è il set di carte attivo.

Il montaggio è scritto in `NeonMonteSkinBuilder` e `FlipCardsLayoutBuilder`, e registrato in `Assets/Resources/FlipCardsUiSkin.asset`. Per rigenerarlo: **FlipCards → Ricostruisci layout di gioco**.

Verifica MCP Unity del 9 settembre 2026: 10 prefab con fronte e retro finali, statistiche dinamiche 0/3/7/9, cariche, assenza di duplicati e raycast sui figli, un ciclo attacco/rullo concluso al turno 2. Report in `Logs/final-assets-verification.txt`; cattura della Main Camera in `Logs/final-assets-verified.png`.
