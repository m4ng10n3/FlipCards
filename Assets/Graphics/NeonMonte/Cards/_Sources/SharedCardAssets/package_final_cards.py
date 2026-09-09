"""Curated, reproducible delivery of the approved front/back assets."""
from pathlib import Path
import json,shutil,hashlib
from PIL import Image
ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT.parent/'_Final'
OUT.mkdir(exist_ok=True)
inventory=[]

def copy(src,dest):
    dest.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(src,dest)

for face,source in [('Front','Front_French_v2'),('Back','Back_French_v2')]:
    src=ROOT/source;dst=OUT/face;dst.mkdir(exist_ok=True)
    prefix=face.lower()
    manifest=json.loads((src/'layout_manifest.json').read_text(encoding='utf-8'))
    if face=='Front':
        templates=['front_clean.png','front_paper.png','front_frame.png','front_frame.svg','charge_backplates.png']
        names=['faction_sun','faction_moon','faction_saturn','star','charge_ring','charge_full']
        indicators=['drop_full','drop_empty','attack_full','attack_empty']
        manifest['tally_fill_order']={'health':'top_to_bottom','attack':'bottom_to_top'}
        manifest['frame_art']={'file':'Template/front_frame.png','svg':'Template/front_frame.svg','technique':'darkened tattoo relief, charcoal contour, warm ivory ridge, generated ink stippling','svg_format':'embedded finished RGBA'}
        limiter=manifest.pop('text_top_limiter')
        optional=OUT/'Optional'/'TextTopLimiter'
        for name in ['text_top_limiter.png','text_top_limiter_mask.png','text_top_limiter.svg','text_top_limiter_overlay.png']:
            copy(src/'Template'/name,optional/name)
        limiter['file']='text_top_limiter.png';limiter['mask']='text_top_limiter_mask.png';limiter['svg']='text_top_limiter.svg';limiter['overlay']='text_top_limiter_overlay.png'
        limiter.pop('preview',None)
        (optional/'placement.json').write_text(json.dumps(limiter,indent=2),encoding='utf-8')
        manifest['optional_text_limiter']='../Optional/TextTopLimiter/placement.json'
    else:
        templates=['back_clean.png','back_paper.png','back_frame.png','back_frame.svg','back_artwork_overlay.png']
        names=['faction_sun','faction_moon','faction_saturn','star','charge_ring','charge_full','attack_spade','defense_club_B']
        indicators=['drop_full','drop_empty','defense_full','defense_empty']
        copy(src/'Artwork/three_eyes_ornament.png',dst/'Artwork/three_eyes_ornament.png')
        manifest.pop('layout_reference',None)
    for name in templates:copy(src/'Template'/name,dst/'Template'/name)
    for group,items in [('Symbols',names),('Indicators',indicators)]:
        for name in items:
            for suffix in ['', '_mask']:
                asset=src/group/(name+suffix+'.png')
                if asset.exists():copy(asset,dst/group/asset.name)
    for name in ['sun','moon','saturn','empty']+(['defense_banner'] if face=='Back' else []):
        for kind in ['preview','overlay']:
            file=f'{prefix}_{name}_{kind}.png'
            copy(src/'Template'/file,dst/'Previews'/file)
    for name in ['symbols_normalized_atlas.png','QA_REPORT.json']:
        copy(src/name,dst/name)
    def production_refs(value):
        if isinstance(value,dict):return {k:production_refs(v) for k,v in value.items() if k!='source'}
        if isinstance(value,list):return [production_refs(v) for v in value]
        if isinstance(value,str) and value.startswith('Template/') and ('_preview.' in value or '_overlay.' in value) and not value.endswith('artwork_overlay.png'):
            return value.replace('Template/','Previews/',1)
        return value
    manifest=production_refs(manifest)
    manifest['release']='final-2026-09-09'
    manifest['preview_gallery']={name:f'Previews/{prefix}_{name}_preview.png' for name in ['sun','moon','saturn','empty']}
    (dst/'layout_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')

copy(ROOT/'SharedCardAssets/FRONT_BACK_COMPARISON.png',OUT/'FINAL_PREVIEW.png')
readme='''# Neon Monte — asset carte finali

Pacchetto di produzione approvato, 9 settembre 2026. Aprire FINAL_PREVIEW.png per il confronto; Front/Previews e Back/Previews contengono le tre fazioni con statistiche di esempio e le versioni con tacche vuote.

## Cartelle

- Front: template definitivo con cornice tatuata leggermente scurita, sprite vita/attacco, simboli astronomici, atlante e manifest.
- Back: template rosso con ornamento a tre occhi, sprite vita/difesa, insegne, atlante e manifest.
- Optional/TextTopLimiter: separatore leggero con PNG, maschera, SVG, overlay e placement.json. Non montato nelle basi o nelle anteprime finali.
- FILES.json: inventario, dimensioni e SHA-256 dei file consegnati. QA_REPORT.json: verifica del pacchetto.

## Montaggio

Usare Front/Template/front_clean.png oppure Back/Template/back_clean.png come base, poi montare simboli, tacche, testo dinamico e cariche secondo il layout_manifest.json della faccia. Le basi hanno già la cornice definitiva: non aggiungerla nuovamente sopra. I livelli paper/frame/artwork sono disponibili per chi vuole ricomporre la base.

Front: gocce dall'alto, lance romboidali dal basso. Back: gocce e scudi semplici entrambi dall'alto; un valore N riempie le prime N posizioni. Gli indici nei manifest sono ordinati per Y crescente. Sette tacche per colonna. Le punte sono già orientate verso il centro, rotazione runtime zero. Nessuna intestazione statistica aggiuntiva.

I simboli grandi d'insegna del back restano separati dalle tacche: attack_spade o defense_club_B con valore numerico dinamico. Le preview usano valore 2, vita 3, attacco/difesa 4. Il front mostra 0 cariche, il back 1. Non usare una preview con valori incorporati come base dinamica.

Sul front applicare charge_backplates.png prima dei tre cerchi per interrompere la cornice sotto le cariche. Sul back la linea è già interrotta. Gli estremi verticali interni del front restano allineati alla Y del centro dei simboli fazione.

PNG base 1024×1536; target 224×336, fattore 0.21875. Coordinate dall'alto a sinistra. I singoli simboli sono celle 256×256 con trasparenza: usare full_sprite_render_size e alpha_bbox del manifest, non confondere la cella con l'ingombro d'inchiostro. Preservare i colori originali o usare i file _mask per una tinta runtime. Gli overlay nelle Previews sono solo esempi di composizione.

Import Unity: Sprite, Point, compressione disattivata, niente mipmap, alpha come trasparenza, Pixels Per Unit 1. Nessun Raycast Target sui figli grafici. Per l'atlante front i rect sono rect_top_left, per il back rect: entrambi partono dall'alto; convertire Y per Sprite Editor. Le cornici SVG finali possono contenere raster incorporati; usare i PNG a runtime.

## Sorgenti e ricostruzione

Le cartelle di lavoro originali restano Front_French_v2, Back_French_v2 e SharedCardAssets in Cards/_Sources. Sources e Studies conservano i riferimenti generati, i prompt e le revisioni storiche; non sono parte del pacchetto di produzione. Non usare le vecchie prove di elmo o glifi come tacche.

Con Python + Pillow + NumPy, dalla cartella Cards/_Sources:

1. python Front_French_v2/build_assets.py
2. python Back_French_v2/build_assets.py
3. python SharedCardAssets/build_comparison.py
4. python SharedCardAssets/package_final_cards.py

La ricostruzione usa le sorgenti già salvate e non effettua chiamate imagegen. I file finali sono una consegna riproducibile dei builder. Questa chiusura riguarda gli asset: nessun prefab, scena o codice di combattimento è stato modificato.
'''
(OUT/'README.md').write_text(readme,encoding='utf-8')

# Validate actual output pixels and shipping files, including top-down shields.
front=OUT/'Front';back=OUT/'Back'
fqa=json.loads((front/'QA_REPORT.json').read_text(encoding='utf-8'))
bqa=json.loads((back/'QA_REPORT.json').read_text(encoding='utf-8'))
paper=Image.open(front/'Template/front_paper.png').convert('RGBA')
frame=Image.open(front/'Template/front_frame.png').convert('RGBA')
clean=Image.open(front/'Template/front_clean.png').convert('RGBA')
checks={'front_clean_is_paper_plus_final_frame':Image.alpha_composite(paper,frame).tobytes()==clean.tobytes(),
    'front_no_tally_frame_overlap':fqa['pip_frame_overlap_pixels']==0,
    'back_no_tally_frame_overlap':bqa['pip_frame_overlap_pixels']==0,
    'front_frame_faction_alignment':fqa['inner_vertical_ends_at_faction_center'],
    'back_shields_fill_from_top':all(bqa['defense_top_to_bottom_raster_checks'].values()),
    'separator_only_optional':not (front/'Template/text_top_limiter.png').exists() and (OUT/'Optional/TextTopLimiter/text_top_limiter.png').exists()}
for face in ['Front','Back']:
    manifest=json.loads((OUT/face/'layout_manifest.json').read_text(encoding='utf-8'))
    def strings(value):
        if isinstance(value,dict):
            for v in value.values():yield from strings(v)
        elif isinstance(value,list):
            for v in value:yield from strings(v)
        elif isinstance(value,str):yield value
    missing=[p for p in strings(manifest) if p.endswith(('.png','.svg','.json')) and not (OUT/face/p).is_file()]
    checks[face.lower()+'_manifest_paths_exist']=not missing
    if missing:raise RuntimeError(missing)
assert all(checks.values()),checks
(OUT/'QA_REPORT.json').write_text(json.dumps({'all_passed':True,'checks':checks},indent=2),encoding='utf-8')
for p in sorted(OUT.rglob('*')):
    if not p.is_file() or p.name=='FILES.json' or p.suffix=='.meta':continue
    item={'path':p.relative_to(OUT).as_posix(),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
    if p.suffix=='.png':
        with Image.open(p) as im:item.update(size=list(im.size),mode=im.mode)
    inventory.append(item)
(OUT/'FILES.json').write_text(json.dumps({'release':'final-2026-09-09','files':inventory},indent=2),encoding='utf-8')
print(json.dumps({'package':str(OUT),'files':len(inventory),'all_passed':True},indent=2))
