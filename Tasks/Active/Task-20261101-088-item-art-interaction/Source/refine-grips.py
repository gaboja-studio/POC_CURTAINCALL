import bpy, math, os
from mathutils import Quaternion
ROOT='D:/github/POC_CURTAINCALL'
s=bpy.data.scenes['CC_ItemInteractions_088']
bpy.context.window.scene=s
r=next(o for o in s.objects if o.type=='ARMATURE')
marker=next(o for o in s.objects if o.name.startswith('ItemMotionBindMarker'))
def rot(name,y=0,z=0):
 b=r.pose.bones[name]
 q=Quaternion((0,0,1),math.radians(z))@Quaternion((0,1,0),math.radians(y))
 rest=b.bone.matrix_local.to_quaternion()
 b.rotation_mode='QUATERNION';b.rotation_quaternion=rest.inverted()@q@rest
for a in list(bpy.data.actions):
 if not a.name.startswith('Item'):continue
 r.animation_data.action=a
 end=round(a.frame_range[1])
 for f in range(1,end+1,2):
  s.frame_set(f)
  rot('RightThumbProximal',-80,-65)
  rot('RightThumbIntermediate',-60,-15)
  rot('RightThumbDistal',-15,0)
  for name in ['RightThumbProximal','RightThumbIntermediate','RightThumbDistal']:
   r.pose.bones[name].keyframe_insert('rotation_quaternion',frame=f,group=name)
 s.frame_start=1;s.frame_end=end;s.frame_set(1)
 bpy.ops.object.select_all(action='DESELECT');r.hide_set(False);r.select_set(True);marker.hide_set(False);marker.select_set(True);bpy.context.view_layer.objects.active=r
 bpy.ops.export_scene.fbx(filepath=ROOT+'/Assets/Resources/ItemArt/Motions/'+a.name+'.fbx',use_selection=True,object_types={'ARMATURE','MESH'},use_armature_deform_only=True,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',apply_unit_scale=True,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
r.animation_data.action=bpy.data.actions['ItemHold'];s.frame_start=1;s.frame_end=61;s.frame_set(1);marker.hide_set(True);marker.hide_render=True
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/Tasks/Active/Task-20261101-088-item-art-interaction/Source/item-interactions.blend')
print('Updated opposed thumb in 28 authored actions')
