"""User-authorized alpha cleanup and matched cropping, no RGB repainting.
Run with bundled Python. Sources are immutable generated outputs.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageFilter, ImageDraw

def close_mask(mask,n=5):
    return mask.filter(ImageFilter.MaxFilter(n*2+1)).filter(ImageFilter.MinFilter(n*2+1))

def fill_holes(mask):
    background=mask.copy()
    ImageDraw.floodfill(background,(0,0),128,thresh=0)
    data=np.asarray(background)
    return Image.fromarray(np.where(data==128,0,255).astype(np.uint8))

ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'Sources'

def cutout(path):
    im=Image.open(path).convert('RGBA')
    rgb=np.asarray(im)[:,:,:3].copy()
    alpha=np.asarray(im)[:,:,3]
    if alpha.min()==0 and (alpha==0).mean()>.1:
        return im
    # Background is achromatic checkerboard. Colored lamp/metal seeds are foreground.
    delta=rgb.max(2).astype(float)-rgb.min(2)
    seed=delta>30
    main=fill_holes(close_mask(Image.fromarray((seed*255).astype(np.uint8))))
    # Keep a narrow near-black outline adjacent to the colored silhouette.
    near=np.asarray(main.filter(ImageFilter.MaxFilter(11)))>0
    outline=near & (rgb.max(2)<80)
    fg=fill_holes(close_mask(Image.fromarray((((np.asarray(main)>0)|outline)*255).astype(np.uint8)),2))
    # Feather only the contour at subpixel width. RGB stays byte-identical.
    a=np.asarray(fg.filter(ImageFilter.GaussianBlur(.45)))
    return Image.fromarray(np.dstack((rgb,a)))

report=[]
for key,name in [('lamp_round','hp_round'),('lamp_taper','atk_taper'),('lamp_tube','def_discharge')]:
    pair=[cutout(SRC/(key+'_generated.png')),cutout(SRC/(key+'_on_generated.png'))]
    boxes=[im.getchannel('A').point(lambda a:255 if a>128 else 0).getbbox() for im in pair]
    box=(min(b[0] for b in boxes)-12,min(b[1] for b in boxes)-12,max(b[2] for b in boxes)+12,max(b[3] for b in boxes)+12)
    for state,im in zip(['off','on'],pair):
        out=ROOT/'Lamps'/f'{name}_{state}.png'
        im.crop(box).save(out)
        report.append({'file':str(out.relative_to(ROOT)),'sourceCrop':box,'size':im.crop(box).size,'alphaOnly':True})

cutout(SRC/'button_pressed_generated.png').save(ROOT/'Controls/attack_mushroom_pressed.png')
(ROOT/'QA/alpha_cleanup.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
