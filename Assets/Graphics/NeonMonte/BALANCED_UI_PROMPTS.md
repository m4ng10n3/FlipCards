# FlipCards — cornici intermedie e componenti comuni

Revisione 2026-09-08. Sostituisce la direzione ornata descritta in CABINET_FAMILY_PROMPTS.md.

Asset attivi: card_front_A/B/C.png (fronte e retro), back_seal.png (solo retro), card_neutral.png (mazzo), stat_ink_fill.png (canali comuni), slot_name_frames.png (tre targhe del nome con seme integrato), cabinet_body.png, reel_lever.png, cabinet_lamps.png (sei sprite on/off). Parti e slicing registrati da NeonMonteSkinBuilder.PrepareParts.

Le statistiche principali usano lo stesso canale e dieci divisioni uniformi (una per punto, estensione a multipli di dieci per valori superiori). Nessun numero impresso nell’asset. Il retro conserva la cornice della famiglia e il sigillo centrale comune. Le lampade mostrano vita corrente, attacco effettivo e difesa effettiva; il rullo attiva la sequenza di caricamento.

## Revisione slot: seme nella cornice del nome

Le trame slot_patterns.png sono superate e non vengono registrate nella skin. slot_name_frames.png contiene tre cornici del nome con il seme incorporato nel terminale sinistro. Il fondo slot_paper e ricavato dalla porzione avorio senza segni dello stesso foglio; nessuna trama e nessuna cornice di famiglia intorno al mostro. Nome e numero lastra restano testo runtime.

### Prompt slot_name_frames.png

Production UI sprite sheet: THREE very slim horizontal NAME FRAMES for FlipCards indie game. Three equally sized rows, stacked top to bottom on a TRUE TRANSPARENT PNG background. Each row contains one frame centered with identical outer bounds. Overall canvas landscape 2:1; each frame approximately 6:1 width:height, with small transparent gutters. Match reference card's warm ivory paper and thin charcoal double rule, hand-printed restrained woodcut, sparse distress. These are NAMEPLATES, no entire cards. Main center from x24% to94% MUST be empty clean ivory for runtime name text. Integrate the family SUIT DIRECTLY INTO THE LEFT END of the name frame: its contour grows seamlessly out of the border and connects to both the upper and lower thin rule. Top frame brick-red simple flame, middle frame muted petrol simple curling wave, bottom frame moss green forked thorn with two leaves. Emblems compact and bold readable at 20px. No separate circle, no badge placed on top, no medallion, no ornate filigree; the emblem forms the left terminal of the border itself. Right corners slightly clipped, one tiny matching color stroke only. Maintain identical geometry across all three. NO letters, numbers, text, words, monster, repeated patterns or background texture outside the small nameplates. Only the three finished blank name-frame sprites.

### Rifinitura finale dello sfondo

Edit this image: replace the entire grey checkerboard background with flat warm ivory paper matching the cream color inside the three nameplates. This time output an OPAQUE RGB sheet, all background must be warm ivory, absolutely no grey, checkerboard, black background or transparency. Preserve the three black-bordered nameplates with integrated flame, wave and thorn exactly. Keep their geometry and relative positions. Greatly reduce paper grain everywhere to subtle fine light grain, no brown stains. The finished image is three blank nameplates printed directly on one continuous warm ivory sheet, suitable to crop into rectangular game headers. No text. Only change the background and lighten grain, no scene or mockup.

## Prompt di generazione

### balancedFrontAPrompt

Create final balanced FRONT card template for FlipCards BRACI family. Image1 is TOO ORNATE, image2 is TOO BARE: find a deliberate middle ground. Exact2:3 portrait full playing card. Warm ivory printed stock, charcoal thin DOUBLE rule and muted copper/brick red accents. Border about5% width, simple short angular flame/ember flourishes only at four corners, each less than9% of card area; upper-right small integrated flame motif, no medallion. A few small etched copper geometric marks on top and bottom; NO continuous vines or curling filigree. CRITICAL shared stat architecture: two narrow continuous recessed ivory/charcoal vertical gauge channels are built into left and right margin, inside the double border. Channel inner spans: left x=8..12%, right x=88..92%; both y=24..80% of card height. Channels have subtle ink edge and plain ivory interior, rounded/slightly chamfered ends. NO preprinted segments or numbers: code draws exact equal divisions and fill in these same fixed channels for every family. Header reserve x16..79%,y7..14% blank ivory. Central portrait reserve x16..84%,y17..85% completely blank ivory. Footer reserve x17..83%,y89..94% blank ivory with just a fine rule above. Refined vintage woodcut print with limited sparse wear. No eyes, characters, letters, text, numbers, floating badges. A crafted but sober game card with INTEGRATED common stat rails, emphatically between the two references in detail.

### balancedFrontBPrompt

Exact ABISSI family variant of this balanced production card template. Preserve EVERY contour and layout measurement, especially the two identical narrow continuous vertical gauge channels, header, central opening and footer. Keep same restrained detail level and warm ivory stock. Replace only brick-red flame/ember ornament at corners with small angular muted petrol WAVE/foam motifs; corner motifs simple4-5 broad woodcut strokes, no continuous curling filigree. Top-right wave integrated into corner, no badge. Thin double black perimeter rule and tiny geometric top/bottom accents unchanged. Gauge interiors completely blank, no preprinted divisions; code supplies exact shared ticks. No text, numbers, eyes, monsters. Exact2:3, understated antique arcade print, a middle ground between plain and ornate.

### balancedFrontCPrompt

Exact ROVI family variant of this balanced production card template. Preserve EVERY contour and layout measurement, especially the two identical narrow continuous vertical gauge channels, header, central opening and footer. Keep same restrained detail level and warm ivory stock. Replace only brick-red flame/ember ornament at corners with small angular muted moss-green THORN branch motifs, each a simple forked stem and two leaves, no continuous vines or elaborate filigree. Top-right thorn integrated into corner, no badge. Thin double black perimeter rule and tiny geometric top/bottom accents unchanged. Gauge interiors completely blank, no preprinted divisions; code supplies exact shared ticks. No text, numbers, eyes, monsters. Exact2:3, understated antique arcade print, a middle ground between plain and ornate.

### neutralMasterPrompt

FlipCards production NEUTRAL card back, the master of a much simpler game style. Reference image art direction only. Exact portrait 2:3. Flat warm ivory cardstock with a single very thin imperfect charcoal rectangular rule, tiny clipped corners, wide quiet margins. Center a SMALL simple three-eye diamond stamp occupying only central 35% width and30% height: three almond eyes one over two, block printed charcoal and muted petrol. Extremely restrained sparse geometric hatch only in the central stamp. Everything else plain warm ivory with very subtle paper grain. NO filigree, no scrollwork, no leaves, no waves, no flames, no faction identity, no badge, no ornate decoration, no panels, no writing, no numbers. Visual simplicity of an authentic old playing card from an indie penny arcade, crisp broad woodcut shapes, limited three-color print. This is the visible top card of the deck and establishes the simpler style for all assets.

### statFillPrompt

Single production UI texture for an INTEGRATED VERTICAL STAT BAR in a printed vintage card. Transparent background, tall narrow aspect1:3 (will be stretched taller in Unity). One continuous vertical elongated ink inlay, occupies central80% width and95% height, slightly chamfered ends. Dark charcoal muted petrol fill, a subtle narrow ivory catchlight on left inner edge, very light block-print paper grain and tiny worn flecks. Simple elegant continuous shape, NOT a stack of rectangles, NO divisions, NO ticks, NO words, NO numbers, NO ornaments or surrounding frame. It should look like ink filling an engraved channel in the reference card. Strong flat silhouette, no outside shadow, no glow. Equal subdivisions and current fill height are applied by game code in a fixed channel; this asset supplies the material of that continuous fill.

### simpleLeverPrompt

Separate moving LEVER ARM sprite for the exact slot cabinet in reference image. Match precisely its simple hand printed charcoal outlines, muted old brass metal and lightly worn red enamel. TRANSPARENT background. Tall vertical asset aspect1:3. ONLY straight narrow brass shaft and a modest round red knob on top, plus small circular brass axle cap at bottom. No box, no pedestal, no extra base (the reference cabinet already contains it). Bottom axle cap centered x50%, y90%; top knob centered x50%,y12%; shaft joins seamlessly between. A few broad facets and one ivory highlight, flat vintage woodcut shape, very sparse wear, not photorealistic and not ornate. The round bottom cap fits inside the cabinet's existing round brass socket. Unified old arcade machine parts. No writing, no numbers, no shadows outside silhouette, no glow.

### lampAtlasFinalPrompt

Background extraction for game sprite atlas. Preserve these exact six lamp objects and EXACT 3-column 2-row grid positions. REMOVE every dark background pixel and ALL surrounding glow halos; replace background and gutters with TRUE TRANSPARENT ALPHA. Keep only the six brass socket and glass silhouettes. Do not paint a checkerboard. No shadow/glow outside the silhouettes. Top row on, bottom row off stays unchanged. This must be a transparent PNG sprite sheet for direct overlay on the petrol machine.

### slotPatternPrompt

Create a production sprite atlas for an indie printed-paper occult arcade card game. Tall portrait canvas exactly three equal horizontal panels stacked vertically, each panel landscape width:height 11:9. Panels fill canvas edge to edge with NO gaps or labels. Warm ivory paper across all. TOP panel: very subtle terracotta repeated small flame/ember marks. MIDDLE panel: very subtle teal repeated small curved wave marks. BOTTOM panel: very subtle moss green repeated little diagonal thorn sprigs. Pattern scale small, ink at 12 percent contrast on ivory, no focal emblem. Center of every panel especially quiet and blank-ish to place a monster illustration, pattern a little stronger near perimeter. Absolutely NO frames, borders, large corner ornaments, titles, lettering, numbers, icons, mockup, shadows or objects. Flat restrained ink-on-paper simple game background textures. Output only this three-panel atlas.

### backSealPrompt

Extract just the central three-eye ink emblem from this reference card as a standalone transparent PNG game sprite. Preserve its arrangement (one eye above two), charcoal outlines, muted petrol irises, tiny rays, worn ink texture. Remove the whole card frame and all paper/background; real alpha transparency everywhere outside the ink and the ivory whites of the three eyes. No checkerboard drawn in image. Center emblem occupying 85% square canvas, small transparent margin. No new ornament, no card, no text.

### cabinetAlphaPrompt

Precisely remove the grey checkerboard outside the cabinet from this image, replacing it by real PNG alpha transparency. Keep all cabinet metal, screws, brass trim, the black interior rectangular window, and right integrated lever socket identical. Do not change composition, proportions or add a lever. Only outside silhouette becomes truly transparent, including below the base and top corners. Output wide transparent game sprite, clean alpha edges, no drawn checkerboard.
