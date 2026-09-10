# Comandi fisici e integrazione

## Leva

Perno fisso in coordinate canvas: (1766, 392). Tre parti indipendenti:
`lever_pivot.png`, `lever_shaft.png`, `lever_grip.png`.
Il perno non trasla e non cambia scala. L'asse fisico è orizzontale, sul lato
destro del mobile. L'asta ruota nel piano verticale/profondità, non attorno alla Z.

La preview contiene una proiezione esplicita, portabile in Unity:

```
L = 212; cameraPitch = 20 gradi; D = 900
y = L*cos(theta); z = L*sin(theta)
depth = z*cos(cameraPitch) + y*sin(cameraPitch)
s = D/(D-depth)
handle.x = pivot.x + 0.22*z*s
handle.y = pivot.y - (y*cos(cameraPitch)-z*sin(cameraPitch))*s
```

La piccola componente orizzontale modella la vista del fianco destro. La manopola
si ingrandisce avvicinandosi, l'asta si accorcia in proiezione. L'asta è disegnata
fra perno e manopola, la manopola è uno sprite sempre rivolto alla camera.
Per una leva realmente 3D usare cilindro e sfera, asse locale X e stessa camera.

| Fase | Tempo | Angolo |
|---|---:|---:|
| Riposo | 0 | -16° |
| Trazione, ease-out cubica | 0–340 ms | -16° → 94° |
| Arresto | 340–460 ms | 94° |
| Ritorno, ease-out cubica | 460–880 ms | 94° → -16° |

La preview fa partire i rulli dopo il ritorno della leva. In Unity il comando
va inviato UNA volta, con latch immediato al click. Non rilasciare InputLocked
dal ritorno della leva: lo rilascia il flusso esistente solo a ingresso slot finito.

## Fungo

`attack_mushroom_idle.png` e `attack_mushroom_pressed.png` hanno la stessa tela.
Il basamento resta fermo e il cappello scende lungo lo stelo. Pressione 80 ms,
fermo 90 ms, ritorno 100 ms. Hover: aumento luce moderato. Disabilitato:
riduzione luminosità e saturazione, non una riduzione dell'alpha che faccia sparire il pezzo.
Il callback dell'attacco si attiva una volta sola; le azioni già in corso lo bloccano.

## Matrice stati

| Stato | Fungo | Leva | Mazzo/carte |
|---|---|---|---|
| Azioni | pronto secondo regole correnti | non pronta | secondo CanAct |
| Risoluzione | disabilitato | disabilitata | bloccati |
| AwaitingEndTurn | disabilitato | pronta | secondo regole correnti |
| Rullo/ingresso | disabilitato | animazione, input bloccato | bloccati |
| Fine partita | disabilitato | disabilitata | bloccati |
| Legenda aperta | intercettato dal modal | intercettata dal modal | intercettati |

I dati dimostrativi della preview non sostituiscono GameManager. Costi e
interactability vanno letti dai campi già esposti, senza introdurre nuove regole.

## Rulli e luci

La carta del rullo è uno sfondo neutro. I simboli sono quelli dei prefab candidati:
stessa immagine, tinta, materiale e rect al settle e allo spawn. Il pattern va
sempre ricavato dallo slot. La preview mostra uno schema esemplificativo.

Sette sedi per ciascuna statistica, con numero esatto a destra anche sopra sette.
Vita: globo a tungsteno. Attacco: siluro a filamento lungo. Difesa: tubo a scarica
con griglie. Nessuna lampadina a forma di glifo. Il valore zero spegne tutte le sedi.
Una lampada accesa vale un punto; luminosità e forma rendono distinguibili gli stati.

## Unity

Applicare la nuova geometria in `FlipCardsLayoutBuilder`, registrare gli asset
in `NeonMonteSkinBuilder` e nello skin. Non modificare a mano la scena.
PPU 1, compressione None, mipmap disattivate, alpha preservato; FilterMode Point
come le carte finali. Bordi delle UI da verificare dopo l'import.
Separare i layer decorativi dalle radici delle corsie. Non aggiungere figli
decorativi ai board root. Graphic dei figli con raycastTarget=false; hit area
dedicate ai comandi. HandZone resta alta 156, senza espansione sul campo.

Il kit consegna asset e preview grafica; il builder e la scena Unity non sono
stati modificati o verificati in Play Mode in questa consegna.
