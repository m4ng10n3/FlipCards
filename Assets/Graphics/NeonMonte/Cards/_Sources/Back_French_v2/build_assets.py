"""Deterministic assembly of the ornate back, with its own reading layout.

Run with Pillow and NumPy. No generation, API calls or Unity changes on rebuild.
"""
from pathlib import Path
import json
import sys
import math
import base64
from io import BytesIO
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

ROOT = Path(__file__).resolve().parent
FRONT = ROOT.parent / 'Front_French_v2'
sys.path.insert(0,str(ROOT.parent))
from SharedCardAssets.tally_shapes import export_tallies,geometry as shared_geometry,verify,FACE_SIZES
SIZES=FACE_SIZES['back']
def geometry(name,im): return shared_geometry(name,im,face='back')
W, H, SS = 1024, 1536, 4
CREAM, INK = (232, 217, 181), (25, 33, 36)
FRAME_CREAM, FRAME_INK = (228, 209, 171), (43, 37, 30)
SECONDARY_OFFSET=8
LEFT, RIGHT, DEPTH = 210, 814, 18
LEFT_PIP, RIGHT_PIP = 154, 870
LY = [324 + 148*i for i in range(7)]
RY = LY.copy()
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
for name in ['attack_spade','star','charge_ring','charge_full','faction_sun','faction_moon','faction_saturn']:
    symbols[name] = tint(Image.open(FRONT/'Symbols'/f'{name}.png').convert('RGBA'))
for name, im in symbols.items():
    save(im, f'Symbols/{name}.png')
    save(tint(im, (255,255,255)), f'Symbols/{name}_mask.png')
    save(tint(im, INK), f'Symbols/{name}_black.png')

# Shared lightweight tallies: original drop, rhombic spear and simple shield.
# No separate stat headers. Spear tip and shield bottom point face LEFT.
indicators=export_tallies(ROOT/'Indicators','back')

# Replace the damaged printed rectangle with one coherent constructed frame.
# Preserve original red paper inside; the new source owns only the outer band.
paper = Image.open(ROOT/'Sources/back_paper_generated.png').convert('RGBA').resize((W,H),Image.Resampling.LANCZOS)
refined=Image.open(ROOT/'Sources/back_paper_refined_generated.png').convert('RGBA').resize((W,H),Image.Resampling.LANCZOS)
py,px=np.mgrid[:H,:W]
edge_distance=np.minimum.reduce([px,W-1-px,py,H-1-py])
mix=np.clip((128-edge_distance)/56,0,1)[:,:,None]
paper=Image.fromarray(np.round(np.asarray(paper)*(1-mix)+np.asarray(refined)*mix).astype('uint8'))
cardmask = Image.new('L',(W*SS,H*SS))
ImageDraw.Draw(cardmask).rounded_rectangle((0,0,W*SS-1,H*SS-1),radius=42*SS,fill=255)
cardmask = cardmask.resize((W,H),Image.Resampling.LANCZOS)
# Source delivered an RGB checker at the extreme paper boundary. Key only
# neutral grey exterior pixels; warm aged paper and its dark wear remain.
pa=np.asarray(paper)
exterior=np.zeros((H,W),dtype=bool)
exterior[:28]=True;exterior[-28:]=True;exterior[:,:28]=True;exterior[:,-28:]=True
grey=(pa[:,:,:3].max(2)-pa[:,:,:3].min(2)<12)&(pa[:,:,1]>80)&exterior
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
top_left=bezier((51,128),(55,174),(110,188),(187,188))
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
# Join the secondary rules to the actual cap curve instead of leaving a butt end.
cap_join_y=float(np.interp(LEFT-SECONDARY_OFFSET,[p[0] for p in top_left],[p[1] for p in top_left]))
secondary_left=[(x-SECONDARY_OFFSET,y) for x,y in left_track]
secondary_left[0]=(LEFT-SECONDARY_OFFSET,cap_join_y)
secondary_left[-1]=(LEFT-SECONDARY_OFFSET,H-cap_join_y)
secondary_paths=[secondary_left,[(W-x,y) for x,y in secondary_left]]
rules=[[(126,CHARGE_Y),(418,CHARGE_Y)],[(606,CHARGE_Y),(898,CHARGE_Y)]]
frame_hi=Image.new('RGBA',(W*SS,H*SS))
fd=ImageDraw.Draw(frame_hi)
def draw_path(points,color,width):
    fd.line([(round(x*SS),round(y*SS)) for x,y in points],fill=color+(255,),width=round(width*SS),joint='curve')
# The complete inset perimeter shares the exact same ink pair and stroke widths.
perimeter_bounds=(51,49,973,1487)
for color,width in [(FRAME_INK,11.5),(FRAME_CREAM,5.5)]:
    # Pillow outlines inward: expand by half the width to share the same axis.
    b=[round((v+(-width/2 if i<2 else width/2))*SS) for i,v in enumerate(perimeter_bounds)]
    fd.rounded_rectangle(b,radius=round((24+width/2)*SS),outline=color+(255,),width=round(width*SS))
for points in secondary_paths:
    draw_path(points,FRAME_INK,4.5)
    draw_path(points,FRAME_CREAM,2.5)
for points in main_paths:
    draw_path(points,FRAME_INK,11.5)
    draw_path(points,FRAME_CREAM,5.5)
for points in rules:
    draw_path(points,CREAM,2.5)
frame=frame_hi.resize((W,H),Image.Resampling.LANCZOS)
# Four familiar small perimeter stars stay decorative and separate from stats.
for x in [78,946]:
    for y in [88,1448]:
        stamp(frame,symbols['star'],(x,y),(24,38))
# One paper grain treatment across all rules, including the inset perimeter.
fr=np.asarray(frame).astype(float)
paper_rgb=np.asarray(paper)[:,:,:3].astype(float)
grain=paper_rgb[:,:,0]-np.asarray(paper.convert('RGB').filter(ImageFilter.GaussianBlur(1.1)))[:,:,0]
coverage=np.clip(.97+grain*.0035,.74,1)
fr[:,:,3]*=coverage
frame=Image.fromarray(np.clip(fr,0,255).astype('uint8'))
save(frame,'Template/back_frame.png')
fa=np.asarray(frame)
def svg_path(points):
    return 'M'+' L'.join(f'{x:.3f},{y:.3f}' for x,y in points)
svg_elements=[]
for color,width in [('#2b251e',11.5),('#e4d1ab',5.5)]:
    svg_elements.append(f'<rect x="51" y="49" width="922" height="1438" rx="24" fill="none" stroke="{color}" stroke-width="{width}"/>')
for points in main_paths:
    for color,width in [('#2b251e',11.5),('#e4d1ab',5.5)]:
        svg_elements.append(f'<path fill="none" stroke="{color}" stroke-width="{width}" d="{svg_path(points)}"/>')
for points in secondary_paths:
    for color,width in [('#2b251e',4.5),('#e4d1ab',2.5)]:
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
(ROOT/'Template/back_frame_geometry.svg').write_text((ROOT/'Template/back_frame.svg').read_text(encoding='utf-8'),encoding='utf-8')
frame_uri='data:image/png;base64,'+base64.b64encode((ROOT/'Template/back_frame.png').read_bytes()).decode('ascii')
(ROOT/'Template/back_frame.svg').write_text(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}"><image width="{W}" height="{H}" href="{frame_uri}"/></svg>',encoding='utf-8')
clean = Image.alpha_composite(Image.alpha_composite(paper,art),frame)
save(clean,'Template/back_clean.png')
charge_backplates=Image.new('RGBA',(W,H))
# Kept as an empty legacy layer; no backplates needed with the split bottom rule.
save(charge_backplates,'Template/charge_backplates.png')

def assemble(faction='sun', health=3, defense=4, charges=1, banner=2, banner_type='attack'):
    layer = Image.new('RGBA',(W,H))
    faction_cell = symbols['faction_'+faction].resize((FACTION_CELL,FACTION_CELL),Image.Resampling.LANCZOS)
    layer.alpha_composite(faction_cell,(FACTION_CENTER[0]-FACTION_CELL//2,FACTION_CENTER[1]-FACTION_CELL//2))
    for i,y in enumerate(LY):
        stamp(layer,indicators['drop_full' if i<health else 'drop_empty'],(LEFT_PIP,y),SIZES['drop'])
    for i,y in enumerate(RY):
        stamp(layer,indicators['defense_full' if i<defense else 'defense_empty'],(RIGHT_PIP,y),SIZES['defense'])
    # Source pips are round and padded consistently with the front.
    for i,x in enumerate(CHARGE_X):
        stamp(layer,symbols['charge_full' if i<charges else 'charge_ring'],(x,CHARGE_Y),(CHARGE_SIZE,CHARGE_SIZE))
    if banner is not None:
        if banner_type=='attack':
            stamp(layer,symbols['attack_spade'],(466,146),(80,108))
        else:
            stamp(layer,symbols['defense_club_B'],(466,146),(100,108),preserve=True)
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
current_items={**symbols,**{n:indicators[n] for n in ['drop_full','drop_empty','defense_full','defense_empty']}}
for i,(name,im) in enumerate(current_items.items()):
    x,y=(i%4)*256,(i//4)*256
    atlas.alpha_composite(im,(x,y))
    entries.append({'name':name,'rect':[x,y,256,256],'alpha_bbox':list(im.getchannel('A').getbbox()),'pivot':[0.5,0.5]})
save(atlas,'symbols_normalized_atlas.png')

manifest = {
    'canvas':[W,H],'coordinates':'top-left origin, y down','runtime_card_size':[224,336],
    'layout_revision':'unified-border-back-v7','layout_reference':'Sources/back_layout_reference.png',
    'tally_fill_order':{'health':'top_to_bottom','defense':'top_to_bottom'},
    'layers':['Template/back_paper.png','Template/back_artwork_overlay.png','Template/back_frame.png','runtime symbols and text'],
    'clean_template':'Template/back_clean.png','clean_has_baked_stats':False,
    'symbol_canvas':[256,256],'indicator_canvas':[256,256],'stat_headers':[],
    'faction_centers':[list(FACTION_CENTER)],'faction_full_sprite_render_size':[FACTION_CELL,FACTION_CELL],
    'faction_placement':'single emblem inside central panel above ornament','faction_rotation_degrees':0,'faction_render_tint':'#e8d9b5 (ivory on red)',
    'inner_margin':{'left_axis':LEFT,'right_axis':RIGHT,'scallop_depth':DEPTH,'cream_stroke':5.5,'dark_understroke':11.5,'secondary_rule_offset':SECONDARY_OFFSET,'secondary_dark_stroke':4.5,'cusp_span':60,'rail_start_y':220,'rail_end_y':1316,'secondary_cap_join_y':cap_join_y,'perimeter_bounds':list(perimeter_bounds),'perimeter_radius':24,'outer_cap_x':51,'blending':'entire inset perimeter uses the same warm dark/ivory rule pair and paper grain; refined narrow worn exterior'},
    'left_tallies':{'centers':[[LEFT_PIP,y] for y in LY],'cusps':[[LEFT+DEPTH,y] for y in LY],'count':7,'step':148,'full':'Indicators/drop_full.png','empty':'Indicators/drop_empty.png',**geometry('drop',indicators['drop_full'])},
    'right_tallies':{'centers':[[RIGHT_PIP,y] for y in RY],'cusps':[[RIGHT-DEPTH,y] for y in RY],'count':7,'step':148,'full':'Indicators/defense_full.png','empty':'Indicators/defense_empty.png','source':'../SharedCardAssets/Sources/defense_simple_shield_upright.png',**geometry('defense',indicators['defense_full'])},
    'charges':{'centers':[[x,CHARGE_Y] for x in CHARGE_X],'ink_size':[CHARGE_SIZE,CHARGE_SIZE],'backplates_required':False},
    'banner':{'icon_center':[466,146],'icon_ink_envelope':[80,108],'value_center':[566,146],'value_font_size':140,'dynamic_text':True,'attack_icon':'Symbols/attack_spade.png','defense_icon':'Symbols/defense_club_B.png'},
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
    'left_count':len(LY),'right_count':len(RY),'step':148,'stat_headers':0,
    'left_alignment':all(a[1]==b[1] for a,b in zip(manifest['left_tallies']['centers'],manifest['left_tallies']['cusps'])),
    'right_alignment':all(a[1]==b[1] for a,b in zip(manifest['right_tallies']['centers'],manifest['right_tallies']['cusps'])),
    'preview_values':manifest['preview_values'],'files':{}}
qa['paper_exterior_grey_removed']=int(grey.sum())
qa['paper_exterior_grey_remaining']=int((grey&(cm>0)).sum())
qa['ornament_alpha_zero_pixels']=int((np.asarray(orn.getchannel('A'))==0).sum())
qa.update(verify(indicators))
qa['secondary_cap_join_exact']=secondary_left[0]==(LEFT-SECONDARY_OFFSET,cap_join_y) and secondary_left[-1]==(LEFT-SECONDARY_OFFSET,H-cap_join_y)
assert qa['secondary_cap_join_exact']
qa['defense_top_to_bottom_raster_checks']={}
for value in [0,1,4,7]:
    _,test_layer=assemble(health=0,defense=value,charges=0,banner=None)
    observed=[test_layer.getpixel((RIGHT_PIP,y))[3]>200 for y in RY]
    qa['defense_top_to_bottom_raster_checks'][str(value)]=observed==[i<value for i in range(7)]
assert all(qa['defense_top_to_bottom_raster_checks'].values())
qa['outer_pip_clearance_pixels']=min(LEFT_PIP-SIZES['drop'][0]/2-50,974-(RIGHT_PIP+SIZES['defense'][0]/2))
qa['tip_to_frame_cusp_clearance_pixels']={'drop':LEFT+DEPTH-(LEFT_PIP+SIZES['drop'][0]/2),'defense':RIGHT_PIP-SIZES['defense'][0]/2-(RIGHT-DEPTH)}
# Compare actual raster masks, not only the declared station coordinates.
pip_mask=Image.new('RGBA',(W,H))
for y in LY:
    stamp(pip_mask,indicators['drop_full'],(LEFT_PIP,y),SIZES['drop'])
    stamp(pip_mask,indicators['defense_full'],(RIGHT_PIP,y),SIZES['defense'])
qa['pip_frame_overlap_pixels']=int(((np.asarray(pip_mask)[:,:,3]>100)&(fa[:,:,3]>100)).sum())
faction_mask=Image.new('RGBA',(W,H))
for faction in ['sun','moon','saturn']:
    cell=symbols['faction_'+faction].resize((FACTION_CELL,FACTION_CELL),Image.Resampling.LANCZOS)
    faction_mask.alpha_composite(cell,(FACTION_CENTER[0]-FACTION_CELL//2,FACTION_CENTER[1]-FACTION_CELL//2))
qa['faction_ornament_overlap_pixels']=int(((np.asarray(faction_mask)[:,:,3]>100)&(np.asarray(art)[:,:,3]>100)).sum())
header_mask=faction_mask.copy()
qa['header_frame_overlap_pixels']=int(((np.asarray(header_mask)[:,:,3]>100)&(fa[:,:,3]>100)).sum())
qa['ornament_frame_overlap_pixels']=int(((np.asarray(art)[:,:,3]>100)&(fa[:,:,3]>100)).sum())
for name,im in {**symbols,**indicators}.items():
    al=np.asarray(im.getchannel('A'))
    qa['files'][name]={'size':list(im.size),'bbox':list(im.getchannel('A').getbbox()),'alpha_zero_pixels':int((al==0).sum())}
for shape in ['drop','defense']:
    qa[shape+'_state_bounds_match']=indicators[shape+'_full'].getchannel('A').getbbox()==indicators[shape+'_empty'].getchannel('A').getbbox()
assert all(qa[k] for k in ['independent_back_layout','rail_geometry_mirrors_horizontally','caps_mirror_vertically','single_faction_inside_panel','left_alignment','right_alignment','defense_primary_tip_rotated_left','drop_state_bounds_match','defense_state_bounds_match'])
assert qa['pip_frame_overlap_pixels']==0 and qa['faction_ornament_overlap_pixels']==0
assert qa['header_frame_overlap_pixels']==0 and qa['ornament_frame_overlap_pixels']==0
assert all(v['alpha_zero_pixels']>0 for v in qa['files'].values())
assert qa['paper_exterior_grey_remaining']==0 and qa['ornament_alpha_zero_pixels']>0
(ROOT/'QA_REPORT.json').write_text(json.dumps(qa,indent=2),encoding='utf-8')

# Review at full composition and actual target game size.
sheet=Image.new('RGB',(1440,1080),INK)
d=ImageDraw.Draw(sheet)
d.text((30,18),'NEON MONTE / RETRO - CORNICI SAGOMATE',font=font(28),fill=CREAM)
d.text((30,58),'GOCCIA / SCUDO SEMPLICE / NESSUNA INTESTAZIONE',font=font(19),fill=(151,183,171))
for i,faction in enumerate(['sun','moon','saturn']):
    im=previews[faction].resize((300,450),Image.Resampling.LANCZOS)
    sheet.paste(im,(30+i*320,105),im)
    d.text((30+i*320,567),faction.upper(),font=font(20),fill=CREAM)
small=previews['sun'].resize((224,336),Image.Resampling.LANCZOS)
sheet.paste(small,(1060,160),small)
d.text((1060,510),'224 x 336',font=font(20),fill=CREAM)
for i,(name,im) in enumerate(current_items.items()):
    col,row=i%7,i//7
    x,y=30+col*198,635+row*198
    sheet.paste(im.resize((112,112),Image.Resampling.LANCZOS),(x+24,y),im.resize((112,112),Image.Resampling.LANCZOS))
    d.text((x,y+124),name,font=font(16),fill=CREAM)
save(sheet,'CONTACT_SHEET.png')
save(previews['sun'].resize((224,336),Image.Resampling.LANCZOS),'QA/back_sun_224x336.png')
save(empty.resize((224,336),Image.Resampling.LANCZOS),'QA/back_empty_224x336.png')
readme=f'''# Neon Monte — retro sagomato, tacche a glifo

Revisione corrente **unified-border-back-v7**. Tutto il bordo interno condivide i filetti scuro/avorio delle curve; usura esterna più fine. Le tacche identificano da sole la statistica: **goccia per vita, scudo semplice per difesa**. Vita e difesa partono dall'alto. Nessun cuore, elmo o simbolo aggiuntivo di intestazione. Consegna di produzione: ../Cards_Final/README.md.

## File correnti

- CONTACT_SHEET.png: tre fazioni, sprite e riduzione a 224×336.
- Template/back_sun_preview.png, back_moon_preview.png, back_saturn_preview.png: esempi con overlay separati.
- Template/back_clean.png: base senza seme, statistiche, insegna o cariche.
- Template/back_paper.png, back_artwork_overlay.png, back_frame.png, back_frame.svg: livelli separati.
- Indicators/drop_full.png / drop_empty.png: goccia nelle proporzioni originali, rivolta a destra.
- Indicators/defense_full.png / defense_empty.png: scudo semplice con la **punta inferiore rivolta a sinistra**, verso il centro.
- Symbols/defense_club_B.png: master verticale usato soltanto per l'eventuale insegna di difesa.
- symbols_normalized_atlas.png, layout_manifest.json, QA_REPORT.json.

## Distribuzione

Sette tacche per lato alle y **{LY}**, passo **148**. Gocce su x={LEFT_PIP}, scudi su x={RIGHT_PIP}. Vita e difesa si riempiono entrambe dall'alto verso il basso: per valore N, sono piene le prime N posizioni. Il manifest dichiara tally_fill_order; la verifica raster copre 0, 1, 4 e 7 scudi.

Inchiostro delle gocce: **{SIZES['drop'][0]}×{SIZES['drop'][1]}**. Inchiostro dello scudo: **{SIZES['defense'][0]}×{SIZES['defense'][1]}**. Lo scudo segue l’ingombro della goccia originale. Le sagome sono condivise con il fronte tramite ../SharedCardAssets/tally_shapes.py.

Seme unico al centro (512,334), dentro il pannello interno. Insegna nella fascia superiore e cariche nella fascia inferiore: indicano informazioni diverse dalle statistiche laterali e rimangono. Esempi: vita 3/7, difesa 4/7, insegna attacco 2, cariche 1/3.

Le cornici laterali restano a x={LEFT}/{RIGHT}, con cuspidi di {DEPTH} px esattamente centrate sulle tacche e filetto secondario verso il margine. Le cornici, le fasce curve e l'ornamento centrale mantengono la composizione del retro precedente.

Bordo interno unificato: tutto il perimetro a (51,49)-(973,1487), raggio 24, usa lo stesso doppio filetto scuro/avorio 11.5/5.5 px delle curve. Filetto secondario a {SECONDARY_OFFSET} px, agganciato alle curve, spessori 4.5/2.5 px. Stessa lieve grana su tutto l'inchiostro, senza cambi di stile nei punti d'incontro. Il bordo cartaceo esterno usa abrasioni più fini della nuova sorgente imagegen; l'interno rosso originale è conservato da 128 px in poi. back_frame.png e back_frame.svg mostrano la stessa finitura; back_frame_geometry.svg conserva i tracciati editabili. Prompt in Sources/BACK_BORDER_REFINEMENT_PROMPT.md.

## Montaggio

Usare back_clean oppure paper → artwork → frame, poi seme, tacche, insegna e cariche. Le rotazioni degli ideogrammi sono già incorporate: **runtime a zero**. Lo scudo semplice è ruotato di 90° in senso orario: la punta inferiore guarda il centro.

I PNG sono celle 256×256 con sagome normalizzate a 192×120. Il manifest specifica ink_size, alpha_bbox e full_sprite_render_size: usare le misure del manifest per mantenere l’ingombro leggero della goccia. Pieno/vuoto mantengono gli stessi ingombri e pivot.

Il filetto inferiore è già interrotto attorno alle cariche. charge_backplates.png è un livello vuoto storico e non va montato.

## Verifica e ricostruzione

build_assets.py usa Pillow/NumPy e le sorgenti locali; nessuna nuova generazione image_gen per questa modifica geometrica. Conserva i master approvati e aggiorna frame, sprite, overlay, atlante e manifest. Le sorgenti raster precedenti sono documentate in Sources/PRODUCTION_PROMPTS.md.

QA: sette stazioni, allineamenti, orientamento rigido delle punte, alpha, bbox pieno/vuoto e assenza di sovrapposizioni con cornici, seme e ornamento. Verifica visiva anche a 224×336. Import Sprite, Point, niente compressione/mipmap, PPU 1, alpha come trasparenza; nessun prefab o scena Unity modificato.

Il master dello scudo precedente è riutilizzato nelle tacche correnti. Cuori e campioni con intestazioni sono storici. Le versioni precedenti degli script sono in Sources/*before_glyph_tallies*.
'''
(ROOT/'README.md').write_text(readme,encoding='utf-8')
print(json.dumps({'built':str(ROOT),'independent_layout':qa['independent_back_layout'],'pip_frame_overlap':qa['pip_frame_overlap_pixels'],'faction_ornament_overlap':qa['faction_ornament_overlap_pixels'],'sprites':len(entries)},indent=2))
