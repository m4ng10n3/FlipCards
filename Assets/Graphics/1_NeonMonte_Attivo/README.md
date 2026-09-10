# Neon Monte — lo stile in uso

Scena attiva: **medaglione, 10 settembre 2026**
(`ArtReferences/2026-09-10_original_medallion_full.png`). Asset montati, misure e
comandi fisici: [08_Integration](SceneKit_v2/08_Integration/README.md). Vecchi
asset, studi e sorgenti stanno fuori da qui, in [2_Archivio](../2_Archivio/README.md).

## Cosa c'e'

| Cartella | Contenuto |
|---|---|
| `ArtReferences` | Solo i riferimenti correnti: medaglione pieno e vuoto, composizione approvata. |
| `Cards/_Final` | Il set delle carte, consegna del 9 settembre 2026 ([anteprima](Cards/_Final/FINAL_PREVIEW.png)). |
| `Runtime` | Ritratti, simboli degli slot, glifi, font e materiali. `Chrome` e' rigenerato dal builder. `Shared` tiene due texture tecniche ereditate: `squareCircle_vector` nei prefab carta e `1FIORE` nel materiale `CardShaderGraph`. |
| `SceneKit_v2` | La scena: `02_Machine` cassa e faccia del rullo, `03_Table` tavolo, `04_Controls` leva, `05_Lamps` lampade, `06_UI` carta dei pannelli, `07_Spec` direzione artistica, `08_Integration` montaggio e `Tools` per rigenerare gli asset derivati. |

## Carte

- `Cards/_Final/Front` e `Back`: basi pulite e sprite di produzione. Vita a
  gocce, attacco a lance, difesa a scudi; sette posizioni per colonna. Nessuna
  preview con numeri incorporati viene montata nel gioco.
- Il fronte usa `Front/Template/front_clean.png`; il retro e il mazzo usano
  `Back/Template/back_clean.png`. I ritratti restano in primo piano con le
  rispettive edizioni foil/polychrome. Le fazioni sono A = Sole, B = Luna,
  C = Saturno, anche sui semi delle caselle nemiche.
- `FinalCardLayout.cs` contiene le misure dai manifest, scalate da 1024×1536 a
  224×336; `FinalCardInk.cs` monta le due facce e aggiorna le statistiche. Sul
  fronte la vita si riempie dall'alto e l'attacco dal basso; sul retro vita e
  difesa dall'alto. Oltre sette punti compare il totale numerico, cosi' i bonus
  non vengono nascosti. I figli grafici non intercettano il mouse.
- `Optional/TextTopLimiter` resta opzionale e non e' montato.

Import: PNG Point, senza compressione o mipmap, PPU 1 per le carte; gli asset di
scena (`SceneKit_v2`) sono Bilinear, e li importa `MedallionSceneSkin`.

## Ricostruire

`NeonMonteSkinBuilder.Prepare()` registra gli sprite in
`Assets/Resources/FlipCardsUiSkin.asset`; `FlipCardsLayoutBuilder.Rebuild()`
aggiorna prefab e scena. Usare **FlipCards → Ricostruisci layout di gioco**. Non
modificare a mano la scena ricostruibile.

Gli asset delle carte si rigenerano dagli script in
`2_Archivio/NeonMonte_Studi/Cards_Sources` (vedi il README dell'archivio): scrivono
di nuovo in `Cards/_Final`. Faccia del rullo e pezzi della leva si rigenerano da
`SceneKit_v2/08_Integration/Tools`.
