# Rebuild only nine revised actions; keep all unrelated FBXs and their GUIDs.
from pathlib import Path
source=Path(r'D:/github/POC_CURTAINCALL/Assets/Resources/Models/Characters/CannonMotion/Source~')
motion_filter={name+'_Cut'+str(stage) for name in ('FlightLand','TrampolineCarry','RopePull') for stage in (1,2,3)}
exec(compile((source/'build-motion.py').read_text(encoding='utf-8-sig').split('for name,length,loop in specs:')[0],'build-motion-setup','exec'))
exec(compile((source/'build-variants.py').read_text(encoding='utf-8-sig'),'build-variants.py','exec'))
