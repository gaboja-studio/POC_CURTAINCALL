import bpy, math
from mathutils import Vector, Quaternion, Matrix
from math import sin, cos, pi, radians
ROOT='D:/github/POC_CURTAINCALL'
SOURCE=ROOT+'/Tasks/Active/Task-20261101-088-item-art-interaction/Source'
s=bpy.data.scenes['CC_ItemInteractions_088'];bpy.context.window.scene=s
r=next(o for o in s.objects if o.type=='ARMATURE')
marker=next(o for o in s.objects if o.name.startswith('ItemMotionBindMarker'))
source=open(SOURCE+'/build-interactions.py').read()
exec(source[source.index('def rot'):source.index('bpy.ops.mesh.primitive_cube_add')])
helpers=open(SOURCE+'/build-right-crawl.py').read()
exec(helpers[helpers.index('def aim_bone'):helpers.index('def hand_frame')])

def wrist_axis(axis):
 h=r.pose.bones['RightHand'];f=(r.pose.bones['RightMiddleProximal'].head-h.head).normalized()
 a=(r.pose.bones['RightIndexProximal'].head-r.pose.bones['RightLittleProximal'].head).normalized()
 n=a.cross(f).normalized();a=f.cross(n).normalized();current=Matrix((f,a,n)).transposed()
 a=Vector(axis).normalized();f=Vector((0,-a.z,a.y)).normalized();n=a.cross(f)
 q=(Matrix((f,a,n)).transposed()@current.inverted()).to_quaternion()
 m=q.to_matrix().to_4x4()@h.matrix;m.translation=h.head.copy();h.matrix=m;bpy.context.view_layer.update()

def strike(t,stage):
 pose('ItemHold',t,stage)
 # Wrist positions relative to shoulder height, and tool-axis pitch measured
 # from vertical toward the front (-Y). Contact at .66s in the 1.2s clip.
 keys=[(0,(-.33,-.22,-.42),0,5),(.28,(-.34,.04,.28),-38,-9),
       (.40,(-.34,.08,.30),-48,-11),(.55,(-.34,-.52,-.08),108,19),
       (.67,(-.35,-.45,-.20),114,24),(1,(-.33,-.22,-.42),0,5)]
 for (a,p0,q0,b0),(b,p1,q1,b1) in zip(keys,keys[1:]):
  if a<=t<=b:
   w=smooth((t-a)/(b-a));p=Vector(p0).lerp(Vector(p1),w);angle=q0+(q1-q0)*w;bend=b0+(b1-b0)*w;break
 rot('Hips',bend*.35,0,-5 if stage==1 else -4 if stage==3 else 0)
 rot('Spine',bend*.35);rot('Chest',bend*.30,0,-5*sin(pi*t));rot('Neck',-bend*.25);rot('Head',-bend*.15)
 bpy.context.view_layer.update()
 shoulder=r.pose.bones['RightUpperArm'].head.copy()
 p.z+=shoulder.z
 if stage>=2:p.z+=.05*sin(pi*t)
 arm('Right',p,(-.72,.03,shoulder.z+.02))
 wrist_axis((0,-sin(radians(angle)),cos(radians(angle))))
 # Preserve pelvis/remaining-foot support after the torso anticipation.
 bpy.context.view_layer.update()
 dz=.23-r.pose.bones['Hips'].head.z if stage>=2 else .075-min(r.pose.bones[n].head.z for n in (['RightToes'] if stage==1 else ['LeftToes','RightToes']))
 h=r.pose.bones['Hips'];h.location+=h.bone.matrix_local.to_3x3().inverted()@Vector((0,0,dz));bpy.context.view_layer.update()

for stage in range(4):
 name='ItemStrike'+('_Cut'+str(stage) if stage else '')
 old=bpy.data.actions.get(name)
 if old:bpy.data.actions.remove(old)
 action=bpy.data.actions.new(name);action.use_fake_user=True;r.animation_data.action=action
 for frame in range(1,38):
  strike((frame-1)/36,stage)
  for bone in r.pose.bones:
   bone.keyframe_insert('rotation_quaternion',frame=frame,group=bone.name);bone.keyframe_insert('location',frame=frame,group=bone.name)
 s.render.fps=30;s.frame_start=1;s.frame_end=37;s.frame_set(1)
 bpy.ops.object.select_all(action='DESELECT');r.hide_set(False);r.select_set(True);marker.hide_set(False);marker.select_set(True);bpy.context.view_layer.objects.active=r
 bpy.ops.export_scene.fbx(filepath=ROOT+'/Assets/Resources/ItemArt/Motions/'+name+'.fbx',use_selection=True,object_types={'ARMATURE','MESH'},use_armature_deform_only=True,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',apply_unit_scale=True,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
r.animation_data.action=bpy.data.actions['ItemStrike'];s.frame_set(13);marker.hide_set(True);marker.hide_render=True
bpy.ops.wm.save_as_mainfile(filepath=SOURCE+'/item-interactions.blend')
print('Rebuilt 4 overhead strikes; 1.2 seconds, contact .66 seconds')
