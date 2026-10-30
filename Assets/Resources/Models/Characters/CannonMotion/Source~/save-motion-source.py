# Run in a separate Blender background process. Packages original animation data only.
import bpy,math,json
from mathutils import Vector
ROOT=r'D:/github/POC_CURTAINCALL/Assets/Resources/Models/Characters/CannonMotion'
bpy.ops.wm.read_factory_settings(use_empty=True)
s=bpy.context.scene;s.name='CannonMotion';s.render.fps=30
names=[c['name'] for c in json.load(open(ROOT+'/Source~/clip-manifest.json'))]
base=None; actions={}
for name in names:
    before=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=ROOT+'/'+name+'.fbx')
    imported=set(bpy.data.objects)-before
    r=next(o for o in imported if o.type=='ARMATURE')
    action=r.animation_data.action;action.name='CC_'+name;action.use_fake_user=True;actions[name]=action
    if base is None:base=r
    else:
        for o in imported:bpy.data.objects.remove(o,do_unlink=True)
base.name='RabbitRig_v14';base['export_axis']='-Z forward, Y up; FBX_SCALE_UNITS'
base['clips']=' / '.join(names)
# A simple, newly authored weighted mannequin makes the editable source readable without private art.
verts=[];faces=[];groups=[];material_indices=[]
profiles={'Hips':.15,'Spine':.14,'Chest':.15,'UpperChest':.16,'Neck':.06,'Head':.17,
'LeftUpperArm':.055,'RightUpperArm':.055,'LeftLowerArm':.048,'RightLowerArm':.048,
'LeftHand':.055,'RightHand':.055,'LeftUpperLeg':.085,'RightUpperLeg':.085,
'LeftLowerLeg':.065,'RightLowerLeg':.065,'LeftFoot':.08,'RightFoot':.08}
for name,radius in profiles.items():
    b=base.data.bones[name];center=(b.head_local+b.tail_local)*.5;direction=(b.tail_local-b.head_local).normalized()
    u=direction.cross(Vector((0,0,1)))
    if u.length<.01:u=direction.cross(Vector((1,0,0)))
    u.normalize();v=direction.cross(u).normalized();half=(b.tail_local-b.head_local).length*.55
    start=len(verts)
    for j in range(9):
        theta=math.pi*j/8
        for k in range(12):
            angle=2*math.pi*k/12
            p=center+direction*(half*math.cos(theta))+(u*math.cos(angle)+v*math.sin(angle))*(radius*math.sin(theta))
            verts.append(tuple(p));groups.append(name)
    for j in range(8):
        for k in range(12):
            a=start+j*12+k;z=start+j*12+(k+1)%12
            faces.append((a,z,z+12,a+12));material_indices.append(0 if name in ('Head','Neck') or 'Hand' in name else 1 if 'Leg' in name or 'Foot' in name else 2)
mesh=bpy.data.meshes.new('MotionStudyMannequin');mesh.from_pydata(verts,[],faces);mesh.update()
proxy=bpy.data.objects.new('MotionStudyMannequin',mesh);s.collection.objects.link(proxy);proxy.parent=base
for name in profiles:
    g=proxy.vertex_groups.new(name=name);g.add([i for i,n in enumerate(groups) if n==name],1,'REPLACE')
for label,color in [('Ivory',(.8,.74,.6,1)),('Burgundy',(.35,.045,.055,1)),('Navy',(.045,.13,.22,1))]:
    m=bpy.data.materials.new(label);m.diffuse_color=color;mesh.materials.append(m)
for p,idx in zip(mesh.polygons,material_indices):p.material_index=idx;p.use_smooth=True
mod=proxy.modifiers.new('Motion Rig','ARMATURE');mod.object=base
for o in s.objects:
    if o.name.startswith('MotionBindMarker'):o.hide_set(True);o.hide_render=True
base.animation_data.action=actions['PanicFall'];base.animation_data.action_slot=actions['PanicFall'].slots[0]
s.frame_start=1;s.frame_end=37;s.frame_set(7)
bpy.ops.object.select_all(action='DESELECT');base.select_set(True);bpy.context.view_layer.objects.active=base;base.show_in_front=True
text=bpy.data.texts.new('README');text.write('44 original humanoid actions in four survival stages at 30fps. Choose CC_ actions in Action Editor. Mannequin is newly authored study geometry. Runtime uses the real project characters. Export only rig + MotionBindMarker; retain rig name RabbitRig_v14, -Z forward/Y up, FBX_SCALE_UNITS. Do not export the mannequin. See build-motion.py and clip-manifest.json.\n')
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/Source~/cannon-motion.blend')
print('SOURCE_SAVED',len(actions),'actions',len(verts),'proxy vertices')

