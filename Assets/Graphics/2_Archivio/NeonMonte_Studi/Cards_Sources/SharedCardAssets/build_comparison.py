"""Render the current two-face proof and validate shared tally silhouettes."""
from pathlib import Path
import json
import numpy as np
from PIL import Image,ImageDraw,ImageFont
from tally_shapes import SIZES,tint,INK,CREAM,build_tallies,verify,geometry
ROOT=Path(__file__).resolve().parent
FRONT=ROOT.parent/'Front_French_v2'
BACK=ROOT.parent/'Back_French_v2'
front=Image.open(FRONT/'Template/front_sun_preview.png').convert('RGBA')
back=Image.open(BACK/'Template/back_sun_preview.png').convert('RGBA')
sheet=Image.new('RGB',(1520,1080),INK);d=ImageDraw.Draw(sheet)
def font(size):return ImageFont.truetype('C:/Windows/Fonts/consola.ttf',size)
d.text((30,20),'NEON MONTE / TACCHE A GLIFO',font=font(30),fill=CREAM)
d.text((30,60),'Vita = goccia   Attacco = lancia romboidale   Difesa = scudo',font=font(21),fill=(153,182,172))
for image,x,label in [(front,30,'FRONT'),(back,450,'BACK')]:
    thumb=image.resize((400,600),Image.Resampling.LANCZOS)
    sheet.paste(thumb,(x,105),thumb)
    d.text((x,720),label,font=font(24),fill=CREAM)
d.text((910,105),'DIMENSIONE DI GIOCO',font=font(23),fill=CREAM)
for image,x in [(front,910),(back,1170)]:
    thumb=image.resize((224,336),Image.Resampling.LANCZOS)
    sheet.paste(thumb,(x,154),thumb)
d.text((910,510),'224 x 336',font=font(20),fill=CREAM)
d.text((910,566),'7 tacche per colonna',font=font(21),fill=CREAM)
d.text((910,605),'Nessuna intestazione statistica',font=font(18),fill=CREAM)
d.text((910,644),'Punte sempre verso il centro',font=font(18),fill=CREAM)
tallies=build_tallies('front')
for col,(name,label) in enumerate([('drop','VITA'),('attack','ATTACCO'),('defense','DIFESA')]):
    x=30+col*495
    d.rounded_rectangle((x,785,x+460,1008),radius=12,fill=CREAM)
    for i,state in enumerate(['full','empty']):
        im=tint(tallies[name+'_'+state],INK)
        im=im.crop(im.getchannel('A').getbbox()).resize(SIZES[name],Image.Resampling.LANCZOS)
        sheet.paste(im,(x+125+i*208-im.width//2,882-im.height//2),im)
    d.text((x+22,966),label+'  '+str(SIZES[name][0])+' x '+str(SIZES[name][1]),font=font(22),fill=INK)
d.text((30,1030),'Goccia originale, lancia romboidale e scudo: stesso ingombro, stati pieno/vuoto.',font=font(20),fill=CREAM)
sheet.save(ROOT/'FRONT_BACK_COMPARISON.png')

qa=verify(tallies)
for name in ['drop','attack','defense']:
    for state in ['full','empty']:
        f=Image.open(FRONT/'Indicators'/f'{name}_{state}.png').getchannel('A')
        b=Image.open(BACK/'Indicators'/f'{name}_{state}.png').getchannel('A')
        qa[name+'_'+state+'_same_alpha_both_faces']=bool(np.array_equal(np.asarray(f),np.asarray(b)))
for face,folder in [('front',FRONT),('back',BACK)]:
    m=json.loads((folder/'layout_manifest.json').read_text(encoding='utf-8'))
    report=json.loads((folder/'QA_REPORT.json').read_text(encoding='utf-8'))
    qa[face+'_no_stat_headers']=m['stat_headers']==[]
    qa[face+'_seven_each_side']=m['left_tallies']['count']==m['right_tallies']['count']==7
    qa[face+'_no_pip_frame_overlap']=report['pip_frame_overlap_pixels']==0
assert all(qa.values()),qa
report={'checks':qa,'all_passed':all(qa.values()),'glyphs':{n:geometry(n,tallies[n+'_full']) for n in SIZES},'preview_size':[224,336]}
(ROOT/'QA_REPORT.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
readme='''# Tacche condivise — goccia, lancia romboidale, scudo semplice

Decisione corrente: le tacche identificano direttamente la statistica. Nessun simbolo separato di intestazione in front o back. Sette stazioni per colonna, stati pieno/vuoto e PNG con alpha reale.

Aprire FRONT_BACK_COMPARISON.png per il confronto completo, incluse le riduzioni a 224×336 e i tre glifi in tinta unica. QA_REPORT.json verifica anche che fronte e retro condividano esattamente le sagome alpha.

## Sorgenti e proporzioni

- Sources/attack_rhombic_spear.png: lancia romboidale originale a due facce contigue, specchiata orizzontalmente per rivolgere la punta a sinistra.
- Sources/defense_simple_shield_upright.png: scudo semplice originale, ruotato di 90° in senso orario per rivolgere la punta inferiore a sinistra.
- Sources/drop_before_rebalance.png: goccia originale ripristinata senza alterare la sagoma o le proporzioni 192×120, punta a destra.

Ingombro comune determinato dalla goccia originale: 94×59 sul fronte, 108×68 sul retro per tutte le tacche. Le punte sono riallineate alle cornici. Le misure per ciascuna faccia sono definite in FACE_SIZES.

Le tre sagome condividono le proporzioni originali della goccia. Ogni manifest di faccia fornisce il bounding box alpha e la misura dell'intera cella sprite da usare. I pieni e i vuoti sono ricavati dalla stessa sagoma; i vuoti mantengono gli ingombri e il pivot dei pieni.

## Ricostruzione

Con Python, Pillow e NumPy, eseguire in ordine:

1. ../Front_French_v2/build_assets.py
2. ../Back_French_v2/build_assets.py
3. build_comparison.py

tally_shapes.py è la fonte comune. I due builder leggono gli stessi master senza eseguire il codice dell'altra faccia. Il retro conserva il suo layout sagomato indipendente, il fronte la cornice francese. I vecchi simboli restano disponibili come sorgenti storiche e per l'insegna, ma non sono montati come intestazioni statistiche.

Nessuna nuova generazione image_gen per questa revisione: riuso dei master precedentemente generati e rifinitura geometrica richiesta dall'utente. Nessun prefab, scena o codice di combattimento modificato.
'''
(ROOT/'README.md').write_text(readme,encoding='utf-8')
print(json.dumps({'all_passed':report['all_passed'],'checks':len(qa),'comparison':str(ROOT/'FRONT_BACK_COMPARISON.png')},indent=2))
