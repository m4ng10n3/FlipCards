from pathlib import Path
from PIL import Image,ImageDraw
root=Path(__file__).resolve().parents[1]
p=root/'Layout/index.html'
s=p.read_text(encoding='utf-8')
s=s.replace(".paper{background:#e9d9b4 url('../UI/ivory_panel.png')", ".paper{border-radius:8px;background:transparent url('../UI/ivory_panel_cropped.png')")
s=s.replace('.inspector{padding:28px 24px;font:19px/1.4 Georgia}', '.inspector{padding:24px 22px;font:18px/1.32 Georgia;overflow:hidden}')
s=s.replace('.inspector h2{font-size:25px;', '.inspector h2{font-size:24px;')
s=s.replace('.stats{background:#102522ed;border:1px solid #957b49;border-radius:5px;padding:5px 10px;box-shadow:inset 0 2px 8px #0009}', '.stats{padding:2px 10px;background:none;border:0}')
s=s.replace('.statrow{display:flex;height:27px;', '.statrow{display:flex;height:27px;')
s=s.replace('src="../Machine/cabinet.png"', 'id="machineImage" src="../Machine/cabinet_integrated.png"')
s=s.replace('left:1610px;top:104px;width:300px;', 'left:1584px;top:32px;width:300px;')
s=s.replace('left:1730px;top:126px;width:166px;height:410px', 'left:1700px;top:80px;width:196px;height:440px')
s=s.replace('left:1714px;top:554px', 'left:1690px;top:488px').replace('left:1704px;top:590px', 'left:1680px;top:523px')
s=s.replace("ctx.drawImage(pivot,91,219,104,138)","ctx.drawImage(pivot,75,219,104,138)")
s=s.replace('style="font-size:17px">La legenda', 'style="font-size:17px">La legenda')
extra='''
// Measured native window centers drive every lane. Lever remains attached to casing.
function applyPitch(pitch=400){
 const nativeCenters=[452,1050,1655.5], scale=pitch/601.75, x=1008-nativeCenters[1]*scale, width=2172*scale;
 const image=document.querySelector('#machineImage');Object.assign(image.style,{left:x+'px',top:'66px',width:width+'px',height:'500px'});
 const galleryY=66+490/724*500;
 nativeCenters.forEach((v,i)=>{const cx=x+v*scale;centers[i]=cx;const well=document.querySelectorAll('.well')[i];Object.assign(well.style,{left:(cx-(i===0?273:i===1?278:279.5)*scale)+'px',top:'189px',width:([546,556,559][i]*scale)+'px',height:'184px'});const st=document.querySelectorAll('.stats')[i];Object.assign(st.style,{left:(cx-166)+'px',top:galleryY+'px',width:'332px',height:'108px'});const card=document.querySelectorAll('#board .card')[i];card.style.left=cx-112+'px';const ax=document.querySelectorAll('.axis')[i];ax.style.left=cx-140+'px';ax.style.top='551px';});
 const edge=x+2089*scale, canvasX=edge-96;document.querySelector('#leverCanvas').style.left=canvasX+'px';document.querySelector('#lever').style.left=canvasX+112+'px';document.querySelector('#leverLabel').style.left=canvasX+90+'px';document.querySelector('#leverHint').style.left=canvasX+82+'px';
 window.layoutGeometry={pitch,centers:[...centers],machine:[x,66,width,500],leverPivot:[canvasX+156,320],reelAxis:[1,0,0],leverAxis:[1,0,0]};leverFrame(-16);
}
applyPitch(400);window.layoutPreview.applyPitch=applyPitch;
'''
s=s.replace('</script></html>',extra+'</script></html>')
p.write_text(s,encoding='utf-8')
im=Image.open(root/'UI/ivory_panel.png').convert('RGBA').crop((44,60,1492,973))
mask=Image.new('L',im.size,0);ImageDraw.Draw(mask).rounded_rectangle((0,0,im.width-1,im.height-1),radius=55,fill=255)
im.putalpha(mask);im.save(root/'UI/ivory_panel_cropped.png')
print('layout refined')
