# Grafica di FlipCards

Qui dentro ci sono due cartelle, e basta.

| Cartella | Cosa contiene |
|---|---|
| [1_NeonMonte_Attivo](1_NeonMonte_Attivo/README.md) | Lo stile in uso, **Neon Monte**: solo cio' che il gioco monta, i riferimenti correnti, la specifica e gli script che generano gli asset montati. |
| [2_Archivio](2_Archivio/README.md) | Vecchi asset, prototipi, studi, sorgenti e riferimenti superati. Da qui non si monta nulla, con un'eccezione dichiarata (sotto). |

La scena attiva segue il riferimento **medaglione** del 10 settembre 2026,
`1_NeonMonte_Attivo/ArtReferences/2026-09-10_original_medallion_full.png`. La
costruisce `MedallionSceneBuilder` dal menu **FlipCards → Ricostruisci layout di
gioco**; asset montati, misure e comandi fisici sono in
[08_Integration](1_NeonMonte_Attivo/SceneKit_v2/08_Integration/README.md).

## Per tenerla ordinata

- Un asset nuovo per il gioco va in `1_NeonMonte_Attivo`. Varianti scartate,
  prove, prompt e sorgenti vanno in `2_Archivio/NeonMonte_Studi`.
- Si sposta sempre un file **insieme al suo `.meta`**, o da Unity: il GUID sta
  li', ed e' quello che tiene agganciati prefab, scena e skin.
- I percorsi nel codice partono da due costanti, `NeonMonteSkinBuilder.Root` e
  `NeonMonteSkinBuilder.Archive`. Rinominando una cartella si cambia la costante.

## L'eccezione: il vecchio kit

`2_Archivio/FlipCards_ArcadeHorrorUI` e' **ancora letto dal builder**.
`NeonMonteSkinBuilder.Prepare` parte dai suoi sprite e sostituisce quelli che
Neon Monte ridisegna: 86 voci della skin (`Assets/Resources/FlipCardsUiSkin.asset`)
puntano ancora li'. Non va cancellato finche' quelle voci non sono migrate.
