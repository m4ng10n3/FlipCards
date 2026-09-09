"""Deterministic assembly of the ornate back, with its own reading layout.

Run with Pillow and NumPy. No generation, API calls or Unity changes on rebuild.
"""
from pathlib import Path
import json
import math
import base64
from io import BytesIO
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

ROOT = Path(__file__).resolve().parent
FRONT = ROOT.parent / 'Front_French_v2'
W, H, SS = 1024, 1536, 4
CREAM, INK = (232, 217, 181), (25, 33, 36)
LEFT, RIGHT, DEPTH = 210, 814, 18
LEFT_PIP, RIGHT_PIP = 154, 870
LEFT_HEADER, RIGHT_HEADER = 146, 878
LY = [470 + 120*i for i in range(7)]
RY = LY.copy()
PIP_SIZE = (108, 68)
FACTION_CENTER, FACTION_CELL = (512,334), 192
ART_CENTER = (512,840)
CHARGE_X, CHARGE_Y, CHARGE_SIZE = [450,512,574], 1414, 38

def save(im, name):
    p = ROOT / name
    p.parent.mkdir(parents=True, exist_ok=True)
    im.save(p)

def tint(im, color=CREAM):
    out = Image.new('RGBA', im.size, color + (0,))
    out.putalpha(im.getchannel('A'))
    return out

def fit_alpha(alpha, envelope=(192, 192)):
    b = alpha.getbbox()
    assert b
    alpha = alpha.crop(b)
    alpha.thumbnail(envelope, Image.Resampling.LANCZOS)
    out = Image.new('L', (256, 256))
    out.paste(alpha, ((256-alpha.width)//2, (256-alpha.height)//2))
    return out

def rgba(alpha, color=CREAM):
    im = Image.new('RGBA', alpha.size, color + (0,))
    im.putalpha(alpha)
    return im

def fill_holes(mask):
    flood = mask.copy()
    ImageDraw.floodfill(flood, (0, 0), 128, thresh=0)
    return Image.fromarray(np.where(np.asarray(flood) == 128, 0, 255).astype('uint8'))

def outline(alpha, width=7):
    inner = alpha.filter(ImageFilter.MinFilter(width*2+1))
    return ImageChops.subtract(alpha, inner)

def bezier(a, b, c, d, count=30):
    return [tuple((1-t)**3*a[k] + 3*(1-t)**2*t*b[k] + 3*(1-t)*t*t*c[k] + t**3*d[k] for k in (0,1)) for t in np.linspace(0,1,count)]

def stamp(layer, sprite, center, size, rotation=0, preserve=False):
    sprite = sprite.crop(sprite.getchannel('A').getbbox())
    if preserve:
        sprite.thumbnail(size, Image.Resampling.LANCZOS)
    else:
        sprite = sprite.resize(size, Image.Resampling.LANCZOS)
    if rotation:
        sprite = sprite.rotate(rotation, expand=True)
    layer.alpha_composite(sprite, (round(center[0]-sprite.width/2), round(center[1]-sprite.height/2)))

def font(size):
    return ImageFont.truetype('C:/Windows/Fonts/consola.ttf', size)

def centered_text(layer, text, center, size, color=CREAM):
    d = ImageDraw.Draw(layer)
    f = font(size)
    b = d.textbbox((0,0), text, font=f)
    d.text((center[0]-(b[2]-b[0])/2-b[0], center[1]-(b[3]-b[1])/2-b[1]), text, font=f, fill=color)

# Approved generated B: crop only the large icon, recover its silhouette and
# remove paper texture. No reinterpretation as the standard stemmed club suit.
source = Image.open(ROOT/'Sources/defense_B_approved.png').convert('RGB')
crop = source.crop((100,200,750,860))
a = np.asarray(crop)
mask = Image.fromarray(np.where(a.min(2)>100, 255, 0).astype('uint8'))
mask = fill_holes(mask)
mask = mask.crop(mask.getbbox())
# Average mirrored contour to remove generative left/right drift, retaining B.
mask = ImageChops.lighter(mask, mask.transpose(Image.Transpose.FLIP_LEFT_RIGHT))
mask = fit_alpha(mask)
symbols = {'defense_club_B': rgba(mask)}
for name in ['heart','attack_spade','star','charge_ring','charge_full','faction_sun','faction_moon','faction_saturn']:
    symbols[name] = tint(Image.open(FRONT/'Symbols'/f'{name}.png').convert('RGBA'))
for name, im in symbols.items():
    save(im, f'Symbols/{name}.png')
    save(tint(im, (255,255,255)), f'Symbols/{name}_mask.png')
    save(tint(im, INK), f'Symbols/{name}_black.png')

# Keep the exact front drop contours and boxes; change only ink to ivory.
indicators = {}
for state in ['full','empty']:
    name = 'drop_'+state
    indicators[name] = tint(Image.open(FRONT/'Indicators'/f'{name}.png').convert('RGBA'))

# Upright heraldic shield, 120 x 192 ink. Clockwise rotation maps its bottom
# point to LEFT, and top central crest to RIGHT. All states share this contour.
points = [(128,32)]
points += bezier((128,32),(108,48),(86,55),(68,56))
points += bezier((68,56),(66,127),(77,185),(128,224))
points += bezier((128,224),(179,185),(190,127),(188,56))
points += bezier((188,56),(170,55),(148,48),(128,32))
large = Image.new('L', (256*SS,256*SS))
ImageDraw.Draw(large).polygon([(round(x*SS),round(y*SS)) for x,y in points], fill=255)
alpha = large.resize((256,256), Image.Resampling.LANCZOS)
# Normalize into an exact 120x192 box before rigid rotation to 192x120.
alpha = alpha.crop(alpha.getbbox()).resize((120,192), Image.Resampling.LANCZOS)
upright = Image.new('L',(256,256)); upright.paste(alpha,(68,32))
side = upright.transpose(Image.Transpose.ROTATE_270)
indicators['shield_full'] = rgba(side)
indicators['shield_empty'] = rgba(outline(side))
save(rgba(upright),'Indicators/shield_upright_source.png')
for name, im in indicators.items():
    save(im,f'Indicators/{name}.png')
    save(tint(im,(255,255,255)),f'Indicators/{name}_mask.png')
path = 'M'+' L'.join(f'{x:.3f},{y:.3f}' for x,y in points)+' Z'
(ROOT/'Indicators/shield_full.svg').write_text(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256"><g transform="rotate(90 128 128)"><path fill="#e8d9b5" d="{path}"/></g></svg>',encoding='utf-8')

# Restore the original distressed perimeter and its printed outer rule.
# The inset rails are now at x=210/814, independent of the front.
paper = Image.open(ROOT/'Sources/back_paper_generated.png').convert('RGBA').resize((W,H),Image.Resampling.LANCZOS)
cardmask = Image.new('L',(W*SS,H*SS))
ImageDraw.Draw(cardmask).rounded_rectangle((0,0,W*SS-1,H*SS-1),radius=42*SS,fill=255)
cardmask = cardmask.resize((W,H),Image.Resampling.LANCZOS)
# Source delivered an RGB checker at the extreme paper boundary. Key only
# neutral grey exterior pixels; warm aged paper and its dark wear remain.
pa=np.asarray(paper)
exterior=np.zeros((H,W),dtype=bool)
exterior[:28]=True;exterior[-28:]=True;exterior[:,:28]=True;exterior[:,-28:]=True
grey=(pa[:,:,:3].max(2)-pa[:,:,:3].min(2)<12)&(pa[:,:,1]>80)&(pa[:,:,1]<225)&exterior
cm=np.asarray(cardmask).copy();cm[grey]=0
cardmask=Image.fromarray(cm)
paper.putalpha(cardmask)
save(paper,'Template/back_paper.png')

# Recover black and cream ornamental inks, removing residual red/neutral
# backing if the generative source did not deliver clean alpha.
orn = Image.open(ROOT/'Sources/ornament_generated.png').convert('RGBA')
arr = np.asarray(orn).astype(float)
r,g,b = [arr[:,:,i] for i in range(3)]
black = np.clip((105-np.maximum.reduce([r,g,b]))/45,0,1)
cream = np.clip((g-95)/60,0,1)*np.clip((r-b-10)/30,0,1)*np.clip((g-b-5)/20,0,1)
opacity = np.maximum(black,cream)*(arr[:,:,3]/255)
out = np.zeros_like(arr,dtype='uint8')
out[:,:,:3] = np.where((black>cream)[:,:,None],np.array(INK),np.array(CREAM))
out[:,:,3] = (opacity*255).astype('uint8')
orn = Image.fromarray(out)
orn = orn.crop(orn.getchannel('A').getbbox())
orn.thumbnail((540,850),Image.Resampling.LANCZOS)
save(orn,'Artwork/three_eyes_ornament.png')
art = Image.new('RGBA',(W,H))
art.alpha_composite(orn,(round(ART_CENTER[0]-orn.width/2),round(ART_CENTER[1]-orn.height/2)))
save(art,'Template/back_artwork_overlay.png')

# Broad scalloped caps traced in spirit and proportion from the supplied back.
# Both vertical rails join the caps at (210,220)/(814,220) and y=1316.
top_left=bezier((52,128),(56,174),(110,188),(187,188))
top_left+=bezier((187,188),(194,198),(204,212),(210,220))
top_left+=bezier((210,220),(290,300),(393,264),(465,240))
top_left+=bezier((465,240),(490,232),(500,230),(512,230))
top_cap=top_left+[(W-x,y) for x,y in reversed(top_left)]
bottom_cap=[(x,H-y) for x,y in top_cap]
left_track=[(LEFT,220)]
for y in LY:
    left_track += [(LEFT,y-30)]
    left_track += bezier((LEFT,y-30),(LEFT,y-15),(LEFT+6,y-6),(LEFT+DEPTH,y))
    left_track += bezier((LEFT+DEPTH,y),(LEFT+6,y+6),(LEFT,y+15),(LEFT,y+30))
left_track += [(LEFT,1316)]
right_track=[(W-x,y) for x,y in left_track]
main_paths=[top_cap,bottom_cap,left_track,right_track]
secondary_paths=[[(x-10,y) for x,y in left_track],[(x+10,y) for x,y in right_track]]
rules=[[(126,CHARGE_Y),(418,CHARGE_Y)],[(606,CHARGE_Y),(898,CHARGE_Y)]]
frame_hi=Image.new('RGBA',(W*SS,H*SS))
fd=ImageDraw.Draw(frame_hi)
def draw_path(points,color,width):
    fd.line([(round(x*SS),round(y*SS)) for x,y in points],fill=color+(255,),width=round(width*SS),joint='curve')
for points in main_paths:
    draw_path(points,INK,13)
    draw_path(points,CREAM,6)
for points in secondary_paths:
    draw_path(points,INK,6)
    draw_path(points,CREAM,2.5)
for points in rules:
    draw_path(points,CREAM,2.5)
frame=frame_hi.resize((W,H),Image.Resampling.LANCZOS)
# Four familiar small perimeter stars stay decorative and separate from stats.
for x in [78,946]:
    for y in [88,1448]:
        stamp(frame,symbols['star'],(x,y),(24,38))
save(frame,'Template/back_frame.png')
fa=np.asarray(frame)
def svg_path(points):
    return 'M'+' L'.join(f'{x:.3f},{y:.3f}' for x,y in points)
svg_elements=[]
for points in main_paths:
    for color,width in [('#192124',13),('#e8d9b5',6)]:
        svg_elements.append(f'<path fill="none" stroke="{color}" stroke-width="{width}" d="{svg_path(points)}"/>')
for points in secondary_paths:
    for color,width in [('#192124',6),('#e8d9b5',2.5)]:
        svg_elements.append(f'<path fill="none" stroke="{color}" stroke-width="{width}" d="{svg_path(points)}"/>')
for points in rules:
    svg_elements.append(f'<path fill="none" stroke="#e8d9b5" stroke-width="2.5" d="{svg_path(points)}"/>')
star_stamp=symbols['star'].crop(symbols['star'].getchannel('A').getbbox()).resize((24,38),Image.Resampling.LANCZOS)
star_bytes=BytesIO();star_stamp.save(star_bytes,format='PNG')
star_uri='data:image/png;base64,'+base64.b64encode(star_bytes.getvalue()).decode('ascii')
for x in [78,946]:
    for y in [88,1448]:
        svg_elements.append(f'<image x="{x-12}" y="{y-19}" width="24" height="38" href="{star_uri}"/>')
(ROOT/'Template/back_frame.svg').write_text('<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1536" viewBox="0 0 1024 1536"><g stroke-linejoin="round">'+''.join(svg_elements)+'</g></svg>',encoding='utf-8')
clean = Image.alpha_composite(Image.alpha_composite(paper,art),frame)
save(clean,'Template/back_clean.png')
charge_backplates=Image.new('RGBA',(W,H))
# Kept as an empty legacy layer; no backplates needed with the split bottom rule.
save(charge_backplates,'Template/charge_backplates.png')

def assemble(faction='sun', health=3, defense=4, charges=1, banner=2, banner_type='attack'):
    layer = Image.new('RGBA',(W,H))
    faction_cell = symbols['faction_'+faction].resize((FACTION_CELL,FACTION_CELL),Image.Resampling.LANCZOS)
    layer.alpha_composite(faction_cell,(FACTION_CENTER[0]-FACTION_CELL//2,FACTION_CENTER[1]-FACTION_CELL//2))
    stamp(layer,symbols['heart'],(LEFT_HEADER,340),(88,88))
    stamp(layer,symbols['defense_club_B'],(RIGHT_HEADER,340),(98,104),preserve=True)
    for i,y in enumerate(LY):
        stamp(layer,indicators['drop_full' if i<health else 'drop_empty'],(LEFT_PIP,y),PIP_SIZE)
    for i,y in enumerate(RY):
        stamp(layer,indicators['shield_full' if i>=7-defense else 'shield_empty'],(RIGHT_PIP,y),PIP_SIZE)
    # Source pips are round and padded consistently with the front.
    for i,x in enumerate(CHARGE_X):
        stamp(layer,symbols['charge_full' if i<charges else 'charge_ring'],(x,CHARGE_Y),(CHARGE_SIZE,CHARGE_SIZE))
    if banner is not None:
        if banner_type=='attack':
            stamp(layer,symbols['attack_spade'],(466,146),(80,108))
        else:
            stamp(layer,rgba(upright),(466,146),(80,108))
        centered_text(layer,str(banner),(566,146),140)
    return Image.alpha_composite(clean,layer),layer

previews = {}
for faction in ['sun','moon','saturn']:
    preview, layer = assemble(faction)
    previews[faction] = preview
    save(preview,f'Template/back_{faction}_preview.png')
    save(layer,f'Template/back_{faction}_overlay.png')
empty, empty_layer = assemble(health=0,defense=0,charges=0,banner=None)
save(empty,'Template/back_empty_preview.png')
save(empty_layer,'Template/back_empty_overlay.png')
guard,guard_layer=assemble(banner_type='defense')
save(guard,'Template/back_defense_banner_preview.png')
save(guard_layer,'Template/back_defense_banner_overlay.png')

# One reusable cell per symbol/indicator, transparent and runtime-tintable.
atlas = Image.new('RGBA',(1024,1024))
entries=[]
for i,(name,im) in enumerate({**symbols,**indicators}.items()):
    x,y=(i%4)*256,(i//4)*256
    atlas.alpha_composite(im,(x,y))
    entries.append({'name':name,'rect':[x,y,256,256],'alpha_bbox':list(im.getchannel('A').getbbox()),'pivot':[0.5,0.5]})
save(atlas,'symbols_normalized_atlas.png')

manifest = {
    'canvas':[W,H],'coordinates':'top-left origin, y down','runtime_card_size':[224,336],
    'layout_revision':'ornate-back-independent-v3','layout_reference':'Sources/back_layout_reference.png',
    'layers':['Template/back_paper.png','Template/back_artwork_overlay.png','Template/back_frame.png','runtime symbols and text'],
    'clean_template':'Template/back_clean.png','clean_has_baked_stats':False,
    'symbol_canvas':[256,256],'indicator_canvas':[256,256],'indicator_ink_envelope':[192,120],
    'faction_centers':[list(FACTION_CENTER)],'faction_full_sprite_render_size':[FACTION_CELL,FACTION_CELL],
    'faction_placement':'single emblem inside central panel above ornament','faction_rotation_degrees':0,'faction_render_tint':'#e8d9b5 (ivory on red)',
    'heart':{'file':'Symbols/heart.png','center':[LEFT_HEADER,340],'ink_size':[88,88]},
    'defense':{'file':'Symbols/defense_club_B.png','source':'Sources/defense_B_approved.png','variant':'B original, pointed base, no stem','center':[RIGHT_HEADER,340],'ink_envelope':[98,104]},
    'inner_margin':{'left_axis':LEFT,'right_axis':RIGHT,'scallop_depth':DEPTH,'cream_stroke':6,'dark_understroke':13,'secondary_rule_offset':10,'cusp_span':60,'rail_start_y':220,'rail_end_y':1316},
    'left_tallies':{'centers':[[LEFT_PIP,y] for y in LY],'cusps':[[LEFT+DEPTH,y] for y in LY],'size':list(PIP_SIZE),'count':7,'step':120,'full':'Indicators/drop_full.png','empty':'Indicators/drop_empty.png','tip':'right/inward'},
    'right_tallies':{'centers':[[RIGHT_PIP,y] for y in RY],'cusps':[[RIGHT-DEPTH,y] for y in RY],'size':list(PIP_SIZE),'count':7,'step':120,'full':'Indicators/shield_full.png','empty':'Indicators/shield_empty.png','tip':'left/inward','rotation_baked_clockwise_degrees':90,'runtime_rotation_degrees':0},
    'indicator_full_sprite_render_size':[PIP_SIZE[0]*256/192,PIP_SIZE[1]*256/120],
    'charges':{'centers':[[x,CHARGE_Y] for x in CHARGE_X],'ink_size':[CHARGE_SIZE,CHARGE_SIZE],'backplates_required':False},
    'banner':{'icon_center':[466,146],'icon_ink_envelope':[80,108],'value_center':[566,146],'value_font_size':140,'dynamic_text':True,'attack_icon':'Symbols/attack_spade.png','defense_icon':'Indicators/shield_upright_source.png'},
    'artwork':{'file':'Artwork/three_eyes_ornament.png','size':list(orn.size),'center':list(ART_CENTER)},
    'frame_paths':{'top_cap':top_cap,'bottom_cap':bottom_cap,'left_track':left_track,'right_track':right_track},
    'atlas':{'file':'symbols_normalized_atlas.png','size':[1024,1024],'entries':entries},
    'import':{'type':'Sprite','filter':'Point','compression':'None','mipmaps':False,'PPU':1,'alpha_is_transparency':True},
    'preview_values':{'health':3,'defense':4,'charges':1,'attack_banner':2},
}
(ROOT/'layout_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')

# Structural checks for the independent back layout and its separated layers.
front_manifest=json.loads((FRONT/'layout_manifest.json').read_text(encoding='utf-8'))
qa={'canvas':[W,H],'independent_back_layout': all(manifest[k]['centers']!=front_manifest[k]['centers'] for k in ['left_tallies','right_tallies']),
    'rail_geometry_mirrors_horizontally':right_track==[(W-x,y) for x,y in left_track],
    'caps_mirror_vertically':bottom_cap==[(x,H-y) for x,y in top_cap],
    'single_faction_inside_panel':len(manifest['faction_centers'])==1 and LEFT+72<FACTION_CENTER[0]<RIGHT-72 and 230+72<FACTION_CENTER[1]<ART_CENTER[1]-orn.height/2-72,
    'left_count':len(LY),'right_count':len(RY),'step':120,
    'left_alignment':all(a[1]==b[1] for a,b in zip(manifest['left_tallies']['centers'],manifest['left_tallies']['cusps'])),
    'right_alignment':all(a[1]==b[1] for a,b in zip(manifest['right_tallies']['centers'],manifest['right_tallies']['cusps'])),
    'shield_is_rigid_clockwise_rotation':bool(np.array_equal(np.asarray(side),np.rot90(np.asarray(upright),-1))),
    'preview_values':manifest['preview_values'],'files':{}}
qa['paper_exterior_grey_removed']=int(grey.sum())
qa['paper_exterior_grey_remaining']=int((grey&(cm>0)).sum())
qa['ornament_alpha_zero_pixels']=int((np.asarray(orn.getchannel('A'))==0).sum())
qa['outer_pip_clearance_pixels']=LEFT_PIP-PIP_SIZE[0]/2-50
qa['tip_to_frame_cusp_clearance_pixels']=LEFT+DEPTH-(LEFT_PIP+PIP_SIZE[0]/2)
# Compare actual raster masks, not only the declared station coordinates.
pip_mask=Image.new('RGBA',(W,H))
for y in LY:
    stamp(pip_mask,indicators['drop_full'],(LEFT_PIP,y),PIP_SIZE)
    stamp(pip_mask,indicators['shield_full'],(RIGHT_PIP,y),PIP_SIZE)
qa['pip_frame_overlap_pixels']=int(((np.asarray(pip_mask)[:,:,3]>100)&(fa[:,:,3]>100)).sum())
faction_mask=Image.new('RGBA',(W,H))
for faction in ['sun','moon','saturn']:
    cell=symbols['faction_'+faction].resize((FACTION_CELL,FACTION_CELL),Image.Resampling.LANCZOS)
    faction_mask.alpha_composite(cell,(FACTION_CENTER[0]-FACTION_CELL//2,FACTION_CENTER[1]-FACTION_CELL//2))
qa['faction_ornament_overlap_pixels']=int(((np.asarray(faction_mask)[:,:,3]>100)&(np.asarray(art)[:,:,3]>100)).sum())
header_mask=faction_mask.copy()
stamp(header_mask,symbols['heart'],(LEFT_HEADER,340),(88,88))
stamp(header_mask,symbols['defense_club_B'],(RIGHT_HEADER,340),(98,104),preserve=True)
qa['header_frame_overlap_pixels']=int(((np.asarray(header_mask)[:,:,3]>100)&(fa[:,:,3]>100)).sum())
qa['ornament_frame_overlap_pixels']=int(((np.asarray(art)[:,:,3]>100)&(fa[:,:,3]>100)).sum())
for name,im in {**symbols,**indicators}.items():
    al=np.asarray(im.getchannel('A'))
    qa['files'][name]={'size':list(im.size),'bbox':list(im.getchannel('A').getbbox()),'alpha_zero_pixels':int((al==0).sum())}
for shape in ['drop','shield']:
    qa[shape+'_state_bounds_match']=indicators[shape+'_full'].getchannel('A').getbbox()==indicators[shape+'_empty'].getchannel('A').getbbox()
assert all(qa[k] for k in ['independent_back_layout','rail_geometry_mirrors_horizontally','caps_mirror_vertically','single_faction_inside_panel','left_alignment','right_alignment','shield_is_rigid_clockwise_rotation','drop_state_bounds_match','shield_state_bounds_match'])
assert qa['pip_frame_overlap_pixels']==0 and qa['faction_ornament_overlap_pixels']==0
assert qa['header_frame_overlap_pixels']==0 and qa['ornament_frame_overlap_pixels']==0
assert all(v['alpha_zero_pixels']>0 for v in qa['files'].values())
assert qa['paper_exterior_grey_remaining']==0 and qa['ornament_alpha_zero_pixels']>0
(ROOT/'QA_REPORT.json').write_text(json.dumps(qa,indent=2),encoding='utf-8')

# Review at full composition and actual target game size.
sheet=Image.new('RGB',(1440,1080),INK)
d=ImageDraw.Draw(sheet)
d.text((30,18),'NEON MONTE / RETRO - CORNICI SAGOMATE',font=font(28),fill=CREAM)
d.text((30,58),'SEME INTERNO / 7 TACCHE / SCUDI VERSO IL CENTRO',font=font(19),fill=(151,183,171))
for i,faction in enumerate(['sun','moon','saturn']):
    im=previews[faction].resize((300,450),Image.Resampling.LANCZOS)
    sheet.paste(im,(30+i*320,105),im)
    d.text((30+i*320,567),faction.upper(),font=font(20),fill=CREAM)
small=previews['sun'].resize((224,336),Image.Resampling.LANCZOS)
sheet.paste(small,(1060,160),small)
d.text((1060,510),'224 x 336',font=font(20),fill=CREAM)
for i,(name,im) in enumerate({**symbols,**indicators}.items()):
    col,row=i%7,i//7
    x,y=30+col*198,635+row*198
    sheet.paste(im.resize((112,112),Image.Resampling.LANCZOS),(x+24,y),im.resize((112,112),Image.Resampling.LANCZOS))
    d.text((x,y+124),name,font=font(16),fill=CREAM)
save(sheet,'CONTACT_SHEET.png')
save(previews['sun'].resize((224,336),Image.Resampling.LANCZOS),'QA/back_sun_224x336.png')
save(empty.resize((224,336),Image.Resampling.LANCZOS),'QA/back_empty_224x336.png')
readme=f'''# Neon Monte — retro a cornici sagomate

Revisione corrente: **ornate-back-independent-v3**. Mantiene il dorso rosso, le ampie fasce sagomate e il motivo a tre occhi del riferimento fornito. Il layout è autonomo dal fronte. Il simbolo della difesa è **B originale / defense_club_B**, con tre lobi appuntiti e base a punta: nessun elmo negli asset correnti.

## File da aprire

- `CONTACT_SHEET.png`: tre fazioni, sprite separati e riduzione a 224×336.
- `Template/back_sun_preview.png`, `back_moon_preview.png`, `back_saturn_preview.png`: composizioni correnti, con overlay separati.
- `Template/back_clean.png`: carta, cornice e ornamento, senza seme, statistiche, insegna o cariche incorporate.
- `Template/back_paper.png`, `back_artwork_overlay.png`, `back_frame.png`: tre livelli separati; gli overlay hanno alpha reale.
- `Template/back_frame.svg`: tracciati precisi e stelline raster incorporate; file autonomo.
- `Artwork/three_eyes_ornament.png`: motivo centrale isolato con alpha reale.
- `Symbols/defense_club_B.png`: il simbolo concordato; varianti `_mask` bianche e `_black` nere.
- `Indicators/`: goccia e scudo pieno/vuoto, PNG 256×256 con pivot centrale e ingombro 192×120. Gli scudi sono già orientati verso sinistra.
- `symbols_normalized_atlas.png`: 13 sprite in celle 256×256, foglio 1024×1024.
- `layout_manifest.json`, `QA_REPORT.json`, `build_assets.py`: montaggio, verifica e ricostruzione.

## Layout del retro

Canvas 1024×1536, origine in alto a sinistra. Un solo seme, centrato in **(512,334)** dentro il pannello interno, sopra ai tre occhi. La cella del seme misura 192×192, con inchiostro entro 144×144 e proporzioni naturali.

Cuore al centro **({LEFT_HEADER},340)** e B originale al centro **({RIGHT_HEADER},340)**, sopra le rispettive colonne. L'insegna sta nella fascia superiore: icona (466,146), valore (566,146). Le tre cariche, nella fascia inferiore, sono centrate su x={CHARGE_X}, y={CHARGE_Y}, con diametro {CHARGE_SIZE}.

Sette tacche per lato alle y **{LY}**, passo 120. Centri x={LEFT_PIP} per le gocce e x={RIGHT_PIP} per gli scudi. Inchiostro {PIP_SIZE[0]}×{PIP_SIZE[1]}. Le gocce puntano a destra, gli scudi a sinistra: entrambe le file guardano l'interno.

Le doppie cornici laterali hanno asse principale x={LEFT}/{RIGHT}, da y=220 a y=1316. Ogni stazione ha una cuspide profonda {DEPTH} px, esattamente all'altezza della tacca. Il profilo si raccorda entro 30 px sopra/sotto il centro; un secondo filetto sta 10 px verso il margine. Le cuspidi principali sono a x={LEFT+DEPTH}/{RIGHT-DEPTH}.

Le fasce superiore e inferiore usano ampie curve derivate dal riferimento `Sources/back_layout_reference.png`, senza replicare la cornice del fronte. L'ornamento centrale misura {orn.width}×{orn.height}, con centro {ART_CENTER}; resta separato dal seme e dai filetti.

## Montaggio e import

Usare `back_clean.png` come base, oppure paper → artwork → frame. Poi aggiungere seme, cuore, defense club B, tacche, insegna e cariche seguendo il manifest. I preview incorporano valori illustrativi: vita 3/7, difesa 4/7, insegna attacco 2, cariche 1/3.

La rotazione di 90° in senso orario degli scudi è già incorporata negli sprite `shield_full` e `shield_empty`: a runtime usare rotazione zero. `shield_upright_source.png` serve per l'insegna difensiva, non per le tacche. Gli stati pieno/vuoto hanno identici bounding box e pivot.

Le misure del manifest sono dell'inchiostro. Per ottenere tacche {PIP_SIZE[0]}×{PIP_SIZE[1]} dal PNG completo 256×256, usare un rect circa {PIP_SIZE[0]*256/192:.2f}×{PIP_SIZE[1]*256/120:.2f}. Per il seme usare l'intera cella a 192×192 senza stirarla. La linea inferiore è già interrotta attorno alle cariche: non occorre `charge_backplates.png`, mantenuto vuoto come livello storico.

Import: Sprite, Point, nessuna compressione o mipmap, PPU 1, alpha come trasparenza; elementi UI con raycast disabilitato. Nessuna modifica a prefab o scena Unity.

## Fonti, ricostruzione e verifica

Sorgenti raster generate con image_gen integrato; nessuna nuova generazione necessaria per questa revisione geometrica. Il B è estratto dalla proposta originale scelta. Carta e ornamento sono ripuliti dal fondo a scacchi RGB delle sorgenti, consegnando alpha reale. Prompt in `Sources/PRODUCTION_PROMPTS.md` e `DEFENSE_SELECTED.md`.

`build_assets.py` ricrea il pacchetto con Pillow/NumPy dalle sorgenti locali e dai simboli riutilizzati del fronte. Non effettua chiamate API e non modifica il fronte. Il montaggio preciso non impone più le posizioni del fronte.

QA automatico: sette tacche per lato, simmetria orizzontale dei tracciati, allineamento cuspidi, scudi ruotati rigidamente, stessi bounding box pieno/vuoto, alpha reale, nessuna sovrapposizione tra tacche e cornice, intestazioni e cornice, seme e ornamento, ornamento e cornice. Controllo visivo del foglio e della riduzione 224×336.

Le revisioni precedenti a lettura identica al fronte sono archiviate in `Sources/*frontlike_revision*`. I PNG con elmo e gli studi alla radice sono riferimenti storici, non asset correnti.
'''
(ROOT/'README.md').write_text(readme,encoding='utf-8')
print(json.dumps({'built':str(ROOT),'independent_layout':qa['independent_back_layout'],'pip_frame_overlap':qa['pip_frame_overlap_pixels'],'faction_ornament_overlap':qa['faction_ornament_overlap_pixels'],'sprites':len(entries)},indent=2))
