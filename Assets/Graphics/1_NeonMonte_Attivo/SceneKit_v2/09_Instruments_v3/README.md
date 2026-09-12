# Lampade v3

Atlas originale generato con ImageGen per la cassa Neon Monte: sei lampade in ottone inciso, con vetro rosso (vita), ambra (attacco) e ciano (difesa); prima riga spenta, seconda accesa. Dimensione 1536 × 1024. Il fondo petrolio è opaco e intenzionale: i ritagli si montano dentro alloggiamenti scuri.

Direzione e misure di integrazione: `tools/free-router/brief-lamps-v3.md` nella radice del progetto. Il taglio in sprite persistenti è responsabilità del builder della skin. Non scalare l'atlas prima dell'importazione.

## Prompt di generazione

Production sprite atlas, 1536x1024, three columns and two rows, six isolated compact slot-machine indicator lamps matching the supplied engraved brass and deep-petrol illustrated cabinet. Columns: round red health glass; compact upright amber bullet-shaped attack tube; broad cyan defense discharge tube with two fine-grid electrodes. Top row OFF, bottom row ON. Same silhouette, alignment and scale between states; chunky brass sockets and strong ink outlines, legible at small UI sizes. No text, numbers, labels or extra objects.

## Prompt della rifinitura finale

Edit this exact 1536x1024 production sprite atlas. Preserve ALL SIX lamp objects, their positions, shapes, colors, luminosity, and 3-column 2-row arrangement precisely. Replace EVERY checkerboard pixel surrounding the six isolated objects with perfectly uniform opaque near-black RGB #050D0E (deep petrol black), including between lamps and all margins. No checkerboard anywhere. These are opaque socket tiles to be placed on a matching dark slot-machine instrument housing, so use the specified solid near-black background, NOT transparency, NOT white, NOT a checkerboard. Preserve the dark top-row OFF lamps and bright bottom-row ON lamps. No changes to lamp silhouettes, no text, no new objects, no vignette. Flat consistent background all the way to every edge.
