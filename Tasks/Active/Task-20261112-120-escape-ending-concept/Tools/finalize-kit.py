from pathlib import Path
import json,shutil,hashlib,tarfile,zipfile
from PIL import Image
root=Path('D:/github/POC_CURTAINCALL')
work=root/'.harness/local/escape-ending-concept'
task=work/'Tasks/Active/Task-20261112-120-escape-ending-concept'
kit=work/'Assets/_ThirdParty/curtaincall_escape_results_v1'
project=root/'.harness/local/ui120'
payload=kit/'Unity~'
for relative in ['Assets/_ThirdParty/EscapeResultsKit','Assets/Scripts/EscapeResultsKit',
                 'Assets/Resources/Prefabs/UIs/EscapeResultsKit','Assets/Scenes/Tests/EscapeResultsKit']:
    shutil.copytree(project/relative,payload/relative,dirs_exist_ok=True)
    meta=project/(relative+'.meta')
    if meta.exists():shutil.copy2(meta,payload/(relative+'.meta'))
shutil.copy2(task/'kit-guide.md',kit/'README.md')
for name in ['verification.json','playmode-verification.json']:
    shutil.copy2(project/'Artifacts'/name,kit/'Docs'/name)
for name in ['players-unity.png','statistics-unity.png']:
    shutil.copy2(project/'Artifacts'/name,kit/'Previews'/name)
package=project/'Artifacts/curtaincall-escape-results-v1.unitypackage'
shutil.copy2(package,kit/package.name)
with tarfile.open(package,'r:gz') as archive:
    exported=[archive.extractfile(m).read().decode('utf-8') for m in archive.getmembers() if m.name.endswith('/pathname')]
(kit/'Docs/package-paths.json').write_text(json.dumps(exported,ensure_ascii=False,indent=2),encoding='utf-8')
manifest=json.loads((payload/'Assets/_ThirdParty/EscapeResultsKit/manifest.json').read_text(encoding='utf-8'))
errors=[];reused=0
for a in manifest['assets']:
    file=payload/'Assets/_ThirdParty/EscapeResultsKit/Sprites'/a['file']
    digest=hashlib.sha256(file.read_bytes()).hexdigest()
    if digest!=a['sha256']:errors.append('Changed PNG: '+a['file'])
    image=Image.open(file)
    if image.mode!='RGBA':errors.append('Not RGBA: '+a['file'])
    if not file.with_suffix(file.suffix+'.meta').exists():errors.append('Missing meta: '+a['file'])
    if a['reused']:
        reused+=1
        if hashlib.sha256((root/a['source']).read_bytes()).hexdigest()!=digest:errors.append('Reuse differs: '+a['file'])
    l,b,r,t=a['border']
    if l+r>image.width or b+t>image.height:errors.append('Invalid border: '+a['file'])
atlas=Image.open(payload/'Assets/_ThirdParty/EscapeResultsKit/Sprites/portraits.png')
alpha=atlas.getchannel('A')
if alpha.getextrema()[0]!=0:errors.append('Portrait atlas not transparent')
guids={}
import re
for meta in (payload/'Assets').rglob('*.meta'):
    match=re.search(r'^guid: (\w+)',meta.read_text(encoding='utf-8'),re.M)
    if match:
        guid=match.group(1)
        if guid in guids:errors.append('Duplicate GUID: '+str(meta))
        guids[guid]=str(meta.relative_to(payload))
report={'reused_pngs_identical':reused,'png_count':len(manifest['assets']),'sprite_count':36,
        'exported_assets':len(exported),'unique_guids':len(guids),'portrait_alpha_range':alpha.getextrema(),
        'errors':errors}
(kit/'Docs/package-verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
assert not errors, errors
archive=kit/'curtaincall-escape-results-kit-v1.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
    for file in sorted(kit.rglob('*')):
        if file.is_file() and file!=archive and file.suffix!='.zip':
            z.write(file,file.relative_to(kit).as_posix())
with zipfile.ZipFile(archive) as z:assert z.testzip() is None
print(json.dumps(dict(report,archive=str(archive),bytes=archive.stat().st_size),ensure_ascii=False))
