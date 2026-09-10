"""Authorized alpha cleanup and crops only; source RGB is preserved."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFilter
import numpy as np,json
# Sources and unused crops live with the studies; the files the game mounts are
# written to the active style.
root=Path(__file__).resolve().parents[1]
active=next(p for p in root.parents if p.name=='Assets')/'Graphics/1_NeonMonte_Attivo/SceneKit_v2'

def clear_background(src,dst,seeds=[]):
    im=Image.open(src).convert('RGBA');a=np.array(im);rgb=a[:,:,:3]
    if a[:,:,3].min()==0 and (a[:,:,3]==0).mean()>.12:
        im.save(dst);return im
    delta=rgb.max(2).astype(int)-rgb.min(2)
    candidate=(delta<22)&(rgb.min(2)>60)
    mask=Image.fromarray(np.where(candidate,0,255).astype('uint8')).copy()
    for p in [(0,0),(im.width-1,0),(0,im.height-1),(im.width-1,im.height-1)]+seeds:
        ImageDraw.floodfill(mask,p,128,thresh=0)
    alpha=Image.fromarray(np.where(np.asarray(mask)==128,0,255).astype('uint8')).filter(ImageFilter.GaussianBlur(.35))
    out=Image.fromarray(np.dstack((rgb,np.asarray(alpha))))
    out.save(dst);return out

c=clear_background(root/'_Sources/v2_cabinet_ink_generated.png',active/'02_Machine/cabinet_illustrated.png',[(440,430),(884,430),(1350,430)])
clear_background(root/'_Sources/v2_button_ink_generated.png',root/'04_Controls/mushroom_idle.png')
clear_background(root/'_Sources/v2_lever_ink_generated.png',active/'04_Controls/lever_rest.png')
clear_background(root/'_Sources/v2_reel_generated.png',active/'02_Machine/reel_drum_blank.png')
clear_background(root/'_Sources/v2_cabinet_generated.png',root/'02_Machine/cabinet_empty.png',[(440,430),(884,430),(1350,430)])
# The center module is repeated; top and foot panels retain depth as separate caps.
w,h=c.size
boxes={'crown':(0,0,w,185),'plinth':(0,777,w,h),'left_cheek':(0,185,166,777),'right_cheek':(1605,185,w,777),'reel_bay':(640,185,1125,777)}
for name,box in boxes.items():c.crop(box).save(root/'02_Machine'/f'{name}.png')
files=[]
for folder in ['02_Machine','03_Table','04_Controls','05_Lamps','06_UI']:
    for p in (active/folder).glob('*.png'):
        im=Image.open(p);files.append({'file':str(p.relative_to(active)).replace('\\','/'),'size':list(im.size),'mode':im.mode,'alpha':im.getchannel('A').getextrema() if im.mode=='RGBA' else None})
(active/'07_Spec/asset_inventory.json').write_text(json.dumps(files,indent=2))
print(json.dumps(files,indent=2))
