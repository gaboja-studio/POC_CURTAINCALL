"""Package original UI resources without modifying them; create new code-native icon geometry.
Original raster art is copied byte-for-byte. Character atlas is generated externally and only
sliced by Unity metadata. No concept image is cropped into UI art.
"""
from pathlib import Path
import json, shutil, hashlib, sys
from PIL import Image, ImageDraw

REPO=Path('D:/github/POC_CURTAINCALL')
WORK=REPO/'.harness/local/escape-ending-concept'
TASK=WORK/'Tasks/Active/Task-20261112-120-escape-ending-concept'
KIT=WORK/'Assets/_ThirdParty/curtaincall_escape_results_v1'
UNITY=KIT/'Unity~'
ART=UNITY/'Assets/_ThirdParty/EscapeResultsKit'
CODE=UNITY/'Assets/Scripts/EscapeResultsKit'
for p in [ART/'Sprites',ART/'Fonts',ART/'Source',CODE/'Editor',KIT/'Docs',KIT/'Previews']:
    p.mkdir(parents=True,exist_ok=True)
manifest=[]
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def add(key,src,border=(0,0,0,0),reused=True):
    dst=ART/'Sprites'/src.name
    if src.resolve()!=dst.resolve(): shutil.copy2(src,dst)
    im=Image.open(dst)
    assert im.mode=='RGBA', (src,im.mode)
    manifest.append(dict(key=key,file=dst.name,width=im.width,height=im.height,border=list(border),
        ppu=200,reused=reused,source=str(src.relative_to(REPO)) if src.is_relative_to(REPO) else src.name,
        sha256=sha(dst)))

root=REPO/'Assets/_ThirdParty/curtaincall_results_v1'
plan=json.loads((root/'Docs/results_asset_plan.json').read_text(encoding='utf-8'))
keys=['ribbon_success','reward_row','panel_shadow','panel_body','panel_frame','panel_corners',
      'panel_divider','confirm_normal','confirm_hover','confirm_pressed','confirm_disabled',
      'button_shadow','button_focus','icon_currency']
for a in plan['assets']:
    if a['key'] in keys:
        l,r,t,b=a['border_lr_tb_px']; add(a['key'],root/a['relative_path'],(l,b,r,t))
shop=REPO/'Assets/_ThirdParty/curtaincall_shop_v1/Assets/CurtainCallUI/CommonTabs/Sprites'
for state in ['normal','hover','pressed','selected','focus','disabled']:
    shopplan=json.loads((REPO/'Assets/_ThirdParty/curtaincall_shop_v1/Docs/shop_asset_plan.json').read_text(encoding='utf-8'))
    spec=next(a for a in shopplan['assets'] if a['key']=='tab_'+state)
    l,r,t,b=spec['border_lr_tb_px'];add('tab_'+state,shop/f'ui_tab_standard_{state}@2x.png',(l,b,r,t))
for item in ['rescue-gloves','sticky-gloves','balloon-pants','spring-shoes','magician-hairpin','stake-puller','uv-lamp','route-key']:
    add('item_'+item,REPO/f'Assets/Resources/ItemArt/Icons/{item}-slot.png')

# Extend the existing code-native UI geometry. SVG masters and PNGs share one specification.
def geom(key,svg,paint,size=128):
    (ART/'Source'/f'{key}.svg').write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="{size}" height="{size}" viewBox="0 0 {size} {size}">{svg}</svg>',encoding='utf-8')
    # Supersampled drawing of NEW vector geometry, not raster editing of existing artwork.
    im=Image.new('RGBA',(size*4,size*4)); d=ImageDraw.Draw(im)
    paint(d,4); im=im.resize((size,size),Image.Resampling.LANCZOS)
    dst=ART/'Sprites'/f'ui_{key}@2x.png'; im.save(dst); add(key,dst,reused=False)
geom('circle','<circle cx="64" cy="64" r="60" fill="white"/>',lambda d,s:d.ellipse((4*s,4*s,124*s,124*s),fill='white'))
geom('portrait_ring','<circle cx="64" cy="64" r="59" fill="none" stroke="white" stroke-width="3"/>',lambda d,s:d.ellipse((5*s,5*s,123*s,123*s),outline='white',width=3*s))
def escaped(d,s):
    d.line([(22*s,112*s),(22*s,16*s),(87*s,16*s),(87*s,52*s)],fill='white',width=7*s)
    d.line([(22*s,112*s),(58*s,112*s)],fill='white',width=7*s)
    d.ellipse((61*s,28*s,77*s,44*s),fill='white')
    for pts,w in [([(65,48),(53,70),(77,83),(69,108)],9), ([(55,67),(39,88),(26,89)],8), ([(64,49),(81,64),(102,64)],8)]:
        d.line([(x*s,y*s) for x,y in pts],fill='white',width=w*s)
    d.polygon([(100*s,53*s),(115*s,64*s),(100*s,75*s)],fill='white')
geom('escaped','<path d="M22 112V16H87V52M22 112H58M65 48L53 70L77 83L69 108M55 67L39 88H26M64 49L81 64H102" fill="none" stroke="white" stroke-width="8"/><circle cx="69" cy="36" r="8" fill="white"/><path d="M100 53L115 64L100 75Z" fill="white"/>',escaped)
def missing(d,s):
    d.rectangle((24*s,16*s,100*s,112*s),outline='white',width=7*s)
    d.ellipse((80*s,61*s,87*s,68*s),fill='white')
    for a,b in [((20,20),(108,108)),((108,20),(20,108))]:d.line([(a[0]*s,a[1]*s),(b[0]*s,b[1]*s)],fill='white',width=8*s)
geom('not_escaped','<path d="M24 16H100V112H24ZM20 20L108 108M108 20L20 108" fill="none" stroke="white" stroke-width="8"/><circle cx="84" cy="65" r="4" fill="white"/>',missing)
atlas=Path('C:/Users/imvag/.codex/generated_images/01a1cf02-a4d4-72d2-a2cc-cc69171be3ca/exec-2d3bbd9d-4f96-411f-9531-e3b8f9d39618.png')
target=ART/'Sprites/portraits.png'; shutil.copy2(atlas,target); add('portraits',target,reused=False)
for name in ['Pretendard-Medium.ttf','Pretendard-SemiBold.ttf','Pretendard-Bold.ttf','LICENSE.txt']:
    shutil.copy2(root/'Source/Fonts'/name,ART/'Fonts'/name)
data=dict(version='1.0.0',referenceResolution=[1920,1080],panelRect=[40,60,800,960],
          reused=sum(x['reused'] for x in manifest),newTextures=sum(not x['reused'] for x in manifest),assets=manifest)
(ART/'manifest.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
names={'escape-results-view.cs.txt':'EscapeResultsView.cs','escape-results-focus.cs.txt':'EscapeResultsFocus.cs',
       'escape-results-preview.cs.txt':'EscapeResultsPreview.cs','escape-results-builder.cs.txt':'EscapeResultsBuilder.cs',
       'escape-results-verification.cs.txt':'EscapeResultsVerification.cs'}
for src in (TASK/'Source').rglob('*.cs.txt'):
    relative=src.relative_to(TASK/'Source')
    dst=CODE/relative.parent/names[src.name];dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst)
print(json.dumps({'kit':str(KIT),'reused':data['reused'],'new_textures':data['newTextures']},ensure_ascii=False))
