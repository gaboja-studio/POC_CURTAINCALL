import bpy,math,json,os
from mathutils import Vector,Quaternion
from math import sin,cos,pi,radians
ROOT='D:/github/POC_CURTAINCALL'
OUT=ROOT+'/Assets/Resources/ItemArt/Motions'
SOURCE=ROOT+'/Tasks/Active/Task-20261101-088-item-art-interaction/Source'
with bpy.data.libraries.load(ROOT+'/Assets/_ThirdParty/CharacterArt/Rabbit/Source~/Source/V14/rabbit-final-v14.blend',link=False) as (a,b):b.scenes=['Rabbit_Rig_v14']
s=b.scenes[0];s.name='CC_ItemInteractions_088';bpy.context.window.scene=s
r=next(o for o in s.objects if o.type=='ARMATURE');r.name='RabbitRig_v14'
for o in s.objects:
 if o.type=='MESH':o.hide_render=o.name!='RabbitSkin_v14';o.hide_set(o.hide_render)
r.animation_data_clear();r.animation_data_create();s.render.fps=30
def rot(name,x=0,y=0,z=0):
 b=r.pose.bones.get(name)
 if not b:return
 q=Quaternion((0,0,1),radians(z))@Quaternion((0,1,0),radians(y))@Quaternion((1,0,0),radians(x));rest=b.bone.matrix_local.to_quaternion();b.rotation_mode='QUATERNION';b.rotation_quaternion=rest.inverted()@q@rest
def smooth(t):return t*t*(3-2*t)
def envelope(t):return sin(pi*t)**2
def pose(kind,t,stage):
 for b in r.pose.bones:b.location=(0,0,0);b.rotation_mode='QUATERNION';b.rotation_quaternion=(1,0,0,0);b.scale=(1,1,1)
 pulse=envelope(t);wave=sin(2*pi*t);pitch=5;twist=0;side=-5 if stage==1 else -7 if stage==3 else 0
 arm=(0,-64,18);elbow=45;wrist=0;curl=58
 if kind=='ItemStrike':
  # Raise -> held anticipation -> fast contact at 55% -> long recovery.
  keys=[(0,0),(.28,-1),(.42,-1),(.56,1),(.72,.45),(1,0)]
  v=0
  for (a,x),(b,y) in zip(keys,keys[1:]):
   if a<=t<=b:v=x+(y-x)*smooth((t-a)/(b-a));break
  arm=(-20*v,-57+48*abs(v),22+57*v);elbow=50-25*max(v,0);pitch=5+14*v;twist=-18*v
 elif kind=='ItemManipulate':
  arm=(0,-64+28*pulse,18+37*pulse);elbow=45+22*pulse;wrist=22*sin(6*pi*t)*pulse;pitch=5+14*pulse;curl=25
 elif kind=='ItemPull':
  reach=sin(pi*min(t/.45,1)/2);pull=smooth(max(0,min((t-.35)/.45,1)))
  release=smooth(max(0,(t-.8)/.2));arm=(0,-64+35*reach*(1-release),18+40*reach*(1-pull)*(1-release));elbow=45+55*pull*(1-release);pitch=5+18*reach*(1-pull)-17*pull*(1-release);twist=8*pull
 elif kind=='ItemAim':arm=(0,-33,48);elbow=23;pitch=4+.4*wave
 elif kind=='ItemEquip':
  v=1-smooth(t);arm=(15*v,-64,18-44*v);elbow=45+45*v;pitch=5+6*v
 elif kind=='ItemStow':
  v=smooth(t);arm=(15*v,-64,18-44*v);elbow=45+45*v;pitch=5+6*v
 else:pitch=5+.5*wave
 rot('Hips',pitch*.4,0,side);rot('Spine',pitch*.6,0,-side*.6);rot('Chest',0,twist,0);rot('Neck',-pitch*.4);rot('Head',-pitch*.2)
 rot('RightUpperArm',*arm);rot('RightLowerArm',0,0,elbow);rot('RightHand',0,wrist,-8)
 rot('LeftUpperArm',0,76,-8);rot('LeftLowerArm',0,0,-15)
 for hand,sign in [('Right',-1),('Left',1)]:
  for digit in ['Index','Middle','Ring','Little']:
   for joint in ['Proximal','Intermediate','Distal']:rot(hand+digit+joint,0,sign*(curl if hand=='Right' else 15),0)
  rot(hand+'ThumbProximal',0,sign*25,sign*25);rot(hand+'ThumbIntermediate',0,sign*25,0)
  if hand=='Right':
   rot('RightThumbProximal',0,-80,-65);rot('RightThumbIntermediate',0,-60,-15);rot('RightThumbDistal',0,-15,0)
 for leg in ['Left','Right']:
  rot(leg+'UpperLeg',-8-6*pulse if stage<2 else -55);rot(leg+'LowerLeg',12+8*pulse if stage<2 else 90);rot(leg+'Foot',-5)
 if stage==1:rot('RightUpperLeg',-12-8*pulse,0,6);rot('RightLowerLeg',20+12*pulse)
 if stage>=2:
  # Pelvis-supported seated pose, never phantom-foot supported.
  if stage==2:rot('LeftUpperArm',0,80,-15);rot('LeftLowerArm',0,0,-10)
  rot('Hips',pitch,0,side);rot('Spine',6,0,-side*.5)
 bpy.context.view_layer.update()
 z=(.23-r.pose.bones['Hips'].head.z) if stage>=2 else .075-min(r.pose.bones[n].head.z for n in (['RightToes'] if stage==1 else ['LeftToes','RightToes']))
 b=r.pose.bones['Hips'];b.location+=b.bone.matrix_local.to_3x3().inverted()@Vector((0,0,z));bpy.context.view_layer.update()
bpy.ops.mesh.primitive_cube_add(size=.01,location=(0,0,.91));marker=bpy.context.object;marker.name='ItemMotionBindMarker';marker.parent=r
g=marker.vertex_groups.new(name='Hips');g.add(list(range(len(marker.data.vertices))),1,'REPLACE');m=marker.modifiers.new('Bind','ARMATURE');m.object=r
specs=[('ItemStrike',36,False),('ItemManipulate',42,False),('ItemPull',48,False),('ItemAim',60,True),('ItemHold',60,True),('ItemEquip',18,False),('ItemStow',18,False)]
manifest=[]
for kind,frames,loop in specs:
 for stage in range(4):
  name=kind+('_Cut'+str(stage) if stage else '');a=bpy.data.actions.new(name);a.use_fake_user=True;r.animation_data.action=a
  for f in range(1,frames+2,2):
   pose(kind,(f-1)/frames,stage)
   for b in r.pose.bones:b.keyframe_insert('rotation_quaternion',frame=f,group=b.name);b.keyframe_insert('location',frame=f,group=b.name)
  s.frame_start=1;s.frame_end=frames+1;s.frame_set(1)
  bpy.ops.object.select_all(action='DESELECT');r.hide_set(False);r.select_set(True);marker.hide_set(False);marker.select_set(True);bpy.context.view_layer.objects.active=r
  bpy.ops.export_scene.fbx(filepath=OUT+'/'+name+'.fbx',use_selection=True,object_types={'ARMATURE','MESH'},use_armature_deform_only=True,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',apply_unit_scale=True,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
  manifest.append(dict(name=name,stage=stage,frames=frames+1,fps=30,loop=loop))
with open(SOURCE+'/motion-manifest.json','w') as f:json.dump(manifest,f,indent=2)
r.animation_data.action=bpy.data.actions['ItemStrike'];s.frame_start=1;s.frame_end=37;s.frame_set(17);marker.hide_set(True);marker.hide_render=True
bpy.ops.wm.save_as_mainfile(filepath=SOURCE+'/item-interactions.blend')
print('AUTHORED',len(manifest),'clips')
