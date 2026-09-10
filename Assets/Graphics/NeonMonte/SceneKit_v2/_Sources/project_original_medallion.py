"""Project the ORIGINAL card overlay as one plane. Never redraw internal geometry."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import numpy as np
import json, hashlib

root=Path(__file__).resolve().parents[1]
source=root.parent/'Cards/_Final/Back/Template/back_artwork_overlay.png'
original=Image.open(source).convert('RGBA')
bbox=original.getchannel('A').getbbox()
art=original.crop(bbox)
art.save(root/'03_Table/medallion_original_crop.png')
# One global material tint; original alpha, pupils, eye positions and lines survive.
a=np.array(art)
light=(a[:,:,:3].mean(2)>90)
rgb=a[:,:,:3].astype(float)
rgb[light]*=np.array([1.04,.83,.43])
a[:,:,:3]=np.clip(rgb,0,255).astype('uint8')
gold=Image.fromarray(a)
w,h=art.size
# Single projective map of the whole source rectangle onto the receding table.
quad=[(300,425),(1372,425),(2060,900),(-388,900)]
uv=[(0,0),(w,0),(w,h),(0,h)]
mat=[]; rhs=[]
for (x,y),(u,v) in zip(quad,uv):
    mat.extend([[x,y,1,0,0,0,-u*x,-u*y],[0,0,0,x,y,1,-v*x,-v*y]])
    rhs.extend([u,v])
c=np.linalg.solve(np.array(mat,float),np.array(rhs,float))
size=(1672,941)
layer=gold.transform(size,Image.Transform.PERSPECTIVE,tuple(c),Image.Resampling.BICUBIC)
layer.save(root/'03_Table/medallion_projected_gold.png')
# Foreground occlusion is separate from the fixed decal and only hides pixels.
mask=Image.new('L',size,255); d=ImageDraw.Draw(mask)
polys=[
 [(266,0),(1415,0),(1415,618),(1379,641),(1310,646),(1303,626),(359,626),(355,646),(291,646),(266,619)],
 [(98,587),(252,587),(253,614),(193,772),(10,772),(7,718)],
 [(49,788),(247,772),(267,846),(49,868)],
 [(451,651),(681,651),(665,844),(391,844)],
 [(722,651),(947,651),(964,844),(704,845)],
 [(987,651),(1214,651),(1278,844),(1012,844)],
 [(1467,607),(1531,607),(1580,638),(1590,680),(1620,718),(1621,775),(1576,807),(1441,807),(1373,778),(1365,739),(1386,697),(1419,679),(1422,644)]
]
for p in polys:d.polygon(p,fill=0)
mask=mask.filter(ImageFilter.GaussianBlur(.7))
mask.save(root/'03_Table/medallion_object_occlusion_mask.png')
for name,base,out in [('empty','plain_empty.png','03_Table/table_original_medallion_empty.png'),('occupied','plain_occupied.png','01_Layout/layout_original_medallion.png')]:
    bg=Image.open(root/'_Sources'/base).convert('RGBA')
    decal=layer.copy()
    if name=='occupied':decal.putalpha(Image.fromarray((np.array(layer.getchannel('A'),float)*np.array(mask)/255).astype('uint8')))
    bg.alpha_composite(decal)
    bg.convert('RGB').save(root/out)
report={'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'crop':bbox,'crop_size':[w,h],'target_quad':quad,'inverse_homography':c.tolist(),'geometry':'One homography for ALL original pixels. No redrawn eyes, loops or diamond. Same decal in both scenes; independent foreground mask.','material':'Global ivory-to-gold tint; original alpha and dark ink preserved.'}
(root/'07_Spec/original_medallion_projection.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
