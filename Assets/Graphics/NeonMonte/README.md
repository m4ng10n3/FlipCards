# Neon Monte ? grafica in uso

**Set attivo: Cards/_Final, consegna del 9 settembre 2026.**
[Anteprima delle due facce](Cards/_Final/FINAL_PREVIEW.png).

- `Cards/_Final/Front` e `Back`: basi pulite e sprite di produzione. Vita a gocce, attacco a lance, difesa a scudi; sette posizioni per colonna. Nessuna preview con numeri incorporati viene montata nel gioco.
- `Runtime/Portraits`, `Slots`, `Board`, `Glyphs`, `Fonts`, `Materials`: illustrazioni e risorse condivise in uso.
- `Runtime/Chrome`: elementi UI derivati, rigenerati dal builder.
- `Cards/_Sources`: sorgenti, studi e script riproducibili; non sono la scelta da montare nei prefab.
- `_Archive`: precedenti carte, prove e documentazione storica, conservate con i loro GUID.

Il fronte usa `Front/Template/front_clean.png`; il retro e il mazzo usano `Back/Template/back_clean.png`. I ritratti rimangono in primo piano con le rispettive edizioni foil/polychrome. Le fazioni sono A = Sole, B = Luna, C = Saturno, anche sui simboli della UI e delle caselle nemiche.

`NeonMonteSkinBuilder.Prepare()` registra gli sprite in `Assets/Resources/FlipCardsUiSkin.asset`; `FlipCardsLayoutBuilder.Rebuild()` aggiorna prefab e scena. Usare **FlipCards ? Ricostruisci layout di gioco**. Non modificare a mano la scena ricostruibile.

`FinalCardLayout.cs` contiene le misure dai manifest, scalate da 1024?1536 a 224?336; `FinalCardInk.cs` monta le due facce e aggiorna le statistiche. Sul fronte la vita si riempie dall'alto e l'attacco dal basso; sul retro vita e difesa dall'alto. Oltre sette punti compare il totale numerico, cos? i bonus non vengono nascosti. Insegne numeriche e cariche restano dinamiche. I figli grafici non intercettano il mouse.

`Optional/TextTopLimiter` resta opzionale e non ? montato. PNG Point, senza compressione o mipmap, PPU 1; colori originali per le carte, maschere per i simboli della UI.

Per ricostruire gli asset, dalla cartella `Cards/_Sources`: eseguire `Front_French_v2/build_assets.py`, `Back_French_v2/build_assets.py`, `SharedCardAssets/build_comparison.py`, `SharedCardAssets/package_final_cards.py`. L'ultimo scrive in `Cards/_Final`. Eseguire `SharedCardAssets/export_unity_layout.py` per sincronizzare le misure C# dai manifest, poi ricostruire il layout Unity. Le note precedenti sono conservate in `_Archive/Notes/README_before_final_integration.md`.
