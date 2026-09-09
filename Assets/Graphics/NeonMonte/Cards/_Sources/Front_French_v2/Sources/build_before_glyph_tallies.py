from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageOps
import numpy as np
import json
import gc

ROOT = Path(__file__).resolve().parent
INK = (25, 33, 36)
RED = (181, 43, 38)
TEAL = (61, 123, 117)
MOSS = (108, 117, 66)
W, H = 1024, 1536
S = 3
LEFT_RAIL, RIGHT_RAIL = 140, 884
SCALLOP_DEPTH = 16
LEFT_PIP, RIGHT_PIP = 94, 930
PIP_SIZE = (94,59)
FACTION_Y, FACTION_SIZE = 180, (168,168)
INDEX_Y, ATTACK_SIZE = 385, (78,104)
INDEX_ZONE_HEIGHT = 174
def save(im, name):
    path = ROOT / name
    path.parent.mkdir(exist_ok=True, parents=True)
    im.save(path)

def normalize(im, size=(256,256), box=(192,192), stretch=False):
    im=im.copy()
    im.putalpha(im.getchannel('A').point(lambda p: p if p>24 else 0))
    bounds = im.getchannel('A').getbbox()
    assert bounds, 'Empty sprite'
    im = im.crop(bounds)
    if stretch:
        im = im.resize(box, Image.Resampling.LANCZOS)
    else:
        im.thumbnail(box, Image.Resampling.LANCZOS)
    out = Image.new('RGBA', size)
    out.alpha_composite(im, ((size[0]-im.width)//2,(size[1]-im.height)//2))
    return out

# The generated atlas contains a baked neutral checkerboard. Remove neutral
# pixels by chroma/luminance, retaining only the individual printed ink marks.
sheet = Image.open(ROOT/'Sources/symbols_generated.png').convert('RGBA')
names = ['sword','shield','heart','lozenge','star','charge_ring']
colors = [INK,INK,RED,INK,RED,INK]
FACTIONS = [('sun','SOLE',RED),('moon','LUNA',TEAL),('saturn','SATURNO',MOSS)]
ASTRONOMICAL_CHARS={'sun':'\u2609','moon':'\u263D','saturn':'\u2644'}
sprites = {}
for i,(name,col) in enumerate(zip(names,colors)):
    x,y = i%3,i//3
    tile = sheet.crop((round(x*sheet.width/3),round(y*sheet.height/3),round((x+1)*sheet.width/3),round((y+1)*sheet.height/3)))
    a = np.asarray(tile).astype(np.float32)
    hi,lo = a[:,:,:3].max(2),a[:,:,:3].min(2)
    if col == INK:
        mask = np.clip((100-hi)/35,0,1)
    else:
        mask = np.clip((hi-lo-24)/30,0,1)
    # Flatten to the shared ink palette; print wear is genuine transparent ink loss.
    out = np.zeros(a.shape,dtype=np.uint8)
    out[:,:,:3] = col
    out[:,:,3] = (mask*255).astype(np.uint8)
    tile = Image.fromarray(out)
    # Small isolated neutral compression marks must not affect the optical bounds.
    alpha=tile.getchannel('A')
    cleaned=alpha.filter(ImageFilter.MedianFilter(3))
    tile.putalpha(cleaned)
    # All faction motifs and the three indices share identical 192px envelopes.
    im=normalize(tile, stretch=name in ['faction_flame','faction_wave','faction_thorn','heart','shield'])
    if name=='sword':
        im=normalize(tile,box=(84,192),stretch=True)
    sprites[name]=im
    save(im,'Symbols/'+name+'.png')

# Established astronomical signs, verified against NASA and Unicode. Rasterize
# actual encoded glyphs, with modest weight correction for playing-card use.
# The font is read from Windows; no font file is copied into the asset package.
symbol_font=ImageFont.truetype('C:/Windows/Fonts/seguisym.ttf',720)
for slug,label,col in FACTIONS:
    char=ASTRONOMICAL_CHARS[slug]
    src=Image.new('RGBA',(1200,1200))
    draw=ImageDraw.Draw(src)
    draw.text((130,100),char,font=symbol_font,fill=col+(255,),stroke_width=5,stroke_fill=col+(255,))
    if slug=='moon':
        # Fill the outlined crescent, retaining its actual historical contour.
        # A solid crescent has the weight of a playing-card suit at small size.
        alpha=src.getchannel('A')
        solid=alpha.point(lambda p:255 if p>100 else 0)
        exterior=solid.copy();ImageDraw.floodfill(exterior,(0,0),128)
        filled=np.maximum(np.asarray(alpha),np.where(np.asarray(exterior)!=128,255,0).astype('uint8'))
        src=Image.new('RGBA',src.size,col+(0,))
        src.putalpha(Image.fromarray(filled))
    im=normalize(src,box=(192,192),stretch=False)
    sprites['faction_'+slug]=im
    save(im,'Symbols/faction_'+slug+'.png')

# Explicit front-card orientation: the lower-right sword points toward the flame.
sword_toward_flame=sprites['sword'].transpose(Image.Transpose.ROTATE_180)
save(sword_toward_flame,'Symbols/sword_toward_flame.png')

# Solid variation of the approved split spear: extend the lower blade tips
# along curved spade shoulders. All variants retain the same size and axis.
def bezier(a,b,c,d,n=32):
    return [tuple((1-t)**3*a[k]+3*(1-t)**2*t*b[k]+3*(1-t)*t*t*c[k]+t**3*d[k] for k in (0,1)) for t in np.linspace(0,1,n)]
def solid_spear(extension):
    right=bezier((128,12),(148,68),(173,114),(202,153))
    if extension:
        end=(192,153+extension)
        right+=bezier((202,153),(204,153+extension*.5),(198,153+extension*.9),end)
        right+=bezier(end,(168,158+extension*.25),(148,182),(146,202))
    else:
        right+=bezier((202,153),(167,161),(148,182),(146,202))
    right+=bezier((146,202),(146,211),(150,219),(153,224))
    points=right+[(103,224)]+[(256-x,y) for x,y in reversed(right)]
    face=[(128,12)]+right+[(128,224)]
    im=Image.new('RGBA',(1024,1024));draw=ImageDraw.Draw(im)
    draw.polygon([(round(x*4),round(y*4)) for x,y in points],fill=INK+(255,))
    draw.polygon([(round(x*4),round(y*4)) for x,y in face],fill=(61,75,77,255))
    return normalize(im.resize((256,256),Image.Resampling.LANCZOS),box=(144,192),stretch=True),points,face
weapon,weapon_points,facet=solid_spear(0)
solid_variants=[solid_spear(depth)[0] for depth in (0,18,36)]
for label,variant in zip(('A_closed','B_balanced','C_extended'),solid_variants):
    save(variant,'Studies/attack_'+label+'.png')
blade_is_solid=all(weapon.getpixel((128,y))[3]==255 for y in range(50,210))
save(weapon,'Symbols/attack_spade.png')
weapon_down=weapon.transpose(Image.Transpose.ROTATE_180)
save(weapon_down,'Symbols/attack_spade_toward_faction.png')
sprites.pop('sword')
sprites={'attack_spade':weapon,**sprites}
def svg_polygon_path(points):
    return 'M'+' L'.join(f'{x} {y}' for x,y in points)+' Z'
(ROOT/'Symbols/attack_spade.svg').write_text('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256"><path fill="#192124" d="'+svg_polygon_path(weapon_points)+'"/><path fill="#3d4b4d" d="'+svg_polygon_path(facet)+'"/></svg>',encoding='utf-8')

# Exact thin-stem and charge states complement the regenerated original library.
stem=Image.new('RGBA',(256,256)); ImageDraw.Draw(stem).line((128,32,128,224),fill=INK+(255,),width=4)
save(stem,'Symbols/stem.png'); sprites['stem']=stem
ring=Image.new('RGBA',(768,768)); rd=ImageDraw.Draw(ring)
rd.ellipse((120,120,648,648),outline=INK+(255,),width=15)
ring=ring.resize((256,256),Image.Resampling.LANCZOS)
save(ring,'Symbols/charge_ring.png');sprites['charge_ring']=ring
full=Image.new('RGBA',(768,768));ImageDraw.Draw(full).ellipse((120,120,648,648),fill=RED+(255,))
full=full.resize((256,256),Image.Resampling.LANCZOS)
save(full,'Symbols/charge_full.png');sprites['charge_full']=full

drop=Image.open(ROOT/'Sources/drop_generated.png').convert('RGBA')
drop=normalize(drop,box=(192,120),stretch=True)
# Recover the alpha contour and use exactly the established card red.
da=np.asarray(drop).copy();da[:,:,:3]=RED;drop=Image.fromarray(da)
save(drop,'Indicators/drop_full.png')

# A matched outline has the exact same silhouette and external bounds as full.
def outline_of(im,color,width=9):
    alpha=im.getchannel('A')
    # Close internal print pinholes before eroding, avoiding halos around texture.
    solid=alpha.point(lambda p:255 if p>90 else 0).filter(ImageFilter.MaxFilter(7)).filter(ImageFilter.MinFilter(7))
    inner=solid.filter(ImageFilter.MinFilter(width*2+1))
    edge=Image.fromarray(np.maximum(np.asarray(alpha).astype(int)-np.asarray(inner).astype(int),0).astype('uint8'))
    out=Image.new('RGBA',im.size,color+(0,));out.putalpha(edge)
    return out
drop_empty=outline_of(drop,RED,7);save(drop_empty,'Indicators/drop_empty.png')

# User-requested geometric simplification: two solid adjoining blade facets.
# The ridge is only a color boundary, with no pale stroke or transparent slit.
spear=Image.new('RGBA',(1024,1024));sd=ImageDraw.Draw(spear)
blade=[(32,128),(96,68),(224,128),(96,188)]
sd.polygon([(x*4,y*4) for x,y in blade],fill=INK+(255,))
sd.polygon([(x*4,y*4) for x,y in [blade[0],blade[1],blade[2]]],fill=(82,96,96,255))
spear=normalize(spear.resize((256,256),Image.Resampling.LANCZOS),box=(192,120),stretch=True)
save(spear,'Indicators/spear_full.png')
spear_empty=outline_of(spear,INK,7);save(spear_empty,'Indicators/spear_empty.png')
indicators={'drop_full':drop,'drop_empty':drop_empty,'spear_full':spear,'spear_empty':spear_empty}

# Paper comes from an empty region of the generated art; never clone ink into it.
del sheet,a,hi,lo,mask,out,tile,alpha,cleaned,src,draw,solid,exterior,filled,sd
gc.collect()
paper=Image.open(ROOT/'Sources/paper_reference.png').convert('RGB').crop((225,255,800,1260)).resize((W,H),Image.Resampling.LANCZOS).convert('RGBA')
cardmask=Image.new('L',(W*S,H*S));ImageDraw.Draw(cardmask).rounded_rectangle((0,0,W*S-1,H*S-1),radius=42*S,fill=255)
paper.putalpha(cardmask.resize((W,H),Image.Resampling.LANCZOS))
save(paper,'Template/front_paper.png')
del cardmask

right_y=[FACTION_Y+i*140 for i in range(7)]
left_y=[H-y for y in reversed(right_y)]
assert len(right_y)==len(left_y)==7
assert all(a+b==H for a,b in zip(left_y,reversed(right_y)))

def bezier(points,n=30):
    p=np.array(points,dtype=float)
    return [tuple((1-t)**3*p[0]+3*(1-t)**2*t*p[1]+3*(1-t)*t*t*p[2]+t**3*p[3]) for t in np.linspace(0,1,n)]
if '--reuse-frame' in __import__('sys').argv:
    frame=Image.open(ROOT/'Template/front_frame.png').convert('RGBA')
else:
    frame=Image.new('RGBA',(W*S,H*S));fd=ImageDraw.Draw(frame)
    fd.rounded_rectangle((31*S,22*S,993*S,1514*S),radius=40*S,outline=INK+(255,),width=4*S)
    half=Image.new('RGBA',frame.size);hd=ImageDraw.Draw(half)
    points=[(250,245),(250,145)]
    points+=bezier([(250,145),(250,104),(276,78),(319,78)])
    points +=[(803,78)]
    points+=bezier([(803,78),(861,78),(RIGHT_RAIL,103),(RIGHT_RAIL,145)])
    for cy in right_y:
        points.append((RIGHT_RAIL,cy-25))
        points+=bezier([(RIGHT_RAIL,cy-25),(RIGHT_RAIL,cy-12),(RIGHT_RAIL-7,cy-5),(RIGHT_RAIL-SCALLOP_DEPTH,cy)])
        points+=bezier([(RIGHT_RAIL-SCALLOP_DEPTH,cy),(RIGHT_RAIL-7,cy+5),(RIGHT_RAIL,cy+12),(RIGHT_RAIL,cy+25)])
    points.append((RIGHT_RAIL,1048))
    hd.line([(round(x*S),round(y*S)) for x,y in points],fill=INK+(255,),width=4*S,joint='curve')
    frame.alpha_composite(half);frame.alpha_composite(half.transpose(Image.Transpose.ROTATE_180))
    frame=frame.resize((W,H),Image.Resampling.LANCZOS)
    fa=np.asarray(frame).copy()
    fa[:,:,3]=np.maximum(fa[:,:,3],fa[::-1,::-1,3])
    fa[:,:,:3]=INK
    frame=Image.fromarray(fa)
    save(frame,'Template/front_frame.png')
base=Image.alpha_composite(paper,frame);save(base,'Template/front_clean.png')

def stamp(layer,im,center,size,rotate=False):
    # Ignore source transparent padding: layout boxes describe the visible ink.
    b=im.getchannel('A').getbbox();im=im.crop(b)
    im=im.resize(size,Image.Resampling.LANCZOS)
    if rotate:im=im.transpose(Image.Transpose.ROTATE_180)
    layer.alpha_composite(im,(round(center[0]-size[0]/2),round(center[1]-size[1]/2)))

def assemble(faction,filled=True):
    ink=Image.new('RGBA',(W,H))
    # Preserve each historical glyph's proportions inside a common square.
    faction_cell=sprites[faction].resize((224,224),Image.Resampling.LANCZOS)
    ink.alpha_composite(faction_cell,(LEFT_RAIL-112,FACTION_Y-112))
    ink.alpha_composite(faction_cell.transpose(Image.Transpose.ROTATE_180),(RIGHT_RAIL-112,H-FACTION_Y-112))
    stamp(ink,sprites['heart'],(LEFT_RAIL,INDEX_Y),(88,88))
    stamp(ink,weapon,(RIGHT_RAIL,H-INDEX_Y),ATTACK_SIZE)
    stamp(ink,sprites['star'],(248,73),(45,45))
    stamp(ink,sprites['star'],(776,1463),(45,45),True)
    for i,y in enumerate(left_y):
        stamp(ink,drop if filled and i<3 else drop_empty,(LEFT_PIP,y),PIP_SIZE)
    for i,y in enumerate(right_y):
        stamp(ink,spear if filled and i>=3 else spear_empty,(RIGHT_PIP,y),PIP_SIZE)
    for x in (466,512,558):
        patch=paper.crop((x-16,1442,x+16,1474))
        mask=Image.new('L',(96,96));ImageDraw.Draw(mask).ellipse((0,0,95,95),fill=255)
        patch.putalpha(mask.resize((32,32),Image.Resampling.LANCZOS))
        ink.alpha_composite(patch,(x-16,1442))
        stamp(ink,ring,(x,1458),(32,32))
    return Image.alpha_composite(base,ink),ink

for fac,_,_ in FACTIONS:
    card,overlay=assemble('faction_'+fac)
    save(card,'Template/front_'+fac+'_preview.png')
    save(overlay,'Template/front_'+fac+'_overlay.png')
empty,overlay=assemble('faction_sun',False);save(empty,'Template/front_empty_preview.png')

# Common cell-size atlas, with real alpha. Individual files are the primary assets.
allitems=list(sprites.items())+list(indicators.items())
atlas=Image.new('RGBA',(1024,1024))
for i,(name,im) in enumerate(allitems):atlas.alpha_composite(im,((i%4)*256,(i//4)*256))
save(atlas,'symbols_normalized_atlas.png')

font_path='C:/Windows/Fonts/consola.ttf'
font=ImageFont.truetype(font_path,18)
small=ImageFont.truetype(font_path,14)
proof=Image.new('RGB',(1400,1030),(28,35,36));pd=ImageDraw.Draw(proof)
pd.text((30,20),'NEON MONTE / SEMI ASTRONOMICI',font=ImageFont.truetype(font_path,28),fill=(232,216,181))
pd.text((30,58),'7 TACCHЕ PER LATO  /  SIMMETRIA 180°  /  ASSET SEPARATI',font=font,fill=(142,174,161))
for j,(fac,label,_) in enumerate(FACTIONS):
    card=Image.open(ROOT/f'Template/front_{fac}_preview.png');card.thumbnail((300,450))
    proof.paste(card,(30+j*320,102),card)
    pd.text((30+j*320,565),label,font=font,fill=(232,216,181))
for i,(name,im) in enumerate(allitems):
    x=30+(i%8)*170;y=630+(i//8)*160
    pd.rounded_rectangle((x,y,x+140,y+115),radius=8,fill=(229,215,182))
    icon=im.resize((108,108),Image.Resampling.LANCZOS);proof.paste(icon,(x+16,y+3),icon)
    pd.text((x,y+122),name,font=small,fill=(232,216,181))
# Actual game-size card to inspect legibility without zooming.
mini=Image.open(ROOT/'Template/front_sun_preview.png').resize((224,336),Image.Resampling.LANCZOS)
proof.paste(mini,(1080,140),mini);pd.text((1080,490),'224 x 336',font=font,fill=(232,216,181))
save(proof,'CONTACT_SHEET_SOLID_SPEAR.png')

comparison=Image.new('RGB',(760,410),(234,222,193));cd=ImageDraw.Draw(comparison)
cd.text((26,20),'VITA / ATTACCO - LAMA PIENA',font=ImageFont.truetype(font_path,25),fill=INK)
for i,(name,icon) in enumerate([('CUORE',sprites['heart']),('PUNTA / ATTACCO',weapon)]):
    tile=Image.new('RGBA',(240,240))
    stamp(tile,icon,(120,120),(150,150) if i==0 else (133,177))
    comparison.paste(tile,(65+i*360,65),tile)
    cd.text((105+i*360,305),name,font=font,fill=INK)
    tiny=Image.new('RGBA',(40,40));stamp(tiny,icon,(20,20),(20,20) if i==0 else (18,24))
    comparison.paste(tiny,(160+i*360,340),tiny)
save(comparison,'ATTACK_SOLID_COMPARISON.png')

study=Image.new('RGB',(1080,520),(234,222,193));sd=ImageDraw.Draw(study)
sd.text((28,20),'LAMA PIENA / ESTENSIONE DELLE PUNTE INFERIORI',font=ImageFont.truetype(font_path,25),fill=INK)
for i,(label,variant) in enumerate(zip(('A / SAGOMA INIZIALE','B / INTERMEDIA','C / PIU ESTESA'),solid_variants)):
    x=180+i*360
    tile=Image.new('RGBA',(240,260));stamp(tile,variant,(120,130),(144,192))
    study.paste(tile,(x-120,65),tile)
    sd.text((x-125,335),label,font=font,fill=INK)
    pair=Image.new('RGBA',(100,48));stamp(pair,sprites['heart'],(25,24),(25,25));stamp(pair,variant,(75,24),(22,30))
    study.paste(pair,(x-50,380),pair)
sd.text((28,467),'A predefinita nei template. B e C conservate come alternative.',font=font,fill=INK)
save(study,'ATTACK_SOLID_STUDY.png')

# Silhouette proof: shape must carry identification independently of faction ink.
legibility=Image.new('RGB',(1000,500),(234,222,193));ld=ImageDraw.Draw(legibility)
ld.text((30,20),'SOLE / LUNA / SATURNO - PROVA DEI SEMI',font=ImageFont.truetype(font_path,24),fill=INK)
for i,(slug,label,col) in enumerate(FACTIONS):
    glyph=sprites['faction_'+slug]
    for suffix,color in [('mask',(255,255,255)),('black',INK)]:
        mono=Image.new('RGBA',glyph.size,color+(0,));mono.putalpha(glyph.getchannel('A'))
        save(mono,f'Symbols/faction_{slug}_{suffix}.png')
    x=70+i*320
    big=mono.resize((180,180),Image.Resampling.LANCZOS);legibility.paste(big,(x,70),big)
    ld.text((x,265),label+' / U+'+f'{ord(ASTRONOMICAL_CHARS[slug]):04X}',font=font,fill=INK)
    for j,size in enumerate((16,24,36)):
        # Source ink occupies 3/4 of the cell; specify actual maximum ink height.
        thumb=mono.resize((round(size*4/3),round(size*4/3)),Image.Resampling.LANCZOS)
        legibility.paste(thumb,(x+j*75,325),thumb)
        ld.text((x+j*75,385),str(size)+' px',font=small,fill=INK)
save(legibility,'SUIT_LEGIBILITY.png')

manifest={'canvas':[W,H],'coordinates':'top-left origin, y down','runtime_card_size':[224,336],
 'symbol_canvas':[256,256],'symbol_ink_envelope':[192,192], 'indicator_canvas':[256,256],'indicator_ink_envelope':[192,120],
 'faction_centers':[[LEFT_RAIL,FACTION_Y],[RIGHT_RAIL,H-FACTION_Y]],'faction_size':list(FACTION_SIZE),'second_faction_rotation_degrees':180,
 'faction_designs':[{'id':slug,'design_label':label,'character':ASTRONOMICAL_CHARS[slug],'unicode':f'U+{ord(ASTRONOMICAL_CHARS[slug]):04X}','file':f'Symbols/faction_{slug}.png','preview':f'Template/front_{slug}_preview.png','alpha_bbox':sprites['faction_'+slug].getchannel('A').getbbox()} for slug,label,_ in FACTIONS],
 'faction_full_sprite_render_size':[224,224],
 'attack':{'file':'Symbols/attack_spade.png','center':[RIGHT_RAIL,H-INDEX_Y],'ink_size':list(ATTACK_SIZE),'reserved_height':INDEX_ZONE_HEIGHT,'rotation_degrees':0,'tip':'up, toward attack tally rail','motif':'solid spearhead, initial closed silhouette','default_variant':'A','lower_tip_extension':0,'study':'ATTACK_SOLID_STUDY.png','generated_reference':'Sources/attack_split_generated.png'},
 'heart':{'center':[LEFT_RAIL,INDEX_Y],'ink_size':[88,88],'reserved_height':INDEX_ZONE_HEIGHT,'vertical_padding_each':(INDEX_ZONE_HEIGHT-88)/2},
 'inner_margin':{'left_axis':LEFT_RAIL,'right_axis':RIGHT_RAIL,'heart_axis':LEFT_RAIL,'attack_axis':RIGHT_RAIL,'scallop_depth':SCALLOP_DEPTH},
 'left_tallies':{'centers':[[LEFT_PIP,y] for y in left_y],'size':list(PIP_SIZE),'count':7,'tip':'right/inward','belly':'left/outward'},
 'right_tallies':{'centers':[[RIGHT_PIP,y] for y in right_y],'size':list(PIP_SIZE),'count':7,'tip':'right/outward','shape':'rhombic spearhead, two solid facets, no central slit'},
 'safe_regions':{'title':[300,112,790,214],'artwork':[254,282,770,1254],'ability':[234,1322,724,1424]},
 'charges':[[466,1458],[512,1458],[558,1458]],
 'atlas':{name:{'rect_top_left':[(i%4)*256,(i//4)*256,256,256],'pivot':[.5,.5]} for i,(name,im) in enumerate(allitems)},
 'import':{'textureType':'Sprite','filterMode':'Point','compression':'None','mipmaps':False,'pixelsPerUnit':1,'alphaIsTransparency':True},
 'notes':'Preview shows 3 life and 4 attack active. Values are illustrative. front_clean contains no state or faction. Frame geometry has exact 180-degree symmetry; the three charge circles are intentionally on lower edge only, as in source.'}
(ROOT/'layout_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')

report={'frame_rotation_equal':bool(np.array_equal(np.asarray(frame),np.asarray(frame.transpose(Image.Transpose.ROTATE_180)))),'left_count':len(left_y),'right_count':len(right_y),'left_last_equals_opposite_faction_center':left_y[-1]==H-FACTION_Y,'right_first_equals_opposite_faction_center':right_y[0]==FACTION_Y,'assets':{}}
report['margin_axes_match_indices']=manifest['inner_margin']['left_axis']==manifest['heart']['center'][0] and manifest['inner_margin']['right_axis']==manifest['attack']['center'][0]
report['shared_index_reservation']=manifest['heart']['reserved_height']==manifest['attack']['reserved_height']
report['attack_elongated']=ATTACK_SIZE[1]>88 and ATTACK_SIZE[0]<88
report['attack_symmetric_outline']=all(any(abs(px-(256-x))<1e-6 and abs(py-y)<1e-6 for px,py in weapon_points) for x,y in weapon_points)
report['attack_blade_center_solid']=blade_is_solid
assert report['attack_blade_center_solid']
report['heart_vertical_padding_each']=manifest['heart']['vertical_padding_each']
report['attack_to_faction_gap']=(H-FACTION_Y-FACTION_SIZE[1]/2)-(H-INDEX_Y+ATTACK_SIZE[1]/2)
assert report['shared_index_reservation'] and report['attack_elongated'] and report['attack_symmetric_outline'] and report['attack_to_faction_gap']>=24
report['horizontal_clearance']={'left_outer':LEFT_PIP-PIP_SIZE[0]/2-31,'left_inner':LEFT_RAIL+SCALLOP_DEPTH-(LEFT_PIP+PIP_SIZE[0]/2),'right_inner':RIGHT_PIP-PIP_SIZE[0]/2-(RIGHT_RAIL-SCALLOP_DEPTH),'right_outer':993-(RIGHT_PIP+PIP_SIZE[0]/2)}
report['spear_ridge_opaque']=min(spear.getpixel((x,128))[3] for x in range(40,215))==255
report['faction_common_bounds']=all((lambda b: b[0]>=32 and b[1]>=32 and b[2]<=224 and b[3]<=224)(sprites['faction_'+slug].getchannel('A').getbbox()) for slug,_,_ in FACTIONS)
assert report['faction_common_bounds']
assert report['margin_axes_match_indices'] and report['spear_ridge_opaque']
for name,im in allitems:
    report['assets'][name]={'canvas':list(im.size),'alpha_bbox':im.getchannel('A').getbbox(),'corner_alpha':im.getpixel((0,0))[3]}
for name,im in indicators.items():
    b=im.getchannel('A').getbbox()
    assert b==(32,68,224,188),(name,b)
assert report['frame_rotation_equal']
assert report['left_last_equals_opposite_faction_center'] and report['right_first_equals_opposite_faction_center']
(ROOT/'QA_REPORT.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='assets'},indent=2))
print('Saved',len(list(ROOT.rglob('*.png'))),'PNG files')
