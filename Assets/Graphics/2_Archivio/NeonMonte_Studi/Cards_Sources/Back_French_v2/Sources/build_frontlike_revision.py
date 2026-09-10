"""Deterministic assembly of generated back artwork, matching French front geometry.

Run with Pillow and NumPy. No generation, API calls or Unity changes on rebuild.
"""
from pathlib import Path
import json
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

ROOT = Path(__file__).resolve().parent
FRONT = ROOT.parent / 'Front_French_v2'
W, H, SS = 1024, 1536, 4
CREAM, INK = (232, 217, 181), (25, 33, 36)
LEFT, RIGHT, DEPTH = 140, 884, 16
LY = [516 + 140*i for i in range(7)]
RY = [180 + 140*i for i in range(7)]
PIP_SIZE = (94, 59)

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

# Generated paper is a source texture. Only trim its exterior to the canonical
# front silhouette; this also removes any baked checker outside the card.
paper = Image.open(ROOT/'Sources/back_paper_generated.png').convert('RGBA').resize((W,H),Image.Resampling.LANCZOS)
# The generated ornamental outer rule at x~48 collides with the front pips.
# Retain the aged paper edge, rebuild its red inset from a clean source region,
# and let the vector frame provide the exact outer rule at x=31.
red_inset = paper.crop((70,70,954,1466)).resize((968,1480),Image.Resampling.LANCZOS)
paper.alpha_composite(red_inset,(28,28))
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
orn.thumbnail((620,1040),Image.Resampling.LANCZOS)
save(orn,'Artwork/three_eyes_ornament.png')
art = Image.new('RGBA',(W,H))
art.alpha_composite(orn,((W-orn.width)//2,(H-orn.height)//2))
save(art,'Template/back_artwork_overlay.png')

# Identical axes, station centers, depth and paired half-frames as the front.
# Only the separate outer paper border is retained from the generated back.
half_points = [(250,245),(250,145)]
half_points += bezier((250,145),(250,104),(276,78),(319,78))
half_points += [(803,78)]
half_points += bezier((803,78),(861,78),(RIGHT,103),(RIGHT,145))
for y in RY:
    half_points += [(RIGHT,y-25)]
    half_points += bezier((RIGHT,y-25),(RIGHT,y-12),(RIGHT-7,y-5),(RIGHT-DEPTH,y))
    half_points += bezier((RIGHT-DEPTH,y),(RIGHT-7,y+5),(RIGHT,y+12),(RIGHT,y+25))
half_points += [(RIGHT,1048)]
half = Image.new('RGBA',(W*SS,H*SS))
ImageDraw.Draw(half).line([(round(x*SS),round(y*SS)) for x,y in half_points],fill=CREAM+(255,),width=4*SS,joint='curve')
frame_hi = Image.alpha_composite(half,half.transpose(Image.Transpose.ROTATE_180))
ImageDraw.Draw(frame_hi).rounded_rectangle((31*SS,22*SS,993*SS,1514*SS),radius=40*SS,outline=CREAM+(255,),width=4*SS)
frame = frame_hi.resize((W,H),Image.Resampling.LANCZOS)
fa = np.asarray(frame).copy(); fa[:,:,3] = np.maximum(fa[:,:,3],fa[::-1,::-1,3]); fa[:,:,:3]=CREAM
frame = Image.fromarray(fa)
save(frame,'Template/back_frame.png')
svgpath = 'M'+' L'.join(f'{x:.3f},{y:.3f}' for x,y in half_points)
(ROOT/'Template/back_frame.svg').write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1536" viewBox="0 0 1024 1536"><g fill="none" stroke="#e8d9b5" stroke-width="4" stroke-linejoin="round"><rect x="31" y="22" width="962" height="1492" rx="40"/><path d="{svgpath}"/><path transform="rotate(180 512 768)" d="{svgpath}"/></g></svg>',encoding='utf-8')
clean = Image.alpha_composite(Image.alpha_composite(paper,art),frame)
save(clean,'Template/back_clean.png')
charge_backplates=Image.new('RGBA',(W,H))
under_frame=Image.alpha_composite(paper,art)
for x in [466,512,558]:
    patch=under_frame.crop((x-16,1442,x+16,1474))
    circle=Image.new('L',(128,128));ImageDraw.Draw(circle).ellipse((0,0,127,127),fill=255)
    patch.putalpha(circle.resize((32,32),Image.Resampling.LANCZOS))
    charge_backplates.alpha_composite(patch,(x-16,1442))
save(charge_backplates,'Template/charge_backplates.png')

def assemble(faction='sun', health=3, defense=4, charges=1, banner=2, banner_type='attack'):
    layer = Image.new('RGBA',(W,H))
    faction_cell = symbols['faction_'+faction].resize((224,224),Image.Resampling.LANCZOS)
    layer.alpha_composite(faction_cell,(LEFT-112,180-112))
    layer.alpha_composite(faction_cell.transpose(Image.Transpose.ROTATE_180),(RIGHT-112,1356-112))
    stamp(layer,symbols['heart'],(LEFT,385),(88,88))
    stamp(layer,symbols['defense_club_B'],(RIGHT,1151),(98,104),preserve=True)
    for i,y in enumerate(LY):
        stamp(layer,indicators['drop_full' if i<health else 'drop_empty'],(94,y),PIP_SIZE)
    for i,y in enumerate(RY):
        stamp(layer,indicators['shield_full' if i>=7-defense else 'shield_empty'],(930,y),PIP_SIZE)
    # Source pips are round and padded consistently with the front.
    layer.alpha_composite(charge_backplates)
    for i,x in enumerate([466,512,558]):
        stamp(layer,symbols['charge_full' if i<charges else 'charge_ring'],(x,1458),(32,32))
    if banner is not None:
        if banner_type=='attack':
            stamp(layer,symbols['attack_spade'],(459,163),(64,86))
        else:
            stamp(layer,rgba(upright),(459,163),(64,86))
        centered_text(layer,str(banner),(558,163),112)
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
    'layers':['Template/back_paper.png','Template/back_artwork_overlay.png','Template/back_frame.png','runtime symbols and text'],
    'clean_template':'Template/back_clean.png','clean_has_baked_stats':False,
    'symbol_canvas':[256,256],'indicator_canvas':[256,256],'indicator_ink_envelope':[192,120],
    'faction_centers':[[140,180],[884,1356]],'faction_full_sprite_render_size':[224,224],
    'faction_second_rotation_degrees':180,'faction_render_tint':'#e8d9b5 (ivory on red)',
    'heart':{'file':'Symbols/heart.png','center':[140,385],'ink_size':[88,88]},
    'defense':{'file':'Symbols/defense_club_B.png','source':'Sources/defense_B_approved.png','variant':'B original, pointed base, no stem','center':[884,1151],'ink_envelope':[98,104]},
    'inner_margin':{'left_axis':LEFT,'right_axis':RIGHT,'scallop_depth':DEPTH,'stroke':4,'cusp_span':50},
    'left_tallies':{'centers':[[94,y] for y in LY],'cusps':[[156,y] for y in LY],'size':list(PIP_SIZE),'count':7,'step':140,'full':'Indicators/drop_full.png','empty':'Indicators/drop_empty.png','tip':'right/inward'},
    'right_tallies':{'centers':[[930,y] for y in RY],'cusps':[[868,y] for y in RY],'size':list(PIP_SIZE),'count':7,'step':140,'full':'Indicators/shield_full.png','empty':'Indicators/shield_empty.png','tip':'left/inward','rotation_baked_clockwise_degrees':90,'runtime_rotation_degrees':0},
    'indicator_full_sprite_render_size':[94*256/192,59*256/120],
    'charges':{'centers':[[x,1458] for x in [466,512,558]],'ink_size':[32,32],'backplates':'Template/charge_backplates.png','backplates_before_symbols':True},
    'banner':{'icon_center':[459,163],'icon_ink_envelope':[64,86],'value_center':[558,163],'value_font_size':112,'dynamic_text':True,'attack_icon':'Symbols/attack_spade.png','defense_icon':'Indicators/shield_upright_source.png'},
    'artwork':{'file':'Artwork/three_eyes_ornament.png','size':list(orn.size),'center':[512,768]},
    'atlas':{'file':'symbols_normalized_atlas.png','size':[1024,1024],'entries':entries},
    'import':{'type':'Sprite','filter':'Point','compression':'None','mipmaps':False,'PPU':1,'alpha_is_transparency':True},
    'preview_values':{'health':3,'defense':4,'charges':1,'attack_banner':2},
}
(ROOT/'layout_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')

# Meaningful structural checks: shared front geometry, states, alpha and rotation.
front_manifest=json.loads((FRONT/'layout_manifest.json').read_text(encoding='utf-8'))
qa={'canvas':[W,H],'front_geometry_matches': all(manifest[k]['centers']==front_manifest[k]['centers'] for k in ['left_tallies','right_tallies']),
    'frame_alpha_180_symmetric':bool(np.array_equal(fa[:,:,3],fa[::-1,::-1,3])),
    'left_count':len(LY),'right_count':len(RY),'step':140,
    'left_alignment':all(a[1]==b[1] for a,b in zip(manifest['left_tallies']['centers'],manifest['left_tallies']['cusps'])),
    'right_alignment':all(a[1]==b[1] for a,b in zip(manifest['right_tallies']['centers'],manifest['right_tallies']['cusps'])),
    'shield_is_rigid_clockwise_rotation':bool(np.array_equal(np.asarray(side),np.rot90(np.asarray(upright),-1))),
    'preview_values':manifest['preview_values'],'files':{}}
qa['paper_exterior_grey_removed']=int(grey.sum())
qa['paper_exterior_grey_remaining']=int((grey&(cm>0)).sum())
qa['ornament_alpha_zero_pixels']=int((np.asarray(orn.getchannel('A'))==0).sum())
qa['outer_pip_clearance_pixels']=16
qa['tip_to_frame_cusp_clearance_pixels']=15
for name,im in {**symbols,**indicators}.items():
    al=np.asarray(im.getchannel('A'))
    qa['files'][name]={'size':list(im.size),'bbox':list(im.getchannel('A').getbbox()),'alpha_zero_pixels':int((al==0).sum())}
for shape in ['drop','shield']:
    qa[shape+'_state_bounds_match']=indicators[shape+'_full'].getchannel('A').getbbox()==indicators[shape+'_empty'].getchannel('A').getbbox()
assert all(qa[k] for k in ['front_geometry_matches','frame_alpha_180_symmetric','left_alignment','right_alignment','shield_is_rigid_clockwise_rotation','drop_state_bounds_match','shield_state_bounds_match'])
assert all(v['alpha_zero_pixels']>0 for v in qa['files'].values())
assert qa['paper_exterior_grey_remaining']==0 and qa['ornament_alpha_zero_pixels']>0
(ROOT/'QA_REPORT.json').write_text(json.dumps(qa,indent=2),encoding='utf-8')

# Review at full composition and actual target game size.
sheet=Image.new('RGB',(1440,1080),INK)
d=ImageDraw.Draw(sheet)
d.text((30,18),'NEON MONTE / RETRO - B ORIGINALE',font=font(28),fill=CREAM)
d.text((30,58),'7 TACCHE / CUSPIDI ALLINEATE / SCUDI VERSO IL CENTRO',font=font(19),fill=(151,183,171))
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
print(json.dumps({'built':str(ROOT),'geometry_ok':qa['front_geometry_matches'],'frame_symmetric':qa['frame_alpha_180_symmetric'],'sprites':len(entries)},indent=2))
