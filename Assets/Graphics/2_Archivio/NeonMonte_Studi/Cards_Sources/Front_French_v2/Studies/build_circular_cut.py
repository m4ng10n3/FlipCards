from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageChops
import math, json
import numpy as np

ROOT=Path(__file__).resolve().parent
INK=(25,33,36); FACE=(61,75,77); PAPER=(238,227,201)
BLUE=(39,103,168); RED=(181,65,48); GREEN=(36,125,106)
raw=Image.new('L',(900,900))
ImageDraw.Draw(raw).text((40,0),'\u2660',font=ImageFont.truetype('C:/Windows/Fonts/seguisym.ttf',760),fill=255)
raw=raw.crop(raw.getbbox())
H=832; W=round(raw.width*H/raw.height)
base=raw.resize((W,H),Image.Resampling.LANCZOS).point(lambda v:255 if v>=128 else 0)
px=base.load();edges={}
for y in range(H):
    for x in range(W):
        if not px[x,y]:continue
        if y==0 or not px[x,y-1]:edges[(x,y)]=(x+1,y)
        if x==W-1 or not px[x+1,y]:edges[(x+1,y)]=(x+1,y+1)
        if y==H-1 or not px[x,y+1]:edges[(x+1,y+1)]=(x,y+1)
        if x==0 or not px[x-1,y]:edges[(x,y+1)]=(x,y)
A=min(edges,key=lambda p:(p[1],abs(p[0]-W/2)))
contour=[A];p=edges[A]
while p!=A:contour.append(p);p=edges[p]
contour.append(A)
B0=(586,645); D=(416,832); C0=(418,630)
assert B0 in edges and D in edges and C0 in edges
outer=min((p for p in contour if p[0]>W/2),key=lambda p:abs(p[0]-693)+abs(p[1]-466))
bc=contour[contour.index(outer):contour.index(C0)+1]
boundary={x:max(y for xx,y in bc if xx==x) for x in set(p[0] for p in bc)}
base_width=2*(D[0]-W/2)
target_neck_width=.72*base_width
candidates=[]
diagnostics={'sweep':0,'vertical':0,'neck':0,'inside':0,'nearest_outside':999}
for x in sorted(boundary)[::2]:
    if not C0[0]+16<=x<=640:continue
    C=np.array((x,boundary[x]),float)
    nearby=np.array([(xx,yy) for xx,yy in boundary.items() if abs(xx-x)<=14])
    slope=float(np.polyfit(nearby[:,0]-x,nearby[:,1],2)[1])
    delta=np.array(D)-C
    midpoint=(C+np.array(D))/2
    for offset in np.linspace(-.25,.45,141):
        O=midpoint+offset*np.array((delta[1],-delta[0]))
        radius=float(np.linalg.norm(C-O))
        a0=math.atan2(C[1]-O[1],C[0]-O[0]);a1=math.atan2(D[1]-O[1],D[0]-O[0])
        while a1>a0:a1-=2*math.pi
        if a0-a1>math.pi*1.1:continue
        diagnostics['sweep']+=1
        angles=np.linspace(a0,a1,161)
        arc=np.column_stack((O[0]+radius*np.cos(angles),O[1]+radius*np.sin(angles)))
        if arc[:,1].min()<C[1]-96 or arc[:,1].max()>H+.01:continue
        diagnostics['vertical']+=1
        min_x=float(arc[:,0].min());neck_width=2*(min_x-W/2)
        if not .25*base_width<=neck_width<=.90*base_width:continue
        diagnostics['neck']+=1
        outside=0
        for ax,ay in arc[2:-2]:
            ix=min(W-1,max(0,int(ax-1)));iy=min(H-1,max(0,int(ay)))
            if not px[ix,iy]:outside+=1
        if outside<diagnostics['nearest_outside']:
            diagnostics['nearest_outside']=outside
            diagnostics['nearest']={'C':C.tolist(),'O':O.tolist(),'radius':radius,'neck':neck_width,'outside_points':[(round(ax,1),round(ay,1)) for ax,ay in arc[2:-2] if not px[min(W-1,max(0,int(ax-1))),min(H-1,max(0,int(ay)))]]}
        if outside>3:continue
        diagnostics['inside']+=1
        incoming=np.array((-1.,-slope));incoming/=np.linalg.norm(incoming)
        outgoing=np.array((math.sin(a0),-math.cos(a0)))
        angle_error=math.degrees(math.acos(float(np.clip(np.dot(incoming,outgoing),-1,1))))
        for bx in range(600,675,2):
            if bx not in boundary or bx-x<45 or bx-x>170:continue
            B=(bx,boundary[bx]);width_ratio=2*(bx-W/2)/(H-A[1])
            score=((width_ratio-.75)/.04)**2+((neck_width/base_width-.72)/.10)**2+.15*(angle_error/35)**2
            candidates.append(dict(B=list(B),C=C.tolist(),center=O.tolist(),radius=radius,
                                  neck_width=neck_width,score=score,tangent_change_degrees=angle_error,
                                  width_ratio=width_ratio,arc=arc.tolist(),outside_samples=outside))
assert candidates,repr(diagnostics)
best=min(candidates,key=lambda c:c['score'])
print(json.dumps({'feasible':len(candidates),'best':{k:v for k,v in best.items() if k!='arc'}},indent=2))

B=tuple(best['B']);C=tuple(map(int,best['C']));O=best['center'];R=best['radius']
right_curve=contour[contour.index(B):contour.index(C)+1]
arc=best['arc']; mirror=lambda p:(W-p[0],p[1])
outline=[A]+right_curve+arc[1:]+[mirror(D)]+[mirror(p) for p in reversed(arc[:-1])]+[mirror(p) for p in reversed(right_curve[:-1])]
clip=Image.new('L',base.size);ImageDraw.Draw(clip).polygon(outline,fill=255)
kept=ImageChops.multiply(base,clip)
kept=ImageChops.darker(kept,kept.transpose(Image.Transpose.FLIP_LEFT_RIGHT))
assert ImageChops.subtract(kept,base).getbbox() is None
assert ImageChops.difference(kept,kept.transpose(Image.Transpose.FLIP_LEFT_RIGHT)).getbbox() is None
removed=ImageChops.subtract(base,kept)
icon=Image.new('RGBA',base.size,INK+(255,))
ImageDraw.Draw(icon).rectangle((W//2,0,W,H),fill=FACE+(255,))
icon.putalpha(kept)
box=kept.getbbox(); cropped=icon.crop(box)
sprite=Image.new('RGBA',(256,256));thumb=cropped.copy()
thumb.thumbnail((192,192),Image.Resampling.LANCZOS)
sprite.alpha_composite(thumb,((256-thumb.width)//2,(256-thumb.height)//2))
sprite.save(ROOT/'attack_E_circular_cut.png')

# Editable source uses true SVG circle arcs, not a fitted Bezier approximation.
def coord(p):return f'{p[0]:.4f},{p[1]:.4f}'
cmd='M'+coord(A)+' '+' '.join('L'+coord(p) for p in right_curve)
cmd+=f' A{R:.6f},{R:.6f} 0 0 0 '+coord(D)+' L'+coord(mirror(D))
cmd+=f' A{R:.6f},{R:.6f} 0 0 0 '+coord(mirror(C))
cmd+=' '+' '.join('L'+coord(mirror(p)) for p in reversed(right_curve[:-1]))+' Z'
(ROOT/'attack_E_circular_cut.svg').write_text(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}"><defs><clipPath id="shape"><path d="{cmd}"/></clipPath></defs><path fill="#192124" d="{cmd}"/><rect x="{W/2}" width="{W/2}" height="{H}" fill="#3d4b4d" clip-path="url(#shape)"/></svg>',encoding='utf-8')

S=2;canvas=Image.new('RGB',(1440*S,920*S),PAPER);draw=ImageDraw.Draw(canvas)
def txt(x,y,t,size=20,color=INK):
    draw.text((int(x*S),int(y*S)),t,font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',size*S),fill=color)
def line(pts,color=BLUE,width=2):
    draw.line([(round(x*S),round(y*S)) for x,y in pts],fill=color,width=width*S)
def dashed(pts,color=BLUE,width=2):
    distance=0
    for a,b in zip(pts,pts[1:]):
        if distance%16<9:line([a,b],color,width)
        distance+=math.dist(a,b)
def place(im,x,y,w,h):
    im=im.resize((round(w*S),round(h*S)),Image.Resampling.LANCZOS)
    canvas.paste(im,(round(x*S),round(y*S)),im)
txt(36,28,'E / TAGLIO CIRCOLARE DEL GAMBO',29)
txt(36,76,'A-B: retta alla punta. B-C: contorno della picca. C-D: arco di cerchio. Costruzione speculare.',18)
left,top=85,170;scale=.5
def tr(p):return(left+p[0]*scale,top+p[1]*scale)
place(icon,left,top,W*scale,H*scale)
lost=Image.new('RGBA',base.size,(207,121,78,0));lost.putalpha(removed.point(lambda v:80 if v else 0))
place(lost,left,top,W*scale,H*scale)
dashed([tr(p) for p in contour])
line([tr(mirror(B)),tr(A),tr(B)],RED)
line([tr(p) for p in right_curve],GREEN,3)
line([tr(mirror(p)) for p in right_curve],GREEN,3)
PURPLE=(125,76,146)
for reflect in (False,True):
    f=mirror if reflect else lambda p:p
    ring=[f((O[0]+R*math.cos(t),O[1]+R*math.sin(t))) for t in np.linspace(0,2*math.pi,241)]
    dashed([tr(p) for p in ring],(171,148,178),1)
    line([tr(f(p)) for p in arc],PURPLE,3)
    line([tr(f(C)),tr(f(O)),tr(f(D))],(171,148,178),1)
    x,y=tr(f(O));line([(x-4,y),(x+4,y)],PURPLE,1);line([(x,y-4),(x,y+4)],PURPLE,1)
for label,p,dx,dy in [('A',A,8,-27),('B',B,14,-22),('C',C,16,0),('D',D,10,12),('B\u2032',mirror(B),-42,-22),('C\u2032',mirror(C),-48,0),('D\u2032',mirror(D),-43,12)]:
    x,y=tr(p);draw.ellipse(((x-4)*S,(y-4)*S,(x+4)*S,(y+4)*S),fill=RED)
    txt(x+dx,y+dy,label,17,RED)
txt(65,638,'COSTRUZIONE / RITAGLI',21)
txt(65,673,'Blu: sagoma  |  Rosso: rette',17,BLUE)
txt(65,703,'Verde: B-C  |  Viola: cerchio',17,GREEN)
ratio=104/(H-A[1]);visible_w=(box[2]-box[0])*ratio
place(cropped,770-visible_w*2,170,visible_w*4,416)
heart=Image.open(ROOT.parent/'Symbols/heart.png').convert('RGBA');heart=heart.crop(heart.getchannel('A').getbbox())
place(heart,1165-176,202,352,352)
txt(646,638,'E / LAMA RISULTANTE',21)
txt(1083,638,'CUORE / 88 x 88',21)
txt(645,674,f'{visible_w:.1f} x 104 nel template',17)
txt(645,704,f'Raggio {R*ratio:.1f} px | collo {best["neck_width"]*ratio:.1f} px',17)
place(cropped,749,762,42,42/visible_w*104)
place(heart,816,770,46,46)
txt(36,851,'B e C ottimizzati insieme: proporzioni della variante C, collo leggibile, curve circolari esatte.',18)
canvas.resize((1440,920),Image.Resampling.LANCZOS).save(ROOT/'E_CIRCULAR_CUT_CONSTRUCTION.png')
meta={k:v for k,v in best.items() if k!='arc'}
meta.update({'A':A,'D':D,'old_B':B0,'old_C':C0,'source_size':[W,H],
             'feasible_candidates':len(candidates),'target_width_height_ratio':.75,
             'target_neck_to_foot_ratio':.72,'template_scale':ratio,
             'neck_width_at_24px_height':best['neck_width']*24/(H-A[1]),
             'objective':'((width/height-.75)/.04)^2 + ((neck/foot-.72)/.10)^2 + .15*(tangent_change_degrees/35)^2',
             'constraints':'D fixed; C moves outward; B on same contour; 45..170 source px between B and C in x; neck >=25% foot; circular cut; at most 3 of 157 samples outside raster boundary, subsequently clipped; mirror symmetric final alpha',
             'scope':'Best sampled feasible candidate under declared aesthetic criteria; not a universal aesthetic optimum.',
             'sampling':'C every 2 source pixels, B every 2 pixels; circle center offset step .005 of perpendicular chord vector',
             'template_ink_size':[visible_w,104]})
(ROOT/'E_CIRCULAR_CUT_OPTIMIZATION.json').write_text(json.dumps(meta,indent=2),encoding='utf-8')
(ROOT/'E_CIRCULAR_CUT_NOTES.md').write_text(
    '# Costruzione E\n\n'
    'A-B e una retta alla punta centrale; B-C conserva il bordo della picca; C-D e un arco di cerchio. '
    'D resta nella posizione della costruzione precedente. B e C sono cercati sul contorno destro, poi specchiati.\n\n'
    f'Scelta: B={B}, C={C}, D={D}; centro cerchio ({O[0]:.2f}, {O[1]:.2f}), raggio {R:.2f}, '
    f'su sorgente {W} x {H}. B precedente {B0}; C precedente {C0}.\n\n'
    f'Migliore fra {len(candidates)} configurazioni ammissibili campionate: rapporto larghezza/altezza vicino a 0.75 '
    '(variante C), collo il piu vicino possibile al 72% del piede, penalita minore sui cambi di tangente. '
    'Questi sono criteri dichiarati di bilanciamento, non una misura oggettiva della bellezza. '
    'Il raccordo in C forma una cuspide: non viene dichiarato tangente.\n\n'
    f'A 104 px di altezza: raggio {R*ratio:.2f} px, collo minimo {best["neck_width"]*ratio:.2f} px; '
    f'a 24 px il collo e circa {best["neck_width"]*24/(H-A[1]):.2f} px. '
    'Il PNG e intersecato con la sagoma raster e simmetrizzato; lo SVG conserva gli archi analitici, '
    'con possibili differenze subpixel sui raccordi rispetto al PNG.\n\n'
    'Asset di studio: il template corrente non viene sostituito.\n',encoding='utf-8')
