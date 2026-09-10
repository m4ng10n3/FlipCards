from pathlib import Path
from PIL import Image
root=Path(__file__).resolve().parents[1]
im=Image.open(root/'Machine/cabinet_integrated.png')
boxes={'cabinet_header':(0,0,2172,160),'cabinet_footer':(0,610,2172,724),'cabinet_left':(0,160,148,610),'cabinet_right':(1956,160,2172,610),'cabinet_reel_module':(748,160,1352,610)}
for name,box in boxes.items():im.crop(box).save(root/'Machine'/f'{name}.png')
p=root/'Layout/index.html';s=p.read_text(encoding='utf-8')
s=s.replace('.reel-art{width:186px;height:186px;', '.reel-art{width:146px;height:146px;')
s=s.replace('transform:translate(-50%,-50%);mix-blend-mode:multiply}', 'transform:translate(-50%,-43%);mix-blend-mode:multiply}')
s=s.replace('.statvalue{margin-left:auto;', '.lamp[src*="_off"]{filter:brightness(.63) saturate(.75)}.statvalue{margin-left:auto;')
s=s.replace('<div id="stats"></div>', '<div id="modularMachine" class="abs" style="pointer-events:none"></div><div id="stats"></div>')
start=s.index('function applyPitch(pitch=400){')
end=s.index('applyPitch(400);window.layoutPreview.applyPitch=applyPitch;',start)
fn='''function applyPitch(pitch=400){
 const scale=pitch/604,left=148*scale,right=216*scale,width=left+3*pitch+right,x=1008-1.5*pitch-left;
 document.querySelector('#machineImage').style.display='none';
 const machine=document.querySelector('#modularMachine');Object.assign(machine.style,{left:x+'px',top:'66px',width:width+'px',height:'500px'});
 function part(file,l,t,w,h){return `<img src="../Machine/${file}.png" alt="" class="abs" style="left:${l}px;top:${t}px;width:${w}px;height:${h}px">`;}
 const y=160/724*500,h=450/724*500;
 machine.innerHTML=part('cabinet_header',0,0,width,y)+part('cabinet_footer',0,610/724*500,width,114/724*500)+part('cabinet_left',0,y,left,h)+part('cabinet_right',left+3*pitch,y,right,h)+[0,1,2].map(i=>part('cabinet_reel_module',left+i*pitch,y,pitch,h)).join('');
 const galleryY=66+490/724*500;
 [0,1,2].forEach(i=>{const cx=1008+(i-1)*pitch;centers[i]=cx;const well=document.querySelectorAll('.well')[i];Object.assign(well.style,{left:(cx-278*scale)+'px',top:'189px',width:(556*scale)+'px',height:'184px'});const st=document.querySelectorAll('.stats')[i];Object.assign(st.style,{left:(cx-166)+'px',top:galleryY+'px',width:'332px',height:'108px'});const card=document.querySelectorAll('#board .card')[i];card.style.left=cx-112+'px';const ax=document.querySelectorAll('.axis')[i];ax.style.left=cx-140+'px';ax.style.top='538px';});
 const edge=x+width-83*scale,canvasX=edge-96;document.querySelector('#leverCanvas').style.left=canvasX+'px';document.querySelector('#lever').style.left=canvasX+112+'px';document.querySelector('#leverLabel').style.left=canvasX+90+'px';document.querySelector('#leverHint').style.left=canvasX+82+'px';
 window.layoutGeometry={pitch,centers:[...centers],machine:[x,66,width,500],leverPivot:[canvasX+156,320],reelAxis:[1,0,0],leverAxis:[1,0,0]};leverFrame(-16);
}
'''
s=s[:start]+fn+s[end:]
p.write_text(s,encoding='utf-8')
print('Modular cabinet: five slices, exact 400 px lane pitch')
