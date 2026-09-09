"""Shared lightweight tallies: original drop, rhombic spear and simple shield.

Assets only. Both face builders use these masters without executing one another.
"""
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter, ImageChops

ROOT=Path(__file__).resolve().parent
INK=(25,33,36)
CREAM=(232,217,181)
RED=(181,43,38)
# The original drop envelope controls the weight of all three tallies.
FACE_SIZES={
    'front':{name:(94,59) for name in ['drop','attack','defense']},
    'back':{name:(108,68) for name in ['drop','attack','defense']},
}
SIZES=FACE_SIZES['front']

def tint(im,color):
    out=Image.new('RGBA',im.size,color+(0,))
    out.putalpha(im.getchannel('A'))
    return out

def edge(im,width=7,color=INK):
    alpha=im.getchannel('A')
    # Matched outside silhouette; eroding only the interior preserves pivot/bbox.
    inner=alpha.filter(ImageFilter.MinFilter(width*2+1))
    out=Image.new('RGBA',im.size,color+(0,))
    out.putalpha(ImageChops.subtract(alpha,inner))
    return out

def masters():
    attack=Image.open(ROOT/'Sources/attack_rhombic_spear.png').convert('RGBA')
    defense=Image.open(ROOT/'Sources/defense_simple_shield_upright.png').convert('RGBA')
    drop=Image.open(ROOT/'Sources/drop_before_rebalance.png').convert('RGBA')
    # Mirror the right-pointing spear to preserve its upper lighter facet.
    # A clockwise quarter turn sends the shield's bottom point to the left.
    return {'drop':drop,'attack':attack.transpose(Image.Transpose.FLIP_LEFT_RIGHT),
            'defense':defense.transpose(Image.Transpose.ROTATE_270)}

def build_tallies(face):
    out={}
    for name,im in masters().items():
        full=tint(im,CREAM) if face=='back' else (im if name=='attack' else tint(im,RED if name=='drop' else INK))
        color=CREAM if face=='back' else (RED if name=='drop' else INK)
        out[name+'_full']=full
        out[name+'_empty']=edge(full,color=color)
    return out

def export_tallies(destination,face):
    destination=Path(destination);destination.mkdir(parents=True,exist_ok=True)
    tallies=build_tallies(face)
    for name,im in tallies.items():
        im.save(destination/(name+'.png'))
        tint(im,(255,255,255)).save(destination/(name+'_mask.png'))
    return tallies

def geometry(name,im,face='front'):
    b=im.getchannel('A').getbbox();width,height=FACE_SIZES[face][name]
    return {'ink_size':[width,height],'alpha_bbox':list(b),'sprite_canvas':[256,256],
            'full_sprite_render_size':[width*256/(b[2]-b[0]),height*256/(b[3]-b[1])],
            'pivot':[0.5,0.5],'tip':'right/inward' if name=='drop' else 'left/inward',
            'source_transform':{'drop':'original unchanged','attack':'horizontal mirror','defense':'90 degrees clockwise'}[name],
            'runtime_rotation_degrees':0}

def verify(tallies):
    source=masters()
    checks={}
    for name in source:
        a=tallies[name+'_full'].getchannel('A');b=tallies[name+'_empty'].getchannel('A')
        checks[name+'_bounds_match']=a.getbbox()==b.getbbox()
        checks[name+'_alpha_matches_master']=bool(np.array_equal(np.asarray(a),np.asarray(source[name].getchannel('A'))))
        checks[name+'_genuine_transparency']=a.getextrema()==(0,255) and b.getextrema()==(0,255)
    for name,file,transform in [('attack','attack_rhombic_spear.png',Image.Transpose.FLIP_LEFT_RIGHT),('defense','defense_simple_shield_upright.png',Image.Transpose.ROTATE_270)]:
        original=Image.open(ROOT/'Sources'/file).convert('RGBA').transpose(transform).getchannel('A')
        checks[name+'_primary_tip_rotated_left']=bool(np.array_equal(np.asarray(source[name].getchannel('A')),np.asarray(original)))
    original_drop=Image.open(ROOT/'Sources/drop_before_rebalance.png').convert('RGBA')
    checks['drop_original_rgba_unchanged']=bool(np.array_equal(np.asarray(source['drop']),np.asarray(original_drop)))
    assert all(checks.values()),checks
    return checks
