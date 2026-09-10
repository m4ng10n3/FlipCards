# FlipCards — Bisca indie

## Carte: asset finali approvati

La consegna corrente è in [Cards_Final](Cards_Final/README.md): front avorio con cornice tatuata più scura, back rosso con scudi allocati dall'alto, tre fazioni, sprite e atlanti, manifest di montaggio e anteprime. Il separatore del testo è conservato soltanto tra gli asset opzionali. [Anteprima finale](Cards_Final/FINAL_PREVIEW.png).

Le cartelle Front_French_v2, Back_French_v2 e SharedCardAssets conservano sorgenti, prompt, studi storici e builder riproducibili. Per la produzione usare Cards_Final; la consegna degli asset non modifica automaticamente prefab o scene Unity. La documentazione sotto descrive l'implementazione e le revisioni precedenti.

## Carte: implementazione precedente a filetti incisi

La versione corrente usa `card_engraved_base.png`: rosso, ellissi e centro avorio. `card_engraved_example.png` e il campione completo; `card_engraved_parts.png` e il foglio dei simboli e dei tratti, ritagliato geometricamente nella libreria `Assets/Resources/EngravedInkLibrary.asset`. `EngravedOrnament` e `MarginTally` compongono il disegno dinamico: rombo = un punto, linea fine = punto assente. Non si usano piu i canali da riempire o `stat_ink_fill.png`.

La spada cambia in scudo sul retro e segue la parata effettiva, inclusi insegne e risonanza; il cuore mostra la vita corrente. Le tre cariche restano una decorazione continua anche a zero e nella mano. Il seme si innesta nel filetto del titolo; l?abilita e centrata nell?ellisse inferiore. Foil del bordo e del personaggio, interazioni e carta neutra del mazzo restano attivi. Prompt e specifica di montaggio: `ENGRAVED_CARD_PROMPTS.md`.

Le sezioni successive conservano la storia delle revisioni precedenti; per la nuova consegna grafica delle carte fa fede Cards_Final/README.md. Le slot usano ancora il seme nella cornice del nome e fondo neutro.


Riferimento approvato: `ArtStudies/indie-concept-study.png`. Carta avorio consumata, inchiostro scuro, rosso mattone, petrolio e verde muschio; mostri e simboli come stampe pixel art.

## Asset

- `portrait_*.png`: dieci creature delle carte, collegate ai prefab per nome.
- `symbol_*.png`: dieci simboli delle slot, usati anche dal rullo attraverso i prefab candidati.
- `card_front.png` e `card_back.png`: supporto avorio e dorso a tre occhi. Le informazioni sono testo e UI reali, non incorporate nelle immagini.
- `board.png`, `reel_housing.png`: tavolo e cassa del rullo.
- `glyph_sword.png`, `glyph_shield.png`, `glyph_broken.png`: insegna e risonanza.
- `Chrome/`: UI deterministica costruita da NeonMonteSkinBuilder.
- `Fonts/`: VT323, SIL Open Font License; licenza inclusa. Testi descrittivi piccoli mantengono il font leggibile esistente.

## Impaginazione e materiale

Nome nel bordo alto e motivo di famiglia inciso nella cornice; artwork 144x212 al centro. Attacco (fronte) o guardia (retro) nel margine sinistro, vita nel destro: canali fissi divisi in dieci parti uguali, una tacca piena per punto; le ferite lasciano divisioni vuote. Le tre cariche rosa restano nello stesso posto sulle due facce, sopra la fascia abilita. Sul retro scompaiono nome e abilita: l'insegna usa glifi e tacche del colore di fazione, senza textbox. Il mazzo usa copie decorative dei dorsi senza overlay informativi.

Edizioni Regular (Anchor, Reaper), Foil (Relay, Bulwark, Hex, Bastion) e Polychrome (Vanguard, Oracle, Spark, Scythe). Il ritratto mantiene CardShaderGraph originale e intensita polychrome 0.3. `FoilBorder` disegna quattro strisce strette usando lo stesso materiale runtime del ritratto, aggiornato dal tilt; il supporto centrale resta PrintedPaper e le informazioni sono inchiostro opaco sopra il riflesso. Il materiale condiviso viene gestito e distrutto da ShaderCode. Le edizioni non modificano regole o statistiche.

I fondi fronte/dorso e slot_face.png sono rigenerati con image_gen integrato; prompt completi in `TEMPLATE_PROMPTS.md`. `MarginTally` genera mesh UI con CanvasRenderer, senza raycast. Le slot hanno artwork centrale 216x216 e fondo avorio neutro; il seme forma il terminale della cornice del nome; vita, attacco e blocco vivono negli strumenti della cassa descritti sotto. Nessuna colonna o tabella: fondo avorio continuo. Il numero della lastra diventa rosso quando ferita; i segni stampati del programma sono centrati sul bordo inferiore, con il giro corrente sottolineato, e la risonanza resta sotto la fazione. Le lampadine sono elementi della cassa; il rullo porta solo identita, programma e risonanza.

## Ricostruzione

Menu **FlipCards → Ricostruisci layout di gioco**. Il builder importa gli sprite con filtro Point, PPU 1, senza mipmap o compressione, prepara font/materiale, aggiorna prefab e ricostruisce la scena. Le tre corsie conservano dimensioni e gerarchia funzionale. Non modificare manualmente scena o immagini Chrome.

Prompt: `PROMPTS.md` e `MONSTER_PROMPTS.md`; generazione tramite image_gen integrato. Studi scartati fuori da Assets, in ArtStudies.

La manovella reel_lever.png segue IsRolling tramite ReelLever. Campo largo 1100, passo corsie 370, centri x=496/866/1236: entrambi i lati e il pronostico condividono la stessa geometria. Dimensioni carta 224x336 e slot 352x288 invariate.

Le famiglie sono Braci (fiamma corallo), Abissi (onda acqua) e Rovi (ramo spinoso muschio). Gli emblemi semplificati in GlyphSprites restano nella legenda; sulle carte la famiglia e raffigurata negli angoli della cornice, sulle slot nella cornice del nome; nomi coerenti in ispettore e log. Gli identificativi serializzati A/B/C restano interni per preservare dati e regole.

## Cornici di famiglia e strumenti della cassa

Le cornici intermedie `card_front_A/B/C.png` condividono geometria, doppio filetto e due canali verticali. Solo i piccoli motivi degli angoli cambiano: fiamme, onde, rami spinosi. Il retro riutilizza la stessa cornice con `back_seal.png`, senza cambiare posizione delle statistiche. Il mazzo mostra `card_neutral.png`, privo di fazione. `MarginTally` suddivide il canale fisso e usa UV continui di `stat_ink_fill.png`, con scala comune 0?10 (estesa a decine oltre dieci).

`slot_name_frames.png` contiene le tre cornici del nome con il seme integrato. `slot_paper` usa una porzione neutra dello stesso foglio. Le precedenti trame `slot_patterns.png` sono escluse dalla skin. `cabinet_body.png` viene importato con una suddivisione a nove sezioni che conserva la sede della leva; `reel_lever.png` fornisce la parte mobile. `cabinet_lamps.png` contiene le sei lenti acceso/spento, suddivise in sprite persistenti sotto Chrome. Prompt correnti e percorsi in `BALANCED_UI_PROMPTS.md`; `CABINET_FAMILY_PROMPTS.md` documenta una direzione precedente, superata.

`ReelInstruments` monta le statistiche sulla cassa, separate dalle facce in movimento: vita con lenti tonde rosse in alto, attacco con lenti a punta ambra in basso a sinistra, difesa con lenti a scudo acqua in basso a destra. Una lampadina accesa vale un punto. Attacco mostra il colpo attivo (zero durante una pausa); difesa mostra la parata effettiva (zero in risonanza). L'ispettore conserva valori base e spiegazioni. Le luci scorrono durante `IsRolling`, poi si riempiono sul valore corrente; una perdita di HP spegne le lenti corrispondenti. Sul rullo rimangono nome, numero lastra, programma sottolineato e risonanza. Tutta la strumentazione ignora i raycast.

`DropPreview` usa lo stesso bersaglio risolto dai gestori di drop per accendere angoli e indicazione di rilascio, sia per giocare dalla mano sia per scambiare in campo. `HandTray.SetDraggedCard` abbassa le altre carte; `CardView.LateUpdate` mantiene quella trascinata nelle coordinate del puntatore senza riparentare gli oggetti o ampliare l'area di input.
