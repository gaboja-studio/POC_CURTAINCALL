import bpy, math
from mathutils import Vector
from math import sin,cos,pi
ROOT='D:/github/POC_CURTAINCALL'
scene=bpy.data.scenes['CC_FieldKit_088'];bpy.context.window.scene=scene
mats={m.name[4:]:m for m in bpy.data.materials if m.name.startswith('Kit_') and '.' not in m.name}
objects=[]
source=open(ROOT+'/Tasks/Active/Task-20261101-088-item-art-interaction/Source/build-props.py').read()
exec(source[source.index('def finish'):source.index('def glove')])
for id in ['magician-hairpin','route-key']:
 old=scene.objects.get(id)
 if old:bpy.data.objects.remove(old,do_unlink=True)
 objects=[]
 if id=='magician-hairpin':
  tube('Black double prong',[(-.006,0,.22),(-.006,0,-.005),(.006,0,-.005),(.006,0,.22)],.0035,'Steel')
  for i in range(18):ring('Silk finger grip',(0,0,.005+i*.005),.007,.002,'Velvet')
  star('Brass star finial',(0,0,-.035),.024,'Brass')
  tube('Red bow',[(-.03,0,-.01),(-.018,-.004,.005),(0,0,-.006),(.018,-.004,.005),(.03,0,-.01),(0,0,-.006)],.004,'Red')
 else:
  tube('Antique brass shaft',[(0,0,-.008),(0,0,.22)],.008,'Brass')
  ring('Open key bow',(0,0,-.042),.028,.007,'Brass','Y')
  for i in range(17):ring('Embossed grip',(0,0,.008+i*.005),.008,.0015,'Brass')
  box('Key bit',(.018,0,.195),(.04,.012,.032),'Brass',.002)
  box('Tooth one',(.035,0,.18),(.012,.012,.018),'Brass',.001)
  box('Tooth two',(.035,0,.213),(.012,.012,.012),'Brass',.001)
  star('Bow emblem',(0,-.003,-.042),.012,'Red')
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=bpy.context.object;o.name=id
 scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
 bpy.ops.export_scene.fbx(filepath=ROOT+'/Assets/Resources/ItemArt/Models/'+id+'.fbx',use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',bake_anim=False)
 o.hide_render=True;o.hide_set(True)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/Tasks/Active/Task-20261101-088-item-art-interaction/Source/field-kit.blend')
selected_ids=['magician-hairpin','route-key']
exec(compile(open(ROOT+'/Tasks/Active/Task-20261101-088-item-art-interaction/Source/render-icons.py').read(),'render-icons.py','exec'))
print('Single held hairpin and waiting-room key exported with matching icons')
