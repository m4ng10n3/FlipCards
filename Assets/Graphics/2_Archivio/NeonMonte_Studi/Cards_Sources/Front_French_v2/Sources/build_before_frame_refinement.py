"""Build the French front with self-describing glyph tallies, no stat headers."""
from pathlib import Path
import sys,json
import numpy as np
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT.parent))
from SharedCardAssets.tally_shapes import export_tallies,geometry,verify,SIZES
W,H,S=1024,1536,4
INK=(25,33,36);RED=(181,43,38)
LEFT_RAIL,RIGHT_RAIL,DEPTH=140,884,16
LEFT_PIP,RIGHT_PIP=94,930
FACTION_Y=180
RY=[180+166*i for i in range(7)]
LY=[H-y for y in reversed(RY)]
FACTIONS=[('sun','SOLE',RED),('moon','LUNA',(61,123,117)),('saturn','SATURNO',(108,117,66))]

def save(im,name):
    p=ROOT/name;p.parent.mkdir(parents=True,exist_ok=True);im.save(p)
def stamp(layer,im,center,size,rotate=False):
    im=im.crop(im.getchannel('A').getbbox()).resize(size,Image.Resampling.LANCZOS)
    if rotate:im=im.transpose(Image.Transpose.ROTATE_180)
    layer.alpha_composite(im,(round(center[0]-im.width/2),round(center[1]-im.height/2)))
def bezier(a,b,c,d):
    return [tuple((1-t)**3*a[k]+3*(1-t)**2*t*b[k]+3*(1-t)*t*t*c[k]+t**3*d[k] for k in (0,1)) for t in np.linspace(0,1,32)]
def font(size):return ImageFont.truetype('C:/Windows/Fonts/consola.ttf',size)

paper=Image.open(ROOT/'Template/front_paper.png').convert('RGBA')
sprites={n:Image.open(ROOT/'Symbols'/f'{n}.png').convert('RGBA') for n in ['faction_sun','faction_moon','faction_saturn','star','charge_ring','charge_full']}
tallies=export_tallies(ROOT/'Indicators','front')
points=[(250,FACTION_Y),(250,145)]
points+=bezier((250,145),(250,104),(276,78),(319,78))
points+=[(753,78)]
points+=bezier((753,78),(811,78),(RIGHT_RAIL,103),(RIGHT_RAIL,145))
for y in RY:
    points+=[(RIGHT_RAIL,y-32)]
    points+=bezier((RIGHT_RAIL,y-32),(RIGHT_RAIL,y-16),(RIGHT_RAIL-8,y-6),(RIGHT_RAIL-DEPTH,y))
    points+=bezier((RIGHT_RAIL-DEPTH,y),(RIGHT_RAIL-8,y+6),(RIGHT_RAIL,y+16),(RIGHT_RAIL,y+32))
points+=[(RIGHT_RAIL,1224)]
half=Image.new('RGBA',(W*S,H*S))
ImageDraw.Draw(half).line([(round(x*S),round(y*S)) for x,y in points],fill=INK+(255,),width=4*S,joint='curve')
frame_hi=Image.alpha_composite(half,half.transpose(Image.Transpose.ROTATE_180))
ImageDraw.Draw(frame_hi).rounded_rectangle((31*S,22*S,993*S,1514*S),radius=40*S,outline=INK+(255,),width=4*S)
frame=frame_hi.resize((W,H),Image.Resampling.LANCZOS)
fa=np.asarray(frame).copy();fa[:,:,3]=np.maximum(fa[:,:,3],fa[::-1,::-1,3]);fa[:,:,:3]=INK
frame=Image.fromarray(fa);save(frame,'Template/front_frame.png')
path='M'+' L'.join(f'{x:.3f},{y:.3f}' for x,y in points)
(ROOT/'Template/front_frame.svg').write_text(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1024 1536"><g fill="none" stroke="#192124" stroke-width="4" stroke-linejoin="round"><rect x="31" y="22" width="962" height="1492" rx="40"/><path d="{path}"/><path transform="rotate(180 512 768)" d="{path}"/></g></svg>',encoding='utf-8')
base=Image.alpha_composite(paper,frame);save(base,'Template/front_clean.png')
backplates=Image.new('RGBA',(W,H))
for x in [466,512,558]:
    patch=paper.crop((x-16,1442,x+16,1474))
    mask=Image.new('L',(128,128));ImageDraw.Draw(mask).ellipse((0,0,127,127),fill=255)
    patch.putalpha(mask.resize((32,32),Image.Resampling.LANCZOS));backplates.alpha_composite(patch,(x-16,1442))
save(backplates,'Template/charge_backplates.png')

def assemble(faction='sun',health=3,attack=4,charges=0):
    ink=Image.new('RGBA',(W,H))
    cell=sprites['faction_'+faction].resize((224,224),Image.Resampling.LANCZOS)
    ink.alpha_composite(cell,(140-112,FACTION_Y-112))
    ink.alpha_composite(cell.transpose(Image.Transpose.ROTATE_180),(884-112,H-FACTION_Y-112))
    stamp(ink,sprites['star'],(248,73),(45,45))
    stamp(ink,sprites['star'],(776,1463),(45,45),True)
    for i,y in enumerate(LY):
        stamp(ink,tallies['drop_full' if i<health else 'drop_empty'],(LEFT_PIP,y),SIZES['drop'])
    for i,y in enumerate(RY):
        stamp(ink,tallies['attack_full' if i>=7-attack else 'attack_empty'],(RIGHT_PIP,y),SIZES['attack'])
    ink.alpha_composite(backplates)
    for i,x in enumerate([466,512,558]):
        stamp(ink,sprites['charge_full' if i<charges else 'charge_ring'],(x,1458),(32,32))
    return Image.alpha_composite(base,ink),ink
previews={}
for faction,_,_ in FACTIONS:
    preview,overlay=assemble(faction);previews[faction]=preview
    save(preview,f'Template/front_{faction}_preview.png');save(overlay,f'Template/front_{faction}_overlay.png')
empty,empty_overlay=assemble(health=0,attack=0)
save(empty,'Template/front_empty_preview.png');save(empty_overlay,'Template/front_empty_overlay.png')

items={**sprites,**{n:tallies[n] for n in ['drop_full','drop_empty','attack_full','attack_empty']}}
atlas=Image.new('RGBA',(1024,1024));entries={}
for i,(name,im) in enumerate(items.items()):
    x,y=(i%4)*256,(i//4)*256;atlas.alpha_composite(im,(x,y))
    entries[name]={'rect_top_left':[x,y,256,256],'pivot':[.5,.5]}
save(atlas,'symbols_normalized_atlas.png')
manifest={
    'layout_revision':'light-tallies-v5','canvas':[W,H],'coordinates':'top-left origin, y down','runtime_card_size':[224,336],
    'stat_headers':[],'clean_template':'Template/front_clean.png','clean_has_baked_stats':False,
    'symbol_canvas':[256,256],'indicator_canvas':[256,256],
    'faction_centers':[[140,FACTION_Y],[884,H-FACTION_Y]],'faction_full_sprite_render_size':[224,224],'second_faction_rotation_degrees':180,
    'inner_vertical_open_endpoints':[[250,FACTION_Y],[W-250,H-FACTION_Y]],
    'inner_vertical_endpoint_rule':'Y equals the center Y of the adjacent faction symbol',
    'faction_designs':[{'id':f,'design_label':l,'file':f'Symbols/faction_{f}.png','preview':f'Template/front_{f}_preview.png'} for f,l,_ in FACTIONS],
    'inner_margin':{'left_axis':LEFT_RAIL,'right_axis':RIGHT_RAIL,'scallop_depth':DEPTH,'cusp_span':64,'stroke':4},
    'left_tallies':{'centers':[[LEFT_PIP,y] for y in LY],'cusps':[[LEFT_RAIL+DEPTH,y] for y in LY],'count':7,'step':166,'full':'Indicators/drop_full.png','empty':'Indicators/drop_empty.png',**geometry('drop',tallies['drop_full'])},
    'right_tallies':{'centers':[[RIGHT_PIP,y] for y in RY],'cusps':[[RIGHT_RAIL-DEPTH,y] for y in RY],'count':7,'step':166,'full':'Indicators/attack_full.png','empty':'Indicators/attack_empty.png','source':'../SharedCardAssets/Sources/attack_rhombic_spear.png',**geometry('attack',tallies['attack_full'])},
    'safe_regions':{'title':[300,112,790,214],'artwork':[254,282,770,1254],'ability':[234,1322,724,1424]},
    'charges':[[466,1458],[512,1458],[558,1458]],'charge_backplates':'Template/charge_backplates.png',
    'atlas':entries,'import':{'textureType':'Sprite','filterMode':'Point','compression':'None','mipmaps':False,'pixelsPerUnit':1,'alphaIsTransparency':True},
    'notes':'No heart or attack header: glyph pips identify the statistic. Drop right; rhombic spear point mirrored left. No artwork/title/ability baked into clean base.'
}
(ROOT/'layout_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
qa=verify(tallies)
qa.update({'stat_headers':0,'left_count':7,'right_count':7,'step':166,
    'inner_vertical_ends_at_faction_center':points[0][1]==FACTION_Y and all(p[1]==f[1] for p,f in zip(manifest['inner_vertical_open_endpoints'],manifest['faction_centers'])),
    'frame_rotation_equal':bool(np.array_equal(fa[:,:,3],fa[::-1,::-1,3])),
    'paired_stations':all(a+b==H for a,b in zip(LY,reversed(RY))),
    'left_cusps_aligned':all((LEFT_RAIL+DEPTH,y)==(W-x,H-yy) for y,(x,yy) in zip(LY,reversed([(RIGHT_RAIL-DEPTH,z) for z in RY]))),
    'right_cusps_aligned':all(any(abs(x-(RIGHT_RAIL-DEPTH))<1e-6 and abs(yy-y)<1e-6 for x,yy in points) for y in RY)})
pip_layer=Image.new('RGBA',(W,H))
for y in LY:stamp(pip_layer,tallies['drop_full'],(LEFT_PIP,y),SIZES['drop'])
for y in RY:stamp(pip_layer,tallies['attack_full'],(RIGHT_PIP,y),SIZES['attack'])
qa['pip_frame_overlap_pixels']=int(((np.asarray(pip_layer)[:,:,3]>100)&(fa[:,:,3]>100)).sum())
faction_layer=Image.new('RGBA',(W,H))
for slug,_,_ in FACTIONS:
    cell=sprites['faction_'+slug].resize((224,224),Image.Resampling.LANCZOS)
    faction_layer.alpha_composite(cell,(28,68))
    faction_layer.alpha_composite(cell.transpose(Image.Transpose.ROTATE_180),(772,1244))
qa['pip_faction_overlap_pixels']=int(((np.asarray(pip_layer)[:,:,3]>100)&(np.asarray(faction_layer)[:,:,3]>100)).sum())
qa['assets']={n:geometry(n.split('_')[0],im) for n,im in tallies.items()}
assert all(qa[k] for k in ['frame_rotation_equal','paired_stations','left_cusps_aligned','right_cusps_aligned','inner_vertical_ends_at_faction_center'])
assert qa['pip_frame_overlap_pixels']==0 and qa['pip_faction_overlap_pixels']==0
(ROOT/'QA_REPORT.json').write_text(json.dumps(qa,indent=2),encoding='utf-8')

sheet=Image.new('RGB',(1400,970),(28,35,36));d=ImageDraw.Draw(sheet);cream=(232,216,181)
d.text((30,20),'NEON MONTE / FRONT - LE TACCHE SONO I SIMBOLI',font=font(26),fill=cream)
d.text((30,58),'GOCCIA / LANCIA ROMBOIDALE VERSO IL CENTRO / NESSUNA INTESTAZIONE',font=font(18),fill=(142,174,161))
for j,(slug,label,_) in enumerate(FACTIONS):
    card=previews[slug].resize((300,450),Image.Resampling.LANCZOS)
    sheet.paste(card,(30+j*320,102),card);d.text((30+j*320,565),label,font=font(18),fill=cream)
mini=previews['sun'].resize((224,336),Image.Resampling.LANCZOS)
sheet.paste(mini,(1080,140),mini);d.text((1080,490),'224 x 336',font=font(18),fill=cream)
for i,(name,im) in enumerate(items.items()):
    x,y=30+(i%7)*194,625+(i//7)*163
    d.rounded_rectangle((x,y,x+160,y+112),radius=8,fill=cream)
    icon=im.resize((112,112),Image.Resampling.LANCZOS);sheet.paste(icon,(x+24,y),icon)
    d.text((x,y+120),name,font=font(16),fill=cream)
save(sheet,'CONTACT_SHEET.png');save(sheet,'CONTACT_SHEET_SOLID_SPEAR.png')
save(mini,'QA/front_sun_224x336.png')
readme=f'''# Neon Monte — fronte francese, tacche a glifo

Revisione corrente **light-tallies-v5**. Le tacche identificano direttamente la statistica: goccia per vita, **punta di lancia romboidale** per attacco. Nessun cuore o simbolo d'attacco aggiuntivo sopra o sotto la colonna.

## File correnti

- CONTACT_SHEET.png: tre fazioni, sprite e controllo a 224×336. Anche CONTACT_SHEET_SOLID_SPEAR.png mostra la revisione corrente.
- Template/front_clean.png: carta e cornice senza dati, titoli o artwork incorporati.
- Template/front_paper.png, front_frame.png, front_frame.svg: livelli separati.
- Template/front_sun_preview.png, front_moon_preview.png, front_saturn_preview.png: campioni con 3 vita e 4 attacco; overlay separati.
- Template/front_empty_preview.png e front_empty_overlay.png: tutte le tacche vuote.
- Indicators/drop_full.png / drop_empty.png: goccia nelle proporzioni originali, punta verso destra.
- Indicators/attack_full.png / attack_empty.png: punta di lancia romboidale a due facce, rivolta a sinistra, dentro la carta.
- symbols_normalized_atlas.png, layout_manifest.json, QA_REPORT.json.

## Misure

Canvas 1024×1536. Fazioni in (140,180) e (884,1356), seconda ruotata 180°. Nessuna intestazione statistica. Gocce {SIZES['drop'][0]}×{SIZES['drop'][1]}, lance {SIZES['attack'][0]}×{SIZES['attack'][1]} di inchiostro visibile.

Sette tacche per lato, passo **166 px**. Gocce centrate su x={LEFT_PIP}, y={LY}. Lance su x={RIGHT_PIP}, y={RY}. I filetti passano per x={LEFT_RAIL}/{RIGHT_RAIL}, con cuspidi profonde {DEPTH} px, centrate esattamente sulle tacche. La cornice mantiene la simmetria di rotazione 180°.

I PNG hanno celle 256×256 e sagome normalizzate nelle proporzioni originali della goccia (192×120). Il manifest specifica ink_size, alpha_bbox e full_sprite_render_size per ogni tacca. Pieno/vuoto hanno stessi contorni esterni e pivot. Il verso della lancia è incorporato: runtime a zero.

Titolo, artwork e abilità mantengono le aree libere del fronte precedente. Le tre cariche restano a y=1458; applicare Template/charge_backplates.png prima dei cerchi per interrompere il filetto all'interno.

## Sorgenti e ricostruzione

../SharedCardAssets/tally_shapes.py è la fonte comune delle tacche di fronte e retro. Ripristina la goccia originale 192×120, la lancia romboidale e lo scudo semplice, nello stesso ingombro. Gli asset defense_full/empty sono disponibili anche qui come libreria comune, ma non sono montati sul fronte.

build_assets.py assembla dagli asset esistenti con Pillow/NumPy; nessuna nuova generazione image_gen necessaria per questa modifica geometrica. La carta e i simboli astronomici restano quelli approvati. Le precedenti istruzioni e lo script sono in Sources/*before_glyph_tallies*.

Import Sprite, Point, niente compressione/mipmap, PPU 1, alpha come trasparenza. QA: conteggi, simmetria, orientamenti, bbox pieni/vuoti, alpha e assenza di sovrapposizioni tra tacche, cornici e fazioni. Nessun prefab o scena Unity modificato.

Il master spear_full precedente è la sorgente della lancia corrente, specchiata verso il centro. Cuore e studi restano storici.
'''
(ROOT/'README.md').write_text(readme,encoding='utf-8')
print(json.dumps({'front':'built','pip_frame_overlap':qa['pip_frame_overlap_pixels'],'pip_faction_overlap':qa['pip_faction_overlap_pixels'],'stat_headers':0},indent=2))
