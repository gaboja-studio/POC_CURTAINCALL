"""Original construction -> separate static chassis / animated rigid barrel; 4 NLA takes."""
import bpy, math, os, json
from mathutils import Vector
ROOT='D:/github/POC_CURTAINCALL/Assets/Resources/Models/Objects/HumanCannonProps'
source=bpy.data.objects['CC_Cannon'];bpy.context.view_layer.update()
parts=[(o,source.matrix_world.inverted()@o.matrix_world) for o in source.children if o.type=='MESH']
scene=bpy.data.scenes.new('CC_CannonBarrelMotion');bpy.context.window.scene=scene
scene.render.fps=60;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
root=bpy.data.objects.new('CC_CannonMotion',None);scene.collection.objects.link(root)
with bpy.data.libraries.load(ROOT+'/Source~/human-cannon-props-game.blend',link=False) as (available, loaded):
    loaded.materials=['CC_PropSurface']
surface=loaded.materials[0]
pivot=bpy.data.objects.new('BarrelPivot',None);scene.collection.objects.link(pivot);pivot.parent=root;pivot.location=(0,.12,1.04)
barrel_names=('EnamelStripe','DeepBoreLining','RearCap','RearBrassBand','MuzzleBrassBand','IvoryPaddedMuzzle','BandRivet','LipStitchSeam','BarrelStar')
for group in ('Chassis','Barrel'):
    clones=[]
    for obj,mat in parts:
        if obj.name.startswith(barrel_names)!=(group=='Barrel'):continue
        c=obj.copy();c.data=obj.data.copy();c.parent=None;scene.collection.objects.link(c);c.matrix_world=mat
        c.data.uv_layers.active.name='UVMap';clones.append(c)
    bpy.ops.object.select_all(action='DESELECT')
    for c in clones:c.select_set(True)
    bpy.context.view_layer.objects.active=clones[0];bpy.ops.object.join();obj=bpy.context.object;obj.name=group
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    tri=obj.modifiers.new('Triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
    obj.data.materials.clear();obj.data.materials.append(surface)
    for polygon in obj.data.polygons:polygon.material_index=0
    if group=='Barrel':
        for v in obj.data.vertices:v.co-=pivot.location
        obj.parent=pivot;obj.location=(0,0,0)
    else:obj.parent=root
    obj.data.name='CC_Cannon_'+group
home=pivot.location.copy();direction=Vector((0,math.cos(math.radians(25)),math.sin(math.radians(25))))
fire=[(0,0,0,0),(.055,4,-.14,1.25),(.13,-3,.025,-1),(.22,2,-.055,.65),(.31,-1,.012,-.35),(.40,-4,0,.1),(.72,-23,0,0),(.94,-34,0,0),(1.07,-31,0,0),(1.2,-32,0,0)]
recover=[(0,-32,0,0),(.14,-32,0,0)]
for i in range(4):
    start=i*.66;target=-32+(i+1)*8
    recover += [(start+.15,target-8,0,0),(start+.39,target+1.4,0,.18 if i%2==0 else -.18),(start+.52,target-.45,0,0),(start+.61,target,0,0)]
recover += [(2.8,0,0,0)]
takes={'Active':[(0,0,0,0),(1,0,0,0)],'Inactive':[(0,-32,0,0),(1,-32,0,0)],'FireDisable':fire,'Recover':recover}
manifest={}
for name,keys in takes.items():
    pivot.animation_data_create();action=bpy.data.actions.new('CC_Cannon_'+name);pivot.animation_data.action=action
    for t,angle,recoil,roll in sorted(keys):
        pivot.rotation_euler=(math.radians(angle),math.radians(roll),0);pivot.location=home+direction*recoil
        pivot.keyframe_insert(data_path='rotation_euler',frame=1+t*60);pivot.keyframe_insert(data_path='location',frame=1+t*60)
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in bag.fcurves:
                    for k in fc.keyframe_points:k.interpolation='BEZIER';k.handle_left_type='AUTO_CLAMPED';k.handle_right_type='AUTO_CLAMPED'
    action.use_fake_user=True
    track=pivot.animation_data.nla_tracks.new();track.name=action.name
    strip=track.strips.new(action.name,1,action);strip.name=action.name
    manifest[name]={'duration':max(k[0] for k in keys),'keys':keys}
pivot.animation_data.action=None
for track in pivot.animation_data.nla_tracks:track.mute=True
pivot.location=home;pivot.rotation_euler=(0,0,0)
scene.frame_start=1;scene.frame_end=169
for im in bpy.data.images:
    if im.name.startswith('CC_PropAtlas'):im.filepath=ROOT+'/Textures/CC_PropAtlas.png';im.reload();im.pack()
    elif im.name.startswith('CC_PropMetalSmooth'):im.filepath=ROOT+'/Textures/CC_PropMetalSmooth.png';im.reload();im.pack()
# Save only authored scene and its dependencies, keeping construction source unchanged.
bpy.data.libraries.write(ROOT+'/Source~/cannon-barrel-motion.blend',{scene},fake_user=True)
for track in pivot.animation_data.nla_tracks:track.mute=False
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=ROOT+'/Models/CC_CannonMotion.fbx',use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=0,add_leaf_bones=False,path_mode='STRIP')
json.dump(manifest,open(ROOT+'/Source~/barrel-clips.json','w'),indent=2)
print('EXPORTED',[(o.name,o.type) for o in scene.objects],list(manifest))
