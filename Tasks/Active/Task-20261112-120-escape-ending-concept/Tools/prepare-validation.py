from pathlib import Path
import json,shutil
root=Path('D:/github/POC_CURTAINCALL')
work=root/'.harness/local/escape-ending-concept'
kit=work/'Assets/_ThirdParty/curtaincall_escape_results_v1/Unity~'
target=root/'.harness/local/ui120'
target.mkdir(parents=True,exist_ok=True)
shutil.copytree(kit/'Assets',target/'Assets',dirs_exist_ok=True)
# This isolated project gets its own Library. Existing project state is never copied.
for folder in ['Packages','ProjectSettings']:(target/folder).mkdir(exist_ok=True)
deps={'com.unity.ugui':'file:'+str(root/'Library/PackageCache/com.unity.ugui@f3ea87521141').replace('\\','/'),
      'com.unity.modules.ui':'1.0.0','com.unity.modules.imgui':'1.0.0','com.unity.modules.imageconversion':'1.0.0',
      'com.unity.modules.jsonserialize':'1.0.0','com.unity.modules.physics':'1.0.0','com.unity.modules.physics2d':'1.0.0',
      'com.unity.modules.uielements':'1.0.0','com.unity.modules.animation':'1.0.0'}
(target/'Packages/manifest.json').write_text(json.dumps({'dependencies':deps},indent=2))
shutil.copy2(root/'ProjectSettings/ProjectVersion.txt',target/'ProjectSettings/ProjectVersion.txt')
# Read-only copies of official TMP Essentials for the validation project only; excluded from kit.
shutil.copytree(root/'Assets/TextMesh Pro',target/'Assets/TextMesh Pro',dirs_exist_ok=True)
print(target)
