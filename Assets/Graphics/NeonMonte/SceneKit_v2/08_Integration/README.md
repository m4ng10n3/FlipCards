# Scena attiva — Medallion

Il layout in uso è costruito da `Assets/Editor/MedallionSceneBuilder.cs`, richiamato dal menu **FlipCards → Ricostruisci layout di gioco**. Riferimento: `ArtReferences/2026-09-10_original_medallion_full.png`. Misure in `layout_manifest.json`, canvas 1920×1080, tre corsie.

## Asset effettivamente montati

| Elemento | Fonte in NeonMonte |
|---|---|
| Carte approvate | `Cards/_Final` |
| Tavolo e medaglione | `SceneKit_v2/03_Table/table_original_medallion_empty.png` |
| Cassa senza targhette | `SceneKit_v2/08_Integration/cabinet_clean.png` |
| Pulsante, cappello e basamento | `SceneKit_v2/08_Integration/mushroom_clean.png`, tagliato a y=600 |
| Leva in tre pezzi | `SceneKit_v1/Controls/lever_{pivot,shaft,grip}.png` |
| Rulli della pergamena | `SceneKit_v1/Controls/lever_shaft.png`, coricato |
| Carta di libretto, pergamena e targhetta di stato | `SceneKit_v2/06_UI/legend_paper.png` (= `SceneKit_v1/UI/ivory_panel_cropped.png`) |
| Vita, attacco, guardia | `SceneKit_v2/05_Lamps`, stati on/off |

## Comandi fisici

**Leva** (`MedallionLever`): perno fermo, asta che ruota, manopola che si
avvicina alla camera. Proiezione e tempi vengono da
`SceneKit_v1/Layout/animation_spec.md` (L=212, pitch 20°, D=900; −16° → 94° in
340 ms, fermo 120, ritorno 420). Il builder la monta a riposo con `ApplyRest()`:
in edit mode il ciclo di gioco non gira e senza quella chiamata i tre pezzi
resterebbero accatastati sul perno.

**Attenzione alle tele degli sprite**: nell'asta il tondino d'ottone occupa il
**14,4%** della larghezza della tela, il resto e' trasparente. Dimensionare il
rect sulla larghezza dell'immagine da un'asta spessa un capello: la larghezza
voluta va divisa per 0,144.

**Fungo** (`TableControlFeedback.cap`): `mushroom_clean.png` e' un disegno solo,
tagliato in cappello (0..600) e basamento (600..1254) da `MedallionSceneSkin.Slice`.
Il basamento sta davanti, cosi' il cappello che scende sparisce dietro la
ghiera. Il fotogramma premuto di `SceneKit_v1` **non e' utilizzabile con questa
cassa**: e' un altro bottone, disegnato a meta' scala sulla stessa tela.

## Lampade

Le sedi seguono i valori veri delle caselle, non le sette per statistica della
specifica del kit: vita 3–5 → **7 sedi**, attacco 1–3 → **3 sedi**, guardia 0–5 →
**5 sedi larghe**. Il tubo a scarica e' quasi quadrato e va sempre con
`preserveAspect`: senza, veniva stirato in una scheggia e le griglie sparivano.
Oltre le sedi disponibili compare il totale numerico, come prima.

`MedallionSceneSkin` configura gli import e registra gli sprite nella skin runtime. La cassa generata usa `CabinetMasked.mat` e l'alpha dell'originale `cabinet_illustrated.png`: i due raster restano integri. Il pulsante generato ha sfondo trasparente.

Le istruzioni dei comandi e le targhette sono rimosse dal tavolo. Leva, pulsante rosso e mazzo sono i controlli reali.

## Superfici di lettura

Non sono piu' due stati di un rettangolo trasparente, ma due oggetti con la
loro forma.

**Dettaglio** e' un **libretto**: chiuso sta sul tavolo oltre il mazzo — piu'
lontano dal giocatore, perche' e' roba da consultare, non da giocare — e aperto
mostra due pagine d'avorio, dorso di pelle opaco e linguette d'indice sul taglio
esterno (Campo, Mano, Rullo, Registro).

**Legenda** e' una **pergamena**: chiusa e' un rotolo in alto a destra, e al
clic va al centro e si srotola (`MedallionScroll`). I due rulli d'ottone stanno
**dentro** il contenuto scorrevole, non ai bordi della finestra: da qui la
regola che se il testo supera l'altezza se ne vede uno per volta, e se ci sta si
vedono tutti e due.

Le pagine sono chiare, quindi il testo e' in inchiostro: `GamePalette.Ink*`,
`InkFaction` e `InkSide`, e le costanti di `InspectorPanel` sono le stesse
letture di prima scurite. Chiudi, Esc e clic esterno chiudono; AP e selezione di
gioco restano intatti.

## Generazione e fonti

`cabinet_clean.png` e `mushroom_clean.png` sono modifiche prodotte con lo strumento integrato **image_gen**, senza CLI. Prompt conservati rispettivamente in `generation_prompt.json` e `mushroom_generation_prompt.json`. Gli originali restano nelle cartelle sorgenti; SceneKit_v1 è lo studio precedente.

Verifica MCP e interazioni: `Logs/medallion-verification.txt` nella radice del progetto. Catture di controllo nella stessa cartella `Logs`.
