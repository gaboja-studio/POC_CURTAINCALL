import bpy,math
from mathutils import Vector,Quaternion,Matrix
from math import sin,cos,pi,radians
ROOT='D:/github/POC_CURTAINCALL'
s=bpy.data.scenes['CC_ItemInteractions_088'];bpy.context.window.scene=s
r=next(o for o in s.objects if o.type=='ARMATURE')
marker=next(o for o in s.objects if o.name.startswith('ItemMotionBindMarker'))
source=open(ROOT+'/Tasks/Active/Task-20261101-088-item-art-interaction/Source/build-interactions.py').read()
exec(source[source.index('def rot'):source.index('bpy.ops.mesh.primitive_cube_add')])
def aim_bone(b,direction):
 q=(b.tail-b.head).rotation_difference(direction)
 m=q.to_matrix().to_4x4()@b.matrix;m.translation=b.head.copy();b.matrix=m;bpy.context.view_layer.update()
def arm(side,target,pole):
 u=r.pose.bones[side+'UpperArm'];l=r.pose.bones[side+'LowerArm'];h=r.pose.bones[side+'Hand']
 root=u.head.copy();l1=(l.head-root).length;l2=(h.head-l.head).length
 delta=Vector(target)-root;d=min(delta.length,l1+l2-.002);direction=delta.normalized()
 bend=Vector(pole)-root;bend=(bend-direction*bend.dot(direction)).normalized()
 along=(l1*l1-l2*l2+d*d)/(2*d);joint=root+direction*along+bend*math.sqrt(max(0,l1*l1-along*along))
 aim_bone(u,joint-root);aim_bone(l,root+direction*d-l.head)
def hand_frame():
 h=r.pose.bones['RightHand'];middle=r.pose.bones['RightMiddleProximal'];index=r.pose.bones['RightIndexProximal'];little=r.pose.bones['RightLittleProximal']
 f=(middle.head-h.head).normalized();a=(index.head-little.head).normalized();n=a.cross(f).normalized();a=f.cross(n).normalized()
 current=Matrix((f,a,n)).transposed()
 f=Vector((0,-1,0));a=Vector((0,0,1));n=a.cross(f);target=Matrix((f,a,n)).transposed()
 q=(target@current.inverted()).to_quaternion();m=q.to_matrix().to_4x4()@h.matrix;m.translation=h.head.copy();h.matrix=m;bpy.context.view_layer.update()
for stage in range(4):
 name='ItemCrawl'+('_Cut'+str(stage) if stage else '')
 old=bpy.data.actions.get(name)
 if old:bpy.data.actions.remove(old)
 action=bpy.data.actions.new(name);action.use_fake_user=True;r.animation_data.action=action
 for frame in range(1,50,2):
  t=(frame-1)/48;w=sin(2*pi*t)
  pose('ItemHold',t,stage)
  rot('Hips',80+2*w,0,-4);rot('Spine',-8,0,2);rot('Chest',-7,0,0);rot('Neck',-38);rot('Head',-12)
  bpy.context.view_layer.update()
  hips=r.pose.bones['Hips'];hips.location+=hips.bone.matrix_local.to_3x3().inverted()@Vector((0,0,.18+.012*w-hips.head.z));bpy.context.view_layer.update()
  # Right forearm advances, plants and pulls. The left hand stays folded at the
  # chest even before it is lost. A held tool travels with the surviving right hand.
  arm('Right',(-.32,-.52-.16*cos(2*pi*t),.25+.06*max(0,w)),(-.48,-.31,.055))
  arm('Left',(.08,-.25,.33),(.22,-.20,.37))
  hand_frame()
  for b in r.pose.bones:
   b.keyframe_insert('rotation_quaternion',frame=frame,group=b.name);b.keyframe_insert('location',frame=frame,group=b.name)
 s.frame_start=1;s.frame_end=49;s.frame_set(1)
 bpy.ops.object.select_all(action='DESELECT');r.hide_set(False);r.select_set(True);marker.hide_set(False);marker.select_set(True);bpy.context.view_layer.objects.active=r
 bpy.ops.export_scene.fbx(filepath=ROOT+'/Assets/Resources/ItemArt/Motions/'+name+'.fbx',use_selection=True,object_types={'ARMATURE','MESH'},use_armature_deform_only=True,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',apply_unit_scale=True,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
marker.hide_set(True);marker.hide_render=True
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/Tasks/Active/Task-20261101-088-item-art-interaction/Source/item-interactions.blend')
print('Right-driven crawl authored/exported for all injury variants')
