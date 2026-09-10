from pathlib import Path
import json

# Generated geometry: every value comes from the delivered face manifests.
# The sources are archived; the delivered package lives in the active style.
assets=next(p for p in Path(__file__).resolve().parents if p.name=='Assets')
root=assets/'Graphics/1_NeonMonte_Attivo/Cards/_Final'
f=json.loads((root/'Front/layout_manifest.json').read_text()); b=json.loads((root/'Back/layout_manifest.json').read_text())
def num(v): return format(v,'.8g')+'f'
def vec(v): return 'new Vector2('+', '.join(num(x) for x in v)+')'
lines=['using UnityEngine;', '', '// Generated from Cards/_Final/*/layout_manifest.json; source pixels, top-left origin.', 'public static class FinalCardLayout', '{', '    public const float Scale = 224f / 1024f;']
for key,label in [('title','Title'),('artwork','Artwork'),('ability','Ability')]:
    x,y,r,d=f['safe_regions'][key]; lines.append(f'    public static readonly Rect {label} = new Rect({num(x*224/1024)}, {num(y*224/1024)}, {num((r-x)*224/1024)}, {num((d-y)*224/1024)});')
for face,data in [('Front',f),('Back',b)]:
    for key,label in [('left_tallies','Health'),('right_tallies','Power')]:
        lines.append(f'    public static readonly Vector2[] {face}{label} = {{ '+', '.join(vec(v) for v in data[key]['centers'])+' };')
        lines.append(f'    public static readonly Vector2 {face}{label}Size = '+vec(data[key]['full_sprite_render_size'])+';')
    for key,label in [('faction_centers','Factions'),('charges','Charges')]:
        values=data[key]['centers'] if isinstance(data[key],dict) else data[key]
        lines.append(f'    public static readonly Vector2[] {face}{label} = {{ '+', '.join(vec(v) for v in values)+' };')
    lines.append(f'    public static readonly Vector2 {face}FactionSize = '+vec(data['faction_full_sprite_render_size'])+';')
lines+=['}', '']
(assets/'Scripts/UI/FinalCardLayout.cs').write_text('\n'.join(lines),encoding='utf-8')
