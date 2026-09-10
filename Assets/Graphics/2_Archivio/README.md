# Archivio grafico

Vecchi asset e studi, tolti dallo stile attivo il 10 settembre 2026. Tutto e'
stato spostato insieme ai propri `.meta`: i GUID sono quelli originali.

| Cartella | Cosa c'e' |
|---|---|
| `FlipCards_ArcadeHorrorUI` (+ zip) | Il kit Arcade Horror CRT del tabellone precedente. **Ancora letto dal builder**, vedi sotto. |
| `Prototipi` | Materiale dei primi prototipi: Atariboy, CARTE E TEMPLATE, Elementals, fogli pixel, preview del kit, archivi zip e 7z. |
| `NeonMonte_Studi/ArtReferences_superate` | Riferimenti respinti: tavolo a tre occhi, cielo e velluto, variante con i bordi dei rulli. |
| `NeonMonte_Studi/Cards_Sources` | Sorgenti, studi e script delle carte finali: `Front_French_v2`, `Back_French_v2`, `SharedCardAssets`. |
| `NeonMonte_Studi/SceneKit_v1` | Studio frontale della scena, con la vecchia leva realistica in tre pezzi e la sua `animation_spec.md`. |
| `NeonMonte_Studi/SceneKit_v2_Studi` | Composizioni, immagini generate, prompt e ritagli non montati dello scene kit v2. |
| `NeonMonte_Studi/Notes`, `PreviousCards` | Note e carte delle integrazioni precedenti. |
| `NeonMonte_Studi/Runtime_superati` | Asset runtime non piu' montati: foglio delle lampade, cassa del rullo, vecchia leva, carta neutra. |

## Ancora in uso

`FlipCards_ArcadeHorrorUI/ArcadeHorrorUI/2x` e' la base della skin:
`NeonMonteSkinBuilder.Kit` punta qui, e 86 voci di
`Assets/Resources/FlipCardsUiSkin.asset` sono ancora sprite del kit (icone,
decal, maschere, fx). Nel kit c'e' anche il menu **Tools → FlipCards → Import UI
Kit**. Prima di cancellarlo quelle voci vanno ridisegnate o copiate nello stile
attivo.

## Script che scrivono nello stile attivo

Gli script Python archiviati trovano la cartella `Assets` risalendo dal proprio
percorso, e scrivono i file montati in `1_NeonMonte_Attivo`:

- `Cards_Sources/SharedCardAssets/package_final_cards.py` → `Cards/_Final`.
- `Cards_Sources/SharedCardAssets/export_unity_layout.py` → legge `Cards/_Final`,
  scrive `Assets/Scripts/UI/FinalCardLayout.cs`.
- `SceneKit_v2_Studi/_Sources/prepare_assets.py` e `project_original_medallion.py`
  → i file montati in `SceneKit_v2`; i ritagli intermedi restano qui.

Poi si ricostruisce da **FlipCards → Ricostruisci layout di gioco**.
