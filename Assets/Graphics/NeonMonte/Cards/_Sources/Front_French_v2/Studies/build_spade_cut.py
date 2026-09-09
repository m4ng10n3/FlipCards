from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageChops
import math, json

ROOT=Path(__file__).resolve().parent
INK=(25,33,36); FACE=(61,75,77); PAPER=(238,227,201)
BLUE=(39,103,168); RED=(181,65,48)
raw=Image.new('L',(900,900))
ImageDraw.Draw(raw).text((40,0),'\u2660',font=ImageFont.truetype('C:/Windows/Fonts/seguisym.ttf',760),fill=255)
raw=raw.crop(raw.getbbox())
H=832; W=round(raw.width*H/raw.height)
base=raw.resize((W,H),Image.Resampling.LANCZOS).point(lambda v:255 if v>=128 else 0)
px=base.load(); edges={}
for y in range(H):
    for x in range(W):
        if not px[x,y]:continue
        if y==0 or not px[x,y-1]:edges[(x,y)]=(x+1,y)
        if x==W-1 or not px[x+1,y]:edges[(x+1,y)]=(x+1,y+1)
        if y==H-1 or not px[x,y+1]:edges[(x+1,y+1)]=(x,y+1)
        if x==0 or not px[x-1,y]:edges[(x,y+1)]=(x,y)
start=min(edges,key=lambda p:(p[1],abs(p[0]-W/2)))
contour=[start];p=edges[start]
while p!=start:contour.append(p);p=edges[p]
contour.append(start)

# Every anchor is an actual point on the source glyph perimeter. Sample both
# sides independently: no arbitrary off-contour control points or warped glyph.
def closest(tx,ty,side=None):
    pts=[p for p in contour if side is None or (p[0]>W/2 if side=='right' else p[0]<W/2)]
    return min(pts,key=lambda p:(p[0]-tx)**2+(p[1]-ty)**2)
A=start
P=closest(W,.56*H,'right');Pl=closest(0,.56*H,'left')
B=closest(W*.86,.81*H,'right');Bl=closest(W*.14,.81*H,'left')
C=closest(W*.60,H*.758,'right');Cl=closest(W*.40,H*.758,'left')
D=closest(W*.60,H,'right');Dl=closest(W*.40,H,'left')
anchors=[A,P,B,C,D,Dl,Cl,Bl,Pl]
def contour_arc(a,b):
    first,last=contour.index(a),contour.index(b)
    return contour[first:last+1] if first<=last else contour[first:-1]+contour[:last+1]
right_curve=contour_arc(B,C)
left_curve=contour_arc(Cl,Bl)
points=[A,P]+right_curve+[D,Dl]+left_curve+[Pl]
clip=Image.new('L',base.size);ImageDraw.Draw(clip).polygon(points,fill=255)
kept=ImageChops.multiply(base,clip)
removed=ImageChops.subtract(base,kept)
assert ImageChops.subtract(kept,base).getbbox() is None
assert all(p in edges for p in points)
icon=Image.new('RGBA',base.size,INK+(255,))
ImageDraw.Draw(icon).rectangle((W//2,0,W,H),fill=FACE+(255,))
icon.putalpha(kept)
box=kept.getbbox();cropped=icon.crop(box)
sprite=Image.new('RGBA',(256,256))
small=cropped.copy();small.thumbnail((192,192),Image.Resampling.LANCZOS)
sprite.alpha_composite(small,((256-small.width)//2,(256-small.height)//2))
sprite.save(ROOT/'attack_D_contour_cut.png')

S=2;canvas=Image.new('RGB',(1440*S,860*S),PAPER);draw=ImageDraw.Draw(canvas)
def txt(x,y,t,size=20,color=INK):
    draw.text((int(x*S),int(y*S)),t,font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',size*S),fill=color)
def line(pts,color=BLUE,width=2):
    draw.line([(round(x*S),round(y*S)) for x,y in pts],fill=color,width=width*S)
def dashed(pts,color=BLUE):
    distance=0
    for a,b in zip(pts,pts[1:]):
        if distance%16<9:line([a,b],color,2)
        distance+=math.dist(a,b)
def place(im,x,y,w,h):
    im=im.resize((round(w*S),round(h*S)),Image.Resampling.LANCZOS)
    canvas.paste(im,(round(x*S),round(y*S)),im)
txt(36,28,'D / RITAGLIO COSTRUITO SUL CONTORNO DELLA PICCA',29)
txt(36,76,'B-C segue la curva originale della picca su entrambi i lati. Gli altri tagli restano invariati.',18)
left,top=85,185;scale=.5
place(icon,left,top,W*scale,H*scale)
lost=Image.new('RGBA',base.size,(207,121,78,0));lost.putalpha(removed.point(lambda v:80 if v else 0))
place(lost,left,top,W*scale,H*scale)
def tr(p):return(left+p[0]*scale,top+p[1]*scale)
dashed([tr(p) for p in contour])
line([tr(p) for p in (Bl,Pl,A,P,B)],RED,2)
line([tr(p) for p in (Cl,Dl)],RED,2)
line([tr(p) for p in (C,D)],RED,2)
line([tr(p) for p in right_curve],(36,125,106),3)
line([tr(p) for p in left_curve],(36,125,106),3)
for label,p,dx,dy in [('A',A,8,-26),('P',P,13,-5),('P\u2032',Pl,-35,-5),('B',B,13,-5),('C',C,15,7),('B\u2032',Bl,-40,-5),('C\u2032',Cl,-47,7),('D',D,12,4),('D\u2032',Dl,-45,4)]:
    x,y=tr(p);draw.ellipse(((x-4)*S,(y-4)*S,(x+4)*S,(y+4)*S),fill=RED)
    txt(x+dx,y+dy,label,17,RED)
txt(65,653,'COSTRUZIONE / PARTI ELIMINATE',20)
txt(65,687,'Blu: picca  |  Rosso: tagli',17,BLUE)
txt(65,716,'Verde: curva B-C conservata',17,(36,125,106))

# Final and heart are shown in the same template scale: 104 and 88 px tall.
visible_w=(box[2]-box[0])*416/(box[3]-box[1])
place(cropped,770-visible_w/2,185,visible_w,416)
heart=Image.open(ROOT.parent/'Symbols/heart.png').convert('RGBA')
heart=heart.crop(heart.getchannel('A').getbbox())
place(heart,1165-176,217,352,352)
txt(656,653,'D / LAMA RISULTANTE',20)
txt(1083,653,'CUORE / 88 x 88',20)
txt(654,687,f'{visible_w/4:.1f} x 104 nel template',17)
txt(36,778,'Lama piena, due facce semplici. Nessuna parte aggiunta fuori dalla picca originale.',19)
canvas.resize((1440,860),Image.Resampling.LANCZOS).save(ROOT/'D_SPADE_CUT_CONSTRUCTION.png')
meta={'reference':'Segoe UI Symbol U+2660','source_size':[W,H],
      'anchors':dict(zip(['A','P','B','C','D',"D'","C'","B'","P'"],anchors)),
      'operation':'intersection of original glyph and cut mask retaining original B-C contour arcs on both sides',
      'retained_arcs':['B-C',"C'-B'"],
      'all_anchors_on_source_contour':True,'result_subset_of_original':True,
      'template_ink_size':[round(visible_w/4,2),104],
      'note':'New study D; current template B unchanged.'}
(ROOT/'D_SPADE_CUT_GEOMETRY.json').write_text(json.dumps(meta,indent=2),encoding='utf-8')
print(json.dumps(meta,indent=2))
