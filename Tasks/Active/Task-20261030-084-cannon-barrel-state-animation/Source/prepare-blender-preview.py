"""Make the authored motion blend open ready to scrub the recovery take."""
import bpy
from mathutils import Vector
scene=bpy.data.scenes['CC_CannonBarrelMotion'];bpy.context.window.scene=scene
pivot=scene.objects['BarrelPivot']
for track in pivot.animation_data.nla_tracks:track.mute=True
pivot.animation_data.action=bpy.data.actions['CC_Cannon_Recover']
scene.frame_start=1;scene.frame_end=169;scene.frame_set(1)
world=bpy.data.worlds.new('Cannon Studio');world.use_nodes=True;scene.world=world
bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs[0].default_value=(.045,.055,.075,1);bg.inputs[1].default_value=.4
for name,position,energy,size in [('Key',(4,4,7),1100,5),('Fill',(-4,2,4),800,5),('Rim',(0,-4,5),900,4)]:
    light=bpy.data.lights.new(name,'AREA');light.energy=energy;light.size=size
    ob=bpy.data.objects.new(name,light);scene.collection.objects.link(ob);ob.location=position;ob.rotation_euler=(Vector((0,0,1))-ob.location).to_track_quat('-Z','Y').to_euler()
camera=bpy.data.objects.new('MotionCamera',bpy.data.cameras.new('MotionCamera'));scene.collection.objects.link(camera)
camera.location=(5,6,3.5);camera.rotation_euler=(Vector((0,.2,1.2))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=4.7;scene.camera=camera
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.resolution_percentage=100
bpy.ops.object.select_all(action='DESELECT');pivot.select_set(True);bpy.context.view_layer.objects.active=pivot
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_location=(0,.2,1.1);area.spaces.active.region_3d.view_distance=5
            area.spaces.active.region_3d.view_rotation=camera.rotation_euler.to_quaternion()
bpy.ops.wm.save_as_mainfile(filepath='D:/github/POC_CURTAINCALL/Assets/Resources/Models/Objects/HumanCannonProps/Source~/cannon-barrel-motion.blend')
print('READY_TO_SCRUB',pivot.animation_data.action.name)
