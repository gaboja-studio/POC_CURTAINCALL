import bpy, math, json
from mathutils import Vector, Quaternion, Matrix
from math import sin, cos, pi, radians
ROOT='D:/github/POC_CURTAINCALL'
SOURCE=ROOT+'/Tasks/Active/Task-20261101-088-item-art-interaction/Source'
s=bpy.data.scenes['CC_ItemInteractions_088'];bpy.context.window.scene=s
r=next(o for o in s.objects if o.type=='ARMATURE')
marker=next(o for o in s.objects if o.name.startswith('ItemMotionBindMarker'))
source=open(SOURCE+'/build-interactions.py').read()
exec(source[source.index('def rot'):source.index('bpy.ops.mesh.primitive_cube_add')])

def anatomical_arm(elevation,flexion):
 # FK: one elbow hinge (local X); no endpoint IK and no independent wrist target.
 # The hinge plane follows the torso and faces forward, not sideways/backward.
 chest=r.pose.bones['Chest'].matrix.to_quaternion()@r.data.bones['Chest'].matrix_local.to_quaternion().inverted()
 y=Vector((-.24,-sin(radians(elevation)),-cos(radians(elevation)))).normalized()
 x=Vector((-1,0,0));x=(x-y*x.dot(y)).normalized();z=x.cross(y).normalized()
 m=(chest.to_matrix()@Matrix((x,y,z)).transposed()).to_4x4()
 upper=r.pose.bones['RightUpperArm'];m.translation=upper.head.copy();upper.matrix=m
 lower=r.pose.bones['RightLowerArm'];lower.rotation_quaternion=Quaternion((1,0,0),radians(flexion))
 hand=r.pose.bones['RightHand'];hand.rotation_quaternion=Quaternion((1,0,0),radians(-8))
 bpy.context.view_layer.update()

def strike(t,stage):
 pose('ItemHold',t,stage)
 # Concept sheet: relaxed ready, lift elbow, apex outside right temple,
 # extend the elbow on the downstroke, soften at contact, recover.
 keys=[(0,15,55,4),(.24,88,72,-5),(.38,125,45,-8),
       (.55,35,18,16),(.68,22,22,20),(1,15,55,4)]
 if stage>=2:keys[3]=(.55,55,18,16);keys[4]=(.68,50,22,20)
 for (a,e0,f0,b0),(b,e1,f1,b1) in zip(keys,keys[1:]):
  if a<=t<=b:
   w=smooth((t-a)/(b-a));e=e0+(e1-e0)*w;f=f0+(f1-f0)*w;bend=b0+(b1-b0)*w;break
 rot('Hips',bend*.3,0,-4 if stage==1 else 0);rot('Spine',bend*.35);rot('Chest',bend*.35,0,0)
 rot('Neck',-bend*.2);rot('Head',-bend*.1)
 rot('LeftUpperArm',0,70,-10);rot('LeftLowerArm',0,0,-20)
 bpy.context.view_layer.update();anatomical_arm(e,f)
 dz=.23-r.pose.bones['Hips'].head.z if stage>=2 else .075-min(r.pose.bones[n].head.z for n in (['RightToes'] if stage==1 else ['LeftToes','RightToes']))
 h=r.pose.bones['Hips'];h.location+=h.bone.matrix_local.to_3x3().inverted()@Vector((0,0,dz));bpy.context.view_layer.update()

samples=[]
for stage in range(4):
 name='ItemStrike'+('_Cut'+str(stage) if stage else '')
 old=bpy.data.actions.get(name)
 if old:bpy.data.actions.remove(old)
 action=bpy.data.actions.new(name);action.use_fake_user=True;r.animation_data.action=action
 for frame in range(1,38):
  strike((frame-1)/36,stage)
  lower=r.pose.bones['RightLowerArm'];hand=r.pose.bones['RightHand']
  samples.append(dict(stage=stage,frame=frame,elbow_degrees=math.degrees(lower.rotation_quaternion.angle),wrist_degrees=math.degrees(hand.rotation_quaternion.angle)))
  for bone in r.pose.bones:
   bone.keyframe_insert('rotation_quaternion',frame=frame,group=bone.name);bone.keyframe_insert('location',frame=frame,group=bone.name)
 s.render.fps=30;s.frame_start=1;s.frame_end=37;s.frame_set(1)
 bpy.ops.object.select_all(action='DESELECT');r.hide_set(False);r.select_set(True);marker.hide_set(False);marker.select_set(True);bpy.context.view_layer.objects.active=r
 bpy.ops.export_scene.fbx(filepath=ROOT+'/Assets/Resources/ItemArt/Motions/'+name+'.fbx',use_selection=True,object_types={'ARMATURE','MESH'},use_armature_deform_only=True,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',apply_unit_scale=True,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
r.animation_data.action=bpy.data.actions['ItemStrike'];s.frame_set(15);marker.hide_set(True);marker.hide_render=True
bpy.ops.wm.save_as_mainfile(filepath=SOURCE+'/item-interactions.blend')
with open(SOURCE+'/anatomical-strike-joints.json','w') as f:json.dump(samples,f,indent=2)
print('Authored 4 FK strikes from four-view concept. Elbow single-axis 18..72 degrees; wrist fixed 8 degrees.')


