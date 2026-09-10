from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageChops
import numpy as np
import math, json

ROOT=Path(__file__).resolve().parent
INK=(25,33,36);FACE=(61,75,77);PAPER=(238,227,201)
BLUE=(39,103,168);RED=(181,65,48);GREEN=(36,125,106);PURPLE=(125,76,146)
raw=Image.new('L',(900,900))
ImageDraw.Draw(raw).text((40,0),'\u2660',font=ImageFont.truetype('C:/Windows/Fonts/seguisym.ttf',760),fill=255)
raw=raw.crop(raw.getbbox());H=832;W=round(raw.width*H/raw.height)
base=raw.resize((W,H),Image.Resampling.LANCZOS).point(lambda v:255 if v>=128 else 0)
pixels=np.asarray(base);px=base.load();edges={}
for y in range(H):
    for x in range(W):
        if not px[x,y]:continue
        if y==0 or not px[x,y-1]:edges[(x,y)]=(x+1,y)
        if x==W-1 or not px[x+1,y]:edges[(x+1,y)]=(x+1,y+1)
        if y==H-1 or not px[x,y+1]:edges[(x+1,y+1)]=(x,y+1)
        if x==0 or not px[x-1,y]:edges[(x,y+1)]=(x,y)
A=min(edges,key=lambda p:(p[1],abs(p[0]-W/2)));contour=[A];p=edges[A]
while p!=A:contour.append(p);p=edges[p]
contour.append(A)
C0=(418,630);B0=(586,645)
# D is the actual outer bottom-right corner of the original spade foot.
bottom_y=max(p[1] for p in contour)
D=np.array(max((p for p in contour if p[1]==bottom_y),key=lambda p:p[0]),float)
outer=min((p for p in contour if p[0]>W/2),key=lambda p:abs(p[0]-693)+abs(p[1]-466))
lower=contour[contour.index(outer):contour.index(C0)+1]
boundary={x:max(y for xx,y in lower if xx==x) for x in set(p[0] for p in lower)}
base_width=2*(D[0]-W/2)
t=np.linspace(0,1,161)[:,None]
def cubic(C,U,V,D):return (1-t)**3*C+3*(1-t)**2*t*U+3*(1-t)*t*t*V+t**3*D
best=None;feasible=0
for x in range(458,632,4):
    if x not in boundary:continue
    C=np.array((float(x),float(boundary[x])))
    Bs=[(bx,boundary[bx]) for bx in range(600,676,2) if bx in boundary and 45<=bx-x<=170]
    if not Bs:continue
    B=min(Bs,key=lambda p:abs(2*(p[0]-W/2)/(H-A[1])-.75))
    for reach in np.linspace(.90,3.70,15):
        for rise in (24,36,48,60,72,84,96,108):
            for indent in (64,80,96,112,128,144,160,176,192,208):
                U=np.array((C[0]-reach*(C[0]-D[0]),C[1]-rise))
                V=np.array((D[0]-indent,D[1]-.34*(D[1]-C[1])))
                arc=cubic(C,U,V,D)
                neck_width=2*(float(arc[:,0].min())-W/2)
                blade_width=2*(B[0]-W/2)
                if not .12*blade_width<=neck_width<=.42*blade_width:continue
                samples=arc[2:-2]
                xs=np.clip((samples[:,0]-1).astype(int),0,W-1);ys=np.clip(samples[:,1].astype(int),0,H-1)
                if np.count_nonzero(pixels[ys,xs]==0)>1:continue
                width_ratio=2*(B[0]-W/2)/(H-A[1])
                # Declared design targets: reference C aspect, a readable waist,
                # a shallow upper return and a calm transition to the foot.
                score=((width_ratio-.75)/.04)**2+((neck_width/blade_width-.25)/.06)**2
                score+=.12*((rise/(D[1]-C[1])-.22)/.12)**2+.05*((reach-1.25)/.35)**2
                feasible+=1
                if best is None or score<best['score']:
                    best=dict(B=B,C=C.tolist(),U=U.tolist(),V=V.tolist(),D=D.tolist(),neck_width=neck_width,score=float(score),arc=arc.tolist())
assert best,'No valid variable-curvature cut'
B=tuple(best['B']);C=tuple(map(int,best['C']));U=best['U'];V=best['V'];arc=best['arc']
right_curve=contour[contour.index(B):contour.index(C)+1]
mirror=lambda p:(W-p[0],p[1])
outline=[A]+right_curve+arc[1:]+[mirror(D)]+[mirror(p) for p in reversed(arc[:-1])]+[mirror(p) for p in reversed(right_curve[:-1])]
clip=Image.new('L',base.size);ImageDraw.Draw(clip).polygon(outline,fill=255)
kept=ImageChops.multiply(base,clip);kept=ImageChops.darker(kept,kept.transpose(Image.Transpose.FLIP_LEFT_RIGHT))
assert ImageChops.subtract(kept,base).getbbox() is None
assert ImageChops.difference(kept,kept.transpose(Image.Transpose.FLIP_LEFT_RIGHT)).getbbox() is None
removed=ImageChops.subtract(base,kept)
icon=Image.new('RGBA',base.size,INK+(255,));ImageDraw.Draw(icon).rectangle((W//2,0,W,H),fill=FACE+(255,));icon.putalpha(kept)
box=kept.getbbox();cropped=icon.crop(box)
sprite=Image.new('RGBA',(256,256));thumb=cropped.copy();thumb.thumbnail((192,192),Image.Resampling.LANCZOS)
sprite.alpha_composite(thumb,((256-thumb.width)//2,(256-thumb.height)//2));sprite.save(ROOT/'attack_F_variable_curve.png')
def coord(p):return f'{p[0]:.4f},{p[1]:.4f}'
cmd='M'+coord(A)+' '+' '.join('L'+coord(p) for p in right_curve)
cmd+=' C'+coord(U)+' '+coord(V)+' '+coord(D)+' L'+coord(mirror(D))
cmd+=' C'+coord(mirror(V))+' '+coord(mirror(U))+' '+coord(mirror(C))
cmd+=' '+' '.join('L'+coord(mirror(p)) for p in reversed(right_curve[:-1]))+' Z'
(ROOT/'attack_F_variable_curve.svg').write_text(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}"><defs><clipPath id="shape"><path d="{cmd}"/></clipPath></defs><path fill="#192124" d="{cmd}"/><rect x="{W/2}" width="{W/2}" height="{H}" fill="#3d4b4d" clip-path="url(#shape)"/></svg>',encoding='utf-8')

S=2;canvas=Image.new('RGB',(1440*S,920*S),PAPER);draw=ImageDraw.Draw(canvas)
def txt(x,y,s,size=20,color=INK):draw.text((int(x*S),int(y*S)),s,font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',size*S),fill=color)
def line(points,color=BLUE,width=2):draw.line([(round(x*S),round(y*S)) for x,y in points],fill=color,width=width*S)
def dashed(points,color=BLUE,width=2):
    distance=0
    for a,b in zip(points,points[1:]):
        if distance%16<9:line([a,b],color,width)
        distance+=math.dist(a,b)
def place(im,x,y,w,h):
    im=im.resize((round(w*S),round(h*S)),Image.Resampling.LANCZOS);canvas.paste(im,(round(x*S),round(y*S)),im)
txt(36,28,'F / GAMBO A CURVATURA VARIABILE',29)
txt(36,76,'D coincide con l\'angolo esterno della picca. A-B: retta. B-C: sagoma. C-D: curva morbida.',18)
left,top=85,170;scale=.5
def tr(p):return(left+p[0]*scale,top+p[1]*scale)
place(icon,left,top,W*scale,H*scale)
lost=Image.new('RGBA',base.size,(207,121,78,0));lost.putalpha(removed.point(lambda v:80 if v else 0));place(lost,left,top,W*scale,H*scale)
dashed([tr(p) for p in contour]);line([tr(mirror(B)),tr(A),tr(B)],RED)
for f in (lambda p:p,mirror):
    line([tr(f(p)) for p in right_curve],GREEN,3)
    line([tr(f(p)) for p in arc],PURPLE,3)
    line([tr(f(C)),tr(f(U))],(173,151,178),1);line([tr(f(D)),tr(f(V))],(173,151,178),1)
    for p in (U,V):
        x,y=tr(f(p));draw.rectangle(((x-3)*S,(y-3)*S,(x+3)*S,(y+3)*S),outline=PURPLE,width=S)
for label,p,dx,dy in [('A',A,8,-27),('B',B,14,-22),('C',C,16,0),('D',D,10,12),('B\u2032',mirror(B),-42,-22),('C\u2032',mirror(C),-48,0),('D\u2032',mirror(D),-43,12)]:
    x,y=tr(p);draw.ellipse(((x-4)*S,(y-4)*S,(x+4)*S,(y+4)*S),fill=RED);txt(x+dx,y+dy,label,17,RED)
txt(65,638,'COSTRUZIONE / RITAGLI',21);txt(65,673,'Blu: picca  |  Rosso: rette',17,BLUE)
txt(65,703,'Verde: B-C  |  Viola: curva C-D',17,GREEN)
ratio=104/(H-A[1]);visible_w=(box[2]-box[0])*ratio
place(cropped,770-visible_w*2,170,visible_w*4,416)
heart=Image.open(ROOT.parent/'Symbols/heart.png').convert('RGBA');heart=heart.crop(heart.getchannel('A').getbbox());place(heart,989,202,352,352)
txt(646,638,'F / LAMA RISULTANTE',21);txt(1083,638,'CUORE / 88 x 88',21)
txt(645,674,f'{visible_w:.1f} x 104 nel template',17)
txt(645,704,f'Collo minimo: {best["neck_width"]*ratio:.1f} px',17)
place(cropped,749,762,42,42/visible_w*104);place(heart,816,770,46,46)
txt(36,851,'B e C ricalcolati sul contorno; raccordo Bezier speculare. Le maniglie regolano la curvatura.',18)
canvas.resize((1440,920),Image.Resampling.LANCZOS).save(ROOT/'F_VARIABLE_CURVE_CONSTRUCTION.png')
meta={k:v for k,v in best.items() if k!='arc'}
meta.update({'source_size':[W,H],'feasible_candidates':feasible,'template_scale':ratio,'template_ink_size':[visible_w,104],
             'neck_px_at_24_height':best['neck_width']*24/(H-A[1]),'curve_type':'cubic Bezier, not circular',
             'D_anchor':'outermost bottom-right corner of the original spade contour; left point mirrored',
             'objective':'((width_ratio-.75)/.04)^2+((neck/blade_width-.25)/.06)^2+.12*((rise/span-.22)/.12)^2+.05*((reach-1.25)/.35)^2',
             'qualification':'Best sampled feasible candidate under declared aesthetic targets, not a universal optimum.',
             'verification':'PNG within source silhouette and exactly mirror symmetric; SVG cubic analytic with raster contour sampling for B-C.'})
(ROOT/'F_VARIABLE_CURVE_GEOMETRY.json').write_text(json.dumps(meta,indent=2),encoding='utf-8')
print(json.dumps(meta,indent=2))
