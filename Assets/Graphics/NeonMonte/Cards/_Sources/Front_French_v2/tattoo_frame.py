"""Register generated tattoo ink details to the exact production frame paths."""
from pathlib import Path
import numpy as np
from PIL import Image,ImageFilter,ImageChops
ROOT=Path(__file__).resolve().parent
INK=(17,24,27)

def rgba(alpha,color):
    out=Image.new('RGBA',alpha.size,color+(0,));out.putalpha(alpha);return out

def shifted(mask,dx,dy):
    out=Image.new('L',mask.size);out.paste(mask,(dx,dy));return out

def render(guide):
    # Generated RGB includes checker pixels: retain only charcoal and warm inks.
    src=Image.open(ROOT/'Sources/frame_tattoo_generated.png').convert('RGB').resize(guide.size,Image.Resampling.LANCZOS)
    a=np.asarray(src).astype(float)
    dark=np.clip((112-a.max(2))/65,0,1)
    warm=np.clip((a[:,:,0]-a[:,:,2]-14)/24,0,1)*(a.min(2)>110)
    generated_alpha=Image.fromarray((np.maximum(dark,warm)*255).astype('uint8'))
    extracted=src.convert('RGBA');extracted.putalpha(generated_alpha)
    extracted.save(ROOT/'Template/frame_tattoo_extracted.png')
    # The original paths own every coordinate; generated stippling adds craft.
    axis=guide.getchannel('A').point(lambda v:255 if v>210 else 0)
    body=axis.filter(ImageFilter.MaxFilter(9))
    recess=shifted(body,2,3).filter(ImageFilter.GaussianBlur(1.4)).point(lambda v:round(v*.28))
    raised=shifted(body,-1,-2).point(lambda v:round(v*.46))
    out=rgba(recess,(80,62,41))
    out=Image.alpha_composite(out,rgba(raised,(255,247,218)))
    out=Image.alpha_composite(out,rgba(body,INK))
    ridge=axis.filter(ImageFilter.MaxFilter(3))
    out=Image.alpha_composite(out,rgba(ridge,(207,187,149)))
    gleam=ImageChops.subtract(ridge,shifted(ridge,1,2)).point(lambda v:round(v*.75))
    out=Image.alpha_composite(out,rgba(gleam,(245,232,198)))
    # Keep only the artist's fine details beside (rather than replacing) the rails.
    corridor=axis.filter(ImageFilter.MaxFilter(45))
    outside=ImageChops.subtract(corridor,axis.filter(ImageFilter.MaxFilter(19)))
    detail=ImageChops.multiply(Image.fromarray((dark*215).astype('uint8')),outside)
    # Reserve the narrow outer columns for the tally silhouettes.
    da=np.asarray(detail).copy()
    da[110:-110,:65]=0;da[110:-110,-65:]=0
    # The generated horizontal shadow strokes wandered away from the registered
    # ridge, creating a second wavy line above the charges and below the top rail.
    # Keep those runs entirely procedural; feather into the inkwork at the bends.
    x=np.arange(guide.width,dtype=float)
    def straight_window(start,full_start,full_end,end):
        return np.minimum(np.clip((x-start)/(full_start-start),0,1),np.clip((end-x)/(end-full_end),0,1))
    outer=straight_window(68,105,919,956)
    inner=straight_window(280,319,776,815)
    da[:55]=(da[:55]*(1-outer)[None,:]).round().astype('uint8')
    da[55:112]=(da[55:112]*(1-inner)[None,:]).round().astype('uint8')
    assert not da[55:112,319:777].any(), 'No generated ghosts on the inner horizontal rule'
    detail=Image.fromarray(da)
    out=Image.alpha_composite(out,rgba(detail,INK))
    # Preserve exact opposite-pair layout and open endpoint Y values.
    ar=np.asarray(out).copy();h=ar.shape[0]
    for x,y,upper in [(250,180,True),(774,1356,False)]:
        if upper:ar[y+1:y+24,x-22:x+23,3]=0
        else:ar[y-23:y,x-22:x+23,3]=0
    ar[h//2:]=ar[:h//2][::-1,::-1]
    out=Image.fromarray(ar)
    out.save(ROOT/'Template/front_frame_tattoo.png')
    details=np.asarray(rgba(detail,INK)).copy()
    details[h//2:]=details[:h//2][::-1,::-1]
    Image.fromarray(details).save(ROOT/'Template/front_frame_ink_details.png')
    return out
