import bpy, math, json, os
from mathutils import Vector, Quaternion
from math import sin, cos, pi, radians
ROOT=r'D:/github/POC_CURTAINCALL'
OUT=ROOT+'/Assets/Resources/Models/Characters/CannonMotion'
os.makedirs(OUT+'/Source~',exist_ok=True)
with bpy.data.libraries.load(ROOT+'/Assets/_ThirdParty/CharacterArt/Rabbit/Source~/Source/V14/rabbit-final-v14.blend',link=False) as (a,b): b.scenes=['Rabbit_Rig_v14']
s=b.scenes[0];bpy.context.window.scene=s
r=next(o for o in s.objects if o.type=='ARMATURE')
r.name='RabbitRig_v14'
# Only the intact character is shown for motion review. The original source file is untouched.
for o in s.objects:
    if o.type=='MESH': o.hide_render=(o.name!='RabbitSkin_v14'); o.hide_set(o.hide_render)
r.animation_data_clear(); r.animation_data_create()
s.render.fps=30
specs=[('CannonLoaded',60,True),('CannonRise',36,True),('FlightApex',18,False),('PanicFall',36,True),('TrampolineRise',36,True),('FlightLand',24,False),('TrampolineCarry',60,True)]

def rotation(name,x=0,y=0,z=0):
    b=r.pose.bones.get(name)
    if not b:return
    q=Quaternion((0,0,1),radians(z)) @ Quaternion((0,1,0),radians(y)) @ Quaternion((1,0,0),radians(x))
    rest=b.bone.matrix_local.to_quaternion()
    b.rotation_mode='QUATERNION';b.rotation_quaternion=rest.inverted()@q@rest

def pose(kind,t):
    for b in r.pose.bones: b.location=(0,0,0);b.rotation_mode='QUATERNION';b.rotation_quaternion=(1,0,0,0);b.scale=(1,1,1)
    wave=sin(t*2*pi); alt=cos(t*2*pi)
    pitch=0; h=-0.0
    arms=[(0,72,-8),(0,-72,8)]; knees=[12,12]; hips=[-5,-5]
    spine=-3; head=-2
    if kind in ('CannonLoaded','CannonRise'):
        pitch=65 if kind=='CannonLoaded' else 60+2*wave
        spine=-4;head=-42
        arms=[(-6,82,8),(-6,-82,-8)]
        knees=[8+2*wave,10-2*wave];hips=[-3,-3]
    elif kind=='TrampolineRise':
        pitch=-4+2*wave;spine=-6;head=3
        arms=[(0,-53,-12),(0,53,12)]
        knees=[8+3*wave,8-3*wave];hips=[-3,-3]
    elif kind=='FlightApex':
        u=t*t*(3-2*t);pitch=45-20*u;spine=8*u;head=-12*u
        arms=[(0,30-25*u,-22),(0,-30-10*u,28)]
        knees=[18+32*u,12+16*u];hips=[-12-14*u,-8]
    elif kind=='PanicFall':
        pitch=28+5*wave;spine=8+4*alt;head=-15+4*wave
        arms=[(-16*alt,10+65*wave,-22-22*alt),(16*alt,-10+65*wave,22-22*alt)]
        knees=[48+30*alt,48-30*alt];hips=[-27-18*alt,-27+18*alt]
        rotation('Chest',0,7*wave,7*alt)
    elif kind=='FlightLand':
        # Contact -> deep squash -> overshoot -> settle. Feet are corrected below.
        points=[(0,0.15),(0.21,1.0),(0.50,0.48),(0.79,-0.08),(1,0)]
        amt=0
        for (a,v),(b,w) in zip(points,points[1:]):
            if a<=t<=b:
                u=(t-a)/(b-a);u=u*u*(3-2*u);amt=v+(w-v)*u;break
        pitch=15*amt;spine=12*amt;head=-10*amt
        arms=[(0,50-20*amt,-25-30*amt),(0,-50+20*amt,25+30*amt)]
        hips=[-65*amt,-65*amt];knees=[95*amt,95*amt]
    elif kind=='TrampolineCarry':
        pitch=6;spine=5;head=-6
        arms=[(0,48,-42),(0,-48,42)]
        knees=[5,5];hips=[-3,-3]
    rotation('Hips',pitch,0,1.5*wave if kind=='PanicFall' else 0)
    rotation('Spine',spine);rotation('Neck',head*.35);rotation('Head',head*.65)
    for i,side in enumerate(('Left','Right')):
        sign=1 if i==0 else -1
        rotation(side+'UpperArm',*arms[i])
        rotation(side+'LowerArm',0,0,sign*(-45 if kind=='TrampolineCarry' else -12 if kind in ('CannonLoaded','CannonRise') else -40 if kind=='PanicFall' else -20))
        rotation(side+'UpperLeg',hips[i],0,sign*(5 if kind=='PanicFall' else 0))
        rotation(side+'LowerLeg',knees[i])
        rotation(side+'Foot',-10 if kind in ('CannonRise','TrampolineRise') else -knees[i]*.36)
        for digit in ('Index','Middle','Ring','Little'):
            for joint in ('Proximal','Intermediate','Distal'):
                rotation(side+digit+joint,0,sign*(40 if kind=='TrampolineCarry' else 12),0)
    bpy.context.view_layer.update()
    if kind=='FlightLand':
        # Match floor height in rig space, so Humanoid receives compression rather than root descent.
        foot=min(r.pose.bones[n].head.z for n in ('LeftToes','RightToes'))
        r.pose.bones['Hips'].location=r.data.bones['Hips'].matrix_local.to_3x3().inverted()@Vector((0,0,.075-foot))
    bpy.context.view_layer.update()

bpy.ops.mesh.primitive_cube_add(size=.01,location=(0,0,.91))
marker=bpy.context.object;marker.name='MotionBindMarker';marker.parent=r
vg=marker.vertex_groups.new(name='Hips');vg.add(list(range(len(marker.data.vertices))),1,'REPLACE');mod=marker.modifiers.new('Bind','ARMATURE');mod.object=r
for name,length,loop in specs:
    a=bpy.data.actions.new('CC_'+name);a.use_fake_user=True;r.animation_data.action=a
    for f in range(1,length+2,2):
        frame=min(f,length+1);pose(name,(frame-1)/length)
        for b in r.pose.bones:
            b.keyframe_insert('rotation_quaternion',frame=frame,group=b.name)
            b.keyframe_insert('location',frame=frame,group=b.name)
    # Dense authored samples, auto-clamped handles avoid ringing at extremes.
    for layer in a.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for c in bag.fcurves:
                    for k in c.keyframe_points:k.interpolation='BEZIER';k.handle_left_type='AUTO_CLAMPED';k.handle_right_type='AUTO_CLAMPED'
    s.frame_start=1;s.frame_end=length+1
    bpy.ops.object.select_all(action='DESELECT');r.hide_set(False);r.select_set(True);marker.select_set(True);bpy.context.view_layer.objects.active=r
    bpy.ops.export_scene.fbx(filepath=OUT+'/'+name+'.fbx',use_selection=True,object_types={'ARMATURE','MESH'},use_armature_deform_only=True,apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0,apply_unit_scale=True)
    print(name,length,'frames')
r.animation_data.action=bpy.data.actions['CC_PanicFall'];s.frame_start=1;s.frame_end=37;s.frame_set(5)

marker.hide_set(True);marker.hide_render=True
with open(OUT+'/Source~/clip-manifest.json','w') as f:json.dump([dict(name=n,frames=l+1,fps=30,loop=loop) for n,l,loop in specs],f,indent=2)
# Run save-motion-source.py in a separate Blender process to package the editable rigs.

