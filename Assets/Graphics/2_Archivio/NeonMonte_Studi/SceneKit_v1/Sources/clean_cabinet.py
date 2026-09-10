from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import numpy as np
root=Path(__file__).resolve().parents[1]
im=Image.open(root/'Sources/cabinet_integrated_generated.png').convert('RGBA')
a=np.asarray(im);rgb=a[:,:,:3]
candidate=(rgb.min(2)>232)&((rgb.max(2).astype(int)-rgb.min(2))<22)
mask=Image.fromarray(np.where(candidate,0,255).astype('uint8')).copy()
for p in [(0,0),(460,300),(1050,300),(1700,300)]:
    ImageDraw.floodfill(mask,p,128,thresh=0)
alpha=Image.fromarray(np.where(np.asarray(mask)==128,0,255).astype('uint8')).filter(ImageFilter.GaussianBlur(.4))
out=Image.fromarray(np.dstack((rgb,np.asarray(alpha))))
out.save(root/'Machine/cabinet_integrated.png')
print('cabinet',out.size,out.getchannel('A').getextrema())
