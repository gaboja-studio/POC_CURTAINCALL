"""Static pose reference ONLY. Run against a fresh copy of original construction blend."""
import bpy, math, os
from mathutils import Vector, Matrix
ROOT = 'D:/github/POC_CURTAINCALL'
OUT = ROOT+'/Tasks/Active/Task-20261030-084-cannon-barrel-state-animation/Previews'
os.makedirs(OUT, exist_ok=True)
source=bpy.data.objects['CC_Cannon']
bpy.context.view_layer.update()
barrel_names=('EnamelStripe','DeepBoreLining','RearCap','RearBrassBand','MuzzleBrassBand','IvoryPaddedMuzzle','BandRivet','LipStitchSeam','BarrelStar')
parts=[(o,source.matrix_world.inverted()@o.matrix_world, o.name.startswith(barrel_names)) for o in source.children if o.type=='MESH']
scene=bpy.data.scenes.new('Cannon_Reference_Static_Poses')
bpy.context.window.scene=scene
for im in bpy.data.images:
    if im.name.startswith('CC_PropAtlas'): im.filepath=ROOT+'/Assets/Resources/Models/Objects/HumanCannonProps/Textures/CC_PropAtlas.png'; im.reload()
view=Vector((7,4.3,3.0)).normalized()
q=(-view).to_track_quat('-Z','Y'); right=q@Vector((1,0,0)); up=q@Vector((0,1,0))
pivot=Vector((0,.12,1.04))
ink=bpy.data.materials.new('Reference type'); ink.diffuse_color=(.85,.73,.49,1); ink.use_nodes=True
bs=next(n for n in ink.node_tree.nodes if n.type=='BSDF_PRINCIPLED'); bs.inputs['Base Color'].default_value=(.85,.73,.49,1);bs.inputs['Emission Color'].default_value=(.85,.73,.49,1);bs.inputs['Emission Strength'].default_value=.6
def label(text,x,y,size=.2):
    cu=bpy.data.curves.new(text,'FONT');cu.body=text;cu.size=size;cu.align_x='CENTER'
    ob=bpy.data.objects.new(text,cu);scene.collection.objects.link(ob);ob.location=right*x+up*y+view*2;ob.rotation_euler=q.to_euler();cu.materials.append(ink)
def pose(name,angle,x,y,recoil=0,roll=0):
    offset=right*x+up*y-up*.9
    rot=Matrix.Rotation(math.radians(angle),4,'X')@Matrix.Rotation(math.radians(roll),4,'Y')
    recoil_vector=Vector((0,math.cos(math.radians(25)),math.sin(math.radians(25))))*recoil
    motion=Matrix.Translation(pivot+recoil_vector)@rot@Matrix.Translation(-pivot)
    for o,m,isbarrel in parts:
        c=o.copy();c.parent=None;scene.collection.objects.link(c);c.matrix_world=Matrix.Translation(offset)@(motion@m if isbarrel else m)
    label(name,x,y-1.45)
label('CIRCUS CANNON  /  BARREL MOTION STUDY',0,6.45,.42)
pose('INACTIVE  /  -32 degrees',-32,-4.8,4.2)
pose('ACTIVE  /  original pose',0,4.8,4.2)
label('FIRE -> RATTLE -> LOWER   /   1.2 sec',0,2.35,.27)
for i,(name,a,r,roll) in enumerate([('1  RELEASE',0,0,0),('2  RECOIL',4,-.14,1.2),('3  RATTLE',-3,.025,-1),('4  LOWER',-22,0,0),('5  REST',-32,0,0)]):pose(name,a,(i-2)*4,0.8,r,roll)
label('RECOVER   /   FOUR SMALL RATCHETING NODS   /   2.8 sec',0,-1.1,.27)
for i,(name,a) in enumerate([('REST',-32),('1',-24),('2',-16),('3',-8),('4',0),('READY',0)]):pose(name,a,(i-2.5)*3.5,-2.9)
label('Fixed wheels and carriage  /  rotate only around brass side axle  /  slight settle between each lift',0,-5.4,.23)
world=bpy.data.worlds.new('Reference dark studio');world.use_nodes=True;scene.world=world
bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs[0].default_value=(.025,.032,.043,1);bg.inputs[1].default_value=.5
for name,loc,energy,size in [('Key',view*10+up*9-right*5,3500,12),('Fill',view*5+right*10,2200,10),('Rim',-view*3+up*5,3000,10)]:
    l=bpy.data.lights.new(name,'AREA');l.energy=energy;l.shape='DISK';l.size=size
    ob=bpy.data.objects.new(name,l);scene.collection.objects.link(ob);ob.location=loc;ob.rotation_euler=(-loc).to_track_quat('-Z','Y').to_euler()
cam=bpy.data.objects.new('ReferenceCamera',bpy.data.cameras.new('ReferenceCamera'));scene.collection.objects.link(cam);cam.location=view*35+up*.3;cam.rotation_euler=q.to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=23;scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=24
scene.render.resolution_x=2400;scene.render.resolution_y=1450;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=OUT+'/cannon-state-reference.png'
bpy.ops.render.render(write_still=True)
print('REFERENCE_RENDERED',scene.render.filepath)
