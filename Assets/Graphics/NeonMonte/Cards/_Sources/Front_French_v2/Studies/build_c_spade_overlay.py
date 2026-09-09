from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import math

ROOT = Path(__file__).resolve().parent
S = 2
PAPER = (238, 227, 201)
INK = (25, 33, 36)
BLUE = (39, 103, 168)
GRAY = (143, 139, 125)
canvas = Image.new('RGB', (1200*S, 800*S), PAPER)
d = ImageDraw.Draw(canvas)

def font(size):
    return ImageFont.truetype('C:/Windows/Fonts/consola.ttf', size*S)

def text(x,y,value,size=20,color=INK):
    d.text((x*S,y*S),value,font=font(size),fill=color)

def line(points, color=GRAY, width=1):
    d.line([(round(x*S),round(y*S)) for x,y in points],fill=color,width=width*S)

def stamp(path,cx,cy,w,h):
    im=Image.open(path).convert('RGBA')
    im=im.crop(im.getchannel('A').getbbox()).resize((round(w*S),round(h*S)),Image.Resampling.LANCZOS)
    canvas.paste(im,(round((cx-w/2)*S),round((cy-h/2)*S)),im)

# A real conventional spade glyph used as comparison, not an invented source
# supposedly subtracted from C. Equal visible height and vertical axis; preserve
# the glyph's natural aspect ratio. Heart and C retain their template ratio.
mask=Image.new('L',(900,900))
ImageDraw.Draw(mask).text((40,0),'\u2660',font=ImageFont.truetype('C:/Windows/Fonts/seguisym.ttf',760),fill=255)
mask=mask.crop(mask.getbbox())
target_h=416
spade_w=round(mask.width*target_h/mask.height)
mask=mask.resize((spade_w,target_h),Image.Resampling.LANCZOS).point(lambda v:255 if v>=128 else 0)
pix=mask.load();w,h=mask.size
edges={}
for y in range(h):
    for x in range(w):
        if not pix[x,y]: continue
        if y==0 or not pix[x,y-1]: edges[(x,y)]=(x+1,y)
        if x==w-1 or not pix[x+1,y]: edges[(x+1,y)]=(x+1,y+1)
        if y==h-1 or not pix[x,y+1]: edges[(x+1,y+1)]=(x,y+1)
        if x==0 or not pix[x-1,y]: edges[(x,y+1)]=(x,y)
start=min(edges,key=lambda p:(p[1],p[0]));path=[start];p=edges[start]
while p!=start:
    path.append(p);p=edges[p]
path.append(start)

text(40,30,'VARIANTE C / CONFRONTO CON LA PICCA',29)
text(40,76,'Contorno blu tratteggiato: picca classica (Segoe UI Symbol, U+2660).',18,BLUE)
text(40,106,'Stessa altezza e stesso asse della C; proporzioni originali della picca.',18)
cx,cy=845,398
stamp(ROOT.parent/'Symbols/heart.png',300,cy,352,352)
stamp(ROOT/'attack_C_extended.png',cx,cy,312,416)
left,top=cx-spade_w/2,cy-208
distance=0
for a,b in zip(path,path[1:]):
    length=math.dist(a,b)
    if distance%18<10:
        line([(left+a[0],top+a[1]),(left+b[0],top+b[1])],BLUE,3)
    distance+=length

# Shared tip and base levels expose the deliberately taller attack silhouette.
line([(cx-spade_w/2-20,190),(cx+spade_w/2+20,190)])
line([(cx-spade_w/2-20,606),(cx+spade_w/2+20,606)])
text(201,646,'CUORE / 88 x 88',21)
text(733,646,'C / 78 x 104',21)
text(676,679,f'PICCA / {spade_w/4:.1f} x 104',19,BLUE)
text(40,736,'Ingrandimento 4x delle misure nel template. C invariata; solo sovrapposizione.',18)
canvas.resize((1200,800),Image.Resampling.LANCZOS).save(ROOT/'C_SPADE_OVERLAY_HEART.png')
(ROOT/'C_SPADE_OVERLAY_NOTES.md').write_text(
    '# Confronto C / picca\n\n'
    'C e cuore sono gli asset esistenti, senza modifiche. '
    'La picca tratteggiata e il glifo U+2660 del font Windows Segoe UI Symbol, '
    'usato esclusivamente come riferimento di confronto: non e una maschera '
    'da cui fosse stata sottratta matematicamente la C.\n\n'
    f'C: 78 x 104. Cuore: 88 x 88. Picca: {spade_w/4:.2f} x 104. '
    'Picca e C allineate per altezza visibile e asse verticale; '
    'la picca mantiene le sue proporzioni. Tavola a ingrandimento 4x.\n',encoding='utf-8')
print(ROOT/'C_SPADE_OVERLAY_HEART.png')
