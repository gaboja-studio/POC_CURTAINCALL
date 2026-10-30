# Run after build-motion.py in the same fresh background Blender process.
# Uses its authored rig, pose helpers and bind marker.
extra=[('RopePull',60,False),('RopeLift',60,False),('RopeHang',60,True),('CarryJump',36,True)]
all_specs=specs+extra

def addrot(name,x=0,y=0,z=0):
    b=r.pose.bones.get(name)
    if not b:return
    rest=b.bone.matrix_local.to_quaternion()
    q=Quaternion((0,0,1),radians(z))@Quaternion((0,1,0),radians(y))@Quaternion((1,0,0),radians(x))
    b.rotation_quaternion=b.rotation_quaternion@(rest.inverted()@q@rest)

def movehips(z):
    b=r.pose.bones['Hips'];b.location+=b.bone.matrix_local.to_3x3().inverted()@Vector((0,0,z))

def extra_pose(name,t):
    if name not in [n for n,_,_ in extra]:pose(name,t);return
    pose('TrampolineCarry',t);u=t*t*(3-2*t)
    if name=='RopePull':
        rotation('Hips',38*(1-u));rotation('Spine',28*(1-u)+8)
        rotation('Neck',-12);rotation('Head',-6)
        for side,sign in [('Left',1),('Right',-1)]:
            rotation(side+'UpperArm',0,sign*(68-18*u),-sign*(8+48*u))
            rotation(side+'LowerArm',0,0,-sign*(10+65*u))
            rotation(side+'UpperLeg',-75+53*u)
            rotation(side+'LowerLeg',115-82*u)
            rotation(side+'Foot',-35+25*u)
        bpy.context.view_layer.update()
        foot=min(r.pose.bones[n].head.z for n in ('LeftToes','RightToes'));movehips(.075-foot)
    elif name=='RopeLift':
        rotation('Hips',8*(1-u));rotation('Spine',8*sin(pi*u));rotation('Head',-12*(1-u))
        for side,sign in [('Left',1),('Right',-1)]:
            rotation(side+'UpperArm',0,sign*(-76+130*u),-sign*(8+48*u))
            rotation(side+'LowerArm',0,0,-sign*(10+55*sin(pi*u)))
            rotation(side+'UpperLeg',-45*(1-u));rotation(side+'LowerLeg',65*(1-u)+5);rotation(side+'Foot',-10*(1-u))
    elif name=='RopeHang':
        rotation('Hips',3,0,2*sin(2*pi*t));rotation('Spine',0);rotation('Head',-12)
        for side,sign in [('Left',1),('Right',-1)]:
            rotation(side+'UpperArm',0,-sign*78,-sign*7);rotation(side+'LowerArm',0,0,-sign*7)
            rotation(side+'UpperLeg',-10);rotation(side+'LowerLeg',22+sign*5*sin(2*pi*t))
    else:
        rotation('Hips',8);rotation('Spine',10)
        for side,sign in [('Left',1),('Right',-1)]:
            rotation(side+'UpperLeg',-28);rotation(side+'LowerLeg',48+sign*4*sin(2*pi*t));rotation(side+'Foot',-16)
    bpy.context.view_layer.update()

def variant(name,t,stage):
    extra_pose(name,t)
    if stage==0:return
    wave=sin(2*pi*t);alt=cos(2*pi*t)
    # Stage 1: right leg remains; torso counterbalances the absent left support.
    if stage==1:
        addrot('Hips',0,0,-5);addrot('Chest',0,0,4)
        if name in ('FlightLand','RopePull','TrampolineCarry'):
            # Keep the surviving foot under the centre of mass, with a deeper
            # single-knee absorption and shoulders counterbalancing the missing leg.
            load=sin(pi*t) if name!='TrampolineCarry' else .25+.1*wave
            addrot('RightUpperLeg',-14*load,0,7);addrot('RightLowerLeg',22*load)
            addrot('Hips',0,0,-4);addrot('Chest',0,0,3)
        elif name in ('PanicFall','TrampolineRise','CarryJump'):
            addrot('RightUpperLeg',-8*wave);addrot('RightLowerLeg',12*alt)
    # Stages 2/3 have no leg support: lower the torso and use a seated/arm-braced recovery.
    if stage>=2:
        ground=name in ('FlightLand','TrampolineCarry','RopePull')
        if not ground:movehips(-.48)
        if ground:
            # A seated pelvis is the support, not dangling fingertips. The arms
            # absorb contact or pull while the trunk leans against that support.
            load=(.2*(1-cos(2*pi*t)) if name=='TrampolineCarry' else
                  sin(pi*min(1,t/.65)) if name=='FlightLand' else sin(pi*t))
            pitch=(8+2*wave if name=='TrampolineCarry' else
                   8+18*load if name=='FlightLand' else 22-32*(t*t*(3-2*t)))
            rotation('Hips',pitch,0,(-7 if stage==3 else 0))
            rotation('Spine',7+10*load,0,(5 if stage==3 else 0))
            rotation('Neck',-8-5*load);rotation('Head',-6)
            if name=='FlightLand':
                for side,sign in [('Left',1),('Right',-1)]:
                    rotation(side+'UpperArm',0,sign*(46-12*load),-sign*28)
                    rotation(side+'LowerArm',0,0,-sign*(65+18*load))
                    rotation(side+'Hand',-38,0,0)
            elif name=='TrampolineCarry':
                for side,sign in [('Left',1),('Right',-1)]:
                    rotation(side+'UpperArm',0,sign*38,-sign*38)
                    rotation(side+'LowerArm',0,0,-sign*78)
            else:
                u=t*t*(3-2*t)
                for side,sign in [('Left',1),('Right',-1)]:
                    rotation(side+'UpperArm',0,sign*(35+18*u),-sign*(25+25*u))
                    rotation(side+'LowerArm',0,0,-sign*(55+45*u))
        elif name in ('PanicFall','FlightApex'):
            addrot('Hips',-10,6*wave,5*alt);addrot('Spine',8)
    if stage==3:
        addrot('Chest',0,-8+8*wave,-9)
        addrot('RightShoulder',0,0,-8)
        if name in ('PanicFall','TrampolineRise','FlightApex'):
            addrot('RightUpperArm',10*alt,-14*wave,8*alt);addrot('RightLowerArm',0,0,12*wave)
        if name in ('RopePull','RopeLift','RopeHang','TrampolineCarry'):
            addrot('Spine',3,5*(wave if name=='TrampolineCarry' else sin(pi*t)),-6)
        if name in ('FlightLand','RopePull','TrampolineCarry'):
            # One supporting/pulling arm: stagger the shoulder and rotate the
            # trunk toward it, instead of playing a mirrored two-arm effort.
            effort=wave if name=='TrampolineCarry' else sin(pi*t)
            addrot('Hips',0,-7,3*effort);addrot('Chest',0,-10*effort,0)
            addrot('RightLowerArm',0,0,12*effort)
    bpy.context.view_layer.update()
    if stage>=2 and name in ('FlightLand','TrampolineCarry','RopePull'):
        # No standing leg chain: place the pelvis above the stump support instead of
        # subtracting leg length again from the already crouched base landing/pull.
        support=.23-.025*sin(pi*t) if name!='TrampolineCarry' else .23
        movehips(support-r.pose.bones['Hips'].head.z)
    if stage==1 and name in ('FlightLand','RopePull','TrampolineCarry'):
        movehips(.075-r.pose.bones['RightToes'].head.z)
    bpy.context.view_layer.update()

manifest=[]
for name,length,loop in all_specs:
    for stage in range(4):
        clip=name+('_Cut'+str(stage) if stage else '')
        manifest.append(dict(name=clip,frames=length+1,fps=30,loop=loop,stage=stage))
        if 'motion_filter' in globals() and clip not in motion_filter:continue
        if stage==0 and name not in [n for n,_,_ in extra]:continue
        a=bpy.data.actions.new('CC_'+clip);a.use_fake_user=True;r.animation_data.action=a
        for frame in range(1,length+2,2):
            variant(name,(frame-1)/length,stage)
            for b in r.pose.bones:
                b.keyframe_insert('rotation_quaternion',frame=frame,group=b.name);b.keyframe_insert('location',frame=frame,group=b.name)
        for layer in a.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        for k in curve.keyframe_points:k.interpolation='BEZIER';k.handle_left_type='AUTO_CLAMPED';k.handle_right_type='AUTO_CLAMPED'
        s.frame_start=1;s.frame_end=length+1;s.frame_set(1)
        bpy.ops.object.select_all(action='DESELECT');r.select_set(True);marker.hide_set(False);marker.select_set(True);bpy.context.view_layer.objects.active=r
        bpy.ops.export_scene.fbx(filepath=OUT+'/'+clip+'.fbx',use_selection=True,object_types={'ARMATURE','MESH'},use_armature_deform_only=True,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',apply_unit_scale=True,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
        print('AUTHORED',clip)
with open(OUT+'/Source~/clip-manifest.json','w') as f:json.dump(manifest,f,indent=2)
print('ALL_CLIPS',len(manifest))
