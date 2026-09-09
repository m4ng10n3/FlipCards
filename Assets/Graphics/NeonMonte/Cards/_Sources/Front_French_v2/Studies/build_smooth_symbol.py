from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import numpy as np
import re, json

ROOT=Path(__file__).resolve().parent
W,H=694,832
INK=(25,33,36);FACE=(61,75,77);PAPER=(238,227,201)
svg=(ROOT/'attack_G_extended_perimeter.svg').read_text(encoding='utf-8')
path=re.search(r'<path d="([^"]+)"',svg).group(1)
tokens=re.findall(r'[MLCZ]|[-+]?(?:\d*\.\d+|\d+)(?:[eE][-+]?\d+)?',path)
points=[];i=0
while i<len(tokens):
    op=tokens[i];i+=1
    if op in ('M','L'):
        p=np.array([float(tokens[i]),float(tokens[i+1])]);i+=2;points.append(p)
    elif op=='C':
        vals=list(map(float,tokens[i:i+6]));i+=6
        a=points[-1];b=np.array(vals[:2]);c=np.array(vals[2:4]);d=np.array(vals[4:])
        for t in np.linspace(0,1,100)[1:]:points.append((1-t)**3*a+3*(1-t)**2*t*b+3*(1-t)*t*t*c+t**3*d)
    elif op=='Z':points.append(points[0])
points=np.array(points)
dist=np.r_[0,np.cumsum(np.linalg.norm(np.diff(points,axis=0),axis=1))]
keep=np.r_[True,np.diff(dist)>1e-8];points=points[keep];dist=dist[keep]
samples=np.arange(0,dist[-1],1.)
outline=np.column_stack([np.interp(samples,dist,points[:,k]) for k in (0,1)])
# Smooth the actual approved contour uniformly by arc length. Retain the sharp
# spear tip; round every other transition without changing the global design.
sigma=16.;radius=48
kernel=np.exp(-.5*(np.arange(-radius,radius+1)/sigma)**2);kernel/=kernel.sum()
smooth=np.column_stack([np.convolve(np.pad(outline[:,k],radius,mode='wrap'),kernel,mode='valid') for k in (0,1)])
tip=points[0];tip_dist=np.linalg.norm(outline-tip,axis=1)
weight=np.clip((tip_dist-28)/42,0,1);weight=weight*weight*(3-2*weight)
smooth=outline+(smooth-outline)*weight[:,None]

# Cubic interpolation of the smoothed curve, instead of a chain of straight
# segments. At the apex the two handles remain independent to keep it pointed.
nodes=smooth[::8]
def xy(p):return f'{p[0]:.4f},{p[1]:.4f}'
cmd='M'+xy(nodes[0]);n=len(nodes)
for j in range(n):
    p0=nodes[(j-1)%n];p1=nodes[j];p2=nodes[(j+1)%n];p3=nodes[(j+2)%n]
    c1=p1+(p2-p0)/6;c2=p2-(p3-p1)/6
    if j==0:c1=p1+(p2-p1)/3
    if j==n-1:c2=p2+(p1-p2)/3
    cmd+=' C'+xy(c1)+' '+xy(c2)+' '+xy(p2)
cmd+=' Z'
(ROOT/'attack_H_smooth.svg').write_text(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}"><defs><clipPath id="shape"><path d="{cmd}"/></clipPath></defs><path d="{cmd}" fill="#192124"/><rect x="{W/2}" width="{W/2}" height="{H}" fill="#3d4b4d" clip-path="url(#shape)"/></svg>',encoding='utf-8')
mask=Image.new('L',(W*2,H*2));ImageDraw.Draw(mask).polygon([tuple(p*2) for p in smooth],fill=255)
mask=mask.resize((W,H),Image.Resampling.LANCZOS)
# Average subpixel raster differences across the mirrored halves.
a=np.asarray(mask,dtype=np.uint16);a=((a+a[:,::-1])//2).astype('uint8');mask=Image.fromarray(a)
icon=Image.new('RGBA',(W,H),INK+(255,));ImageDraw.Draw(icon).rectangle((W//2,0,W,H),fill=FACE+(255,));icon.putalpha(mask)
box=mask.getbbox();cropped=icon.crop(box)
thumb=cropped.copy();thumb.thumbnail((192,192),Image.Resampling.LANCZOS)
sprite=Image.new('RGBA',(256,256));sprite.alpha_composite(thumb,((256-thumb.width)//2,(256-thumb.height)//2))
sprite.save(ROOT/'attack_H_smooth.png')
icon.save(ROOT/'attack_H_smooth_master.png')

S=2;canvas=Image.new('RGB',(1320*S,760*S),PAPER);draw=ImageDraw.Draw(canvas)
def txt(x,y,s,size=20,color=INK):draw.text((x*S,y*S),s,font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',size*S),fill=color)
def place(im,cx,cy,w,h):
    im=im.resize((round(w*S),round(h*S)),Image.Resampling.LANCZOS)
    canvas.paste(im,(round((cx-w/2)*S),round((cy-h/2)*S)),im)
txt(34,26,'H / CONTORNO UNIFORME',29)
txt(34,74,'Raccordi continui sulla sagoma approvata, con punta e proporzioni conservate.',18)
old=Image.open(ROOT/'attack_G_extended_perimeter.png').convert('RGBA');old=old.crop(old.getchannel('A').getbbox())
heart=Image.open(ROOT.parent/'Symbols/heart.png').convert('RGBA');heart=heart.crop(heart.getchannel('A').getbbox())
ratio=104/(H-2);width=cropped.width*ratio
place(old,240,354,86.46*3.6,104*3.6)
place(cropped,680,354,width*3.6,104*3.6)
place(heart,1100,354,88*3.6,88*3.6)
txt(128,575,'G / PRECEDENTE',21)
txt(571,575,'H / RIFINITA',21)
txt(1056,575,'CUORE',21)
place(cropped,661,660,24*width/104,24);place(heart,700,660,20.3,20.3)
txt(34,712,'Curve morbide sotto la lama e lungo il gambo. PNG trasparente e SVG modificabile.',18)
canvas.resize((1320,760),Image.Resampling.LANCZOS).save(ROOT/'H_SMOOTH_SYMBOL_COMPARISON.png')
meta={'source':'attack_G_extended_perimeter.svg','method':'arc-length Gaussian contour smoothing, continuous cubic SVG interpolation, sharp tip preserved',
      'smoothing_sigma_source_px':sigma,'maximum_contour_displacement_source_px':float(np.linalg.norm(smooth-outline,axis=1).max()),
      'geometry':'two flat contiguous faces, transparent background, mirror-symmetric raster alpha',
      'template_ink_size':[width,104],'note':'New study H; current template unchanged.'}
(ROOT/'H_SMOOTH_SYMBOL_NOTES.json').write_text(json.dumps(meta,indent=2),encoding='utf-8')
print(json.dumps(meta,indent=2))
